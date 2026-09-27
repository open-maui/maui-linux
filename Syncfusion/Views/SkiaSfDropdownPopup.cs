// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Syncfusion.Maui.Core;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// The open drop-down of a Syncfusion <c>SfDropdownView</c> (SfComboBox,
/// SfAutocomplete): its content (the suggestion list) drawn as a popup overlay
/// below or above the anchor control, as the native builds show it in a
/// WinUI <c>Popup</c> / Android <c>PopupWindow</c>. It is not part of the
/// page's view tree: it hangs off the window's root view (for invalidation and
/// the render context) and receives pointer input through the popup-overlay
/// routing, which it hands to the content under the pointer.
/// </summary>
internal sealed class SkiaSfDropdownPopup : SkiaView
{
    /// <summary>Height used when the control has not computed one (the native builds' default).</summary>
    private const double DefaultPopupHeight = 400;

    private readonly SfDropdownController _controller;
    private SkiaView? _content;
    private SkiaView? _hovered;
    private SkiaView? _pressed;
    private Rect _laidOutAt;

    internal SkiaSfDropdownPopup(SfDropdownController controller)
    {
        _controller = controller;
    }

    /// <summary>The popup's rectangle in window coordinates, as last placed.</summary>
    internal Rect PopupRect { get; private set; }

    internal bool IsShown { get; private set; }

    internal void Show(SkiaView root, SkiaView content)
    {
        Parent = root;
        if (!ReferenceEquals(_content, content))
        {
            if (_content != null && ReferenceEquals(_content.Parent, this))
                _content.Parent = null;
            _content = content;
        }
        content.Parent = this;
        _laidOutAt = Rect.Zero;
        if (!Place())
            return;
        IsShown = true;
        RegisterPopupOverlay(this, DrawPopup);
        Invalidate();
    }

    internal void Hide()
    {
        if (!IsShown)
            return;
        IsShown = false;
        UnregisterPopupOverlay(this);
        if (_hovered != null)
        {
            var hovered = _hovered;
            _hovered = null;
            hovered.OnPointerExited(new PointerEventArgs(float.NaN, float.NaN));
        }
        _pressed = null;
        // The frame that removes an overlay is repainted whole.
        Invalidate();
        _controller.AnchorPlatformView?.Invalidate();
    }

    /// <summary>
    /// Computes the popup's rectangle from the anchor's current position and
    /// the control's popup settings, and lays the content out there when the
    /// rectangle changed or the content asked for a new layout. False when the
    /// anchor is gone (the popup cannot be placed).
    /// </summary>
    private bool Place()
    {
        if (_content == null || _controller.AnchorPlatformView is not { } anchorView || Parent is not { } root)
            return false;

        var anchor = anchorView.ScreenBounds;
        var window = root.Bounds;
        var settings = _controller.ReadSettings();

        double width = settings.Width > 0 ? settings.Width : anchor.Width;
        width = Math.Min(width, Math.Max(0, window.Width));
        double height = settings.Height > 0 && double.IsFinite(settings.Height) ? settings.Height : DefaultPopupHeight;

        double spaceBelow = window.Bottom - (anchor.Bottom + settings.OffsetY);
        double spaceAbove = anchor.Top - settings.OffsetY - window.Top;
        bool above = settings.Placement == DropDownPlacement.Top
            || (height > spaceBelow && spaceAbove > spaceBelow);
        height = Math.Max(0, Math.Min(height, above ? spaceAbove : spaceBelow));

        double x = anchor.X + settings.OffsetX;
        x = Math.Max(window.Left, Math.Min(x, window.Right - width));
        double y = above ? anchor.Top - settings.OffsetY - height : anchor.Bottom + settings.OffsetY;

        var rect = new Rect(x, y, width, height);
        PopupRect = rect;
        Bounds = rect;
        if (rect != _laidOutAt || _content.DesiredSize == Size.Zero)
        {
            _laidOutAt = rect;
            try
            {
                _content.Measure(new Size(rect.Width, rect.Height));
                _content.Arrange(rect);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("Syncfusion", "Laying out a drop-down failed", ex);
            }
        }
        return true;
    }

