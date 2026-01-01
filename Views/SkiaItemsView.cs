// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using System.Collections;
using System.Collections.Specialized;
using Microsoft.Maui.Graphics;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Base class for Skia-rendered items views (CollectionView, ListView).
/// Provides item rendering, scrolling, and virtualization.
/// </summary>
public class SkiaItemsView : SkiaView
{
    private IEnumerable? _itemsSource;
    private List<object> _items = new();
    protected float _scrollOffset;
    private float _itemHeight = 44; // Default item height
    private float _itemSpacing = 0;
    private int _firstVisibleIndex;
    private int _lastVisibleIndex;
    private bool _isDragging;
    private bool _isDraggingScrollbar;
    private float _dragStartY;
    private float _dragStartOffset;
    private float _scrollbarDragStartY;
    private float _scrollbarDragStartScrollOffset;
    private float _scrollbarDragAvailableTrack;
    private float _scrollbarDragMaxScroll;
    private float _velocity;
    private DateTime _lastDragTime;

    // Scroll bar
    private bool _showVerticalScrollBar = true;
    private float _scrollBarWidth = 8;
    private SKColor _scrollBarColor = new SKColor(128, 128, 128, 128);
    private SKColor _scrollBarTrackColor = new SKColor(200, 200, 200, 64);

    public IEnumerable? ItemsSource
    {
        get => _itemsSource;
        set
        {
            if (_itemsSource is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= OnCollectionChanged;
            }

            _itemsSource = value;
            RefreshItems();

            if (_itemsSource is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += OnCollectionChanged;
            }

            Invalidate();
        }
    }

    public float ItemHeight
    {
        get => _itemHeight;
        set
        {
            _itemHeight = value;
            Invalidate();
        }
    }

    public float ItemSpacing
    {
        get => _itemSpacing;
        set
        {
            _itemSpacing = value;
            Invalidate();
        }
    }

