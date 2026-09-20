// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Views;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI WebView handler over <see cref="WpeWebView"/> (WPE WebKit composited
/// in the Skia tree). Selected by <see cref="WebViewBackend"/> when WPE is
/// installed; the GTK-hosted <see cref="GtkWebViewHandler"/> remains the
/// fallback. Unlike the GTK path this works in native Wayland/X11 mode, maps
/// UserAgent, and returns real results from EvaluateJavaScriptAsync.
/// </summary>
public class WpeWebViewHandler : ViewHandler<IWebView, WpeWebView>
{
    public static IPropertyMapper<IWebView, WpeWebViewHandler> Mapper = new PropertyMapper<IWebView, WpeWebViewHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IWebView.Source)] = MapSource,
        [nameof(IWebView.UserAgent)] = MapUserAgent,
    };

    public static CommandMapper<IWebView, WpeWebViewHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
        [nameof(IWebView.GoBack)] = (h, v, a) => h.PlatformView.GoBack(),
        [nameof(IWebView.GoForward)] = (h, v, a) => h.PlatformView.GoForward(),
        [nameof(IWebView.Reload)] = (h, v, a) => h.PlatformView.Reload(),
        [nameof(IWebView.Eval)] = MapEval,
        [nameof(IWebView.EvaluateJavaScriptAsync)] = MapEvaluateJavaScriptAsync,
    };

    public WpeWebViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public WpeWebViewHandler(IPropertyMapper? mapper = null, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override WpeWebView CreatePlatformView() => new WpeWebView();

    protected override void ConnectHandler(WpeWebView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.NavigationStarted += OnNavigationStarted;
        platformView.NavigationCompleted += OnNavigationCompleted;
        platformView.NavigationDecision += OnNavigationDecision;
    }

    protected override void DisconnectHandler(WpeWebView platformView)
    {
        platformView.NavigationStarted -= OnNavigationStarted;
        platformView.NavigationCompleted -= OnNavigationCompleted;
        platformView.NavigationDecision -= OnNavigationDecision;
        platformView.Dispose();
        base.DisconnectHandler(platformView);
    }

    private void OnNavigationDecision(object? sender, WpeWebView.NavigationDecisionEventArgs e)
    {
        // MAUI's Navigating is the cancellable one; raise it here so a handler
        // can veto before WebKit follows the link.
        if (VirtualView is IWebViewController controller)
        {
            var args = new Microsoft.Maui.Controls.WebNavigatingEventArgs(WebNavigationEvent.NewPage, null, e.Url);
            controller.SendNavigating(args);
            e.Cancel = args.Cancel;
        }
    }

    private void OnNavigationStarted(object? sender, string uri)
    {
        DiagnosticLog.Debug("WpeWebViewHandler", $"Navigation started: {uri}");
    }

    private void OnNavigationCompleted(object? sender, (string Url, bool Success) e)
    {
        try
        {
            if (VirtualView is IWebViewController controller)
            {
                var result = e.Success ? WebNavigationResult.Success : WebNavigationResult.Failure;
                controller.SendNavigated(new Microsoft.Maui.Controls.WebNavigatedEventArgs(WebNavigationEvent.NewPage, null, e.Url, result));
                controller.CanGoBack = PlatformView.CanGoBack;
                controller.CanGoForward = PlatformView.CanGoForward;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WpeWebViewHandler", "SendNavigated failed", ex);
        }
    }

    public static void MapSource(WpeWebViewHandler handler, IWebView webView)
    {
        switch (webView.Source)
        {
            case UrlWebViewSource url when !string.IsNullOrEmpty(url.Url):
                handler.PlatformView.Navigate(url.Url);
                break;
            case HtmlWebViewSource html when !string.IsNullOrEmpty(html.Html):
                handler.PlatformView.LoadHtml(html.Html, html.BaseUrl);
                break;
        }
    }

    public static void MapUserAgent(WpeWebViewHandler handler, IWebView webView)
    {
        if (!string.IsNullOrEmpty(webView.UserAgent))
            handler.PlatformView.UserAgent = webView.UserAgent;
    }

    public static void MapEval(WpeWebViewHandler handler, IWebView webView, object? args)
    {
        if (args is string script)
            handler.PlatformView.Eval(script);
    }

    public static void MapEvaluateJavaScriptAsync(WpeWebViewHandler handler, IWebView webView, object? args)
    {
        if (args is EvaluateJavaScriptAsyncRequest request)
        {
            handler.PlatformView.EvaluateJavaScriptAsync(request.Script).ContinueWith(t =>
            {
                if (t.IsFaulted) request.SetException(t.Exception!.GetBaseException());
                else request.SetResult(t.Result);
            }, TaskScheduler.Default);
        }
        else if (args is string script)
        {
            handler.PlatformView.Eval(script);
        }
    }
}

/// <summary>
/// Chooses the WebView implementation: WPE WebKit (composited, native-mode
/// capable) when libWPEWebKit-2.0 loads, otherwise the GTK-hosted WebKitGTK
/// view. <c>OPENMAUI_WEBVIEW=wpe|webkitgtk|auto</c> overrides.
/// </summary>
public static class WebViewBackend
{
    public const string EnvironmentVariable = "OPENMAUI_WEBVIEW";

    public enum Kind { Wpe, WebKitGtk }

    public static Kind Resolve()
    {
        var env = Environment.GetEnvironmentVariable(EnvironmentVariable)?.Trim().ToLowerInvariant();
        switch (env)
        {
            case "wpe":
                if (WpeWebView.IsSupported) return Kind.Wpe;
                DiagnosticLog.Error("WebViewBackend", "OPENMAUI_WEBVIEW=wpe but libWPEWebKit-2.0 is not installed; using WebKitGTK");
                return Kind.WebKitGtk;
            case "webkitgtk":
            case "gtk":
                return Kind.WebKitGtk;
            case null:
            case "":
            case "auto":
                break;
            default:
                DiagnosticLog.Warn("WebViewBackend", $"Unknown {EnvironmentVariable}='{env}' (expected wpe|webkitgtk|auto); ignoring");
                break;
        }
        return WpeWebView.IsSupported ? Kind.Wpe : Kind.WebKitGtk;
    }

    /// <summary>Diagnostic name of the backend that will be used.</summary>
    public static string Name => Resolve() == Kind.Wpe ? "wpe" : "webkitgtk";
}
