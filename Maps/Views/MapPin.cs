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

    /// <summary>
    /// Optional opaque payload set by the <c>LinuxMapHandler</c> so the
    /// handler can route hit-tests back to the originating MAUI <c>IMapPin</c>
    /// and fire its <c>MarkerClicked</c> / <c>InfoWindowClicked</c> events.
    /// Apps using SkiaMap directly can ignore this.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>
    /// Raised by <see cref="SkiaMap"/> when the user clicks this pin's marker.
    /// Apps using SkiaMap directly can subscribe here; the MAUI handler uses
    /// <see cref="Tag"/> instead to fan out to <c>IMapPin.SendMarkerClick</c>.
    /// </summary>
    public event EventHandler? Clicked;
    internal void RaiseClicked() => Clicked?.Invoke(this, EventArgs.Empty);
}
