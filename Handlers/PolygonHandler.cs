// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Specialized;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public partial class PolygonHandler : LinuxViewHandler<Polygon, SkiaPolygon>, IShapeViewHandler
{
    public static IPropertyMapper<Polygon, PolygonHandler> Mapper =
        new PropertyMapper<Polygon, PolygonHandler>(ShapeViewHandler.Mapper)
        {
            [nameof(Polygon.Points)] = MapPoints,
            [nameof(Polygon.Fill)] = MapFill,
            [nameof(Polygon.Stroke)] = MapStroke,
            [nameof(Polygon.StrokeThickness)] = MapStrokeThickness,
            [nameof(Polygon.StrokeDashArray)] = MapStrokeDashArray,
            [nameof(Polygon.StrokeDashOffset)] = MapStrokeDashOffset,
            [nameof(Polygon.FillRule)] = MapFillRule,
        };

    public PolygonHandler() : base(Mapper) { }

    protected override SkiaPolygon CreatePlatformView() => new();

    public static void MapPoints(PolygonHandler h, Polygon p) { h.UpdatePointsSubscription(p.Points); h.PlatformView.Points = p.Points; h.PlatformView.Invalidate(); }
    public static void MapFill(PolygonHandler h, Polygon p) { h.PlatformView.Fill = p.Fill; h.PlatformView.Invalidate(); }
    public static void MapStroke(PolygonHandler h, Polygon p) { h.PlatformView.Stroke = p.Stroke; h.PlatformView.Invalidate(); }
    public static void MapStrokeThickness(PolygonHandler h, Polygon p) { h.PlatformView.StrokeThickness = p.StrokeThickness; h.PlatformView.Invalidate(); }
    public static void MapFillRule(PolygonHandler h, Polygon p) { h.PlatformView.FillRule = p.FillRule; h.PlatformView.ShapeWindingMode = p.FillRule == FillRule.EvenOdd ? WindingMode.EvenOdd : WindingMode.NonZero; h.PlatformView.Invalidate(); }
    public static void MapStrokeDashArray(PolygonHandler h, Polygon p) { h.PlatformView.StrokeDashArray = p.StrokeDashArray; h.PlatformView.Invalidate(); }
    public static void MapStrokeDashOffset(PolygonHandler h, Polygon p) { h.PlatformView.StrokeDashOffset = p.StrokeDashOffset; h.PlatformView.Invalidate(); }

    IShapeView IShapeViewHandler.VirtualView => VirtualView;

    object IShapeViewHandler.PlatformView => PlatformView;

    protected override void DisconnectHandler(SkiaPolygon platformView)
    {
        ClearPointsSubscription();
        platformView.ClearShape();
        base.DisconnectHandler(platformView);
    }

    // A change inside the Points collection redraws the shape, as MAUI's PolygonHandler
    // does (the shape raises no property change for it).
    private PointCollection? _subscribedPoints;

    private void UpdatePointsSubscription(PointCollection? points)
    {
        if (ReferenceEquals(_subscribedPoints, points))
            return;
        ClearPointsSubscription();
        if (points is null)
            return;
        _subscribedPoints = points;
        _subscribedPoints.CollectionChanged += OnPointsCollectionChanged;
    }

    private void ClearPointsSubscription()
    {
        if (_subscribedPoints is null)
            return;
        _subscribedPoints.CollectionChanged -= OnPointsCollectionChanged;
        _subscribedPoints = null;
    }

    private void OnPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        UpdateValue(nameof(Polygon.Points));
}
