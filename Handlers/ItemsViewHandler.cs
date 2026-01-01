// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Base handler for ItemsView on Linux using Skia rendering.
/// Maps ItemsView to SkiaItemsView platform view.
/// </summary>
public partial class ItemsViewHandler<TItemsView> : ViewHandler<TItemsView, SkiaItemsView>
    where TItemsView : ItemsView
{
    public static IPropertyMapper<TItemsView, ItemsViewHandler<TItemsView>> ItemsViewMapper =
        new PropertyMapper<TItemsView, ItemsViewHandler<TItemsView>>(ViewHandler.ViewMapper)
        {
            [nameof(ItemsView.ItemsSource)] = MapItemsSource,
            [nameof(ItemsView.ItemTemplate)] = MapItemTemplate,
            [nameof(ItemsView.EmptyView)] = MapEmptyView,
            [nameof(ItemsView.EmptyViewTemplate)] = MapEmptyViewTemplate,
            [nameof(ItemsView.HorizontalScrollBarVisibility)] = MapHorizontalScrollBarVisibility,
            [nameof(ItemsView.VerticalScrollBarVisibility)] = MapVerticalScrollBarVisibility,
            [nameof(IView.Background)] = MapBackground,
        };

    public static CommandMapper<TItemsView, ItemsViewHandler<TItemsView>> ItemsViewCommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
            ["ScrollTo"] = MapScrollTo,
        };

    public ItemsViewHandler() : base(ItemsViewMapper, ItemsViewCommandMapper)
    {
    }

    public ItemsViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? ItemsViewMapper, commandMapper ?? ItemsViewCommandMapper)
    {
    }

    protected override SkiaItemsView CreatePlatformView()
    {
        return new SkiaItemsView();
    }

    protected override void ConnectHandler(SkiaItemsView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Scrolled += OnScrolled;
        platformView.ItemTapped += OnItemTapped;

        // Set up item renderer
        platformView.ItemRenderer = RenderItem;
    }

    protected override void DisconnectHandler(SkiaItemsView platformView)
    {
        platformView.Scrolled -= OnScrolled;
        platformView.ItemTapped -= OnItemTapped;
        platformView.ItemRenderer = null;
        base.DisconnectHandler(platformView);
    }

    private void OnScrolled(object? sender, ItemsScrolledEventArgs e)
    {
        // Fire scrolled event on virtual view
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
        // Item tap handling - can be extended for selection
    }

    protected virtual bool RenderItem(object item, int index, SKRect bounds, SKCanvas canvas, SKPaint paint)
    {
        // Check if we have an ItemTemplate
        var template = VirtualView?.ItemTemplate;
        if (template == null)
            return false; // Use default rendering

        // For now, render based on item ToString
        // Full DataTemplate support would require creating actual views
        return false;
    }

    public static void MapItemsSource(ItemsViewHandler<TItemsView> handler, TItemsView itemsView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.ItemsSource = itemsView.ItemsSource;
    }

    public static void MapItemTemplate(ItemsViewHandler<TItemsView> handler, TItemsView itemsView)
    {
        // ItemTemplate affects how items are rendered
        // The renderer will check this when drawing items
        handler.PlatformView?.Invalidate();
    }

    public static void MapEmptyView(ItemsViewHandler<TItemsView> handler, TItemsView itemsView)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.EmptyView = itemsView.EmptyView;
        if (itemsView.EmptyView is string text)
        {
            handler.PlatformView.EmptyViewText = text;
        }
    }

    public static void MapEmptyViewTemplate(ItemsViewHandler<TItemsView> handler, TItemsView itemsView)
    {
        // EmptyViewTemplate would be used to render custom empty view
        handler.PlatformView?.Invalidate();
    }

    public static void MapHorizontalScrollBarVisibility(ItemsViewHandler<TItemsView> handler, TItemsView itemsView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.HorizontalScrollBarVisibility = (ScrollBarVisibility)itemsView.HorizontalScrollBarVisibility;
    }

    public static void MapVerticalScrollBarVisibility(ItemsViewHandler<TItemsView> handler, TItemsView itemsView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.VerticalScrollBarVisibility = (ScrollBarVisibility)itemsView.VerticalScrollBarVisibility;
    }

    public static void MapBackground(ItemsViewHandler<TItemsView> handler, TItemsView itemsView)
    {
        if (handler.PlatformView is null) return;

        if (itemsView.Background is SolidColorBrush solidBrush)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color.ToSKColor();
        }
    }

    public static void MapScrollTo(ItemsViewHandler<TItemsView> handler, TItemsView itemsView, object? args)
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
