// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Proxy view that bridges SkiaView layout to GTK WebView positioning.
/// </summary>
public partial class GtkWebViewProxy : SkiaView
{
    private readonly GtkWebViewHandler? _handler;
    private readonly GtkWebViewHostLink? _hostLink;
    private readonly GtkWebViewPlatformView _platformView;

    public GtkWebViewPlatformView PlatformView => _platformView;
    public bool CanGoBack => _platformView.CanGoBack();
    public bool CanGoForward => _platformView.CanGoForward();

    public GtkWebViewProxy(GtkWebViewHandler handler, GtkWebViewPlatformView platformView)
    {
        _handler = handler;
        _platformView = platformView;
        AttachBrowser();
    }

    /// <summary>A proxy placed in the GTK host by <paramref name="hostLink"/> (for handlers other than <see cref="GtkWebViewHandler"/>).</summary>
    internal GtkWebViewProxy(GtkWebViewPlatformView platformView, GtkWebViewHostLink hostLink)
    {
        _hostLink = hostLink;
        _platformView = platformView;
        AttachBrowser();
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        // Bounds are already in absolute window coordinates - use them directly
        // The Skia layout system uses absolute coordinates throughout
        var placed = new SKRect((float)Bounds.Left, (float)Bounds.Top, (float)Bounds.Right, (float)Bounds.Bottom);
        if (_hostLink != null)
            _hostLink.Register(placed);
        else
            _handler?.RegisterWithHost(placed);
    }

    public override void Draw(SKCanvas canvas)
    {
        // Draw transparent placeholder - actual WebView is rendered by GTK
        using var paint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 0),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(new SKRect((float)Bounds.Left, (float)Bounds.Top, (float)Bounds.Right, (float)Bounds.Bottom), paint);
    }

    public void Navigate(string url)
    {
        _platformView.Navigate(url);
    }

    public void LoadHtml(string html, string? baseUrl = null)
    {
        _platformView.LoadHtml(html, baseUrl);
    }

    public void GoBack()
    {
        _platformView.GoBack();
    }

    public void GoForward()
    {
        _platformView.GoForward();
    }

    public void Reload()
    {
        _platformView.Reload();
    }
}
