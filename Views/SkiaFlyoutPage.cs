// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    /// Gets or sets whether the flyout is currently presented.
    /// </summary>
    public bool IsPresented
    {
        get => _isPresented;
        set
        {
            if (_isPresented != value)
            {
                _isPresented = value;
                _flyoutAnimationProgress = value ? 1f : 0f;
                IsPresentedChanged?.Invoke(this, EventArgs.Empty);
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

    /// <summary>
    /// The flyout layout behavior.
    /// </summary>
    public FlyoutLayoutBehavior FlyoutLayoutBehavior { get; set; } = FlyoutLayoutBehavior.Default;

    /// <summary>
    /// Background color of the scrim when flyout is open.
    /// </summary>
    public SKColor ScrimColor { get; set; } = new SKColor(0, 0, 0, 100);

    /// <summary>
    /// Shadow width for the flyout.
    /// </summary>
    public float ShadowWidth { get; set; } = 8f;

    /// <summary>
    /// Event raised when IsPresented changes.
    /// </summary>
    public event EventHandler? IsPresentedChanged;

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        // Measure flyout
        if (_flyout != null)
        {
            _flyout.Measure(new SKSize(FlyoutWidth, availableSize.Height));
        }

        // Measure detail to full size
        if (_detail != null)
        {
            _detail.Measure(availableSize);
        }

        return availableSize;
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        // Arrange detail to fill the entire area
        if (_detail != null)
        {
            _detail.Arrange(bounds);
        }

        // Arrange flyout (positioned based on animation progress)
        if (_flyout != null)
        {
            float flyoutX = bounds.Left - FlyoutWidth + (FlyoutWidth * _flyoutAnimationProgress);
            var flyoutBounds = new SKRect(
                flyoutX,
                bounds.Top,
                flyoutX + FlyoutWidth,
                bounds.Bottom);
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

        // If flyout is visible, draw scrim and flyout
        if (_flyoutAnimationProgress > 0)
        {
            // Draw scrim (semi-transparent overlay)
            using var scrimPaint = new SKPaint
            {
                Color = ScrimColor.WithAlpha((byte)(ScrimColor.Alpha * _flyoutAnimationProgress)),
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(Bounds, scrimPaint);

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

        float shadowRight = _flyout.Bounds.Right;
        var shadowRect = new SKRect(
            shadowRight,
            Bounds.Top,
            shadowRight + ShadowWidth,
            Bounds.Bottom);

        using var shadowPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(shadowRect.Left, shadowRect.MidY),
                new SKPoint(shadowRect.Right, shadowRect.MidY),
                new SKColor[] { new SKColor(0, 0, 0, 60), SKColors.Transparent },
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

            // Hit on scrim closes flyout
            if (_isPresented)
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
        if (_isPresented && _flyout != null && !_flyout.Bounds.Contains(e.X, e.Y))
        {
            IsPresented = false;
            e.Handled = true;
            return;
        }

        // Start drag gesture
        if (_gestureEnabled)
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

            IsPresentedChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        base.OnPointerReleased(e);
    }

    /// <summary>
    /// Toggles the flyout presentation state.
    /// </summary>
    public void ToggleFlyout()
    {
        IsPresented = !IsPresented;
    }
}

/// <summary>
/// Defines how the flyout behaves.
/// </summary>
public enum FlyoutLayoutBehavior
{
    /// <summary>
    /// Default behavior based on device/window size.
    /// </summary>
    Default,

    /// <summary>
    /// Flyout slides over the detail content.
    /// </summary>
    Popover,

    /// <summary>
    /// Flyout and detail are shown side by side.
    /// </summary>
    Split,

    /// <summary>
    /// Flyout pushes the detail content.
    /// </summary>
    SplitOnLandscape,

    /// <summary>
    /// Flyout is always shown in portrait, side by side in landscape.
    /// </summary>
    SplitOnPortrait
}
