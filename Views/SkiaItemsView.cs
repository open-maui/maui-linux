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
    private float _dragStartY;
    private float _dragStartOffset;
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

    // Item rendering delegate
    public Func<object, int, SKRect, SKCanvas, SKPaint, bool>? ItemRenderer { get; set; }

    // Selection support (overridden in SkiaCollectionView)
    public virtual int SelectedIndex { get; set; } = -1;

    public event EventHandler<ItemsScrolledEventArgs>? Scrolled;
    public event EventHandler<ItemsViewItemTappedEventArgs>? ItemTapped;

    public SkiaItemsView()
    {
        IsFocusable = true;
    }

    private void RefreshItems()
    {
        _items.Clear();
        if (_itemsSource != null)
        {
            foreach (var item in _itemsSource)
            {
                _items.Add(item);
            }
        }
        _scrollOffset = 0;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshItems();
        Invalidate();
    }

    protected float TotalContentHeight => _items.Count * (_itemHeight + _itemSpacing) - _itemSpacing;
    protected float MaxScrollOffset => Math.Max(0, TotalContentHeight - Bounds.Height);

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
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

        // Calculate visible range
        _firstVisibleIndex = Math.Max(0, (int)(_scrollOffset / (_itemHeight + _itemSpacing)));
        _lastVisibleIndex = Math.Min(_items.Count - 1,
            (int)((_scrollOffset + bounds.Height) / (_itemHeight + _itemSpacing)) + 1);

        // Clip to bounds
        canvas.Save();
        canvas.ClipRect(bounds);

        // Draw visible items
        using var paint = new SKPaint
        {
            IsAntialias = true
        };

        for (int i = _firstVisibleIndex; i <= _lastVisibleIndex; i++)
        {
            var itemY = bounds.Top + (i * (_itemHeight + _itemSpacing)) - _scrollOffset;
            var itemRect = new SKRect(bounds.Left, itemY, bounds.Right - (_showVerticalScrollBar ? _scrollBarWidth : 0), itemY + _itemHeight);

            if (itemRect.Bottom < bounds.Top || itemRect.Top > bounds.Bottom)
                continue;

            DrawItem(canvas, _items[i], i, itemRect, paint);
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
            paint.Color = new SKColor(0x21, 0x96, 0xF3, 0x40); // Light blue
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(bounds, paint);
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
        if (!IsEnabled) return;

        _isDragging = true;
        _dragStartY = e.Y;
        _dragStartOffset = _scrollOffset;
        _lastDragTime = DateTime.Now;
        _velocity = 0;
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
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
        if (_isDragging)
        {
            _isDragging = false;

            // Check for tap (minimal movement)
            var totalDrag = Math.Abs(e.Y - _dragStartY);
            if (totalDrag < 5)
            {
                // This was a tap - find which item was tapped
                var tapY = e.Y + _scrollOffset - Bounds.Top;
                var tappedIndex = (int)(tapY / (_itemHeight + _itemSpacing));

                if (tappedIndex >= 0 && tappedIndex < _items.Count)
                {
                    OnItemTapped(tappedIndex, _items[tappedIndex]);
                }
            }
        }
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

        var targetOffset = index * (_itemHeight + _itemSpacing);
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
        var itemTop = index * (_itemHeight + _itemSpacing);
        var itemBottom = itemTop + _itemHeight;

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

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        // Items view takes all available space
        return new SKSize(
            availableSize.Width < float.MaxValue ? availableSize.Width : 200,
            availableSize.Height < float.MaxValue ? availableSize.Height : 300);
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
