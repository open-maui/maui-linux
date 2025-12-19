// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using SkiaSharp;
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
/// Uses wl_shm for software rendering with SkiaSharp.
/// </summary>
public class WaylandDisplayWindow : IDisplayWindow
{
    #region Native Interop

    private const string LibWaylandClient = "libwayland-client.so.0";

    [DllImport(LibWaylandClient)]
    private static extern IntPtr wl_display_connect(string? name);

    [DllImport(LibWaylandClient)]
    private static extern void wl_display_disconnect(IntPtr display);

    [DllImport(LibWaylandClient)]
    private static extern int wl_display_dispatch(IntPtr display);

    [DllImport(LibWaylandClient)]
    private static extern int wl_display_dispatch_pending(IntPtr display);

    [DllImport(LibWaylandClient)]
    private static extern int wl_display_roundtrip(IntPtr display);

    [DllImport(LibWaylandClient)]
    private static extern int wl_display_flush(IntPtr display);

    [DllImport(LibWaylandClient)]
    private static extern IntPtr wl_display_get_registry(IntPtr display);

    [DllImport(LibWaylandClient)]
    private static extern IntPtr wl_compositor_create_surface(IntPtr compositor);

    [DllImport(LibWaylandClient)]
    private static extern void wl_surface_attach(IntPtr surface, IntPtr buffer, int x, int y);

    [DllImport(LibWaylandClient)]
    private static extern void wl_surface_damage(IntPtr surface, int x, int y, int width, int height);

    [DllImport(LibWaylandClient)]
    private static extern void wl_surface_commit(IntPtr surface);

    [DllImport(LibWaylandClient)]
    private static extern void wl_surface_destroy(IntPtr surface);

    [DllImport(LibWaylandClient)]
    private static extern IntPtr wl_shm_create_pool(IntPtr shm, int fd, int size);

    [DllImport(LibWaylandClient)]
    private static extern void wl_shm_pool_destroy(IntPtr pool);

    [DllImport(LibWaylandClient)]
    private static extern IntPtr wl_shm_pool_create_buffer(IntPtr pool, int offset, int width, int height, int stride, uint format);

    [DllImport(LibWaylandClient)]
    private static extern void wl_buffer_destroy(IntPtr buffer);

    [DllImport("libc", EntryPoint = "shm_open")]
    private static extern int shm_open([MarshalAs(UnmanagedType.LPStr)] string name, int oflag, int mode);

    [DllImport("libc", EntryPoint = "shm_unlink")]
    private static extern int shm_unlink([MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport("libc", EntryPoint = "ftruncate")]
    private static extern int ftruncate(int fd, long length);

    [DllImport("libc", EntryPoint = "mmap")]
    private static extern IntPtr mmap(IntPtr addr, nuint length, int prot, int flags, int fd, long offset);

    [DllImport("libc", EntryPoint = "munmap")]
    private static extern int munmap(IntPtr addr, nuint length);

    [DllImport("libc", EntryPoint = "close")]
    private static extern int close(int fd);

    private const int O_RDWR = 2;
    private const int O_CREAT = 0x40;
    private const int O_EXCL = 0x80;
    private const int PROT_READ = 1;
    private const int PROT_WRITE = 2;
    private const int MAP_SHARED = 1;
    private const uint WL_SHM_FORMAT_XRGB8888 = 1;

    #endregion

    private IntPtr _display;
    private IntPtr _registry;
    private IntPtr _compositor;
    private IntPtr _shm;
    private IntPtr _surface;
    private IntPtr _shmPool;
    private IntPtr _buffer;
    private IntPtr _pixelData;
    private int _shmFd = -1;
    private int _bufferSize;

    private int _width;
    private int _height;
    private string _title;
    private bool _isRunning;
    private bool _disposed;

    private SKBitmap? _bitmap;
    private SKCanvas? _canvas;

    public int Width => _width;
    public int Height => _height;
    public bool IsRunning => _isRunning;

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
        _title = title;
        _width = width;
        _height = height;

        Initialize();
    }

    private void Initialize()
    {
        _display = wl_display_connect(null);
        if (_display == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to connect to Wayland display. Is WAYLAND_DISPLAY set?");
        }

        _registry = wl_display_get_registry(_display);
        if (_registry == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get Wayland registry");
        }

        // Note: A full implementation would set up registry listeners to get
        // compositor and shm handles. For now, we throw an informative error
        // and fall back to X11 via XWayland in DisplayServerFactory.
        
        // This is a placeholder - proper Wayland support requires:
        // 1. Setting up wl_registry_listener with callbacks
        // 2. Binding to wl_compositor, wl_shm, wl_seat, xdg_wm_base
        // 3. Implementing the xdg-shell protocol for toplevel windows
        
        wl_display_roundtrip(_display);

        // For now, signal that native Wayland isn't fully implemented
        throw new NotSupportedException(
            "Native Wayland support is experimental. " +
            "Set MAUI_PREFER_X11=1 to use XWayland, or run with DISPLAY set.");
    }

