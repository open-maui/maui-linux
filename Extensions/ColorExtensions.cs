// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Graphics;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Extension methods for color conversions between MAUI and SkiaSharp.
/// </summary>
public static class ColorExtensions
{
    /// <summary>
    /// Converts a MAUI Color to an SKColor.
    /// </summary>
    public static SKColor ToSKColor(this Color color)
    {
        if (color == null)
            return SKColors.Transparent;

        return new SKColor(
            (byte)(color.Red * 255),
            (byte)(color.Green * 255),
            (byte)(color.Blue * 255),
            (byte)(color.Alpha * 255));
    }

    /// <summary>
    /// Converts an SKColor to a MAUI Color.
    /// </summary>
    public static Color ToMauiColor(this SKColor color)
    {
        return new Color(
            color.Red / 255f,
            color.Green / 255f,
            color.Blue / 255f,
            color.Alpha / 255f);
    }

    /// <summary>
    /// Creates a new SKColor with the specified alpha value.
    /// </summary>
    public static SKColor WithAlpha(this SKColor color, byte alpha)
    {
        return new SKColor(color.Red, color.Green, color.Blue, alpha);
    }

    /// <summary>
    /// Creates a lighter version of the color.
    /// </summary>
    public static SKColor Lighter(this SKColor color, float factor = 0.2f)
    {
        return new SKColor(
            (byte)Math.Min(255, color.Red + (255 - color.Red) * factor),
            (byte)Math.Min(255, color.Green + (255 - color.Green) * factor),
            (byte)Math.Min(255, color.Blue + (255 - color.Blue) * factor),
            color.Alpha);
    }

    /// <summary>
    /// Creates a darker version of the color.
    /// </summary>
    public static SKColor Darker(this SKColor color, float factor = 0.2f)
    {
        return new SKColor(
            (byte)(color.Red * (1 - factor)),
            (byte)(color.Green * (1 - factor)),
            (byte)(color.Blue * (1 - factor)),
            color.Alpha);
    }

    /// <summary>
    /// Gets the luminance of the color.
    /// </summary>
    public static float GetLuminance(this SKColor color)
    {
        return 0.299f * color.Red / 255f +
               0.587f * color.Green / 255f +
               0.114f * color.Blue / 255f;
    }

    /// <summary>
    /// Determines if the color is considered light.
    /// </summary>
    public static bool IsLight(this SKColor color)
    {
        return color.GetLuminance() > 0.5f;
    }

    /// <summary>
    /// Gets a contrasting color (black or white) for text on this background.
    /// </summary>
    public static SKColor GetContrastingColor(this SKColor backgroundColor)
    {
        return backgroundColor.IsLight() ? SKColors.Black : SKColors.White;
    }

    /// <summary>
    /// Converts a MAUI Paint to an SKColor if possible.
    /// </summary>
    public static SKColor? ToSKColorOrNull(this Paint? paint)
    {
        if (paint is SolidPaint solidPaint && solidPaint.Color != null)
            return solidPaint.Color.ToSKColor();

        return null;
    }

    /// <summary>
    /// Converts a MAUI Paint to an SKColor, using a default if not a solid color.
    /// </summary>
    public static SKColor ToSKColor(this Paint? paint, SKColor defaultColor)
    {
        return paint.ToSKColorOrNull() ?? defaultColor;
    }
}

/// <summary>
/// Font extensions for converting MAUI fonts to SkiaSharp.
/// </summary>
public static class FontExtensions
{
    /// <summary>
    /// Gets the SKFontStyle from a MAUI Font.
    /// </summary>
    public static SKFontStyle ToSKFontStyle(this Font font)
    {
        // Map MAUI FontWeight (enum with numeric values) to SKFontStyleWeight
        var weight = (int)font.Weight switch
        {
            100 => SKFontStyleWeight.Thin,        // Thin
            200 => SKFontStyleWeight.ExtraLight,  // UltraLight
            300 => SKFontStyleWeight.Light,       // Light
            400 => SKFontStyleWeight.Normal,      // Regular
            500 => SKFontStyleWeight.Medium,      // Medium
            600 => SKFontStyleWeight.SemiBold,    // Semibold
            700 => SKFontStyleWeight.Bold,        // Bold
            800 => SKFontStyleWeight.ExtraBold,   // Heavy
            900 => SKFontStyleWeight.Black,       // Black
            _ => font.Weight >= FontWeight.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal
        };

        var slant = font.Slant switch
        {
            FontSlant.Italic => SKFontStyleSlant.Italic,
            FontSlant.Oblique => SKFontStyleSlant.Oblique,
            _ => SKFontStyleSlant.Upright
        };

        return new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);
    }

    /// <summary>
    /// Creates an SKFont from a MAUI Font.
    /// </summary>
    public static SKFont ToSKFont(this Font font, float defaultSize = 14f)
    {
        var size = font.Size > 0 ? (float)font.Size : defaultSize;
        var typeface = SKTypeface.FromFamilyName(font.Family ?? "sans-serif", font.ToSKFontStyle());
        return new SKFont(typeface, size);
    }
}

/// <summary>
/// Thickness extensions for converting MAUI Thickness to SKRect.
/// </summary>
public static class ThicknessExtensions
{
    /// <summary>
    /// Converts a MAUI Thickness to an SKRect representing padding/margin.
    /// </summary>
    public static SKRect ToSKRect(this Thickness thickness)
    {
        return new SKRect(
            (float)thickness.Left,
            (float)thickness.Top,
            (float)thickness.Right,
            (float)thickness.Bottom);
    }
}
