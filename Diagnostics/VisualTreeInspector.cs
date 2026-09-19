// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform.Linux.Diagnostics;

/// <summary>
/// A developer-only "Live Visual Tree" inspector for the Skia view hierarchy.
///
/// It walks the live <see cref="SkiaView"/> tree from the application root,
/// produces a read-only <see cref="VisualTreeNode"/> snapshot, draws a debug
/// overlay highlighting a selected/hovered node, and offers a "pick" mode that
/// selects the element under the cursor via the existing hit-test path.
///
/// Overlay hook: the inspector reuses the engine's existing static popup-overlay
/// registry (<see cref="SkiaView.RegisterPopupOverlay"/>) rather than touching the
/// rendering engine. A dedicated owner view whose <c>HitTestPopupArea</c> returns
/// false carries the draw callback, so the overlay paints on top of every frame
/// without ever intercepting pointer routing.
///
/// All activation is programmatic (<see cref="Enable"/>/<see cref="Disable"/>/
/// <see cref="Toggle"/>); a default toggle hotkey can be opted into via
/// <see cref="BindDefaultHotkey"/>. Input handling entered from the application's
/// pointer/key handlers is exception-guarded so a walk error can never crash the app.
/// </summary>
public sealed class VisualTreeInspector
{
    /// <summary>The process-wide inspector instance.</summary>
    public static VisualTreeInspector Instance { get; } = new();

    private VisualTreeInspector() { }

    // Guard against pathological/cyclic trees when walking or dumping.
    private const int MaxDepth = 512;

    private InspectorOverlayView? _overlay;
    private readonly ConcurrentDictionary<Type, PropertyInfo?> _textProps = new();

    /// <summary>Whether the overlay is currently active.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Whether pick mode is active: the next pointer press selects the element
    /// under the cursor and the hovered node is highlighted while moving.
    /// </summary>
    public bool PickModeActive { get; private set; }

    /// <summary>
    /// When true the overlay also draws a dim outline around every node in the
    /// tree, not just the selected/hovered one.
    /// </summary>
    public bool ShowAllOutlines { get; set; }

    /// <summary>The node currently selected (committed) for highlighting.</summary>
    public SkiaView? SelectedNode { get; private set; }

    /// <summary>The node under the cursor while pick mode is active.</summary>
    public SkiaView? HoveredNode { get; private set; }

    private static SkiaView? Root => LinuxApplication.Current?.RootView;

    #region Activation

    /// <summary>Enables the overlay (idempotent).</summary>
    public void Enable()
    {
        if (IsEnabled)
            return;

        IsEnabled = true;
        _overlay ??= new InspectorOverlayView();
        SkiaView.RegisterPopupOverlay(_overlay, DrawOverlay);
        RequestRepaint();
    }

    /// <summary>Disables the overlay and clears pick/selection state (idempotent).</summary>
    public void Disable()
    {
        if (!IsEnabled)
            return;

        IsEnabled = false;
        PickModeActive = false;
        HoveredNode = null;
        if (_overlay != null)
            SkiaView.UnregisterPopupOverlay(_overlay);
        RequestRepaint();
    }

    /// <summary>Toggles the overlay on/off.</summary>
    public void Toggle()
    {
        if (IsEnabled)
            Disable();
        else
            Enable();
    }

    /// <summary>
    /// Enables the overlay (if needed) and enters pick mode. Point at the window
    /// and click to select the element under the cursor.
    /// </summary>
    public void EnablePickMode()
    {
        Enable();
        PickModeActive = true;
        RequestRepaint();
    }

    /// <summary>Exits pick mode without disabling the overlay.</summary>
    public void DisablePickMode()
    {
        if (!PickModeActive)
            return;
        PickModeActive = false;
        HoveredNode = null;
        RequestRepaint();
    }

    /// <summary>Selects a node explicitly (e.g. from a snapshot's Source).</summary>
    public void SelectNode(SkiaView? node)
    {
        SelectedNode = node;
        RequestRepaint();
    }

    #endregion

    #region Snapshot / dump

    /// <summary>
    /// Captures a read-only snapshot of the live tree from the application root,
    /// or null when there is no root view.
    /// </summary>
    public VisualTreeNode? Snapshot() => Root is { } root ? BuildNode(root, 0) : null;

    /// <summary>
    /// Captures a snapshot rooted at an arbitrary view. Exposed primarily for
    /// tests that build a tree in-memory without a live window.
    /// </summary>
    public VisualTreeNode Snapshot(SkiaView root) => BuildNode(root, 0);

    private VisualTreeNode BuildNode(SkiaView view, int depth)
    {
        var children = new List<VisualTreeNode>();
        if (depth < MaxDepth)
        {
            foreach (var child in EnumerateChildren(view))
                children.Add(BuildNode(child, depth + 1));
        }

        return new VisualTreeNode
        {
            TypeName = view.GetType().Name,
            Bounds = view.ScreenBounds,
            Text = TryGetText(view),
            BackgroundColor = view.BackgroundColor,
            IsVisible = view.IsVisible,
            ChildCount = children.Count,
            Children = children,
            Source = view,
        };
    }

