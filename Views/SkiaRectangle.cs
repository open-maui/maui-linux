// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered Rectangle shape.
/// </summary>
public class SkiaRectangle : SkiaView
{
    public static readonly BindableProperty FillProperty =
        BindableProperty.Create(nameof(Fill), typeof(Brush), typeof(SkiaRectangle), null,
            propertyChanged: (b, o, n) => ((SkiaRectangle)b).Invalidate());

    public static readonly BindableProperty StrokeProperty =
        BindableProperty.Create(nameof(Stroke), typeof(Brush), typeof(SkiaRectangle), null,
            propertyChanged: (b, o, n) => ((SkiaRectangle)b).Invalidate());

    public static readonly BindableProperty StrokeThicknessProperty =
        BindableProperty.Create(nameof(StrokeThickness), typeof(double), typeof(SkiaRectangle), 0.0,
            propertyChanged: (b, o, n) => ((SkiaRectangle)b).Invalidate());

    public static readonly BindableProperty RadiusXProperty =
        BindableProperty.Create(nameof(RadiusX), typeof(double), typeof(SkiaRectangle), 0.0,
            propertyChanged: (b, o, n) => ((SkiaRectangle)b).Invalidate());

    public static readonly BindableProperty RadiusYProperty =
        BindableProperty.Create(nameof(RadiusY), typeof(double), typeof(SkiaRectangle), 0.0,
            propertyChanged: (b, o, n) => ((SkiaRectangle)b).Invalidate());

    public Brush? Fill { get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public Brush? Stroke { get => (Brush?)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public double RadiusX { get => (double)GetValue(RadiusXProperty); set => SetValue(RadiusXProperty, value); }
    public double RadiusY { get => (double)GetValue(RadiusYProperty); set => SetValue(RadiusYProperty, value); }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var halfStroke = (float)StrokeThickness / 2f;
        var rect = new SKRect(bounds.Left + halfStroke, bounds.Top + halfStroke, bounds.Right - halfStroke, bounds.Bottom - halfStroke);

        var fillColor = BrushToSKColor(Fill);
        if (fillColor == SKColors.Transparent && BackgroundColor != null)
            fillColor = BackgroundColor.ToSKColor();

        if (fillColor != SKColors.Transparent)
        {
            using var fillPaint = new SKPaint { Color = fillColor, Style = SKPaintStyle.Fill, IsAntialias = true };
            if (RadiusX > 0 || RadiusY > 0)
                canvas.DrawRoundRect(rect, (float)RadiusX, (float)RadiusY, fillPaint);
            else
                canvas.DrawRect(rect, fillPaint);
        }

        if (StrokeThickness > 0)
        {
            var strokeColor = BrushToSKColor(Stroke);
            if (strokeColor != SKColors.Transparent)
            {
                using var strokePaint = new SKPaint { Color = strokeColor, Style = SKPaintStyle.Stroke, StrokeWidth = (float)StrokeThickness, IsAntialias = true };
                if (RadiusX > 0 || RadiusY > 0)
                    canvas.DrawRoundRect(rect, (float)RadiusX, (float)RadiusY, strokePaint);
                else
                    canvas.DrawRect(rect, strokePaint);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = WidthRequest >= 0 ? WidthRequest : (double.IsInfinity(availableSize.Width) ? 40 : availableSize.Width);
        var h = HeightRequest >= 0 ? HeightRequest : (double.IsInfinity(availableSize.Height) ? 40 : availableSize.Height);
        if (double.IsNaN(w)) w = 40;
        if (double.IsNaN(h)) h = 40;
        return new Size(w, h);
    }

    private static SKColor BrushToSKColor(Brush? brush)
    {
        if (brush is SolidColorBrush s && s.Color != null) return s.Color.ToSKColor();
        return SKColors.Transparent;
    }
}
