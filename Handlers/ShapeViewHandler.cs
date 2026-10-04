// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI's ShapeViewHandler on Linux: the handler of a core <see cref="IShapeView"/>
/// (a library's shape view, a Controls BoxView or shape given this handler). Its
/// platform view is a <see cref="SkiaShapeView"/> drawing MAUI's
/// <c>ShapeDrawable</c>, so fill, stroke, dash pattern and offset, line cap and
/// join, miter limit and aspect follow MAUI's shape drawing. OpenMaui's handlers for
/// the Controls shapes (<see cref="RectangleHandler"/>, <see cref="LineHandler"/>,
/// <see cref="BoxViewHandler"/>, ...) chain <see cref="Mapper"/> and draw the same way.
/// </summary>
public partial class ShapeViewHandler : LinuxViewHandler<IShapeView, SkiaShapeView>, IShapeViewHandler
{
    public static IPropertyMapper<IShapeView, IShapeViewHandler> Mapper = new PropertyMapper<IShapeView, IShapeViewHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IShapeView.Background)] = MapBackground,
        [nameof(IShapeView.Shape)] = MapShape,
        [nameof(IShapeView.Aspect)] = MapAspect,
        [nameof(IShapeView.Fill)] = MapFill,
        [nameof(IShapeView.Stroke)] = MapStroke,
        [nameof(IShapeView.StrokeThickness)] = MapStrokeThickness,
        [nameof(IShapeView.StrokeDashPattern)] = MapStrokeDashPattern,
        [nameof(IShapeView.StrokeDashOffset)] = MapStrokeDashOffset,
        [nameof(IShapeView.StrokeLineCap)] = MapStrokeLineCap,
        [nameof(IShapeView.StrokeLineJoin)] = MapStrokeLineJoin,
        [nameof(IShapeView.StrokeMiterLimit)] = MapStrokeMiterLimit,
        // Controls' Shape.RemapForControls adds the Controls dash array to MAUI's mapper.
        ["StrokeDashArray"] = MapStrokeDashPattern,
    };

    public static CommandMapper<IShapeView, IShapeViewHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public ShapeViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public ShapeViewHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper, CommandMapper)
    {
    }

    public ShapeViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    IShapeView IShapeViewHandler.VirtualView => VirtualView;

    object IShapeViewHandler.PlatformView => PlatformView;

    protected override SkiaShapeView CreatePlatformView() => new();

    protected override void DisconnectHandler(SkiaShapeView platformView)
    {
        platformView.ClearShape();
        base.DisconnectHandler(platformView);
    }

    /// <summary>The Skia shape view of a shape handler (this one or a Controls shape's).</summary>
    internal static SkiaShapeView? ShapePlatformView(IShapeViewHandler handler) =>
        handler.PlatformView as SkiaShapeView;

    /// <summary>
    /// With both a Fill and a Background, the Background paints the view behind the
    /// shape; with only a Background, it fills the shape (<see cref="SkiaShapeView"/>).
    /// </summary>
    public static void MapBackground(IShapeViewHandler handler, IShapeView shapeView) =>
        ShapePlatformView(handler)?.InvalidateShape(shapeView);

    public static void MapShape(IShapeViewHandler handler, IShapeView shapeView) =>
        ShapePlatformView(handler)?.UpdateShape(shapeView);

    public static void MapAspect(IShapeViewHandler handler, IShapeView shapeView) =>
        ShapePlatformView(handler)?.InvalidateShape(shapeView);

    public static void MapFill(IShapeViewHandler handler, IShapeView shapeView) =>
        ShapePlatformView(handler)?.InvalidateShape(shapeView);

    public static void MapStroke(IShapeViewHandler handler, IShapeView shapeView) =>
        ShapePlatformView(handler)?.InvalidateShape(shapeView);

    public static void MapStrokeThickness(IShapeViewHandler handler, IShapeView shapeView) =>
        ShapePlatformView(handler)?.InvalidateShape(shapeView);

    public static void MapStrokeDashPattern(IShapeViewHandler handler, IShapeView shapeView) =>
        ShapePlatformView(handler)?.InvalidateShape(shapeView);

    public static void MapStrokeDashOffset(IShapeViewHandler handler, IShapeView shapeView) =>
        ShapePlatformView(handler)?.InvalidateShape(shapeView);

    public static void MapStrokeLineCap(IShapeViewHandler handler, IShapeView shapeView) =>
        ShapePlatformView(handler)?.InvalidateShape(shapeView);

    public static void MapStrokeLineJoin(IShapeViewHandler handler, IShapeView shapeView) =>
        ShapePlatformView(handler)?.InvalidateShape(shapeView);

    public static void MapStrokeMiterLimit(IShapeViewHandler handler, IShapeView shapeView) =>
        ShapePlatformView(handler)?.InvalidateShape(shapeView);
}
