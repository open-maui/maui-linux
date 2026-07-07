// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Maps.Views;

/// <summary>
/// Circle overlay on a <see cref="SkiaMap"/>: a geographic center plus a
/// radius in meters. Rendered as a flat circle whose pixel radius is derived
/// from the Mercator meters-per-pixel at the circle's center latitude — the
/// same approximation the mobile platforms use (not geodesic-accurate, which
/// only matters for continent-scale radii).
/// </summary>
public sealed class MapCircle
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>Radius in meters on the ground.</summary>
    public double RadiusMeters { get; set; }

    public SKColor FillColor { get; set; } = new SKColor(0x21, 0x96, 0xF3, 0x40);    // translucent material blue
    public SKColor StrokeColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);        // material blue 500
    public float StrokeWidth { get; set; } = 4f;
}