    private void CreateShmBuffer()
    {
        int stride = _width * 4;
        _bufferSize = stride * _height;

        string shmName = $"/maui-shm-{Environment.ProcessId}-{DateTime.Now.Ticks}";
        _shmFd = shm_open(shmName, O_RDWR | O_CREAT | O_EXCL, 0600);
        
        if (_shmFd < 0)
        {
            throw new InvalidOperationException("Failed to create shared memory file");
        }

        shm_unlink(shmName);

        if (ftruncate(_shmFd, _bufferSize) < 0)
        {
            close(_shmFd);
            throw new InvalidOperationException("Failed to resize shared memory");
        }

        _pixelData = mmap(IntPtr.Zero, (nuint)_bufferSize, PROT_READ | PROT_WRITE, MAP_SHARED, _shmFd, 0);
        if (_pixelData == IntPtr.Zero || _pixelData == new IntPtr(-1))
        {
            close(_shmFd);
            throw new InvalidOperationException("Failed to mmap shared memory");
        }

        _shmPool = wl_shm_create_pool(_shm, _shmFd, _bufferSize);
        if (_shmPool == IntPtr.Zero)
        {
            munmap(_pixelData, (nuint)_bufferSize);
            close(_shmFd);
            throw new InvalidOperationException("Failed to create wl_shm_pool");
        }

        _buffer = wl_shm_pool_create_buffer(_shmPool, 0, _width, _height, stride, WL_SHM_FORMAT_XRGB8888);
        if (_buffer == IntPtr.Zero)
        {
            wl_shm_pool_destroy(_shmPool);
            munmap(_pixelData, (nuint)_bufferSize);
            close(_shmFd);
            throw new InvalidOperationException("Failed to create wl_buffer");
        }

        // Create Skia bitmap backed by shared memory
        var info = new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        _bitmap = new SKBitmap();
        _bitmap.InstallPixels(info, _pixelData, stride);
        _canvas = new SKCanvas(_bitmap);
    }

    public void Show()
    {
        if (_surface == IntPtr.Zero || _buffer == IntPtr.Zero) return;

        wl_surface_attach(_surface, _buffer, 0, 0);
        wl_surface_damage(_surface, 0, 0, _width, _height);
        wl_surface_commit(_surface);
        wl_display_flush(_display);
    }

    public void Hide()
    {
        if (_surface == IntPtr.Zero) return;

        wl_surface_attach(_surface, IntPtr.Zero, 0, 0);
        wl_surface_commit(_surface);
        wl_display_flush(_display);
    }

    public void SetTitle(string title)
    {
        _title = title;
    }

    public void Resize(int width, int height)
    {
        if (width == _width && height == _height) return;

        _canvas?.Dispose();
        _bitmap?.Dispose();

        if (_buffer != IntPtr.Zero)
            wl_buffer_destroy(_buffer);
        if (_shmPool != IntPtr.Zero)
            wl_shm_pool_destroy(_shmPool);
        if (_pixelData != IntPtr.Zero)
            munmap(_pixelData, (nuint)_bufferSize);
        if (_shmFd >= 0)
            close(_shmFd);

        _width = width;
        _height = height;

        CreateShmBuffer();
        Resized?.Invoke(this, (width, height));
    }

    public void ProcessEvents()
    {
        if (!_isRunning || _display == IntPtr.Zero) return;

        wl_display_dispatch_pending(_display);
        wl_display_flush(_display);
    }

    public void Stop()
    {
        _isRunning = false;
    }

    public SKCanvas? GetCanvas() => _canvas;

    public void CommitFrame()
    {
        if (_surface != IntPtr.Zero && _buffer != IntPtr.Zero)
        {
            wl_surface_attach(_surface, _buffer, 0, 0);
            wl_surface_damage(_surface, 0, 0, _width, _height);
            wl_surface_commit(_surface);
            wl_display_flush(_display);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _isRunning = false;

        _canvas?.Dispose();
        _bitmap?.Dispose();

        if (_buffer != IntPtr.Zero)
        {
            wl_buffer_destroy(_buffer);
            _buffer = IntPtr.Zero;
        }

        if (_shmPool != IntPtr.Zero)
        {
            wl_shm_pool_destroy(_shmPool);
            _shmPool = IntPtr.Zero;
        }

        if (_pixelData != IntPtr.Zero && _pixelData != new IntPtr(-1))
        {
            munmap(_pixelData, (nuint)_bufferSize);
            _pixelData = IntPtr.Zero;
        }

        if (_shmFd >= 0)
        {
            close(_shmFd);
            _shmFd = -1;
        }

        if (_surface != IntPtr.Zero)
        {
            wl_surface_destroy(_surface);
            _surface = IntPtr.Zero;
        }

        if (_display != IntPtr.Zero)
        {
            wl_display_disconnect(_display);
            _display = IntPtr.Zero;
        }
    }
}
