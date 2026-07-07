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
    /// Raised by <see cref="SkiaMap"/> when the user clicks this pin's marker
    /// while it is not the currently selected pin (the first tap selects it).
    /// </summary>
    public event EventHandler? Clicked;
    internal void RaiseClicked() => Clicked?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Raised by <see cref="SkiaMap"/> when the user clicks a pin that is
    /// already selected — the closest Linux analogue to tapping the info
    /// window on the mobile platforms, where the first marker tap opens the
    /// info bubble and a subsequent tap "clicks" it.
    /// </summary>
    public event EventHandler? InfoWindowClicked;
    internal void RaiseInfoWindowClicked() => InfoWindowClicked?.Invoke(this, EventArgs.Empty);
}
