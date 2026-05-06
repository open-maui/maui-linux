// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public partial class RectangleHandler : ViewHandler<Microsoft.Maui.Controls.Shapes.Rectangle, SkiaRectangle>
{
    public static IPropertyMapper<Microsoft.Maui.Controls.Shapes.Rectangle, RectangleHandler> Mapper =
        new PropertyMapper<Microsoft.Maui.Controls.Shapes.Rectangle, RectangleHandler>(ViewMapper)
        {
            [nameof(Microsoft.Maui.Controls.Shapes.Rectangle.Fill)] = MapFill,
            [nameof(Microsoft.Maui.Controls.Shapes.Rectangle.Stroke)] = MapStroke,
            [nameof(Microsoft.Maui.Controls.Shapes.Rectangle.StrokeThickness)] = MapStrokeThickness,
            [nameof(Microsoft.Maui.Controls.Shapes.Rectangle.RadiusX)] = MapRadiusX,
            [nameof(Microsoft.Maui.Controls.Shapes.Rectangle.RadiusY)] = MapRadiusY,
        };

    public RectangleHandler() : base(Mapper) { }

    protected override SkiaRectangle CreatePlatformView() => new();

    protected override void ConnectHandler(SkiaRectangle pv)
    {
        base.ConnectHandler(pv);
        if (VirtualView is Microsoft.Maui.Controls.Shapes.Rectangle r) { if (r.WidthRequest >= 0) pv.WidthRequest = r.WidthRequest; if (r.HeightRequest >= 0) pv.HeightRequest = r.HeightRequest; }
    }

    public static void MapFill(RectangleHandler h, Microsoft.Maui.Controls.Shapes.Rectangle r) { h.PlatformView.Fill = r.Fill; h.PlatformView.Invalidate(); }
    public static void MapStroke(RectangleHandler h, Microsoft.Maui.Controls.Shapes.Rectangle r) { h.PlatformView.Stroke = r.Stroke; h.PlatformView.Invalidate(); }
    public static void MapStrokeThickness(RectangleHandler h, Microsoft.Maui.Controls.Shapes.Rectangle r) { h.PlatformView.StrokeThickness = r.StrokeThickness; h.PlatformView.Invalidate(); }
    public static void MapRadiusX(RectangleHandler h, Microsoft.Maui.Controls.Shapes.Rectangle r) { h.PlatformView.RadiusX = r.RadiusX; h.PlatformView.Invalidate(); }
    public static void MapRadiusY(RectangleHandler h, Microsoft.Maui.Controls.Shapes.Rectangle r) { h.PlatformView.RadiusY = r.RadiusY; h.PlatformView.Invalidate(); }
}
