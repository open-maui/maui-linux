// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Globalization;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Converters;

public class SKPointTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || sourceType == typeof(Point) || base.CanConvertFrom(context, sourceType);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || destinationType == typeof(Point) || base.CanConvertTo(context, destinationType);
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
        {
            return SKPoint.Empty;
        }

        str = str.Trim();
        var parts = str.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 2 &&
            float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            return new SKPoint(x, y);
        }

        return SKPoint.Empty;
    }
}
