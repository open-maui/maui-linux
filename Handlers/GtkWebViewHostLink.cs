// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Places a WebKitGTK widget in the GTK host window over its Skia view's bounds, and takes it out
/// again. Shared by every handler that shows a <see cref="GtkWebViewPlatformView"/>.
/// </summary>
internal sealed class GtkWebViewHostLink
{
    private readonly GtkWebViewPlatformView _view;
    private bool _registered;
    private SKRect _lastBounds;

    internal GtkWebViewHostLink(GtkWebViewPlatformView view) => _view = view;

    internal void Register(SKRect bounds)
    {
        var host = GtkHostService.Instance;
        if (host.HostWindow == null || host.WebViewManager == null)
        {
            DiagnosticLog.Warn("GtkWebViewHostLink", "GTK host not initialized, cannot show the WebView");
            return;
        }
        int x = (int)bounds.Left, y = (int)bounds.Top, width = (int)bounds.Width, height = (int)bounds.Height;
        if (width <= 0 || height <= 0)
            return;
        if (!_registered)
        {
            host.HostWindow.AddWebView(_view.Widget, x, y, width, height);
            _registered = true;
        }
        else if (bounds != _lastBounds)
        {
            host.HostWindow.MoveResizeWebView(_view.Widget, x, y, width, height);
        }
        _lastBounds = bounds;
    }

    internal void Unregister()
    {
        if (!_registered)
            return;
        GtkHostService.Instance.HostWindow?.RemoveWebView(_view.Widget);
        _registered = false;
    }
}