    /// <summary>
    /// Produces an indented text dump of the live tree for logging/quick
    /// inspection. Returns a short placeholder when there is no root view.
    /// </summary>
    public string DumpTree()
    {
        var root = Root;
        return root == null ? "(no root view)" : DumpTree(root);
    }

    /// <summary>Produces an indented text dump rooted at an arbitrary view.</summary>
    public string DumpTree(SkiaView root)
    {
        var sb = new StringBuilder();
        var visited = new HashSet<SkiaView>(ReferenceEqualityComparer.Instance);
        DumpNode(root, 0, sb, visited);
        return sb.ToString();
    }

    private void DumpNode(SkiaView view, int depth, StringBuilder sb, HashSet<SkiaView> visited)
    {
        if (depth >= MaxDepth || !visited.Add(view))
            return;

        var b = view.ScreenBounds;
        sb.Append(' ', depth * 2);
        sb.Append(view.GetType().Name);
        sb.Append(" [")
          .Append((int)b.X).Append(',').Append((int)b.Y).Append(' ')
          .Append((int)b.Width).Append('x').Append((int)b.Height).Append(']');

        var text = TryGetText(view);
        if (!string.IsNullOrEmpty(text))
            sb.Append(" Text=\"").Append(Truncate(text, 40)).Append('"');

        var bg = view.BackgroundColor;
        if (bg != null)
            sb.Append(" bg=").Append(bg.ToHex());

        sb.Append(" vis=").Append(view.IsVisible);

        int childCount = 0;
        foreach (var _ in EnumerateChildren(view))
            childCount++;
        if (childCount > 0)
            sb.Append(" (").Append(childCount).Append(" children)");

        sb.Append('\n');

        foreach (var child in EnumerateChildren(view))
            DumpNode(child, depth + 1, sb, visited);
    }

    /// <summary>
    /// Enumerates a view's children, honoring the established invariant that
    /// <see cref="SkiaLayoutView"/> shadows the base <c>Children</c> collection,
    /// and appending any <see cref="SkiaView.ExtraContentRoots"/> (e.g. shell
    /// section content and navigation-stack pages kept in private fields).
    /// </summary>
    internal static IEnumerable<SkiaView> EnumerateChildren(SkiaView view)
    {
        var children = view is SkiaLayoutView layout ? layout.Children : view.Children;
        for (int i = 0; i < children.Count; i++)
            yield return children[i];

        foreach (var extra in view.ExtraContentRoots)
            yield return extra;
    }

