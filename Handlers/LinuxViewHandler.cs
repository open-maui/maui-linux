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
/// <c>Layout</c> run by its <c>ILayoutManager</c>, a ControlTemplate's
/// presenter) saw every control as 0x0.
/// </summary>
public abstract class LinuxViewHandler<TVirtualView, TPlatformView> : ViewHandler<TVirtualView, TPlatformView>, ISkiaLayoutBridge
    where TVirtualView : class, IView
    where TPlatformView : class
{
    protected LinuxViewHandler(IPropertyMapper mapper, CommandMapper? commandMapper = null)
        : base(mapper, commandMapper)
    {
    }

    /// <summary>
    /// A handler connected the way MAUI connects one (<c>view.Handler = new XHandler()</c>,
    /// <c>ToHandler</c>) gives its Skia view the MAUI view, as OpenMaui's own handler factory
    /// does: without it the view had no MAUI frame, no Loaded and no layout options.
    /// </summary>
    protected override void ConnectHandler(TPlatformView platformView)
    {
        base.ConnectHandler(platformView);
        if (platformView is SkiaView skia && skia.MauiView is null && VirtualView is Microsoft.Maui.Controls.View view)
            skia.MauiView = view;
    }

    /// <inheritdoc />
    public override Size GetDesiredSize(double widthConstraint, double heightConstraint)
    {
        if (PlatformView is not SkiaView skia || VirtualView is not { } view)
            return base.GetDesiredSize(widthConstraint, heightConstraint);
        return SkiaLayoutBridge.GetDesiredSize(skia, view, widthConstraint, heightConstraint);
    }

    /// <inheritdoc />
    public override void PlatformArrange(Rect frame)
    {
        if (PlatformView is SkiaView skia)
            SkiaLayoutBridge.PlatformArrange(skia, VirtualView, frame);
        else
            base.PlatformArrange(frame);

        // As on MAUI's platforms (PlatformArrangeHandler), an arrange runs the Frame command,
        // which apps and libraries extend (ViewCommandMapper.AppendToMapping(nameof(IView.Frame), ...))
        // to react to a view being placed. A negative frame is Controls' "not laid out yet".
        if (frame.Width >= 0 && frame.Height >= 0)
            Invoke(nameof(IView.Frame), frame);
    }
}

/// <summary>
/// Marks a handler that measures and places its Skia view when MAUI asks it
/// to (GetDesiredSize / PlatformArrange, through <see cref="SkiaLayoutBridge"/>).
/// A layout places the Skia view of a child whose handler is not one (a
/// library handler built on the portable ViewHandler) itself, from the frame
/// MAUI gave the child.
/// </summary>
internal interface ISkiaLayoutBridge
{
}

/// <summary>
/// MAUI's measure and arrange of a view, carried out on its Skia view: what
/// <see cref="LinuxViewHandler{TVirtualView, TPlatformView}"/> does, shared
/// with handlers that cannot derive from it (MediaElement's derives from the
/// toolkit's handler).
/// </summary>
internal static class SkiaLayoutBridge
{
    /// <summary>The handler's GetDesiredSize: the Skia view measured at MAUI's constraints.</summary>
    public static Size GetDesiredSize(SkiaView skia, IView view, double widthConstraint, double heightConstraint)
    {
        // MAUI's measure (the view's own MeasureOverride included) is what
        // called this: the Skia view measures itself, with no second trip
        // through MAUI (a library control's override ran twice per measure).
        var wasInMaui = skia.InMauiMeasure;
        skia.InMauiMeasure = true;
        try
        {
            return LinuxViewMeasure.Measure(skia, view, widthConstraint, heightConstraint);
        }
        finally
        {
            skia.InMauiMeasure = wasInMaui;
        }
    }

    /// <summary>The handler's PlatformArrange: the Skia view placed at MAUI's frame.</summary>
    public static void PlatformArrange(SkiaView skia, IView? view, Rect frame)
    {
        // A cross-platform layout arranges its children in its own
        // coordinates, as MAUI does everywhere; Skia bounds are
        // window-absolute, so shift by the parent's origin. Done here
        // rather than in the layout's pass so arranges made outside it
        // (a list placing items as it scrolls) land right too. The parent
        // is the platform one, as a native PlatformArrange places a view in
        // its superview: a view shown in a layout other than its logical
        // parent (a drop-down list parented to its combo box but hosted in
        // the popup's grid) is placed in the layout showing it.
        var platformParent = skia.Parent;
        if (platformParent == null
            && view is Microsoft.Maui.Controls.Element { Parent: Microsoft.Maui.Controls.VisualElement { Handler.PlatformView: SkiaView logicalParent } })
            platformParent = logicalParent;
        bool mirrored = false;
        if (platformParent is ILocalArrangeHost host)
            frame = host.ToWindow(frame, out mirrored);

        // MAUI's arrange (the view's ArrangeOverride included) is what called
        // this. A mirrored view keeps the unmirrored Frame MAUI just set, as
        // on Android and iOS.
        var wasInMaui = skia.InMauiArrange;
        var wasKeeping = skia.KeepMauiFrame;
        skia.InMauiArrange = true;
        skia.KeepMauiFrame = mirrored;
        try
        {
            skia.Arrange(frame);
        }
        finally
        {
            skia.InMauiArrange = wasInMaui;
            skia.KeepMauiFrame = wasKeeping;
        }
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

    /// <summary>
    /// The width children's local frames are mirrored in (a right-to-left
    /// layout), or null when they are placed as given.
    /// </summary>
    double? MirrorWidth => null;

    /// <summary>
    /// A child's local frame in window space: mirrored inside
    /// <see cref="MirrorWidth"/> when set (a right-to-left layout places its
    /// children mirrored, as Android and iOS do), then shifted by
    /// <see cref="ArrangeOrigin"/>.
    /// </summary>
    Rect ToWindow(Rect localFrame, out bool mirrored)
    {
        mirrored = false;
        if (MirrorWidth is double width)
        {
            localFrame = new Rect(width - localFrame.X - localFrame.Width, localFrame.Y, localFrame.Width, localFrame.Height);
            mirrored = true;
        }
        return localFrame.Offset(ArrangeOrigin.X, ArrangeOrigin.Y);
    }
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
        // A platform view's desired size never exceeds the space it was offered
        // (Android measures AT_MOST, WinUI's DesiredSize is capped to the available
        // size): a no-wrap or truncated label in a 100-wide stack is 100 wide.
        // Explicit sizes and minimums still win, as on MAUI's platforms.
        if (!IsSet(view.Width) && !double.IsInfinity(widthConstraint) && !double.IsNaN(widthConstraint))
            width = Math.Min(width, widthConstraint);
        if (!IsSet(view.Height) && !double.IsInfinity(heightConstraint) && !double.IsNaN(heightConstraint))
            height = Math.Min(height, heightConstraint);
        if (IsSet(view.MinimumWidth)) width = Math.Max(width, view.MinimumWidth);
        if (IsSet(view.MinimumHeight)) height = Math.Max(height, view.MinimumHeight);
        if (IsSet(view.MaximumWidth)) width = Math.Min(width, view.MaximumWidth);
        if (IsSet(view.MaximumHeight)) height = Math.Min(height, view.MaximumHeight);
        return new Size(width, height);
    }
}
