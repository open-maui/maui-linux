// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for GraphicsView on Linux using Skia rendering.
/// Maps IGraphicsView interface to SkiaGraphicsView platform view.
/// IGraphicsView has: Drawable, Invalidate()
/// </summary>
public partial class GraphicsViewHandler : ViewHandler<IGraphicsView, SkiaGraphicsView>
{
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

    public static void MapDrawable(GraphicsViewHandler handler, IGraphicsView graphicsView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Drawable = graphicsView.Drawable;
    }

    public static void MapBackground(GraphicsViewHandler handler, IGraphicsView graphicsView)
    {
        if (handler.PlatformView is null) return;

        if (graphicsView.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }

    public static void MapInvalidate(GraphicsViewHandler handler, IGraphicsView graphicsView, object? args)
    {
        handler.PlatformView?.Invalidate();
    }
}
