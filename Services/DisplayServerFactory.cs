// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Window;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform.Linux.Services;

public static class DisplayServerFactory
{
    private static DisplayServerType? _cachedServerType;

    /// <summary>
    /// Detects the current display server type.
    /// </summary>
    public static DisplayServerType DetectDisplayServer()
    {
        if (_cachedServerType.HasValue)
            return _cachedServerType.Value;

        // Check for Wayland first (modern default)
        var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        if (!string.IsNullOrEmpty(waylandDisplay))
        {
            // Check if XWayland is available - prefer it for now until native Wayland is fully tested
            var xDisplay = Environment.GetEnvironmentVariable("DISPLAY");
            var preferX11 = Environment.GetEnvironmentVariable("MAUI_PREFER_X11");
            
            if (!string.IsNullOrEmpty(xDisplay) && !string.IsNullOrEmpty(preferX11))
            {
                Console.WriteLine("[DisplayServer] XWayland detected, using X11 backend (MAUI_PREFER_X11 set)");
                _cachedServerType = DisplayServerType.X11;
                return DisplayServerType.X11;
            }

            Console.WriteLine("[DisplayServer] Wayland display detected");
            _cachedServerType = DisplayServerType.Wayland;
            return DisplayServerType.Wayland;
        }

        // Fall back to X11
        var x11Display = Environment.GetEnvironmentVariable("DISPLAY");
        if (!string.IsNullOrEmpty(x11Display))
        {
            Console.WriteLine("[DisplayServer] X11 display detected");
            _cachedServerType = DisplayServerType.X11;
            return DisplayServerType.X11;
        }

        // Default to X11 and let it fail if not available
        Console.WriteLine("[DisplayServer] No display server detected, defaulting to X11");
        _cachedServerType = DisplayServerType.X11;
        return DisplayServerType.X11;
    }

    /// <summary>
    /// Creates a window for the specified or detected display server.
    /// </summary>
    public static IDisplayWindow CreateWindow(string title, int width, int height, DisplayServerType serverType = DisplayServerType.Auto)
    {
        if (serverType == DisplayServerType.Auto)
        {
            serverType = DetectDisplayServer();
        }

        return serverType switch
        {
            DisplayServerType.X11 => CreateX11Window(title, width, height),
            DisplayServerType.Wayland => CreateWaylandWindow(title, width, height),
            _ => CreateX11Window(title, width, height)
        };
    }

    private static IDisplayWindow CreateX11Window(string title, int width, int height)
    {
        try
        {
            Console.WriteLine($"[DisplayServer] Creating X11 window: {title} ({width}x{height})");
            return new X11DisplayWindow(title, width, height);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DisplayServer] Failed to create X11 window: {ex.Message}");
            throw;
        }
    }

    private static IDisplayWindow CreateWaylandWindow(string title, int width, int height)
    {
        try
        {
            Console.WriteLine($"[DisplayServer] Creating Wayland window: {title} ({width}x{height})");
            return new WaylandDisplayWindow(title, width, height);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DisplayServer] Failed to create Wayland window: {ex.Message}");
            
            // Try to fall back to X11 via XWayland
            var xDisplay = Environment.GetEnvironmentVariable("DISPLAY");
            if (!string.IsNullOrEmpty(xDisplay))
            {
                Console.WriteLine("[DisplayServer] Falling back to X11 (XWayland)");
                return CreateX11Window(title, width, height);
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
            _ => "Unknown"
        };
    }
}
