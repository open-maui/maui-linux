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
/// Handler for the Controls <see cref="SwipeView"/> on Linux, platform view
/// <see cref="SkiaSwipeView"/>. It chains <see cref="CoreSwipeViewHandler"/>'s mappers (MAUI's
/// keys, for any <see cref="ISwipeView"/>), so the swipe reports SwipeStarted, SwipeChanging,
/// SwipeEnded and IsOpen to the SwipeView, and its items get their handlers.
/// </summary>
public partial class SwipeViewHandler : LinuxViewHandler<SwipeView, SkiaSwipeView>, ISwipeViewHandler
{
    public static IPropertyMapper<SwipeView, SwipeViewHandler> Mapper =
        new PropertyMapper<SwipeView, SwipeViewHandler>(CoreSwipeViewHandler.Mapper)
        {
            [nameof(SwipeView.Content)] = MapContent,
            [nameof(SwipeView.LeftItems)] = MapLeftItems,
            [nameof(SwipeView.RightItems)] = MapRightItems,
            [nameof(SwipeView.TopItems)] = MapTopItems,
            [nameof(SwipeView.BottomItems)] = MapBottomItems,
            [nameof(SwipeView.Threshold)] = MapThreshold,
            [nameof(IView.Background)] = MapBackground,
        };

    public static CommandMapper<SwipeView, SwipeViewHandler> CommandMapper =
        new(CoreSwipeViewHandler.CommandMapper)
        {
            ["RequestOpen"] = MapRequestOpen,
            ["RequestClose"] = MapRequestClose,
        };

    private SwipeViewConnection? _connection;

    public SwipeViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public SwipeViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    ISwipeView ISwipeViewHandler.VirtualView => VirtualView;

    object ISwipeViewHandler.PlatformView => PlatformView;

    protected override SkiaSwipeView CreatePlatformView()
    {
        return new SkiaSwipeView();
    }

    protected override void ConnectHandler(SkiaSwipeView platformView)
    {
        base.ConnectHandler(platformView);
        // The swipe is reported to the SwipeView (its SwipeStarted, SwipeChanging and SwipeEnded
        // events, IsOpen), as MAUI's platform swipe views do.
        _connection = new SwipeViewConnection(platformView, () => VirtualView);
    }

    protected override void DisconnectHandler(SkiaSwipeView platformView)
    {
        _connection?.Disconnect();
        _connection = null;
        base.DisconnectHandler(platformView);
    }

    /// <summary>MAUI's <c>MapContent</c> signature; maps the property on a Linux <see cref="SwipeViewHandler"/>.</summary>
    public static void MapContent(ISwipeViewHandler handler, ISwipeView view)
    {
        if (handler is SwipeViewHandler linuxHandler && view is SwipeView linuxView)
            MapContentCore(linuxHandler, linuxView);
    }

    private static void MapContentCore(SwipeViewHandler handler, SwipeView swipeView) =>
        CoreSwipeViewHandler.MapContent(handler, swipeView);

    /// <summary>MAUI's <c>MapLeftItems</c> signature; maps the property on a Linux <see cref="SwipeViewHandler"/>.</summary>
    public static void MapLeftItems(ISwipeViewHandler handler, ISwipeView view)
    {
        if (handler is SwipeViewHandler linuxHandler && view is SwipeView linuxView)
            MapLeftItemsCore(linuxHandler, linuxView);
    }

    private static void MapLeftItemsCore(SwipeViewHandler handler, SwipeView swipeView) =>
        CoreSwipeViewHandler.MapLeftItems(handler, swipeView);

    /// <summary>MAUI's <c>MapRightItems</c> signature; maps the property on a Linux <see cref="SwipeViewHandler"/>.</summary>
    public static void MapRightItems(ISwipeViewHandler handler, ISwipeView view)
    {
        if (handler is SwipeViewHandler linuxHandler && view is SwipeView linuxView)
            MapRightItemsCore(linuxHandler, linuxView);
    }

    private static void MapRightItemsCore(SwipeViewHandler handler, SwipeView swipeView) =>
        CoreSwipeViewHandler.MapRightItems(handler, swipeView);

    /// <summary>MAUI's <c>MapTopItems</c> signature; maps the property on a Linux <see cref="SwipeViewHandler"/>.</summary>
    public static void MapTopItems(ISwipeViewHandler handler, ISwipeView view)
    {
        if (handler is SwipeViewHandler linuxHandler && view is SwipeView linuxView)
            MapTopItemsCore(linuxHandler, linuxView);
    }

    private static void MapTopItemsCore(SwipeViewHandler handler, SwipeView swipeView) =>
        CoreSwipeViewHandler.MapTopItems(handler, swipeView);

    /// <summary>MAUI's <c>MapBottomItems</c> signature; maps the property on a Linux <see cref="SwipeViewHandler"/>.</summary>
    public static void MapBottomItems(ISwipeViewHandler handler, ISwipeView view)
    {
        if (handler is SwipeViewHandler linuxHandler && view is SwipeView linuxView)
            MapBottomItemsCore(linuxHandler, linuxView);
    }

    private static void MapBottomItemsCore(SwipeViewHandler handler, SwipeView swipeView) =>
        CoreSwipeViewHandler.MapBottomItems(handler, swipeView);

    /// <summary>
    /// MAUI's Threshold: how far a swipe must go for the items to open (0 means 60% of their
    /// size), not how far they open.
    /// </summary>
    public static void MapThreshold(SwipeViewHandler handler, SwipeView swipeView) =>
        CoreSwipeViewHandler.MapThreshold(handler, swipeView);

    public static void MapBackground(SwipeViewHandler handler, SwipeView swipeView)
    {
        if (handler.PlatformView is null) return;

        if (swipeView.Background is SolidColorBrush { Color: not null } solidBrush)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color;
        }
    }

    /// <summary>MAUI's <c>MapRequestOpen</c> signature; runs the command on a Linux <see cref="SwipeViewHandler"/>.</summary>
    public static void MapRequestOpen(ISwipeViewHandler handler, ISwipeView swipeView, object? args)
    {
        if (handler is SwipeViewHandler linuxHandler && swipeView is SwipeView linuxView)
            MapRequestOpenCore(linuxHandler, linuxView, args);
    }

    private static void MapRequestOpenCore(SwipeViewHandler handler, SwipeView swipeView, object? args) =>
        CoreSwipeViewHandler.MapRequestOpen(handler, swipeView, args);

    /// <summary>MAUI's <c>MapRequestClose</c> signature; runs the command on a Linux <see cref="SwipeViewHandler"/>.</summary>
    public static void MapRequestClose(ISwipeViewHandler handler, ISwipeView swipeView, object? args)
    {
        if (handler is SwipeViewHandler linuxHandler && swipeView is SwipeView linuxView)
            MapRequestCloseCore(linuxHandler, linuxView, args);
    }

    private static void MapRequestCloseCore(SwipeViewHandler handler, SwipeView swipeView, object? args) =>
        CoreSwipeViewHandler.MapRequestClose(handler, swipeView, args);

    /// <summary>MAUI's <c>MapSwipeTransitionMode</c>: how the items appear (Reveal or Drag).</summary>
    public static void MapSwipeTransitionMode(ISwipeViewHandler handler, ISwipeView swipeView) =>
        CoreSwipeViewHandler.MapSwipeTransitionMode(handler, swipeView);
}
