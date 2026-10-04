// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Views;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI's WebView on Linux, on either backend (WPE WebKit when installed, else WebKitGTK; see
/// <see cref="WebViewBackend"/>). A library's own WebView handler derives from it: it gets the
/// whole <see cref="ILinuxWebView"/> through <see cref="Browser"/> and overrides the <c>On…</c>
/// hooks (calling base keeps MAUI's events), as MarketAlly.ViewEngine's Linux head does.
/// </summary>
public class LinuxWebViewHandler : LinuxViewHandler<IWebView, SkiaView>
{
    public static IPropertyMapper<IWebView, LinuxWebViewHandler> Mapper = new PropertyMapper<IWebView, LinuxWebViewHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IWebView.Source)] = MapSource,
        [nameof(IWebView.UserAgent)] = MapUserAgent,
    };

    public static CommandMapper<IWebView, LinuxWebViewHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
        [nameof(IWebView.GoBack)] = MapGoBack,
        [nameof(IWebView.GoForward)] = MapGoForward,
        [nameof(IWebView.Reload)] = MapReload,
        [nameof(IWebView.Eval)] = MapEval,
        [nameof(IWebView.EvaluateJavaScriptAsync)] = MapEvaluateJavaScriptAsync,
    };

    private GtkWebViewPlatformView? _gtkView;
    private GtkWebViewHostLink? _gtkHostLink;

    public LinuxWebViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public LinuxWebViewHandler(IPropertyMapper? mapper = null, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    /// <summary>The view's browser, once the handler has its platform view.</summary>
    public ILinuxWebView? Browser => PlatformView as ILinuxWebView;

    protected override SkiaView CreatePlatformView()
    {
        if (WebViewBackend.Resolve() == WebViewBackend.Kind.Wpe)
            return new WpeWebView();
        _gtkView = new GtkWebViewPlatformView();
        _gtkHostLink = new GtkWebViewHostLink(_gtkView);
        return new GtkWebViewProxy(_gtkView, _gtkHostLink);
    }

    protected override void ConnectHandler(SkiaView platformView)
    {
        base.ConnectHandler(platformView);
        if (platformView is ILinuxWebView browser)
        {
            browser.NavigationStarting += OnBrowserNavigationStarting;
            browser.NavigationFinished += OnBrowserNavigationFinished;
            browser.UrlChanged += OnBrowserUrlChanged;
            browser.TitleChanged += OnBrowserTitleChanged;
            browser.ResponseReceived += OnBrowserResponseReceived;
            browser.DownloadStarting += OnBrowserDownloadStarting;
            browser.DownloadFinished += OnBrowserDownloadFinished;
        }
        if (_gtkView != null)
            _gtkView.ScriptDialogRequested += OnGtkScriptDialogRequested;
    }

    protected override void DisconnectHandler(SkiaView platformView)
    {
        if (platformView is ILinuxWebView browser)
        {
            browser.NavigationStarting -= OnBrowserNavigationStarting;
            browser.NavigationFinished -= OnBrowserNavigationFinished;
            browser.UrlChanged -= OnBrowserUrlChanged;
            browser.TitleChanged -= OnBrowserTitleChanged;
            browser.ResponseReceived -= OnBrowserResponseReceived;
            browser.DownloadStarting -= OnBrowserDownloadStarting;
            browser.DownloadFinished -= OnBrowserDownloadFinished;
        }
        if (_gtkView != null)
        {
            _gtkView.ScriptDialogRequested -= OnGtkScriptDialogRequested;
            (platformView as GtkWebViewProxy)?.DetachBrowser();
            _gtkHostLink?.Unregister();
            _gtkView.Dispose();
            _gtkView = null;
            _gtkHostLink = null;
        }
        else
        {
            platformView.Dispose();
        }
        base.DisconnectHandler(platformView);
    }

    private void OnBrowserNavigationStarting(object? sender, LinuxWebNavigationStartingEventArgs e) => Guard(() => OnNavigationStarting(e), "NavigationStarting");
    private void OnBrowserNavigationFinished(object? sender, LinuxWebNavigationFinishedEventArgs e) => Guard(() => OnNavigationFinished(e), "NavigationFinished");
    private void OnBrowserUrlChanged(object? sender, string url) => Guard(() => OnUrlChanged(url), "UrlChanged");
    private void OnBrowserTitleChanged(object? sender, string title) => Guard(() => OnTitleChanged(title), "TitleChanged");
    private void OnBrowserResponseReceived(object? sender, LinuxWebResponseEventArgs e) => Guard(() => OnResponseReceived(e), "ResponseReceived");
    private void OnBrowserDownloadStarting(object? sender, LinuxWebDownloadEventArgs e) => Guard(() => OnDownloadStarting(e), "DownloadStarting");
    private void OnBrowserDownloadFinished(object? sender, LinuxWebDownloadFinishedEventArgs e) => Guard(() => OnDownloadFinished(e), "DownloadFinished");

    private static void Guard(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("LinuxWebViewHandler", $"{what} failed", ex);
        }
    }

    /// <summary>
    /// A navigation (or new window) is about to start: raises MAUI's cancellable Navigating for a
    /// navigation in this view. Set <see cref="LinuxWebNavigationStartingEventArgs.Cancel"/> to stop it.
    /// </summary>
    protected virtual void OnNavigationStarting(LinuxWebNavigationStartingEventArgs e)
    {
        if (e.IsNewWindow || VirtualView is not IWebViewController controller)
            return;
        var args = new Microsoft.Maui.Controls.WebNavigatingEventArgs(ToMauiEvent(e.NavigationType), new UrlWebViewSource { Url = e.Url }, e.Url);
        controller.SendNavigating(args);
        if (args.Cancel)
            e.Cancel = true;
    }

    /// <summary>A main-frame load finished or failed: raises MAUI's Navigated and updates CanGoBack and CanGoForward.</summary>
    protected virtual void OnNavigationFinished(LinuxWebNavigationFinishedEventArgs e)
    {
        if (VirtualView is not IWebViewController controller)
            return;
        var result = e.Success ? WebNavigationResult.Success : WebNavigationResult.Failure;
        controller.SendNavigated(new Microsoft.Maui.Controls.WebNavigatedEventArgs(WebNavigationEvent.NewPage, new UrlWebViewSource { Url = e.Url }, e.Url, result));
        UpdateHistoryState();
    }

    /// <summary>The page's URL changed (also within a page).</summary>
    protected virtual void OnUrlChanged(string url) => UpdateHistoryState();

    /// <summary>The page's title changed.</summary>
    protected virtual void OnTitleChanged(string title)
    {
    }

    /// <summary>A response arrived; set <see cref="LinuxWebResponseEventArgs.Action"/> to show, ignore or download it.</summary>
    protected virtual void OnResponseReceived(LinuxWebResponseEventArgs e)
    {
    }

    /// <summary>
    /// A download is starting; cancel it or choose its destination. By default the user is asked
    /// where to save it (<see cref="LinuxWebDownloadEventArgs.AskWhereToSave"/>), as a WebView
    /// offers on the other platforms.
    /// </summary>
    protected virtual void OnDownloadStarting(LinuxWebDownloadEventArgs e)
    {
        if (string.IsNullOrEmpty(e.DestinationPath))
            e.AskWhereToSave = true;
    }

    /// <summary>A download finished or failed.</summary>
    protected virtual void OnDownloadFinished(LinuxWebDownloadFinishedEventArgs e)
    {
    }

    private void UpdateHistoryState()
    {
        if (VirtualView is IWebViewController controller && Browser is { } browser)
        {
            controller.CanGoBack = browser.CanGoBack;
            controller.CanGoForward = browser.CanGoForward;
        }
    }

    private static WebNavigationEvent ToMauiEvent(LinuxWebNavigationType type) => type switch
    {
        LinuxWebNavigationType.BackForward => WebNavigationEvent.Back,
        LinuxWebNavigationType.Reload => WebNavigationEvent.Refresh,
        _ => WebNavigationEvent.NewPage,
    };

    private void OnGtkScriptDialogRequested(object? sender, (ScriptDialogType Type, string Message, Action<bool> Callback) e) =>
        ShowGtkScriptDialog(e);

    /// <summary>Answers a WebKitGTK page's alert, confirm or prompt with OpenMaui's dialog.</summary>
    internal static async void ShowGtkScriptDialog((ScriptDialogType Type, string Message, Action<bool> Callback) e)
    {
        string title = e.Type switch
        {
            ScriptDialogType.Alert => "Alert",
            ScriptDialogType.Confirm => "Confirm",
            ScriptDialogType.Prompt => "Prompt",
            _ => "Message",
        };
        try
        {
            bool result = await LinuxDialogService.ShowAlertAsync(title, e.Message, "OK", e.Type == ScriptDialogType.Alert ? null : "Cancel");
            e.Callback(result);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("LinuxWebViewHandler", "Showing a script dialog failed", ex);
            e.Callback(false);
        }
    }

    public static void MapSource(LinuxWebViewHandler handler, IWebView webView)
    {
        if (handler.Browser is not { } browser)
            return;
        switch (webView.Source)
        {
            case UrlWebViewSource url when !string.IsNullOrEmpty(url.Url):
                browser.Navigate(url.Url);
                break;
            case HtmlWebViewSource html when !string.IsNullOrEmpty(html.Html):
                browser.LoadHtml(html.Html, html.BaseUrl);
                break;
        }
    }

    public static void MapUserAgent(LinuxWebViewHandler handler, IWebView webView)
    {
        if (handler.Browser is { } browser && !string.IsNullOrEmpty(webView.UserAgent))
            browser.UserAgent = webView.UserAgent;
    }

    public static void MapGoBack(LinuxWebViewHandler handler, IWebView webView, object? args) => handler.Browser?.GoBack();
    public static void MapGoForward(LinuxWebViewHandler handler, IWebView webView, object? args) => handler.Browser?.GoForward();
    public static void MapReload(LinuxWebViewHandler handler, IWebView webView, object? args) => handler.Browser?.Reload();

    public static void MapEval(LinuxWebViewHandler handler, IWebView webView, object? args)
    {
        if (args is string script && handler.Browser is { } browser)
            _ = browser.EvaluateJavaScriptAsync(script);
    }

    public static void MapEvaluateJavaScriptAsync(LinuxWebViewHandler handler, IWebView webView, object? args)
    {
        if (handler.Browser is not { } browser)
        {
            (args as EvaluateJavaScriptAsyncRequest)?.SetResult(null);
            return;
        }
        if (args is EvaluateJavaScriptAsyncRequest request)
        {
            browser.EvaluateJavaScriptAsync(request.Script).ContinueWith(t =>
            {
                if (t.IsFaulted) request.SetException(t.Exception!.GetBaseException());
                else request.SetResult(t.Result);
            }, TaskScheduler.Default);
        }
        else if (args is string script)
        {
            _ = browser.EvaluateJavaScriptAsync(script);
        }
    }
}
