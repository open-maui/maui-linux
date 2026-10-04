// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Hosting;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for CarouselView on Linux using Skia rendering.
/// Maps CarouselView to SkiaCarouselView platform view.
/// </summary>
public partial class CarouselViewHandler : LinuxViewHandler<CarouselView, SkiaCarouselView>
{
    private bool _isUpdatingPosition;

    public static IPropertyMapper<CarouselView, CarouselViewHandler> Mapper =
        new PropertyMapper<CarouselView, CarouselViewHandler>(ViewHandler.ViewMapper)
        {
            // ItemsView properties
            [nameof(ItemsView.ItemsSource)] = MapItemsSource,
            [nameof(ItemsView.ItemTemplate)] = MapItemTemplate,
            [nameof(ItemsView.EmptyView)] = MapEmptyView,

            // CarouselView specific properties
            [nameof(CarouselView.Position)] = MapPosition,
            [nameof(CarouselView.CurrentItem)] = MapCurrentItem,
            [nameof(CarouselView.IsBounceEnabled)] = MapIsBounceEnabled,
            [nameof(CarouselView.IsSwipeEnabled)] = MapIsSwipeEnabled,
            [nameof(CarouselView.Loop)] = MapLoop,
            [nameof(CarouselView.PeekAreaInsets)] = MapPeekAreaInsets,

            [nameof(IView.Background)] = MapBackground,
        };

