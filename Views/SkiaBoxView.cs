// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered BoxView - a simple colored rectangle.
/// </summary>
public class SkiaBoxView : SkiaView
{
    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(nameof(Color), typeof(SKColor), typeof(SkiaBoxView), SKColors.Transparent,
            propertyChanged: (b, o, n) => ((SkiaBoxView)b).Invalidate());

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(float), typeof(SkiaBoxView), 0f,
            propertyChanged: (b, o, n) => ((SkiaBoxView)b).Invalidate());

    public SKColor Color
    {
        get => (SKColor)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public float CornerRadius
    {
        get => (float)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        using var paint = new SKPaint
        {
            Color = Color,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        if (CornerRadius > 0)
        {
            canvas.DrawRoundRect(bounds, CornerRadius, CornerRadius, paint);
        }
        else
        {
            canvas.DrawRect(bounds, paint);
        }
    }

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
}
