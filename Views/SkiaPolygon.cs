// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered Polygon shape - closed path of connected points.
/// </summary>
public class SkiaPolygon : SkiaView
{
    public static readonly BindableProperty PointsProperty =
        BindableProperty.Create(nameof(Points), typeof(PointCollection), typeof(SkiaPolygon), null,
            propertyChanged: (b, o, n) => ((SkiaPolygon)b).Invalidate());

    public static readonly BindableProperty FillProperty =
        BindableProperty.Create(nameof(Fill), typeof(Brush), typeof(SkiaPolygon), null,
            propertyChanged: (b, o, n) => ((SkiaPolygon)b).Invalidate());

    public static readonly BindableProperty StrokeProperty =
        BindableProperty.Create(nameof(Stroke), typeof(Brush), typeof(SkiaPolygon), null,
            propertyChanged: (b, o, n) => ((SkiaPolygon)b).Invalidate());

    public static readonly BindableProperty StrokeThicknessProperty =
        BindableProperty.Create(nameof(StrokeThickness), typeof(double), typeof(SkiaPolygon), 1.0,
            propertyChanged: (b, o, n) => ((SkiaPolygon)b).Invalidate());

    public static readonly BindableProperty FillRuleProperty =
        BindableProperty.Create(nameof(FillRule), typeof(FillRule), typeof(SkiaPolygon), FillRule.EvenOdd,
            propertyChanged: (b, o, n) => ((SkiaPolygon)b).Invalidate());

    public PointCollection? Points { get => (PointCollection?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public Brush? Fill { get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public Brush? Stroke { get => (Brush?)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public FillRule FillRule { get => (FillRule)GetValue(FillRuleProperty); set => SetValue(FillRuleProperty, value); }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var pts = Points;
        if (pts == null || pts.Count < 2) return;

        using var path = new SKPath();
        path.FillType = FillRule == FillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        path.MoveTo((float)pts[0].X, (float)pts[0].Y);
        for (int i = 1; i < pts.Count; i++)
            path.LineTo((float)pts[i].X, (float)pts[i].Y);
        path.Close();

        var fillColor = BrushToSKColor(Fill);
        if (fillColor != SKColors.Transparent)
        {
            using var fillPaint = new SKPaint { Color = fillColor, Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawPath(path, fillPaint);
        }

        var strokeColor = BrushToSKColor(Stroke);
        if (strokeColor != SKColors.Transparent && StrokeThickness > 0)
        {
            using var strokePaint = new SKPaint { Color = strokeColor, Style = SKPaintStyle.Stroke, StrokeWidth = (float)StrokeThickness, IsAntialias = true };
            canvas.DrawPath(path, strokePaint);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var pts = Points;
        if (pts == null || pts.Count == 0) return new Size(40, 40);
        double maxX = 0, maxY = 0;
        foreach (var p in pts) { maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); }
        return new Size(maxX + StrokeThickness, maxY + StrokeThickness);
    }

    private static SKColor BrushToSKColor(Brush? brush)
    {
        if (brush is SolidColorBrush s && s.Color != null) return s.Color.ToSKColor();
        return SKColors.Transparent;
    }
}
