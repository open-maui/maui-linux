// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Linux handler for WebView control using WebKitGTK.
/// </summary>
public partial class WebViewHandler : ViewHandler<IWebView, LinuxWebView>
{
    /// <summary>
    /// Property mapper for WebView properties.
    /// </summary>
    public static IPropertyMapper<IWebView, WebViewHandler> Mapper = new PropertyMapper<IWebView, WebViewHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IWebView.Source)] = MapSource,
        [nameof(IWebView.UserAgent)] = MapUserAgent,
    };

    /// <summary>
    /// Command mapper for WebView commands.
    /// </summary>
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

    public WebViewHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper, CommandMapper)
    {
    }

    public WebViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override LinuxWebView CreatePlatformView()
    {
        DiagnosticLog.Debug("WebViewHandler", "Creating LinuxWebView");
        return new LinuxWebView();
    }

    protected override void ConnectHandler(LinuxWebView platformView)
    {
        base.ConnectHandler(platformView);

        platformView.Navigating += OnNavigating;
        platformView.Navigated += OnNavigated;

        // Map initial properties
        if (VirtualView != null)
        {
            MapSource(this, VirtualView);
            MapUserAgent(this, VirtualView);
        }

        DiagnosticLog.Debug("WebViewHandler", "Handler connected");
    }

    protected override void DisconnectHandler(LinuxWebView platformView)
    {
        platformView.Navigating -= OnNavigating;
        platformView.Navigated -= OnNavigated;

        base.DisconnectHandler(platformView);
        DiagnosticLog.Debug("WebViewHandler", "Handler disconnected");
    }

    private void OnNavigating(object? sender, WebViewNavigatingEventArgs e)
    {
        if (VirtualView == null)
            return;

        // Notify the virtual view about navigation starting
        VirtualView.Navigating(WebNavigationEvent.NewPage, e.Url);
    }

    private void OnNavigated(object? sender, WebViewNavigatedEventArgs e)
    {
        if (VirtualView == null)
            return;

        // Notify the virtual view about navigation completed
        var result = e.Success ? WebNavigationResult.Success : WebNavigationResult.Failure;
        VirtualView.Navigated(WebNavigationEvent.NewPage, e.Url, result);
    }

    #region Property Mappers

    public static void MapSource(WebViewHandler handler, IWebView webView)
    {
        var source = webView.Source;
        if (source == null)
            return;

        DiagnosticLog.Debug("WebViewHandler", $"MapSource: {source.GetType().Name}");

        if (source is IUrlWebViewSource urlSource && !string.IsNullOrEmpty(urlSource.Url))
        {
            handler.PlatformView?.LoadUrl(urlSource.Url);
        }
        else if (source is IHtmlWebViewSource htmlSource && !string.IsNullOrEmpty(htmlSource.Html))
        {
            handler.PlatformView?.LoadHtml(htmlSource.Html, htmlSource.BaseUrl);
        }
    }

    public static void MapUserAgent(WebViewHandler handler, IWebView webView)
    {
        if (handler.PlatformView != null && !string.IsNullOrEmpty(webView.UserAgent))
        {
            handler.PlatformView.UserAgent = webView.UserAgent;
            DiagnosticLog.Debug("WebViewHandler", $"MapUserAgent: {webView.UserAgent}");
        }
    }

    #endregion

    #region Command Mappers

    public static void MapGoBack(WebViewHandler handler, IWebView webView, object? args)
    {
        if (handler.PlatformView?.CanGoBack == true)
        {
            handler.PlatformView.GoBack();
            DiagnosticLog.Debug("WebViewHandler", "GoBack");
        }
    }

    public static void MapGoForward(WebViewHandler handler, IWebView webView, object? args)
    {
        if (handler.PlatformView?.CanGoForward == true)
        {
            handler.PlatformView.GoForward();
            DiagnosticLog.Debug("WebViewHandler", "GoForward");
        }
    }

    public static void MapReload(WebViewHandler handler, IWebView webView, object? args)
    {
        handler.PlatformView?.Reload();
        DiagnosticLog.Debug("WebViewHandler", "Reload");
    }

    public static void MapEval(WebViewHandler handler, IWebView webView, object? args)
    {
        if (args is string script)
        {
            handler.PlatformView?.Eval(script);
            DiagnosticLog.Debug("WebViewHandler", $"Eval: {script.Substring(0, Math.Min(50, script.Length))}...");
        }
    }

    public static void MapEvaluateJavaScriptAsync(WebViewHandler handler, IWebView webView, object? args)
    {
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
            DiagnosticLog.Debug("WebViewHandler", $"EvaluateJavaScriptAsync: {request.Script.Substring(0, Math.Min(50, request.Script.Length))}...");
        }
    }

    #endregion
}

// NOTE: EvaluateJavaScriptAsync commands use Microsoft.Maui.EvaluateJavaScriptAsyncRequest
// from the MAUI framework. Do NOT define a local EvaluateJavaScriptAsyncRequest class here.
