using System.Collections.Generic;
using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Manages WebView instances within the GTK host window.
/// Handles creation, layout updates, and cleanup of WebKit-based web views.
/// </summary>
public sealed class GtkWebViewManager
{
    private readonly GtkHostWindow _host;
    private readonly Dictionary<object, GtkWebViewPlatformView> _webViews = new();

    public GtkWebViewManager(GtkHostWindow host)
    {
        _host = host;
    }

    public GtkWebViewPlatformView CreateWebView(object key, int x, int y, int width, int height)
    {
        var webView = new GtkWebViewPlatformView();
        _webViews[key] = webView;
        _host.AddWebView(webView.Widget, x, y, width, height);
        return webView;
    }

    public void UpdateLayout(object key, int x, int y, int width, int height)
    {
        if (_webViews.TryGetValue(key, out var webView))
        {
            _host.MoveResizeWebView(webView.Widget, x, y, width, height);
        }
    }

    public GtkWebViewPlatformView? GetWebView(object key)
    {
        return _webViews.TryGetValue(key, out var webView) ? webView : null;
    }

    public void RemoveWebView(object key)
    {
        if (_webViews.TryGetValue(key, out var webView))
        {
            _host.RemoveWebView(webView.Widget);
            webView.Dispose();
            _webViews.Remove(key);
        }
    }

    public void Clear()
    {
        foreach (var kvp in _webViews)
        {
            _host.RemoveWebView(kvp.Value.Widget);
            kvp.Value.Dispose();
        }
        _webViews.Clear();
    }
}
