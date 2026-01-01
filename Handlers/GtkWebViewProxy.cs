// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Proxy view that bridges SkiaView layout to GTK WebView positioning.
/// </summary>
public class GtkWebViewProxy : SkiaView
{
    private readonly GtkWebViewHandler _handler;
    private readonly GtkWebViewPlatformView _platformView;

    public GtkWebViewPlatformView PlatformView => _platformView;
    public bool CanGoBack => _platformView.CanGoBack();
    public bool CanGoForward => _platformView.CanGoForward();

    public GtkWebViewProxy(GtkWebViewHandler handler, GtkWebViewPlatformView platformView)
    {
        _handler = handler;
        _platformView = platformView;
    }

    public override void Arrange(SKRect bounds)
    {
        base.Arrange(bounds);
        var windowBounds = TransformToWindow(bounds);
        _handler.RegisterWithHost(windowBounds);
    }

    private SKRect TransformToWindow(SKRect localBounds)
    {
        float x = localBounds.Left;
        float y = localBounds.Top;

        for (var parent = Parent; parent != null; parent = parent.Parent)
        {
            x += parent.Bounds.Left;
            y += parent.Bounds.Top;
        }

        return new SKRect(x, y, x + localBounds.Width, y + localBounds.Height);
    }

    public override void Draw(SKCanvas canvas)
    {
        // Draw transparent placeholder - actual WebView is rendered by GTK
        using var paint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 0),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(Bounds, paint);
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
