// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Maps.Views;

/// <summary>
/// Closed sequence of geographic coordinates drawn as a filled, stroked shape
/// on a <see cref="SkiaMap"/>. Suitable for zones, boundaries, coverage
/// areas, etc. The path is closed automatically (last point connects back to
/// the first).
/// </summary>
public sealed class MapPolygon
{
    public List<(double Latitude, double Longitude)> Points { get; } = new();

    public SKColor FillColor { get; set; } = new SKColor(0x21, 0x96, 0xF3, 0x40);    // translucent material blue
    public SKColor StrokeColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);        // material blue 500
    public float StrokeWidth { get; set; } = 4f;
}