    private void DrawPopup(SKCanvas canvas)
    {
        if (!IsShown || _content == null)
            return;
        if (!_controller.IsAnchorAttached || !Place())
        {
            // Closed after the overlay pass: the overlay list is being walked.
            Microsoft.Maui.Platform.Linux.Native.GLibNative.IdleAdd(() =>
            {
                if (IsShown && !_controller.IsAnchorAttached)
                    _controller.OnAnchorLost();
                return false;
            });
            return;
        }

        var settings = _controller.ReadSettings();
        var rect = new SKRect((float)PopupRect.Left, (float)PopupRect.Top, (float)PopupRect.Right, (float)PopupRect.Bottom);
        if (rect.Width <= 0 || rect.Height <= 0)
            return;
        float radius = (float)Math.Max(0, settings.CornerRadius);
        using var shape = new SKRoundRect(rect, radius);

        if (settings.Shadow)
        {
            using var shadow = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(0, 0, 0, 48),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6),
            };
            canvas.Save();
            canvas.Translate(0, 3);
            canvas.DrawRoundRect(shape, shadow);
            canvas.Restore();
        }

        using (var background = new SKPaint { IsAntialias = true, Color = SKColors.White, Style = SKPaintStyle.Fill })
            canvas.DrawRoundRect(shape, background);

        canvas.Save();
        canvas.ClipRoundRect(shape, SKClipOperation.Intersect, antialias: true);
        try
        {
            _content.Draw(canvas);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Drawing a drop-down failed", ex);
        }
        canvas.Restore();

        if (settings.BorderColor is { } border && border.Alpha > 0)
        {
            using var stroke = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                Color = new SKColor((byte)(border.Red * 255), (byte)(border.Green * 255), (byte)(border.Blue * 255), (byte)(border.Alpha * 255)),
            };
            using var inset = new SKRoundRect(SKRect.Inflate(rect, -0.5f, -0.5f), Math.Max(0, radius - 0.5f));
            canvas.DrawRoundRect(inset, stroke);
        }
    }

    /// <summary>The popup takes the points on it while it is open.</summary>
    protected override bool HitTestPopupArea(float x, float y) =>
        IsShown && PopupRect.Contains(x, y) && _controller.IsAnchorAttached;

    /// <summary>Never hit in the page's own tree: the popup is only reachable as an overlay.</summary>
    public override SkiaView? HitTest(float x, float y) => null;

    protected override Size MeasureOverride(Size availableSize) => Size.Zero;

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
    }

    private SkiaView? ContentAt(float x, float y) =>
        _content != null && PopupRect.Contains(x, y) ? _content.HitTest(x, y) : null;

    public override void OnPointerEntered(PointerEventArgs e) => Hover(ContentAt(e.X, e.Y), e);

    public override void OnPointerExited(PointerEventArgs e) => Hover(null, e);

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (_pressed != null)
        {
            _pressed.OnPointerMoved(e);
            return;
        }
        var target = ContentAt(e.X, e.Y);
        Hover(target, e);
        target?.OnPointerMoved(e);
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        _pressed = ContentAt(e.X, e.Y);
        _pressed?.OnPointerPressed(e);
        e.Handled = true;
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        var target = _pressed ?? ContentAt(e.X, e.Y);
        _pressed = null;
        target?.OnPointerReleased(e);
        e.Handled = true;
    }

    /// <summary>
    /// Scrolls the scroll view under the pointer inside the popup (the
    /// suggestion list) and keeps the wheel from reaching the page beneath.
    /// </summary>
    public override void OnScroll(ScrollEventArgs e)
    {
        for (var view = ContentAt(e.X, e.Y); view != null && !ReferenceEquals(view, this); view = view.Parent)
        {
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
}

/// <summary>The drop-down's popup settings, read from the <c>SfDropdownView</c>.</summary>
internal readonly record struct SfDropdownSettings(
    double Width,
    double Height,
    int OffsetX,
    int OffsetY,
    DropDownPlacement Placement,
    Color? BorderColor,
    double CornerRadius,
    bool Shadow);
