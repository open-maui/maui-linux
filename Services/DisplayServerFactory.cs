// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Platform.Linux.Services;

public static class DisplayServerFactory
{
    private static DisplayServerType? _cachedServerType;

    /// <summary>
    /// Detects the active display server.
    /// Wayland is preferred when WAYLAND_DISPLAY is set; users can opt out by
    /// setting MAUI_PREFER_X11=1, in which case the X11/XWayland path runs instead.
    /// </summary>
    public static DisplayServerType DetectDisplayServer()
    {
        if (_cachedServerType.HasValue)
            return _cachedServerType.Value;

        var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        var xDisplay = Environment.GetEnvironmentVariable("DISPLAY");
        var preferX11 = Environment.GetEnvironmentVariable("MAUI_PREFER_X11");

        if (!string.IsNullOrEmpty(waylandDisplay))
        {
            if (!string.IsNullOrEmpty(preferX11) && !string.IsNullOrEmpty(xDisplay))
            {
                DiagnosticLog.Debug("DisplayServerFactory", "MAUI_PREFER_X11 set; using X11/XWayland");
                _cachedServerType = DisplayServerType.X11;
                return DisplayServerType.X11;
            }

            DiagnosticLog.Debug("DisplayServerFactory", "Wayland session detected");
            _cachedServerType = DisplayServerType.Wayland;
            return DisplayServerType.Wayland;
        }

        if (!string.IsNullOrEmpty(xDisplay))
        {
            DiagnosticLog.Debug("DisplayServerFactory", "X11 session detected");
            _cachedServerType = DisplayServerType.X11;
            return DisplayServerType.X11;
        }

        DiagnosticLog.Warn("DisplayServerFactory", "No display server detected, defaulting to X11");
        _cachedServerType = DisplayServerType.X11;
        return DisplayServerType.X11;
    }

    /// <summary>
    /// Creates a window for the specified or detected display server. On Wayland,
    /// transparently falls back to X11 if the Wayland connection cannot be opened
    /// (typically because libwayland-client is missing).
    /// </summary>
    public static IDisplayWindow CreateWindow(string title, int width, int height, DisplayServerType serverType = DisplayServerType.Auto)
    {
        if (serverType == DisplayServerType.Auto)
            serverType = DetectDisplayServer();

        return serverType switch
        {
            DisplayServerType.Wayland => CreateWaylandOrFallback(title, width, height),
            _ => CreateX11(title, width, height),
        };
    }

    private static IDisplayWindow CreateX11(string title, int width, int height)
    {
        DiagnosticLog.Debug("DisplayServerFactory", $"Creating X11 window: {title} ({width}x{height})");
        return new X11Window(title, width, height);
    }

    private static IDisplayWindow CreateWaylandOrFallback(string title, int width, int height)
    {
        try
        {
            DiagnosticLog.Debug("DisplayServerFactory", $"Creating Wayland window: {title} ({width}x{height})");
            return new WaylandWindow(title, width, height);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("DisplayServerFactory", $"Wayland window creation failed: {ex.Message}");

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
            {
                DiagnosticLog.Warn("DisplayServerFactory", "Falling back to X11 (XWayland)");
                _cachedServerType = DisplayServerType.X11;
                return CreateX11(title, width, height);
            }

            throw;
        }
    }

    /// <summary>
    /// Gets a human-readable name for the display server.
    /// </summary>
    public static string GetDisplayServerName(DisplayServerType serverType = DisplayServerType.Auto)
    {
        if (serverType == DisplayServerType.Auto)
            serverType = DetectDisplayServer();

        return serverType switch
        {
            DisplayServerType.X11 => "X11",
            DisplayServerType.Wayland => "Wayland",
            _ => "Unknown",
        };
    }
}
