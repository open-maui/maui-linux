// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Maps.Views;

/// <summary>
/// A point overlay on a <see cref="SkiaMap"/>. Drawn as a teardrop marker
/// centered horizontally on the latitude/longitude with its tip at that point.
/// </summary>
public sealed class MapPin
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>Optional label drawn below the marker.</summary>
    public string? Label { get; set; }

    public SKColor FillColor { get; set; } = new SKColor(0xE5, 0x39, 0x35);   // matte red
    public SKColor BorderColor { get; set; } = SKColors.White;
    public float Size { get; set; } = 28f;
}