    public ScrollBarVisibility VerticalScrollBarVisibility { get; set; } = ScrollBarVisibility.Default;
    public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; } = ScrollBarVisibility.Never;

    public object? EmptyView { get; set; }
    public string? EmptyViewText { get; set; } = "No items";

    // Item rendering delegate (legacy)
    public Func<object, int, SKRect, SKCanvas, SKPaint, bool>? ItemRenderer { get; set; }

    // Item view creator - creates SkiaView from data item using DataTemplate
    public Func<object, SkiaView?>? ItemViewCreator { get; set; }

    // Cache of created item views for virtualization
    protected readonly Dictionary<int, SkiaView> _itemViewCache = new();

    // Cache of individual item heights for variable height items
    protected readonly Dictionary<int, float> _itemHeights = new();

    // Track last measured width to clear cache when width changes
    private float _lastMeasuredWidth = 0;

    // Selection support (overridden in SkiaCollectionView)
    public virtual int SelectedIndex { get; set; } = -1;

    public event EventHandler<ItemsScrolledEventArgs>? Scrolled;
    public event EventHandler<ItemsViewItemTappedEventArgs>? ItemTapped;

    public SkiaItemsView()
    {
        IsFocusable = true;
    }

    protected virtual void RefreshItems()
    {
        Console.WriteLine($"[SkiaItemsView] RefreshItems called, clearing {_items.Count} items and {_itemViewCache.Count} cached views");
        _items.Clear();
        _itemViewCache.Clear(); // Clear cached views when items change
        _itemHeights.Clear(); // Clear cached heights
        if (_itemsSource != null)
        {
            foreach (var item in _itemsSource)
            {
                _items.Add(item);
            }
        }
        Console.WriteLine($"[SkiaItemsView] RefreshItems done, now have {_items.Count} items");
        _scrollOffset = 0;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshItems();
        Invalidate();
    }

    /// <summary>
    /// Gets the height for a specific item, using cached height or default.
    /// </summary>
    protected float GetItemHeight(int index)
    {
        return _itemHeights.TryGetValue(index, out var height) ? height : _itemHeight;
    }

    /// <summary>
    /// Gets the Y offset for a specific item (cumulative height of all previous items).
    /// </summary>
    protected float GetItemOffset(int index)
    {
        float offset = 0;
        for (int i = 0; i < index && i < _items.Count; i++)
        {
            offset += GetItemHeight(i) + _itemSpacing;
        }
        return offset;
    }

    /// <summary>
    /// Calculates total content height based on individual item heights.
    /// </summary>
    protected float TotalContentHeight
    {
        get
        {
            if (_items.Count == 0) return 0;

            float total = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                total += GetItemHeight(i);
                if (i < _items.Count - 1) total += _itemSpacing;
            }
            return total;
        }
    }

    // Use ScreenBounds.Height for visible viewport
    protected float MaxScrollOffset => Math.Max(0, TotalContentHeight - ScreenBounds.Height);

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        Console.WriteLine($"[SkiaItemsView] OnDraw - bounds={bounds}, items={_items.Count}, ItemViewCreator={(ItemViewCreator != null ? "set" : "null")}");

        // Draw background
        if (BackgroundColor != SKColors.Transparent)
        {
            using var bgPaint = new SKPaint
            {
                Color = BackgroundColor,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(bounds, bgPaint);
        }

        // If no items, show empty view
        if (_items.Count == 0)
        {
            DrawEmptyView(canvas, bounds);
            return;
        }

        // Find first visible index by walking through items
        _firstVisibleIndex = 0;
        float cumulativeOffset = 0;
        for (int i = 0; i < _items.Count; i++)
        {
            var itemH = GetItemHeight(i);
            if (cumulativeOffset + itemH > _scrollOffset)
            {
                _firstVisibleIndex = i;
                break;
            }
            cumulativeOffset += itemH + _itemSpacing;
        }

        // Clip to bounds
        canvas.Save();
        canvas.ClipRect(bounds);

        // Draw visible items using variable heights
        using var paint = new SKPaint
        {
            IsAntialias = true
        };

        float currentY = bounds.Top + GetItemOffset(_firstVisibleIndex) - _scrollOffset;
        for (int i = _firstVisibleIndex; i < _items.Count; i++)
        {
            var itemH = GetItemHeight(i);
            var itemRect = new SKRect(bounds.Left, currentY, bounds.Right - (_showVerticalScrollBar ? _scrollBarWidth : 0), currentY + itemH);

            // Stop if we've passed the visible area
            if (itemRect.Top > bounds.Bottom)
            {
                _lastVisibleIndex = i - 1;
                break;
            }

            _lastVisibleIndex = i;

            if (itemRect.Bottom >= bounds.Top)
            {
                DrawItem(canvas, _items[i], i, itemRect, paint);
            }

            currentY += itemH + _itemSpacing;
        }

        canvas.Restore();

        // Draw scrollbar
        if (_showVerticalScrollBar && TotalContentHeight > bounds.Height)
        {
            DrawScrollBar(canvas, bounds);
        }
    }

    protected virtual void DrawItem(SKCanvas canvas, object item, int index, SKRect bounds, SKPaint paint)
    {
        // Draw selection highlight
        if (index == SelectedIndex)
        {
            paint.Color = new SKColor(0x21, 0x96, 0xF3, 0x59); // Light blue with 35% opacity
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(bounds, paint);
        }

        // Try to use ItemViewCreator for templated rendering
        if (ItemViewCreator != null)
        {
            Console.WriteLine($"[SkiaItemsView] DrawItem {index} - ItemViewCreator exists, item: {item}");
            // Get or create cached view for this index
            if (!_itemViewCache.TryGetValue(index, out var itemView) || itemView == null)
            {
                itemView = ItemViewCreator(item);
                if (itemView != null)
                {
                    itemView.Parent = this;
                    _itemViewCache[index] = itemView;
                }
            }

            if (itemView != null)
            {
                // Measure with large height to get natural size
                var availableSize = new SKSize(bounds.Width, float.MaxValue);
                var measuredSize = itemView.Measure(availableSize);

                // Store individual item height (with minimum of default height)
                var measuredHeight = Math.Max(measuredSize.Height, _itemHeight);
                if (!_itemHeights.TryGetValue(index, out var cachedHeight) || Math.Abs(cachedHeight - measuredHeight) > 1)
                {
                    _itemHeights[index] = measuredHeight;
                    // Request redraw if height changed significantly
                    if (Math.Abs(cachedHeight - measuredHeight) > 5)
                    {
                        Invalidate();
                    }
                }

                // Arrange with the actual measured height
                var actualBounds = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + measuredHeight);
                itemView.Arrange(actualBounds);
                itemView.Draw(canvas);
                return;
            }
        }
        else
        {
            Console.WriteLine($"[SkiaItemsView] DrawItem {index} - ItemViewCreator is NULL, falling back to ToString");
        }

        // Draw separator
        paint.Color = new SKColor(0xE0, 0xE0, 0xE0);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1;
        canvas.DrawLine(bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom, paint);

        // Use custom renderer if provided
        if (ItemRenderer != null)
        {
            if (ItemRenderer(item, index, bounds, canvas, paint))
                return;
        }

        // Default rendering - just show ToString
        paint.Color = SKColors.Black;
        paint.Style = SKPaintStyle.Fill;

        using var font = new SKFont(SKTypeface.Default, 14);
        using var textPaint = new SKPaint(font)
        {
            Color = SKColors.Black,
            IsAntialias = true
        };

        var text = item?.ToString() ?? "";
        var textBounds = new SKRect();
        textPaint.MeasureText(text, ref textBounds);

        var x = bounds.Left + 16;
        var y = bounds.MidY - textBounds.MidY;
        canvas.DrawText(text, x, y, textPaint);
    }

    protected virtual void DrawEmptyView(SKCanvas canvas, SKRect bounds)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(0x80, 0x80, 0x80),
            IsAntialias = true
        };

        using var font = new SKFont(SKTypeface.Default, 16);
        using var textPaint = new SKPaint(font)
        {
            Color = new SKColor(0x80, 0x80, 0x80),
            IsAntialias = true
        };

        var text = EmptyViewText ?? "No items";
        var textBounds = new SKRect();
        textPaint.MeasureText(text, ref textBounds);

        var x = bounds.MidX - textBounds.MidX;
        var y = bounds.MidY - textBounds.MidY;
        canvas.DrawText(text, x, y, textPaint);
    }

    private void DrawScrollBar(SKCanvas canvas, SKRect bounds)
    {
        var trackRect = new SKRect(
            bounds.Right - _scrollBarWidth,
            bounds.Top,
            bounds.Right,
            bounds.Bottom);

        // Draw track
        using var trackPaint = new SKPaint
        {
            Color = _scrollBarTrackColor,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(trackRect, trackPaint);

        // Calculate thumb size and position
        var viewportRatio = bounds.Height / TotalContentHeight;
        var thumbHeight = Math.Max(20, bounds.Height * viewportRatio);
        var scrollRatio = _scrollOffset / MaxScrollOffset;
        var thumbY = bounds.Top + (bounds.Height - thumbHeight) * scrollRatio;

        var thumbRect = new SKRect(
            bounds.Right - _scrollBarWidth + 1,
            thumbY,
            bounds.Right - 1,
            thumbY + thumbHeight);

        // Draw thumb
        using var thumbPaint = new SKPaint
        {
            Color = _scrollBarColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        var cornerRadius = (_scrollBarWidth - 2) / 2;
        canvas.DrawRoundRect(new SKRoundRect(thumbRect, cornerRadius), thumbPaint);
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        Console.WriteLine($"[SkiaItemsView] OnPointerPressed - x={e.X}, y={e.Y}, Bounds={Bounds}, ScreenBounds={ScreenBounds}, ItemCount={_items.Count}");
        if (!IsEnabled) return;

        // Check if clicking on scrollbar thumb
        if (_showVerticalScrollBar && TotalContentHeight > Bounds.Height)
        {
            var thumbBounds = GetScrollbarThumbBounds();
            if (thumbBounds.Contains(e.X, e.Y))
            {
                _isDraggingScrollbar = true;
                _scrollbarDragStartY = e.Y;
                _scrollbarDragStartScrollOffset = _scrollOffset;
                // Cache values to prevent stutter
                var thumbHeight = Math.Max(20, Bounds.Height * (Bounds.Height / TotalContentHeight));
                _scrollbarDragAvailableTrack = Bounds.Height - thumbHeight;
                _scrollbarDragMaxScroll = MaxScrollOffset;
                return;
            }
        }

        // Regular content drag
        _isDragging = true;
        _dragStartY = e.Y;
        _dragStartOffset = _scrollOffset;
        _lastDragTime = DateTime.Now;
        _velocity = 0;
    }

    /// <summary>
    /// Gets the bounds of the scrollbar thumb in screen coordinates.
    /// </summary>
    private SKRect GetScrollbarThumbBounds()
    {
        // Use ScreenBounds for hit testing (input events use screen coordinates)
        var screenBounds = ScreenBounds;
        var viewportRatio = screenBounds.Height / TotalContentHeight;
        var thumbHeight = Math.Max(20, screenBounds.Height * viewportRatio);
        var scrollRatio = MaxScrollOffset > 0 ? _scrollOffset / MaxScrollOffset : 0;
        var thumbY = screenBounds.Top + (screenBounds.Height - thumbHeight) * scrollRatio;

        return new SKRect(
            screenBounds.Right - _scrollBarWidth,
            thumbY,
            screenBounds.Right,
            thumbY + thumbHeight);
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        // Handle scrollbar dragging - use cached values to prevent stutter
        if (_isDraggingScrollbar)
        {
            if (_scrollbarDragAvailableTrack > 0)
            {
                var deltaY = e.Y - _scrollbarDragStartY;
                var scrollDelta = (deltaY / _scrollbarDragAvailableTrack) * _scrollbarDragMaxScroll;
                SetScrollOffset(_scrollbarDragStartScrollOffset + scrollDelta);
            }
            return;
        }

        if (!_isDragging) return;

        var delta = _dragStartY - e.Y;
        var newOffset = _dragStartOffset + delta;

        // Calculate velocity for momentum scrolling
        var now = DateTime.Now;
        var timeDelta = (now - _lastDragTime).TotalSeconds;
        if (timeDelta > 0)
        {
            _velocity = (float)((_scrollOffset - newOffset) / timeDelta);
        }
        _lastDragTime = now;

        SetScrollOffset(newOffset);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        // Handle scrollbar drag release
        if (_isDraggingScrollbar)
        {
            _isDraggingScrollbar = false;
            return;
        }

        if (_isDragging)
        {
            _isDragging = false;

            // Check for tap (minimal movement)
            var totalDrag = Math.Abs(e.Y - _dragStartY);
            if (totalDrag < 5)
            {
                // This was a tap - find which item was tapped using variable heights
                var screenBounds = ScreenBounds;
                var localY = e.Y - screenBounds.Top + _scrollOffset;

                // Find tapped index by walking through item heights
                int tappedIndex = -1;
                float cumulativeY = 0;
                for (int i = 0; i < _items.Count; i++)
                {
                    var itemH = GetItemHeight(i);
                    if (localY >= cumulativeY && localY < cumulativeY + itemH)
                    {
                        tappedIndex = i;
                        break;
                    }
                    cumulativeY += itemH + _itemSpacing;
                }

                Console.WriteLine($"[SkiaItemsView] Tap at Y={e.Y}, screenBounds.Top={screenBounds.Top}, scrollOffset={_scrollOffset}, localY={localY}, index={tappedIndex}");

                if (tappedIndex >= 0 && tappedIndex < _items.Count)
                {
                    OnItemTapped(tappedIndex, _items[tappedIndex]);
                }
            }
        }
    }

    /// <summary>
    /// Gets the total Y scroll offset from all parent ScrollViews.
    /// </summary>
    private float GetTotalParentScrollY()
    {
        float total = 0;
        var parent = Parent;
        while (parent != null)
        {
            if (parent is SkiaScrollView scrollView)
            {
                total += scrollView.ScrollY;
            }
            parent = parent.Parent;
        }
        return total;
    }

    protected virtual void OnItemTapped(int index, object item)
    {
        SelectedIndex = index;
        ItemTapped?.Invoke(this, new ItemsViewItemTappedEventArgs(index, item));
        Invalidate();
    }

    public override void OnScroll(ScrollEventArgs e)
    {
        var delta = e.DeltaY * 20;
        SetScrollOffset(_scrollOffset + delta);
        e.Handled = true;
    }

    private void SetScrollOffset(float offset)
    {
        var oldOffset = _scrollOffset;
        _scrollOffset = Math.Clamp(offset, 0, MaxScrollOffset);

        if (Math.Abs(_scrollOffset - oldOffset) > 0.1f)
        {
            Scrolled?.Invoke(this, new ItemsScrolledEventArgs(_scrollOffset, TotalContentHeight));
            Invalidate();
        }
    }

    public void ScrollToIndex(int index, bool animate = true)
    {
        if (index < 0 || index >= _items.Count) return;

        var targetOffset = GetItemOffset(index);
        SetScrollOffset(targetOffset);
    }

    public void ScrollToItem(object item, bool animate = true)
    {
        var index = _items.IndexOf(item);
        if (index >= 0)
        {
            ScrollToIndex(index, animate);
        }
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        switch (e.Key)
        {
            case Key.Up:
                if (SelectedIndex > 0)
                {
                    SelectedIndex--;
                    EnsureIndexVisible(SelectedIndex);
                    Invalidate();
                }
                e.Handled = true;
                break;

            case Key.Down:
                if (SelectedIndex < _items.Count - 1)
                {
                    SelectedIndex++;
                    EnsureIndexVisible(SelectedIndex);
                    Invalidate();
                }
                e.Handled = true;
                break;

            case Key.PageUp:
                SetScrollOffset(_scrollOffset - Bounds.Height);
                e.Handled = true;
                break;

            case Key.PageDown:
                SetScrollOffset(_scrollOffset + Bounds.Height);
                e.Handled = true;
                break;

            case Key.Home:
                SelectedIndex = 0;
                SetScrollOffset(0);
                Invalidate();
                e.Handled = true;
                break;

            case Key.End:
                SelectedIndex = _items.Count - 1;
                SetScrollOffset(MaxScrollOffset);
                Invalidate();
                e.Handled = true;
                break;

            case Key.Enter:
                if (SelectedIndex >= 0 && SelectedIndex < _items.Count)
                {
                    OnItemTapped(SelectedIndex, _items[SelectedIndex]);
                }
                e.Handled = true;
                break;
        }
    }

    private void EnsureIndexVisible(int index)
    {
        var itemTop = GetItemOffset(index);
        var itemBottom = itemTop + GetItemHeight(index);

        if (itemTop < _scrollOffset)
        {
            SetScrollOffset(itemTop);
        }
        else if (itemBottom > _scrollOffset + Bounds.Height)
        {
            SetScrollOffset(itemBottom - Bounds.Height);
        }
    }

    protected int ItemCount => _items.Count;
    protected object? GetItemAt(int index) => index >= 0 && index < _items.Count ? _items[index] : null;

    /// <summary>
    /// Override HitTest to handle scrollbar clicks properly.
    /// HitTest receives content-space coordinates (already transformed by parent ScrollView).
    /// </summary>
    public override SkiaView? HitTest(float x, float y)
    {
        // HitTest uses Bounds (content space) - coordinates are transformed by parent
        if (!IsVisible || !Bounds.Contains(new SKPoint(x, y)))
            return null;

        // Check scrollbar area FIRST before content
        // This ensures scrollbar clicks are handled by this view
        if (_showVerticalScrollBar && TotalContentHeight > Bounds.Height)
        {
            var trackArea = new SKRect(Bounds.Right - _scrollBarWidth, Bounds.Top, Bounds.Right, Bounds.Bottom);
            if (trackArea.Contains(x, y))
                return this;
        }

        return this;
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        var width = availableSize.Width < float.MaxValue ? availableSize.Width : 200;
        var height = availableSize.Height < float.MaxValue ? availableSize.Height : 300;

        // Clear item caches when width changes significantly (items need re-measurement for text wrapping)
        if (Math.Abs(width - _lastMeasuredWidth) > 5)
        {
            _itemHeights.Clear();
            _itemViewCache.Clear();
            _lastMeasuredWidth = width;
        }

        // Items view takes all available space
        return new SKSize(width, height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_itemsSource is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged -= OnCollectionChanged;
            }
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Event args for items view scroll events.
/// </summary>
public class ItemsScrolledEventArgs : EventArgs
{
    public float ScrollOffset { get; }
    public float TotalHeight { get; }

    public ItemsScrolledEventArgs(float scrollOffset, float totalHeight)
    {
        ScrollOffset = scrollOffset;
        TotalHeight = totalHeight;
    }
}

/// <summary>
/// Event args for items view item tap events.
/// </summary>
public class ItemsViewItemTappedEventArgs : EventArgs
{
    public int Index { get; }
    public object Item { get; }

    public ItemsViewItemTappedEventArgs(int index, object item)
    {
        Index = index;
        Item = item;
    }
}
