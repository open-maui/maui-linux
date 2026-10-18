// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for WebView control on Linux using WebKitGTK.
/// </summary>
public partial class WebViewHandler : LinuxViewHandler<IWebView, SkiaWebView>, IWebViewHandler
{
    IWebView IWebViewHandler.VirtualView => VirtualView;
    object IWebViewHandler.PlatformView => PlatformView;

    public static IPropertyMapper<IWebView, WebViewHandler> Mapper = new PropertyMapper<IWebView, WebViewHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IWebView.Source)] = MapSource,
        [nameof(IWebView.UserAgent)] = MapUserAgent,
    };

    public static CommandMapper<IWebView, WebViewHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
        [nameof(IWebView.GoBack)] = MapGoBack,
        [nameof(IWebView.GoForward)] = MapGoForward,
        [nameof(IWebView.Reload)] = MapReload,
        [nameof(IWebView.Eval)] = MapEval,
        [nameof(IWebView.EvaluateJavaScriptAsync)] = MapEvaluateJavaScriptAsync,
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

    private void OnNavigating(object? sender, SkiaWebNavigatingEventArgs e)
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

    private void OnNavigated(object? sender, SkiaWebNavigatedEventArgs e)
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

    /// <summary>MAUI's <c>MapSource</c> signature; maps the property on a Linux <see cref="WebViewHandler"/>.</summary>
    public static void MapSource(IWebViewHandler handler, IWebView webView)
    {
        if (handler is WebViewHandler linuxHandler)
            MapSourceCore(linuxHandler, webView);
    }

    private static void MapSourceCore(WebViewHandler handler, IWebView webView)
    {
        DiagnosticLog.Debug("WebViewHandler", "MapSource called");
        if (handler.PlatformView == null)
        {
            DiagnosticLog.Warn("WebViewHandler", "PlatformView is null!");
            return;
        }

        var source = webView.Source;
        DiagnosticLog.Debug("WebViewHandler", $"Source type: {source?.GetType().Name ?? "null"}");

        if (source is UrlWebViewSource urlSource)
        {
            DiagnosticLog.Debug("WebViewHandler", $"Loading URL: {urlSource.Url}");
            handler.PlatformView.Source = urlSource.Url ?? "";
        }
        else if (source is HtmlWebViewSource htmlSource)
        {
            DiagnosticLog.Debug("WebViewHandler", $"Loading HTML ({htmlSource.Html?.Length ?? 0} chars)");
            DiagnosticLog.Debug("WebViewHandler", $"HTML preview: {htmlSource.Html?.Substring(0, Math.Min(100, htmlSource.Html?.Length ?? 0))}...");
            handler.PlatformView.Html = htmlSource.Html ?? "";
        }
        else
        {
            DiagnosticLog.Debug("WebViewHandler", "Unknown source type or null");
        }
    }

    /// <summary>MAUI's <c>MapGoBack</c> signature; runs the command on a Linux <see cref="WebViewHandler"/>.</summary>
    public static void MapGoBack(IWebViewHandler handler, IWebView webView, object? arg)
    {
        if (handler is WebViewHandler linuxHandler)
            MapGoBackCore(linuxHandler, webView, arg);
    }

    private static void MapGoBackCore(WebViewHandler handler, IWebView webView, object? args)
    {
        handler.PlatformView?.GoBack();
    }

    /// <summary>MAUI's <c>MapGoForward</c> signature; runs the command on a Linux <see cref="WebViewHandler"/>.</summary>
    public static void MapGoForward(IWebViewHandler handler, IWebView webView, object? arg)
    {
        if (handler is WebViewHandler linuxHandler)
            MapGoForwardCore(linuxHandler, webView, arg);
    }

    private static void MapGoForwardCore(WebViewHandler handler, IWebView webView, object? args)
    {
        handler.PlatformView?.GoForward();
    }

    /// <summary>MAUI's <c>MapReload</c> signature; runs the command on a Linux <see cref="WebViewHandler"/>.</summary>
    public static void MapReload(IWebViewHandler handler, IWebView webView, object? arg)
    {
        if (handler is WebViewHandler linuxHandler)
            MapReloadCore(linuxHandler, webView, arg);
    }

    private static void MapReloadCore(WebViewHandler handler, IWebView webView, object? args)
    {
        handler.PlatformView?.Reload();
    }

    /// <summary>MAUI's <c>MapUserAgent</c> signature; maps the property on a Linux <see cref="WebViewHandler"/>.</summary>
    public static void MapUserAgent(IWebViewHandler handler, IWebView webView)
    {
        if (handler is WebViewHandler linuxHandler)
            MapUserAgentCore(linuxHandler, webView);
    }

    private static void MapUserAgentCore(WebViewHandler handler, IWebView webView)
    {
        if (handler.PlatformView != null && !string.IsNullOrEmpty(webView.UserAgent))
        {
            handler.PlatformView.UserAgent = webView.UserAgent;
        }
    }

    /// <summary>MAUI's <c>MapEval</c> signature; runs the command on a Linux <see cref="WebViewHandler"/>.</summary>
    public static void MapEval(IWebViewHandler handler, IWebView webView, object? arg)
    {
        if (handler is WebViewHandler linuxHandler)
            MapEvalCore(linuxHandler, webView, arg);
    }

    private static void MapEvalCore(WebViewHandler handler, IWebView webView, object? args)
    {
        if (args is string script)
        {
            handler.PlatformView?.Eval(script);
        }
    }

    /// <summary>MAUI's <c>MapEvaluateJavaScriptAsync</c> signature; runs the command on a Linux <see cref="WebViewHandler"/>.</summary>
    public static void MapEvaluateJavaScriptAsync(IWebViewHandler handler, IWebView webView, object? arg)
    {
        if (handler is WebViewHandler linuxHandler)
            MapEvaluateJavaScriptAsyncCore(linuxHandler, webView, arg);
    }

    private static void MapEvaluateJavaScriptAsyncCore(WebViewHandler handler, IWebView webView, object? args)
    {
        // Use MAUI's EvaluateJavaScriptAsyncRequest type (Microsoft.Maui namespace)
        if (args is Microsoft.Maui.EvaluateJavaScriptAsyncRequest request)
        {
            var result = handler.PlatformView?.EvaluateJavaScriptAsync(request.Script);
            if (result != null)
            {
                result.ContinueWith(t =>
                {
                    request.SetResult(t.Result);
                });
            }
            else
            {
                request.SetResult(null);
            }
        }
        else if (args is string script)
        {
            // Direct script string
            handler.PlatformView?.EvaluateJavaScriptAsync(script);
        }
    }
}

// NOTE: EvaluateJavaScriptAsync commands use Microsoft.Maui.EvaluateJavaScriptAsyncRequest
// from the MAUI framework. Do NOT define a local EvaluateJavaScriptAsyncRequest class here.
