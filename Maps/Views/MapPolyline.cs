// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Maps.Views;

/// <summary>
/// Sequence of geographic coordinates drawn as a connected stroke on a
/// <see cref="SkiaMap"/>. Suitable for routes, tracks, transit lines, etc.
/// </summary>
public sealed class MapPolyline
{
    public List<(double Latitude, double Longitude)> Points { get; } = new();

    public SKColor StrokeColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);   // material blue 500
    public float StrokeWidth { get; set; } = 4f;

    /// <summary>Optional dash pattern (lengths in pixels, alternating on/off).</summary>
    public float[]? DashPattern { get; set; }
}
