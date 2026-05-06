// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using ShapePath = Microsoft.Maui.Controls.Shapes.Path;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public partial class PathHandler : ViewHandler<ShapePath, SkiaPath>
{
    public static IPropertyMapper<ShapePath, PathHandler> Mapper =
        new PropertyMapper<ShapePath, PathHandler>(ViewMapper)
        {
            [nameof(ShapePath.Data)] = MapData,
            [nameof(ShapePath.Fill)] = MapFill,
            [nameof(ShapePath.Stroke)] = MapStroke,
            [nameof(ShapePath.StrokeThickness)] = MapStrokeThickness,
        };

    public PathHandler() : base(Mapper) { }

    protected override SkiaPath CreatePlatformView() => new();

    public static void MapData(PathHandler h, ShapePath p) { h.PlatformView.Data = p.Data; h.PlatformView.Invalidate(); }
    public static void MapFill(PathHandler h, ShapePath p) { h.PlatformView.Fill = p.Fill; h.PlatformView.Invalidate(); }
    public static void MapStroke(PathHandler h, ShapePath p) { h.PlatformView.Stroke = p.Stroke; h.PlatformView.Invalidate(); }
    public static void MapStrokeThickness(PathHandler h, ShapePath p) { h.PlatformView.StrokeThickness = p.StrokeThickness; h.PlatformView.Invalidate(); }
}
