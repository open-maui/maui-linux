// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Specialized;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
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
            [nameof(ItemsView.EmptyViewTemplate)] = MapEmptyView,
            [nameof(ItemsView.HorizontalScrollBarVisibility)] = MapHorizontalScrollBarVisibility,
            [nameof(ItemsView.VerticalScrollBarVisibility)] = MapVerticalScrollBarVisibility,
            [nameof(ItemsView.ItemsUpdatingScrollMode)] = MapItemsUpdatingScrollMode,
            [nameof(VisualElement.IsVisible)] = MapIsVisible,

            // CarouselView specific properties
            [nameof(CarouselView.Position)] = MapPosition,
            [nameof(CarouselView.CurrentItem)] = MapCurrentItem,
            [nameof(CarouselView.IsBounceEnabled)] = MapIsBounceEnabled,
            [nameof(CarouselView.IsSwipeEnabled)] = MapIsSwipeEnabled,
            [nameof(CarouselView.Loop)] = MapLoop,
            [nameof(CarouselView.PeekAreaInsets)] = MapPeekAreaInsets,
            [nameof(CarouselView.ItemsLayout)] = MapItemsLayout,

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
        // A MAUI CarouselView draws no position dots (that is an IndicatorView's job), so its
        // items take its whole height.
        return new SkiaCarouselView { ShowIndicators = false };
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

    // The items shown and their views' MAUI elements (logical children of the CarouselView while
    // shown), in the platform view's order.
    private readonly List<object?> _items = new();
    private readonly List<Element?> _itemElements = new();

    private void ReleaseItemElements()
    {
        foreach (var element in _itemElements)
            ReleaseItemElement(element);
        _itemElements.Clear();
        _items.Clear();
    }

    private static void ReleaseItemElement(Element? element)
    {
        if (element == null)
            return;
        if (element.Parent is ItemsView owner)
            owner.RemoveLogicalChild(element);
        try
        {
            if (element is IView view)
                view.DisconnectHandlers();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("CarouselViewHandler", "Disconnecting an item failed", ex);
        }
    }

    /// <summary>
    /// The items source changed. The item views follow it in place (only the items added or
    /// removed are made or released), and the position moves as MAUI's CarouselView moves it on
    /// every platform after a change: KeepItemsInView (the default) goes to the first item,
    /// KeepLastItemInView to the last, and KeepScrollOffset stays on the current item (or, when it
    /// was removed, on the item that took its place).
    /// </summary>
    private void OnItemsSourceChanged(NotifyCollectionChangedEventArgs e)
    {
        if (VirtualView is not { } carouselView || PlatformView is null || MauiContext is null)
            return;

        var oldPosition = carouselView.Position;
        var currentItem = carouselView.CurrentItem;

        if (!TryApplyChange(carouselView, e))
            RebuildItems(carouselView);

        int count = _items.Count;
        int position;
        if (count == 0)
            position = 0;
        else
        {
            position = carouselView.ItemsUpdatingScrollMode switch
            {
                ItemsUpdatingScrollMode.KeepItemsInView => 0,
                ItemsUpdatingScrollMode.KeepLastItemInView => count - 1,
                _ when e.Action == NotifyCollectionChangedAction.Reset => 0,
                _ => KeptPosition(currentItem, oldPosition, count),
            };
        }
        SetCarouselPosition(carouselView, position);
        UpdateEmptyView(carouselView);
    }

    /// <summary>KeepScrollOffset: the current item where it is now, or the item now at its place.</summary>
    private int KeptPosition(object? currentItem, int oldPosition, int count)
    {
        if (currentItem != null)
        {
            var index = _items.IndexOf(currentItem);
            if (index >= 0)
                return index;
        }
        return Math.Clamp(oldPosition, 0, count - 1);
    }

    /// <summary>Moves the carousel to <paramref name="position"/> at once and tells the CarouselView (Position, CurrentItem).</summary>
    private void SetCarouselPosition(CarouselView carouselView, int position)
    {
        if (PlatformView is null)
            return;
        try
        {
            _isUpdatingPosition = true;
            if (PlatformView.ItemCount > 0)
                PlatformView.ScrollTo(position, false);
            if (carouselView.Position != position)
                carouselView.Position = position;
            var item = position >= 0 && position < _items.Count ? _items[position] : null;
            if (!Equals(carouselView.CurrentItem, item))
                carouselView.CurrentItem = item;
        }
        finally
        {
            _isUpdatingPosition = false;
        }
    }

    private bool TryApplyChange(CarouselView carouselView, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems != null:
            {
                int index = e.NewStartingIndex < 0 ? _items.Count : e.NewStartingIndex;
                if (index > _items.Count)
                    return false;
                for (int i = 0; i < e.NewItems.Count; i++)
                    InsertItem(carouselView, index + i, e.NewItems[i]);
                break;
            }
            case NotifyCollectionChangedAction.Remove when e.OldItems != null:
            {
                int index = e.OldStartingIndex;
                if (index < 0 || index + e.OldItems.Count > _items.Count)
                    return false;
                for (int i = 0; i < e.OldItems.Count; i++)
                    RemoveItemAt(index);
                break;
            }
            case NotifyCollectionChangedAction.Replace when e.NewItems != null && e.OldItems != null && e.NewItems.Count == e.OldItems.Count:
            {
                int index = e.OldStartingIndex;
                if (index < 0 || index + e.OldItems.Count > _items.Count)
                    return false;
                for (int i = 0; i < e.NewItems.Count; i++)
                {
                    RemoveItemAt(index + i);
                    InsertItem(carouselView, index + i, e.NewItems[i]);
                }
                break;
            }
            default:
                return false;
        }
        return carouselView.ItemsSource is not ICollection collection || collection.Count == _items.Count;
    }

    private void InsertItem(CarouselView carouselView, int index, object? item)
    {
        var (view, element) = CreateItemView(carouselView, item);
        _items.Insert(index, item);
        _itemElements.Insert(index, element);
        PlatformView!.InsertItem(index, view);
    }

    private void RemoveItemAt(int index)
    {
        var element = _itemElements[index];
        _items.RemoveAt(index);
        _itemElements.RemoveAt(index);
        PlatformView!.RemoveItemAt(index);
        ReleaseItemElement(element);
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
            if (e.CurrentPosition >= 0 && e.CurrentPosition < _items.Count)
            {
                VirtualView.CurrentItem = _items[e.CurrentPosition];
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

    /// <summary>The view of one item: its ItemTemplate's content (a logical child of the carousel), or a label with its text.</summary>
    private (SkiaView View, Element? Element) CreateItemView(CarouselView carouselView, object? item)
    {
        var template = carouselView.ItemTemplate;
        if (template != null && MauiContext != null)
        {
            try
            {
                var content = ItemTemplateContent.Create(template, item, carouselView);
                if (content is View view)
                {
                    // The item first, then the parent (for RelativeSource AncestorType
                    // bindings): parented first, the page would bind to the carousel's context.
                    view.BindingContext = item;
                    Element? element = null;
                    if (view.Parent == null)
                    {
                        // A logical child of the CarouselView, as MAUI adds a realized item.
                        carouselView.AddLogicalChild(view);
                        element = view;
                    }

                    view.Handler ??= view.ToViewHandler(MauiContext);
                    if (view.Handler?.PlatformView is SkiaView skiaView)
                        return (skiaView, element);

                    ReleaseItemElement(element);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("CarouselViewHandler", $"Creating the view for {item?.GetType().Name} failed", ex);
            }
        }
        return (new SkiaLabel { Text = item?.ToString() ?? "" }, null);
    }

    /// <summary>Makes every item view again from the items source.</summary>
    private void RebuildItems(CarouselView carouselView)
    {
        if (PlatformView is null)
            return;
        PlatformView.ClearItems();
        ReleaseItemElements();
        if (carouselView.ItemsSource is { } itemsSource)
        {
            foreach (var item in itemsSource)
            {
                var (view, element) = CreateItemView(carouselView, item);
                _items.Add(item);
                _itemElements.Add(element);
                PlatformView.AddItem(view);
            }
        }
        PlatformView.Invalidate();
    }

    public static void MapItemsSource(CarouselViewHandler handler, CarouselView carouselView)
    {
        if (handler.PlatformView is null || handler.MauiContext is null) return;

        var itemsSource = carouselView.ItemsSource;
        if (!ReferenceEquals(handler._collectionSubscription?.Source, itemsSource))
        {
            handler._collectionSubscription?.Dispose();
            handler._collectionSubscription = itemsSource is INotifyCollectionChanged observable
                ? new WeakCollectionChangedProxy<CarouselViewHandler>(observable, handler,
                    static (h, _, e) => h.OnItemsSourceChanged(e))
                : null;
        }

        handler.RebuildItems(carouselView);
        handler.UpdateEmptyView(carouselView);

        // The items made again show the CarouselView's position.
        var count = handler.PlatformView.ItemCount;
        if (count > 0 && carouselView.Position > 0)
        {
            try
            {
                handler._isUpdatingPosition = true;
                handler.PlatformView.ScrollTo(Math.Min(carouselView.Position, count - 1), false);
            }
            finally
            {
                handler._isUpdatingPosition = false;
            }
        }
    }

    public static void MapItemTemplate(CarouselViewHandler handler, CarouselView carouselView)
    {
        // Re-map items when template changes
        MapItemsSource(handler, carouselView);
    }

    /// <summary>
    /// The EmptyView (or EmptyViewTemplate) shown while the carousel has no items, as MAUI's
    /// CarouselView shows it on every platform.
    /// </summary>
    public static void MapEmptyView(CarouselViewHandler handler, CarouselView carouselView)
    {
        if (handler.PlatformView is null) return;
        handler._emptyViewDirty = true;
        handler.UpdateEmptyView(carouselView);
    }

    private bool _emptyViewDirty = true;

    private void UpdateEmptyView(CarouselView carouselView)
    {
        if (PlatformView is null)
            return;
        PlatformView.EmptyViewText = carouselView.EmptyView as string;
        // The empty view is made when it is first needed and when it changes.
        if (_emptyViewDirty && (_items.Count == 0 || PlatformView.EmptyViewContent != null))
        {
            _emptyViewDirty = false;
            PlatformView.EmptyViewContent = CollectionViewHandler.CreateEmptyViewContent(carouselView, MauiContext);
        }
    }

    public static void MapHorizontalScrollBarVisibility(CarouselViewHandler handler, CarouselView carouselView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.HorizontalScrollBarVisibility = (ScrollBarVisibility)carouselView.HorizontalScrollBarVisibility;
        handler.PlatformView.Invalidate();
    }

    public static void MapVerticalScrollBarVisibility(CarouselViewHandler handler, CarouselView carouselView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.VerticalScrollBarVisibility = (ScrollBarVisibility)carouselView.VerticalScrollBarVisibility;
    }

    /// <summary>Read when the items change (see <see cref="OnItemsSourceChanged"/>); nothing to set on the platform view.</summary>
    public static void MapItemsUpdatingScrollMode(CarouselViewHandler handler, CarouselView carouselView)
    {
    }

    /// <summary>MAUI's items handlers map IsVisible again (Windows updates the list's visibility).</summary>
    public static void MapIsVisible(CarouselViewHandler handler, CarouselView carouselView)
    {
        LinuxViewMappers.MapVisibility(handler, carouselView);
    }

    /// <summary>
    /// Moves to the Position, and makes the item there the CurrentItem, as MAUI's handlers do
    /// once the carousel has moved (the CarouselView does not keep the two in step itself).
    /// </summary>
    public static void MapPosition(CarouselViewHandler handler, CarouselView carouselView)
    {
        if (handler.PlatformView is null || handler._isUpdatingPosition) return;

        try
        {
            handler._isUpdatingPosition = true;
            var position = carouselView.Position;
            if (handler.PlatformView.Position != position)
            {
                handler.PlatformView.Position = position;
            }
            if (position >= 0 && position < handler._items.Count && !Equals(carouselView.CurrentItem, handler._items[position]))
            {
                carouselView.CurrentItem = handler._items[position];
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
        if (carouselView.CurrentItem != null)
        {
            int index = handler._items.IndexOf(carouselView.CurrentItem);
            if (index >= 0 && index != handler.PlatformView.Position)
            {
                try
                {
                    handler._isUpdatingPosition = true;
                    handler.PlatformView.Position = index;
                    if (carouselView.Position != index)
                        carouselView.Position = index;
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
        handler.PlatformView.Invalidate();
    }

    public static void MapPeekAreaInsets(CarouselViewHandler handler, CarouselView carouselView)
    {
        if (handler.PlatformView is null) return;
        // PeekAreaInsets is a Thickness in MAUI: its leading side along the carousel's axis.
        var insets = carouselView.PeekAreaInsets;
        handler.PlatformView.PeekAreaInsets = (float)(handler.PlatformView.Orientation == Platform.ItemsLayoutOrientation.Vertical ? insets.Top : insets.Left);
        handler.PlatformView.Invalidate();
    }

    /// <summary>
    /// The carousel's LinearItemsLayout: a vertical carousel moves its items up and down, and
    /// ItemSpacing separates them, as MAUI's handlers lay a CarouselView out.
    /// </summary>
    public static void MapItemsLayout(CarouselViewHandler handler, CarouselView carouselView)
    {
        if (handler.PlatformView is null) return;
        var layout = carouselView.ItemsLayout;
        handler.PlatformView.Orientation = layout?.Orientation == Controls.ItemsLayoutOrientation.Vertical
            ? Platform.ItemsLayoutOrientation.Vertical
            : Platform.ItemsLayoutOrientation.Horizontal;
        handler.PlatformView.ItemSpacing = layout?.ItemSpacing ?? 0;
        MapPeekAreaInsets(handler, carouselView);
        if (handler.PlatformView.ItemCount > 0)
            handler.PlatformView.ScrollTo(Math.Clamp(carouselView.Position, 0, handler.PlatformView.ItemCount - 1), false);
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
            var index = scrollArgs.Mode == ScrollToMode.Element
                ? handler._items.IndexOf(scrollArgs.Item)
                : scrollArgs.Index;
            handler.PlatformView.ScrollTo(index, scrollArgs.IsAnimated);
        }
    }
}
