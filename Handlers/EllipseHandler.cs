// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Ellipse (Microsoft.Maui.Controls.Shapes.Ellipse) on Linux.
/// Maps MAUI shape properties to SkiaEllipse for rendering.
/// </summary>
public partial class EllipseHandler : ViewHandler<Ellipse, SkiaEllipse>
{
    public static IPropertyMapper<Ellipse, EllipseHandler> Mapper =
        new PropertyMapper<Ellipse, EllipseHandler>(ViewMapper)
        {
            [nameof(Ellipse.Fill)] = MapFill,
            [nameof(Ellipse.Stroke)] = MapStroke,
            [nameof(Ellipse.StrokeThickness)] = MapStrokeThickness,
            [nameof(Ellipse.Aspect)] = MapAspect,
            [nameof(IView.Background)] = MapBackground,
            ["BackgroundColor"] = MapBackgroundColor,
        };

    public EllipseHandler() : base(Mapper)
    {
    }

    protected override SkiaEllipse CreatePlatformView()
    {
        return new SkiaEllipse();
    }

    protected override void ConnectHandler(SkiaEllipse platformView)
    {
        base.ConnectHandler(platformView);

        if (VirtualView is Ellipse ellipse)
        {
            if (ellipse.WidthRequest >= 0)
                platformView.WidthRequest = ellipse.WidthRequest;
            if (ellipse.HeightRequest >= 0)
                platformView.HeightRequest = ellipse.HeightRequest;
        }
    }

    public static void MapFill(EllipseHandler handler, Ellipse ellipse)
    {
        handler.PlatformView.Fill = ellipse.Fill;
        handler.PlatformView.Invalidate();
    }

    public static void MapStroke(EllipseHandler handler, Ellipse ellipse)
    {
        handler.PlatformView.Stroke = ellipse.Stroke;
        handler.PlatformView.Invalidate();
    }

    public static void MapStrokeThickness(EllipseHandler handler, Ellipse ellipse)
    {
        handler.PlatformView.StrokeThickness = ellipse.StrokeThickness;
        handler.PlatformView.Invalidate();
    }

    public static void MapAspect(EllipseHandler handler, Ellipse ellipse)
    {
        handler.PlatformView.Aspect = ellipse.Aspect;
        handler.PlatformView.Invalidate();
    }

    public static void MapBackground(EllipseHandler handler, Ellipse ellipse)
    {
        if (ellipse.Background is SolidColorBrush solidBrush && solidBrush.Color != null)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color;
            handler.PlatformView.Invalidate();
        }
    }

    public static void MapBackgroundColor(EllipseHandler handler, Ellipse ellipse)
    {
        if (ellipse.BackgroundColor != null)
        {
            handler.PlatformView.BackgroundColor = ellipse.BackgroundColor;
            handler.PlatformView.Invalidate();
        }
    }
}
