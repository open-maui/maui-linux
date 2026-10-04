// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for CollectionView on Linux using Skia rendering.
/// Maps CollectionView to SkiaCollectionView platform view.
/// </summary>
public partial class CollectionViewHandler : LinuxViewHandler<CollectionView, SkiaCollectionView>
{
    private bool _isUpdatingSelection;

    public static IPropertyMapper<CollectionView, CollectionViewHandler> Mapper =
        new PropertyMapper<CollectionView, CollectionViewHandler>(ViewHandler.ViewMapper)
        {
            // ItemsView properties
            [nameof(ItemsView.ItemsSource)] = MapItemsSource,
            [nameof(ItemsView.ItemTemplate)] = MapItemTemplate,
            [nameof(ItemsView.EmptyView)] = MapEmptyView,
            [nameof(ItemsView.EmptyViewTemplate)] = MapEmptyView,
            [nameof(ItemsView.HorizontalScrollBarVisibility)] = MapHorizontalScrollBarVisibility,
            [nameof(ItemsView.VerticalScrollBarVisibility)] = MapVerticalScrollBarVisibility,

            // SelectableItemsView properties
            [nameof(SelectableItemsView.SelectedItem)] = MapSelectedItem,
            [nameof(SelectableItemsView.SelectedItems)] = MapSelectedItems,
            [nameof(SelectableItemsView.SelectionMode)] = MapSelectionMode,

            // StructuredItemsView properties
            [nameof(StructuredItemsView.Header)] = MapHeader,
            [nameof(StructuredItemsView.HeaderTemplate)] = MapHeader,
            [nameof(StructuredItemsView.Footer)] = MapFooter,
            [nameof(StructuredItemsView.FooterTemplate)] = MapFooter,
            [nameof(StructuredItemsView.ItemsLayout)] = MapItemsLayout,

            // GroupableItemsView properties
            [nameof(GroupableItemsView.IsGrouped)] = MapIsGrouped,
            [nameof(GroupableItemsView.GroupHeaderTemplate)] = MapIsGrouped,
            [nameof(GroupableItemsView.GroupFooterTemplate)] = MapIsGrouped,

            [nameof(IView.Background)] = MapBackground,
            [nameof(CollectionView.BackgroundColor)] = MapBackgroundColor,
        };

