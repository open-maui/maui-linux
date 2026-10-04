// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A carousel: one item at a time across its width (or down its height, when
/// <see cref="Orientation"/> is vertical), moved by dragging, snapping to an item.
/// </summary>
public class SkiaCarouselView : SkiaLayoutView, IPointerDragInterceptor
{
    private readonly List<SkiaView> _items = new();
    private int _currentPosition = 0;
    // Along the scroll axis. Unbounded while looping (any multiple of the strip length is the same place).
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

    // Indicator color fields
    private Color _indicatorColor = Color.FromRgb(180, 180, 180);
    private Color _selectedIndicatorColor = Color.FromRgb(33, 150, 243);
    private SKColor _indicatorColorSK = SkiaTheme.IndicatorUnselectedSK;
    private SKColor _selectedIndicatorColorSK = SkiaTheme.PrimarySK;

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

    private bool _loop;

    /// <summary>
    /// Loops: after the last item comes the first again (and before the first, the last), in both
    /// directions, as MAUI's CarouselView.Loop does. Needs more than one item.
    /// </summary>
    public bool Loop
    {
        get => _loop;
        set
        {
            if (_loop == value)
                return;
            _loop = value;
            _isAnimating = false;
            _scrollOffset = _targetScrollOffset = GetOffsetForPosition(_currentPosition);
            Invalidate();
        }
    }

    private ItemsLayoutOrientation _orientation = ItemsLayoutOrientation.Horizontal;

