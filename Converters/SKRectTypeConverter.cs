// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Globalization;
using SkiaSharp;
using Microsoft.Maui;

namespace Microsoft.Maui.Platform.Linux.Converters;

/// <summary>
/// Type converter for converting between MAUI Thickness and SKRect (for padding/margin).
/// Enables XAML styling with Thickness values that get applied to Skia controls.
/// </summary>
public class SKRectTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) ||
               sourceType == typeof(Thickness) ||
               sourceType == typeof(double) ||
               sourceType == typeof(float) ||
               base.CanConvertFrom(context, sourceType);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) ||
               destinationType == typeof(Thickness) ||
               base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is Thickness thickness)
        {
            return ThicknessToSKRect(thickness);
        }

        if (value is double d)
        {
            return new SKRect((float)d, (float)d, (float)d, (float)d);
        }

        if (value is float f)
        {
            return new SKRect(f, f, f, f);
        }

        if (value is string str)
        {
            return ParseRect(str);
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is SKRect rect)
        {
            if (destinationType == typeof(string))
            {
                return $"{rect.Left},{rect.Top},{rect.Right},{rect.Bottom}";
            }

            if (destinationType == typeof(Thickness))
            {
                return SKRectToThickness(rect);
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    /// <summary>
    /// Converts a MAUI Thickness to an SKRect (used as padding storage).
    /// </summary>
    public static SKRect ThicknessToSKRect(Thickness thickness)
    {
        return new SKRect(
            (float)thickness.Left,
            (float)thickness.Top,
            (float)thickness.Right,
            (float)thickness.Bottom);
    }

    /// <summary>
    /// Converts an SKRect (used as padding storage) to a MAUI Thickness.
    /// </summary>
    public static Thickness SKRectToThickness(SKRect rect)
    {
        return new Thickness(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    /// <summary>
    /// Parses a string into an SKRect for padding/margin.
    /// Supports formats: "uniform", "horizontal,vertical", "left,top,right,bottom"
    /// </summary>
    private static SKRect ParseRect(string str)
    {
        if (string.IsNullOrWhiteSpace(str))
            return SKRect.Empty;

        str = str.Trim();
        var parts = str.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 1)
        {
            // Uniform padding
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var uniform))
            {
                return new SKRect(uniform, uniform, uniform, uniform);
            }
        }
        else if (parts.Length == 2)
        {
            // Horizontal, Vertical
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var horizontal) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var vertical))
            {
                return new SKRect(horizontal, vertical, horizontal, vertical);
            }
        }
        else if (parts.Length == 4)
        {
            // Left, Top, Right, Bottom
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var left) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var top) &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var right) &&
                float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var bottom))
            {
                return new SKRect(left, top, right, bottom);
            }
        }

        return SKRect.Empty;
    }
}
