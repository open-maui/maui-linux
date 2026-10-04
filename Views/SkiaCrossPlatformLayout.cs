// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Platform view for every MAUI <c>Layout</c>: Grid, the stack layouts,
/// FlexLayout, AbsoluteLayout, and any third-party or app-defined
/// <c>Layout</c> subclass. Measure and arrange are delegated to the layout's
/// own <c>ILayoutManager</c> through MAUI's <c>CrossPlatformMeasure</c> /
/// <c>CrossPlatformArrange</c>, exactly as on the other platforms: the layout
/// sees local bounds starting at (0,0), and <c>LinuxViewHandler</c> shifts
/// each child's frame to window space when it places the child's Skia view.
/// This view keeps what the platform owns: the Skia children (drawing order,
/// clipping, background, hit-testing and input) and invalidation.
/// </summary>
public class SkiaCrossPlatformLayout : SkiaLayoutView, ILocalArrangeHost
{
    private Point _arrangeOrigin;
    private double? _mirrorWidth;

    private ILayout? Layout => MauiView as ILayout;

    Point ILocalArrangeHost.ArrangeOrigin => _arrangeOrigin;

    double? ILocalArrangeHost.MirrorWidth => _mirrorWidth;

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
            // A right-to-left layout mirrors its children's frames inside its
            // own width when they are placed, as Android and iOS do in MAUI's
            // PlatformArrange; the layout managers themselves are direction-blind.
            _mirrorWidth = MauiView is IVisualElementController controller
                && (controller.EffectiveFlowDirection & EffectiveFlowDirection.RightToLeft) != 0
                ? bounds.Width
                : null;
            layout.CrossPlatformArrange(new Rect(0, 0, bounds.Width, bounds.Height));
            PlaceUnbridgedChildren();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaCrossPlatformLayout", $"{layout.GetType().Name} arrange failed", ex);
        }
        return bounds;
    }

    /// <summary>
    /// Places the Skia view of each child whose handler does not place it
    /// when MAUI arranges the child (a library handler built on the portable
    /// ViewHandler, whose PlatformArrange does nothing) at the frame the
    /// layout's manager gave the child, so it is drawn in its cell rather
    /// than wherever it was last left.
    /// </summary>
    private void PlaceUnbridgedChildren()
    {
        var host = (ILocalArrangeHost)this;
        foreach (var child in Children.ToArray())
        {
            if (child.MauiView is not { } view || view.Handler is null or ISkiaLayoutBridge
                || ((IView)view).Visibility == Visibility.Collapsed)
                continue;
            var frame = host.ToWindow(view.Frame, out var mirrored);
            var wasInMaui = child.InMauiArrange;
            var wasKeeping = child.KeepMauiFrame;
            child.InMauiArrange = true;
            child.KeepMauiFrame = mirrored;
            try
            {
                child.Arrange(frame);
            }
            finally
            {
                child.InMauiArrange = wasInMaui;
                child.KeepMauiFrame = wasKeeping;
            }
        }
    }
}
