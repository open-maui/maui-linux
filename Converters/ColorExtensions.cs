// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Converters;

public static class ColorExtensions
{
    public static SKColor ToSKColor(this Color color)
    {
        return SKColorTypeConverter.ToSKColor(color);
    }

    public static Color ToMauiColor(this SKColor color)
    {
        return SKColorTypeConverter.ToMauiColor(color);
    }
}
