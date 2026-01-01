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

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        // Forward to virtual view if needed
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        // Forward to virtual view if needed
    }

    public static void MapSource(WebViewHandler handler, IWebView webView)
    {
        if (handler.PlatformView == null) return;

        var source = webView.Source;
        if (source is UrlWebViewSource urlSource)
        {
            handler.PlatformView.Source = urlSource.Url ?? "";
        }
        else if (source is HtmlWebViewSource htmlSource)
        {
            handler.PlatformView.Html = htmlSource.Html ?? "";
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
