// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Globalization;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Converters;

public class SKSizeTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || sourceType == typeof(Size) || base.CanConvertFrom(context, sourceType);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || destinationType == typeof(Size) || base.CanConvertTo(context, destinationType);
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
        {
            return SKSize.Empty;
        }

        str = str.Trim();
        var parts = str.Split(new[] { ',', ' ', 'x', 'X' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 1)
        {
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var single))
            {
                return new SKSize(single, single);
            }
        }
        else if (parts.Length == 2 &&
            float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var width) &&
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
        {
            return new SKSize(width, height);
        }

        return SKSize.Empty;
    }
}
