// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Specialized;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public partial class PolylineHandler : LinuxViewHandler<Polyline, SkiaPolyline>, IShapeViewHandler
{
    public static IPropertyMapper<Polyline, PolylineHandler> Mapper =
        new PropertyMapper<Polyline, PolylineHandler>(ShapeViewHandler.Mapper)
        {
            [nameof(Polyline.Points)] = MapPoints,
            [nameof(Polyline.Stroke)] = MapStroke,
            [nameof(Polyline.StrokeThickness)] = MapStrokeThickness,
            [nameof(Polyline.StrokeDashArray)] = MapStrokeDashArray,
            [nameof(Polyline.StrokeDashOffset)] = MapStrokeDashOffset,
            [nameof(Polyline.Fill)] = MapFill,
            [nameof(Polyline.FillRule)] = MapFillRule,
        };

    public PolylineHandler() : base(Mapper) { }

    protected override SkiaPolyline CreatePlatformView() => new();

    public static void MapPoints(PolylineHandler h, Polyline p) { h.UpdatePointsSubscription(p.Points); h.PlatformView.Points = p.Points; h.PlatformView.Invalidate(); }
    public static void MapStroke(PolylineHandler h, Polyline p) { h.PlatformView.Stroke = p.Stroke; h.PlatformView.Invalidate(); }
    public static void MapStrokeThickness(PolylineHandler h, Polyline p) { h.PlatformView.StrokeThickness = p.StrokeThickness; h.PlatformView.Invalidate(); }
    public static void MapFillRule(PolylineHandler h, Polyline p) { h.PlatformView.ShapeWindingMode = p.FillRule == FillRule.EvenOdd ? WindingMode.EvenOdd : WindingMode.NonZero; h.PlatformView.Invalidate(); }
    public static void MapFill(PolylineHandler h, Polyline p) { h.PlatformView.Fill = p.Fill; h.PlatformView.Invalidate(); }
    public static void MapStrokeDashArray(PolylineHandler h, Polyline p) { h.PlatformView.StrokeDashArray = p.StrokeDashArray; h.PlatformView.Invalidate(); }
    public static void MapStrokeDashOffset(PolylineHandler h, Polyline p) { h.PlatformView.StrokeDashOffset = p.StrokeDashOffset; h.PlatformView.Invalidate(); }

    IShapeView IShapeViewHandler.VirtualView => VirtualView;

    object IShapeViewHandler.PlatformView => PlatformView;

    protected override void DisconnectHandler(SkiaPolyline platformView)
    {
        ClearPointsSubscription();
        platformView.ClearShape();
        base.DisconnectHandler(platformView);
    }

    // A change inside the Points collection redraws the shape, as MAUI's PolylineHandler
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
        UpdateValue(nameof(Polyline.Points));
}
