// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Converters;

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
