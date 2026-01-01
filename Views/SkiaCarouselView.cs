// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A horizontally scrolling carousel view with snap-to-item behavior.
/// </summary>
public class SkiaCarouselView : SkiaLayoutView
{
    private readonly List<SkiaView> _items = new();
    private int _currentPosition = 0;
    private float _scrollOffset = 0f;
    private float _targetScrollOffset = 0f;
    private bool _isDragging = false;
    private float _dragStartX;
    private float _dragStartOffset;
    private float _velocity = 0f;
    private DateTime _lastDragTime;
    private float _lastDragX;

    // Animation
    private bool _isAnimating = false;
    private float _animationStartOffset;
    private float _animationTargetOffset;
    private DateTime _animationStartTime;
    private const float AnimationDurationMs = 300f;

    /// <summary>
    /// Gets or sets the current position (item index).
    /// </summary>
    public int Position
    {
        get => _currentPosition;
        set
        {
            if (value >= 0 && value < _items.Count && value != _currentPosition)
            {
                int oldPosition = _currentPosition;
                _currentPosition = value;
                AnimateToPosition(value);
                PositionChanged?.Invoke(this, new PositionChangedEventArgs(oldPosition, value));
            }
        }
    }

    /// <summary>
    /// Gets the item count.
    /// </summary>
    public int ItemCount => _items.Count;

    /// <summary>
    /// Gets or sets whether looping is enabled.
    /// </summary>
    public bool Loop { get; set; } = false;

    /// <summary>
    /// Gets or sets the peek amount (how much of adjacent items to show).
    /// </summary>
    public float PeekAreaInsets { get; set; } = 0f;

    /// <summary>
    /// Gets or sets the spacing between items.
    /// </summary>
    public float ItemSpacing { get; set; } = 0f;

    /// <summary>
    /// Gets or sets whether swipe gestures are enabled.
    /// </summary>
    public bool IsSwipeEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the indicator visibility.
    /// </summary>
    public bool ShowIndicators { get; set; } = true;

    /// <summary>
    /// Gets or sets the indicator color.
    /// </summary>
    public SKColor IndicatorColor { get; set; } = new SKColor(180, 180, 180);

    /// <summary>
    /// Gets or sets the selected indicator color.
    /// </summary>
    public SKColor SelectedIndicatorColor { get; set; } = new SKColor(33, 150, 243);

    /// <summary>
    /// Event raised when position changes.
    /// </summary>
    public event EventHandler<PositionChangedEventArgs>? PositionChanged;

    /// <summary>
    /// Event raised when scrolling.
    /// </summary>
    public event EventHandler? Scrolled;

