// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered scroll view container with full XAML styling support.
/// </summary>
public class SkiaScrollView : SkiaView
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for Orientation.
    /// </summary>
    public static readonly BindableProperty OrientationProperty =
        BindableProperty.Create(
            nameof(Orientation),
            typeof(ScrollOrientation),
            typeof(SkiaScrollView),
            ScrollOrientation.Both,
            propertyChanged: (b, o, n) => ((SkiaScrollView)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for HorizontalScrollBarVisibility.
    /// </summary>
    public static readonly BindableProperty HorizontalScrollBarVisibilityProperty =
        BindableProperty.Create(
            nameof(HorizontalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(SkiaScrollView),
            ScrollBarVisibility.Auto,
            propertyChanged: (b, o, n) => ((SkiaScrollView)b).Invalidate());

    /// <summary>
    /// Bindable property for VerticalScrollBarVisibility.
    /// </summary>
    public static readonly BindableProperty VerticalScrollBarVisibilityProperty =
        BindableProperty.Create(
            nameof(VerticalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(SkiaScrollView),
            ScrollBarVisibility.Auto,
            propertyChanged: (b, o, n) => ((SkiaScrollView)b).Invalidate());

    /// <summary>
    /// Bindable property for ScrollBarColor.
    /// </summary>
    public static readonly BindableProperty ScrollBarColorProperty =
        BindableProperty.Create(
            nameof(ScrollBarColor),
            typeof(SKColor),
            typeof(SkiaScrollView),
            new SKColor(0x80, 0x80, 0x80, 0x80),
            propertyChanged: (b, o, n) => ((SkiaScrollView)b).Invalidate());

    /// <summary>
    /// Bindable property for ScrollBarWidth.
    /// </summary>
    public static readonly BindableProperty ScrollBarWidthProperty =
        BindableProperty.Create(
            nameof(ScrollBarWidth),
            typeof(float),
            typeof(SkiaScrollView),
            8f,
            propertyChanged: (b, o, n) => ((SkiaScrollView)b).Invalidate());

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the scroll orientation.
    /// </summary>
    public ScrollOrientation Orientation
    {
        get => (ScrollOrientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show horizontal scrollbar.
    /// </summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show vertical scrollbar.
    /// </summary>
    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    /// <summary>
    /// Scrollbar color.
    /// </summary>
    public SKColor ScrollBarColor
    {
        get => (SKColor)GetValue(ScrollBarColorProperty);
        set => SetValue(ScrollBarColorProperty, value);
    }

    /// <summary>
    /// Scrollbar width.
    /// </summary>
    public float ScrollBarWidth
    {
        get => (float)GetValue(ScrollBarWidthProperty);
        set => SetValue(ScrollBarWidthProperty, value);
    }

    #endregion

    private SkiaView? _content;
    private float _scrollX;
    private float _scrollY;
    private float _velocityX;
    private float _velocityY;
    private bool _isDragging;
    private bool _isDraggingVerticalScrollbar;
    private bool _isDraggingHorizontalScrollbar;
    private float _scrollbarDragStartY;
    private float _scrollbarDragStartScrollY;
    private float _scrollbarDragStartX;
    private float _scrollbarDragStartScrollX;
    private float _scrollbarDragAvailableTrack; // Cache to prevent stutter
    private float _scrollbarDragScrollableExtent; // Cache to prevent stutter
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
                {
                    _content.Parent = this;

                    // Propagate binding context to new content
                    if (BindingContext != null)
                    {
                        SetInheritedBindingContext(_content, BindingContext);
                    }
                }

                InvalidateMeasure();
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Called when binding context changes. Propagates to content.
    /// </summary>
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        // Propagate binding context to content
        if (_content != null)
        {
            SetInheritedBindingContext(_content, BindingContext);
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
    public float ScrollableWidth
    {
        get
        {
            // Handle infinite or NaN bounds - use a reasonable default viewport
            var viewportWidth = float.IsInfinity(Bounds.Width) || float.IsNaN(Bounds.Width) || Bounds.Width <= 0
                ? 800f
                : Bounds.Width;
            return Math.Max(0, ContentSize.Width - viewportWidth);
        }
    }

    /// <summary>
    /// Gets the maximum vertical scroll extent.
    /// </summary>
    public float ScrollableHeight
    {
        get
        {
            // Handle infinite, NaN, or unreasonably large bounds - use a reasonable default viewport
            var boundsHeight = Bounds.Height;
            var viewportHeight = (float.IsInfinity(boundsHeight) || float.IsNaN(boundsHeight) || boundsHeight <= 0 || boundsHeight > 10000)
                ? 544f  // Default viewport height (600 - 56 for shell header)
                : boundsHeight;
            return Math.Max(0, ContentSize.Height - viewportHeight);
        }
    }

    /// <summary>
    /// Gets the content size.
    /// </summary>
    public SKSize ContentSize { get; private set; }

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
            // Ensure content is measured and arranged
            var availableSize = new SKSize(bounds.Width, float.PositiveInfinity);
            _content.Measure(availableSize);

            // Apply content's margin
            var margin = _content.Margin;
            var contentBounds = new SKRect(
                bounds.Left + (float)margin.Left,
                bounds.Top + (float)margin.Top,
                bounds.Left + Math.Max(bounds.Width, _content.DesiredSize.Width) - (float)margin.Right,
                bounds.Top + Math.Max(bounds.Height, _content.DesiredSize.Height) - (float)margin.Bottom);
            _content.Arrange(contentBounds);

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
        Console.WriteLine($"[SkiaScrollView] OnScroll - DeltaY={e.DeltaY}, ScrollableHeight={ScrollableHeight}, ContentSize={ContentSize}, Bounds={Bounds}");

        // Handle mouse wheel scrolling
        var deltaMultiplier = 40f; // Scroll speed
        bool scrolled = false;

        if (Orientation != ScrollOrientation.Horizontal && ScrollableHeight > 0)
        {
            var oldScrollY = _scrollY;
            ScrollY += e.DeltaY * deltaMultiplier;
            Console.WriteLine($"[SkiaScrollView] ScrollY changed: {oldScrollY} -> {_scrollY}");
            if (_scrollY != oldScrollY)
                scrolled = true;
        }

        if (Orientation != ScrollOrientation.Vertical && ScrollableWidth > 0)
        {
            var oldScrollX = _scrollX;
            ScrollX += e.DeltaX * deltaMultiplier;
            if (_scrollX != oldScrollX)
                scrolled = true;
        }

        // Mark as handled so parent scroll views don't also scroll
        if (scrolled)
            e.Handled = true;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        // Check if clicking on vertical scrollbar thumb
        if (ShouldShowVerticalScrollbar() && ScrollableHeight > 0)
        {
            var thumbBounds = GetVerticalScrollbarThumbBounds();
            if (thumbBounds.Contains(e.X, e.Y))
            {
                _isDraggingVerticalScrollbar = true;
                _scrollbarDragStartY = e.Y;
                _scrollbarDragStartScrollY = _scrollY;
                // Cache values to prevent stutter from floating-point recalculations
                var hasHorizontal = ShouldShowHorizontalScrollbar();
                var trackHeight = Bounds.Height - (hasHorizontal ? ScrollBarWidth : 0);
                var thumbHeight = Math.Max(20, (Bounds.Height / ContentSize.Height) * trackHeight);
                _scrollbarDragAvailableTrack = trackHeight - thumbHeight;
                _scrollbarDragScrollableExtent = ScrollableHeight;
                return;
            }
        }

        // Check if clicking on horizontal scrollbar thumb
        if (ShouldShowHorizontalScrollbar() && ScrollableWidth > 0)
        {
            var thumbBounds = GetHorizontalScrollbarThumbBounds();
            if (thumbBounds.Contains(e.X, e.Y))
            {
                _isDraggingHorizontalScrollbar = true;
                _scrollbarDragStartX = e.X;
                _scrollbarDragStartScrollX = _scrollX;
                // Cache values to prevent stutter from floating-point recalculations
                var hasVertical = ShouldShowVerticalScrollbar();
                var trackWidth = Bounds.Width - (hasVertical ? ScrollBarWidth : 0);
                var thumbWidth = Math.Max(20, (Bounds.Width / ContentSize.Width) * trackWidth);
                _scrollbarDragAvailableTrack = trackWidth - thumbWidth;
                _scrollbarDragScrollableExtent = ScrollableWidth;
                return;
            }
        }

        // Forward click to content first
        if (_content != null)
        {
            // Translate coordinates for scroll offset
            var contentE = new PointerEventArgs(e.X + _scrollX, e.Y + _scrollY, e.Button);
            var hit = _content.HitTest(contentE.X, contentE.Y);
            if (hit != null && hit != _content)
            {
                // A child view was hit - forward the event to it
                hit.OnPointerPressed(contentE);
                return;
            }
        }

        // Regular content dragging
        _isDragging = true;
        _lastPointerX = e.X;
        _lastPointerY = e.Y;
        _velocityX = 0;
        _velocityY = 0;
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        // Handle vertical scrollbar dragging - use cached values to prevent stutter
        if (_isDraggingVerticalScrollbar)
        {
            if (_scrollbarDragAvailableTrack > 0)
            {
                var deltaY = e.Y - _scrollbarDragStartY;
                var scrollDelta = (deltaY / _scrollbarDragAvailableTrack) * _scrollbarDragScrollableExtent;
                ScrollY = _scrollbarDragStartScrollY + scrollDelta;
            }
            return;
        }

        // Handle horizontal scrollbar dragging - use cached values to prevent stutter
        if (_isDraggingHorizontalScrollbar)
        {
            if (_scrollbarDragAvailableTrack > 0)
            {
                var deltaX = e.X - _scrollbarDragStartX;
                var scrollDelta = (deltaX / _scrollbarDragAvailableTrack) * _scrollbarDragScrollableExtent;
                ScrollX = _scrollbarDragStartScrollX + scrollDelta;
            }
            return;
        }

        // Handle content dragging
        if (!_isDragging) return;

        var contentDeltaX = _lastPointerX - e.X;
        var contentDeltaY = _lastPointerY - e.Y;

        _velocityX = contentDeltaX;
        _velocityY = contentDeltaY;

        if (Orientation != ScrollOrientation.Horizontal)
            ScrollY += contentDeltaY;

        if (Orientation != ScrollOrientation.Vertical)
            ScrollX += contentDeltaX;

        _lastPointerX = e.X;
        _lastPointerY = e.Y;
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        _isDragging = false;
        _isDraggingVerticalScrollbar = false;
        _isDraggingHorizontalScrollbar = false;
        // Momentum scrolling could be added here
    }

    private SKRect GetVerticalScrollbarThumbBounds()
    {
        var hasHorizontal = ShouldShowHorizontalScrollbar();
        var trackHeight = Bounds.Height - (hasHorizontal ? ScrollBarWidth : 0);
        var thumbHeight = Math.Max(20, (Bounds.Height / ContentSize.Height) * trackHeight);
        var thumbY = ScrollableHeight > 0 ? (ScrollY / ScrollableHeight) * (trackHeight - thumbHeight) : 0;

        return new SKRect(
            Bounds.Right - ScrollBarWidth,
            Bounds.Top + thumbY,
            Bounds.Right,
            Bounds.Top + thumbY + thumbHeight);
    }

    private SKRect GetHorizontalScrollbarThumbBounds()
    {
        var hasVertical = ShouldShowVerticalScrollbar();
        var trackWidth = Bounds.Width - (hasVertical ? ScrollBarWidth : 0);
        var thumbWidth = Math.Max(20, (Bounds.Width / ContentSize.Width) * trackWidth);
        var thumbX = ScrollableWidth > 0 ? (ScrollX / ScrollableWidth) * (trackWidth - thumbWidth) : 0;

        return new SKRect(
            Bounds.Left + thumbX,
            Bounds.Bottom - ScrollBarWidth,
            Bounds.Left + thumbX + thumbWidth,
            Bounds.Bottom);
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !IsEnabled || !Bounds.Contains(new SKPoint(x, y)))
            return null;

        // Check scrollbar areas FIRST before content
        // This ensures scrollbar clicks are handled by the ScrollView, not content underneath
        if (ShouldShowVerticalScrollbar() && ScrollableHeight > 0)
        {
            var thumbBounds = GetVerticalScrollbarThumbBounds();
            // Check if click is in the scrollbar track area (not just thumb)
            var trackArea = new SKRect(Bounds.Right - ScrollBarWidth, Bounds.Top, Bounds.Right, Bounds.Bottom);
            if (trackArea.Contains(x, y))
                return this;
        }

        if (ShouldShowHorizontalScrollbar() && ScrollableWidth > 0)
        {
            var trackArea = new SKRect(Bounds.Left, Bounds.Bottom - ScrollBarWidth, Bounds.Right, Bounds.Bottom);
            if (trackArea.Contains(x, y))
                return this;
        }

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
            // For responsive layout:
            // - Vertical: give content viewport width, infinite height
            // - Horizontal: give content infinite width, viewport height
            // - Both: give content viewport width first (for responsive layout),
            //         but if content exceeds it, horizontal scrollbar appears
            // - Neither: give content exact viewport size

            float contentWidth, contentHeight;

            switch (Orientation)
            {
                case ScrollOrientation.Horizontal:
                    contentWidth = float.PositiveInfinity;
                    contentHeight = float.IsInfinity(availableSize.Height) ? 400f : availableSize.Height;
                    break;
                case ScrollOrientation.Neither:
                    contentWidth = float.IsInfinity(availableSize.Width) ? 400f : availableSize.Width;
                    contentHeight = float.IsInfinity(availableSize.Height) ? 400f : availableSize.Height;
                    break;
                case ScrollOrientation.Both:
                    // For Both: first measure with viewport width to get responsive layout
                    // Content can still exceed viewport if it has minimum width constraints
                    contentWidth = float.IsInfinity(availableSize.Width) ? 800f : availableSize.Width;
                    contentHeight = float.PositiveInfinity;
                    break;
                case ScrollOrientation.Vertical:
                default:
                    contentWidth = float.IsInfinity(availableSize.Width) ? 800f : availableSize.Width;
                    contentHeight = float.PositiveInfinity;
                    break;
            }

            ContentSize = _content.Measure(new SKSize(contentWidth, contentHeight));
        }
        else
        {
            ContentSize = SKSize.Empty;
        }

        // Return available size, but clamp infinite dimensions
        // IMPORTANT: When available is infinite, return a reasonable viewport size, NOT content size
        // A ScrollView should NOT expand to fit its content - it should stay at a fixed viewport
        // and scroll the content. Use a default viewport size when parent gives infinity.
        const float DefaultViewportWidth = 400f;
        const float DefaultViewportHeight = 400f;

        var width = float.IsInfinity(availableSize.Width) || float.IsNaN(availableSize.Width)
            ? Math.Min(ContentSize.Width, DefaultViewportWidth)
            : availableSize.Width;
        var height = float.IsInfinity(availableSize.Height) || float.IsNaN(availableSize.Height)
            ? Math.Min(ContentSize.Height, DefaultViewportHeight)
            : availableSize.Height;

        return new SKSize(width, height);
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {

        // CRITICAL: If bounds has infinite height, use a fixed viewport size
        // NOT ContentSize.Height - that would make ScrollableHeight = 0
        const float DefaultViewportHeight = 544f; // 600 - 56 for shell header
        var actualBounds = bounds;
        if (float.IsInfinity(bounds.Height) || float.IsNaN(bounds.Height))
        {
            Console.WriteLine($"[SkiaScrollView] WARNING: Infinite/NaN height, using default viewport={DefaultViewportHeight}");
            actualBounds = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + DefaultViewportHeight);
        }

        if (_content != null)
        {
            // Apply content's margin and arrange content at its full size
            var margin = _content.Margin;
            var contentBounds = new SKRect(
                actualBounds.Left + (float)margin.Left,
                actualBounds.Top + (float)margin.Top,
                actualBounds.Left + Math.Max(actualBounds.Width, ContentSize.Width) - (float)margin.Right,
                actualBounds.Top + Math.Max(actualBounds.Height, ContentSize.Height) - (float)margin.Bottom);

            _content.Arrange(contentBounds);
        }
        return actualBounds;
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
