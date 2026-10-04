// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI's SwipeItemViewHandler on Linux: the handler of a swipe item with custom content (a
/// Controls <c>SwipeItemView</c> or any <see cref="ISwipeItemView"/>). Its platform view is a
/// <see cref="SkiaContentView"/> showing the content, which the <see cref="SkiaSwipeView"/> lays
/// out and draws in the item's place (<see cref="SwipeItem"/>); a tap on it invokes the item, as
/// on MAUI's Android and iOS (MAUI's Windows SwipeControl shows menu items only).
/// </summary>
public partial class SwipeItemViewHandler : ContentViewHandler, ISwipeItemViewHandler
{
    public static new IPropertyMapper<ISwipeItemView, ISwipeItemViewHandler> Mapper =
        new PropertyMapper<ISwipeItemView, ISwipeItemViewHandler>(ContentViewHandler.Mapper)
        {
            [nameof(ISwipeItemView.Content)] = MapContent,
            [nameof(ISwipeItemView.Visibility)] = MapVisibility,
        };

    public static new CommandMapper<ISwipeItemView, ISwipeItemViewHandler> CommandMapper =
        new(ContentViewHandler.CommandMapper)
        {
        };

    private SwipeItem? _swipeItem;

    public SwipeItemViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public SwipeItemViewHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper, CommandMapper)
    {
    }

    public SwipeItemViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    ISwipeItemView ISwipeItemViewHandler.VirtualView => (ISwipeItemView)VirtualView;

    object ISwipeItemViewHandler.PlatformView => PlatformView;

    /// <summary>
    /// The item the swipe view shows for this item view: its <see cref="SwipeItem.Content"/> is
    /// this handler's platform view, and a tap on it invokes the item view.
    /// </summary>
    public SwipeItem SwipeItem => _swipeItem ??= new SwipeItem
    {
        Content = PlatformView,
        Invoker = VirtualView as ISwipeItemView,
        IsVisible = VirtualView?.Visibility == Visibility.Visible,
    };

    protected override void DisconnectHandler(SkiaContentView platformView)
    {
        if (_swipeItem is not null)
        {
            _swipeItem.Content = null;
            _swipeItem.Invoker = null;
            _swipeItem = null;
        }
        base.DisconnectHandler(platformView);
    }

    /// <summary>The item view's content, as a content view shows it.</summary>
    public static void MapContent(ISwipeItemViewHandler handler, ISwipeItemView view)
    {
        if (handler is ContentViewHandler contentHandler)
            ContentViewHandler.MapContent(contentHandler, view);
    }

    /// <summary>
    /// A hidden item view is not shown in the swipe view and does not count in its open
    /// distance: the swipe view lays its items out again.
    /// </summary>
    public static void MapVisibility(ISwipeItemViewHandler handler, ISwipeItemView view)
    {
        LinuxViewMappers.MapVisibility(handler, view);
        if (handler is SwipeItemViewHandler { _swipeItem: { } item })
            item.IsVisible = view.Visibility == Visibility.Visible;
        if (handler.PlatformView is SkiaView { Parent: SkiaSwipeView swipeView })
            swipeView.OnItemsChanged();
    }
}
