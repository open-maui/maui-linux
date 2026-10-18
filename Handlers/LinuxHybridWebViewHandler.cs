// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Views;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI's HybridWebView on Linux, on either WebKit backend (WPE WebKit when installed, else
/// WebKitGTK; see <see cref="WebViewBackend"/>). It works as MAUI's handler does on iOS and Mac
/// Catalyst, which are WebKit too: the web app is served from <c>app://0.0.0.1/</c> out of the
/// app's <c>Resources/Raw/{HybridRoot}</c> (<c>DefaultFile</c> for the root), with MAUI's own
/// <c>_framework/hybridwebview.js</c>; <c>SendRawMessage</c>, <c>RawMessageReceived</c>,
/// <c>InvokeJavaScriptAsync</c>, <c>window.HybridWebView.InvokeDotNet</c>,
/// <c>EvaluateJavaScriptAsync</c>, <c>WebResourceRequested</c> (for requests to the app origin,
/// and, as on Windows, for http and https requests when the app handles the event by the time the
/// handler creates the view; see <see cref="HybridWebViewHttpInterception"/>), <c>WebViewInitializing</c> and
/// <c>WebViewInitialized</c> behave as there. <c>AddHybridWebViewDeveloperTools()</c> turns on
/// WebKit's inspector. <see cref="Browser"/> is the view's <see cref="ILinuxWebView"/>.
/// </summary>
public class LinuxHybridWebViewHandler : LinuxViewHandler<IHybridWebView, SkiaView>, IHybridWebViewHandler
{
    public static IPropertyMapper<IHybridWebView, LinuxHybridWebViewHandler> Mapper = new PropertyMapper<IHybridWebView, LinuxHybridWebViewHandler>(ViewHandler.ViewMapper);

