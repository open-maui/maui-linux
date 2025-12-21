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

/// <summary>
/// Type converter for SKSize.
/// </summary>
public class SKSizeTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) ||
               sourceType == typeof(Size) ||
               base.CanConvertFrom(context, sourceType);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) ||
               destinationType == typeof(Size) ||
               base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is Size size)
        {
            return new SKSize((float)size.Width, (float)size.Height);
        }

        if (value is string str)
        {
            return ParseSize(str);
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is SKSize size)
        {
            if (destinationType == typeof(string))
            {
                return $"{size.Width},{size.Height}";
            }

            if (destinationType == typeof(Size))
            {
                return new Size(size.Width, size.Height);
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    private static SKSize ParseSize(string str)
    {
        if (string.IsNullOrWhiteSpace(str))
            return SKSize.Empty;

        str = str.Trim();
        var parts = str.Split(new[] { ',', ' ', 'x', 'X' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 1)
        {
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var uniform))
            {
                return new SKSize(uniform, uniform);
            }
        }
        else if (parts.Length == 2)
        {
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var width) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
            {
                return new SKSize(width, height);
            }
        }

        return SKSize.Empty;
    }
}

/// <summary>
/// Type converter for SKPoint.
/// </summary>
public class SKPointTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) ||
               sourceType == typeof(Point) ||
               base.CanConvertFrom(context, sourceType);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) ||
               destinationType == typeof(Point) ||
               base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is Point point)
        {
            return new SKPoint((float)point.X, (float)point.Y);
        }

        if (value is string str)
        {
            return ParsePoint(str);
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is SKPoint point)
        {
            if (destinationType == typeof(string))
            {
                return $"{point.X},{point.Y}";
            }

            if (destinationType == typeof(Point))
            {
                return new Point(point.X, point.Y);
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    private static SKPoint ParsePoint(string str)
    {
        if (string.IsNullOrWhiteSpace(str))
            return SKPoint.Empty;

        str = str.Trim();
        var parts = str.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 2)
        {
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                return new SKPoint(x, y);
            }
        }

        return SKPoint.Empty;
    }
}

/// <summary>
/// Extension methods for SkiaSharp type conversions.
/// </summary>
public static class SKTypeExtensions
{
    public static SKRect ToSKRect(this Thickness thickness)
    {
        return SKRectTypeConverter.ThicknessToSKRect(thickness);
    }

    public static Thickness ToThickness(this SKRect rect)
    {
        return SKRectTypeConverter.SKRectToThickness(rect);
    }

    public static SKSize ToSKSize(this Size size)
    {
        return new SKSize((float)size.Width, (float)size.Height);
    }

    public static Size ToSize(this SKSize size)
    {
        return new Size(size.Width, size.Height);
    }

    public static SKPoint ToSKPoint(this Point point)
    {
        return new SKPoint((float)point.X, (float)point.Y);
    }

    public static Point ToPoint(this SKPoint point)
    {
        return new Point(point.X, point.Y);
    }
}