    private string? TryGetText(SkiaView view)
    {
        var prop = _textProps.GetOrAdd(view.GetType(), static t =>
        {
            var p = t.GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
            return p != null && p.CanRead && p.PropertyType == typeof(string) ? p : null;
        });

        if (prop == null)
            return null;

        try { return prop.GetValue(view) as string; }
        catch { return null; }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "…";

    #endregion

    #region Input hooks (called from LinuxApplication input handlers)

    /// <summary>
    /// Pick-mode pointer-move hook. Returns true when the inspector consumed the
    /// event (pick mode active), so normal routing is skipped. Never throws.
    /// </summary>
    internal bool HandlePointerMoved(float x, float y)
    {
        if (!PickModeActive)
            return false;

        try
        {
            var hit = Root?.HitTest(x, y);
            if (!ReferenceEquals(hit, HoveredNode))
            {
                HoveredNode = hit;
                RequestRepaint();
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("VisualTreeInspector", "pick pointer-move failed", ex);
        }

        return true;
    }

    /// <summary>
    /// Pick-mode pointer-press hook. Commits the element under the cursor as the
    /// selection and exits pick mode. Returns true when consumed. Never throws.
    /// </summary>
    internal bool HandlePointerPressed(float x, float y)
    {
        if (!PickModeActive)
            return false;

        try
        {
            SelectedNode = Root?.HitTest(x, y);
            HoveredNode = null;
            PickModeActive = false;

            if (SelectedNode != null)
            {
                DiagnosticLog.Info(
                    "VisualTreeInspector",
                    $"Picked {SelectedNode.GetType().Name} bounds={SelectedNode.ScreenBounds}");
            }

            RequestRepaint();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("VisualTreeInspector", "pick pointer-press failed", ex);
            PickModeActive = false;
        }

        return true;
    }

    /// <summary>
    /// Key hook. Escape exits pick mode when active. Returns true when consumed.
    /// Never throws.
    /// </summary>
    internal bool HandleKeyDown(Key key)
    {
        if (PickModeActive && key == Key.Escape)
        {
            DisablePickMode();
            return true;
        }
        return false;
    }

    #endregion

    #region Hotkey

    private int _hotkeyId = -1;

    /// <summary>
    /// Opt-in convenience: registers a global hotkey (default Ctrl+Shift+D) that
    /// toggles the inspector. Not wired by default — call this explicitly from app
    /// startup to enable it. Safe to call more than once (re-registers).
    /// </summary>
    public void BindDefaultHotkey(
        GlobalHotkeyService hotkeys,
        HotkeyKey key = HotkeyKey.D,
        HotkeyModifiers modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift)
    {
        ArgumentNullException.ThrowIfNull(hotkeys);

        if (_hotkeyId >= 0)
        {
            try { hotkeys.Unregister(_hotkeyId); } catch { /* best effort */ }
            _hotkeyId = -1;
        }

        _hotkeyId = hotkeys.Register(key, modifiers);
        hotkeys.HotkeyPressed += OnHotkeyPressed;
    }

    private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
    {
        if (e.Id == _hotkeyId)
            Toggle();
    }

    #endregion

    #region Overlay drawing

    private void RequestRepaint()
    {
        // Overlays only paint when a frame is (re)drawn; force a full redraw so the
        // overlay follows selection/hover changes even under dirty-region culling.
        LinuxApplication.Current?.RenderingEngine?.InvalidateAll();
        LinuxApplication.RequestRedraw();
    }

    private void DrawOverlay(SKCanvas canvas)
    {
        // Runs inside the render pass; must never throw back into the engine.
        try
        {
            if (ShowAllOutlines && Root is { } root)
                DrawAllOutlines(canvas, root, 0);

            var target = HoveredNode ?? SelectedNode;
            if (target != null)
                DrawHighlight(canvas, target);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("VisualTreeInspector", "overlay draw failed", ex);
        }
    }

    private void DrawAllOutlines(SKCanvas canvas, SkiaView view, int depth)
    {
        if (depth >= MaxDepth)
            return;

        if (view.IsVisible)
        {
            var b = view.ScreenBounds;
            if (b.Width > 0 && b.Height > 0)
            {
                using var outline = new SKPaint
                {
                    Color = new SKColor(0x33, 0x99, 0xFF, 0x40),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1,
                    IsAntialias = true,
                };
                canvas.DrawRect((float)b.X, (float)b.Y, (float)b.Width, (float)b.Height, outline);
            }
        }

        foreach (var child in EnumerateChildren(view))
            DrawAllOutlines(canvas, child, depth + 1);
    }

    private void DrawHighlight(SKCanvas canvas, SkiaView view)
    {
        var b = view.ScreenBounds;
        var rect = new SKRect((float)b.X, (float)b.Y, (float)(b.X + b.Width), (float)(b.Y + b.Height));

        using var fill = new SKPaint
        {
            Color = new SKColor(0x33, 0x99, 0xFF, 0x33),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        canvas.DrawRect(rect, fill);

        using var stroke = new SKPaint
        {
            Color = new SKColor(0x33, 0x99, 0xFF, 0xFF),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
        };
        canvas.DrawRect(rect, stroke);

        DrawLabel(canvas, rect, $"{view.GetType().Name}  {(int)b.Width}x{(int)b.Height}");
    }

    private static void DrawLabel(SKCanvas canvas, SKRect rect, string text)
    {
        using var font = SkiaFontFactory.Create(12f);
        float textWidth = font.MeasureText(text);
        const float padX = 6f;
        const float padY = 4f;
        float chipHeight = font.Size + padY * 2;
        float chipWidth = textWidth + padX * 2;

        // Prefer just above the highlighted rect; drop below if there is no room.
        float chipTop = rect.Top - chipHeight - 2f;
        if (chipTop < 0)
            chipTop = rect.Top + 2f;
        float chipLeft = rect.Left;

        var chip = new SKRect(chipLeft, chipTop, chipLeft + chipWidth, chipTop + chipHeight);

        using var chipPaint = new SKPaint
        {
            Color = new SKColor(0x20, 0x2A, 0x3A, 0xE6),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        canvas.DrawRoundRect(chip, 3f, 3f, chipPaint);

        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
        };
        float baseline = chipTop + padY + font.Size * 0.8f;
        canvas.DrawText(text, chipLeft + padX, baseline, font, textPaint);
    }

    #endregion

    /// <summary>
    /// Overlay owner view: it exists only to carry the inspector's draw callback in
    /// the popup-overlay registry. It is never inserted into the visual tree, so its
    /// <c>OnDraw</c> is unused; and its <c>HitTestPopupArea</c> returns false so it
    /// never diverts pointer routing (unlike real popups).
    /// </summary>
    private sealed class InspectorOverlayView : SkiaView
    {
        protected override void OnDraw(SKCanvas canvas, SKRect bounds) { }

        protected override bool HitTestPopupArea(float x, float y) => false;
    }
}
