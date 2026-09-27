// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// The window overlay of one Syncfusion <c>SfWindowOverlay</c> (SfPopup's
/// PopupOverlay): a window-sized surface drawn over the page, its modal layers
/// and its popups, holding the overlay's views at absolute window positions.
/// The native builds host the overlay's <c>WindowOverlayContainer</c> as a
/// canvas in a WinUI <c>Popup</c> (a separate window on Android and iOS) and
/// place each view on it; the platform-neutral build Linux apps get has every
/// member of <c>SfWindowOverlay</c> empty, so nothing built on it ever showed.
/// </summary>
/// <remarks>
/// <para>
/// The container view is this view's <see cref="SkiaView.MauiView"/>, as it is
/// the canvas's virtual view natively: its Background paints the whole window
/// (SfPopup's overlay colour, or Transparent to catch outside presses), its
/// Opacity (SfPopup's fade-in) applies to the views on it, and IsVisible hides
/// them all. A container with no background lets presses through to the page,
/// as a WinUI canvas with a null background is not hit.
/// </para>
/// <para>
/// It is not part of the page's view tree: it hangs off the window's root view
/// (for invalidation and the render context), draws as a popup overlay, lays
/// its views out itself (the renderer only lays out the page and its modal
/// layers) and receives pointer input through the popup-overlay routing, which
/// it hands to the view under the pointer.
/// </para>
/// </remarks>
internal sealed class SkiaSfOverlay : SkiaView
{
    private readonly List<Placement> _placements = new();
    private SkiaView? _hovered;
    private SkiaView? _pressed;
    private Size _laidOutFor;
    private bool _registered;

    /// <summary>The overlay's container (SfPopup's SfPopupOverlayContainer), when it has one.</summary>
    internal View? Container { get; private set; }

    /// <summary>The window the overlay is shown in, from the last show.</summary>
    internal WindowContext? Window { get; private set; }

    internal bool IsShown => _registered;

    /// <summary>Blur applied to what is beneath the overlay (SfPopup's OverlayMode Blur); 0 for none.</summary>
    internal float BackdropBlurRadius
    {
        get => _backdropBlurRadius;
        set
        {
            if (_backdropBlurRadius == value)
                return;
            _backdropBlurRadius = value;
            Invalidate();
        }
    }

    private float _backdropBlurRadius;

    /// <summary>
    /// The drop shadow drawn under a view on the overlay (SfPopup's
    /// PopupStyle.HasShadow, a composition drop shadow on Windows): its corner
    /// radius, or null for none.
    /// </summary>
    internal Func<SkiaView, float?>? ShadowOf { get; set; }

    /// <summary>Raised (after the frame) when the window the overlay covers changed size.</summary>
    internal event EventHandler? WindowSizeChanged;

    internal void SetContainer(View? container)
    {
        if (ReferenceEquals(Container, container))
            return;
        Container = container;
        MauiView = container;
    }

    /// <summary>The views on the overlay, bottom to top.</summary>
    internal IEnumerable<SkiaView> Views => _placements.Select(p => p.View);

    /// <summary>
    /// Places <paramref name="view"/> on the overlay (adding it on top when it
    /// is not on it yet) and shows the overlay in <paramref name="window"/>.
    /// </summary>
    internal void Place(WindowContext window, View mauiView, SkiaView view, Placement placement)
    {
        Attach(window);
        int index = _placements.FindIndex(p => ReferenceEquals(p.View, view));
        if (index >= 0)
        {
            _placements[index] = placement;
        }
        else
        {
            _placements.Add(placement);
            if (view.Parent is { } oldParent && !ReferenceEquals(oldParent, this))
                oldParent.RemoveChild(view);
            if (!Children.Contains(view))
                AddChild(view);
            // The view's logical parent is the page (SfPopup parents its view
            // to the container, the container to the page): presses on the
            // overlay stop at the view, as they do in a native popup window.
            SkiaView.SetPointerBubbleBoundary(mauiView, true);
        }
        LayOut(placement, measure: true);
        Show();
    }

