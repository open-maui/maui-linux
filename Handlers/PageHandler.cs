// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Base handler for Page on Linux using Skia rendering.
/// </summary>
public partial class PageHandler : ViewHandler<Page, SkiaPage>
{
    public static IPropertyMapper<Page, PageHandler> Mapper =
        new PropertyMapper<Page, PageHandler>(ViewHandler.ViewMapper)
        {
            [nameof(Page.Title)] = MapTitle,
            [nameof(Page.BackgroundImageSource)] = MapBackgroundImageSource,
            [nameof(Page.Padding)] = MapPadding,
            [nameof(IView.Background)] = MapBackground,
        };

    public static CommandMapper<Page, PageHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
        };

    public PageHandler() : base(Mapper, CommandMapper)
    {
    }

    public PageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaPage CreatePlatformView()
    {
        return new SkiaPage();
    }

    protected override void ConnectHandler(SkiaPage platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Appearing += OnAppearing;
        platformView.Disappearing += OnDisappearing;
    }

    protected override void DisconnectHandler(SkiaPage platformView)
    {
        platformView.Appearing -= OnAppearing;
        platformView.Disappearing -= OnDisappearing;
        base.DisconnectHandler(platformView);
    }

    private void OnAppearing(object? sender, EventArgs e)
    {
        (VirtualView as IPageController)?.SendAppearing();
    }

    private void OnDisappearing(object? sender, EventArgs e)
    {
        (VirtualView as IPageController)?.SendDisappearing();
    }

    public static void MapTitle(PageHandler handler, Page page)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Title = page.Title ?? "";
    }

    public static void MapBackgroundImageSource(PageHandler handler, Page page)
    {
        // Background image would be loaded and set here
        // For now, we just invalidate
        handler.PlatformView?.Invalidate();
    }

    public static void MapPadding(PageHandler handler, Page page)
    {
        if (handler.PlatformView is null) return;

        var padding = page.Padding;
        handler.PlatformView.PaddingLeft = (float)padding.Left;
        handler.PlatformView.PaddingTop = (float)padding.Top;
        handler.PlatformView.PaddingRight = (float)padding.Right;
        handler.PlatformView.PaddingBottom = (float)padding.Bottom;
    }

    public static void MapBackground(PageHandler handler, Page page)
    {
        if (handler.PlatformView is null) return;

        if (page.Background is SolidColorBrush solidBrush)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color.ToSKColor();
        }
    }
}

/// <summary>
/// Handler for ContentPage on Linux using Skia rendering.
/// </summary>
public partial class ContentPageHandler : PageHandler
{
    public static new IPropertyMapper<ContentPage, ContentPageHandler> Mapper =
        new PropertyMapper<ContentPage, ContentPageHandler>(PageHandler.Mapper)
        {
            [nameof(ContentPage.Content)] = MapContent,
        };

    public static new CommandMapper<ContentPage, ContentPageHandler> CommandMapper =
        new(PageHandler.CommandMapper)
        {
        };

    public ContentPageHandler() : base(Mapper, CommandMapper)
    {
    }

    public ContentPageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaPage CreatePlatformView()
    {
        return new SkiaContentPage();
    }

    public static void MapContent(ContentPageHandler handler, ContentPage page)
    {
        if (handler.PlatformView is null) return;

        // Get the platform view for the content
        var content = page.Content;
        if (content != null)
        {
            // The content's handler should provide the platform view
            var contentHandler = content.Handler;
            if (contentHandler?.PlatformView is SkiaView skiaContent)
            {
                handler.PlatformView.Content = skiaContent;
            }
        }
        else
        {
            handler.PlatformView.Content = null;
        }
    }
}
