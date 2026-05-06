// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered Line shape.
/// </summary>
public class SkiaLine : SkiaView
{
    public static readonly BindableProperty X1Property =
        BindableProperty.Create(nameof(X1), typeof(double), typeof(SkiaLine), 0.0,
            propertyChanged: (b, o, n) => ((SkiaLine)b).Invalidate());

    public static readonly BindableProperty Y1Property =
        BindableProperty.Create(nameof(Y1), typeof(double), typeof(SkiaLine), 0.0,
            propertyChanged: (b, o, n) => ((SkiaLine)b).Invalidate());

    public static readonly BindableProperty X2Property =
        BindableProperty.Create(nameof(X2), typeof(double), typeof(SkiaLine), 0.0,
            propertyChanged: (b, o, n) => ((SkiaLine)b).Invalidate());

    public static readonly BindableProperty Y2Property =
        BindableProperty.Create(nameof(Y2), typeof(double), typeof(SkiaLine), 0.0,
            propertyChanged: (b, o, n) => ((SkiaLine)b).Invalidate());

    public static readonly BindableProperty StrokeProperty =
        BindableProperty.Create(nameof(Stroke), typeof(Brush), typeof(SkiaLine), null,
            propertyChanged: (b, o, n) => ((SkiaLine)b).Invalidate());

    public static readonly BindableProperty StrokeThicknessProperty =
        BindableProperty.Create(nameof(StrokeThickness), typeof(double), typeof(SkiaLine), 1.0,
            propertyChanged: (b, o, n) => ((SkiaLine)b).Invalidate());

    public double X1 { get => (double)GetValue(X1Property); set => SetValue(X1Property, value); }
    public double Y1 { get => (double)GetValue(Y1Property); set => SetValue(Y1Property, value); }
    public double X2 { get => (double)GetValue(X2Property); set => SetValue(X2Property, value); }
    public double Y2 { get => (double)GetValue(Y2Property); set => SetValue(Y2Property, value); }
    public Brush? Stroke { get => (Brush?)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var strokeColor = BrushToSKColor(Stroke);
        if (strokeColor == SKColors.Transparent) return;

        using var paint = new SKPaint
        {
            Color = strokeColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)StrokeThickness,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
        };
        canvas.DrawLine((float)X1, (float)Y1, (float)X2, (float)Y2, paint);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = Math.Max(Math.Abs(X2 - X1), 1) + StrokeThickness;
        var h = Math.Max(Math.Abs(Y2 - Y1), 1) + StrokeThickness;
        if (WidthRequest >= 0) w = WidthRequest;
        if (HeightRequest >= 0) h = HeightRequest;
        return new Size(w, h);
    }

    private static SKColor BrushToSKColor(Brush? brush)
    {
        if (brush is SolidColorBrush s && s.Color != null) return s.Color.ToSKColor();
        return SKColors.Transparent;
    }
}
