// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered scroll view container.
/// </summary>
public class SkiaScrollView : SkiaView
{
    private SkiaView? _content;
    private float _scrollX;
    private float _scrollY;
    private float _velocityX;
    private float _velocityY;
    private bool _isDragging;
    private float _lastPointerX;
    private float _lastPointerY;

    /// <summary>
    /// Gets or sets the content view.
    /// </summary>
    public SkiaView? Content
    {
        get => _content;
        set
        {
            if (_content != value)
            {
                if (_content != null)
                    _content.Parent = null;

                _content = value;

                if (_content != null)
                    _content.Parent = this;

                InvalidateMeasure();
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the horizontal scroll position.
    /// </summary>
    public float ScrollX
    {
        get => _scrollX;
        set
        {
            var clamped = ClampScrollX(value);
            if (_scrollX != clamped)
            {
                _scrollX = clamped;
                Scrolled?.Invoke(this, new ScrolledEventArgs(_scrollX, _scrollY));
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the vertical scroll position.
    /// </summary>
    public float ScrollY
    {
        get => _scrollY;
        set
        {
            var clamped = ClampScrollY(value);
            if (_scrollY != clamped)
            {
                _scrollY = clamped;
                Scrolled?.Invoke(this, new ScrolledEventArgs(_scrollX, _scrollY));
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets the maximum horizontal scroll extent.
    /// </summary>
    public float ScrollableWidth => Math.Max(0, ContentSize.Width - Bounds.Width);

    /// <summary>
    /// Gets the maximum vertical scroll extent.
    /// </summary>
    public float ScrollableHeight => Math.Max(0, ContentSize.Height - Bounds.Height);

    /// <summary>
    /// Gets the content size.
    /// </summary>
    public SKSize ContentSize { get; private set; }

    /// <summary>
    /// Gets or sets the scroll orientation.
    /// </summary>
    public ScrollOrientation Orientation { get; set; } = ScrollOrientation.Both;

    /// <summary>
    /// Gets or sets whether to show horizontal scrollbar.
    /// </summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; } = ScrollBarVisibility.Auto;

    /// <summary>
    /// Gets or sets whether to show vertical scrollbar.
    /// </summary>
    public ScrollBarVisibility VerticalScrollBarVisibility { get; set; } = ScrollBarVisibility.Auto;

    /// <summary>
    /// Scrollbar color.
    /// </summary>
    public SKColor ScrollBarColor { get; set; } = new SKColor(0x80, 0x80, 0x80, 0x80);

    /// <summary>
    /// Scrollbar width.
    /// </summary>
    public float ScrollBarWidth { get; set; } = 8;

    /// <summary>
    /// Event raised when scroll position changes.
    /// </summary>
    public event EventHandler<ScrolledEventArgs>? Scrolled;

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Clip to bounds
        canvas.Save();
        canvas.ClipRect(bounds);

        // Draw content with scroll offset
        if (_content != null)
        {
            canvas.Save();
            canvas.Translate(-_scrollX, -_scrollY);
            _content.Draw(canvas);
            canvas.Restore();
        }

        // Draw scrollbars
        DrawScrollbars(canvas, bounds);

        canvas.Restore();
    }

    private void DrawScrollbars(SKCanvas canvas, SKRect bounds)
    {
        var showVertical = ShouldShowVerticalScrollbar();
        var showHorizontal = ShouldShowHorizontalScrollbar();

        if (showVertical && ScrollableHeight > 0)
        {
            DrawVerticalScrollbar(canvas, bounds, showHorizontal);
        }

        if (showHorizontal && ScrollableWidth > 0)
        {
            DrawHorizontalScrollbar(canvas, bounds, showVertical);
        }
    }

    private bool ShouldShowVerticalScrollbar()
    {
        if (Orientation == ScrollOrientation.Horizontal) return false;

        return VerticalScrollBarVisibility switch
        {
            ScrollBarVisibility.Always => true,
            ScrollBarVisibility.Never => false,
            _ => ScrollableHeight > 0
        };
    }

    private bool ShouldShowHorizontalScrollbar()
    {
        if (Orientation == ScrollOrientation.Vertical) return false;

        return HorizontalScrollBarVisibility switch
        {
            ScrollBarVisibility.Always => true,
            ScrollBarVisibility.Never => false,
            _ => ScrollableWidth > 0
        };
    }

    private void DrawVerticalScrollbar(SKCanvas canvas, SKRect bounds, bool hasHorizontal)
    {
        var trackHeight = bounds.Height - (hasHorizontal ? ScrollBarWidth : 0);
        var thumbHeight = Math.Max(20, (bounds.Height / ContentSize.Height) * trackHeight);
        var thumbY = (ScrollY / ScrollableHeight) * (trackHeight - thumbHeight);

        using var paint = new SKPaint
        {
            Color = ScrollBarColor,
            IsAntialias = true
        };

        var thumbRect = new SKRoundRect(
            new SKRect(
                bounds.Right - ScrollBarWidth,
                bounds.Top + thumbY,
                bounds.Right,
                bounds.Top + thumbY + thumbHeight),
            ScrollBarWidth / 2);

        canvas.DrawRoundRect(thumbRect, paint);
    }

    private void DrawHorizontalScrollbar(SKCanvas canvas, SKRect bounds, bool hasVertical)
    {
        var trackWidth = bounds.Width - (hasVertical ? ScrollBarWidth : 0);
        var thumbWidth = Math.Max(20, (bounds.Width / ContentSize.Width) * trackWidth);
        var thumbX = (ScrollX / ScrollableWidth) * (trackWidth - thumbWidth);

        using var paint = new SKPaint
        {
            Color = ScrollBarColor,
            IsAntialias = true
        };

        var thumbRect = new SKRoundRect(
            new SKRect(
                bounds.Left + thumbX,
                bounds.Bottom - ScrollBarWidth,
                bounds.Left + thumbX + thumbWidth,
                bounds.Bottom),
            ScrollBarWidth / 2);

        canvas.DrawRoundRect(thumbRect, paint);
    }

    public override void OnScroll(ScrollEventArgs e)
    {
        // Handle mouse wheel scrolling
        var deltaMultiplier = 40f; // Scroll speed

        if (Orientation != ScrollOrientation.Horizontal)
        {
            ScrollY += e.DeltaY * deltaMultiplier;
        }

        if (Orientation != ScrollOrientation.Vertical)
        {
            ScrollX += e.DeltaX * deltaMultiplier;
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        _isDragging = true;
        _lastPointerX = e.X;
        _lastPointerY = e.Y;
        _velocityX = 0;
        _velocityY = 0;
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_isDragging) return;

        var deltaX = _lastPointerX - e.X;
        var deltaY = _lastPointerY - e.Y;

        _velocityX = deltaX;
        _velocityY = deltaY;

        if (Orientation != ScrollOrientation.Horizontal)
            ScrollY += deltaY;

        if (Orientation != ScrollOrientation.Vertical)
            ScrollX += deltaX;

        _lastPointerX = e.X;
        _lastPointerY = e.Y;
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        _isDragging = false;
        // Momentum scrolling could be added here
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !Bounds.Contains(new SKPoint(x, y)))
            return null;

        // Hit test content with scroll offset
        if (_content != null)
        {
            var hit = _content.HitTest(x + _scrollX, y + _scrollY);
            if (hit != null)
                return hit;
        }

        return this;
    }

    /// <summary>
    /// Scrolls to the specified position.
    /// </summary>
    public void ScrollTo(float x, float y, bool animated = false)
    {
        // TODO: Implement animation
        ScrollX = x;
        ScrollY = y;
    }

    /// <summary>
    /// Scrolls to make the specified view visible.
    /// </summary>
    public void ScrollToView(SkiaView view, bool animated = false)
    {
        if (_content == null) return;

        var viewBounds = view.Bounds;

        // Check if view is fully visible
        var visibleRect = new SKRect(
            ScrollX,
            ScrollY,
            ScrollX + Bounds.Width,
            ScrollY + Bounds.Height);

        if (visibleRect.Contains(viewBounds))
            return;

        // Calculate scroll position to bring view into view
        float targetX = ScrollX;
        float targetY = ScrollY;

        if (viewBounds.Left < visibleRect.Left)
            targetX = viewBounds.Left;
        else if (viewBounds.Right > visibleRect.Right)
            targetX = viewBounds.Right - Bounds.Width;

        if (viewBounds.Top < visibleRect.Top)
            targetY = viewBounds.Top;
        else if (viewBounds.Bottom > visibleRect.Bottom)
            targetY = viewBounds.Bottom - Bounds.Height;

        ScrollTo(targetX, targetY, animated);
    }

    private float ClampScrollX(float value)
    {
        if (Orientation == ScrollOrientation.Vertical) return 0;
        return Math.Clamp(value, 0, ScrollableWidth);
    }

    private float ClampScrollY(float value)
    {
        if (Orientation == ScrollOrientation.Horizontal) return 0;
        return Math.Clamp(value, 0, ScrollableHeight);
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        if (_content != null)
        {
            // Give content unlimited size in scrollable directions
            var contentAvailable = new SKSize(
                Orientation == ScrollOrientation.Vertical ? availableSize.Width : float.PositiveInfinity,
                Orientation == ScrollOrientation.Horizontal ? availableSize.Height : float.PositiveInfinity);

            ContentSize = _content.Measure(contentAvailable);
        }
        else
        {
            ContentSize = SKSize.Empty;
        }

        return availableSize;
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        if (_content != null)
        {
            // Arrange content at its full size, starting from scroll position
            var contentBounds = new SKRect(
                bounds.Left,
                bounds.Top,
                bounds.Left + Math.Max(bounds.Width, ContentSize.Width),
                bounds.Top + Math.Max(bounds.Height, ContentSize.Height));

            _content.Arrange(contentBounds);
        }
        return bounds;
    }
}

/// <summary>
/// Scroll orientation options.
/// </summary>
public enum ScrollOrientation
{
    Vertical,
    Horizontal,
    Both,
    Neither
}

/// <summary>
/// Scrollbar visibility options.
/// </summary>
public enum ScrollBarVisibility
{
    Default,
    Always,
    Never,
    Auto
}

/// <summary>
/// Event args for scroll events.
/// </summary>
public class ScrolledEventArgs : EventArgs
{
    public float ScrollX { get; }
    public float ScrollY { get; }

    public ScrolledEventArgs(float scrollX, float scrollY)
    {
        ScrollX = scrollX;
        ScrollY = scrollY;
    }
}