    /// <summary>Takes <paramref name="view"/> off the overlay; hides the overlay when nothing is left on it.</summary>
    internal void Remove(SkiaView view)
    {
        int index = _placements.FindIndex(p => ReferenceEquals(p.View, view));
        if (index < 0)
            return;
        _placements.RemoveAt(index);
        if (view.MauiView is { } maui)
            SkiaView.SetPointerBubbleBoundary(maui, false);
        if (ReferenceEquals(view.Parent, this))
            RemoveChild(view);
        if (ReferenceEquals(_hovered, view) || (_hovered != null && IsWithin(_hovered, view)))
            _hovered = null;
        if (_pressed != null && IsWithin(_pressed, view))
            _pressed = null;
        if (_placements.Count == 0)
            Hide();
        else
            Invalidate();
    }

    /// <summary>Takes every view off the overlay and hides it.</summary>
    internal void Clear()
    {
        foreach (var placement in _placements.ToArray())
            Remove(placement.View);
        Hide();
    }

    private void Attach(WindowContext window)
    {
        if (ReferenceEquals(Window, window) && window.RootView is { } same && ReferenceEquals(Parent, same))
            return;
        if (Window?.DisplayWindow is { } oldDisplay)
            oldDisplay.Resized -= OnWindowResized;
        Window = window;
        Parent = window.RootView;
        if (window.DisplayWindow is { } display)
            display.Resized += OnWindowResized;
        _laidOutFor = Size.Zero;
    }

    private void Show()
    {
        UpdateBounds();
        // Registering again moves the overlay to the top: the popup opened
        // last draws over, and takes input before, the ones opened earlier.
        RegisterPopupOverlay(this, DrawOverlay);
        _registered = true;
        Invalidate();
    }

    private void Hide()
    {
        if (!_registered)
            return;
        _registered = false;
        UnregisterPopupOverlay(this);
        if (_hovered != null)
        {
            var hovered = _hovered;
            _hovered = null;
            hovered.OnPointerExited(new PointerEventArgs(float.NaN, float.NaN));
        }
        _pressed = null;
        if (Window?.DisplayWindow is { } display)
            display.Resized -= OnWindowResized;
        // The frame that removes an overlay is repainted whole.
        Invalidate();
    }