    public static CommandMapper<IHybridWebView, LinuxHybridWebViewHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
        [nameof(IHybridWebView.EvaluateJavaScriptAsync)] = MapEvaluateJavaScriptAsync,
        [nameof(IHybridWebView.InvokeJavaScriptAsync)] = MapInvokeJavaScriptAsync,
        [nameof(IHybridWebView.SendRawMessage)] = MapSendRawMessage,
    };

    private GtkWebViewPlatformView? _gtkView;
    private GtkWebViewHostLink? _gtkHostLink;
    private HybridWebViewBridge? _bridge;
    private IDisposable? _schemeRegistration;
    private IDisposable? _messageRegistration;
    private HybridWebViewHttpInterception? _httpInterception;

    public LinuxHybridWebViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public LinuxHybridWebViewHandler(IPropertyMapper? mapper = null, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    /// <summary>The app's origin: <c>app://0.0.0.1/</c>, as on iOS and Mac Catalyst.</summary>
    public static Uri AppOriginUri => HybridWebViewBridge.AppOriginUri;

    /// <summary>The view's browser, once the handler has its platform view.</summary>
    public ILinuxWebView? Browser => PlatformView as ILinuxWebView;

    IHybridWebView IHybridWebViewHandler.VirtualView => VirtualView;

    object IHybridWebViewHandler.PlatformView => PlatformView;

    protected override SkiaView CreatePlatformView()
    {
        RaiseInitializationStarted();
        SkiaView view;
        if (WebViewBackend.Resolve() == WebViewBackend.Kind.Wpe)
        {
            // An app that handles WebResourceRequested gets http and https requests too: the
            // view then has a network of its own, behind the interception proxy.
            _httpInterception = HybridWebViewHttpInterception.TryCreate(VirtualView,
                WebKitContentApi.For(WpeNative.LibWpeWebKit, WebKitContentApi.Flavor.Modern));
            try
            {
                view = new WpeWebView(_httpInterception?.Network ?? IntPtr.Zero);
            }
            catch
            {
                _httpInterception?.Dispose(viewAlive: false);
                _httpInterception = null;
                throw;
            }
        }
        else
        {
            if (WebKitNative.Initialize() && WebKitNative.LoadedLibraryName is { } library)
                _httpInterception = HybridWebViewHttpInterception.TryCreate(VirtualView,
                    WebKitContentApi.For(library, WebKitContentApi.Flavor.WebKitGtk41));
            try
            {
                _gtkView = new GtkWebViewPlatformView(_httpInterception?.Network ?? IntPtr.Zero);
            }
            catch
            {
                _httpInterception?.Dispose(viewAlive: false);
                _httpInterception = null;
                throw;
            }
            _gtkHostLink = new GtkWebViewHostLink(_gtkView);
            view = new GtkWebViewProxy(_gtkView, _gtkHostLink);
        }
        if (view is ILinuxWebView browser && browser.NativeWebView != IntPtr.Zero && IsDeveloperToolsEnabled())
            browser.Content.SetDeveloperExtrasEnabled(browser.NativeWebView, true);
        return view;
    }

    protected override void ConnectHandler(SkiaView platformView)
    {
        base.ConnectHandler(platformView);
        HybridWebViewResourceInterception.Install();
        if (_gtkView != null)
            _gtkView.ScriptDialogRequested += OnGtkScriptDialogRequested;

        if (platformView is not ILinuxWebView browser || browser.NativeWebView == IntPtr.Zero)
        {
            DiagnosticLog.Error("HybridWebView", "No WebKit view: the HybridWebView stays empty");
            return;
        }

        var bridge = new HybridWebViewBridge(VirtualView, new BrowserScriptHost(browser));
        _bridge = bridge;
        try
        {
            var native = browser.NativeWebView;
            _schemeRegistration = browser.Content.RegisterUriSchemeHandler(native, HybridWebViewBridge.AppHostScheme, request => Serve(bridge, request));
            _messageRegistration = browser.Content.AddScriptMessageHandler(native, HybridWebViewBridge.ScriptMessageHandlerName, message => OnScriptMessage(bridge, message));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("HybridWebView", "Connecting the app scheme and message bridge failed", ex);
        }

        if (_httpInterception != null)
        {
            try
            {
                _httpInterception.Attach(browser);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("HybridWebView", "Starting http and https interception failed: WebResourceRequested is raised for the app origin only", ex);
                _httpInterception.Dispose();
                _httpInterception = null;
            }
        }

        RaiseInitializationCompleted();
        browser.Navigate(HybridWebViewBridge.AppOrigin);
    }

    protected override void DisconnectHandler(SkiaView platformView)
    {
        _bridge?.CancelPendingTasks();
        _bridge = null;
        _messageRegistration?.Dispose();
        _messageRegistration = null;
        _schemeRegistration?.Dispose();
        _schemeRegistration = null;
        // Before the native view goes: the interception disconnects from it.
        _httpInterception?.Dispose(viewAlive: _gtkView == null || _gtkView.Widget != IntPtr.Zero);
        _httpInterception = null;

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

    private static void Serve(HybridWebViewBridge bridge, WebKitContentApi.SchemeRequest request)
    {
        if (!Uri.TryCreate(request.Uri, UriKind.Absolute, out var uri))
        {
            request.Finish(404, "Not Found", null, null);
            return;
        }
        Task<HybridWebResponse> pending;
        try
        {
            pending = bridge.HandleRequestAsync(new HybridWebRequest(uri, request.Method, request.Headers, request.Body));
        }
        catch (Exception ex)
        {
            pending = Task.FromException<HybridWebResponse>(ex);
        }
        pending.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
            {
                var response = t.Result;
                request.Finish(response.StatusCode, response.ReasonPhrase, response.ContentType, response.Body, response.Headers);
            }
            else
            {
                DiagnosticLog.Error("HybridWebView", $"Serving {request.Uri} failed", t.Exception?.GetBaseException() ?? new TaskCanceledException());
                request.Finish(500, "Internal Server Error", null, null);
            }
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private static void OnScriptMessage(HybridWebViewBridge bridge, string message)
    {
        try
        {
            bridge.OnScriptMessage(message);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("HybridWebView", "A message from the page could not be handled", ex);
        }
    }

    private void OnGtkScriptDialogRequested(object? sender, (ScriptDialogType Type, string Message, Action<bool> Callback) e) =>
        LinuxWebViewHandler.ShowGtkScriptDialog(e);

    /// <summary>Raw messages, results and script all go to the page this handler shows.</summary>
    private sealed class BrowserScriptHost : IHybridWebViewScriptHost
    {
        private readonly ILinuxWebView _browser;

        public BrowserScriptHost(ILinuxWebView browser) => _browser = browser;

        public Task<string?> EvaluateJavaScriptAsync(string script) => _browser.EvaluateJavaScriptAsync(script);

        public string? CurrentUrl => _browser.Url;
    }

    #region Initialization events

    private void RaiseInitializationStarted()
    {
        if (VirtualView is not IInitializationAwareWebView aware)
            return;
        try
        {
            if (CreateInternal<WebViewInitializationStartedEventArgs>() is { } args)
                aware.WebViewInitializationStarted(args);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("HybridWebView", "WebViewInitializing threw", ex);
        }
    }

    private void RaiseInitializationCompleted()
    {
        if (VirtualView is not IInitializationAwareWebView aware)
            return;
        try
        {
            if (CreateInternal<WebViewInitializationCompletedEventArgs>() is { } args)
                aware.WebViewInitializationCompleted(args);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("HybridWebView", "WebViewInitialized threw", ex);
        }
    }

    // The platform-neutral args have only an internal, empty constructor.
    private static T? CreateInternal<T>() where T : class =>
        typeof(T).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, Type.EmptyTypes)?.Invoke(null) as T;

    private bool IsDeveloperToolsEnabled()
    {
        // services.AddHybridWebViewDeveloperTools() registers MAUI's (internal) HybridWebViewDeveloperTools.
        var type = typeof(HybridWebViewHandler).Assembly.GetType("Microsoft.Maui.Hosting.HybridWebViewDeveloperTools");
        var tools = type == null ? null : MauiContext?.Services.GetService(type);
        return tools?.GetType().GetProperty("Enabled")?.GetValue(tools) is true;
    }

    #endregion

    #region Commands

    /// <summary>MAUI's <c>MapEvaluateJavaScriptAsync</c> signature; runs the command on a Linux <see cref="LinuxHybridWebViewHandler"/>.</summary>
    public static void MapEvaluateJavaScriptAsync(IHybridWebViewHandler handler, IHybridWebView hybridWebView, object? arg)
    {
        if (handler is LinuxHybridWebViewHandler linuxHandler)
            MapEvaluateJavaScriptAsyncCore(linuxHandler, hybridWebView, arg);
    }

    private static async void MapEvaluateJavaScriptAsyncCore(LinuxHybridWebViewHandler handler, IHybridWebView hybridWebView, object? args)
    {
        if (args is not EvaluateJavaScriptAsyncRequest request)
            return;
        if (handler._bridge is not { } bridge)
        {
            request.TrySetCanceled();
            return;
        }
        try
        {
            request.TrySetResult((await bridge.EvaluateJavaScriptAsync(request.Script))!);
        }
        catch (Exception ex)
        {
            request.TrySetException(ex);
        }
    }

    /// <summary>MAUI's <c>MapInvokeJavaScriptAsync</c> signature; runs the command on a Linux <see cref="LinuxHybridWebViewHandler"/>.</summary>
    public static void MapInvokeJavaScriptAsync(IHybridWebViewHandler handler, IHybridWebView hybridWebView, object? arg)
    {
        if (handler is LinuxHybridWebViewHandler linuxHandler)
            MapInvokeJavaScriptAsyncCore(linuxHandler, hybridWebView, arg);
    }

    private static async void MapInvokeJavaScriptAsyncCore(LinuxHybridWebViewHandler handler, IHybridWebView hybridWebView, object? args)
    {
        if (args is not HybridWebViewInvokeJavaScriptRequest request)
            return;
        if (handler._bridge is not { } bridge)
        {
            request.TrySetCanceled();
            return;
        }
        try
        {
            request.TrySetResult(await bridge.InvokeJavaScriptAsync(request));
        }
        catch (Exception ex)
        {
            request.TrySetException(ex);
        }
    }

    /// <summary>MAUI's <c>MapSendRawMessage</c> signature; runs the command on a Linux <see cref="LinuxHybridWebViewHandler"/>.</summary>
    public static void MapSendRawMessage(IHybridWebViewHandler handler, IHybridWebView hybridWebView, object? arg)
    {
        if (handler is LinuxHybridWebViewHandler linuxHandler)
            MapSendRawMessageCore(linuxHandler, hybridWebView, arg);
    }

    private static void MapSendRawMessageCore(LinuxHybridWebViewHandler handler, IHybridWebView hybridWebView, object? args)
    {
        if (args is HybridWebViewRawMessage message)
            handler._bridge?.SendRawMessage(message.Message ?? string.Empty);
    }

    #endregion
}
