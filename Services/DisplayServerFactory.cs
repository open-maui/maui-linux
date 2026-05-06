// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Platform.Linux.Services;

public static class DisplayServerFactory
{
    private static DisplayServerType? _cachedServerType;

    /// <summary>
    /// Detects the active display server.
    ///
    /// **Default is X11/XWayland** — both on pure-X11 sessions and when running
    /// under a Wayland compositor (the compositor's XWayland bridge handles us).
    /// Wayland-native is opt-in via <c>MAUI_PREFER_WAYLAND=1</c> while the native
    /// path's protocol bindings are stabilized; today's <c>wl_interface</c> stubs
    /// have NULL methods tables, which segfaults the first request after a global
    /// is bound. Native-Wayland will become the default once the methods tables
    /// are populated from the wayland-protocols XML (a follow-up rev).
    /// </summary>
    public static DisplayServerType DetectDisplayServer()
    {
        if (_cachedServerType.HasValue)
            return _cachedServerType.Value;

        var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        var xDisplay = Environment.GetEnvironmentVariable("DISPLAY");
        var preferWayland = Environment.GetEnvironmentVariable("MAUI_PREFER_WAYLAND");

        if (!string.IsNullOrEmpty(preferWayland) && !string.IsNullOrEmpty(waylandDisplay))
        {
            DiagnosticLog.Debug("DisplayServerFactory", "MAUI_PREFER_WAYLAND set; using native Wayland (experimental)");
            _cachedServerType = DisplayServerType.Wayland;
            return DisplayServerType.Wayland;
        }

        if (!string.IsNullOrEmpty(xDisplay))
        {
            DiagnosticLog.Debug("DisplayServerFactory",
                !string.IsNullOrEmpty(waylandDisplay)
                    ? "Wayland session detected; using XWayland (set MAUI_PREFER_WAYLAND=1 for native)"
                    : "X11 session detected");
            _cachedServerType = DisplayServerType.X11;
            return DisplayServerType.X11;
        }

        if (!string.IsNullOrEmpty(waylandDisplay))
        {
            // No XWayland available; native Wayland is the only option.
            DiagnosticLog.Warn("DisplayServerFactory", "Pure-Wayland session (no DISPLAY); using native Wayland");
            _cachedServerType = DisplayServerType.Wayland;
            return DisplayServerType.Wayland;
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
