// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI's SwipeViewHandler on Linux for any <see cref="ISwipeView"/>: a library's own swipe view,
/// or a view given this handler. (OpenMaui's <see cref="SwipeViewHandler"/> is typed to the
/// Controls <c>SwipeView</c> and chains <see cref="Mapper"/> and <see cref="CommandMapper"/>.)
/// The platform view is a <see cref="SkiaSwipeView"/>. It maps MAUI's keys (Content,
/// SwipeTransitionMode, the four item sides, RequestOpen, RequestClose) and reports the swipe
/// back to the view as MAUI's platforms do: <see cref="ISwipeView.SwipeStarted"/>,
/// <see cref="ISwipeView.SwipeChanging"/>, <see cref="ISwipeView.SwipeEnded"/> and
/// <see cref="ISwipeView.IsOpen"/>. Each item gets its handler: a menu item a
/// <see cref="SwipeItemMenuItemHandler"/>, an item view a <see cref="SwipeItemViewHandler"/>.
/// </summary>
public partial class CoreSwipeViewHandler : LinuxViewHandler<ISwipeView, SkiaSwipeView>, ISwipeViewHandler
{
    public static IPropertyMapper<ISwipeView, ISwipeViewHandler> Mapper = new PropertyMapper<ISwipeView, ISwipeViewHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IContentView.Content)] = MapContent,
        [nameof(ISwipeView.SwipeTransitionMode)] = MapSwipeTransitionMode,
        [nameof(ISwipeView.LeftItems)] = MapLeftItems,
        [nameof(ISwipeView.TopItems)] = MapTopItems,
        [nameof(ISwipeView.RightItems)] = MapRightItems,
        [nameof(ISwipeView.BottomItems)] = MapBottomItems,
        [nameof(ISwipeView.Threshold)] = MapThreshold,
        [nameof(IView.IsEnabled)] = MapIsEnabled,
        [nameof(IView.Background)] = MapBackground,
    };

    public static CommandMapper<ISwipeView, ISwipeViewHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
        [nameof(ISwipeView.RequestOpen)] = MapRequestOpen,
        [nameof(ISwipeView.RequestClose)] = MapRequestClose,
    };

    private SwipeViewConnection? _connection;

    public CoreSwipeViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public CoreSwipeViewHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper, CommandMapper)
    {
    }

    public CoreSwipeViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    ISwipeView ISwipeViewHandler.VirtualView => VirtualView;

    object ISwipeViewHandler.PlatformView => PlatformView;

    protected override SkiaSwipeView CreatePlatformView() => new();

    protected override void ConnectHandler(SkiaSwipeView platformView)
    {
        base.ConnectHandler(platformView);
        _connection = new SwipeViewConnection(platformView, () => VirtualView);
    }

    protected override void DisconnectHandler(SkiaSwipeView platformView)
    {
        _connection?.Disconnect();
        _connection = null;
        base.DisconnectHandler(platformView);
    }

    /// <summary>The Skia swipe view of a swipe view handler (this one or the Controls one).</summary>
    internal static SkiaSwipeView? SwipePlatformView(ISwipeViewHandler handler) =>
        handler.PlatformView as SkiaSwipeView;

    /// <summary>The view's presented content (its Content when that is a view), realized with the handler's context.</summary>
    public static void MapContent(ISwipeViewHandler handler, ISwipeView view)
    {
        if (SwipePlatformView(handler) is not { } platformView || handler.MauiContext is null)
            return;

        var content = view.PresentedContent ?? view.Content as IView;
        if (content is null)
        {
            platformView.Content = null;
            return;
        }

        try
        {
            if (content.Handler is null)
                content.Handler = content.ToViewHandler(handler.MauiContext);
            platformView.Content = content.Handler?.PlatformView as SkiaView;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SwipeViewHandler", $"Failed to realize the content ({content.GetType().Name})", ex);
        }
    }

    public static void MapSwipeTransitionMode(ISwipeViewHandler handler, ISwipeView swipeView)
    {
        if (SwipePlatformView(handler) is { } platformView)
        {
            platformView.TransitionMode = swipeView.SwipeTransitionMode;
            platformView.Invalidate();
        }
    }

    public static void MapLeftItems(ISwipeViewHandler handler, ISwipeView view) =>
        UpdateItems(handler, view, OpenSwipeItem.LeftItems);

    public static void MapTopItems(ISwipeViewHandler handler, ISwipeView view) =>
        UpdateItems(handler, view, OpenSwipeItem.TopItems);

    public static void MapRightItems(ISwipeViewHandler handler, ISwipeView view) =>
        UpdateItems(handler, view, OpenSwipeItem.RightItems);

    public static void MapBottomItems(ISwipeViewHandler handler, ISwipeView view) =>
        UpdateItems(handler, view, OpenSwipeItem.BottomItems);

    /// <summary>
    /// MAUI's Threshold is how far a swipe must go to open the items (0: 60% of their size), not
    /// how far they open; the open distance comes from the items.
    /// </summary>
    public static void MapThreshold(ISwipeViewHandler handler, ISwipeView view)
    {
        if (SwipePlatformView(handler) is { } platformView)
            platformView.Threshold = (float)Math.Max(0, view.Threshold);
    }

    /// <summary>A disabled swipe view does not swipe (MAUI's Android and iOS handlers map it).</summary>
    public static void MapIsEnabled(ISwipeViewHandler handler, ISwipeView view)
    {
        if (SwipePlatformView(handler) is { } platformView)
        {
            platformView.IsEnabled = view.IsEnabled;
            if (!view.IsEnabled && platformView.IsOpen)
                platformView.Close();
        }
    }

    public static void MapBackground(ISwipeViewHandler handler, ISwipeView view)
    {
        if (SwipePlatformView(handler) is not { } platformView)
            return;
        if (view.Background is SolidPaint { Color: { } color })
            platformView.BackgroundColor = color;
        else if (view.Background is null)
            platformView.BackgroundColor = null;
    }

    /// <summary>Opens the requested items (the swipe view's IsOpen follows).</summary>
    public static void MapRequestOpen(ISwipeViewHandler handler, ISwipeView swipeView, object? args)
    {
        if (args is not SwipeViewOpenRequest request || SwipePlatformView(handler) is not { } platformView)
            return;

        platformView.Open(request.OpenSwipeItem);
    }

    /// <summary>Closes the view (the swipe view's IsOpen follows).</summary>
    public static void MapRequestClose(ISwipeViewHandler handler, ISwipeView swipeView, object? args)
    {
        if (SwipePlatformView(handler) is not { } platformView)
            return;

        platformView.Close();
    }

    /// <summary>
    /// Shows a side's items: each menu item through its handler's platform item (text, colours,
    /// visibility and icon follow the item, a tap invokes it), each item view through its
    /// handler's view, as MAUI's SwipeView handlers realize their items; the items' Mode and
    /// SwipeBehaviorOnInvoked apply to the side.
    /// </summary>
    internal static void UpdateItems(ISwipeViewHandler handler, ISwipeView view, OpenSwipeItem side)
    {
        if (SwipePlatformView(handler) is not { } platformView)
            return;

        var platformItems = side switch
        {
            OpenSwipeItem.LeftItems => platformView.LeftItems,
            OpenSwipeItem.RightItems => platformView.RightItems,
            OpenSwipeItem.TopItems => platformView.TopItems,
            _ => platformView.BottomItems,
        };
        var items = side switch
        {
            OpenSwipeItem.LeftItems => view.LeftItems,
            OpenSwipeItem.RightItems => view.RightItems,
            OpenSwipeItem.TopItems => view.TopItems,
            _ => view.BottomItems,
        };

        platformItems.Clear();
        if (items is not null)
        {
            platformView.SetItemsMode(side, items.Mode == Microsoft.Maui.SwipeMode.Execute ? SwipeMode.Execute : SwipeMode.Reveal);
            platformView.SetItemsBehaviorOnInvoked(side, items.SwipeBehaviorOnInvoked);

            foreach (var item in items)
            {
                if (item is null)
                    continue;
                if (PlatformItem(handler, item) is { } platformItem)
                    platformItems.Add(platformItem);
            }
        }

        platformView.OnItemsChanged();
    }

    /// <summary>The platform item of a swipe item, from its handler.</summary>
    private static SwipeItem? PlatformItem(ISwipeViewHandler handler, ISwipeItem item)
    {
        try
        {
            if (item is ISwipeItemMenuItem menuItem)
                return MenuItemHandler(handler, menuItem)?.PlatformView;

            if (item is ISwipeItemView itemView)
            {
                if (handler.MauiContext is null)
                    return null;
                if (itemView.Handler is null)
                    itemView.Handler = itemView.ToViewHandler(handler.MauiContext);
                if (itemView.Handler is SwipeItemViewHandler itemViewHandler)
                    return itemViewHandler.SwipeItem;
                // A handler that is not the Linux one: show its view as the item.
                return new SwipeItem
                {
                    Content = itemView.Handler?.PlatformView as SkiaView,
                    IsVisible = itemView.Visibility == Visibility.Visible,
                    Invoker = itemView,
                };
            }

            // A plain ISwipeItem: a default item that invokes it.
            return new SwipeItem { Invoker = item };
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SwipeViewHandler", $"Realizing a swipe item ({item.GetType().Name}) failed", ex);
            return null;
        }
    }

    /// <summary>
    /// The Linux handler of a menu item, created with the swipe view's context if needed: the
    /// handler registered for the item's type when it is a Linux one (an app's own subclass),
    /// else <see cref="SwipeItemMenuItemHandler"/> (MAUI's platform-neutral one has no
    /// platform item).
    /// </summary>
    internal static SwipeItemMenuItemHandler? MenuItemHandler(ISwipeViewHandler handler, ISwipeItemMenuItem item)
    {
        if (item.Handler is SwipeItemMenuItemHandler existing)
            return existing;
        if (handler.MauiContext is null)
            return null;

        SwipeItemMenuItemHandler? itemHandler = null;
        var registered = handler.MauiContext.Handlers.GetHandlerType(item.GetType());
        if (registered is not null && typeof(SwipeItemMenuItemHandler).IsAssignableFrom(registered))
            itemHandler = handler.MauiContext.Handlers.GetHandler(item.GetType()) as SwipeItemMenuItemHandler;
        itemHandler ??= new SwipeItemMenuItemHandler();
        itemHandler.SetMauiContext(handler.MauiContext);
        item.Handler = itemHandler;
        return itemHandler;
    }
}

