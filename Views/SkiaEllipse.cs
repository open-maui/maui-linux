// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered Ellipse - draws a filled/stroked ellipse (or circle when width == height).
/// Implements MAUI Shapes.Ellipse patterns.
/// </summary>
public class SkiaEllipse : SkiaView
{
    #region BindableProperties

    public static readonly BindableProperty FillProperty =
        BindableProperty.Create(nameof(Fill), typeof(Brush), typeof(SkiaEllipse), null,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaEllipse)b).Invalidate());

    public static readonly BindableProperty StrokeProperty =
        BindableProperty.Create(nameof(Stroke), typeof(Brush), typeof(SkiaEllipse), null,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaEllipse)b).Invalidate());

    public static readonly BindableProperty StrokeThicknessProperty =
        BindableProperty.Create(nameof(StrokeThickness), typeof(double), typeof(SkiaEllipse), 0.0,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaEllipse)b).Invalidate());

    public static readonly BindableProperty AspectProperty =
        BindableProperty.Create(nameof(Aspect), typeof(Stretch), typeof(SkiaEllipse), Stretch.None,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaEllipse)b).Invalidate());

    #endregion

    #region Properties

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public Stretch Aspect
    {
        get => (Stretch)GetValue(AspectProperty);
        set => SetValue(AspectProperty, value);
    }

    #endregion

    #region Helper Methods

    private static SKColor BrushToSKColor(Brush? brush)
    {
        if (brush is SolidColorBrush solidBrush && solidBrush.Color != null)
            return solidBrush.Color.ToSKColor();
        return SKColors.Transparent;
    }

    private static SKColor ColorToSKColor(Color? color)
    {
        if (color == null) return SKColors.Transparent;
        return color.ToSKColor();
    }

    #endregion

    #region Drawing

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var strokeWidth = (float)StrokeThickness;
        var halfStroke = strokeWidth / 2f;

        // Inset bounds by half the stroke so the stroke doesn't clip outside.
        var ellipseBounds = new SKRect(
            bounds.Left + halfStroke,
            bounds.Top + halfStroke,
            bounds.Right - halfStroke,
            bounds.Bottom - halfStroke);

        // Draw fill.
        var fillColor = BrushToSKColor(Fill);
        if (fillColor == SKColors.Transparent && BackgroundColor != null)
            fillColor = ColorToSKColor(BackgroundColor);

        if (fillColor != SKColors.Transparent)
        {
            using var fillPaint = new SKPaint
            {
                Color = fillColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
            };
            canvas.DrawOval(ellipseBounds, fillPaint);
        }

        // Draw stroke.
        if (strokeWidth > 0)
        {
            var strokeColor = BrushToSKColor(Stroke);
            if (strokeColor != SKColors.Transparent)
            {
                using var strokePaint = new SKPaint
                {
                    Color = strokeColor,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = strokeWidth,
                    IsAntialias = true,
                };
                canvas.DrawOval(ellipseBounds, strokePaint);
            }
        }
    }

    #endregion

    #region Measurement

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = WidthRequest >= 0 ? WidthRequest :
                    (double.IsInfinity(availableSize.Width) ? 40.0 : availableSize.Width);
        var height = HeightRequest >= 0 ? HeightRequest :
                     (double.IsInfinity(availableSize.Height) ? 40.0 : availableSize.Height);

        if (double.IsNaN(width)) width = 40.0;
        if (double.IsNaN(height)) height = 40.0;

        return new Size(width, height);
    }

    #endregion
}
