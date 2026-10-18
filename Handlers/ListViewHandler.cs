// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Specialized;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for the classic ListView on Linux, rendered with the platform's
/// <see cref="SkiaCollectionView"/>. Rows come from MAUI's own
/// <c>TemplatedItems</c> (the cell list every ListView renderer consumes):
/// ItemTemplate cells (TextCell, ViewCell, ...) or the default TextCell,
/// grouped with a header row per group when IsGroupingEnabled. Taps go
/// through <see cref="ListView.NotifyRowTapped(int, Cell)"/>, so
/// ItemSelected / ItemTapped and SelectedItem behave exactly as MAUI defines.
/// </summary>
public partial class ListViewHandler : LinuxViewHandler<ListView, SkiaCollectionView>
{
    /// <summary>One platform row: a cell, its position, and whether it heads a group.</summary>
    public sealed class Row
    {
        public Row(Cell cell, int groupIndex, int index, bool isGroupHeader)
        {
            Cell = cell;
            GroupIndex = groupIndex;
            Index = index;
            IsGroupHeader = isGroupHeader;
        }

        public Cell Cell { get; }
        // Kept up to date as items are added or removed around the row (the row keeps its view).
        public int GroupIndex { get; internal set; }
        public int Index { get; internal set; }
        public bool IsGroupHeader { get; }
        public object? Item => Cell.BindingContext;

        public override string ToString() => Cell is TextCell t ? t.Text ?? string.Empty : Item?.ToString() ?? string.Empty;
    }

    public static IPropertyMapper<ListView, ListViewHandler> Mapper =
        new PropertyMapper<ListView, ListViewHandler>(ViewHandler.ViewMapper)
        {
            [nameof(ListView.ItemsSource)] = MapItems,
            [nameof(ListView.ItemTemplate)] = MapItems,
            [nameof(ListView.IsGroupingEnabled)] = MapItems,
            [nameof(ListView.GroupDisplayBinding)] = MapItems,
            [nameof(ListView.GroupHeaderTemplate)] = MapItems,
            [nameof(ListView.RowHeight)] = MapRowHeight,
            [nameof(ListView.HasUnevenRows)] = MapRowHeight,
            [nameof(ListView.Header)] = MapHeader,
            [nameof(ListView.Footer)] = MapFooter,
            [nameof(ListView.SelectedItem)] = MapSelectedItem,
            [nameof(ListView.SelectionMode)] = MapSelectionMode,
            [nameof(ListView.SeparatorColor)] = MapSeparatorColor,
            [nameof(ListView.SeparatorVisibility)] = MapSeparatorVisibility,
            [nameof(ListView.VerticalScrollBarVisibility)] = MapVerticalScrollBarVisibility,
            [nameof(IView.Background)] = MapBackground,
            [nameof(ListView.BackgroundColor)] = MapBackgroundColor,
        };

