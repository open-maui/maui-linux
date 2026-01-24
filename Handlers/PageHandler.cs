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
/// Base handler for Page on Linux using Skia rendering.
/// </summary>
public partial class PageHandler : ViewHandler<Page, SkiaPage>
{
    public static IPropertyMapper<Page, PageHandler> Mapper =
        new PropertyMapper<Page, PageHandler>(ViewHandler.ViewMapper)
        {
            [nameof(Page.Title)] = MapTitle,
            [nameof(Page.BackgroundImageSource)] = MapBackgroundImageSource,
            [nameof(Page.IconImageSource)] = MapIconImageSource,
            [nameof(Page.Padding)] = MapPadding,
            [nameof(Page.IsBusy)] = MapIsBusy,
            [nameof(IView.Background)] = MapBackground,
            [nameof(VisualElement.BackgroundColor)] = MapBackgroundColor,
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

        // Set MauiPage reference for theme refresh support
        platformView.MauiPage = VirtualView;

        platformView.Appearing += OnAppearing;
        platformView.Disappearing += OnDisappearing;
    }

    protected override void DisconnectHandler(SkiaPage platformView)
    {
        platformView.Appearing -= OnAppearing;
        platformView.Disappearing -= OnDisappearing;
        platformView.MauiPage = null;
        base.DisconnectHandler(platformView);
    }

    private void OnAppearing(object? sender, EventArgs e)
    {
        Console.WriteLine($"[PageHandler] OnAppearing received for: {VirtualView?.Title}");
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
            handler.PlatformView.BackgroundColor = solidBrush.Color;
        }
    }

    public static void MapBackgroundColor(PageHandler handler, Page page)
    {
        if (handler.PlatformView is null) return;

        var backgroundColor = page.BackgroundColor;
        if (backgroundColor != null && backgroundColor != Colors.Transparent)
        {
            handler.PlatformView.BackgroundColor = backgroundColor;
            Console.WriteLine($"[PageHandler] MapBackgroundColor: {backgroundColor}");
        }
    }

    public static void MapIconImageSource(PageHandler handler, Page page)
    {
        // Icon is typically used by navigation containers (Shell, TabbedPage)
        // Store for later use but don't render directly on the page
        handler.PlatformView?.Invalidate();
    }

    public static void MapIsBusy(PageHandler handler, Page page)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsBusy = page.IsBusy;
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
            [nameof(ContentPage.ToolbarItems)] = MapToolbarItems,
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

    protected override void ConnectHandler(SkiaPage platformView)
    {
        base.ConnectHandler(platformView);

        // Sync toolbar items initially
        if (VirtualView is ContentPage contentPage && platformView is SkiaContentPage skiaContentPage)
        {
            SyncToolbarItems(skiaContentPage, contentPage);
        }
    }

    public static void MapContent(ContentPageHandler handler, ContentPage page)
    {
        if (handler.PlatformView is null || handler.MauiContext is null) return;

        // Get the platform view for the content
        var content = page.Content;
        if (content != null)
        {
            // Create handler for content if it doesn't exist
            if (content.Handler == null)
            {
                Console.WriteLine($"[ContentPageHandler] Creating handler for content: {content.GetType().Name}");
                content.Handler = content.ToViewHandler(handler.MauiContext);
            }

            // The content's handler should provide the platform view
            if (content.Handler?.PlatformView is SkiaView skiaContent)
            {
                Console.WriteLine($"[ContentPageHandler] Setting content: {skiaContent.GetType().Name}");
                handler.PlatformView.Content = skiaContent;
            }
            else
            {
                Console.WriteLine($"[ContentPageHandler] Content handler PlatformView is not SkiaView: {content.Handler?.PlatformView?.GetType().Name ?? "null"}");
            }
        }
        else
        {
            handler.PlatformView.Content = null;
        }
    }

    public static void MapToolbarItems(ContentPageHandler handler, ContentPage page)
    {
        if (handler.PlatformView is not SkiaContentPage skiaContentPage) return;
        SyncToolbarItems(skiaContentPage, page);
    }

    private static void SyncToolbarItems(SkiaContentPage platformView, ContentPage page)
    {
        platformView.ToolbarItems.Clear();

        foreach (var item in page.ToolbarItems)
        {
            var skiaItem = new SkiaToolbarItem
            {
                Text = item.Text ?? "",
                Command = item.Command,
                Order = item.Order == ToolbarItemOrder.Primary
                    ? SkiaToolbarItemOrder.Primary
                    : SkiaToolbarItemOrder.Secondary
            };

            // Load icon if present
            if (item.IconImageSource is FileImageSource fileSource)
            {
                // Icon loading would be async - simplified for now
                Console.WriteLine($"[ContentPageHandler] Toolbar item icon: {fileSource.File}");
            }

            platformView.ToolbarItems.Add(skiaItem);
        }

        platformView.Invalidate();
    }
}
