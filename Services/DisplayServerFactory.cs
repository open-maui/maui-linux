// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Window;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Supported display server types.
/// </summary>
public enum DisplayServerType
{
    Auto,
    X11,
    Wayland
}

/// <summary>
/// Factory for creating display server connections.
/// Supports X11 and Wayland display servers.
/// </summary>
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

/// <summary>
/// Common interface for display server windows.
/// </summary>
public interface IDisplayWindow : IDisposable
{
    int Width { get; }
    int Height { get; }
    bool IsRunning { get; }
    void Show();
    void Hide();
    void SetTitle(string title);
    void Resize(int width, int height);
    void ProcessEvents();
    void Stop();
    event EventHandler<KeyEventArgs>? KeyDown;
    event EventHandler<KeyEventArgs>? KeyUp;
    event EventHandler<TextInputEventArgs>? TextInput;
    event EventHandler<PointerEventArgs>? PointerMoved;
    event EventHandler<PointerEventArgs>? PointerPressed;
    event EventHandler<PointerEventArgs>? PointerReleased;
    event EventHandler<ScrollEventArgs>? Scroll;
    event EventHandler? Exposed;
    event EventHandler<(int Width, int Height)>? Resized;
    event EventHandler? CloseRequested;
}

/// <summary>
/// X11 display window wrapper implementing the common interface.
/// </summary>
public class X11DisplayWindow : IDisplayWindow
{
    private readonly X11Window _window;

    public int Width => _window.Width;
    public int Height => _window.Height;
    public bool IsRunning => _window.IsRunning;

    public event EventHandler<KeyEventArgs>? KeyDown;
    public event EventHandler<KeyEventArgs>? KeyUp;
    public event EventHandler<TextInputEventArgs>? TextInput;
    public event EventHandler<PointerEventArgs>? PointerMoved;
    public event EventHandler<PointerEventArgs>? PointerPressed;
    public event EventHandler<PointerEventArgs>? PointerReleased;
    public event EventHandler<ScrollEventArgs>? Scroll;
    public event EventHandler? Exposed;
    public event EventHandler<(int Width, int Height)>? Resized;
    public event EventHandler? CloseRequested;

    public X11DisplayWindow(string title, int width, int height)
    {
        _window = new X11Window(title, width, height);

        _window.KeyDown += (s, e) => KeyDown?.Invoke(this, e);
        _window.KeyUp += (s, e) => KeyUp?.Invoke(this, e);
        _window.TextInput += (s, e) => TextInput?.Invoke(this, e);
        _window.PointerMoved += (s, e) => PointerMoved?.Invoke(this, e);
        _window.PointerPressed += (s, e) => PointerPressed?.Invoke(this, e);
        _window.PointerReleased += (s, e) => PointerReleased?.Invoke(this, e);
        _window.Scroll += (s, e) => Scroll?.Invoke(this, e);
        _window.Exposed += (s, e) => Exposed?.Invoke(this, e);
        _window.Resized += (s, e) => Resized?.Invoke(this, e);
        _window.CloseRequested += (s, e) => CloseRequested?.Invoke(this, e);
    }

    public void Show() => _window.Show();
    public void Hide() => _window.Hide();
    public void SetTitle(string title) => _window.SetTitle(title);
    public void Resize(int width, int height) => _window.Resize(width, height);
    public void ProcessEvents() => _window.ProcessEvents();
    public void Stop() => _window.Stop();
    public void Dispose() => _window.Dispose();
}

/// <summary>
/// Wayland display window wrapper implementing IDisplayWindow.
/// Uses the full WaylandWindow implementation with xdg-shell protocol.
/// </summary>
public class WaylandDisplayWindow : IDisplayWindow
{
    private readonly WaylandWindow _window;

    public int Width => _window.Width;
    public int Height => _window.Height;
    public bool IsRunning => _window.IsRunning;

    /// <summary>
    /// Gets the pixel data pointer for rendering.
    /// </summary>
    public IntPtr PixelData => _window.PixelData;

    /// <summary>
    /// Gets the stride (bytes per row) of the pixel buffer.
    /// </summary>
    public int Stride => _window.Stride;

    public event EventHandler<KeyEventArgs>? KeyDown;
    public event EventHandler<KeyEventArgs>? KeyUp;
    public event EventHandler<TextInputEventArgs>? TextInput;
    public event EventHandler<PointerEventArgs>? PointerMoved;
    public event EventHandler<PointerEventArgs>? PointerPressed;
    public event EventHandler<PointerEventArgs>? PointerReleased;
    public event EventHandler<ScrollEventArgs>? Scroll;
    public event EventHandler? Exposed;
    public event EventHandler<(int Width, int Height)>? Resized;
    public event EventHandler? CloseRequested;

    public WaylandDisplayWindow(string title, int width, int height)
    {
        _window = new WaylandWindow(title, width, height);

        // Wire up events
        _window.KeyDown += (s, e) => KeyDown?.Invoke(this, e);
        _window.KeyUp += (s, e) => KeyUp?.Invoke(this, e);
        _window.TextInput += (s, e) => TextInput?.Invoke(this, e);
        _window.PointerMoved += (s, e) => PointerMoved?.Invoke(this, e);
        _window.PointerPressed += (s, e) => PointerPressed?.Invoke(this, e);
        _window.PointerReleased += (s, e) => PointerReleased?.Invoke(this, e);
        _window.Scroll += (s, e) => Scroll?.Invoke(this, e);
        _window.Exposed += (s, e) => Exposed?.Invoke(this, e);
        _window.Resized += (s, e) => Resized?.Invoke(this, e);
        _window.CloseRequested += (s, e) => CloseRequested?.Invoke(this, e);
    }

    public void Show() => _window.Show();
    public void Hide() => _window.Hide();
    public void SetTitle(string title) => _window.SetTitle(title);
    public void Resize(int width, int height) => _window.Resize(width, height);
    public void ProcessEvents() => _window.ProcessEvents();
    public void Stop() => _window.Stop();
    public void CommitFrame() => _window.CommitFrame();
    public void Dispose() => _window.Dispose();
}
