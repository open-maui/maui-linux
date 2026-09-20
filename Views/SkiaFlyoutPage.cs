// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A page that displays a flyout menu and detail content.
/// </summary>
public class SkiaFlyoutPage : SkiaLayoutView
{
    private SkiaView? _flyout;
    private SkiaView? _detail;
    private bool _isPresented = false;
    private float _flyoutWidth = 300f;
    private float _flyoutAnimationProgress = 0f;
    private bool _gestureEnabled = true;

    // Gesture tracking
    private bool _isDragging = false;
    private float _dragStartX;
    private float _dragCurrentX;

    // ScrimColor backing fields
    private SKColor _scrimColorSK = SkiaTheme.Overlay40SK;
    private Color _scrimColor = SkiaTheme.Overlay40;

    /// <summary>
    /// Gets or sets the flyout content (menu).
    /// </summary>
    public SkiaView? Flyout
    {
        get => _flyout;
        set
        {
            if (_flyout != value)
            {
                if (_flyout != null)
                {
                    RemoveChild(_flyout);
                }

                _flyout = value;

                if (_flyout != null)
                {
                    AddChild(_flyout);
                }

                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the detail content (main content).
    /// </summary>
    public SkiaView? Detail
    {
        get => _detail;
        set
        {
            if (_detail != value)
            {
                if (_detail != null)
                {
                    RemoveChild(_detail);
                }

                _detail = value;

                if (_detail != null)
                {
                    AddChild(_detail);
                }

                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the flyout is currently presented. In split
    /// layout the flyout is always presented and the value cannot be cleared.
    /// </summary>
    public bool IsPresented
    {
        get => _isPresented;
        set
        {
            if (IsSplit && !value) return;
            if (_isPresented != value)
            {
                _isPresented = value;
                _flyoutAnimationProgress = value ? 1f : 0f;
                IsPresentedChanged?.Invoke(this, EventArgs.Empty);
                InvalidateMeasure();
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the width of the flyout panel.
    /// </summary>
    public float FlyoutWidth
    {
        get => _flyoutWidth;
        set
        {
            if (_flyoutWidth != value)
            {
                _flyoutWidth = Math.Max(100, value);
                InvalidateMeasure();
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether swipe gestures are enabled.
    /// </summary>
    public bool GestureEnabled
    {
        get => _gestureEnabled;
        set => _gestureEnabled = value;
    }

    private FlyoutLayoutBehavior _flyoutLayoutBehavior = FlyoutLayoutBehavior.Default;

    /// <summary>
    /// The flyout layout behavior. <see cref="FlyoutLayoutBehavior.Split"/>,
    /// <see cref="FlyoutLayoutBehavior.SplitOnLandscape"/> and
    /// <see cref="FlyoutLayoutBehavior.SplitOnPortrait"/> show the flyout
    /// side by side with the detail (desktop windows are treated as
    /// landscape); <see cref="FlyoutLayoutBehavior.Popover"/> and
    /// <see cref="FlyoutLayoutBehavior.Default"/> overlay it on demand.
    /// </summary>
    public FlyoutLayoutBehavior FlyoutLayoutBehavior
    {
        get => _flyoutLayoutBehavior;
        set
        {
            if (_flyoutLayoutBehavior == value) return;
            _flyoutLayoutBehavior = value;
            if (IsSplit)
            {
                // Split keeps the flyout open; entering split presents it.
                if (!_isPresented)
                {
                    _isPresented = true;
                    IsPresentedChanged?.Invoke(this, EventArgs.Empty);
                }
                _flyoutAnimationProgress = 1f;
            }
            InvalidateMeasure();
            Invalidate();
        }
    }

    /// <summary>
    /// True when the current layout behavior places the flyout beside the
    /// detail rather than over it.
    /// </summary>
    public bool IsSplit => _flyoutLayoutBehavior is FlyoutLayoutBehavior.Split
        or FlyoutLayoutBehavior.SplitOnLandscape
        or FlyoutLayoutBehavior.SplitOnPortrait;

    /// <summary>
    /// The horizontal offset of the detail content: the flyout width in
    /// split layout, zero when the flyout overlays the detail.
    /// </summary>
    public float DetailOffset => IsSplit ? FlyoutWidth : 0f;

    /// <summary>
    /// Background color of the scrim when flyout is open.
    /// </summary>
    public Color ScrimColor
    {
        get => _scrimColor;
        set
        {
            _scrimColor = value;
            _scrimColorSK = value.ToSKColor();
            Invalidate();
        }
    }

    /// <summary>
    /// Shadow width for the flyout.
    /// </summary>
    public float ShadowWidth { get; set; } = 8f;

    /// <summary>
    /// Event raised when IsPresented changes.
    /// </summary>
    public event EventHandler? IsPresentedChanged;

    protected override Size MeasureOverride(Size availableSize)
    {
        // Measure flyout
        if (_flyout != null)
        {
            _flyout.Measure(new Size(FlyoutWidth, availableSize.Height));
        }

        // Measure detail: full size when overlaid, beside the flyout when split
        if (_detail != null)
        {
            _detail.Measure(new Size(Math.Max(0, availableSize.Width - DetailOffset), availableSize.Height));
        }

        return availableSize;
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        // Arrange detail: the whole area, or the area right of a split flyout
        if (_detail != null)
        {
            float offset = DetailOffset;
            _detail.Arrange(new Rect(bounds.Left + offset, bounds.Top, Math.Max(0, bounds.Width - offset), bounds.Height));
        }

        // Arrange flyout (positioned based on animation progress; pinned when split)
        if (_flyout != null)
        {
            float flyoutX = IsSplit
                ? (float)bounds.Left
                : (float)bounds.Left - FlyoutWidth + (FlyoutWidth * _flyoutAnimationProgress);
            var flyoutBounds = new Rect(flyoutX, bounds.Top, FlyoutWidth, bounds.Height);
            _flyout.Arrange(flyoutBounds);
        }

        return bounds;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        canvas.Save();
        canvas.ClipRect(bounds);

        // Draw detail content first
        _detail?.Draw(canvas);

        if (IsSplit)
        {
            // Side-by-side: the flyout is a permanent panel, no scrim; a
            // hairline separates it from the detail.
            _flyout?.Draw(canvas);
            using var dividerPaint = new SKPaint
            {
                Color = SkiaTheme.Gray300SK,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f
            };
            float x = (float)Bounds.Left + FlyoutWidth;
            canvas.DrawLine(x, (float)Bounds.Top, x, (float)Bounds.Bottom, dividerPaint);
        }
        else if (_flyoutAnimationProgress > 0)
        {
            // Draw scrim (semi-transparent overlay)
            using var scrimPaint = new SKPaint
            {
                Color = _scrimColorSK.WithAlpha((byte)(_scrimColorSK.Alpha * _flyoutAnimationProgress)),
                Style = SKPaintStyle.Fill
            };
            var skBounds = new SKRect((float)Bounds.Left, (float)Bounds.Top, (float)(Bounds.Left + Bounds.Width), (float)(Bounds.Top + Bounds.Height));
            canvas.DrawRect(skBounds, scrimPaint);

            // Draw flyout shadow
            if (_flyout != null && ShadowWidth > 0)
            {
                DrawFlyoutShadow(canvas);
            }

            // Draw flyout
            _flyout?.Draw(canvas);
        }

        canvas.Restore();
    }

    private void DrawFlyoutShadow(SKCanvas canvas)
    {
        if (_flyout == null) return;

        float shadowRight = (float)(_flyout.Bounds.Left + _flyout.Bounds.Width);
        var shadowRect = new SKRect(
            shadowRight,
            (float)Bounds.Top,
            shadowRight + ShadowWidth,
            (float)(Bounds.Top + Bounds.Height));

        using var shadowPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(shadowRect.Left, shadowRect.MidY),
                new SKPoint(shadowRect.Right, shadowRect.MidY),
                new SKColor[] { SkiaTheme.Shadow25SK, SKColors.Transparent },
                null,
                SKShaderTileMode.Clamp)
        };

        canvas.DrawRect(shadowRect, shadowPaint);
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !Bounds.Contains(x, y)) return null;

        // If flyout is presented, check if hit is in flyout
        if (_flyoutAnimationProgress > 0 && _flyout != null)
        {
            var flyoutHit = _flyout.HitTest(x, y);
            if (flyoutHit != null) return flyoutHit;

            // Hit on scrim closes flyout (overlay layouts only)
            if (_isPresented && !IsSplit)
            {
                return this; // Return self to handle scrim tap
            }
        }

        // Check detail content
        if (_detail != null)
        {
            var detailHit = _detail.HitTest(x, y);
            if (detailHit != null) return detailHit;
        }

        return this;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        // Check if tap is on scrim (outside flyout but flyout is open)
        if (_isPresented && !IsSplit && _flyout != null && !_flyout.Bounds.Contains(e.X, e.Y))
        {
            IsPresented = false;
            e.Handled = true;
            return;
        }

        // Start drag gesture (overlay layouts only; a split flyout is pinned)
        if (_gestureEnabled && !IsSplit)
        {
            _isDragging = true;
            _dragStartX = e.X;
            _dragCurrentX = e.X;
        }

        base.OnPointerPressed(e);
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (_isDragging && _gestureEnabled)
        {
            _dragCurrentX = e.X;
            float delta = _dragCurrentX - _dragStartX;

            // Calculate new animation progress
            if (_isPresented)
            {
                // Dragging to close
                _flyoutAnimationProgress = Math.Clamp(1f + (delta / FlyoutWidth), 0f, 1f);
            }
            else
            {
                // Dragging to open (only from left edge)
                if (_dragStartX < 30)
                {
                    _flyoutAnimationProgress = Math.Clamp(delta / FlyoutWidth, 0f, 1f);
                }
            }

            Invalidate();
            e.Handled = true;
        }

        base.OnPointerMoved(e);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;

            // Determine final state based on progress
            bool wasPresented = _isPresented;
            if (_flyoutAnimationProgress > 0.5f)
            {
                _isPresented = true;
                _flyoutAnimationProgress = 1f;
            }
            else
            {
                _isPresented = false;
                _flyoutAnimationProgress = 0f;
            }

            if (wasPresented != _isPresented)
            {
                IsPresentedChanged?.Invoke(this, EventArgs.Empty);
            }
            Invalidate();
        }

        base.OnPointerReleased(e);
    }

    /// <summary>
    /// Toggles the flyout presentation state (no-op in split layout).
    /// </summary>
    public void ToggleFlyout()
    {
        IsPresented = !IsPresented;
    }
}