    private void OnWindowResized(object? sender, (int Width, int Height) size)
    {
        // After the window context has taken the new size and laid the page out.
        Microsoft.Maui.Platform.Linux.Native.GLibNative.IdleAdd(() =>
        {
            if (_registered)
            {
                UpdateBounds();
                LayoutDirty = true;
                WindowSizeChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
            return false;
        });
    }

    /// <summary>The logical size of the view tree of <paramref name="window"/> (below a Wayland CSD title bar).</summary>
    internal static Size WindowSizeOf(WindowContext? window)
    {
        if (window == null)
            return Size.Zero;
        if (window.RenderingEngine is { LogicalWidth: > 0, LogicalHeight: > 0 } engine)
            return new Size(engine.LogicalWidth, Math.Max(0, engine.LogicalHeight - window.CsdPointerInsetLogical));
        if (window.RootView is { Bounds: { Width: > 0 } rootBounds })
            return rootBounds.Size;
        if (window.DisplayWindow is { } display)
            return new Size(display.Width / Math.Max(1f, window.Scale), display.Height / Math.Max(1f, window.Scale));
        return Size.Zero;
    }

    private void UpdateBounds()
    {
        var size = WindowSizeOf(Window);
        Bounds = new Rect(0, 0, size.Width, size.Height);
    }

    /// <summary>
    /// Lays out the views that asked for it, the ones whose anchor moved and
    /// all of them when the window changed size.
    /// </summary>
    private void LayOutAll()
    {
        var size = Bounds.Size;
        bool all = LayoutDirty || size != _laidOutFor;
        LayoutDirty = false;
        _laidOutFor = size;
        foreach (var placement in _placements.ToArray())
            LayOut(placement, measure: all || placement.View.DesiredSize == Size.Zero);
    }

    private void LayOut(Placement placement, bool measure)
    {
        var view = placement.View;
        var window = Bounds.Size;
        try
        {
            if (measure || view.DesiredSize == Size.Zero)
                view.Measure(window);
            var desired = view.DesiredSize;
            if (Position(placement, desired, window) is not { } origin)
                return;
            var rect = new Rect(origin.X, origin.Y, desired.Width, desired.Height);
            if (measure || view.Bounds != rect)
                view.Arrange(rect);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Laying out {view.MauiView?.GetType().Name ?? view.GetType().Name} on the window overlay failed", ex);
        }
    }

    /// <summary>
    /// Where a view of <paramref name="size"/> goes: the native
    /// <c>SfWindowOverlay</c>'s AlignPosition (absolute) and
    /// AlignPositionToRelative (next to an anchor view, kept inside the window).
    /// </summary>
    private static Point? Position(Placement placement, Size size, Size window)
    {
        double x = placement.X, y = placement.Y;
        if (placement.Relative is not { } relative)
        {
            switch (placement.Horizontal)
            {
                case OverlayAlignment.End: x -= size.Width; break;
                case OverlayAlignment.Center: x -= size.Width / 2; break;
            }
            switch (placement.Vertical)
            {
                case OverlayAlignment.End: y -= size.Height; break;
                case OverlayAlignment.Center: y -= size.Height / 2; break;
            }
            return new Point(x, y);
        }

        if (relative.Handler?.PlatformView is not SkiaView anchor)
            return null;
        var bounds = anchor.ScreenBounds;
        switch (placement.Horizontal)
        {
            case OverlayAlignment.End: x += bounds.Width; break;
            case OverlayAlignment.Center: x += bounds.Width / 2 - size.Width / 2; break;
            case OverlayAlignment.Start: x -= size.Width; break;
        }
        switch (placement.Vertical)
        {
            case OverlayAlignment.End: y += bounds.Height; break;
            case OverlayAlignment.Center: y += bounds.Height / 2 - size.Height / 2; break;
            case OverlayAlignment.Start: y -= size.Height; break;
        }
        x = Math.Max(0, Math.Min(x + bounds.X, window.Width - size.Width));
        y = Math.Max(0, Math.Min(y + bounds.Y, window.Height - size.Height));
        return new Point(x, y);
    }

    private void DrawOverlay(SKCanvas canvas)
    {
        if (!_registered)
            return;
        if (Window?.RootView is not { } root || !ReferenceEquals(Parent, root))
        {
            // The window's page was replaced or the window closed.
            if (Window?.RootView is { } newRoot)
                Parent = newRoot;
            else
                return;
        }
        UpdateBounds();
        LayOutAll();

        if (_backdropBlurRadius > 0 && IsVisible && Opacity > 0)
        {
            // Blurs what the page and the layers beneath drew: the native
            // Gaussian backdrop brush over the window.
            float sigma = _backdropBlurRadius * Opacity;
            using var blur = SKImageFilter.CreateBlur(sigma, sigma);
            var rec = new SKCanvasSaveLayerRec { Bounds = BoundsSK, Backdrop = blur };
            canvas.SaveLayer(rec);
            canvas.Restore();
        }

        try
        {
            Draw(canvas);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Drawing the window overlay failed", ex);
        }
    }

    /// <summary>The views' drop shadows, over the overlay's background and under the views.</summary>
    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (ShadowOf is not { } shadowOf)
            return;
        foreach (var placement in _placements)
        {
            var view = placement.View;
            if (!view.IsVisible || view.Opacity <= 0 || shadowOf(view) is not { } radius)
                continue;
            var rect = view.BoundsSK;
            if (rect.Width <= 0 || rect.Height <= 0)
                continue;

            // Follows the view's own zoom and slide (SfPopup's open animation).
            float scaleX = (float)(view.Scale * view.ScaleX), scaleY = (float)(view.Scale * view.ScaleY);
            float cx = rect.Left + rect.Width * (float)view.AnchorX, cy = rect.Top + rect.Height * (float)view.AnchorY;
            rect = new SKRect(cx + (rect.Left - cx) * scaleX, cy + (rect.Top - cy) * scaleY,
                cx + (rect.Right - cx) * scaleX, cy + (rect.Bottom - cy) * scaleY);
            rect.Offset((float)view.TranslationX, (float)view.TranslationY);

            using var paint = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(0, 0, 0, (byte)(77 * view.Opacity)),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 10),
            };
            canvas.DrawRoundRect(rect, radius, radius, paint);
        }
    }

    /// <summary>
    /// The points the overlay takes while it is shown: those on its views, and
    /// every point when its container paints a background (Transparent
    /// included), as the native canvas is hit there.
    /// </summary>
    protected override bool HitTestPopupArea(float x, float y) =>
        _registered && IsVisible && Window?.RootView is { } root && ReferenceEquals(Parent, root)
        && (TakesBackdropInput || PlacementAt(x, y) != null);

    private bool TakesBackdropInput => Container is { } container && !Brush.IsNullOrEmpty(container.Background);

    /// <summary>Never hit in the page's own tree: the overlay is only reachable as a popup overlay.</summary>
    public override SkiaView? HitTest(float x, float y) => null;

    protected override Size MeasureOverride(Size availableSize) => Size.Zero;

    private Placement? PlacementAt(float x, float y)
    {
        for (int i = _placements.Count - 1; i >= 0; i--)
        {
            var view = _placements[i].View;
            if (view.IsVisible && view.Bounds.Contains(x, y))
                return _placements[i];
        }
        return null;
    }

    /// <summary>
    /// The view under the point on the overlay. A point on an overlay view's own
    /// surface with nothing interactive there is still the overlay view's.
    /// </summary>
    private SkiaView? ViewAt(float x, float y, out bool onView)
    {
        var placement = PlacementAt(x, y);
        onView = placement != null;
        return placement?.View.HitTest(x, y);
    }

    public override void OnPointerEntered(PointerEventArgs e) => Hover(ViewAt(e.X, e.Y, out _), e);

    public override void OnPointerExited(PointerEventArgs e) => Hover(null, e);

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (_pressed != null)
        {
            _pressed.OnPointerMoved(e);
            return;
        }
        var target = ViewAt(e.X, e.Y, out _);
        Hover(target, e);
        target?.OnPointerMoved(e);
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        e.Handled = true;
        _pressed = ViewAt(e.X, e.Y, out bool onView);
        if (_pressed != null)
        {
            if (_pressed.IsFocusable && Window != null)
                Window.FocusedView = _pressed;
            _pressed.OnPointerPressed(e);
            return;
        }
        if (!onView && Container is { } container)
            SfOverlayPatches.ProcessTouchInteraction(container, e.X, e.Y);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        e.Handled = true;
        var target = _pressed;
        _pressed = null;
        target?.OnPointerReleased(e);
    }

    /// <summary>
    /// Scrolls the scroll view under the pointer on the overlay and keeps the
    /// wheel from reaching the page beneath.
    /// </summary>
    public override void OnScroll(ScrollEventArgs e)
    {
        for (var view = ViewAt(e.X, e.Y, out _); view != null && !ReferenceEquals(view, this); view = view.Parent)
        {
            RaiseScrollRouted(view, e);
            if (e.Handled)
                break;
            if (view is SkiaScrollView scrollView)
            {
                scrollView.OnScroll(e);
                break;
            }
            view.OnScroll(e);
            if (e.Handled)
                break;
        }
        e.Handled = true;
    }

    private void Hover(SkiaView? target, PointerEventArgs e)
    {
        if (ReferenceEquals(target, _hovered))
            return;
        _hovered?.OnPointerExited(e);
        _hovered = target;
        _hovered?.OnPointerEntered(e);
    }

    private static bool IsWithin(SkiaView view, SkiaView ancestor)
    {
        for (var current = view; current != null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Where a view sits on the overlay: at (X, Y) in the window with the
    /// alignments, or, with <see cref="Relative"/>, next to that view with
    /// (X, Y) added.
    /// </summary>
    internal sealed record Placement(
        SkiaView View,
        double X,
        double Y,
        OverlayAlignment Horizontal = OverlayAlignment.Start,
        OverlayAlignment Vertical = OverlayAlignment.Start,
        View? Relative = null);
}

/// <summary>
/// Syncfusion's internal WindowOverlayHorizontalAlignment (Left, Right, Center)
/// and WindowOverlayVerticalAlignment (Top, Bottom, Center), same values.
/// </summary>
internal enum OverlayAlignment
{
    Start = 0,
    End = 1,
    Center = 2,
}
