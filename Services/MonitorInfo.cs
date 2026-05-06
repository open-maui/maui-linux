// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Represents information about a display monitor.
/// </summary>
public record MonitorInfo
{
    /// <summary>
    /// Gets the unique identifier for this monitor (XRandR output ID).
    /// </summary>
    public ulong Id { get; init; }

    /// <summary>
    /// Gets the monitor name (e.g., "HDMI-1", "DP-2", "eDP-1").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether this is the primary monitor.
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// Gets the X position of the monitor in the virtual desktop.
    /// </summary>
    public int X { get; init; }

    /// <summary>
    /// Gets the Y position of the monitor in the virtual desktop.
    /// </summary>
    public int Y { get; init; }

    /// <summary>
    /// Gets the width in pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Gets the height in pixels.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Gets the physical width in millimeters.
    /// </summary>
    public int PhysicalWidthMm { get; init; }

    /// <summary>
    /// Gets the physical height in millimeters.
    /// </summary>
    public int PhysicalHeightMm { get; init; }

    /// <summary>
    /// Gets the refresh rate in Hz.
    /// </summary>
    public double RefreshRate { get; init; }

    /// <summary>
    /// Gets the horizontal DPI.
    /// </summary>
    public double DpiX
    {
        get
        {
            if (PhysicalWidthMm <= 0) return 96.0;
            return Width / (PhysicalWidthMm / 25.4);
        }
    }

    /// <summary>
    /// Gets the vertical DPI.
    /// </summary>
    public double DpiY
    {
        get
        {
            if (PhysicalHeightMm <= 0) return 96.0;
            return Height / (PhysicalHeightMm / 25.4);
        }
    }

    /// <summary>
    /// Gets the average DPI.
    /// </summary>
    public double Dpi => (DpiX + DpiY) / 2.0;

    /// <summary>
    /// Gets the scale factor based on DPI (1.0 = 96 DPI).
    /// </summary>
    public double ScaleFactor => Dpi / 96.0;

    /// <summary>
    /// Gets the bounds rectangle.
    /// </summary>
    public (int X, int Y, int Width, int Height) Bounds => (X, Y, Width, Height);

    public override string ToString()
    {
        return $"{Name}: {Width}x{Height}+{X}+{Y} @ {RefreshRate:F1}Hz ({Dpi:F0} DPI){(IsPrimary ? " [Primary]" : "")}";
    }
}
