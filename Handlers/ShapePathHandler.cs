// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Microsoft.Maui.Controls.Shapes.Path on Linux using Skia rendering.
/// Supports PathGeometry with LineSegment, BezierSegment, QuadraticBezierSegment,
/// ArcSegment, PolyLineSegment, PolyBezierSegment, PolyQuadraticBezierSegment.
/// </summary>
public partial class ShapePathHandler : ViewHandler<Path, SkiaShapePath>
{
    public static IPropertyMapper<Path, ShapePathHandler> Mapper =
        new PropertyMapper<Path, ShapePathHandler>(ViewHandler.ViewMapper)
        {
            [nameof(Path.Data)] = MapData,
            [nameof(Path.Fill)] = MapFill,
            [nameof(Path.Stroke)] = MapStroke,
            [nameof(Path.StrokeThickness)] = MapStrokeThickness,
            [nameof(Path.StrokeLineCap)] = MapStrokeLineCap,
            [nameof(Path.StrokeLineJoin)] = MapStrokeLineJoin,
            [nameof(Path.Aspect)] = MapAspect,
        };

    public ShapePathHandler() : base(Mapper) { }

    public ShapePathHandler(IPropertyMapper? mapper = null)
        : base(mapper ?? Mapper) { }

    protected override SkiaShapePath CreatePlatformView() => new SkiaShapePath();

    protected override void ConnectHandler(SkiaShapePath platformView)
    {
        base.ConnectHandler(platformView);

        if (VirtualView == null) return;

        // Sync all properties that may have been set before handler creation
        MapData(this, VirtualView);
        MapFill(this, VirtualView);
        MapStroke(this, VirtualView);
        MapStrokeThickness(this, VirtualView);
        MapAspect(this, VirtualView);
    }

    public static void MapData(ShapePathHandler handler, Path path)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.Data = path.Data;
        handler.PlatformView.InvalidatePath();
    }

    public static void MapFill(ShapePathHandler handler, Path path)
    {
        if (handler.PlatformView is null) return;

        if (path.Fill is SolidColorBrush scb)
        {
            handler.PlatformView.FillColor = scb.Color;
        }
        else
        {
            handler.PlatformView.FillColor = null;
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapStroke(ShapePathHandler handler, Path path)
    {
        if (handler.PlatformView is null) return;

        if (path.Stroke is SolidColorBrush scb)
        {
            handler.PlatformView.StrokeColor = scb.Color;
        }
        else
        {
            handler.PlatformView.StrokeColor = null;
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapStrokeThickness(ShapePathHandler handler, Path path)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.StrokeThickness = path.StrokeThickness;
        handler.PlatformView.Invalidate();
    }

    public static void MapStrokeLineCap(ShapePathHandler handler, Path path)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.StrokeLineCap = path.StrokeLineCap;
        handler.PlatformView.Invalidate();
    }

    public static void MapStrokeLineJoin(ShapePathHandler handler, Path path)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.StrokeLineJoin = path.StrokeLineJoin;
        handler.PlatformView.Invalidate();
    }

    public static void MapAspect(ShapePathHandler handler, Path path)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Aspect = path.Aspect;
        handler.PlatformView.Invalidate();
    }
}
