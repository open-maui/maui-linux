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
public partial class ShapePathHandler : LinuxViewHandler<Path, SkiaShapePath>, IShapeViewHandler
{
    public static IPropertyMapper<Path, ShapePathHandler> Mapper =
        new PropertyMapper<Path, ShapePathHandler>(ShapeViewHandler.Mapper)
        {
            [nameof(IShapeView.Shape)] = MapShape,
            [nameof(Path.Data)] = MapData,
            [nameof(Path.RenderTransform)] = MapRenderTransform,
            [nameof(Path.Fill)] = MapFill,
            [nameof(Path.Stroke)] = MapStroke,
            [nameof(Path.StrokeThickness)] = MapStrokeThickness,
            [nameof(Path.StrokeLineCap)] = MapStrokeLineCap,
            [nameof(Path.StrokeLineJoin)] = MapStrokeLineJoin,
            [nameof(Path.Aspect)] = MapAspect,
            [nameof(Path.StrokeDashArray)] = MapStrokeDashArray,
            [nameof(Path.StrokeDashOffset)] = MapStrokeDashOffset,
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
        MapStrokeLineCap(this, VirtualView);
        MapStrokeLineJoin(this, VirtualView);
        MapStrokeDashArray(this, VirtualView);
        MapStrokeDashOffset(this, VirtualView);
        MapAspect(this, VirtualView);
    }

    public static void MapData(ShapePathHandler handler, Path path)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.Data = path.Data;
        handler.PlatformView.ShapeWindingMode = PathWindingMode(path);
        handler.PlatformView.InvalidatePath();
    }

    /// <summary>
    /// MAUI's PathHandler.MapShape (UpdatePath): the shape drawable, filled with the
    /// winding of the path's geometry.
    /// </summary>
    public static void MapShape(ShapePathHandler handler, Path path)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.UpdateShape(path);
        handler.PlatformView.ShapeWindingMode = PathWindingMode(path);
    }

    /// <summary>The path's RenderTransform, applied to the shape after its aspect (MAUI's PathHandler.MapRenderTransform).</summary>
    public static void MapRenderTransform(ShapePathHandler handler, Path path)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.ShapeRenderTransform = path.RenderTransform?.Value is { } m
            ? new System.Numerics.Matrix3x2((float)m.M11, (float)m.M12, (float)m.M21, (float)m.M22, (float)m.OffsetX, (float)m.OffsetY)
            : null;
        handler.PlatformView.Invalidate();
    }

    // MAUI's ShapeExtensions.GetPathWindingMode: the geometry's FillRule (EvenOdd when it has none).
    private static Microsoft.Maui.Graphics.WindingMode PathWindingMode(Path path)
    {
        var fillRule = FillRule.EvenOdd;
        if (path.Data is GeometryGroup group)
            fillRule = group.FillRule;
        if (path.Data is PathGeometry geometry)
            fillRule = geometry.FillRule;
        return fillRule == FillRule.EvenOdd ? Microsoft.Maui.Graphics.WindingMode.EvenOdd : Microsoft.Maui.Graphics.WindingMode.NonZero;
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

    public static void MapStrokeDashArray(ShapePathHandler handler, Path path)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.StrokeDashArray = path.StrokeDashArray;
        handler.PlatformView.Invalidate();
    }

    public static void MapStrokeDashOffset(ShapePathHandler handler, Path path)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.StrokeDashOffset = path.StrokeDashOffset;
        handler.PlatformView.Invalidate();
    }

    public static void MapAspect(ShapePathHandler handler, Path path)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Aspect = path.Aspect;
        handler.PlatformView.Invalidate();
    }

    IShapeView IShapeViewHandler.VirtualView => VirtualView;

    object IShapeViewHandler.PlatformView => PlatformView;

    protected override void DisconnectHandler(SkiaShapePath platformView)
    {
        platformView.ClearShape();
        base.DisconnectHandler(platformView);
    }
}
