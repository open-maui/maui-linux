// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered BoxView - a simple colored rectangle.
/// Implements MAUI IBoxView interface patterns.
/// </summary>
public class SkiaBoxView : SkiaView
{
    #region BindableProperties

    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(nameof(Color), typeof(Color), typeof(SkiaBoxView), Colors.Transparent,
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBoxView)b).Invalidate());

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(CornerRadius), typeof(SkiaBoxView), new CornerRadius(0),
            BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaBoxView)b).Invalidate());

    #endregion

    #region Properties

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Converts a MAUI Color to SkiaSharp SKColor.
    /// </summary>
    private static SKColor ToSKColor(Color? color)
    {
        if (color == null) return SKColors.Transparent;
        return new SKColor(
            (byte)(color.Red * 255),
            (byte)(color.Green * 255),
            (byte)(color.Blue * 255),
            (byte)(color.Alpha * 255));
    }

    #endregion

    #region Drawing

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        SKColor fillColor = ToSKColor(Color);

        using var paint = new SKPaint
        {
            Color = fillColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // Check if any corner radius is set
        var cr = CornerRadius;
        bool hasRadius = cr.TopLeft > 0 || cr.TopRight > 0 || cr.BottomLeft > 0 || cr.BottomRight > 0;

        if (hasRadius)
        {
            // Check if all corners are the same (uniform radius)
            if (cr.TopLeft == cr.TopRight && cr.TopRight == cr.BottomRight && cr.BottomRight == cr.BottomLeft)
            {
                canvas.DrawRoundRect(bounds, (float)cr.TopLeft, (float)cr.TopLeft, paint);
            }
            else
            {
                // Different corner radii - use SKRoundRect with individual radii
                var radii = new SKPoint[]
                {
                    new SKPoint((float)cr.TopLeft, (float)cr.TopLeft),
                    new SKPoint((float)cr.TopRight, (float)cr.TopRight),
                    new SKPoint((float)cr.BottomRight, (float)cr.BottomRight),
                    new SKPoint((float)cr.BottomLeft, (float)cr.BottomLeft)
                };
                var roundRect = new SKRoundRect();
                roundRect.SetRectRadii(bounds, radii);
                canvas.DrawRoundRect(roundRect, paint);
            }
        }
        else
        {
            canvas.DrawRect(bounds, paint);
        }
    }

    #endregion

    #region Measurement

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        // BoxView uses explicit size or a default size when in unbounded context
        var width = WidthRequest >= 0 ? (float)WidthRequest :
                    (float.IsInfinity(availableSize.Width) ? 40f : availableSize.Width);
        var height = HeightRequest >= 0 ? (float)HeightRequest :
                     (float.IsInfinity(availableSize.Height) ? 40f : availableSize.Height);

        // Ensure no NaN values
        if (float.IsNaN(width)) width = 40f;
        if (float.IsNaN(height)) height = 40f;

        return new SKSize(width, height);
    }

    #endregion
}
