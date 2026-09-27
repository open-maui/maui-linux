// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Platform view for a <c>Layout</c> the platform has no dedicated handler for
/// (a third-party or app-defined <c>Layout</c> subclass): measure and arrange
/// are delegated to the layout's own <c>ILayoutManager</c> through MAUI's
/// <c>CrossPlatformMeasure</c> / <c>CrossPlatformArrange</c>, exactly as on the
/// other platforms: the layout sees local bounds starting at (0,0), and
/// <c>LinuxViewHandler</c> shifts each child's frame to window space when it
/// places the child's Skia view.
/// </summary>
public class SkiaCrossPlatformLayout : SkiaLayoutView, ILocalArrangeHost
{
    private Point _arrangeOrigin;

    private ILayout? Layout => MauiView as ILayout;

    Point ILocalArrangeHost.ArrangeOrigin => _arrangeOrigin;

    protected override Size MeasureOverride(Size availableSize)
    {
        var layout = Layout;
        if (layout == null)
            return base.MeasureOverride(availableSize);
        try
        {
            return layout.CrossPlatformMeasure(availableSize.Width, availableSize.Height);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaCrossPlatformLayout", $"{layout.GetType().Name} measure failed", ex);
            return Size.Zero;
        }
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        var layout = Layout;
        if (layout == null)
            return base.ArrangeOverride(bounds);
        try
        {
            _arrangeOrigin = bounds.Location;
            layout.CrossPlatformArrange(new Rect(0, 0, bounds.Width, bounds.Height));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaCrossPlatformLayout", $"{layout.GetType().Name} arrange failed", ex);
        }
        return bounds;
    }
}
