// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux;

public class LinuxApplicationOptions
{
    public string? Title { get; set; } = "MAUI Application";

    public int Width { get; set; } = 800;

    public int Height { get; set; } = 600;

    /// <summary>
    /// Force a specific display server. Default <see cref="DisplayServerType.Auto"/>
    /// detects from <c>WAYLAND_DISPLAY</c> / <c>DISPLAY</c> / <c>MAUI_PREFER_X11</c>.
    /// Programmatic override; takes precedence over the env-var heuristics.
    /// </summary>
    public DisplayServerType DisplayServer { get; set; } = DisplayServerType.Auto;

    public string? IconPath { get; set; }

    public bool UseGtk { get; set; }

    // Gesture configuration
    public double SwipeMinDistance { get; set; } = 50.0;
    public double SwipeMaxTime { get; set; } = 500.0;
    public double SwipeDirectionThreshold { get; set; } = 0.5;
    public double PanMinDistance { get; set; } = 10.0;
    public double PinchScrollScale { get; set; } = 0.1;

    // Rendering configuration
    public int MaxDirtyRegions { get; set; } = 32;
    public float RegionMergeThreshold { get; set; } = 0.3f;
}