    /// <summary>
    /// The direction the items follow each other: across (Horizontal, the default) or down
    /// (Vertical), as the CarouselView's LinearItemsLayout orientation sets it.
    /// </summary>
    public ItemsLayoutOrientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation == value)
                return;
            _orientation = value;
            _isAnimating = false;
            _scrollOffset = _targetScrollOffset = 0;
            InvalidateMeasure();
            Invalidate();
            // Keep the current item in view along the new axis once laid out.
            _scrollOffset = _targetScrollOffset = GetOffsetForPosition(_currentPosition);
        }
    }

    private bool IsVertical => _orientation == ItemsLayoutOrientation.Vertical;

    /// <summary>
    /// Gets or sets the peek amount (how much of adjacent items to show).
    /// </summary>
    public double PeekAreaInsets { get; set; } = 0.0;

    /// <summary>
    /// Gets or sets the spacing between items.
    /// </summary>
    public double ItemSpacing { get; set; } = 0.0;

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
    public Color IndicatorColor
    {
        get => _indicatorColor;
        set
        {
            _indicatorColor = value;
            _indicatorColorSK = value.ToSKColor();
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the selected indicator color.
    /// </summary>
    public Color SelectedIndicatorColor
    {
        get => _selectedIndicatorColor;
        set
        {
            _selectedIndicatorColor = value;
            _selectedIndicatorColorSK = value.ToSKColor();
            Invalidate();
        }
    }

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
    /// Inserts an item at <paramref name="index"/> (an item added to the CarouselView's collection
    /// in place, without making the others again). The position is not changed; the owner sets it.
    /// </summary>
    public void InsertItem(int index, SkiaView item)
    {
        index = Math.Clamp(index, 0, _items.Count);
        _items.Insert(index, item);
        AddChild(item);
        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>Removes the item at <paramref name="index"/> (see <see cref="InsertItem"/>).</summary>
    public void RemoveItemAt(int index)
    {
        if (index < 0 || index >= _items.Count)
            return;
        var item = _items[index];
        _items.RemoveAt(index);
        RemoveChild(item);
        if (_currentPosition >= _items.Count)
            _currentPosition = Math.Max(0, _items.Count - 1);
        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>The item views, in order.</summary>
    public IReadOnlyList<SkiaView> Items => _items;

    private SkiaView? _emptyViewContent;

    /// <summary>
    /// The view shown while the carousel has no items (the EmptyView, or the EmptyViewTemplate's
    /// content), placed by its own alignment and taking input, as MAUI's CarouselView shows it.
    /// </summary>
    public SkiaView? EmptyViewContent
    {
        get => _emptyViewContent;
        set
        {
            if (ReferenceEquals(_emptyViewContent, value))
                return;
            if (_emptyViewContent != null && ReferenceEquals(_emptyViewContent.Parent, this))
                _emptyViewContent.Parent = null;
            _emptyViewContent = value;
            if (value != null)
                value.Parent = this;
            InvalidateMeasure();
            Invalidate();
        }
    }

    /// <summary>Text shown while the carousel has no items (a string EmptyView without a template).</summary>
    public string? EmptyViewText
    {
        get => _emptyViewText;
        set
        {
            _emptyViewText = value;
            Invalidate();
        }
    }

    private string? _emptyViewText;

    private bool ShowsEmptyView => _items.Count == 0 && _emptyViewContent is { IsVisible: true };

    /// <summary>
    /// The horizontal scroll bar of a horizontal carousel: Default shows it while the carousel is
    /// dragged or moving to an item (Windows shows its auto-hiding bar as the carousel is
    /// scrolled), Always keeps it shown, Never hides it. A looping carousel shows none, as on
    /// Windows. A vertical carousel does not scroll across, so it never shows one.
    /// </summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; } = ScrollBarVisibility.Default;

    /// <summary>The vertical scroll bar of a vertical carousel; see <see cref="HorizontalScrollBarVisibility"/>.</summary>
    public ScrollBarVisibility VerticalScrollBarVisibility { get; set; } = ScrollBarVisibility.Default;

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
            _isAnimating = false;
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
        _animationTargetOffset = NearestOffsetFor(position);
        _animationStartTime = DateTime.UtcNow;
        _isAnimating = true;
        Invalidate();
    }

    private bool Loops => _loop && _items.Count > 1;

    /// <summary>The viewport length along the scroll axis.</summary>
    private float Viewport => _arrangeViewport ?? (float)(IsVertical ? Bounds.Height : Bounds.Width);

    // The viewport being arranged to (Bounds takes it only after ArrangeOverride returns).
    private float? _arrangeViewport;

    /// <summary>The length of an item along the scroll axis (the viewport less the peek on both sides).</summary>
    private float ItemLength => Viewport - (float)PeekAreaInsets * 2;

    /// <summary>The distance from one item to the next.</summary>
    private float Stride => ItemLength + (float)ItemSpacing;

    /// <summary>The length of one round of a looping carousel.</summary>
    private float LoopLength => _items.Count * Stride;

    private float GetOffsetForPosition(int position) => position * Stride;

    /// <summary>
    /// The offset that shows <paramref name="position"/>: a looping carousel goes the short way
    /// round (from the last item forward to the first).
    /// </summary>
    private float NearestOffsetFor(int position)
    {
        var target = GetOffsetForPosition(position);
        if (!Loops || LoopLength <= 0)
            return target;
        var round = LoopLength;
        var k = MathF.Round((_scrollOffset - target) / round);
        return target + k * round;
    }

    private int GetPositionForOffset(float offset)
    {
        var stride = Stride;
        if (ItemLength <= 0 || stride <= 0) return 0;
        var index = (int)Math.Round(offset / stride);
        if (Loops)
            return Mod(index, _items.Count);
        return Math.Clamp(index, 0, Math.Max(0, _items.Count - 1));
    }

    private static int Mod(int value, int count) => ((value % count) + count) % count;

    /// <summary>Where item <paramref name="index"/> starts along the scroll axis, relative to the carousel's start.</summary>
    private float ItemStart(int index)
    {
        var start = (float)PeekAreaInsets + index * Stride - _scrollOffset;
        if (Loops && LoopLength > 0)
        {
            // The copy of the item nearest the viewport: one round before or after.
            var round = LoopLength;
            var stride = Stride;
            start = ((start + stride) % round + round) % round - stride;
        }
        return start;
    }

    /// <summary>Folds a looping offset back into the first round once the carousel is at rest.</summary>
    private void NormalizeLoopOffset()
    {
        if (!Loops || LoopLength <= 0)
            return;
        var round = LoopLength;
        _scrollOffset = (_scrollOffset % round + round) % round;
        _targetScrollOffset = _scrollOffset;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (ShowsEmptyView)
            _emptyViewContent!.Measure(availableSize);

        var indicators = ShowIndicators ? 30 : 0;
        float itemWidth, itemHeight;
        if (IsVertical)
        {
            itemWidth = (float)availableSize.Width;
            itemHeight = (float)availableSize.Height - (float)PeekAreaInsets * 2 - indicators;
        }
        else
        {
            itemWidth = (float)availableSize.Width - (float)PeekAreaInsets * 2;
            itemHeight = (float)availableSize.Height - indicators;
        }

        foreach (var item in _items)
        {
            item.Measure(new Size(itemWidth, itemHeight));
        }

        return availableSize;
    }

    /// <summary>The bounds of item <paramref name="index"/> in a carousel at <paramref name="bounds"/>.</summary>
    private SKRect ItemBounds(int index, SKRect bounds)
    {
        var indicators = ShowIndicators ? 30f : 0f;
        var start = ItemStart(index);
        var length = ItemLength;
        return IsVertical
            ? new SKRect(bounds.Left, bounds.Top + start, bounds.Right, bounds.Top + start + Math.Max(0, length - indicators))
            : new SKRect(bounds.Left + start, bounds.Top, bounds.Left + start + length, bounds.Bottom - indicators);
    }

    // The item stride the offset was last computed with: a carousel resized (or laid out after a
    // ScrollTo made before it had a size) keeps showing its current item.
    private float _layoutStride = -1;

    private void KeepCurrentItemOnResize()
    {
        var stride = Stride;
        if (Math.Abs(stride - _layoutStride) < 0.01f)
            return;
        _layoutStride = stride;
        if (!_isDragging)
        {
            _isAnimating = false;
            _scrollOffset = _targetScrollOffset = GetOffsetForPosition(_currentPosition);
        }
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        _arrangeViewport = (float)(IsVertical ? bounds.Height : bounds.Width);
        try
        {
            KeepCurrentItemOnResize();
            var area = new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom);
            for (int i = 0; i < _items.Count; i++)
            {
                var itemBounds = ItemBounds(i, area);
                _items[i].Arrange(new Rect(itemBounds.Left, itemBounds.Top, itemBounds.Width, itemBounds.Height));
            }
        }
        finally
        {
            _arrangeViewport = null;
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
                NormalizeLoopOffset();
            }
            else
            {
                Invalidate(); // Continue animation
            }
        }

        KeepCurrentItemOnResize();
        canvas.Save();
        canvas.ClipRect(bounds);

        if (_items.Count == 0)
        {
            DrawEmptyView(canvas, bounds);
            canvas.Restore();
            return;
        }

        // Draw the items in view, where the offset puts them now.
        for (int i = 0; i < _items.Count; i++)
        {
            var itemBounds = ItemBounds(i, bounds);
            if (itemBounds.Right <= bounds.Left || itemBounds.Left >= bounds.Right
                || itemBounds.Bottom <= bounds.Top || itemBounds.Top >= bounds.Bottom)
                continue;
            var item = _items[i];
            if (Math.Abs(item.Bounds.Left - itemBounds.Left) > 0.01 || Math.Abs(item.Bounds.Top - itemBounds.Top) > 0.01
                || Math.Abs(item.Bounds.Width - itemBounds.Width) > 0.01 || Math.Abs(item.Bounds.Height - itemBounds.Height) > 0.01)
                item.Arrange(new Rect(itemBounds.Left, itemBounds.Top, itemBounds.Width, itemBounds.Height));
            item.Draw(canvas);
        }

        // Draw indicators
        if (ShowIndicators && _items.Count > 1)
        {
            DrawIndicators(canvas, bounds);
        }

        if (ShowsScrollBar)
        {
            DrawScrollBar(canvas, bounds);
        }

        canvas.Restore();
    }

    private bool ShowsScrollBar => !Loop && _items.Count > 1 && (IsVertical ? VerticalScrollBarVisibility : HorizontalScrollBarVisibility) switch
    {
        ScrollBarVisibility.Always => true,
        ScrollBarVisibility.Never => false,
        _ => _isDragging || _isAnimating,
    };

    private void DrawScrollBar(SKCanvas canvas, SKRect bounds)
    {
        const float thickness = 4f, margin = 2f;
        var track = IsVertical
            ? new SKRect(bounds.Right - thickness - margin, bounds.Top + margin, bounds.Right - margin, bounds.Bottom - margin)
            : new SKRect(bounds.Left + margin, bounds.Bottom - thickness - margin, bounds.Right - margin, bounds.Bottom - margin);
        var trackLength = IsVertical ? track.Height : track.Width;
        float total = _items.Count * Stride - (float)ItemSpacing;
        if (total <= 0 || trackLength <= 0)
            return;
        float maxOffset = Math.Max(1f, GetOffsetForPosition(_items.Count - 1));
        float thumbLength = Math.Max(24f, trackLength * Math.Min(1f, Viewport / total));
        float ratio = Math.Clamp(_scrollOffset / maxOffset, 0f, 1f);
        float start = (trackLength - thumbLength) * ratio;
        var thumb = IsVertical
            ? new SKRect(track.Left, track.Top + start, track.Right, track.Top + start + thumbLength)
            : new SKRect(track.Left + start, track.Top, track.Left + start + thumbLength, track.Bottom);
        using var paint = new SKPaint { Color = SkiaTheme.ScrollbarThumbSK, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(thumb, thickness / 2), paint);
    }

    private void DrawEmptyView(SKCanvas canvas, SKRect bounds)
    {
        if (_emptyViewContent is { IsVisible: true } content)
        {
            var margin = content.Margin;
            var area = new SKRect(bounds.Left + (float)margin.Left, bounds.Top + (float)margin.Top,
                bounds.Right - (float)margin.Right, bounds.Bottom - (float)margin.Bottom);
            var desired = content.Measure(new Size(Math.Max(0, area.Width), Math.Max(0, area.Height)));
            content.Arrange(SkiaPage.AlignContent(content, area, desired));
            content.Draw(canvas);
            return;
        }
        if (string.IsNullOrEmpty(_emptyViewText))
            return;
        using var font = SkiaFontFactory.Create(16);
        using var textPaint = new SKPaint { Color = SkiaTheme.TextPlaceholderSK, IsAntialias = true };
        font.MeasureText(_emptyViewText, out var textBounds);
        var y = TextRenderingHelper.BaselineForVerticalCenter(font, bounds.MidY);
        canvas.DrawText(_emptyViewText, bounds.MidX - textBounds.MidX, y, SKTextAlign.Left, font, textPaint);
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
            Color = _indicatorColorSK,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var selectedPaint = new SKPaint
        {
            Color = _selectedIndicatorColorSK,
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

        // The empty view takes input like any content.
        if (ShowsEmptyView && _emptyViewContent!.HitTestAt(x, y) is { } emptyHit)
            return emptyHit;

        // Check items
        foreach (var item in _items)
        {
            var hit = item.HitTestAt(x, y);
            if (hit != null) return hit;
        }

        return this;
    }

    /// <summary>The pointer position along the scroll axis.</summary>
    private float Along(PointerEventArgs e) => IsVertical ? e.Y : e.X;

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled || !IsSwipeEnabled) return;

        BeginDrag(e);

        e.Handled = true;
        base.OnPointerPressed(e);
    }

    private void BeginDrag(PointerEventArgs e)
    {
        _isDragging = true;
        _dragStartX = Along(e);
        _dragStartOffset = _scrollOffset;
        _lastDragX = Along(e);
        _lastDragTime = DateTime.UtcNow;
        _velocity = 0;
        _isAnimating = false;
    }

    private const float SwipeStartDistance = 8f;
    private bool _trackingDescendantPress;

    /// <summary>A press on an item's content (a label, an image) can still become a swipe of the carousel.</summary>
    void IPointerDragInterceptor.OnDescendantPointerPressed(PointerEventArgs e)
    {
        _trackingDescendantPress = IsEnabled && IsSwipeEnabled && _items.Count > 0;
        if (_trackingDescendantPress)
        {
            _dragStartX = Along(e);
            _dragStartOffset = _scrollOffset;
        }
    }

    /// <summary>The press becomes the carousel's swipe once it moves along the carousel's axis, as a native carousel takes it.</summary>
    bool IPointerDragInterceptor.ShouldInterceptDrag(PointerEventArgs e)
    {
        if (!_trackingDescendantPress || !IsEnabled || !IsSwipeEnabled)
            return false;
        if (Math.Abs(Along(e) - _dragStartX) < SwipeStartDistance)
            return false;
        _trackingDescendantPress = false;
        var start = _dragStartX;
        BeginDrag(e);
        _dragStartX = start;
        _lastDragX = start;
        return true;
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_isDragging) return;

        float delta = _dragStartX - Along(e);
        _scrollOffset = _dragStartOffset + delta;

        // Clamp scrolling (a looping carousel goes round instead)
        if (!Loops)
        {
            float maxOffset = GetOffsetForPosition(_items.Count - 1);
            _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, maxOffset));
        }

        // Calculate velocity
        var now = DateTime.UtcNow;
        float timeDelta = (float)(now - _lastDragTime).TotalSeconds;
        if (timeDelta > 0)
        {
            _velocity = (_lastDragX - Along(e)) / timeDelta;
        }
        _lastDragX = Along(e);
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
        var stride = Stride;
        int targetSlot = stride > 0 ? (int)Math.Round(_scrollOffset / stride) : 0;

        // Apply velocity influence
        if (Math.Abs(_velocity) > 500)
        {
            if (_velocity > 0) targetSlot++;
            else if (_velocity < 0) targetSlot--;
        }

        if (Loops)
        {
            // Snap to the slot reached (in whichever round), then report the item there.
            int target = Mod(targetSlot, _items.Count);
            int oldPosition = _currentPosition;
            _currentPosition = target;
            _animationStartOffset = _scrollOffset;
            _animationTargetOffset = targetSlot * stride;
            _animationStartTime = DateTime.UtcNow;
            _isAnimating = true;
            Invalidate();
            if (oldPosition != target)
                PositionChanged?.Invoke(this, new PositionChangedEventArgs(oldPosition, target));
        }
        else
        {
            ScrollTo(Math.Clamp(targetSlot, 0, Math.Max(0, _items.Count - 1)), true);
        }
        e.Handled = true;

        base.OnPointerReleased(e);
    }
}
