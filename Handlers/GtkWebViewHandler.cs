// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for WebView using native GTK WebKitGTK widget.
/// </summary>
public class GtkWebViewHandler : ViewHandler<IWebView, GtkWebViewProxy>
{
    private GtkWebViewPlatformView? _platformWebView;
    private bool _isRegisteredWithHost;
    private SKRect _lastBounds;

    public static IPropertyMapper<IWebView, GtkWebViewHandler> Mapper = new PropertyMapper<IWebView, GtkWebViewHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IWebView.Source)] = MapSource,
    };

    public static CommandMapper<IWebView, GtkWebViewHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
        [nameof(IWebView.GoBack)] = MapGoBack,
        [nameof(IWebView.GoForward)] = MapGoForward,
        [nameof(IWebView.Reload)] = MapReload,
    };

    public GtkWebViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public GtkWebViewHandler(IPropertyMapper? mapper = null, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override GtkWebViewProxy CreatePlatformView()
    {
        _platformWebView = new GtkWebViewPlatformView();
        return new GtkWebViewProxy(this, _platformWebView);
    }

    protected override void ConnectHandler(GtkWebViewProxy platformView)
    {
        base.ConnectHandler(platformView);
        if (_platformWebView != null)
        {
            _platformWebView.NavigationStarted += OnNavigationStarted;
            _platformWebView.NavigationCompleted += OnNavigationCompleted;
        }
        Console.WriteLine("[GtkWebViewHandler] ConnectHandler - WebView ready");
    }

    protected override void DisconnectHandler(GtkWebViewProxy platformView)
    {
        if (_platformWebView != null)
        {
            _platformWebView.NavigationStarted -= OnNavigationStarted;
            _platformWebView.NavigationCompleted -= OnNavigationCompleted;
            UnregisterFromHost();
            _platformWebView.Dispose();
            _platformWebView = null;
        }
        base.DisconnectHandler(platformView);
    }

    private void OnNavigationStarted(object? sender, string uri)
    {
        Console.WriteLine($"[GtkWebViewHandler] Navigation started: {uri}");
        try
        {
            GLibNative.IdleAdd(() =>
            {
                try
                {
                    if (VirtualView is IWebViewController controller)
                    {
                        var args = new Microsoft.Maui.Controls.WebNavigatingEventArgs(
                            WebNavigationEvent.NewPage, null, uri);
                        controller.SendNavigating(args);
                        Console.WriteLine("[GtkWebViewHandler] Sent Navigating event to VirtualView");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GtkWebViewHandler] Error in SendNavigating: {ex.Message}");
                }
                return false;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GtkWebViewHandler] Error dispatching navigation started: {ex.Message}");
        }
    }

    private void OnNavigationCompleted(object? sender, (string Url, bool Success) e)
    {
        Console.WriteLine($"[GtkWebViewHandler] Navigation completed: {e.Url} (Success: {e.Success})");
        try
        {
            GLibNative.IdleAdd(() =>
            {
                try
                {
                    if (VirtualView is IWebViewController controller)
                    {
                        var result = e.Success ? WebNavigationResult.Success : WebNavigationResult.Failure;
                        var args = new Microsoft.Maui.Controls.WebNavigatedEventArgs(
                            WebNavigationEvent.NewPage, null, e.Url, result);
                        controller.SendNavigated(args);

                        bool canGoBack = _platformWebView?.CanGoBack() ?? false;
                        bool canGoForward = _platformWebView?.CanGoForward() ?? false;
                        controller.CanGoBack = canGoBack;
                        controller.CanGoForward = canGoForward;
                        Console.WriteLine($"[GtkWebViewHandler] Sent Navigated, CanGoBack={canGoBack}, CanGoForward={canGoForward}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GtkWebViewHandler] Error in SendNavigated: {ex.Message}");
                }
                return false;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GtkWebViewHandler] Error dispatching navigation completed: {ex.Message}");
        }
    }

    internal void RegisterWithHost(SKRect bounds)
    {
        if (_platformWebView == null)
            return;

        var hostService = GtkHostService.Instance;
        if (hostService.HostWindow == null || hostService.WebViewManager == null)
        {
            Console.WriteLine("[GtkWebViewHandler] Warning: GTK host not initialized, cannot register WebView");
            return;
        }

        int x = (int)bounds.Left;
        int y = (int)bounds.Top;
        int width = (int)bounds.Width;
        int height = (int)bounds.Height;

        if (width <= 0 || height <= 0)
        {
            Console.WriteLine($"[GtkWebViewHandler] Skipping invalid bounds: {bounds}");
            return;
        }

        if (!_isRegisteredWithHost)
        {
            hostService.HostWindow.AddWebView(_platformWebView.Widget, x, y, width, height);
            _isRegisteredWithHost = true;
            Console.WriteLine($"[GtkWebViewHandler] Registered WebView at ({x}, {y}) size {width}x{height}");
        }
        else if (bounds != _lastBounds)
        {
            hostService.HostWindow.MoveResizeWebView(_platformWebView.Widget, x, y, width, height);
            Console.WriteLine($"[GtkWebViewHandler] Updated WebView to ({x}, {y}) size {width}x{height}");
        }

        _lastBounds = bounds;
    }

    private void UnregisterFromHost()
    {
        if (_isRegisteredWithHost && _platformWebView != null)
        {
            var hostService = GtkHostService.Instance;
            if (hostService.HostWindow != null)
            {
                hostService.HostWindow.RemoveWebView(_platformWebView.Widget);
                Console.WriteLine("[GtkWebViewHandler] Unregistered WebView from host");
            }
            _isRegisteredWithHost = false;
        }
    }

    public static void MapSource(GtkWebViewHandler handler, IWebView webView)
    {
        if (handler._platformWebView == null)
            return;

        var source = webView.Source;
        Console.WriteLine($"[GtkWebViewHandler] MapSource: {source?.GetType().Name ?? "null"}");

        if (source is UrlWebViewSource urlSource)
        {
            var url = urlSource.Url;
            if (!string.IsNullOrEmpty(url))
            {
                handler._platformWebView.Navigate(url);
            }
        }
        else if (source is HtmlWebViewSource htmlSource)
        {
            var html = htmlSource.Html;
            if (!string.IsNullOrEmpty(html))
            {
                handler._platformWebView.LoadHtml(html, htmlSource.BaseUrl);
            }
        }
    }

    public static void MapGoBack(GtkWebViewHandler handler, IWebView webView, object? args)
    {
        Console.WriteLine($"[GtkWebViewHandler] MapGoBack called, CanGoBack={handler._platformWebView?.CanGoBack()}");
        handler._platformWebView?.GoBack();
    }

    public static void MapGoForward(GtkWebViewHandler handler, IWebView webView, object? args)
    {
        Console.WriteLine($"[GtkWebViewHandler] MapGoForward called, CanGoForward={handler._platformWebView?.CanGoForward()}");
        handler._platformWebView?.GoForward();
    }

    public static void MapReload(GtkWebViewHandler handler, IWebView webView, object? args)
    {
        Console.WriteLine("[GtkWebViewHandler] MapReload called");
        handler._platformWebView?.Reload();
    }
}