/// <summary>
/// Reports a <see cref="SkiaSwipeView"/>'s swipe to its <see cref="ISwipeView"/>, as MAUI's
/// platform swipe views do: started, changing and ended, and IsOpen.
/// </summary>
internal sealed class SwipeViewConnection
{
    private readonly SkiaSwipeView _platformView;
    private readonly Func<ISwipeView?> _view;

    public SwipeViewConnection(SkiaSwipeView platformView, Func<ISwipeView?> view)
    {
        _platformView = platformView;
        _view = view;
        platformView.SwipeStarted += OnSwipeStarted;
        platformView.SwipeChanging += OnSwipeChanging;
        platformView.SwipeEnded += OnSwipeEnded;
        platformView.IsOpenChanged += OnIsOpenChanged;
    }

    public void Disconnect()
    {
        _platformView.SwipeStarted -= OnSwipeStarted;
        _platformView.SwipeChanging -= OnSwipeChanging;
        _platformView.SwipeEnded -= OnSwipeEnded;
        _platformView.IsOpenChanged -= OnIsOpenChanged;
    }

    internal static Microsoft.Maui.SwipeDirection ToMaui(SwipeDirection direction) => direction switch
    {
        SwipeDirection.Left => Microsoft.Maui.SwipeDirection.Left,
        SwipeDirection.Up => Microsoft.Maui.SwipeDirection.Up,
        SwipeDirection.Down => Microsoft.Maui.SwipeDirection.Down,
        _ => Microsoft.Maui.SwipeDirection.Right,
    };

    private void OnSwipeStarted(object? sender, SwipeStartedEventArgs e) =>
        _view()?.SwipeStarted(new SwipeViewSwipeStarted(ToMaui(e.Direction)));

    private void OnSwipeChanging(object? sender, SwipeOffsetChangedEventArgs e) =>
        _view()?.SwipeChanging(new SwipeViewSwipeChanging(ToMaui(e.Direction), e.Offset));

    private void OnSwipeEnded(object? sender, SwipeEndedEventArgs e) =>
        _view()?.SwipeEnded(new SwipeViewSwipeEnded(ToMaui(e.Direction), e.IsOpen));

    private void OnIsOpenChanged(object? sender, EventArgs e)
    {
        if (_view() is { } view && view.IsOpen != _platformView.IsOpen)
            view.IsOpen = _platformView.IsOpen;
    }
}