    /// <summary>
    /// Adds an item to the carousel.
    /// </summary>
    public void AddItem(SkiaView item)
    {
        _items.Add(item);
        AddChild(item);
        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// Removes an item from the carousel.
    /// </summary>
    public void RemoveItem(SkiaView item)
    {
        if (_items.Remove(item))
        {
            RemoveChild(item);
            if (_currentPosition >= _items.Count)
            {
                _currentPosition = Math.Max(0, _items.Count - 1);
            }
            InvalidateMeasure();
            Invalidate();
        }
    }

    /// <summary>
    /// Clears all items.
    /// </summary>
    public void ClearItems()
    {
        foreach (var item in _items)
        {
            RemoveChild(item);
        }
        _items.Clear();
        _currentPosition = 0;
        _scrollOffset = 0;
        _targetScrollOffset = 0;
        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// Scrolls to the specified position.
    /// </summary>
    public void ScrollTo(int position, bool animate = true)
    {
        if (position < 0 || position >= _items.Count) return;

        int oldPosition = _currentPosition;
        _currentPosition = position;

        if (animate)
        {
            AnimateToPosition(position);
        }
        else
        {
            _scrollOffset = GetOffsetForPosition(position);
            _targetScrollOffset = _scrollOffset;
            Invalidate();
        }

        if (oldPosition != position)
        {
            PositionChanged?.Invoke(this, new PositionChangedEventArgs(oldPosition, position));
        }
    }

    private void AnimateToPosition(int position)
    {
        _animationStartOffset = _scrollOffset;
        _animationTargetOffset = GetOffsetForPosition(position);
        _animationStartTime = DateTime.UtcNow;
        _isAnimating = true;
        Invalidate();
    }

    private float GetOffsetForPosition(int position)
    {
        float itemWidth = Bounds.Width - PeekAreaInsets * 2;
        return position * (itemWidth + ItemSpacing);
    }

    private int GetPositionForOffset(float offset)
    {
        float itemWidth = Bounds.Width - PeekAreaInsets * 2;
        if (itemWidth <= 0) return 0;
        return Math.Clamp((int)Math.Round(offset / (itemWidth + ItemSpacing)), 0, Math.Max(0, _items.Count - 1));
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        float itemWidth = availableSize.Width - PeekAreaInsets * 2;
        float itemHeight = availableSize.Height - (ShowIndicators ? 30 : 0);

        foreach (var item in _items)
        {
            item.Measure(new SKSize(itemWidth, itemHeight));
        }

        return availableSize;
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        float itemWidth = bounds.Width - PeekAreaInsets * 2;
        float itemHeight = bounds.Height - (ShowIndicators ? 30 : 0);

        for (int i = 0; i < _items.Count; i++)
        {
            float x = bounds.Left + PeekAreaInsets + i * (itemWidth + ItemSpacing) - _scrollOffset;
            var itemBounds = new SKRect(x, bounds.Top, x + itemWidth, bounds.Top + itemHeight);
            _items[i].Arrange(itemBounds);
        }

        return bounds;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Update animation
        if (_isAnimating)
        {
            float elapsed = (float)(DateTime.UtcNow - _animationStartTime).TotalMilliseconds;
            float progress = Math.Clamp(elapsed / AnimationDurationMs, 0f, 1f);

            // Ease out cubic
            float t = 1f - (1f - progress) * (1f - progress) * (1f - progress);

            _scrollOffset = _animationStartOffset + (_animationTargetOffset - _animationStartOffset) * t;

            if (progress >= 1f)
            {
                _isAnimating = false;
                _scrollOffset = _animationTargetOffset;
            }
            else
            {
                Invalidate(); // Continue animation
            }
        }

        canvas.Save();
        canvas.ClipRect(bounds);

        // Draw visible items
        float itemWidth = bounds.Width - PeekAreaInsets * 2;
        float contentHeight = bounds.Height - (ShowIndicators ? 30 : 0);

        for (int i = 0; i < _items.Count; i++)
        {
            float x = bounds.Left + PeekAreaInsets + i * (itemWidth + ItemSpacing) - _scrollOffset;

            // Only draw visible items
            if (x + itemWidth > bounds.Left && x < bounds.Right)
            {
                _items[i].Draw(canvas);
            }
        }

        // Draw indicators
        if (ShowIndicators && _items.Count > 1)
        {
            DrawIndicators(canvas, bounds);
        }

        canvas.Restore();
    }

    private void DrawIndicators(SKCanvas canvas, SKRect bounds)
    {
        float indicatorSize = 8f;
        float indicatorSpacing = 12f;
        float totalWidth = _items.Count * indicatorSize + (_items.Count - 1) * (indicatorSpacing - indicatorSize);
        float startX = bounds.MidX - totalWidth / 2;
        float y = bounds.Bottom - 15;

        using var normalPaint = new SKPaint
        {
            Color = IndicatorColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var selectedPaint = new SKPaint
        {
            Color = SelectedIndicatorColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        for (int i = 0; i < _items.Count; i++)
        {
            float x = startX + i * indicatorSpacing;
            var paint = i == _currentPosition ? selectedPaint : normalPaint;
            canvas.DrawCircle(x, y, indicatorSize / 2, paint);
        }
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !Bounds.Contains(x, y)) return null;

        // Check items
        foreach (var item in _items)
        {
            var hit = item.HitTest(x, y);
            if (hit != null) return hit;
        }

        return this;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled || !IsSwipeEnabled) return;

        _isDragging = true;
        _dragStartX = e.X;
        _dragStartOffset = _scrollOffset;
        _lastDragX = e.X;
        _lastDragTime = DateTime.UtcNow;
        _velocity = 0;
        _isAnimating = false;

        e.Handled = true;
        base.OnPointerPressed(e);
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_isDragging) return;

        float delta = _dragStartX - e.X;
        _scrollOffset = _dragStartOffset + delta;

        // Clamp scrolling
        float maxOffset = GetOffsetForPosition(_items.Count - 1);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, maxOffset);

        // Calculate velocity
        var now = DateTime.UtcNow;
        float timeDelta = (float)(now - _lastDragTime).TotalSeconds;
        if (timeDelta > 0)
        {
            _velocity = (_lastDragX - e.X) / timeDelta;
        }
        _lastDragX = e.X;
        _lastDragTime = now;

        Scrolled?.Invoke(this, EventArgs.Empty);
        Invalidate();
        e.Handled = true;

        base.OnPointerMoved(e);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (!_isDragging) return;

        _isDragging = false;

        // Determine target position based on velocity and position
        float itemWidth = Bounds.Width - PeekAreaInsets * 2;
        int targetPosition = GetPositionForOffset(_scrollOffset);

        // Apply velocity influence
        if (Math.Abs(_velocity) > 500)
        {
            if (_velocity > 0 && targetPosition < _items.Count - 1)
            {
                targetPosition++;
            }
            else if (_velocity < 0 && targetPosition > 0)
            {
                targetPosition--;
            }
        }

        ScrollTo(targetPosition, true);
        e.Handled = true;

        base.OnPointerReleased(e);
    }
}

/// <summary>
/// Event args for position changed events.
/// </summary>
public class PositionChangedEventArgs : EventArgs
{
    public int PreviousPosition { get; }
    public int CurrentPosition { get; }

    public PositionChangedEventArgs(int previousPosition, int currentPosition)
    {
        PreviousPosition = previousPosition;
        CurrentPosition = currentPosition;
    }
}
