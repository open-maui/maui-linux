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
public partial class WebViewHandler : ViewHandler<IWebView, SkiaWebView>
{
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

    public static void MapUserAgent(WebViewHandler handler, IWebView webView)
    {
        if (handler.PlatformView != null && !string.IsNullOrEmpty(webView.UserAgent))
        {
            handler.PlatformView.UserAgent = webView.UserAgent;
        }
    }

    public static void MapEval(WebViewHandler handler, IWebView webView, object? args)
    {
        if (args is string script)
        {
            handler.PlatformView?.Eval(script);
        }
    }

    public static void MapEvaluateJavaScriptAsync(WebViewHandler handler, IWebView webView, object? args)
    {
        // Handle EvaluateJavaScriptAsyncRequest from Microsoft.Maui.Platform namespace
        if (args is EvaluateJavaScriptAsyncRequest request)
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

/// <summary>
/// Request object for async JavaScript evaluation (matches Microsoft.Maui.Platform.EvaluateJavaScriptAsyncRequest).
/// </summary>
public class EvaluateJavaScriptAsyncRequest
{
    public string Script { get; }
    private readonly System.Threading.Tasks.TaskCompletionSource<string?> _tcs = new();

    public EvaluateJavaScriptAsyncRequest(string script)
    {
        Script = script;
    }

    public System.Threading.Tasks.Task<string?> Task => _tcs.Task;

    public void SetResult(string? result)
    {
        _tcs.TrySetResult(result);
    }
}
