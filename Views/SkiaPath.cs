// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered Path shape - arbitrary geometry via SVG-style path data.
/// Converts MAUI Geometry to an SVG path string and parses it with SkiaSharp.
/// </summary>
public class SkiaPath : SkiaView
{
    public static readonly BindableProperty DataProperty =
        BindableProperty.Create(nameof(Data), typeof(Geometry), typeof(SkiaPath), null,
            propertyChanged: (b, o, n) => { ((SkiaPath)b)._cachedPath = null; ((SkiaPath)b).Invalidate(); });

    public static readonly BindableProperty FillProperty =
        BindableProperty.Create(nameof(Fill), typeof(Brush), typeof(SkiaPath), null,
            propertyChanged: (b, o, n) => ((SkiaPath)b).Invalidate());

    public static readonly BindableProperty StrokeProperty =
        BindableProperty.Create(nameof(Stroke), typeof(Brush), typeof(SkiaPath), null,
            propertyChanged: (b, o, n) => ((SkiaPath)b).Invalidate());

    public static readonly BindableProperty StrokeThicknessProperty =
        BindableProperty.Create(nameof(StrokeThickness), typeof(double), typeof(SkiaPath), 1.0,
            propertyChanged: (b, o, n) => ((SkiaPath)b).Invalidate());

    public Geometry? Data { get => (Geometry?)GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public Brush? Fill { get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public Brush? Stroke { get => (Brush?)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    private SKPath? _cachedPath;

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var skPath = GetOrBuildPath();
        if (skPath == null) return;

        var fillColor = BrushToSKColor(Fill);
        if (fillColor != SKColors.Transparent)
        {
            using var fillPaint = new SKPaint { Color = fillColor, Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawPath(skPath, fillPaint);
        }

        var strokeColor = BrushToSKColor(Stroke);
        if (strokeColor != SKColors.Transparent && StrokeThickness > 0)
        {
            using var strokePaint = new SKPaint { Color = strokeColor, Style = SKPaintStyle.Stroke, StrokeWidth = (float)StrokeThickness, IsAntialias = true };
            canvas.DrawPath(skPath, strokePaint);
        }
    }

    private SKPath? GetOrBuildPath()
    {
        if (_cachedPath != null) return _cachedPath;

        var data = Data;
        if (data == null) return null;

        // Try to get SVG path string from the Geometry.
        // PathGeometry and StreamGeometry expose path data that can be converted.
        try
        {
            var svgData = data switch
            {
                PathGeometry pg => ConvertPathGeometry(pg),
                _ => null,
            };

            if (svgData != null)
            {
                _cachedPath = SKPath.ParseSvgPathData(svgData);
            }
        }
        catch
        {
            // Unsupported geometry type — render nothing.
        }

        return _cachedPath;
    }

    private static string? ConvertPathGeometry(PathGeometry pg)
    {
        if (pg.Figures == null || pg.Figures.Count == 0) return null;

        var sb = new System.Text.StringBuilder();
        foreach (var figure in pg.Figures)
        {
            sb.Append($"M {figure.StartPoint.X} {figure.StartPoint.Y} ");
            foreach (var segment in figure.Segments)
            {
                switch (segment)
                {
                    case LineSegment line:
                        sb.Append($"L {line.Point.X} {line.Point.Y} ");
                        break;
                    case BezierSegment bezier:
                        sb.Append($"C {bezier.Point1.X} {bezier.Point1.Y} {bezier.Point2.X} {bezier.Point2.Y} {bezier.Point3.X} {bezier.Point3.Y} ");
                        break;
                    case QuadraticBezierSegment quad:
                        sb.Append($"Q {quad.Point1.X} {quad.Point1.Y} {quad.Point2.X} {quad.Point2.Y} ");
                        break;
                    case ArcSegment arc:
                        var sweep = arc.SweepDirection == SweepDirection.Clockwise ? 1 : 0;
                        var largeArc = arc.IsLargeArc ? 1 : 0;
                        sb.Append($"A {arc.Size.Width} {arc.Size.Height} {arc.RotationAngle} {largeArc} {sweep} {arc.Point.X} {arc.Point.Y} ");
                        break;
                    case PolyLineSegment polyLine:
                        foreach (var pt in polyLine.Points)
                            sb.Append($"L {pt.X} {pt.Y} ");
                        break;
                    case PolyBezierSegment polyBezier:
                        for (int i = 0; i + 2 < polyBezier.Points.Count; i += 3)
                            sb.Append($"C {polyBezier.Points[i].X} {polyBezier.Points[i].Y} {polyBezier.Points[i + 1].X} {polyBezier.Points[i + 1].Y} {polyBezier.Points[i + 2].X} {polyBezier.Points[i + 2].Y} ");
                        break;
                    case PolyQuadraticBezierSegment polyQuad:
                        for (int i = 0; i + 1 < polyQuad.Points.Count; i += 2)
                            sb.Append($"Q {polyQuad.Points[i].X} {polyQuad.Points[i].Y} {polyQuad.Points[i + 1].X} {polyQuad.Points[i + 1].Y} ");
                        break;
                }
            }
            if (figure.IsClosed) sb.Append("Z ");
        }
        var result = sb.ToString().Trim();
        return result.Length > 0 ? result : null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var skPath = GetOrBuildPath();
        if (skPath != null)
        {
            var pathBounds = skPath.Bounds;
            var w = WidthRequest >= 0 ? WidthRequest : pathBounds.Width + StrokeThickness;
            var h = HeightRequest >= 0 ? HeightRequest : pathBounds.Height + StrokeThickness;
            return new Size(Math.Max(w, 1), Math.Max(h, 1));
        }

        var fw = WidthRequest >= 0 ? WidthRequest : (double.IsInfinity(availableSize.Width) ? 40 : availableSize.Width);
        var fh = HeightRequest >= 0 ? HeightRequest : (double.IsInfinity(availableSize.Height) ? 40 : availableSize.Height);
        if (double.IsNaN(fw)) fw = 40;
        if (double.IsNaN(fh)) fh = 40;
        return new Size(fw, fh);
    }

    private static SKColor BrushToSKColor(Brush? brush)
    {
        if (brush is SolidColorBrush s && s.Color != null) return s.Color.ToSKColor();
        return SKColors.Transparent;
    }
}
