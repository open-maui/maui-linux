// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Base for the platform's view handlers: bridges MAUI's cross-platform
/// measure/arrange (<c>IView.Measure</c> / <c>IView.Arrange</c>, which ask the
/// handler) to the Skia view. The portable MAUI handler returns
/// <c>Size.Zero</c> from GetDesiredSize and ignores PlatformArrange, so before
/// this any MAUI-side measurement (app code calling <c>view.Measure</c>, a
/// custom <c>Layout</c> with its own <c>ILayoutManager</c>, a ControlTemplate's
/// presenter) saw every control as 0x0.
/// </summary>
public abstract class LinuxViewHandler<TVirtualView, TPlatformView> : ViewHandler<TVirtualView, TPlatformView>
    where TVirtualView : class, IView
    where TPlatformView : class
{
    protected LinuxViewHandler(IPropertyMapper mapper, CommandMapper? commandMapper = null)
        : base(mapper, commandMapper)
    {
    }

    /// <inheritdoc />
    public override Size GetDesiredSize(double widthConstraint, double heightConstraint)
    {
        if (PlatformView is not SkiaView skia || VirtualView is not { } view)
            return base.GetDesiredSize(widthConstraint, heightConstraint);
        return LinuxViewMeasure.Measure(skia, view, widthConstraint, heightConstraint);
    }

    /// <inheritdoc />
    public override void PlatformArrange(Rect frame)
    {
        if (PlatformView is SkiaView skia)
        {
            // A cross-platform layout arranges its children in its own
            // coordinates, as MAUI does everywhere; Skia bounds are
            // window-absolute, so shift by the parent's origin. Done here
            // rather than in the layout's pass so arranges made outside it
            // (a list placing items as it scrolls) land right too.
            if (VirtualView is Microsoft.Maui.Controls.Element { Parent: Microsoft.Maui.Controls.VisualElement { Handler.PlatformView: ILocalArrangeHost host } })
                frame = frame.Offset(host.ArrangeOrigin.X, host.ArrangeOrigin.Y);
            skia.Arrange(frame);
        }
        else
            base.PlatformArrange(frame);
    }
}

/// <summary>
/// A platform view whose children are arranged in its local coordinates (the
/// MAUI contract for <c>CrossPlatformArrange</c>) and shifted to window space
/// by <see cref="ArrangeOrigin"/>.
/// </summary>
internal interface ILocalArrangeHost
{
    /// <summary>The window-space position of the host's local origin.</summary>
    Point ArrangeOrigin { get; }
}

/// <summary>Shared measure logic, honouring MAUI's explicit and min/max sizes.</summary>
internal static class LinuxViewMeasure
{
    private static bool IsSet(double v) => !double.IsNaN(v) && v >= 0 && !double.IsInfinity(v);

    public static Size Measure(SkiaView skia, IView view, double widthConstraint, double heightConstraint)
    {
        double w = IsSet(view.Width) ? view.Width : widthConstraint;
        double h = IsSet(view.Height) ? view.Height : heightConstraint;
        var measured = skia.Measure(new Size(w, h));

        double width = IsSet(view.Width) ? view.Width : measured.Width;
        double height = IsSet(view.Height) ? view.Height : measured.Height;
        if (IsSet(view.MinimumWidth)) width = Math.Max(width, view.MinimumWidth);
        if (IsSet(view.MinimumHeight)) height = Math.Max(height, view.MinimumHeight);
        if (IsSet(view.MaximumWidth)) width = Math.Min(width, view.MaximumWidth);
        if (IsSet(view.MaximumHeight)) height = Math.Min(height, view.MaximumHeight);
        return new Size(width, height);
    }
}
