// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A fill paint for a MAUI brush over a rectangle (solid, linear or radial gradient, the
/// gradient's points relative to the rectangle, as MAUI defines them). Bar backgrounds
/// (TabbedPage.BarBackground, Shell.TabBarBackgroundColor) are painted with it.
/// </summary>
internal static class BrushPaint
{
    /// <summary>The paint, or null when <paramref name="brush"/> paints nothing.</summary>
    public static SKPaint? Create(Microsoft.Maui.Controls.Brush? brush, SKRect bounds)
    {
        if (Microsoft.Maui.Controls.Brush.IsNullOrEmpty(brush))
            return null;
        var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        switch (brush)
        {
            case Microsoft.Maui.Controls.SolidColorBrush solid when solid.Color != null:
                paint.Color = solid.Color.ToSKColor();
                return paint;
            case Microsoft.Maui.Controls.LinearGradientBrush linear when linear.GradientStops.Count > 0:
            {
                var start = new SKPoint(bounds.Left + (float)(linear.StartPoint.X * bounds.Width), bounds.Top + (float)(linear.StartPoint.Y * bounds.Height));
                var end = new SKPoint(bounds.Left + (float)(linear.EndPoint.X * bounds.Width), bounds.Top + (float)(linear.EndPoint.Y * bounds.Height));
                var stops = linear.GradientStops.OrderBy(s => s.Offset).ToArray();
                paint.Shader = SKShader.CreateLinearGradient(start, end,
                    stops.Select(s => s.Color.ToSKColor()).ToArray(), stops.Select(s => s.Offset).ToArray(), SKShaderTileMode.Clamp);
                return paint;
            }
            case Microsoft.Maui.Controls.RadialGradientBrush radial when radial.GradientStops.Count > 0:
            {
                var center = new SKPoint(bounds.Left + (float)(radial.Center.X * bounds.Width), bounds.Top + (float)(radial.Center.Y * bounds.Height));
                var radius = (float)(radial.Radius * Math.Max(bounds.Width, bounds.Height));
                var stops = radial.GradientStops.OrderBy(s => s.Offset).ToArray();
                paint.Shader = SKShader.CreateRadialGradient(center, radius,
                    stops.Select(s => s.Color.ToSKColor()).ToArray(), stops.Select(s => s.Offset).ToArray(), SKShaderTileMode.Clamp);
                return paint;
            }
        }
        paint.Dispose();
        return null;
    }
}
