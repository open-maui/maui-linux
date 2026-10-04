// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using System.Collections;
using System.Collections.Specialized;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Rendering;
using ItemsUpdatingScrollMode = Microsoft.Maui.Controls.ItemsUpdatingScrollMode;
using ItemSizingStrategy = Microsoft.Maui.Controls.ItemSizingStrategy;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Base class for Skia-rendered items views (CollectionView, ListView).
/// Provides item rendering, scrolling, and virtualization.
/// </summary>
/// <remarks>
/// The items are laid out in lines along the scroll axis: one item per line for a list, and
/// <see cref="Span"/> items per line for a grid (a row of a vertical grid, a column of a
/// horizontal one). A line is as long as its longest item. Item extents and offsets are along
/// the scroll axis (Y for a vertical list, X for a horizontal one).
/// </remarks>
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
            Subscribe(value);
            CancelReorder();
            RefreshItems();
            InvalidateMeasure();
            Invalidate();
        }
    }

    private void Subscribe(IEnumerable? source)
    {
        if (ReferenceEquals(_collectionSubscription?.Source, source) && source != null)
        {
            _itemsSource = source;
            return;
        }
        _collectionSubscription?.Dispose();
        _collectionSubscription = null;

        _itemsSource = source;
        if (source is INotifyCollectionChanged newCollection)
        {
            _collectionSubscription = new WeakCollectionChangedProxy<SkiaItemsView>(newCollection, this,
                static (view, sender, e) => view.OnCollectionChanged(sender, e));
        }
    }

    /// <summary>
    /// Shows <paramref name="items"/> in place of the current items without starting over: the
    /// views of the items still shown are kept (an item is the same when it is the same object),
    /// the selection keeps the items still there, and the scroll position follows
    /// <see cref="ItemsUpdatingScrollMode"/>, as a collection change does. Setting
    /// <see cref="ItemsSource"/> instead starts over at the first item.
    /// </summary>
    public void UpdateItemsSource(IEnumerable? items)
    {
        Subscribe(items);
        ApplyItemsChange(null);
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

    /// <summary>The space between two lines along the scroll axis (a list's item spacing).</summary>
    public float ItemSpacing
    {
        get => _itemSpacing;
        set
        {
            _itemSpacing = value;
            InvalidateMeasure();
            Invalidate();
        }
    }

    private float _crossItemSpacing;

    /// <summary>
    /// The space between two items of a grid line, across the scroll axis: a vertical grid's
    /// HorizontalItemSpacing, a horizontal grid's VerticalItemSpacing.
    /// </summary>
    public float CrossItemSpacing
    {
        get => _crossItemSpacing;
        set
        {
            _crossItemSpacing = Math.Max(0, value);
            InvalidateMeasure();
            Invalidate();
        }
    }

    /// <summary>
    /// The items in a line across the scroll axis: 1 for a list, a grid's Span. A vertical grid
    /// fills each row left to right; a horizontal grid fills each column top to bottom, as
    /// MAUI's GridItemsLayout does on every platform.
    /// </summary>
    protected virtual int Span => 1;

    /// <summary>
    /// Where the list scrolls when its items change (MAUI's ItemsView.ItemsUpdatingScrollMode).
    /// KeepItemsInView (the default) keeps the items in view: a list at its start stays there
    /// (an item inserted first is shown), and a scrolled list keeps its first visible item where
    /// it is, so items added or removed above it do not move what is shown; KeepScrollOffset
    /// keeps the scroll offset; KeepLastItemInView scrolls to the last item after every change.
    /// </summary>
    public ItemsUpdatingScrollMode ItemsUpdatingScrollMode { get; set; }

    private ItemSizingStrategy _itemSizingStrategy;

    /// <summary>
    /// MeasureFirstItem gives every item the size of the first one (only the first is measured to
    /// place the rows), as MAUI's ItemSizingStrategy does; MeasureAllItems measures each item.
    /// </summary>
    public ItemSizingStrategy ItemSizingStrategy
    {
        get => _itemSizingStrategy;
        set
        {
            if (_itemSizingStrategy == value)
                return;
            _itemSizingStrategy = value;
            InvalidateMeasure();
            Invalidate();
        }
    }

    public ScrollBarVisibility VerticalScrollBarVisibility { get; set; } = ScrollBarVisibility.Default;
    public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; } = ScrollBarVisibility.Never;

    /// <summary>True when a vertical list shows (and takes input on) its scroll bar.</summary>
    private bool ShowsVerticalScrollBar => !IsHorizontal && VerticalScrollBarVisibility != ScrollBarVisibility.Never;

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

    // The cells drawn in the last frame (item index and rectangle, relative to the list's
    // top-left corner): what a tap or a reorder drag lands on, in any layout.
    private List<(int Index, SKRect Rect)> _cellsDrawing = new();
    private List<(int Index, SKRect Rect)> _cellsShown = new();

    /// <summary>Views kept beyond the drawn rows before recycling starts (minimum 64).</summary>
    internal int ItemViewCacheSlack { get; set; } = 64;

    /// <summary>Records that the row at <paramref name="index"/> was drawn this frame.</summary>
    protected void NoteItemDrawn(int index)
    {
        if (index < _drawnMin) _drawnMin = index;
        if (index > _drawnMax) _drawnMax = index;
    }

    /// <summary>
    /// Records the cell of the item at <paramref name="index"/> drawn this frame at
    /// <paramref name="cell"/> in a list drawn at <paramref name="bounds"/>: taps and reorder
    /// drags find the item under the pointer through it.
    /// </summary>
    protected void NoteCellDrawn(int index, SKRect cell, SKRect bounds)
    {
        var local = new SKRect(cell.Left - bounds.Left, cell.Top - bounds.Top, cell.Right - bounds.Left, cell.Bottom - bounds.Top);
        _cellsDrawing.Add((index, local));
    }

    public override void Draw(SKCanvas canvas)
    {
        _drawnMin = int.MaxValue;
        _drawnMax = -1;
        _cellsDrawing.Clear();
        base.Draw(canvas);
        _shownMin = _drawnMin;
        _shownMax = _drawnMax;
        (_cellsShown, _cellsDrawing) = (_cellsDrawing, _cellsShown);
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
            if ((index < keepFrom || index > keepTo) && index != _reorderFrom)
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

    // The cross size the rows were last measured at (a cell's width in a vertical list), so
    // rows added by a collection change are measured like the others before the list scrolls.
    private float _lastCellCrossSize;

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
        ForgetShownRows();
    }

    /// <summary>
    /// Called after the items changed in place (a collection change, or
    /// <see cref="UpdateItemsSource"/>), once the rows and the scroll position are updated.
    /// </summary>
    protected virtual void OnItemsChanged()
    {
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

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ApplyItemsChange(e);

    // --- Collection changes ------------------------------------------------------------------

    /// <summary>
    /// Applies a change of the items source in place, as MAUI's items views do on every
    /// platform: only the rows that changed are made again (the others keep their views and
    /// measured sizes), and the scroll position follows <see cref="ItemsUpdatingScrollMode"/>.
    /// A null change (or one that does not match the items shown) re-reads the source and keeps
    /// the views of the items still there.
    /// </summary>
    private void ApplyItemsChange(NotifyCollectionChangedEventArgs? e)
    {
        CancelReorder();
        var anchor = CaptureAnchor();
        bool applied = e != null && e.Action switch
        {
            NotifyCollectionChangedAction.Add => TryInsert(e.NewStartingIndex, e.NewItems),
            NotifyCollectionChangedAction.Remove => TryRemove(e.OldStartingIndex, e.OldItems),
            NotifyCollectionChangedAction.Replace => TryReplace(e.OldStartingIndex, e.OldItems, e.NewItems),
            NotifyCollectionChangedAction.Move => TryMove(e.OldStartingIndex, e.NewStartingIndex, e.OldItems),
            _ => false,
        };
        if (applied && _itemsSource is ICollection collection && collection.Count != _items.Count)
            applied = false;
        if (!applied)
            Resync();
        ForgetShownRows();

        RestoreScrollPosition(anchor);
        OnItemsChanged();
        InvalidateMeasure();
        Invalidate();
    }

    private void ForgetShownRows()
    {
        _shownMin = int.MaxValue;
        _shownMax = -1;
        _cellsShown.Clear();
    }

    /// <summary>Moves the cached views and sizes of the items at or after <paramref name="from"/> by <paramref name="delta"/>.</summary>
    private void ShiftRows(int from, int delta)
    {
        if (delta == 0)
            return;
        if (_itemViewCache.Keys.Any(k => k >= from))
        {
            var moved = _itemViewCache.Where(kv => kv.Key >= from).ToList();
            foreach (var kv in moved)
                _itemViewCache.Remove(kv.Key);
            foreach (var kv in moved)
                _itemViewCache[kv.Key + delta] = kv.Value;
        }
        if (_itemHeights.Keys.Any(k => k >= from))
        {
            var moved = _itemHeights.Where(kv => kv.Key >= from).ToList();
            foreach (var kv in moved)
                _itemHeights.Remove(kv.Key);
            foreach (var kv in moved)
                _itemHeights[kv.Key + delta] = kv.Value;
        }
    }

    /// <summary>Drops the views and sizes of the rows [<paramref name="index"/>, +<paramref name="count"/>).</summary>
    private void DropRows(int index, int count)
    {
        for (int i = index; i < index + count; i++)
        {
            if (_itemViewCache.Remove(i, out var view) && view != null)
                ReleaseItemView(view);
            _itemHeights.Remove(i);
        }
    }

    private bool TryInsert(int index, IList? items)
    {
        if (items == null || items.Count == 0)
            return items != null;
        if (index < 0)
            index = _items.Count;
        if (index > _items.Count)
            return false;
        ShiftRows(index, items.Count);
        for (int i = 0; i < items.Count; i++)
            _items.Insert(index + i, items[i]!);
        return true;
    }

    private bool TryRemove(int index, IList? items)
    {
        if (items == null || index < 0 || index + items.Count > _items.Count)
            return false;
        for (int i = 0; i < items.Count; i++)
            if (!Equals(_items[index + i], items[i]))
                return false;
        DropRows(index, items.Count);
        _items.RemoveRange(index, items.Count);
        ShiftRows(index + items.Count, -items.Count);
        return true;
    }

    private bool TryReplace(int index, IList? oldItems, IList? newItems)
    {
        if (oldItems == null || newItems == null || oldItems.Count != newItems.Count
            || index < 0 || index + oldItems.Count > _items.Count)
            return false;
        DropRows(index, oldItems.Count);
        for (int i = 0; i < newItems.Count; i++)
            _items[index + i] = newItems[i]!;
        return true;
    }

    private bool TryMove(int oldIndex, int newIndex, IList? items)
    {
        if (items == null || items.Count == 0 || oldIndex < 0 || newIndex < 0
            || oldIndex + items.Count > _items.Count || newIndex + items.Count > _items.Count)
            return false;
        int count = items.Count;
        // The moved rows keep their views and sizes: they show the same items.
        var views = new SkiaView?[count];
        var heights = new float?[count];
        for (int i = 0; i < count; i++)
        {
            views[i] = _itemViewCache.Remove(oldIndex + i, out var v) ? v : null;
            heights[i] = _itemHeights.Remove(oldIndex + i, out var h) ? h : null;
        }
        var moved = _items.GetRange(oldIndex, count);
        _items.RemoveRange(oldIndex, count);
        ShiftRows(oldIndex + count, -count);
        ShiftRows(newIndex, count);
        _items.InsertRange(newIndex, moved);
        for (int i = 0; i < count; i++)
        {
            if (views[i] != null) _itemViewCache[newIndex + i] = views[i]!;
            if (heights[i] is { } height) _itemHeights[newIndex + i] = height;
        }
        return true;
    }

    /// <summary>
    /// Re-reads the source. An item still there keeps its view and size (matched by identity, or
    /// by equality for rows that are not items, such as a group's header).
    /// </summary>
    private void Resync()
    {
        var oldRows = new Dictionary<object, Queue<int>>(RowIdentityComparer.Instance);
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item == null) continue;
            if (!oldRows.TryGetValue(item, out var queue))
                oldRows[item] = queue = new Queue<int>();
            queue.Enqueue(i);
        }

        var newItems = new List<object>();
        if (_itemsSource != null)
            foreach (var item in _itemsSource)
                newItems.Add(item);

        var views = new Dictionary<int, SkiaView>();
        var heights = new Dictionary<int, float>();
        for (int i = 0; i < newItems.Count; i++)
        {
            var item = newItems[i];
            if (item == null || !oldRows.TryGetValue(item, out var queue) || queue.Count == 0)
                continue;
            var old = queue.Dequeue();
            if (_itemViewCache.Remove(old, out var view) && view != null)
                views[i] = view;
            if (_itemHeights.TryGetValue(old, out var height))
                heights[i] = height;
        }
        foreach (var view in _itemViewCache.Values.ToList())
        {
            if (view != null)
                ReleaseItemView(view);
        }
        _itemViewCache.Clear();
        foreach (var kv in views)
            _itemViewCache[kv.Key] = kv.Value;
        _itemHeights.Clear();
        foreach (var kv in heights)
            _itemHeights[kv.Key] = kv.Value;
        _items = newItems;
    }

    /// <summary>Items are the same object; other rows (group headers) are the same when equal.</summary>
    private sealed class RowIdentityComparer : IEqualityComparer<object>
    {
        public static readonly RowIdentityComparer Instance = new();

        public new bool Equals(object? x, object? y) =>
            ReferenceEquals(x, y) || (x is INonSelectableItem && x.Equals(y));

        public int GetHashCode(object obj) =>
            obj is INonSelectableItem ? obj.GetHashCode() : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// The first item shown, and how far the list is scrolled past its start. None while the list
    /// is at its start: it stays there, showing the first item (an item inserted first is shown).
    /// </summary>
    private (object Item, int Index, float Delta)? CaptureAnchor()
    {
        if (_items.Count == 0 || _scrollOffset <= 0)
            return null;
        var line = LineAt(_scrollOffset);
        if (line < 0)
            return null;
        var index = ItemAtSlot(line * Span);
        if (index < 0 || index >= _items.Count)
            return null;
        return (_items[index], index, _scrollOffset - GetLineOffset(line));
    }

    /// <summary>Scrolls as <see cref="ItemsUpdatingScrollMode"/> asks after the items changed.</summary>
    private void RestoreScrollPosition((object Item, int Index, float Delta)? anchor)
    {
        switch (ItemsUpdatingScrollMode)
        {
            case ItemsUpdatingScrollMode.KeepLastItemInView:
                if (_items.Count > 0)
                {
                    MeasureRows(0, _items.Count);
                    SetScrollOffset(MaxScrollOffset);
                }
                else
                {
                    SetScrollOffset(0);
                }
                break;

            case ItemsUpdatingScrollMode.KeepScrollOffset:
                SetScrollOffset(_scrollOffset);
                break;

            default:
                if (anchor is { } a && _items.Count > 0)
                {
                    // The anchor item where it is now (it may have moved), or the one in its place.
                    int index = FindNear(a.Item, a.Index);
                    float delta = a.Delta;
                    if (index < 0)
                    {
                        index = Math.Clamp(a.Index, 0, _items.Count - 1);
                        delta = 0;
                    }
                    MeasureRows(0, index + 1);
                    SetScrollOffset(GetItemOffset(index) + delta);
                }
                else
                {
                    SetScrollOffset(_scrollOffset);
                }
                break;
        }
    }

    private int FindNear(object item, int near)
    {
        for (int d = 0; d < _items.Count; d++)
        {
            int before = near - d, after = near + d;
            if (before >= 0 && before < _items.Count && ReferenceEquals(_items[before], item)) return before;
            if (after >= 0 && after < _items.Count && ReferenceEquals(_items[after], item)) return after;
            if (before < 0 && after >= _items.Count) break;
        }
        return -1;
    }

    /// <summary>Measures the rows [<paramref name="from"/>, <paramref name="to"/>) not measured yet, at the size the list measures its rows.</summary>
    private void MeasureRows(int from, int to)
    {
        if (_lastCellCrossSize <= 0 || ItemViewCreator == null)
            return;
        for (int i = from; i < to && i < _items.Count; i++)
            EnsureItemMeasured(i, _lastCellCrossSize);
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
        if (availableWidth > 0 && !float.IsInfinity(availableWidth))
            _lastCellCrossSize = availableWidth;
        // MeasureFirstItem: every row takes the first item's size.
        if (index > 0 && _itemSizingStrategy == ItemSizingStrategy.MeasureFirstItem)
            index = 0;
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
        if (index > 0 && _itemSizingStrategy == ItemSizingStrategy.MeasureFirstItem)
            index = 0;
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

    // --- Lines: the layout along the scroll axis -----------------------------------------------

    /// <summary>The number of lines (rows of a vertical grid, columns of a horizontal one).</summary>
    protected int LineCount
    {
        get
        {
            var span = Math.Max(1, Span);
            return (_items.Count + span - 1) / span;
        }
    }

    /// <summary>The cross size of a cell in a line <paramref name="crossExtent"/> long (the grid's span and spacing taken out).</summary>
    protected float CellCrossSize(float crossExtent)
    {
        var span = Math.Max(1, Span);
        if (span == 1)
            return crossExtent;
        return Math.Max(0, (crossExtent - _crossItemSpacing * (span - 1)) / span);
    }

    /// <summary>The length of a line along the scroll axis: its longest item.</summary>
    protected float GetLineExtent(int line)
    {
        var span = Math.Max(1, Span);
        float extent = 0;
        for (int slot = line * span; slot < (line + 1) * span && slot < _items.Count; slot++)
            extent = Math.Max(extent, GetItemHeight(ItemAtSlot(slot)));
        return extent;
    }

    /// <summary>Where a line starts along the scroll axis (the header before it included).</summary>
    protected float GetLineOffset(int line)
    {
        float offset = LeadingContentHeight;
        var lines = LineCount;
        for (int l = 0; l < line && l < lines; l++)
            offset += GetLineExtent(l) + _itemSpacing;
        return offset;
    }

    /// <summary>The line at <paramref name="offset"/> along the scroll axis (the first one past it), or -1 when there are none.</summary>
    private int LineAt(float offset)
    {
        var lines = LineCount;
        if (lines == 0)
            return -1;
        float position = LeadingContentHeight;
        for (int l = 0; l < lines; l++)
        {
            var extent = GetLineExtent(l);
            if (position + extent > offset)
                return l;
            position += extent + _itemSpacing;
        }
        return lines - 1;
    }

    /// <summary>
    /// Gets the Y offset for a specific item (cumulative height of all previous items).
    /// </summary>
    protected float GetItemOffset(int index) => GetLineOffset(SlotOfItem(index) / Math.Max(1, Span));

    /// <summary>
    /// The indexes of the first, center and last items in view (-1 when there are none), as
    /// MAUI reports them in ItemsViewScrolledEventArgs.
    /// </summary>
    public (int First, int Center, int Last) GetVisibleItemRange()
    {
        var lines = LineCount;
        var viewport = ViewportExtent;
        if (lines == 0 || viewport <= 0)
            return (-1, -1, -1);
        var span = Math.Max(1, Span);
        float start = _scrollOffset, end = _scrollOffset + viewport, middle = _scrollOffset + viewport / 2;
        int firstLine = -1, lastLine = -1, centerLine = -1;
        float position = LeadingContentHeight;
        for (int l = 0; l < lines; l++)
        {
            var extent = GetLineExtent(l);
            if (position + extent > start && position < end)
            {
                if (firstLine < 0) firstLine = l;
                lastLine = l;
            }
            if (centerLine < 0 && position + extent > middle)
                centerLine = l;
            if (position >= end)
                break;
            position += extent + _itemSpacing;
        }
        if (firstLine < 0)
            return (-1, -1, -1);
        if (centerLine < 0) centerLine = lastLine;
        int first = ItemAtSlot(firstLine * span);
        int last = ItemAtSlot(Math.Min(_items.Count - 1, lastLine * span + span - 1));
        int center = ItemAtSlot(Math.Min(_items.Count - 1, centerLine * span + (span - 1) / 2));
        return (first, center, last);
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
        var cell = CellCrossSize(width);
        for (int i = 0; i < _items.Count && i < NaturalMeasureLimit; i++)
            EnsureItemMeasured(i, cell);
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
            var lines = LineCount;
            for (int l = 0; l < lines; l++)
            {
                total += GetLineExtent(l);
                if (l < lines - 1) total += _itemSpacing;
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

        var showBar = ShowsVerticalScrollBar;
        // Content width excludes the scrollbar gutter (matches the itemRect below)
        var contentWidth = bounds.Width - (showBar ? _scrollBarWidth : 0);

        // Find first visible index by walking through items.
        // Measure each item before using its height so the first frame after a
        // cache refresh positions rows correctly (no mis-layout flash).
        _firstVisibleIndex = 0;
        float cumulativeOffset = LeadingContentHeight;
        for (int i = 0; i < _items.Count; i++)
        {
            EnsureItemMeasured(ItemAtSlot(i), contentWidth);
            var itemH = GetItemHeight(ItemAtSlot(i));
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

        float currentY = bounds.Top + GetLineOffset(_firstVisibleIndex) - _scrollOffset;
        for (int slot = _firstVisibleIndex; slot < _items.Count; slot++)
        {
            int i = ItemAtSlot(slot);
            EnsureItemMeasured(i, contentWidth);
            var itemH = GetItemHeight(i);
            var itemRect = new SKRect(bounds.Left, currentY, bounds.Right - (showBar ? _scrollBarWidth : 0), currentY + itemH);

            // Stop if we've passed the visible area
            if (itemRect.Top > bounds.Bottom)
            {
                _lastVisibleIndex = i - 1;
                break;
            }

            _lastVisibleIndex = i;

            if (itemRect.Bottom >= bounds.Top && i != DraggedItemIndex)
            {
                NoteCellDrawn(i, itemRect, bounds);
                DrawItem(canvas, _items[i], i, itemRect, paint);
            }

            currentY += itemH + _itemSpacing;
        }

        DrawDraggedItem(canvas, bounds, paint);
        canvas.Restore();

        // Draw scrollbar
        if (showBar && (TotalContentHeight > bounds.Height || VerticalScrollBarVisibility == ScrollBarVisibility.Always))
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
        var total = Math.Max(TotalContentHeight, bounds.Height);
        var viewportRatio = total > 0 ? bounds.Height / total : 1f;
        var thumbHeight = Math.Max(20, bounds.Height * viewportRatio);
        var scrollRatio = MaxScrollOffset > 0 ? _scrollOffset / MaxScrollOffset : 0f;
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

    // --- Reordering ----------------------------------------------------------------------------

    private bool _canReorderItems;

    /// <summary>
    /// Lets the user drag an item to another place in the list (MAUI's CanReorderItems). A drag
    /// that starts on an item moves it, as a mouse drag does on Windows: the item follows the
    /// pointer, the others make room, and the list scrolls when the pointer nears its edge.
    /// Dropping it raises <see cref="ItemReordered"/>; the owner moves the item in its source.
    /// Escape cancels the drag. The list still scrolls with the wheel and the scroll bar.
    /// </summary>
    public bool CanReorderItems
    {
        get => _canReorderItems;
        set
        {
            if (_canReorderItems == value)
                return;
            _canReorderItems = value;
            if (!value)
                CancelReorder();
        }
    }

    /// <summary>Whether the item at an index can be dragged (null: every item can).</summary>
    public Func<int, bool>? CanDragItem { get; set; }

    /// <summary>
    /// Whether the dragged item (first argument) can be dropped where it would end up at the
    /// index of the second (null: anywhere). A grouped list keeps an item off group headers, or
    /// in its own group.
    /// </summary>
    public Func<int, int, bool>? CanDropItem { get; set; }

    /// <summary>Raised when a reorder drag ends: the item, and the index it was dropped at (the same when it did not move).</summary>
    public event EventHandler<ItemsReorderedEventArgs>? ItemReordered;

    /// <summary>True while an item is being dragged to a new place.</summary>
    public bool IsReordering => _reorderFrom >= 0 && _reorderStarted;

    private const float ReorderDragThreshold = 8f;
    private const float ReorderAutoScrollZone = 32f;

    private int _reorderFrom = -1;     // the dragged item
    private int _reorderTo = -1;       // the index it would be dropped at
    private bool _reorderStarted;
    private float _reorderPressX, _reorderPressY;   // list-local
    private float _reorderPointerX, _reorderPointerY;
    private SKRect _reorderCell;        // the item's cell at the press, list-local

    /// <summary>The item being dragged (its row is left empty while it follows the pointer), or -1.</summary>
    protected int DraggedItemIndex => IsReordering ? _reorderFrom : -1;

    /// <summary>The item shown at a place in the list: during a reorder drag the others move up or down to make room.</summary>
    protected int ItemAtSlot(int slot)
    {
        if (!IsReordering || _reorderFrom == _reorderTo)
            return slot;
        int from = _reorderFrom, to = _reorderTo;
        if (slot == to) return from;
        if (from < to && slot >= from && slot < to) return slot + 1;
        if (from > to && slot > to && slot <= from) return slot - 1;
        return slot;
    }

    /// <summary>The place in the list an item is shown at (see <see cref="ItemAtSlot"/>).</summary>
    protected int SlotOfItem(int index)
    {
        if (!IsReordering || _reorderFrom == _reorderTo)
            return index;
        int from = _reorderFrom, to = _reorderTo;
        if (index == from) return to;
        if (from < to && index > from && index <= to) return index - 1;
        if (from > to && index >= to && index < from) return index + 1;
        return index;
    }

    /// <summary>Stops a reorder drag without moving anything.</summary>
    private void CancelReorder()
    {
        if (_reorderFrom < 0)
            return;
        _reorderFrom = _reorderTo = -1;
        _reorderStarted = false;
        Invalidate();
    }

    /// <summary>The item whose cell (as last drawn) is at the list-local point, or -1.</summary>
    private int CellAt(float x, float y)
    {
        foreach (var (index, rect) in _cellsShown)
            if (rect.Contains(x, y))
                return index;
        return -1;
    }

    private SKRect CellOf(int index)
    {
        foreach (var (i, rect) in _cellsShown)
            if (i == index)
                return rect;
        return SKRect.Empty;
    }

    /// <summary>Draws the dragged item under the pointer, over the others.</summary>
    protected void DrawDraggedItem(SKCanvas canvas, SKRect bounds, SKPaint paint)
    {
        if (!IsReordering || _reorderFrom >= _items.Count)
            return;
        var left = bounds.Left + _reorderCell.Left + (_reorderPointerX - _reorderPressX);
        var top = bounds.Top + _reorderCell.Top + (_reorderPointerY - _reorderPressY);
        var rect = new SKRect(left, top, left + _reorderCell.Width, top + _reorderCell.Height);
        // Translucent, as a dragged list item is shown on Windows.
        using var layer = new SKPaint { Color = SKColors.White.WithAlpha(204) };
        canvas.SaveLayer(layer);
        DrawItem(canvas, _items[_reorderFrom], _reorderFrom, rect, paint);
        canvas.Restore();
    }

    private void UpdateReorderTarget()
    {
        int under = CellAt(_reorderPointerX, _reorderPointerY);
        if (under < 0 || under == _reorderFrom)
            return;
        int slot = SlotOfItem(under);
        if (slot == _reorderTo)
            return;
        if (CanDropItem != null && !CanDropItem(_reorderFrom, slot))
            return;
        _reorderTo = slot;
        Invalidate();
    }

    private void AutoScrollForReorder()
    {
        var main = IsHorizontal ? _reorderPointerX : _reorderPointerY;
        var viewport = ViewportExtent;
        float step = 0;
        if (main < ReorderAutoScrollZone)
            step = -(ReorderAutoScrollZone - main) / 2;
        else if (main > viewport - ReorderAutoScrollZone)
            step = (main - (viewport - ReorderAutoScrollZone)) / 2;
        if (step != 0)
            SetScrollOffset(_scrollOffset + step);
    }

    private void FinishReorder()
    {
        int from = _reorderFrom, to = _reorderTo;
        _reorderFrom = _reorderTo = -1;
        _reorderStarted = false;
        Invalidate();
        try
        {
            ItemReordered?.Invoke(this, new ItemsReorderedEventArgs(from, to));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaItemsView", "Reordering an item failed", ex);
        }
    }

    // --- Pointer events for row content ------------------------------------------------------

    // The list keeps presses on plain row content (item tap, selection, scrolling, reordering),
    // yet on Windows the content under the pointer still gets its pointer events: a
    // TouchBehavior or a PointerGestureRecognizer in a row works, and the item is still
    // selected. The MAUI views of the row content under the pointer (innermost first) and of the
    // press in progress are told about the list's pointer events through PointerRouted and their
    // PointerGestureRecognizers. Controls in a row (a button) take the pointer themselves.
    private readonly List<Microsoft.Maui.Controls.View> _rowContentHovered = new();
    private List<Microsoft.Maui.Controls.View>? _rowContentPressed;

    /// <summary>The MAUI views of the row content under a point (list space), innermost first.</summary>
    private List<Microsoft.Maui.Controls.View> RowContentAt(float x, float y)
    {
        var chain = new List<Microsoft.Maui.Controls.View>();
        if (float.IsNaN(x) || float.IsNaN(y) || !Bounds.Contains(x, y))
            return chain;
        for (var view = InnermostViewAt(x, y); view != null && !ReferenceEquals(view, this); view = view.Parent)
        {
            if (view.MauiView is Microsoft.Maui.Controls.View mauiView && !chain.Contains(mauiView))
                chain.Add(mauiView);
        }
        return chain;
    }

    /// <summary>
    /// Follows the row content under the pointer: the views it left get Exited, those it
    /// reached get Entered (outermost first).
    /// </summary>
    private void UpdateRowContentHover(PointerEventArgs e, bool inside)
    {
        var now = inside && !ShowsEmptyView ? RowContentAt(e.X, e.Y) : new List<Microsoft.Maui.Controls.View>();
        var left = _rowContentHovered.Where(v => !now.Contains(v)).ToList();
        var reached = now.Where(v => !_rowContentHovered.Contains(v)).Reverse().ToList();
        _rowContentHovered.Clear();
        _rowContentHovered.AddRange(now);
        RaiseOnRowContent(left, RoutedPointerKind.Exited, e);
        RaiseOnRowContent(reached, RoutedPointerKind.Entered, e);
    }

    /// <summary>
    /// Ends the row content's press without a release over it (the list scrolls or a reorder
    /// drag starts), as WinUI cancels a pointer a list takes over: the pointer leaves the
    /// content and is released away from it.
    /// </summary>
    private void CancelRowContentPress(PointerEventArgs e)
    {
        if (_rowContentPressed is not { } pressed)
            return;
        _rowContentPressed = null;
        _rowContentHovered.Clear();
        var away = new PointerEventArgs(-1_000_000f, -1_000_000f, e.Button);
        RaiseOnRowContent(pressed, RoutedPointerKind.Exited, away);
        RaiseOnRowContent(pressed, RoutedPointerKind.Released, away);
    }

    private void RaiseOnRowContent(List<Microsoft.Maui.Controls.View> views, RoutedPointerKind kind, PointerEventArgs e)
    {
        if (views.Count == 0)
            return;
        var windowE = InWindowSpace(e);
        var type = kind switch
        {
            RoutedPointerKind.Entered => GestureManager.PointerEventType.Entered,
            RoutedPointerKind.Exited => GestureManager.PointerEventType.Exited,
            RoutedPointerKind.Pressed => GestureManager.PointerEventType.Pressed,
            RoutedPointerKind.Released => GestureManager.PointerEventType.Released,
            _ => GestureManager.PointerEventType.Moved,
        };
        foreach (var view in views.ToArray())
        {
            try
            {
                GestureManager.ProcessPointerRecognizers(view, windowE.X, windowE.Y, type);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("SkiaItemsView", $"Pointer recognizer failed for {view.GetType().Name}", ex);
            }
            RaisePointerRouted(view, kind, windowE);
        }
    }

    public override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (_rowContentPressed == null && !_isDragging)
            UpdateRowContentHover(e, inside: true);
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        UpdateRowContentHover(e, inside: false);
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        DiagnosticLog.Debug("SkiaItemsView", $"OnPointerPressed - x={e.X}, y={e.Y}, Bounds={Bounds}, ScreenBounds={ScreenBounds}, ItemCount={_items.Count}");
        if (!IsEnabled) return;

        // Check if clicking on scrollbar thumb
        if (ShowsVerticalScrollBar && TotalContentHeight > Bounds.Height)
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

        // The row content under the pointer is pressed too, as on Windows; the list still
        // takes the press for the item tap, selection, scrolling and reordering.
        UpdateRowContentHover(e, inside: true);
        _rowContentPressed = new List<Microsoft.Maui.Controls.View>(_rowContentHovered);
        RaiseOnRowContent(_rowContentPressed, RoutedPointerKind.Pressed, e);

        // A press on an item of a reorderable list may start dragging it.
        _reorderFrom = -1;
        _reorderStarted = false;
        if (_canReorderItems)
        {
            var screen = ScreenBounds;
            float x = e.X - (float)screen.Left, y = e.Y - (float)screen.Top;
            int index = CellAt(x, y);
            if (index >= 0 && index < _items.Count && (CanDragItem?.Invoke(index) ?? true))
            {
                _reorderFrom = _reorderTo = index;
                _reorderPressX = _reorderPointerX = x;
                _reorderPressY = _reorderPointerY = y;
                _reorderCell = CellOf(index);
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

        if (_rowContentPressed != null)
        {
            RaiseOnRowContent(_rowContentPressed, RoutedPointerKind.Moved, e);
        }
        else if (!_isDragging && _reorderFrom < 0)
        {
            UpdateRowContentHover(e, inside: true);
            RaiseOnRowContent(_rowContentHovered, RoutedPointerKind.Moved, e);
        }

        if (_reorderFrom >= 0)
        {
            var screen = ScreenBounds;
            _reorderPointerX = e.X - (float)screen.Left;
            _reorderPointerY = e.Y - (float)screen.Top;
            if (!_reorderStarted)
            {
                var dx = _reorderPointerX - _reorderPressX;
                var dy = _reorderPointerY - _reorderPressY;
                if (dx * dx + dy * dy < ReorderDragThreshold * ReorderDragThreshold)
                    return;
                // The drag moves the item, not the list.
                _reorderStarted = true;
                _isDragging = false;
                CancelRowContentPress(e);
            }
            AutoScrollForReorder();
            UpdateReorderTarget();
            Invalidate();
            e.Handled = true;
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

        // Dragging scrolled the list: the row content's press is cancelled, as a WinUI list
        // takes a touch over (PointerCanceled) once it pans.
        if (_scrollOffset != _dragStartOffset)
            CancelRowContentPress(e);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        // Handle scrollbar drag release
        if (_isDraggingScrollbar)
        {
            _isDraggingScrollbar = false;
            return;
        }

        // The row content's release comes first, as WinUI raises PointerReleased before the
        // list's item click; then the hover follows the pointer again.
        if (_rowContentPressed is { } pressedContent)
        {
            _rowContentPressed = null;
            RaiseOnRowContent(pressedContent, RoutedPointerKind.Released, e);
        }
        UpdateRowContentHover(e, inside: true);

        if (_reorderFrom >= 0 && _reorderStarted)
        {
            _isDragging = false;
            FinishReorder();
            e.Handled = true;
            return;
        }
        _reorderFrom = _reorderTo = -1;

        if (_isDragging)
        {
            _isDragging = false;

            // Check for tap (minimal movement)
            var totalDrag = Math.Abs(MainAxis(e) - _dragStartY);
            if (totalDrag < 5)
            {
                var screenBounds = ScreenBounds;
                int tappedIndex = -1;
                if (_cellsShown.Count > 0)
                {
                    // The cell under the pointer, as the last frame drew it (any layout).
                    tappedIndex = CellAt(e.X - (float)screenBounds.Left, e.Y - (float)screenBounds.Top);
                }
                else
                {
                    // Not drawn yet: walk the rows.
                    var localY = IsHorizontal
                        ? e.X - (float)screenBounds.Left + _scrollOffset
                        : e.Y - (float)screenBounds.Top + _scrollOffset;
                    float cumulativeY = LeadingContentHeight;
                    for (int i = 0; i < _items.Count && Span == 1; i++)
                    {
                        var itemH = GetItemHeight(i);
                        if (localY >= cumulativeY && localY < cumulativeY + itemH)
                        {
                            tappedIndex = i;
                            break;
                        }
                        cumulativeY += itemH + _itemSpacing;
                    }
                }

                DiagnosticLog.Debug("SkiaItemsView", $"Tap at ({e.X},{e.Y}), screenBounds={screenBounds}, scrollOffset={_scrollOffset}, index={tappedIndex}");

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
            var (first, center, last) = GetVisibleItemRange();
            Scrolled?.Invoke(this, new ItemsScrolledEventArgs(_scrollOffset, TotalContentHeight)
            {
                FirstVisibleIndex = first,
                CenterIndex = center,
                LastVisibleIndex = last,
                ItemCount = _items.Count,
            });
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
        var itemLength = GetLineExtent(SlotOfItem(index) / Math.Max(1, Span));
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
            case Key.Escape when _reorderFrom >= 0:
                CancelReorder();
                _isDragging = false;
                e.Handled = true;
                break;

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
        var itemBottom = itemTop + GetLineExtent(SlotOfItem(index) / Math.Max(1, Span));

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
        if (ShowsVerticalScrollBar && TotalContentHeight > (float)Bounds.Height)
        {
            var trackArea = new SKRect((float)(Bounds.Left + Bounds.Width) - _scrollBarWidth, (float)Bounds.Top, (float)(Bounds.Left + Bounds.Width), (float)(Bounds.Top + Bounds.Height));
            if (trackArea.Contains(x, y))
                return this;
        }

        // The empty view takes input like any content (its "add the first one" button).
        if (ShowsEmptyView && _emptyViewContent!.HitTestAt(x, y) is { } emptyHit)
            return emptyHit;

        // A reorderable list takes the press on a row itself (the drag moves the row).
        if (IsReordering)
            return this;

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
    /// <paramref name="height"/>), margins included. A horizontal grid stacks <see cref="Span"/>
    /// such rows.
    /// </summary>
    protected virtual float NaturalCrossExtent(float height)
    {
        float cross = 0;
        if (_items.Count == 0 && _emptyViewContent is { IsVisible: true } empty)
        {
            var size = empty.Measure(new Size(double.PositiveInfinity, height));
            return (float)(size.Height + empty.Margin.VerticalThickness);
        }
        var span = Math.Max(1, Span);
        var cell = float.IsInfinity(height) ? height : CellCrossSize(height);
        for (int i = 0; i < _items.Count && i < NaturalMeasureLimit; i++)
        {
            EnsureItemMeasured(i, cell);
            if (_itemViewCache.TryGetValue(i, out var view) && view != null)
                cross = Math.Max(cross, (float)(view.DesiredSize.Height + view.Margin.VerticalThickness));
        }
        if (span > 1)
        {
            var rows = Math.Min(span, _items.Count);
            cross = cross * rows + _crossItemSpacing * Math.Max(0, rows - 1);
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

    /// <summary>The first item in view (-1 when none), as MAUI's ItemsViewScrolledEventArgs reports it.</summary>
    public int FirstVisibleIndex { get; init; } = -1;

    /// <summary>The item at the middle of the view (-1 when none).</summary>
    public int CenterIndex { get; init; } = -1;

    /// <summary>The last item in view (-1 when none).</summary>
    public int LastVisibleIndex { get; init; } = -1;

    /// <summary>The number of items (rows) in the list.</summary>
    public int ItemCount { get; init; }

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

/// <summary>A reorder drag ended (<see cref="SkiaItemsView.ItemReordered"/>).</summary>
public class ItemsReorderedEventArgs : EventArgs
{
    /// <summary>The index of the dragged item.</summary>
    public int FromIndex { get; }

    /// <summary>The index the item would have after the move (the same as <see cref="FromIndex"/> when it was dropped where it was).</summary>
    public int ToIndex { get; }

    public ItemsReorderedEventArgs(int fromIndex, int toIndex)
    {
        FromIndex = fromIndex;
        ToIndex = toIndex;
    }
}
