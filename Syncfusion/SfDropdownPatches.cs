// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using Syncfusion.Maui.Core;
using KeyboardKey = Syncfusion.Maui.Core.Internals.KeyboardKey;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// The drop-downs of SfComboBox and SfAutocomplete (Syncfusion's internal
/// <c>SfDropdownView</c>, a child of every <c>SfDropdownEntry</c>). The native
/// builds show the drop-down from <c>SfDropdownViewHandler.ShowPopup</c>, a
/// native popup anchored to the control; the platform-neutral build Linux apps
/// get has that handler with a throwing platform view and empty methods, and
/// OpenMaui resolves the view as a plain ContentView, so the list never showed.
/// <c>SfDropdownView.ShowPopup</c> / <c>HidePopup</c> (which the control calls
/// whenever the drop-down opens or closes, and which raise the control's
/// opened/closed events) are patched to show the content in a
/// <see cref="SkiaSfDropdownPopup"/> overlay. While it is open, a press
/// anywhere outside the control and its drop-down closes it (the native
/// builds' root pointer handler), and Up, Down, Enter, Escape and Tab typed in
/// the control reach its keyboard handling (highlight, pick, close), which
/// the native builds feed from the platform's key events.
/// </summary>
internal static class SfDropdownPatches
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private static readonly ConditionalWeakTable<ContentView, SfDropdownController> s_controllers = new();
    private static readonly List<SfDropdownController> s_open = new();
    private static int s_installed;

    internal static BindableProperty? IsOpenProperty { get; private set; }
    internal static BindableProperty? AnchorViewProperty { get; private set; }
    internal static BindableProperty? PopupHeightProperty { get; private set; }
    internal static BindableProperty? PopupWidthProperty { get; private set; }
    internal static BindableProperty? PopupXProperty { get; private set; }
    internal static BindableProperty? PopupYProperty { get; private set; }
    internal static BindableProperty? PlacementProperty { get; private set; }
    internal static BindableProperty? BorderColorProperty { get; private set; }
    internal static BindableProperty? CornerRadiusProperty { get; private set; }
    internal static BindableProperty? ShadowProperty { get; private set; }
    internal static FieldInfo? PopupContentField { get; private set; }
    internal static BindableProperty? DropdownContentProperty { get; private set; }
    internal static MethodInfo? RaisePopupClosing { get; private set; }
    internal static FieldInfo? DropDownTappedField { get; private set; }
    internal static FieldInfo? TextInputLayoutEntryField { get; private set; }
    internal static FieldInfo? DownOrUpClickField { get; private set; }

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        try
        {
            var type = typeof(SfDropdownEntry).Assembly.GetType("Syncfusion.Maui.Core.SfDropdownView");
            var show = type?.GetMethod("ShowPopup", Any, null, Type.EmptyTypes, null);
            var hide = type?.GetMethod("HidePopup", Any, null, Type.EmptyTypes, null);
            IsOpenProperty = Property(type, "IsOpenProperty");
            AnchorViewProperty = Property(type, "AnchorViewProperty");
            if (type == null || show == null || hide == null || IsOpenProperty == null || AnchorViewProperty == null)
            {
                DiagnosticLog.Warn("Syncfusion",
                    "This Syncfusion.Maui.Core release lacks the SfDropdownView members the Linux bridge drives; SfComboBox and SfAutocomplete drop-downs do not open.");
                return;
            }

            PopupHeightProperty = Property(type, "PopupHeightProperty");
            PopupWidthProperty = Property(type, "PopupWidthProperty");
            PopupXProperty = Property(type, "PopupXProperty");
            PopupYProperty = Property(type, "PopupYProperty");
            PlacementProperty = Property(type, "DropDownPlacementProperty");
            BorderColorProperty = Property(type, "BorderColorProperty");
            CornerRadiusProperty = Property(type, "DropDownCornerRadiusProperty");
            ShadowProperty = Property(type, "IsDropDownShadowVisibleProperty");
            PopupContentField = type.GetField("popupListView", Any);
            DropdownContentProperty = Property(typeof(SfDropdownEntry), "DropdownContentProperty");
            RaisePopupClosing = typeof(SfDropdownEntry).GetMethod("RaisePopupClosingEvent", Any, null, [typeof(bool)], null);
            DropDownTappedField = typeof(SfDropdownEntry).GetField("isDropDownTapped", Any);
            TextInputLayoutEntryField = typeof(SfTextInputLayout).GetField("dropdownEntry", Any);
            DownOrUpClickField = typeof(SfDropdownEntry).GetField("IsDownOrUpButtonClickEnabled", Any);

            WindowContext.TabNavigating += OnTabNavigating;

            var harmony = new Harmony("com.openmaui.syncfusion.dropdown");
            harmony.Patch(show, postfix: new HarmonyMethod(typeof(SfDropdownPatches).GetMethod(nameof(ShowPopup_Postfix), BindingFlags.Static | BindingFlags.NonPublic)));
            harmony.Patch(hide, postfix: new HarmonyMethod(typeof(SfDropdownPatches).GetMethod(nameof(HidePopup_Postfix), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Patching Syncfusion's drop-down popup failed", ex);
        }
    }

    private static BindableProperty? Property(Type? type, string name) =>
        type?.GetField(name, Any)?.GetValue(null) as BindableProperty;

    // Runs after SfDropdownView.ShowPopup, which raised PopupOpened (the control
    // is now open) unless a DropDownOpening handler cancelled it.
    private static void ShowPopup_Postfix(object __instance)
    {
        if (__instance is not ContentView view)
            return;
        try
        {
            var controller = s_controllers.GetValue(view, v => new SfDropdownController(v));
            if (controller.ShouldShow())
            {
                controller.Open();
                if (controller.Popup.IsShown && !s_open.Contains(controller))
                    s_open.Add(controller);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Opening a drop-down failed", ex);
        }
    }

    private static void HidePopup_Postfix(object __instance)
    {
        if (__instance is not ContentView view || !s_controllers.TryGetValue(view, out var controller))
            return;
        Closed(controller);
    }

    /// <summary>
    /// Tab is about to move focus out of a control whose drop-down is open:
    /// the control hears the Tab first and closes its drop-down, as WinUI's
    /// controls see the key before focus moves.
    /// </summary>
    private static void OnTabNavigating(WindowContext window, SkiaView? focused)
    {
        if (focused?.MauiView is not Microsoft.Maui.Controls.View view)
            return;
        foreach (var controller in s_open.ToArray())
        {
            if (controller.Popup.IsShown && controller.Contains(view))
                controller.CloseByTab();
        }
    }

    internal static void Closed(SfDropdownController controller)
    {
        s_open.Remove(controller);
        try
        {
            controller.Detach();
            controller.Popup.Hide();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Closing a drop-down failed", ex);
        }
    }
}

/// <summary>Shows and hides one <c>SfDropdownView</c>'s popup.</summary>
internal sealed class SfDropdownController
{
    private readonly WeakReference<ContentView> _view;
    private WindowContext? _window;

    internal SfDropdownController(ContentView view)
    {
        _view = new WeakReference<ContentView>(view);
        Popup = new SkiaSfDropdownPopup(this);
    }

    internal SkiaSfDropdownPopup Popup { get; }

    private ContentView? View => _view.TryGetTarget(out var view) ? view : null;

    /// <summary>The control the drop-down belongs to (the SfComboBox, or the SfTextInputLayout around it).</summary>
    internal Microsoft.Maui.Controls.View? Anchor =>
        View is { } view && SfDropdownPatches.AnchorViewProperty is { } p ? view.GetValue(p) as Microsoft.Maui.Controls.View : null;

    internal SkiaView? AnchorPlatformView => Anchor?.Handler?.PlatformView as SkiaView;

    /// <summary>The control whose IsDropDownOpen the drop-down reflects.</summary>
    private SfDropdownEntry? Entry => Anchor switch
    {
        SfDropdownEntry entry => entry,
        SfTextInputLayout layout => SfDropdownPatches.TextInputLayoutEntryField?.GetValue(layout) as SfDropdownEntry
            ?? layout.Content as SfDropdownEntry,
        _ => null,
    };

    private Microsoft.Maui.Controls.View? PopupContent
    {
        get
        {
            if (View is { } view && SfDropdownPatches.PopupContentField?.GetValue(view) is Microsoft.Maui.Controls.View content)
                return content;
            return Entry is { } entry && SfDropdownPatches.DropdownContentProperty is { } p
                ? entry.GetValue(p) as Microsoft.Maui.Controls.View
                : null;
        }
    }

    internal bool ShouldShow()
    {
        if (View is not { } view || SfDropdownPatches.IsOpenProperty is not { } isOpen || view.GetValue(isOpen) is not true)
            return false;
        // A DropDownOpening handler that cancelled leaves the control closed.
        return Entry?.IsDropDownOpen ?? true;
    }

    internal void Open()
    {
        if (Anchor is not { } anchor || AnchorPlatformView is not { } anchorView || PopupContent is not { } content)
            return;
        var context = anchor.Handler?.MauiContext;
        if (context == null)
            return;
        if (content.Handler == null)
            content.Handler = content.ToViewHandler(context);
        if (content.Handler?.PlatformView is not SkiaView platformContent)
        {
            DiagnosticLog.Warn("Syncfusion", $"The drop-down content ({content.GetType().Name}) has no Skia view; the drop-down does not show.");
            return;
        }
        // The list is parented to the control; presses in the popup stop at
        // the popup's content, as they would in a native popup window.
        SkiaView.SetPointerBubbleBoundary(content, true);
        if (content is Layout layout)
        {
            foreach (var child in layout.Children.OfType<Element>())
            {
                if (!ReferenceEquals(child.Parent, content))
                    SkiaView.SetPointerBubbleBoundary(child, true);
            }
        }
        var root = RootOf(anchorView);
        Popup.Show(root, platformContent);
        if (Popup.IsShown)
            Attach(WindowOf(root));
    }

    private void Attach(WindowContext? window)
    {
        if (ReferenceEquals(window, _window))
            return;
        Detach();
        _window = window;
        if (window?.DisplayWindow is { } display)
        {
            display.PointerPressed += OnWindowPointerPressed;
            display.KeyDown += OnWindowKeyDown;
        }
    }

    internal void Detach()
    {
        if (_window?.DisplayWindow is { } display)
        {
            display.PointerPressed -= OnWindowPointerPressed;
            display.KeyDown -= OnWindowKeyDown;
        }
        _window = null;
    }

    /// <summary>
    /// A press in the window, after the window dispatched it: one outside the
    /// control and its drop-down closes the drop-down, and the view there
    /// still gets it, as on the other platforms. Runs inside the native input
    /// callback, so nothing may escape it.
    /// </summary>
    private void OnWindowPointerPressed(object? sender, PointerEventArgs e)
    {
        try
        {
            if (!Popup.IsShown || _window is not { } window)
                return;
            var (x, y) = window.ToLogicalPoint(e.X, e.Y);
            if (Popup.PopupRect.Contains(x, y) || (AnchorPlatformView is { } anchor && anchor.ScreenBounds.Contains(x, y)))
                return;
            CloseFromOutside();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Closing a drop-down on an outside press failed", ex);
        }
    }

    /// <summary>
    /// Keys typed in the control while its drop-down is open: the control's
    /// own keyboard handling moves the highlight (Up/Down), picks (Enter) and
    /// closes (Escape, Tab). The text field keeps every other key.
    /// </summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        KeyboardKey key = e.Key switch
        {
            Key.Up => KeyboardKey.Up,
            Key.Down => KeyboardKey.Down,
            Key.Enter or Key.NumPadEnter => KeyboardKey.Enter,
            Key.Escape => KeyboardKey.Escape,
            Key.Tab => KeyboardKey.Tab,
            _ => KeyboardKey.None,
        };
        if (key == KeyboardKey.None || !Popup.IsShown)
            return;
        try
        {
            // A non-editable combo box leaves nothing focused; keys then belong to it.
            if (Entry is not { } entry || _window is not { } window
                || (window.FocusedView is { } focused && (focused.MauiView is not { } view || !Contains(view))))
                return;
            if (key is KeyboardKey.Up or KeyboardKey.Down)
                MoveHighlight(entry, up: key == KeyboardKey.Up);
            else
                entry.OnKeyDown(new global::Syncfusion.Maui.Core.Internals.KeyEventArgs(key));
            SfInvalidation.InvalidateAll(drawingOnly: false);
            Popup.Invalidate();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Drop-down key {key} failed", ex);
        }
    }

    /// <summary>
    /// Up/Down in an open drop-down. The native builds move the list's
    /// selection with the platform list's own key navigation, then let the
    /// control mirror it (<c>OnDownOrUpButtonPressed</c>: the text field shows
    /// the highlighted item, Enter picks it); the platform-neutral build has no
    /// such navigation, so the selection is moved here.
    /// </summary>
    private static void MoveHighlight(SfDropdownEntry entry, bool up)
    {
        var type = entry.GetType();
        var listView = FindProperty(type, "ListView")?.GetValue(entry) as BindableObject;
        if (listView == null)
            return;
        var listType = listView.GetType();
        var selected = listType.GetProperty("SelectedItem");
        var current = listType.GetProperty("CurrentItem");
        if (listType.GetProperty("ItemsSource")?.GetValue(listView) is not System.Collections.IEnumerable source || selected == null)
            return;
        var items = source.Cast<object>().ToList();
        if (items.Count == 0)
            return;
        int index = items.IndexOf(selected.GetValue(listView)!);
        index = index < 0 ? (up ? items.Count - 1 : 0) : Math.Clamp(index + (up ? -1 : 1), 0, items.Count - 1);
        var item = items[index];
        selected.SetValue(listView, item);
        current?.SetValue(listView, item);
        var scrollTo = listType.GetMethods().FirstOrDefault(m => m.Name == "ScrollTo" && m.GetParameters() is { Length: 3 } p
            && p[0].ParameterType == typeof(object) && p[1].ParameterType.IsEnum);
        scrollTo?.Invoke(listView, [item, Enum.ToObject(scrollTo.GetParameters()[1].ParameterType, 0), true]);

        SfDropdownPatches.DownOrUpClickField?.SetValue(entry, true);
        FindMethod(type, "OnDownOrUpButtonPressed", typeof(bool))?.Invoke(entry, [up]);
    }

    private static PropertyInfo? FindProperty(Type? type, string name)
    {
        for (; type != null; type = type.BaseType)
        {
            if (type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) is { } property)
                return property;
        }
        return null;
    }

    private static MethodInfo? FindMethod(Type? type, string name, params Type[] parameters)
    {
        for (; type != null; type = type.BaseType)
        {
            if (type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, parameters, null) is { } method)
                return method;
        }
        return null;
    }

    private static WindowContext? WindowOf(SkiaView root)
    {
        if (LinuxApplication.Current is not { } app)
            return null;
        foreach (var context in app.WindowContexts)
        {
            if (ReferenceEquals(context.RootView, root) || context.ModalViews.Contains(root))
                return context;
        }
        return null;
    }

    /// <summary>
    /// True while the anchor is still shown in the live window the popup was
    /// opened in (not navigated away from, not in a closed window).
    /// </summary>
    internal bool IsAnchorAttached =>
        AnchorPlatformView is { IsVisible: true } anchorView && Popup.Parent is { } root
        && ReferenceEquals(RootOf(anchorView), root) && WindowOf(root) != null;

    internal void OnAnchorLost() => SfDropdownPatches.Closed(this);

    /// <summary>
    /// True when <paramref name="view"/> is the control, part of it, or part of
    /// its drop-down (the list is parented to the control, its header and
    /// footer to the drop-down's content).
    /// </summary>
    internal bool Contains(Microsoft.Maui.Controls.View view)
    {
        var anchor = Anchor;
        var content = PopupContent;
        for (Element? element = view; element != null; element = element.Parent)
        {
            if (ReferenceEquals(element, anchor) || ReferenceEquals(element, content) || ReferenceEquals(element, View))
                return true;
        }
        return false;
    }

    /// <summary>Tab in the control: its own keyboard handling closes the drop-down.</summary>
    internal void CloseByTab()
    {
        try
        {
            if (Entry is { } entry)
                entry.OnKeyDown(new global::Syncfusion.Maui.Core.Internals.KeyEventArgs(KeyboardKey.Tab));
            SfInvalidation.InvalidateAll(drawingOnly: false);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Closing a drop-down on Tab failed", ex);
        }
    }

    /// <summary>A press outside: close as the native builds do, honouring DropDownClosing.</summary>
    internal void CloseFromOutside()
    {
        try
        {
            if (Entry is { } entry)
            {
                if (!entry.IsDropDownOpen)
                {
                    SfDropdownPatches.Closed(this);
                    return;
                }
                if (SfDropdownPatches.RaisePopupClosing?.Invoke(entry, [false]) is false)
                    return;
                entry.IsDropDownOpen = false;
                SfDropdownPatches.DropDownTappedField?.SetValue(entry, false);
            }
            else if (View is { } view && SfDropdownPatches.IsOpenProperty is { } isOpen)
            {
                view.SetValue(isOpen, false);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Closing a drop-down failed", ex);
        }
        if (Popup.IsShown && !(Entry?.IsDropDownOpen ?? false))
            SfDropdownPatches.Closed(this);
    }

    internal SfDropdownSettings ReadSettings()
    {
        var view = View;
        T Read<T>(BindableProperty? property, T fallback) =>
            view != null && property != null && view.GetValue(property) is T value ? value : fallback;

        return new SfDropdownSettings(
            Width: Read(SfDropdownPatches.PopupWidthProperty, 0d),
            Height: Read(SfDropdownPatches.PopupHeightProperty, 0d),
            OffsetX: Read(SfDropdownPatches.PopupXProperty, 0),
            OffsetY: Read(SfDropdownPatches.PopupYProperty, 0),
            Placement: Read(SfDropdownPatches.PlacementProperty, DropDownPlacement.Auto),
            BorderColor: Read<Color?>(SfDropdownPatches.BorderColorProperty, Color.FromRgb(220, 220, 220)),
            CornerRadius: Read(SfDropdownPatches.CornerRadiusProperty, 4d),
            Shadow: Read(SfDropdownPatches.ShadowProperty, true));
    }

    private static SkiaView RootOf(SkiaView view)
    {
        var top = view;
        while (top.Parent != null)
            top = top.Parent;
        return top;
    }
}
