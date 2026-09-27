// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Geometry requests from MAUI's <c>Window</c> (Width/Height, X/Y and the
/// minimum/maximum sizes), in logical (device-independent) pixels. Each
/// backend applies what its protocol allows: X11 moves, resizes and sets
/// WM_NORMAL_HINTS; Wayland resizes its own surface and sends
/// <c>xdg_toplevel.set_min_size</c>/<c>set_max_size</c>, but a Wayland client
/// cannot choose its position, so position requests are ignored there.
/// </summary>
public interface IDesktopWindowControl
{
    /// <summary>Asks for a new logical size.</summary>
    void RequestLogicalSize(int width, int height);

    /// <summary>Asks for a new logical position (ignored on Wayland).</summary>
    void RequestLogicalPosition(int x, int y);

    /// <summary>Sets size limits; 0 means "no limit" for that bound.</summary>
    void SetLogicalSizeLimits(int minWidth, int minHeight, int maxWidth, int maxHeight);
}
