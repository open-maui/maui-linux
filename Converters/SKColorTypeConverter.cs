// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Globalization;
using SkiaSharp;
using Microsoft.Maui.Graphics;

namespace Microsoft.Maui.Platform.Linux.Converters;

/// <summary>
/// Type converter for converting between MAUI Color and SKColor.
/// Enables XAML styling with Color values that get applied to Skia controls.
/// </summary>
public class SKColorTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) ||
               sourceType == typeof(Color) ||
               base.CanConvertFrom(context, sourceType);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) ||
               destinationType == typeof(Color) ||
               base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is Color mauiColor)
        {
            return ToSKColor(mauiColor);
        }

        if (value is string str)
        {
            return ParseColor(str);
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is SKColor skColor)
        {
            if (destinationType == typeof(string))
            {
                return $"#{skColor.Alpha:X2}{skColor.Red:X2}{skColor.Green:X2}{skColor.Blue:X2}";
            }

            if (destinationType == typeof(Color))
            {
                return ToMauiColor(skColor);
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    /// <summary>
    /// Converts a MAUI Color to an SKColor.
    /// </summary>
    public static SKColor ToSKColor(Color mauiColor)
    {
        return new SKColor(
            (byte)(mauiColor.Red * 255),
            (byte)(mauiColor.Green * 255),
            (byte)(mauiColor.Blue * 255),
            (byte)(mauiColor.Alpha * 255));
    }

    /// <summary>
    /// Converts an SKColor to a MAUI Color.
    /// </summary>
    public static Color ToMauiColor(SKColor skColor)
    {
        return new Color(
            skColor.Red / 255f,
            skColor.Green / 255f,
            skColor.Blue / 255f,
            skColor.Alpha / 255f);
    }

    /// <summary>
    /// Parses a color string (hex, named, or rgb format).
    /// </summary>
    private static SKColor ParseColor(string colorString)
    {
        if (string.IsNullOrWhiteSpace(colorString))
            return SKColors.Black;

        colorString = colorString.Trim();

        // Try hex format
        if (colorString.StartsWith("#"))
        {
            return SKColor.Parse(colorString);
        }

        // Try named colors
        var namedColor = GetNamedColor(colorString.ToLowerInvariant());
        if (namedColor.HasValue)
            return namedColor.Value;

        // Try rgb/rgba format
        if (colorString.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            return ParseRgbColor(colorString);
        }

        // Fallback to SKColor.Parse
        if (SKColor.TryParse(colorString, out var parsed))
            return parsed;

        return SKColors.Black;
    }

    private static SKColor? GetNamedColor(string name) => name switch
    {
        "transparent" => SKColors.Transparent,
        "black" => SKColors.Black,
        "white" => SKColors.White,
        "red" => SKColors.Red,
        "green" => SKColors.Green,
        "blue" => SKColors.Blue,
        "yellow" => SKColors.Yellow,
        "cyan" => SKColors.Cyan,
        "magenta" => SKColors.Magenta,
        "gray" or "grey" => SKColors.Gray,
        "darkgray" or "darkgrey" => SKColors.DarkGray,
        "lightgray" or "lightgrey" => SKColors.LightGray,
        "orange" => new SKColor(0xFF, 0xA5, 0x00),
        "pink" => new SKColor(0xFF, 0xC0, 0xCB),
        "purple" => new SKColor(0x80, 0x00, 0x80),
        "brown" => new SKColor(0xA5, 0x2A, 0x2A),
        "navy" => new SKColor(0x00, 0x00, 0x80),
        "teal" => new SKColor(0x00, 0x80, 0x80),
        "olive" => new SKColor(0x80, 0x80, 0x00),
        "silver" => new SKColor(0xC0, 0xC0, 0xC0),
        "maroon" => new SKColor(0x80, 0x00, 0x00),
        "lime" => new SKColor(0x00, 0xFF, 0x00),
        "aqua" => new SKColor(0x00, 0xFF, 0xFF),
        "fuchsia" => new SKColor(0xFF, 0x00, 0xFF),
        "gold" => new SKColor(0xFF, 0xD7, 0x00),
        "coral" => new SKColor(0xFF, 0x7F, 0x50),
        "salmon" => new SKColor(0xFA, 0x80, 0x72),
        "crimson" => new SKColor(0xDC, 0x14, 0x3C),
        "indigo" => new SKColor(0x4B, 0x00, 0x82),
        "violet" => new SKColor(0xEE, 0x82, 0xEE),
        "turquoise" => new SKColor(0x40, 0xE0, 0xD0),
        "tan" => new SKColor(0xD2, 0xB4, 0x8C),
        "chocolate" => new SKColor(0xD2, 0x69, 0x1E),
        "tomato" => new SKColor(0xFF, 0x63, 0x47),
        "steelblue" => new SKColor(0x46, 0x82, 0xB4),
        "skyblue" => new SKColor(0x87, 0xCE, 0xEB),
        "slategray" or "slategrey" => new SKColor(0x70, 0x80, 0x90),
        "seagreen" => new SKColor(0x2E, 0x8B, 0x57),
        "royalblue" => new SKColor(0x41, 0x69, 0xE1),
        "plum" => new SKColor(0xDD, 0xA0, 0xDD),
        "peru" => new SKColor(0xCD, 0x85, 0x3F),
        "orchid" => new SKColor(0xDA, 0x70, 0xD6),
        "orangered" => new SKColor(0xFF, 0x45, 0x00),
        "olivedrab" => new SKColor(0x6B, 0x8E, 0x23),
        "midnightblue" => new SKColor(0x19, 0x19, 0x70),
        "mediumblue" => new SKColor(0x00, 0x00, 0xCD),
        "limegreen" => new SKColor(0x32, 0xCD, 0x32),
        "hotpink" => new SKColor(0xFF, 0x69, 0xB4),
        "honeydew" => new SKColor(0xF0, 0xFF, 0xF0),
        "greenyellow" => new SKColor(0xAD, 0xFF, 0x2F),
        "forestgreen" => new SKColor(0x22, 0x8B, 0x22),
        "firebrick" => new SKColor(0xB2, 0x22, 0x22),
        "dodgerblue" => new SKColor(0x1E, 0x90, 0xFF),
        "deeppink" => new SKColor(0xFF, 0x14, 0x93),
        "deepskyblue" => new SKColor(0x00, 0xBF, 0xFF),
        "darkviolet" => new SKColor(0x94, 0x00, 0xD3),
        "darkturquoise" => new SKColor(0x00, 0xCE, 0xD1),
        "darkslategray" or "darkslategrey" => new SKColor(0x2F, 0x4F, 0x4F),
        "darkred" => new SKColor(0x8B, 0x00, 0x00),
        "darkorange" => new SKColor(0xFF, 0x8C, 0x00),
        "darkolivegreen" => new SKColor(0x55, 0x6B, 0x2F),
        "darkmagenta" => new SKColor(0x8B, 0x00, 0x8B),
        "darkkhaki" => new SKColor(0xBD, 0xB7, 0x6B),
        "darkgreen" => new SKColor(0x00, 0x64, 0x00),
        "darkgoldenrod" => new SKColor(0xB8, 0x86, 0x0B),
        "darkcyan" => new SKColor(0x00, 0x8B, 0x8B),
        "darkblue" => new SKColor(0x00, 0x00, 0x8B),
        "cornflowerblue" => new SKColor(0x64, 0x95, 0xED),
        "cadetblue" => new SKColor(0x5F, 0x9E, 0xA0),
        "blueviolet" => new SKColor(0x8A, 0x2B, 0xE2),
        "azure" => new SKColor(0xF0, 0xFF, 0xFF),
        "aquamarine" => new SKColor(0x7F, 0xFF, 0xD4),
        "aliceblue" => new SKColor(0xF0, 0xF8, 0xFF),
        _ => null
    };

    private static SKColor ParseRgbColor(string colorString)
    {
        try
        {
            var isRgba = colorString.StartsWith("rgba", StringComparison.OrdinalIgnoreCase);
            var startIndex = colorString.IndexOf('(');
            var endIndex = colorString.IndexOf(')');

            if (startIndex == -1 || endIndex == -1)
                return SKColors.Black;

            var values = colorString.Substring(startIndex + 1, endIndex - startIndex - 1)
                .Split(',')
                .Select(v => v.Trim())
                .ToArray();

            if (values.Length < 3)
                return SKColors.Black;

            var r = byte.Parse(values[0]);
            var g = byte.Parse(values[1]);
            var b = byte.Parse(values[2]);
            byte a = 255;

            if (isRgba && values.Length >= 4)
            {
                var alphaValue = float.Parse(values[3], CultureInfo.InvariantCulture);
                a = (byte)(alphaValue <= 1 ? alphaValue * 255 : alphaValue);
            }

            return new SKColor(r, g, b, a);
        }
        catch
        {
            return SKColors.Black;
        }
    }
}

/// <summary>
/// Extension methods for color conversion.
/// </summary>
public static class ColorExtensions
{
    /// <summary>
    /// Converts a MAUI Color to an SKColor.
    /// </summary>
    public static SKColor ToSKColor(this Color color)
    {
        return SKColorTypeConverter.ToSKColor(color);
    }

    /// <summary>
    /// Converts an SKColor to a MAUI Color.
    /// </summary>
    public static Color ToMauiColor(this SKColor color)
    {
        return SKColorTypeConverter.ToMauiColor(color);
    }
}