    public static CommandMapper<CarouselView, CarouselViewHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
            ["ScrollTo"] = MapScrollTo,
        };

    public CarouselViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public CarouselViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaCarouselView CreatePlatformView()
    {
        return new SkiaCarouselView();
    }

    protected override void ConnectHandler(SkiaCarouselView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.PositionChanged += OnPositionChanged;
        platformView.Scrolled += OnScrolled;
        ObserveScrollToRequests(VirtualView);
    }

    /// <summary>
    /// Releases what the handler attached, as MAUI's items handlers do: the items source's change
    /// notifications (a disconnected CarouselView no longer hooks its collection), the item views
    /// (out of the CarouselView's logical children, handlers disconnected), ScrollTo requests.
    /// </summary>
    protected override void DisconnectHandler(SkiaCarouselView platformView)
    {
        platformView.PositionChanged -= OnPositionChanged;
        platformView.Scrolled -= OnScrolled;
        ObserveScrollToRequests(null);
        _collectionSubscription?.Dispose();
        _collectionSubscription = null;
        platformView.ClearItems();
        ReleaseItemElements();
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    public override void SetVirtualView(IView view)
    {
        base.SetVirtualView(view);
        if (PlatformView != null)
            ObserveScrollToRequests(VirtualView);
    }

    // The items source's changes, observed weakly (as MAUI's ObservableItemsSource does): items
    // added or removed after the carousel is shown are shown.
    private WeakCollectionChangedProxy<CarouselViewHandler>? _collectionSubscription;

    // The item views' MAUI elements: logical children of the CarouselView while shown.
    private readonly List<Element> _itemElements = new();

    private void ReleaseItemElements()
    {
        foreach (var element in _itemElements)
        {
            if (element.Parent is ItemsView owner)
                owner.RemoveLogicalChild(element);
            try
            {
                if (element is IView view)
                    view.DisconnectHandlers();
            }
            catch (Exception ex)
            {
                Microsoft.Maui.Platform.Linux.Services.DiagnosticLog.Error("CarouselViewHandler", "Disconnecting an item failed", ex);
            }
        }
        _itemElements.Clear();
    }

    private void OnItemsSourceChanged()
    {
        if (VirtualView is not { } carouselView || PlatformView is null)
            return;
        var position = carouselView.Position;
        MapItemsSource(this, carouselView);
        var count = PlatformView.ItemCount;
        if (count > 0)
            PlatformView.ScrollTo(Math.Clamp(position, 0, count - 1), false);
    }

    // CarouselView.ScrollTo raises ScrollToRequested, which MAUI's handlers listen to.
    private CarouselView? _scrollRequestsView;

    private void ObserveScrollToRequests(CarouselView? view)
    {
        if (ReferenceEquals(_scrollRequestsView, view))
            return;
        if (_scrollRequestsView != null)
            _scrollRequestsView.ScrollToRequested -= OnScrollToRequested;
        _scrollRequestsView = view;
        if (view != null)
            view.ScrollToRequested += OnScrollToRequested;
    }

    private void OnScrollToRequested(object? sender, ScrollToRequestEventArgs e)
    {
        if (VirtualView is { } view)
            MapScrollTo(this, view, e);
    }

    private void OnPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        if (VirtualView is null || _isUpdatingPosition) return;

        try
        {
            _isUpdatingPosition = true;

            if (VirtualView.Position != e.CurrentPosition)
            {
                VirtualView.Position = e.CurrentPosition;
            }

            // Update CurrentItem
            if (VirtualView.ItemsSource is System.Collections.IList list &&
                e.CurrentPosition >= 0 && e.CurrentPosition < list.Count)
            {
                VirtualView.CurrentItem = list[e.CurrentPosition];
            }
        }
        finally
        {
            _isUpdatingPosition = false;
        }
    }

    private void OnScrolled(object? sender, EventArgs e)
    {
        // CarouselView doesn't have a direct Scrolled event in MAUI
        // but we can use this for internal state tracking
    }

    public static void MapItemsSource(CarouselViewHandler handler, CarouselView carouselView)
    {
        if (handler.PlatformView is null || handler.MauiContext is null) return;

        handler.PlatformView.ClearItems();
        handler.ReleaseItemElements();

        var itemsSource = carouselView.ItemsSource;
        if (!ReferenceEquals(handler._collectionSubscription?.Source, itemsSource))
        {
            handler._collectionSubscription?.Dispose();
            handler._collectionSubscription = itemsSource is System.Collections.Specialized.INotifyCollectionChanged observable
                ? new WeakCollectionChangedProxy<CarouselViewHandler>(observable, handler,
                    static (h, _, _) => h.OnItemsSourceChanged())
                : null;
        }
        if (itemsSource == null) return;

        var template = carouselView.ItemTemplate;

        foreach (var item in itemsSource)
        {
            SkiaView? skiaView = null;

            if (template != null)
            {
                try
                {
                    var content = ItemTemplateContent.Create(template, item, carouselView);
                    if (content is View view)
                    {
                        // The item first, then the parent (for RelativeSource AncestorType
                        // bindings): parented first, the page would bind to the carousel's context.
                        view.BindingContext = item;
                        if (view.Parent == null)
                        {
                            // A logical child of the CarouselView, as MAUI adds a realized item.
                            carouselView.AddLogicalChild(view);
                            handler._itemElements.Add(view);
                        }

                        if (view.Handler == null)
                        {
                            view.Handler = view.ToViewHandler(handler.MauiContext);
                        }

                        if (view.Handler?.PlatformView is SkiaView sv)
                        {
                            skiaView = sv;
                        }
                    }
                }
                catch
                {
                    // Ignore template errors
                }
            }

            if (skiaView == null)
            {
                // Create a simple label for the item
                skiaView = new SkiaLabel { Text = item?.ToString() ?? "" };
            }

            handler.PlatformView.AddItem(skiaView);
        }

        handler.PlatformView.Invalidate();
    }

    public static void MapItemTemplate(CarouselViewHandler handler, CarouselView carouselView)
    {
        // Re-map items when template changes
        MapItemsSource(handler, carouselView);
    }

    public static void MapEmptyView(CarouselViewHandler handler, CarouselView carouselView)
    {
        // CarouselView doesn't typically show empty view - handled by ItemsSource
    }

    public static void MapPosition(CarouselViewHandler handler, CarouselView carouselView)
    {
        if (handler.PlatformView is null || handler._isUpdatingPosition) return;

        try
        {
            handler._isUpdatingPosition = true;
            if (handler.PlatformView.Position != carouselView.Position)
            {
                handler.PlatformView.Position = carouselView.Position;
            }
        }
        finally
        {
            handler._isUpdatingPosition = false;
        }
    }

    public static void MapCurrentItem(CarouselViewHandler handler, CarouselView carouselView)
    {
        if (handler.PlatformView is null || handler._isUpdatingPosition) return;

        // Find position of current item
        if (carouselView.ItemsSource is System.Collections.IList list && carouselView.CurrentItem != null)
        {
            int index = list.IndexOf(carouselView.CurrentItem);
            if (index >= 0 && index != handler.PlatformView.Position)
            {
                try
                {
                    handler._isUpdatingPosition = true;
                    handler.PlatformView.Position = index;
                }
                finally
                {
                    handler._isUpdatingPosition = false;
                }
            }
        }
    }

    public static void MapIsBounceEnabled(CarouselViewHandler handler, CarouselView carouselView)
    {
        // SkiaCarouselView handles bounce internally
        // Could add IsBounceEnabled property if needed
    }

    public static void MapIsSwipeEnabled(CarouselViewHandler handler, CarouselView carouselView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsSwipeEnabled = carouselView.IsSwipeEnabled;
    }

    public static void MapLoop(CarouselViewHandler handler, CarouselView carouselView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Loop = carouselView.Loop;
    }

    public static void MapPeekAreaInsets(CarouselViewHandler handler, CarouselView carouselView)
    {
        if (handler.PlatformView is null) return;
        // PeekAreaInsets is a Thickness in MAUI, use Left for horizontal peek
        handler.PlatformView.PeekAreaInsets = (float)carouselView.PeekAreaInsets.Left;
    }

    public static void MapBackground(CarouselViewHandler handler, CarouselView carouselView)
    {
        if (handler.PlatformView is null) return;

        if (carouselView.Background is SolidColorBrush solidBrush)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color;
        }
    }

    public static void MapScrollTo(CarouselViewHandler handler, CarouselView carouselView, object? args)
    {
        if (handler.PlatformView is null) return;

        if (args is ScrollToRequestEventArgs scrollArgs)
        {
            var index = scrollArgs.Mode == ScrollToMode.Element && carouselView.ItemsSource is System.Collections.IList list
                ? list.IndexOf(scrollArgs.Item)
                : scrollArgs.Index;
            handler.PlatformView.ScrollTo(index, scrollArgs.IsAnimated);
        }
    }
}
