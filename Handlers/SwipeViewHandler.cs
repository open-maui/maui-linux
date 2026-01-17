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
/// Handler for SwipeView on Linux using Skia rendering.
/// Maps SwipeView to SkiaSwipeView platform view.
/// </summary>
public partial class SwipeViewHandler : ViewHandler<SwipeView, SkiaSwipeView>
{
    public static IPropertyMapper<SwipeView, SwipeViewHandler> Mapper =
        new PropertyMapper<SwipeView, SwipeViewHandler>(ViewHandler.ViewMapper)
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
        new(ViewHandler.ViewCommandMapper)
        {
            ["RequestOpen"] = MapRequestOpen,
            ["RequestClose"] = MapRequestClose,
        };

    public SwipeViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public SwipeViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaSwipeView CreatePlatformView()
    {
        return new SkiaSwipeView();
    }

    protected override void ConnectHandler(SkiaSwipeView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.SwipeStarted += OnSwipeStarted;
        platformView.SwipeEnded += OnSwipeEnded;
    }

    protected override void DisconnectHandler(SkiaSwipeView platformView)
    {
        platformView.SwipeStarted -= OnSwipeStarted;
        platformView.SwipeEnded -= OnSwipeEnded;
        base.DisconnectHandler(platformView);
    }

    private void OnSwipeStarted(object? sender, Platform.SwipeStartedEventArgs e)
    {
        // SwipeView events are handled internally by the platform view
    }

    private void OnSwipeEnded(object? sender, Platform.SwipeEndedEventArgs e)
    {
        // SwipeView events are handled internally by the platform view
    }

    public static void MapContent(SwipeViewHandler handler, SwipeView swipeView)
    {
        if (handler.PlatformView is null || handler.MauiContext is null) return;

        var content = swipeView.Content;
        if (content == null)
        {
            handler.PlatformView.Content = null;
            return;
        }

        // Create handler for content
        if (content.Handler == null)
        {
            content.Handler = content.ToViewHandler(handler.MauiContext);
        }

        if (content.Handler?.PlatformView is SkiaView skiaContent)
        {
            handler.PlatformView.Content = skiaContent;
        }
    }

    public static void MapLeftItems(SwipeViewHandler handler, SwipeView swipeView)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.LeftItems.Clear();

        if (swipeView.LeftItems != null)
        {
            foreach (var item in swipeView.LeftItems)
            {
                handler.PlatformView.LeftItems.Add(CreatePlatformSwipeItem(item));
            }
        }
    }

    public static void MapRightItems(SwipeViewHandler handler, SwipeView swipeView)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.RightItems.Clear();

        if (swipeView.RightItems != null)
        {
            foreach (var item in swipeView.RightItems)
            {
                handler.PlatformView.RightItems.Add(CreatePlatformSwipeItem(item));
            }
        }
    }

    public static void MapTopItems(SwipeViewHandler handler, SwipeView swipeView)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.TopItems.Clear();

        if (swipeView.TopItems != null)
        {
            foreach (var item in swipeView.TopItems)
            {
                handler.PlatformView.TopItems.Add(CreatePlatformSwipeItem(item));
            }
        }
    }

    public static void MapBottomItems(SwipeViewHandler handler, SwipeView swipeView)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.BottomItems.Clear();

        if (swipeView.BottomItems != null)
        {
            foreach (var item in swipeView.BottomItems)
            {
                handler.PlatformView.BottomItems.Add(CreatePlatformSwipeItem(item));
            }
        }
    }

    public static void MapThreshold(SwipeViewHandler handler, SwipeView swipeView)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.LeftSwipeThreshold = (float)swipeView.Threshold;
        handler.PlatformView.RightSwipeThreshold = (float)swipeView.Threshold;
    }

    public static void MapBackground(SwipeViewHandler handler, SwipeView swipeView)
    {
        if (handler.PlatformView is null) return;

        if (swipeView.Background is SolidColorBrush solidBrush)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color;
        }
    }

    public static void MapRequestOpen(SwipeViewHandler handler, SwipeView swipeView, object? args)
    {
        if (handler.PlatformView is null) return;

        if (args is SwipeViewOpenRequest request)
        {
            var direction = request.OpenSwipeItem switch
            {
                OpenSwipeItem.LeftItems => Platform.SwipeDirection.Right,
                OpenSwipeItem.RightItems => Platform.SwipeDirection.Left,
                OpenSwipeItem.TopItems => Platform.SwipeDirection.Down,
                OpenSwipeItem.BottomItems => Platform.SwipeDirection.Up,
                _ => Platform.SwipeDirection.Right
            };

            handler.PlatformView.Open(direction);
        }
    }

    public static void MapRequestClose(SwipeViewHandler handler, SwipeView swipeView, object? args)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Close();
    }

    private static Platform.SwipeItem CreatePlatformSwipeItem(ISwipeItem item)
    {
        var platformItem = new Platform.SwipeItem();

        if (item is Controls.SwipeItem swipeItem)
        {
            platformItem.Text = swipeItem.Text ?? "";

            // Get background color
            var bgColor = swipeItem.BackgroundColor;
            if (bgColor is not null)
            {
                platformItem.BackgroundColor = bgColor;
            }
        }
        else if (item is Controls.SwipeItemView swipeItemView)
        {
            // SwipeItemView uses custom content - use a simple representation
            platformItem.Text = "Action";
            platformItem.BackgroundColor = Color.FromRgb(100, 100, 100);
        }

        return platformItem;
    }
}
