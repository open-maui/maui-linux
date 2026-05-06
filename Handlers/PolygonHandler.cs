// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public partial class PolygonHandler : ViewHandler<Polygon, SkiaPolygon>
{
    public static IPropertyMapper<Polygon, PolygonHandler> Mapper =
        new PropertyMapper<Polygon, PolygonHandler>(ViewMapper)
        {
            [nameof(Polygon.Points)] = MapPoints,
            [nameof(Polygon.Fill)] = MapFill,
            [nameof(Polygon.Stroke)] = MapStroke,
            [nameof(Polygon.StrokeThickness)] = MapStrokeThickness,
            [nameof(Polygon.FillRule)] = MapFillRule,
        };

    public PolygonHandler() : base(Mapper) { }

    protected override SkiaPolygon CreatePlatformView() => new();

    public static void MapPoints(PolygonHandler h, Polygon p) { h.PlatformView.Points = p.Points; h.PlatformView.Invalidate(); }
    public static void MapFill(PolygonHandler h, Polygon p) { h.PlatformView.Fill = p.Fill; h.PlatformView.Invalidate(); }
    public static void MapStroke(PolygonHandler h, Polygon p) { h.PlatformView.Stroke = p.Stroke; h.PlatformView.Invalidate(); }
    public static void MapStrokeThickness(PolygonHandler h, Polygon p) { h.PlatformView.StrokeThickness = p.StrokeThickness; h.PlatformView.Invalidate(); }
    public static void MapFillRule(PolygonHandler h, Polygon p) { h.PlatformView.FillRule = p.FillRule; h.PlatformView.Invalidate(); }
}
