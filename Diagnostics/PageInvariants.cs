// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Diagnostics;

/// <summary>One broken rule on a rendered page.</summary>
internal sealed record PageInvariantViolation(string Rule, string View, string Detail)
{
    public override string ToString() => $"{Rule}: {Detail} ({View})";
}

/// <summary>
/// Rules every rendered MAUI page should satisfy, whatever the app, checked on the live view tree
/// so whole classes of parity bugs show up on any page rather than one test at a time:
/// <list type="bullet">
/// <item><b>type-name-text</b>: no text is a .NET type name (a View drawn through its ToString,
/// as a CollectionView header was).</item>
/// <item><b>draws-outside-bounds</b>: an image or video draws nothing outside its frame (AspectFill
/// pictures spilled over the views around them).</item>
/// <item><b>unreachable-control</b>: a visible, enabled control that takes input is what a click
/// at its centre reaches (controls inside CollectionView rows never got the pointer).</item>
/// <item><b>binding-failed</b>: no binding failed while the page was shown.</item>
/// <item><b>error-logged</b>: OpenMaui logged no error while the page was shown.</item>
/// </list>
/// Set <c>OPENMAUI_INVARIANTS=1</c> to check every window once it stops redrawing and print each
/// violation once per page to stderr as <c>[Invariant] rule: detail (view)</c>; tests call
/// <see cref="Check"/> directly.
/// </summary>
internal static class PageInvariants
{
    internal static readonly bool Enabled = Environment.GetEnvironmentVariable("OPENMAUI_INVARIANTS") == "1";

