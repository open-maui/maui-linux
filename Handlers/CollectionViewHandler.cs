// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for CollectionView on Linux using Skia rendering.
/// Maps CollectionView to SkiaCollectionView platform view.
/// </summary>
public partial class CollectionViewHandler : ViewHandler<CollectionView, SkiaCollectionView>
{
    public static IPropertyMapper<CollectionView, CollectionViewHandler> Mapper =
        new PropertyMapper<CollectionView, CollectionViewHandler>(ViewHandler.ViewMapper)
        {
            // ItemsView properties
            [nameof(ItemsView.ItemsSource)] = MapItemsSource,
            [nameof(ItemsView.ItemTemplate)] = MapItemTemplate,
            [nameof(ItemsView.EmptyView)] = MapEmptyView,
            [nameof(ItemsView.HorizontalScrollBarVisibility)] = MapHorizontalScrollBarVisibility,
            [nameof(ItemsView.VerticalScrollBarVisibility)] = MapVerticalScrollBarVisibility,

            // SelectableItemsView properties
            [nameof(SelectableItemsView.SelectedItem)] = MapSelectedItem,
            [nameof(SelectableItemsView.SelectedItems)] = MapSelectedItems,
            [nameof(SelectableItemsView.SelectionMode)] = MapSelectionMode,

            // StructuredItemsView properties
            [nameof(StructuredItemsView.Header)] = MapHeader,
            [nameof(StructuredItemsView.Footer)] = MapFooter,
            [nameof(StructuredItemsView.ItemsLayout)] = MapItemsLayout,

            [nameof(IView.Background)] = MapBackground,
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
    }

    protected override void DisconnectHandler(SkiaCollectionView platformView)
    {
        platformView.SelectionChanged -= OnSelectionChanged;
        platformView.Scrolled -= OnScrolled;
        platformView.ItemTapped -= OnItemTapped;
        base.DisconnectHandler(platformView);
    }

    private void OnSelectionChanged(object? sender, CollectionSelectionChangedEventArgs e)
    {
        if (VirtualView is null) return;

        // Update virtual view selection
        if (VirtualView.SelectionMode == SelectionMode.Single)
        {
            VirtualView.SelectedItem = e.CurrentSelection.FirstOrDefault();
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

    private void OnScrolled(object? sender, ItemsScrolledEventArgs e)
    {
        VirtualView?.SendScrolled(new ItemsViewScrolledEventArgs
        {
            VerticalOffset = e.ScrollOffset,
            VerticalDelta = 0,
            HorizontalOffset = 0,
            HorizontalDelta = 0
        });
    }

    private void OnItemTapped(object? sender, ItemsViewItemTappedEventArgs e)
    {
        // Item tap is handled through selection
    }

    public static void MapItemsSource(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.ItemsSource = collectionView.ItemsSource;
    }

    public static void MapItemTemplate(CollectionViewHandler handler, CollectionView collectionView)
    {
        handler.PlatformView?.Invalidate();
    }

    public static void MapEmptyView(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.EmptyView = collectionView.EmptyView;
        if (collectionView.EmptyView is string text)
        {
            handler.PlatformView.EmptyViewText = text;
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
        if (handler.PlatformView is null) return;
        handler.PlatformView.SelectedItem = collectionView.SelectedItem;
    }

    public static void MapSelectedItems(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null) return;

        // Sync selected items
        var selectedItems = collectionView.SelectedItems;
        if (selectedItems != null && selectedItems.Count > 0)
        {
            handler.PlatformView.SelectedItem = selectedItems.First();
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
    }

    public static void MapFooter(CollectionViewHandler handler, CollectionView collectionView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Footer = collectionView.Footer;
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

        if (collectionView.Background is SolidColorBrush solidBrush)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color.ToSKColor();
        }
    }

    public static void MapScrollTo(CollectionViewHandler handler, CollectionView collectionView, object? args)
    {
        if (handler.PlatformView is null || args is not ScrollToRequestEventArgs scrollArgs)
            return;

        if (scrollArgs.Mode == ScrollToMode.Position)
        {
            handler.PlatformView.ScrollToIndex(scrollArgs.Index, scrollArgs.IsAnimated);
        }
        else if (scrollArgs.Item != null)
        {
            handler.PlatformView.ScrollToItem(scrollArgs.Item, scrollArgs.IsAnimated);
        }
    }
}