    public static CommandMapper<ListView, ListViewHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
        };

    private readonly List<Row> _rows = new();
    private bool _isUpdatingSelection;
    private Microsoft.Maui.Controls.Internals.TemplatedItemsList<ItemsView<Cell>, Cell>? _observedItems;

    public ListViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public ListViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    /// <summary>The rows currently presented, in display order.</summary>
    public IReadOnlyList<Row> Rows => _rows;

    protected override SkiaCollectionView CreatePlatformView()
    {
        return new SkiaCollectionView
        {
            SelectionMode = SkiaSelectionMode.Single,
            ItemHeight = SkiaCellView.DefaultHeight,
            MinimumItemHeight = SkiaCellView.DefaultHeight,
        };
    }

    protected override void ConnectHandler(SkiaCollectionView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.ItemTapped += OnItemTapped;
        platformView.ItemViewCreator = CreateRowView;
        VirtualView.ScrollToRequested += OnScrollToRequested;
        ObserveTemplatedItems();
    }

    protected override void DisconnectHandler(SkiaCollectionView platformView)
    {
        platformView.ItemTapped -= OnItemTapped;
        platformView.ItemViewCreator = null;
        if (VirtualView != null)
            VirtualView.ScrollToRequested -= OnScrollToRequested;
        UnobserveTemplatedItems();
        platformView.ItemsSource = null;
        base.DisconnectHandler(platformView);
    }

    private void ObserveTemplatedItems()
    {
        var items = VirtualView?.TemplatedItems;
        if (ReferenceEquals(items, _observedItems)) return;
        UnobserveTemplatedItems();
        _observedItems = items;
        if (items != null)
        {
            items.CollectionChanged += OnTemplatedItemsChanged;
            items.GroupedCollectionChanged += OnTemplatedItemsChanged;
        }
    }

    private void UnobserveTemplatedItems()
    {
        if (_observedItems == null) return;
        _observedItems.CollectionChanged -= OnTemplatedItemsChanged;
        _observedItems.GroupedCollectionChanged -= OnTemplatedItemsChanged;
        _observedItems = null;
    }

    /// <summary>
    /// The items (or a group's items) changed: the rows follow in place, as Windows' ListView
    /// updates its rows. The rows of the items still there keep their cells and views, the list
    /// keeps the items in view where they are (WinUI's KeepItemsInView; a Reset keeps the
    /// scroll offset), and the selection stays on its item.
    /// </summary>
    private void OnTemplatedItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => UpdateRows(keepScrollOffset: e.Action == NotifyCollectionChangedAction.Reset);

    private bool RecyclesElements => VirtualView is { } list && (list.CachingStrategy & ListViewCachingStrategy.RecycleElement) != 0;

    /// <summary>
    /// What identifies a row across changes: its cell, which MAUI keeps for an item while it
    /// stays in the list (RetainElement); with RecycleElement MAUI makes a new item cell on every
    /// read, so the row is its item. A group header's cell is kept in both.
    /// </summary>
    private object RowKey(Row row) => RecyclesElements && !row.IsGroupHeader ? row.Item ?? row.Cell : row.Cell;

    /// <summary>
    /// Reads the rows from MAUI's TemplatedItems. Grouped: each top level entry is a group's
    /// header cell, followed by the cells of the group's templated list; flat: each entry is an
    /// item cell. A row still there (see <see cref="RowKey"/>) is reused, with its new place.
    /// </summary>
    private List<Row> ReadRows(bool reuse)
    {
        var rows = new List<Row>();
        if (VirtualView is null)
            return rows;
        var recycle = RecyclesElements;
        var previous = new Dictionary<object, Queue<Row>>(RowKeyComparer.Instance);
        if (reuse)
        {
            foreach (var row in _rows)
            {
                var key = RowKey(row);
                if (!previous.TryGetValue(key, out var queue))
                    previous[key] = queue = new Queue<Row>();
                queue.Enqueue(row);
            }
        }

        Row Take(object? key, Func<Row> create, int groupIndex, int index)
        {
            if (key != null && previous.TryGetValue(key, out var queue) && queue.Count > 0)
            {
                var row = queue.Dequeue();
                row.GroupIndex = groupIndex;
                row.Index = index;
                return row;
            }
            return create();
        }

        var templated = VirtualView.TemplatedItems;
        if (VirtualView.IsGroupingEnabled)
        {
            for (int g = 0; g < templated.Count; g++)
            {
                int groupIndex = g;
                // A group's header cell is kept while the group is there, in either caching strategy.
                var headerCell = templated[g];
                rows.Add(Take(headerCell, () => new Row(headerCell, groupIndex, -1, isGroupHeader: true), g, -1));
                // The group's items (its templated list; the header's BindingContext is the group's data when a GroupHeaderTemplate makes it).
                var items = ((ITemplatedItemsList<Cell>)templated).GetGroup(g);
                if (items != null)
                    AddItemRows(rows, items, g, recycle, Take);
            }
        }
        else
        {
            AddItemRows(rows, templated, -1, recycle, Take);
        }
        return rows;
    }

    /// <summary>The same cell or item object; an item that is a value (a number, a string) is the same when equal.</summary>
    private sealed class RowKeyComparer : IEqualityComparer<object>
    {
        public static readonly RowKeyComparer Instance = new();

        public new bool Equals(object? x, object? y) =>
            ReferenceEquals(x, y) || (x is ValueType or string && x.Equals(y));

        public int GetHashCode(object obj) =>
            obj is ValueType or string ? obj.GetHashCode() : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    private static void AddItemRows(List<Row> rows, ITemplatedItemsList<Cell> items, int groupIndex, bool recycle,
        Func<object?, Func<Row>, int, int, Row> take)
    {
        for (int i = 0; i < items.Count; i++)
        {
            int index = i;
            if (recycle)
            {
                // Only a new item gets a cell made for it.
                var item = items.ListProxy[i];
                rows.Add(take(item, () => new Row(items[index], groupIndex, index, isGroupHeader: false), groupIndex, i));
            }
            else
            {
                var cell = items[i];
                rows.Add(take(cell, () => new Row(cell, groupIndex, index, isGroupHeader: false), groupIndex, i));
            }
        }
    }

    /// <summary>Shows the ListView's items from the start (a new ItemsSource, template or grouping).</summary>
    private void RebuildRows()
    {
        if (PlatformView is null || VirtualView is null) return;

        ObserveTemplatedItems();
        var rows = ReadRows(reuse: false);
        _rows.Clear();
        _rows.AddRange(rows);

        PlatformView.ItemsSource = _rows.ToList();
        ApplySelection();
        PlatformView.InvalidateMeasure();
    }

    /// <summary>Follows a change of the items in place (see <see cref="OnTemplatedItemsChanged"/>).</summary>
    private void UpdateRows(bool keepScrollOffset)
    {
        if (PlatformView is null || VirtualView is null) return;

        ObserveTemplatedItems();
        var rows = ReadRows(reuse: true);
        _rows.Clear();
        _rows.AddRange(rows);

        // KeepItemsInView, as WinUI's list panel: the first item shown stays where it is. A
        // Reset brings new rows, so the list keeps its offset instead.
        var mode = PlatformView.ItemsUpdatingScrollMode;
        PlatformView.ItemsUpdatingScrollMode = keepScrollOffset
            ? ItemsUpdatingScrollMode.KeepScrollOffset
            : ItemsUpdatingScrollMode.KeepItemsInView;
        try
        {
            PlatformView.UpdateItemsSource(_rows.ToList());
        }
        finally
        {
            PlatformView.ItemsUpdatingScrollMode = mode;
        }
        ApplySelection();
    }

    private SkiaView? CreateRowView(object item)
    {
        if (item is not Row row) return null;

        var view = row.IsGroupHeader
            ? CellViewFactory.CreateGroupHeader(row.Cell, MauiContext)
            : CellViewFactory.Create(row.Cell, MauiContext);

        if (VirtualView != null && !VirtualView.HasUnevenRows && VirtualView.RowHeight > 0 && !row.IsGroupHeader)
            view.RowHeight = (float)VirtualView.RowHeight;

        return view;
    }

    private void OnItemTapped(object? sender, ItemsViewItemTappedEventArgs e)
    {
        if (VirtualView is null || e.Item is not Row row) return;

        if (row.IsGroupHeader)
        {
            // Headers are not selectable; restore the selection highlight.
            ApplySelection();
            return;
        }

        // SwitchCell rows toggle on tap (the platform list owns row taps).
        if (row.Cell is SwitchCell switchCell && switchCell.IsEnabled)
            switchCell.On = !switchCell.On;

        try
        {
            _isUpdatingSelection = true;
            if (VirtualView.IsGroupingEnabled)
                VirtualView.NotifyRowTapped(row.GroupIndex, row.Index, row.Cell);
            else
                VirtualView.NotifyRowTapped(row.Index, row.Cell);
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        ApplySelection();
    }

    private void OnScrollToRequested(object? sender, ScrollToRequestedEventArgs e)
    {
        if (PlatformView is null) return;

        // The item travels on the internal args type; read it reflectively so
        // ScrollTo(item, ...) lands on the right row without private references.
        var item = e.GetType().GetProperty("Item")?.GetValue(e);
        if (item == null) return;
        var row = _rows.FirstOrDefault(r => !r.IsGroupHeader && Equals(r.Item, item));
        if (row != null)
            PlatformView.ScrollToItem(row, e.ShouldAnimate);
    }

    /// <summary>Highlights the row whose item is the ListView's SelectedItem.</summary>
    private void ApplySelection()
    {
        if (PlatformView is null || VirtualView is null) return;

        var selected = VirtualView.SelectedItem;
        var row = selected == null ? null : _rows.FirstOrDefault(r => !r.IsGroupHeader && Equals(r.Item, selected));
        if (!ReferenceEquals(PlatformView.SelectedItem, row))
            PlatformView.SelectedItem = row;
    }

    public static void MapItems(ListViewHandler handler, ListView listView)
    {
        handler.RebuildRows();
    }

    public static void MapRowHeight(ListViewHandler handler, ListView listView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.ItemHeight = listView.RowHeight > 0 ? (float)listView.RowHeight : SkiaCellView.DefaultHeight;
        handler.PlatformView.MinimumItemHeight = handler.PlatformView.ItemHeight; // a ListView row keeps its row height
        handler.PlatformView.RefreshTheme(); // drops cached row views so heights re-measure
    }

    public static void MapHeader(ListViewHandler handler, ListView listView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Header = TextOf(listView.Header);
    }

    public static void MapFooter(ListViewHandler handler, ListView listView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Footer = TextOf(listView.Footer);
    }

    /// <summary>
    /// The platform list draws header/footer text; a Label header contributes
    /// its Text, anything else its string form.
    /// </summary>
    private static object? TextOf(object? value) => value switch
    {
        null => null,
        string s => s,
        Label label => label.Text,
        _ => value,
    };

    public static void MapSelectedItem(ListViewHandler handler, ListView listView)
    {
        if (handler._isUpdatingSelection) return;
        handler.ApplySelection();
    }

    public static void MapSelectionMode(ListViewHandler handler, ListView listView)
    {
        if (handler.PlatformView is null) return;
        // Selection state is driven by MAUI (NotifyRowTapped); the platform
        // only highlights. Keep Single so the highlight can be shown at all.
        handler.PlatformView.SelectionMode = SkiaSelectionMode.Single;
        handler.ApplySelection();
    }

    public static void MapSeparatorColor(ListViewHandler handler, ListView listView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.SeparatorColor = listView.SeparatorColor;
    }

    public static void MapSeparatorVisibility(ListViewHandler handler, ListView listView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.ShowSeparators = listView.SeparatorVisibility == SeparatorVisibility.Default;
    }

    public static void MapVerticalScrollBarVisibility(ListViewHandler handler, ListView listView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.VerticalScrollBarVisibility = listView.VerticalScrollBarVisibility;
    }

    public static void MapBackground(ListViewHandler handler, ListView listView)
    {
        if (handler.PlatformView is null) return;
        if (listView.BackgroundColor is not null) return;
        if (listView.Background is SolidColorBrush { Color: Color color })
            handler.PlatformView.BackgroundColor = color;
    }

    public static void MapBackgroundColor(ListViewHandler handler, ListView listView)
    {
        if (handler.PlatformView is null) return;
        if (listView.BackgroundColor is not null)
            handler.PlatformView.BackgroundColor = listView.BackgroundColor;
    }
}
