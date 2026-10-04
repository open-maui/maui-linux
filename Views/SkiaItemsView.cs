// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using System.Collections;
using System.Collections.Specialized;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Rendering;

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
    private Color _scrollBarColor = SkiaTheme.ScrollbarThumb;
    private Color _scrollBarTrackColor = SkiaTheme.ScrollbarTrack;

    // The items source's change notifications, observed weakly (MAUI's items handlers do the
    // same): a view model's collection outlives the pages that show it, and must not keep
    // this list, its handler and its item views alive.
    private WeakCollectionChangedProxy<SkiaItemsView>? _collectionSubscription;

    public IEnumerable? ItemsSource
    {
        get => _itemsSource;
        set
        {
            _collectionSubscription?.Dispose();
            _collectionSubscription = null;

            _itemsSource = value;
            RefreshItems();

            if (_itemsSource is INotifyCollectionChanged newCollection)
            {
                _collectionSubscription = new WeakCollectionChangedProxy<SkiaItemsView>(newCollection, this,
                    static (view, sender, e) => view.OnCollectionChanged(sender, e));
            }

            InvalidateMeasure();
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

    /// <summary>
    /// The least height a measured row gets. A MAUI CollectionView's rows are
    /// as tall as their template on every platform (0); a ListView's rows
    /// keep its row height (the handler sets it). <see cref="ItemHeight"/> is
    /// only the estimate for rows not yet measured.
    /// </summary>
    public float MinimumItemHeight
    {
        get => _minimumItemHeight;
        set
        {
            _minimumItemHeight = value;
            _itemHeights.Clear();
            Invalidate();
        }
    }

    private float _minimumItemHeight;

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

    private SkiaView? _emptyViewContent;

    /// <summary>
    /// The view shown in the list's area while it has no items: the platform view of a View
    /// EmptyView, or of the EmptyViewTemplate's content, as MAUI shows it on every platform
    /// (placed by its own alignment, and taking input, so a button in it works).
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

    private bool ShowsEmptyView => _items.Count == 0 && _emptyViewContent is { IsVisible: true };
    /// <summary>
    /// Text shown when there are no items: a string <c>EmptyView</c>. None by
    /// default, as MAUI shows nothing for an empty list without an EmptyView.
    /// </summary>
    public string? EmptyViewText { get; set; }

    // Item rendering delegate (legacy)
    public Func<object, int, SKRect, SKCanvas, SKPaint, bool>? ItemRenderer { get; set; }

    // Item view creator - creates SkiaView from data item using DataTemplate
    private Func<object, SkiaView?>? _itemViewCreator;

    /// <summary>
    /// Creates the view for an item (the ItemTemplate). A new creator drops the
    /// views made by the old one: a template swapped at run time (a flyout
    /// switching its rows to rail mode) showed the old rows until they happened
    /// to be recreated, one change behind.
    /// </summary>
    public Func<object, SkiaView?>? ItemViewCreator
    {
        get => _itemViewCreator;
        set
        {
            if (ReferenceEquals(_itemViewCreator, value))
                return;
            _itemViewCreator = value;
            ReleaseItemViews();
            _itemHeights.Clear();
            InvalidateMeasure();
            Invalidate();
        }
    }

    // Cache of created item views for virtualization
    protected readonly Dictionary<int, SkiaView> _itemViewCache = new();

    // Recycling: item views are created as rows are drawn; views far outside
    // the rows drawn in the last frame are released once the cache grows past
    // a bound, so a list scrolled end to end does not keep one view per item.
    // Measured heights (_itemHeights) are kept, so positions stay stable.
    private int _drawnMin = int.MaxValue;
    private int _drawnMax = -1;

    // The rows on screen after the last frame, where their views' Bounds are (hit testing).
    private int _shownMin = int.MaxValue;
    private int _shownMax = -1;

    /// <summary>Views kept beyond the drawn rows before recycling starts (minimum 64).</summary>
    internal int ItemViewCacheSlack { get; set; } = 64;

    /// <summary>Records that the row at <paramref name="index"/> was drawn this frame.</summary>
    protected void NoteItemDrawn(int index)
    {
        if (index < _drawnMin) _drawnMin = index;
        if (index > _drawnMax) _drawnMax = index;
    }

    public override void Draw(SKCanvas canvas)
    {
        _drawnMin = int.MaxValue;
        _drawnMax = -1;
        base.Draw(canvas);
        _shownMin = _drawnMin;
        _shownMax = _drawnMax;
        TrimItemViewCache();
    }

    /// <summary>
    /// Releases cached item views outside [first drawn - slack, last drawn + slack]
    /// when the cache holds more than the drawn rows plus twice the slack.
    /// </summary>
    internal void TrimItemViewCache()
    {
        if (_drawnMax < 0) return;
        int slack = Math.Max(64, ItemViewCacheSlack);
        int drawn = _drawnMax - _drawnMin + 1;
        if (_itemViewCache.Count <= drawn + 2 * slack) return;

        int keepFrom = _drawnMin - slack, keepTo = _drawnMax + slack;
        List<int>? evict = null;
        foreach (var index in _itemViewCache.Keys)
            if (index < keepFrom || index > keepTo)
                (evict ??= new List<int>()).Add(index);
        if (evict == null) return;
        foreach (var index in evict)
        {
            if (_itemViewCache.Remove(index, out var view) && view != null)
                ReleaseItemView(view);
        }
        DiagnosticLog.Debug("SkiaItemsView", $"Recycled {evict.Count} item views; {_itemViewCache.Count} kept around rows {_drawnMin}-{_drawnMax}");
    }

    /// <summary>
    /// Raised when the list drops a view made by <see cref="ItemViewCreator"/>: its row was
    /// recycled, the items changed, or the creator was replaced. The view is no longer shown and
    /// is not reused, so the creator's owner can release what it attached to it (MAUI's handlers
    /// remove a recycled item from the list's logical children).
    /// </summary>
    public event EventHandler<SkiaView>? ItemViewReleased;

    /// <summary>Drops one item view: unparents it and raises <see cref="ItemViewReleased"/>.</summary>
    private void ReleaseItemView(SkiaView view)
    {
        _rowOrigins.Remove(view);
        if (ReferenceEquals(view.Parent, this))
            view.Parent = null;
        try
        {
            ItemViewReleased?.Invoke(this, view);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaItemsView", "Releasing an item view failed", ex);
        }
    }

    /// <summary>Drops every item view (see <see cref="ItemViewReleased"/>).</summary>
    protected void ReleaseItemViews()
    {
        if (_itemViewCache.Count == 0)
            return;
        var views = _itemViewCache.Values.ToList();
        _itemViewCache.Clear();
        foreach (var view in views)
        {
            if (view != null)
                ReleaseItemView(view);
        }
    }

    // Where each item view's row starts: MAUI gives an item its Frame relative to its cell
    // (the row, margin outside the item), not to the list.
    private readonly Dictionary<SkiaView, Point> _rowOrigins = new();

    /// <summary>Records the top-left corner of the row <paramref name="itemView"/> is placed in.</summary>
    protected void SetRowOrigin(SkiaView itemView, float x, float y) => _rowOrigins[itemView] = new Point(x, y);

    internal override Point? FrameOriginFor(SkiaView child) =>
        _rowOrigins.TryGetValue(child, out var origin) ? origin : null;

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
        DiagnosticLog.Debug("SkiaItemsView", $"RefreshItems called, clearing {_items.Count} items and {_itemViewCache.Count} cached views");
        _items.Clear();
        ReleaseItemViews(); // Clear cached views when items change
        _itemHeights.Clear(); // Clear cached heights
        if (_itemsSource != null)
        {
            foreach (var item in _itemsSource)
            {
                _items.Add(item);
            }
        }
        DiagnosticLog.Debug("SkiaItemsView", $"RefreshItems done, now have {_items.Count} items");
        _scrollOffset = 0;
    }

    /// <summary>
    /// Called when theme changes to refresh all cached item views.
    /// Clears the item view cache so items are recreated with new theme colors.
    /// </summary>
    public virtual void RefreshTheme()
    {
        // Clear cached views to force recreation with new AppThemeBinding values
        ReleaseItemViews();
        _itemHeights.Clear();
        Invalidate();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshItems();
        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// Ensures the item view for <paramref name="index"/> is created and its height measured
    /// BEFORE row positions are computed, so the very first draw after an ItemsSource change
    /// (or cache refresh) lays rows out with real heights. Without this, the first frame
    /// positions rows using the default ItemHeight, then a second frame corrects them -
    /// a visible one-frame layout flash (e.g. when a page's OnAppearing resets ItemsSource
    /// after a navigation pop).
    /// </summary>
    protected void EnsureItemMeasured(int index, float availableWidth)
    {
        if (ItemViewCreator == null) return;
        if (index < 0 || index >= _items.Count) return;
        if (_itemHeights.ContainsKey(index)) return;

        var itemView = GetOrCreateItemView(index);
        if (itemView == null) return;

        _itemHeights[index] = Math.Max(MeasureItemExtent(itemView, availableWidth, out _), _minimumItemHeight);
    }

    /// <summary>The view of the item at <paramref name="index"/>, made by <see cref="ItemViewCreator"/> if needed.</summary>
    protected SkiaView? GetOrCreateItemView(int index)
    {
        if (ItemViewCreator == null || index < 0 || index >= _items.Count) return null;
        if (_itemViewCache.TryGetValue(index, out var itemView) && itemView != null)
            return itemView;
        itemView = ItemViewCreator(_items[index]);
        if (itemView == null) return null;
        itemView.Parent = this;
        _itemViewCache[index] = itemView;
        return itemView;
    }

    /// <summary>
    /// Measures an item view in a row whose cross size (width of a vertical list, height of a
    /// horizontal one) is <paramref name="crossSize"/>, and returns the row's extent along the
    /// scroll axis. The item's margin is inside its row, as MAUI's cells hold it.
    /// <paramref name="itemSize"/> is the item's own desired size.
    /// </summary>
    protected float MeasureItemExtent(SkiaView itemView, float crossSize, out Size itemSize)
    {
        var margin = itemView.Margin;
        var horizontal = IsHorizontal;
        var constraint = horizontal
            ? new Size(double.PositiveInfinity, Math.Max(0, crossSize - margin.VerticalThickness))
            : new Size(Math.Max(0, crossSize - margin.HorizontalThickness), float.MaxValue);
        itemSize = itemView.Measure(constraint);
        var raw = (float)(horizontal ? itemSize.Width : itemSize.Height);
        if (float.IsNaN(raw) || float.IsInfinity(raw) || raw > 10000f)
        {
            raw = _itemHeight;
            itemSize = horizontal ? new Size(raw, itemSize.Height) : new Size(itemSize.Width, raw);
        }
        return raw + (float)(horizontal ? margin.HorizontalThickness : margin.VerticalThickness);
    }

    /// <summary>
    /// True when the items are laid out in a row and scroll horizontally (a CollectionView with
    /// a horizontal ItemsLayout). Item extents, offsets and the scroll offset are then along X.
    /// </summary>
    protected virtual bool IsHorizontal => false;

    /// <summary>The visible length along the scroll axis.</summary>
    protected float ViewportExtent => (float)(IsHorizontal ? ScreenBounds.Width : ScreenBounds.Height);

    /// <summary>
    /// Gets the height for a specific item: its measured height (at least
    /// <see cref="MinimumItemHeight"/>), or the <see cref="ItemHeight"/>
    /// estimate until it has been measured.
    /// </summary>
    protected float GetItemHeight(int index)
    {
        var cached = _itemHeights.TryGetValue(index, out var height) ? height : _itemHeight;
        return Math.Max(cached, _minimumItemHeight);
    }

    /// <summary>
    /// Content scrolled before the first item and after the last (a CollectionView's header and
    /// footer, which scroll with the items as on every MAUI platform). Zero for a plain list.
    /// </summary>
    protected virtual float LeadingContentHeight => 0f;

    /// <inheritdoc cref="LeadingContentHeight"/>
    protected virtual float TrailingContentHeight => 0f;

    /// <summary>
    /// Gets the Y offset for a specific item (cumulative height of all previous items).
    /// </summary>
    protected float GetItemOffset(int index)
    {
        float offset = LeadingContentHeight;
        for (int i = 0; i < index && i < _items.Count; i++)
        {
            offset += GetItemHeight(i) + _itemSpacing;
        }
        return offset;
    }

    /// <summary>Rows measured for real when sizing to content; the rest use the estimate.</summary>
    private const int NaturalMeasureLimit = 200;

    /// <summary>
    /// The height the list wants when unconstrained: its rows (the first ones
    /// measured at <paramref name="width"/>), plus a line for the empty text.
    /// </summary>
    protected virtual float NaturalHeight(float width)
    {
        if (_items.Count == 0 && _emptyViewContent is { IsVisible: true } empty)
        {
            var size = empty.Measure(new Size(width, double.PositiveInfinity));
            return (float)(size.Height + empty.Margin.VerticalThickness);
        }
        if (_items.Count == 0)
            return string.IsNullOrEmpty(EmptyViewText) ? 0 : 44;
        for (int i = 0; i < _items.Count && i < NaturalMeasureLimit; i++)
            EnsureItemMeasured(i, width);
        return TotalContentHeight;
    }

    /// <summary>
    /// Calculates total content height based on individual item heights.
    /// </summary>
    protected float TotalContentHeight
    {
        get
        {
            float total = LeadingContentHeight + TrailingContentHeight;
            if (_items.Count == 0) return total;

            for (int i = 0; i < _items.Count; i++)
            {
                total += GetItemHeight(i);
                if (i < _items.Count - 1) total += _itemSpacing;
            }
            return total;
        }
    }

    // Use ScreenBounds.Height for visible viewport
    protected float MaxScrollOffset => Math.Max(0, TotalContentHeight - ViewportExtent);

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        DiagnosticLog.Debug("SkiaItemsView", $"OnDraw - bounds={bounds}, items={_items.Count}, ItemViewCreator={(ItemViewCreator != null ? "set" : "null")}");

        // Draw background
        if (BackgroundColor != null && BackgroundColor != Colors.Transparent)
        {
            using var bgPaint = new SKPaint
            {
                Color = GetEffectiveBackgroundColor(),
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

        // Content width excludes the scrollbar gutter (matches the itemRect below)
        var contentWidth = bounds.Width - (_showVerticalScrollBar ? _scrollBarWidth : 0);

        // Find first visible index by walking through items.
        // Measure each item before using its height so the first frame after a
        // cache refresh positions rows correctly (no mis-layout flash).
        _firstVisibleIndex = 0;
        float cumulativeOffset = LeadingContentHeight;
        for (int i = 0; i < _items.Count; i++)
        {
            EnsureItemMeasured(i, contentWidth);
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
            EnsureItemMeasured(i, contentWidth);
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
            paint.Color = SkiaTheme.PrimarySelectionSK;
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(bounds, paint);
        }

        // Try to use ItemViewCreator for templated rendering
        if (ItemViewCreator != null)
        {
            DiagnosticLog.Debug("SkiaItemsView", $"DrawItem {index} - ItemViewCreator exists, item: {item}");
            // Get or create cached view for this index
            NoteItemDrawn(index);
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
                var availableSize = new Size(bounds.Width, float.MaxValue);
                var measuredSize = itemView.Measure(availableSize);

                // Store individual item height (with minimum of default height)
                var measuredHeight = Math.Max((float)measuredSize.Height, _itemHeight);
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
                var actualBounds = new Rect(bounds.Left, bounds.Top, bounds.Width, measuredHeight);
                itemView.Arrange(actualBounds);
                itemView.Draw(canvas);
                return;
            }
        }
        else
        {
            DiagnosticLog.Debug("SkiaItemsView", $"DrawItem {index} - ItemViewCreator is NULL, falling back to ToString");
        }

        // Draw separator
        paint.Color = SkiaTheme.Gray300SK;
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
        paint.Color = SkiaTheme.TextPrimarySK;
        paint.Style = SKPaintStyle.Fill;

        using var font = SkiaFontFactory.Create(14);
        using var textPaint = new SKPaint
        {
            Color = SkiaTheme.TextPrimarySK,
            IsAntialias = true
        };

        var text = item?.ToString() ?? "";

        var x = bounds.Left + 16;
        var y = TextRenderingHelper.BaselineForVerticalCenter(font, bounds.MidY);
        canvas.DrawText(text, x, y, SKTextAlign.Left, font, textPaint);
    }

    protected virtual void DrawEmptyView(SKCanvas canvas, SKRect bounds)
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

        using var paint = new SKPaint
        {
            Color = SkiaTheme.TextPlaceholderSK,
            IsAntialias = true
        };

        using var font = SkiaFontFactory.Create(16);
        using var textPaint = new SKPaint
        {
            Color = SkiaTheme.TextPlaceholderSK,
            IsAntialias = true
        };

        var text = EmptyViewText;
        if (string.IsNullOrEmpty(text))
            return;
        font.MeasureText(text, out var textBounds);

        var x = bounds.MidX - textBounds.MidX;
        var y = TextRenderingHelper.BaselineForVerticalCenter(font, bounds.MidY);
        canvas.DrawText(text, x, y, SKTextAlign.Left, font, textPaint);
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
            Color = SkiaTheme.ScrollbarTrackSK,
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
            Color = SkiaTheme.ScrollbarThumbSK,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        var cornerRadius = (_scrollBarWidth - 2) / 2;
        canvas.DrawRoundRect(new SKRoundRect(thumbRect, cornerRadius), thumbPaint);
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        DiagnosticLog.Debug("SkiaItemsView", $"OnPointerPressed - x={e.X}, y={e.Y}, Bounds={Bounds}, ScreenBounds={ScreenBounds}, ItemCount={_items.Count}");
        if (!IsEnabled) return;

        // Check if clicking on scrollbar thumb
        if (!IsHorizontal && _showVerticalScrollBar && TotalContentHeight > Bounds.Height)
        {
            var thumbBounds = GetScrollbarThumbBounds();
            if (thumbBounds.Contains(e.X, e.Y))
            {
                _isDraggingScrollbar = true;
                _scrollbarDragStartY = e.Y;
                _scrollbarDragStartScrollOffset = _scrollOffset;
                // Cache values to prevent stutter
                var thumbHeight = Math.Max(20f, (float)Bounds.Height * ((float)Bounds.Height / TotalContentHeight));
                _scrollbarDragAvailableTrack = (float)Bounds.Height - thumbHeight;
                _scrollbarDragMaxScroll = MaxScrollOffset;
                return;
            }
        }

        // Regular content drag
        _isDragging = true;
        _dragStartY = MainAxis(e);
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
        var viewportRatio = (float)screenBounds.Height / TotalContentHeight;
        var thumbHeight = Math.Max(20f, (float)screenBounds.Height * viewportRatio);
        var scrollRatio = MaxScrollOffset > 0 ? _scrollOffset / MaxScrollOffset : 0f;
        var thumbY = (float)screenBounds.Top + ((float)screenBounds.Height - thumbHeight) * scrollRatio;

        return new SKRect(
            (float)(screenBounds.Left + screenBounds.Width) - _scrollBarWidth,
            thumbY,
            (float)(screenBounds.Left + screenBounds.Width),
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

        var delta = _dragStartY - MainAxis(e);
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
            var totalDrag = Math.Abs(MainAxis(e) - _dragStartY);
            if (totalDrag < 5)
            {
                // This was a tap - find which item was tapped using variable heights
                var screenBounds = ScreenBounds;
                var localY = IsHorizontal
                    ? e.X - (float)screenBounds.Left + _scrollOffset
                    : e.Y - (float)screenBounds.Top + _scrollOffset;

                // Find tapped index by walking through item heights
                int tappedIndex = -1;
                float cumulativeY = LeadingContentHeight;
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

                DiagnosticLog.Debug("SkiaItemsView", $"Tap at Y={e.Y}, screenBounds.Top={screenBounds.Top}, scrollOffset={_scrollOffset}, localY={localY}, index={tappedIndex}");

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
        RaiseItemTapped(index, item);
    }

    /// <summary>Raises <see cref="ItemTapped"/> for the row at <paramref name="index"/>.</summary>
    protected void RaiseItemTapped(int index, object item)
    {
        ItemTapped?.Invoke(this, new ItemsViewItemTappedEventArgs(index, item));
        Invalidate();
    }

    /// <summary>The pointer position along the scroll axis.</summary>
    private float MainAxis(PointerEventArgs e) => IsHorizontal ? e.X : e.Y;

    public override void OnScroll(ScrollEventArgs e)
    {
        // A horizontal list scrolls with a horizontal wheel, or a vertical one when that is all there is.
        var delta = (IsHorizontal && e.DeltaX != 0 ? e.DeltaX : e.DeltaY) * 20;
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
        ScrollToIndex(index, ScrollToPosition.Start, animate);
    }

    public void ScrollToItem(object item, bool animate = true)
    {
        ScrollToItem(item, ScrollToPosition.Start, animate);
    }

    /// <summary>
    /// Scrolls the row at <paramref name="index"/> into view at <paramref name="position"/>:
    /// the start, center or end of the viewport, or (MakeVisible) as little as it takes,
    /// as MAUI's ItemsView.ScrollTo places an item.
    /// </summary>
    public void ScrollToIndex(int index, ScrollToPosition position, bool animate = true)
    {
        if (index < 0 || index >= _items.Count) return;

        var itemStart = GetItemOffset(index);
        var itemLength = GetItemHeight(index);
        var viewport = ViewportExtent;
        var targetOffset = position switch
        {
            ScrollToPosition.Center => itemStart - (viewport - itemLength) / 2,
            ScrollToPosition.End => itemStart + itemLength - viewport,
            ScrollToPosition.MakeVisible when itemStart >= _scrollOffset && itemStart + itemLength <= _scrollOffset + viewport => _scrollOffset,
            ScrollToPosition.MakeVisible when itemStart + itemLength > _scrollOffset + viewport && itemLength <= viewport => itemStart + itemLength - viewport,
            _ => itemStart,
        };
        SetScrollOffset(targetOffset);
    }

    /// <summary>Scrolls the row of <paramref name="item"/> into view; see <see cref="ScrollToIndex(int, ScrollToPosition, bool)"/>.</summary>
    public void ScrollToItem(object item, ScrollToPosition position, bool animate = true)
    {
        var index = _items.IndexOf(item);
        if (index >= 0)
        {
            ScrollToIndex(index, position, animate);
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
                SetScrollOffset(_scrollOffset - ViewportExtent);
                e.Handled = true;
                break;

            case Key.PageDown:
                SetScrollOffset(_scrollOffset + ViewportExtent);
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
        else if (itemBottom > _scrollOffset + ViewportExtent)
        {
            SetScrollOffset(itemBottom - ViewportExtent);
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
        if (!IsVisible || !Bounds.Contains(x, y))
            return null;

        // Check scrollbar area FIRST before content
        // This ensures scrollbar clicks are handled by this view
        if (!IsHorizontal && _showVerticalScrollBar && TotalContentHeight > (float)Bounds.Height)
        {
            var trackArea = new SKRect((float)(Bounds.Left + Bounds.Width) - _scrollBarWidth, (float)Bounds.Top, (float)(Bounds.Left + Bounds.Width), (float)(Bounds.Top + Bounds.Height));
            if (trackArea.Contains(x, y))
                return this;
        }

        // The empty view takes input like any content (its "add the first one" button).
        if (ShowsEmptyView && _emptyViewContent!.HitTestAt(x, y) is { } emptyHit)
            return emptyHit;

        // A control inside a row takes the pointer, as on the other platforms: a button, an
        // entry, a view with its own tap recognizer below the row's root (CiteLynq's article
        // card: a ContentView whose Border opens the article). Taps on plain row content, and
        // recognizers on the row's root, stay with the list (item tap, selection, drag).
        if (RowControlAt(x, y) is { } control)
            return control;

        return this;
    }

    /// <inheritdoc />
    internal override SkiaView? InnermostViewAt(float x, float y)
    {
        for (int i = _shownMin; i <= _shownMax; i++)
        {
            if (_itemViewCache.TryGetValue(i, out var row) && row != null && row.Bounds.Contains(x, y))
                return row.HitTestAt(x, y);
        }
        return null;
    }

    private SkiaView? RowControlAt(float x, float y)
    {
        for (int i = _shownMin; i <= _shownMax; i++)
        {
            if (!_itemViewCache.TryGetValue(i, out var row) || row == null || !row.Bounds.Contains(x, y))
                continue;
            var hit = row.HitTestAt(x, y);
            // An open swipe view row takes the tap (its item, or closing it), as on MAUI.
            if (hit is SkiaSwipeView { IsOpen: true })
                return hit;
            if (hit == null || ReferenceEquals(hit, row))
                return null;
            return SkiaLayoutView.ClaimsInput(hit, row) ? hit : null;
        }
        return null;
    }

    /// <summary>
    /// The height a horizontal list wants: its tallest item (the first ones, measured at
    /// <paramref name="height"/>), margins included.
    /// </summary>
    protected virtual float NaturalCrossExtent(float height)
    {
        float cross = 0;
        if (_items.Count == 0 && _emptyViewContent is { IsVisible: true } empty)
        {
            var size = empty.Measure(new Size(double.PositiveInfinity, height));
            return (float)(size.Height + empty.Margin.VerticalThickness);
        }
        for (int i = 0; i < _items.Count && i < NaturalMeasureLimit; i++)
        {
            EnsureItemMeasured(i, height);
            if (_itemViewCache.TryGetValue(i, out var view) && view != null)
                cross = Math.Max(cross, (float)(view.DesiredSize.Height + view.Margin.VerticalThickness));
        }
        return cross;
    }

    /// <summary>
    /// A horizontal list is as wide as its items (and header and footer), up to the room it is
    /// given, as MAUI sizes a CollectionView to its content on every platform, and as tall as
    /// its tallest item.
    /// </summary>
    private Size MeasureHorizontal(Size availableSize)
    {
        bool finiteHeight = availableSize.Height < double.MaxValue && !double.IsInfinity(availableSize.Height);
        bool finiteWidth = availableSize.Width < double.MaxValue && !double.IsInfinity(availableSize.Width);
        var crossConstraint = finiteHeight ? (float)availableSize.Height : float.PositiveInfinity;
        var cross = NaturalCrossExtent(crossConstraint);
        float extent;
        if (_items.Count == 0 && _emptyViewContent is { IsVisible: true } empty)
            extent = (float)(empty.DesiredSize.Width + empty.Margin.HorizontalThickness);
        else
            extent = TotalContentHeight;
        var width = finiteWidth ? Math.Min(availableSize.Width, extent) : extent;
        var height = finiteHeight ? Math.Min(availableSize.Height, cross) : cross;
        return new Size(width, height);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsHorizontal)
            return MeasureHorizontal(availableSize);

        var width = availableSize.Width < double.MaxValue ? availableSize.Width : 200;
        // As tall as its content, up to the room it is given, as on the other
        // platforms: an empty list without an EmptyView asks for no room. A
        // Star row or Fill alignment still stretches it when it is arranged,
        // and it scrolls when the content is taller than the room.
        var natural = NaturalHeight((float)width);
        var height = availableSize.Height < double.MaxValue ? Math.Min(availableSize.Height, natural) : natural;

        // Track width changes but don't preemptively clear caches: parent layouts often
        // probe with two different widths in a single measure pass (e.g. infinite during
        // Auto-column measure, then constrained during the actual measure), and clearing
        // here thrashes the cache to "no entries" before DrawItem can ever store the
        // measured heights. DrawItem updates _itemHeights[i] on every paint when the
        // measurement changes, so width changes propagate naturally without an eager wipe.
        if (Math.Abs(width - _lastMeasuredWidth) > 5)
        {
            _lastMeasuredWidth = (float)width;
        }

        // Items view takes all available space
        return new Size(width, height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _collectionSubscription?.Dispose();
            _collectionSubscription = null;
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Gets the SkiaView for a given item index from the cache.
    /// </summary>
    public SkiaView? GetItemView(int index)
    {
        if (!_itemViewCache.TryGetValue(index, out var view))
        {
            return null;
        }
        return view;
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
