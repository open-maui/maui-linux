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
public partial class ListViewHandler : ViewHandler<ListView, SkiaCollectionView>
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
        public int GroupIndex { get; }
        public int Index { get; }
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

    private void OnTemplatedItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildRows();

    /// <summary>
    /// Rebuilds the row list from MAUI's TemplatedItems. Grouped: each top
    /// level entry is the group header cell whose BindingContext is the
    /// group's own templated list; flat: each entry is an item cell.
    /// </summary>
    private void RebuildRows()
    {
        if (PlatformView is null || VirtualView is null) return;

        ObserveTemplatedItems();
        _rows.Clear();

        var templated = VirtualView.TemplatedItems;
        if (VirtualView.IsGroupingEnabled)
        {
            for (int g = 0; g < templated.Count; g++)
            {
                var headerCell = templated[g];
                _rows.Add(new Row(headerCell, g, -1, isGroupHeader: true));
                if (headerCell.BindingContext is ITemplatedItemsList<Cell> group)
                {
                    for (int i = 0; i < group.Count; i++)
                        _rows.Add(new Row(group[i], g, i, isGroupHeader: false));
                }
            }
        }
        else
        {
            for (int i = 0; i < templated.Count; i++)
                _rows.Add(new Row(templated[i], -1, i, isGroupHeader: false));
        }

        PlatformView.ItemsSource = _rows.ToList();
        ApplySelection();
        PlatformView.InvalidateMeasure();
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
        // SkiaCollectionView draws theme separators; colour is not yet configurable.
    }

    public static void MapVerticalScrollBarVisibility(ListViewHandler handler, ListView listView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.VerticalScrollBarVisibility = (Platform.ScrollBarVisibility)listView.VerticalScrollBarVisibility;
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
