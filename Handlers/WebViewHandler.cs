// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for WebView control on Linux using WebKitGTK.
/// </summary>
public partial class WebViewHandler : ViewHandler<IWebView, SkiaWebView>
{
    public static IPropertyMapper<IWebView, WebViewHandler> Mapper = new PropertyMapper<IWebView, WebViewHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IWebView.Source)] = MapSource,
    };

    public static CommandMapper<IWebView, WebViewHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
        [nameof(IWebView.GoBack)] = MapGoBack,
        [nameof(IWebView.GoForward)] = MapGoForward,
        [nameof(IWebView.Reload)] = MapReload,
    };

    public WebViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public WebViewHandler(IPropertyMapper? mapper = null, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaWebView CreatePlatformView()
    {
        return new SkiaWebView();
    }

    protected override void ConnectHandler(SkiaWebView platformView)
    {
        base.ConnectHandler(platformView);

        platformView.Navigating += OnNavigating;
        platformView.Navigated += OnNavigated;
    }

    protected override void DisconnectHandler(SkiaWebView platformView)
    {
        platformView.Navigating -= OnNavigating;
        platformView.Navigated -= OnNavigated;

        base.DisconnectHandler(platformView);
    }

    private void OnNavigating(object? sender, Microsoft.Maui.Platform.WebNavigatingEventArgs e)
    {
        IWebView virtualView = VirtualView;
        IWebViewController? controller = virtualView as IWebViewController;
        if (controller != null)
        {
            var args = new Microsoft.Maui.Controls.WebNavigatingEventArgs(
                WebNavigationEvent.NewPage,
                null,
                e.Url);
            controller.SendNavigating(args);
        }
    }

    private void OnNavigated(object? sender, Microsoft.Maui.Platform.WebNavigatedEventArgs e)
    {
        IWebView virtualView = VirtualView;
        IWebViewController? controller = virtualView as IWebViewController;
        if (controller != null)
        {
            WebNavigationResult result = e.Success ? WebNavigationResult.Success : WebNavigationResult.Failure;
            var args = new Microsoft.Maui.Controls.WebNavigatedEventArgs(
                WebNavigationEvent.NewPage,
                null,
                e.Url,
                result);
            controller.SendNavigated(args);
        }
    }

    public static void MapSource(WebViewHandler handler, IWebView webView)
    {
        Console.WriteLine("[WebViewHandler] MapSource called");
        if (handler.PlatformView == null)
        {
            Console.WriteLine("[WebViewHandler] PlatformView is null!");
            return;
        }

        var source = webView.Source;
        Console.WriteLine($"[WebViewHandler] Source type: {source?.GetType().Name ?? "null"}");

        if (source is UrlWebViewSource urlSource)
        {
            Console.WriteLine($"[WebViewHandler] Loading URL: {urlSource.Url}");
            handler.PlatformView.Source = urlSource.Url ?? "";
        }
        else if (source is HtmlWebViewSource htmlSource)
        {
            Console.WriteLine($"[WebViewHandler] Loading HTML ({htmlSource.Html?.Length ?? 0} chars)");
            Console.WriteLine($"[WebViewHandler] HTML preview: {htmlSource.Html?.Substring(0, Math.Min(100, htmlSource.Html?.Length ?? 0))}...");
            handler.PlatformView.Html = htmlSource.Html ?? "";
        }
        else
        {
            Console.WriteLine("[WebViewHandler] Unknown source type or null");
        }
    }

    public static void MapGoBack(WebViewHandler handler, IWebView webView, object? args)
    {
        handler.PlatformView?.GoBack();
    }

    public static void MapGoForward(WebViewHandler handler, IWebView webView, object? args)
    {
        handler.PlatformView?.GoForward();
    }

    public static void MapReload(WebViewHandler handler, IWebView webView, object? args)
    {
        handler.PlatformView?.Reload();
    }
}
