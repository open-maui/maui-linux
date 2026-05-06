// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public partial class PolylineHandler : ViewHandler<Polyline, SkiaPolyline>
{
    public static IPropertyMapper<Polyline, PolylineHandler> Mapper =
        new PropertyMapper<Polyline, PolylineHandler>(ViewMapper)
        {
            [nameof(Polyline.Points)] = MapPoints,
            [nameof(Polyline.Stroke)] = MapStroke,
            [nameof(Polyline.StrokeThickness)] = MapStrokeThickness,
            [nameof(Polyline.Fill)] = MapFill,
        };

    public PolylineHandler() : base(Mapper) { }

    protected override SkiaPolyline CreatePlatformView() => new();

    public static void MapPoints(PolylineHandler h, Polyline p) { h.PlatformView.Points = p.Points; h.PlatformView.Invalidate(); }
    public static void MapStroke(PolylineHandler h, Polyline p) { h.PlatformView.Stroke = p.Stroke; h.PlatformView.Invalidate(); }
    public static void MapStrokeThickness(PolylineHandler h, Polyline p) { h.PlatformView.StrokeThickness = p.StrokeThickness; h.PlatformView.Invalidate(); }
    public static void MapFill(PolylineHandler h, Polyline p) { h.PlatformView.Fill = p.Fill; h.PlatformView.Invalidate(); }
}