    public static CommandMapper<CollectionView, CollectionViewHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
            ["ScrollTo"] = MapScrollTo,
        };

    public CollectionViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public CollectionViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaCollectionView CreatePlatformView()
    {
        return new SkiaCollectionView();
    }

    protected override void ConnectHandler(SkiaCollectionView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.SelectionChanged += OnSelectionChanged;
        platformView.Scrolled += OnScrolled;
        platformView.ItemTapped += OnItemTapped;
        platformView.ItemViewReleased += OnItemViewReleased;
        ObserveScrollToRequests(VirtualView);
    }

    // MAUI's ItemsView.ScrollTo raises ScrollToRequested; its handlers listen to it (there is no
    // ScrollTo command on any platform), so ScrollTo did nothing here.
    private CollectionView? _scrollRequestsView;

    private void ObserveScrollToRequests(CollectionView? view)
    {
        if (ReferenceEquals(_scrollRequestsView, view))
            return;
        if (_scrollRequestsView != null)
            _scrollRequestsView.ScrollToRequested -= OnScrollToRequested;
        _scrollRequestsView = view;
        if (view != null)
            view.ScrollToRequested += OnScrollToRequested;
    }

    /// <inheritdoc />
    public override void SetVirtualView(IView view)
    {
        base.SetVirtualView(view);
        // A handler given another CollectionView listens to that one's ScrollTo requests.
        if (PlatformView != null)
            ObserveScrollToRequests(VirtualView);
    }

    private void OnScrollToRequested(object? sender, ScrollToRequestEventArgs e)
    {
        if (VirtualView is { } view)
            MapScrollTo(this, view, e);
    }

    /// <summary>
    /// Releases what the handler attached, as MAUI's items handlers do: the items source is let
    /// go (its change notifications no longer reach the list), and the item views leave the
    /// CollectionView's logical children with their handlers disconnected. The header and
    /// footer stay the CollectionView's (they are its properties).
    /// </summary>
    protected override void DisconnectHandler(SkiaCollectionView platformView)
    {
        platformView.SelectionChanged -= OnSelectionChanged;
        platformView.Scrolled -= OnScrolled;
        platformView.ItemTapped -= OnItemTapped;
        ObserveScrollToRequests(null);
        UnobserveGroups();
        platformView.ItemsSource = null;
        platformView.ItemViewCreator = null;
        platformView.ItemViewReleased -= OnItemViewReleased;
        ReleaseAllItemElements();
        base.DisconnectHandler(platformView);
    }

    // The MAUI element behind each item view the list shows: a logical child of the
    // CollectionView while its row exists (MAUI adds a realized item and removes a recycled one).
    private readonly Dictionary<SkiaView, Element> _itemElements = new();

    private void OnItemViewReleased(object? sender, SkiaView view)
    {
        if (_itemElements.Remove(view, out var element))
            ReleaseItemElement(element);
    }

    private void ReleaseAllItemElements()
    {
        if (_itemElements.Count == 0)
            return;
        var elements = _itemElements.Values.ToList();
        _itemElements.Clear();
        foreach (var element in elements)
            ReleaseItemElement(element);
    }

    private void ReleaseItemElement(Element element)
    {
        if (element.Parent is ItemsView owner)
            owner.RemoveLogicalChild(element);
        try
        {
            if (element is IView view)
                view.DisconnectHandlers();
            else if (element is ViewCell { View: { } cellView })
                cellView.DisconnectHandlers();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("CollectionViewHandler", "Disconnecting a recycled item failed", ex);
        }
    }

    private void OnSelectionChanged(object? sender, CollectionSelectionChangedEventArgs e)
    {
        if (VirtualView is null || _isUpdatingSelection) return;

        try
        {
            _isUpdatingSelection = true;

            // Update virtual view selection
            if (VirtualView.SelectionMode == SelectionMode.Single)
            {
                var newItem = e.CurrentSelection.FirstOrDefault();
                if (!Equals(VirtualView.SelectedItem, newItem))
                {
                    VirtualView.SelectedItem = newItem;
                }
            }
            else if (VirtualView.SelectionMode == SelectionMode.Multiple)
            {
                // Clear and update selected items
                VirtualView.SelectedItems.Clear();
                foreach (var item in e.CurrentSelection)
                {
                    VirtualView.SelectedItems.Add(item);
                }
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private float _lastScrollOffset;

    private void OnScrolled(object? sender, ItemsScrolledEventArgs e)
    {
        var delta = e.ScrollOffset - _lastScrollOffset;
        _lastScrollOffset = e.ScrollOffset;
        var horizontal = PlatformView?.Orientation == Platform.ItemsLayoutOrientation.Horizontal && PlatformView.SpanCount == 1;
        VirtualView?.SendScrolled(new ItemsViewScrolledEventArgs
        {
            VerticalOffset = horizontal ? 0 : e.ScrollOffset,
            VerticalDelta = horizontal ? 0 : delta,
            HorizontalOffset = horizontal ? e.ScrollOffset : 0,
            HorizontalDelta = horizontal ? delta : 0
        });
    }

    private void OnItemTapped(object? sender, ItemsViewItemTappedEventArgs e)
    {
        if (VirtualView is null || _isUpdatingSelection) return;
        // A group header or footer is not an item: it is not selected.
        if (e.Item is GroupRow) return;

        try
        {
            _isUpdatingSelection = true;

            DiagnosticLog.Debug("CollectionViewHandler", $"OnItemTapped index={e.Index}, item={e.Item}, SelectionMode={VirtualView.SelectionMode}");

            // Try to get the item view and process gestures
            var skiaView = PlatformView?.GetItemView(e.Index);
            DiagnosticLog.Debug("CollectionViewHandler", $"GetItemView({e.Index}) returned: {skiaView?.GetType().Name ?? "null"}, MauiView={skiaView?.MauiView?.GetType().Name ?? "null"}");

            if (skiaView?.MauiView != null)
            {
                DiagnosticLog.Debug("CollectionViewHandler", $"Found MauiView: {skiaView.MauiView.GetType().Name}, GestureRecognizers={skiaView.MauiView.GestureRecognizers?.Count ?? 0}");
                if (GestureManager.ProcessTap(skiaView.MauiView, 0, 0))
                {
                    DiagnosticLog.Debug("CollectionViewHandler", "Gesture processed successfully");
                    return;
                }
            }

            // Handle selection if gesture wasn't processed
            if (VirtualView.SelectionMode == SelectionMode.Single)
            {
                VirtualView.SelectedItem = e.Item;
            }
            else if (VirtualView.SelectionMode == SelectionMode.Multiple)
            {
                if (VirtualView.SelectedItems.Contains(e.Item))
                {
                    VirtualView.SelectedItems.Remove(e.Item);
                }
                else
                {
                    VirtualView.SelectedItems.Add(e.Item);
                }
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    public static void MapItemsSource(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null) return;
        if (collectionView.IsGrouped)
        {
            handler.RebuildGroupedRows();
            return;
        }
        handler.UnobserveGroups();
        handler.PlatformView.ItemsSource = collectionView.ItemsSource;
    }

    public static void MapIsGrouped(CollectionViewHandler handler, CollectionView collectionView)
    {
        MapItemsSource(handler, collectionView);
    }

    public static void MapItemTemplate(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null || handler.MauiContext is null) return;

        // The creator also makes group headers and footers, which have templates of their own.
        handler.PlatformView.ItemViewCreator = collectionView.ItemTemplate != null || collectionView.IsGrouped
            ? handler.CreateItemView
            : null;

        handler.PlatformView.Invalidate();
    }

    /// <summary>
    /// Creates the view of a row: an item from the ItemTemplate, or a group's header or footer
    /// from GroupHeaderTemplate / GroupFooterTemplate. The view is added to the CollectionView's
    /// logical children (MAUI does this as an item is realized) and leaves them when the list
    /// releases its row.
    /// </summary>
    private SkiaView? CreateItemView(object item)
    {
        var collectionView = VirtualView;
        if (collectionView is null || MauiContext is null)
            return null;

        var (template, bindingContext) = item switch
        {
            GroupHeaderRow header => (collectionView.GroupHeaderTemplate, header.Group),
            GroupFooterRow footer => (collectionView.GroupFooterTemplate, footer.Group),
            _ => (collectionView.ItemTemplate, item),
        };
        if (template is null)
            return null;

        try
        {
            // Create view from template
            var content = ItemTemplateContent.Create(template, bindingContext, collectionView);
            if (content is View view)
            {
                // The row's root takes the item; its children inherit it, as on every
                // platform, so a child given a context of its own (a control's
                // Root.BindingContext = this) keeps it. The item comes first: a row
                // parented first inherits the list's context for a moment, and its
                // compiled bindings run against the wrong type (MAUI's "x:DataType
                // mismatch" binding failure, once per row).
                view.BindingContext = bindingContext;

                // The CollectionView is the parent (a logical child, as MAUI adds a realized
                // item), so RelativeSource AncestorType bindings can walk up to the Page.
                if (view.Parent == null)
                    collectionView.AddLogicalChild(view);

                // Create handler for the view
                if (view.Handler == null)
                {
                    view.Handler = view.ToViewHandler(MauiContext);
                }

                if (view.Handler?.PlatformView is SkiaView skiaView)
                {
                    // Set MauiView so gestures can be processed
                    skiaView.MauiView = view;
                    if (ReferenceEquals(view.Parent, collectionView))
                        _itemElements[skiaView] = view;
                    return skiaView;
                }

                // No row for it: it does not stay a logical child either.
                if (ReferenceEquals(view.Parent, collectionView))
                    collectionView.RemoveLogicalChild(view);
            }
            else if (content is ViewCell cell)
            {
                cell.BindingContext = bindingContext;
                var cellView = cell.View;
                if (cellView != null)
                {
                    if (cell.Parent == null)
                        collectionView.AddLogicalChild(cell);

                    if (cellView.Handler == null)
                    {
                        cellView.Handler = cellView.ToViewHandler(MauiContext);
                    }

                    if (cellView.Handler?.PlatformView is SkiaView skiaView)
                    {
                        if (ReferenceEquals(cell.Parent, collectionView))
                            _itemElements[skiaView] = cell;
                        return skiaView;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("CollectionViewHandler", $"Creating the row for {item?.GetType().Name} failed", ex);
        }
        return null;
    }

    /// <summary>A row of a grouped list that is not an item: it is drawn, never selected.</summary>
    private abstract class GroupRow : INonSelectableItem
    {
        protected GroupRow(object group, int groupIndex)
        {
            Group = group;
            GroupIndex = groupIndex;
        }

        public object Group { get; }
        public int GroupIndex { get; }
        public override string ToString() => Group.ToString() ?? string.Empty;
    }

    private sealed class GroupHeaderRow : GroupRow
    {
        public GroupHeaderRow(object group, int groupIndex) : base(group, groupIndex) { }
    }

    private sealed class GroupFooterRow : GroupRow
    {
        public GroupFooterRow(object group, int groupIndex) : base(group, groupIndex) { }
    }

    // A grouped list as the platform shows it: per group, its header (when there is a
    // GroupHeaderTemplate), its items, its footer (GroupFooterTemplate). _groupItemStarts[g]
    // is the row of group g's first item.
    private readonly List<object> _groups = new();
    private readonly List<int> _groupItemStarts = new();
    private readonly List<IDisposable> _groupSubscriptions = new();

    /// <summary>
    /// Flattens a grouped ItemsSource (an enumerable of groups, each an enumerable of items) into
    /// rows, as MAUI's grouped items sources do, and watches the groups and the list of groups
    /// for changes.
    /// </summary>
    private void RebuildGroupedRows()
    {
        if (PlatformView is null || VirtualView is null)
            return;

        UnobserveGroups();
        _groups.Clear();
        _groupItemStarts.Clear();
        var rows = new List<object>();
        var collectionView = VirtualView;
        if (collectionView.ItemsSource is System.Collections.IEnumerable groups)
        {
            if (groups is System.Collections.Specialized.INotifyCollectionChanged outer)
                _groupSubscriptions.Add(new WeakCollectionChangedProxy<CollectionViewHandler>(outer, this,
                    static (handler, _, _) => handler.RebuildGroupedRows()));

            int g = 0;
            foreach (var group in groups)
            {
                if (group is null)
                    continue;
                _groups.Add(group);
                if (collectionView.GroupHeaderTemplate != null)
                    rows.Add(new GroupHeaderRow(group, g));
                _groupItemStarts.Add(rows.Count);
                if (group is System.Collections.IEnumerable items and not string)
                {
                    foreach (var item in items)
                        rows.Add(item!);
                    if (group is System.Collections.Specialized.INotifyCollectionChanged inner)
                        _groupSubscriptions.Add(new WeakCollectionChangedProxy<CollectionViewHandler>(inner, this,
                            static (handler, _, _) => handler.RebuildGroupedRows()));
                }
                if (collectionView.GroupFooterTemplate != null)
                    rows.Add(new GroupFooterRow(group, g));
                g++;
            }
        }

        // The rows (and their views) are made again: the creator knows the group templates.
        MapItemTemplate(this, collectionView);
        PlatformView.ItemsSource = rows;
    }

    private void UnobserveGroups()
    {
        foreach (var subscription in _groupSubscriptions)
            subscription.Dispose();
        _groupSubscriptions.Clear();
    }

    /// <summary>The platform row of item <paramref name="index"/> of group <paramref name="groupIndex"/>; -1 when there is none.</summary>
    private int GroupedRowIndex(int groupIndex, int index)
    {
        if (groupIndex < 0 || groupIndex >= _groupItemStarts.Count || index < 0)
            return -1;
        var row = _groupItemStarts[groupIndex] + index;
        var end = groupIndex + 1 < _groupItemStarts.Count ? _groupItemStarts[groupIndex + 1] : int.MaxValue;
        return row < end ? row : -1;
    }

    public static void MapEmptyView(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.EmptyView = collectionView.EmptyView;
        handler.PlatformView.EmptyViewText = collectionView.EmptyView as string;
        handler.PlatformView.EmptyViewContent = EmptyViewContent(handler, collectionView);
    }

    /// <summary>
    /// The empty view as MAUI builds it: a View as it is, or the EmptyViewTemplate's content
    /// bound to the EmptyView object (a string with no template stays text). It is the list's
    /// child, so it inherits the list's BindingContext unless it sets its own.
    /// </summary>
    private static SkiaView? EmptyViewContent(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.MauiContext is null)
            return null;
        try
        {
            View? view = collectionView.EmptyView as View;
            if (view == null && collectionView.EmptyViewTemplate is { } template && collectionView.EmptyView is { } data)
            {
                view = ItemTemplateContent.Create(template, data, collectionView) as View;
                if (view != null)
                    view.BindingContext = data;
            }
            if (view == null)
                return null;
            if (view.Parent == null)
                view.Parent = collectionView;
            view.Handler ??= view.ToViewHandler(handler.MauiContext);
            return view.Handler?.PlatformView as SkiaView;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("CollectionViewHandler", "Creating the empty view failed", ex);
            return null;
        }
    }

    public static void MapHorizontalScrollBarVisibility(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.HorizontalScrollBarVisibility = (ScrollBarVisibility)collectionView.HorizontalScrollBarVisibility;
    }

    public static void MapVerticalScrollBarVisibility(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.VerticalScrollBarVisibility = (ScrollBarVisibility)collectionView.VerticalScrollBarVisibility;
    }

    public static void MapSelectedItem(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null || handler._isUpdatingSelection) return;

        try
        {
            handler._isUpdatingSelection = true;
            if (!Equals(handler.PlatformView.SelectedItem, collectionView.SelectedItem))
            {
                handler.PlatformView.SelectedItem = collectionView.SelectedItem;
            }
        }
        finally
        {
            handler._isUpdatingSelection = false;
        }
    }

    public static void MapSelectedItems(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null || handler._isUpdatingSelection) return;

        try
        {
            handler._isUpdatingSelection = true;

            // Sync selected items
            var selectedItems = collectionView.SelectedItems;
            if (selectedItems != null && selectedItems.Count > 0)
            {
                handler.PlatformView.SelectedItem = selectedItems.First();
            }
        }
        finally
        {
            handler._isUpdatingSelection = false;
        }
    }

    public static void MapSelectionMode(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.SelectionMode = collectionView.SelectionMode switch
        {
            SelectionMode.None => SkiaSelectionMode.None,
            SelectionMode.Single => SkiaSelectionMode.Single,
            SelectionMode.Multiple => SkiaSelectionMode.Multiple,
            _ => SkiaSelectionMode.None
        };
    }

    public static void MapHeader(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Header = collectionView.Header;
        handler.PlatformView.HeaderView = SlotView(handler, collectionView, collectionView.Header, collectionView.HeaderTemplate, ref handler._headerElement)
            ?? handler.PlatformView.HeaderView;
    }

    public static void MapFooter(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Footer = collectionView.Footer;
        handler.PlatformView.FooterView = SlotView(handler, collectionView, collectionView.Footer, collectionView.FooterTemplate, ref handler._footerElement)
            ?? handler.PlatformView.FooterView;
    }

    // The header and footer views the handler made logical children of the CollectionView
    // (MAUI's handlers add a View header or footer, or a template's content, as one).
    private View? _headerElement;
    private View? _footerElement;

    /// <summary>
    /// A header or footer as MAUI builds it: a View as it is, or the template's content bound to
    /// the header object. Null for a string or other object without a template (the platform view
    /// shows its text). The view is the list's child, so it inherits the list's BindingContext
    /// unless it sets its own.
    /// </summary>
    private static SkiaView? SlotView(CollectionViewHandler handler, CollectionView collectionView, object? value, DataTemplate? template, ref View? current)
    {
        // The previous header (or footer) leaves the logical children when it is replaced.
        if (current != null && !ReferenceEquals(current, value))
        {
            if (ReferenceEquals(current.Parent, collectionView))
                collectionView.RemoveLogicalChild(current);
            current = null;
        }
        if (handler.MauiContext is null || value is null)
            return null;
        try
        {
            View? view = value as View;
            if (view == null && template != null)
            {
                view = ItemTemplateContent.Create(template, value, collectionView) as View;
                if (view != null)
                    view.BindingContext = value;
            }
            if (view == null)
                return null;
            if (view.Parent == null)
            {
                collectionView.AddLogicalChild(view);
                current = view;
            }
            view.Handler ??= view.ToViewHandler(handler.MauiContext);
            return view.Handler?.PlatformView as SkiaView;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("CollectionViewHandler", "Creating the header or footer failed", ex);
            return null;
        }
    }

    public static void MapItemsLayout(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null) return;

        var layout = collectionView.ItemsLayout;
        if (layout is LinearItemsLayout linearLayout)
        {
            handler.PlatformView.Orientation = linearLayout.Orientation == Controls.ItemsLayoutOrientation.Vertical
                ? Platform.ItemsLayoutOrientation.Vertical
                : Platform.ItemsLayoutOrientation.Horizontal;
            handler.PlatformView.SpanCount = 1;
            handler.PlatformView.ItemSpacing = (float)linearLayout.ItemSpacing;
        }
        else if (layout is GridItemsLayout gridLayout)
        {
            handler.PlatformView.Orientation = gridLayout.Orientation == Controls.ItemsLayoutOrientation.Vertical
                ? Platform.ItemsLayoutOrientation.Vertical
                : Platform.ItemsLayoutOrientation.Horizontal;
            handler.PlatformView.SpanCount = gridLayout.Span;
            handler.PlatformView.ItemSpacing = (float)gridLayout.VerticalItemSpacing;
        }
    }

    public static void MapBackground(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null) return;

        // Don't override if BackgroundColor is explicitly set
        if (collectionView.BackgroundColor is not null)
            return;

        if (collectionView.Background is SolidColorBrush solidBrush)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color;
        }
    }

    public static void MapBackgroundColor(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null) return;

        if (collectionView.BackgroundColor is not null)
        {
            handler.PlatformView.BackgroundColor = collectionView.BackgroundColor;
        }
    }

    /// <summary>
    /// ScrollTo by index (with a group index in a grouped list) or by item (in a group when one is
    /// given), placing the item where <see cref="ScrollToRequestEventArgs.ScrollToPosition"/> asks.
    /// </summary>
    public static void MapScrollTo(CollectionViewHandler handler, CollectionView collectionView, object? args)
    {
        if (handler.PlatformView is null || args is not ScrollToRequestEventArgs scrollArgs)
            return;

        var position = (Platform.ScrollToPosition)(int)scrollArgs.ScrollToPosition;
        var grouped = collectionView.IsGrouped;
        int row = -1;
        if (scrollArgs.Mode == ScrollToMode.Position)
        {
            row = grouped ? handler.GroupedRowIndex(Math.Max(0, scrollArgs.GroupIndex), scrollArgs.Index) : scrollArgs.Index;
        }
        else if (scrollArgs.Item != null)
        {
            if (!grouped)
            {
                handler.PlatformView.ScrollToItem(scrollArgs.Item, position, scrollArgs.IsAnimated);
                return;
            }
            for (int g = 0; g < handler._groups.Count && row < 0; g++)
            {
                if (scrollArgs.Group != null && !Equals(handler._groups[g], scrollArgs.Group))
                    continue;
                if (handler._groups[g] is System.Collections.IEnumerable items)
                {
                    int i = 0;
                    foreach (var item in items)
                    {
                        if (Equals(item, scrollArgs.Item))
                        {
                            row = handler.GroupedRowIndex(g, i);
                            break;
                        }
                        i++;
                    }
                }
            }
        }

        if (row >= 0)
            handler.PlatformView.ScrollToIndex(row, position, scrollArgs.IsAnimated);
    }
}
