// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for CommunityToolkit.Maui's <c>DrawingView</c>. The toolkit's
/// generic net10.0 handler has no platform view (it throws when MAUI asks for
/// one), so without this the control rendered as OpenMaui's unsupported-view
/// placeholder. Registered automatically when the toolkit is part of the app;
/// OpenMaui takes no dependency on it (see <see cref="SkiaDrawingView"/>).
/// </summary>
public class DrawingViewHandler : LinuxViewHandler<IView, SkiaDrawingView>
{
    public static IPropertyMapper<IView, DrawingViewHandler> Mapper = new PropertyMapper<IView, DrawingViewHandler>(ViewHandler.ViewMapper)
    {
        // The platform view observes the virtual view's PropertyChanged and its
        // Lines/Points collections; these keep the mapper-driven paths
        // (initial sync, handler re-attach) invalidating too.
        ["Lines"] = MapRefresh,
        ["LineColor"] = MapInvalidate,
        ["LineWidth"] = MapInvalidate,
        ["DrawAction"] = MapInvalidate,
        [nameof(IView.Background)] = MapInvalidate,
    };

    public DrawingViewHandler() : base(Mapper, ViewHandler.ViewCommandMapper)
    {
    }

    /// <summary>
    /// The toolkit's DrawingView type when CommunityToolkit.Maui is loadable in
    /// this app, otherwise null (the handler is then never registered).
    /// </summary>
    internal static Type? ToolkitDrawingViewType { get; } = ResolveToolkitType();

    private static Type? ResolveToolkitType()
    {
        try
        {
            return Type.GetType(DrawingViewContract.DrawingViewTypeName, throwOnError: false);
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or TypeLoadException)
        {
            return null;
        }
    }

    protected override SkiaDrawingView CreatePlatformView() => new();

    protected override void ConnectHandler(SkiaDrawingView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.DrawingView = VirtualView;
    }

    protected override void DisconnectHandler(SkiaDrawingView platformView)
    {
        platformView.CancelStroke();
        platformView.DrawingView = null;
        base.DisconnectHandler(platformView);
    }

    public static void MapRefresh(DrawingViewHandler handler, IView view) => handler.PlatformView?.Refresh();

    public static void MapInvalidate(DrawingViewHandler handler, IView view) => handler.PlatformView?.Invalidate();
}
