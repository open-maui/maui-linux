// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for GraphicsView on Linux using Skia rendering.
/// Maps IGraphicsView interface to SkiaGraphicsView platform view.
/// IGraphicsView has: Drawable, Invalidate()
/// </summary>
public partial class GraphicsViewHandler : LinuxViewHandler<IGraphicsView, SkiaGraphicsView>, IGraphicsViewHandler
{
    IGraphicsView IGraphicsViewHandler.VirtualView => VirtualView;
    object IGraphicsViewHandler.PlatformView => PlatformView;

    public static IPropertyMapper<IGraphicsView, GraphicsViewHandler> Mapper = new PropertyMapper<IGraphicsView, GraphicsViewHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IGraphicsView.Drawable)] = MapDrawable,
        [nameof(IView.Background)] = MapBackground,
    };

    public static CommandMapper<IGraphicsView, GraphicsViewHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
        [nameof(IGraphicsView.Invalidate)] = MapInvalidate,
    };

    public GraphicsViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public GraphicsViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaGraphicsView CreatePlatformView()
    {
        return new SkiaGraphicsView();
    }

    /// <summary>MAUI's <c>MapDrawable</c> signature; maps the property on a Linux <see cref="GraphicsViewHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapDrawable(IGraphicsViewHandler handler, IGraphicsView graphicsView)
    {
        if (handler is GraphicsViewHandler linuxHandler)
            MapDrawableCore(linuxHandler, graphicsView);
    }

    [Obsolete("Use the IGraphicsViewHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapDrawable(GraphicsViewHandler handler, IGraphicsView graphicsView) => MapDrawableCore(handler, graphicsView);

    private static void MapDrawableCore(GraphicsViewHandler handler, IGraphicsView graphicsView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Drawable = graphicsView.Drawable;
    }

    public static void MapBackground(GraphicsViewHandler handler, IGraphicsView graphicsView)
    {
        if (handler.PlatformView is null) return;

        if (graphicsView.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
    }

    /// <summary>MAUI's <c>MapInvalidate</c> signature; runs the command on a Linux <see cref="GraphicsViewHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapInvalidate(IGraphicsViewHandler handler, IGraphicsView graphicsView, object? arg)
    {
        if (handler is GraphicsViewHandler linuxHandler)
            MapInvalidateCore(linuxHandler, graphicsView, arg);
    }

    [Obsolete("Use the IGraphicsViewHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapInvalidate(GraphicsViewHandler handler, IGraphicsView graphicsView, object? args) => MapInvalidateCore(handler, graphicsView, args);

    private static void MapInvalidateCore(GraphicsViewHandler handler, IGraphicsView graphicsView, object? args)
    {
        handler.PlatformView?.Invalidate();
    }

    /// <summary>MAUI's <c>MapFlowDirection</c>: the view's flow direction (the view mapper's), then a redraw.</summary>
    public static void MapFlowDirection(IGraphicsViewHandler handler, IGraphicsView graphicsView)
    {
        ViewHandler.ViewMapper.UpdateProperty(handler, graphicsView, nameof(IView.FlowDirection));
        (handler.PlatformView as SkiaView)?.Invalidate();
    }
}
