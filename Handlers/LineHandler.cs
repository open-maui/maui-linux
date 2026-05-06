// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public partial class LineHandler : ViewHandler<Line, SkiaLine>
{
    public static IPropertyMapper<Line, LineHandler> Mapper =
        new PropertyMapper<Line, LineHandler>(ViewMapper)
        {
            [nameof(Line.X1)] = MapX1,
            [nameof(Line.Y1)] = MapY1,
            [nameof(Line.X2)] = MapX2,
            [nameof(Line.Y2)] = MapY2,
            [nameof(Line.Stroke)] = MapStroke,
            [nameof(Line.StrokeThickness)] = MapStrokeThickness,
        };

    public LineHandler() : base(Mapper) { }

    protected override SkiaLine CreatePlatformView() => new();

    protected override void ConnectHandler(SkiaLine pv)
    {
        base.ConnectHandler(pv);
        if (VirtualView is Line l) { if (l.WidthRequest >= 0) pv.WidthRequest = l.WidthRequest; if (l.HeightRequest >= 0) pv.HeightRequest = l.HeightRequest; }
    }

    public static void MapX1(LineHandler h, Line l) { h.PlatformView.X1 = l.X1; h.PlatformView.Invalidate(); }
    public static void MapY1(LineHandler h, Line l) { h.PlatformView.Y1 = l.Y1; h.PlatformView.Invalidate(); }
    public static void MapX2(LineHandler h, Line l) { h.PlatformView.X2 = l.X2; h.PlatformView.Invalidate(); }
    public static void MapY2(LineHandler h, Line l) { h.PlatformView.Y2 = l.Y2; h.PlatformView.Invalidate(); }
    public static void MapStroke(LineHandler h, Line l) { h.PlatformView.Stroke = l.Stroke; h.PlatformView.Invalidate(); }
    public static void MapStrokeThickness(LineHandler h, Line l) { h.PlatformView.StrokeThickness = l.StrokeThickness; h.PlatformView.Invalidate(); }
}