    private static readonly Regex s_typeName = new(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_`]*)+(\[.*\])?$", RegexOptions.Compiled);
    private static HashSet<string>? s_typeNames;
    private static readonly Dictionary<Type, PropertyInfo?> s_textProperty = new();

    private static readonly object s_gate = new();
    private static readonly List<string> s_bindingFailures = new();
    private static readonly List<string> s_errors = new();
    private static int s_installed;

    /// <summary>Starts collecting binding failures and logged errors (MAUI reports bindings only with its diagnostics on).</summary>
    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        AppContext.SetSwitch("Microsoft.Maui.RuntimeFeature.EnableMauiDiagnostics", true);
        Microsoft.Maui.Controls.Xaml.Diagnostics.BindingDiagnostics.BindingFailed += OnBindingFailed;
        DiagnosticLog.ErrorLogged += OnErrorLogged;
    }

    private static void OnBindingFailed(object? sender, Microsoft.Maui.Controls.Xaml.Diagnostics.BindingBaseErrorEventArgs e)
    {
        string message;
        try { message = string.Format(System.Globalization.CultureInfo.InvariantCulture, e.Message, e.MessageArgs ?? Array.Empty<object>()); }
        catch (FormatException) { message = e.Message; }
        lock (s_gate) s_bindingFailures.Add(message);
    }

    private static void OnErrorLogged(string tag, string message)
    {
        lock (s_gate) s_errors.Add($"[{tag}] {message}");
    }

    /// <summary>
    /// Checks the tree under <paramref name="root"/> (a window's top layer, laid out in a
    /// <paramref name="width"/> by <paramref name="height"/> window) and returns what is broken,
    /// including the binding failures and errors collected since the last check.
    /// </summary>
    internal static List<PageInvariantViolation> Check(SkiaView root, int width, int height)
    {
        var violations = new List<PageInvariantViolation>();
        var window = new Rect(0, 0, width, height);
        foreach (var view in Walk(root))
        {
            if (!view.IsVisible)
                continue;
            CheckText(view, violations);
            CheckDrawing(view, width, height, violations);
            CheckReachable(root, view, window, violations);
        }

        lock (s_gate)
        {
            foreach (var failure in s_bindingFailures)
                violations.Add(new PageInvariantViolation("binding-failed", "binding", failure));
            foreach (var error in s_errors)
                violations.Add(new PageInvariantViolation("error-logged", "log", error));
            s_bindingFailures.Clear();
            s_errors.Clear();
        }
        return violations;
    }

    private static IEnumerable<SkiaView> Walk(SkiaView root)
    {
        var stack = new Stack<SkiaView>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var view = stack.Pop();
            yield return view;
            foreach (var child in ChildrenOf(view))
                stack.Push(child);
        }
    }

    private static readonly Dictionary<Type, PropertyInfo?> s_contentProperty = new();

    /// <summary>
    /// A view's children: its child list, a single <c>Content</c> view (pages, scroll, refresh
    /// and swipe views, presenters), Shell's current page and flyout views, and the rows,
    /// header, footer and empty view an items view draws.
    /// </summary>
    private static IEnumerable<SkiaView> ChildrenOf(SkiaView view)
    {
        var seen = new HashSet<SkiaView>(ReferenceEqualityComparer.Instance);
        IEnumerable<SkiaView> children = view is SkiaLayoutView layout ? layout.Children : view.Children;
        foreach (var child in children)
            if (seen.Add(child))
                yield return child;

        PropertyInfo? content;
        lock (s_contentProperty)
        {
            if (!s_contentProperty.TryGetValue(view.GetType(), out content))
            {
                content = view.GetType().GetProperty("Content", BindingFlags.Public | BindingFlags.Instance);
                if (content == null || !typeof(SkiaView).IsAssignableFrom(content.PropertyType))
                    content = null;
                s_contentProperty[view.GetType()] = content;
            }
        }
        if (content?.GetValue(view) is SkiaView single && seen.Add(single))
            yield return single;

        if (view is SkiaShell shell)
        {
            foreach (var part in new[] { shell.CurrentContent, shell.FlyoutHeaderView, shell.FlyoutContentView, shell.FlyoutFooterView })
                if (part != null && seen.Add(part))
                    yield return part;
        }
        if (view is SkiaItemsView items)
        {
            for (int i = 0; i < 4096; i++)
            {
                var row = items.GetItemView(i);
                if (row == null && i > 256)
                    break;
                if (row != null && ReferenceEquals(row.Parent, view) && seen.Add(row))
                    yield return row;
            }
            if (items.EmptyViewContent is { } empty)
                yield return empty;
            if (items is SkiaCollectionView collection)
            {
                if (collection.HeaderView is { } header) yield return header;
                if (collection.FooterView is { } footer) yield return footer;
            }
        }
    }

    private static string Describe(SkiaView view)
    {
        var id = view.AutomationId;
        var maui = view.MauiView?.GetType().Name;
        return string.IsNullOrEmpty(id) ? $"{view.GetType().Name}{(maui != null ? " for " + maui : "")}" : $"{view.GetType().Name} '{id}'";
    }

    // --- type-name-text -------------------------------------------------------------------

    private static void CheckText(SkiaView view, List<PageInvariantViolation> violations)
    {
        PropertyInfo? property;
        lock (s_textProperty)
        {
            var type = view.GetType();
            if (!s_textProperty.TryGetValue(type, out property))
            {
                property = type.GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
                if (property?.PropertyType != typeof(string))
                    property = null;
                s_textProperty[type] = property;
            }
        }
        if (property?.GetValue(view) is string text && IsTypeName(text))
            violations.Add(new PageInvariantViolation("type-name-text", Describe(view), $"shows the type name \"{text}\" (an object drawn through ToString)"));
    }

    private static bool IsTypeName(string text)
    {
        if (text.Length < 5 || text.Length > 300 || !s_typeName.IsMatch(text))
            return false;
        var names = s_typeNames;
        if (names == null)
        {
            names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                        if (type.FullName != null)
                            names.Add(type.FullName);
                }
                catch (ReflectionTypeLoadException ex)
                {
                    foreach (var type in ex.Types)
                        if (type?.FullName != null)
                            names.Add(type.FullName);
                }
            }
            s_typeNames = names;
        }
        var bare = text.Contains('[') ? text[..text.IndexOf('[')] : text;
        return names.Contains(bare);
    }

    // --- draws-outside-bounds --------------------------------------------------------------

    private static bool DrawsPictures(SkiaView view) =>
        view is SkiaImage or SkiaImageButton || view.GetType().Name.Contains("MediaElement", StringComparison.Ordinal);

    private static void CheckDrawing(SkiaView view, int width, int height, List<PageInvariantViolation> violations)
    {
        if (!DrawsPictures(view) || view.Shadow != null)
            return;
        var bounds = view.Bounds;
        if (bounds.Width < 2 || bounds.Height < 2 || bounds.Left < 0 || bounds.Top < 0 || bounds.Right > width || bounds.Bottom > height)
            return;

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        if (surface == null)
            return;
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        view.Draw(canvas);
        canvas.Flush();
        using var pixmap = surface.PeekPixels();

        // One pixel of tolerance for antialiased edges.
        int left = (int)Math.Floor(bounds.Left) - 1, top = (int)Math.Floor(bounds.Top) - 1;
        int right = (int)Math.Ceiling(bounds.Right) + 1, bottom = (int)Math.Ceiling(bounds.Bottom) + 1;
        int outside = 0;
        for (int y = 0; y < height; y += 2)
        {
            for (int x = 0; x < width; x += 2)
            {
                if (x >= left && x < right && y >= top && y < bottom)
                    continue;
                if (pixmap.GetPixelColor(x, y).Alpha > 16)
                    outside++;
            }
        }
        if (outside > 0)
            violations.Add(new PageInvariantViolation("draws-outside-bounds", Describe(view),
                $"draws about {outside * 4} pixels outside its {bounds.Width:0}x{bounds.Height:0} frame"));
    }

    // --- unreachable-control ---------------------------------------------------------------

    private static bool TakesInput(SkiaView view) =>
        view.IsEnabled && (view.IsFocusable || SkiaLayoutView.HasTapRecognizer(view.MauiView));

    private static void CheckReachable(SkiaView root, SkiaView view, Rect window, List<PageInvariantViolation> violations)
    {
        if (ReferenceEquals(view, root) || !TakesInput(view) || view.InputTransparent || !view.IsVisibleInTree())
            return;
        var screen = view.ScreenBounds;
        if (screen.Width < 4 || screen.Height < 4)
            return;
        var center = new Point(screen.Center.X, screen.Center.Y);
        if (!window.Contains(center))
            return;
        // Only where every ancestor shows it (not scrolled or clipped out of a viewport).
        for (var parent = view.Parent; parent != null; parent = parent.Parent)
        {
            if (!parent.ScreenBounds.Contains(center))
                return;
        }

        var hit = root.HitTestAt((float)center.X, (float)center.Y);
        for (var v = hit; v != null; v = v.Parent)
        {
            if (ReferenceEquals(v, view))
                return; // the control, or something inside it
        }
        // A list dispatches taps on a row's own recognizer itself (item tap).
        if (hit is SkiaItemsView && ReferenceEquals(view.Parent, hit))
            return;
        violations.Add(new PageInvariantViolation("unreachable-control", Describe(view),
            $"a click at its centre ({center.X:0},{center.Y:0}) reaches {(hit == null ? "nothing" : Describe(hit))}"));
    }

    // --- running on live windows ----------------------------------------------------------

    private static readonly Dictionary<object, HashSet<string>> s_reported = new(ReferenceEqualityComparer.Instance);

    /// <summary>Checks a settled window and prints violations not printed for its current page.</summary>
    internal static void CheckWindow(object pageKey, SkiaView root, int width, int height)
    {
        List<PageInvariantViolation> violations;
        try
        {
            violations = Check(root, width, height);
        }
        catch (Exception ex)
        {
            System.Console.Error.WriteLine($"[Invariant] check failed: {ex.GetType().Name}: {ex.Message}");
            return;
        }
        if (!s_reported.TryGetValue(pageKey, out var seen))
        {
            s_reported[pageKey] = seen = new HashSet<string>(StringComparer.Ordinal);
            // A clean page says so: no output would not tell a clean page from an unchecked one.
            System.Console.Error.WriteLine($"[Invariant] checked {pageKey.GetType().Name}: {violations.Count} violation(s)");
        }
        foreach (var violation in violations)
        {
            if (seen.Add(violation.ToString()))
                System.Console.Error.WriteLine($"[Invariant] {violation}");
        }
    }
}
