using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>
/// GTK-based host window for MAUI applications on Linux.
/// Uses GTK3 with X11 backend for windowing and event handling.
/// </summary>
public sealed class GtkHostWindow : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool DeleteEventDelegate(IntPtr widget, IntPtr eventData, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool ConfigureEventDelegate(IntPtr widget, IntPtr eventData, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool ButtonEventDelegate(IntPtr widget, IntPtr eventData, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool MotionEventDelegate(IntPtr widget, IntPtr eventData, IntPtr userData);

    [StructLayout(LayoutKind.Explicit)]
    private struct GdkEventButton
    {
        [FieldOffset(0)]
        public int type;

        [FieldOffset(8)]
        public IntPtr window;

        [FieldOffset(16)]
        public sbyte send_event;

        [FieldOffset(20)]
        public uint time;

        [FieldOffset(24)]
        public double x;

        [FieldOffset(32)]
        public double y;

        [FieldOffset(40)]
        public IntPtr axes;

        [FieldOffset(48)]
        public uint state;

        [FieldOffset(52)]
        public uint button;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct GdkEventMotion
    {
        [FieldOffset(0)]
        public int type;

        [FieldOffset(8)]
        public IntPtr window;

        [FieldOffset(16)]
        public sbyte send_event;

        [FieldOffset(20)]
        public uint time;

        [FieldOffset(24)]
        public double x;

        [FieldOffset(32)]
        public double y;
    }

    private IntPtr _window;
    private IntPtr _overlay;
    private IntPtr _webViewLayer;
    private GtkSkiaSurfaceWidget? _skiaSurface;
    private bool _disposed;
    private bool _isRunning;
    private int _width;
    private int _height;

    private readonly DeleteEventDelegate _deleteEventHandler;
    private readonly ConfigureEventDelegate _configureEventHandler;
    private readonly ButtonEventDelegate _buttonPressHandler;
    private readonly ButtonEventDelegate _buttonReleaseHandler;
    private readonly MotionEventDelegate _motionHandler;
    private ulong _deleteSignalId;
    private ulong _configureSignalId;
    private ulong _buttonPressSignalId;
    private ulong _buttonReleaseSignalId;
    private ulong _motionSignalId;

    public IntPtr Window => _window;
    public IntPtr Overlay => _overlay;
    public IntPtr WebViewLayer => _webViewLayer;
    public GtkSkiaSurfaceWidget? SkiaSurface => _skiaSurface;
    public int Width => _width;
    public int Height => _height;
    public bool IsRunning => _isRunning;

    public event EventHandler<(int Width, int Height)>? Resized;
    public event EventHandler? CloseRequested;
    public event EventHandler<(double X, double Y, int Button)>? PointerPressed;
    public event EventHandler<(double X, double Y, int Button)>? PointerReleased;
    public event EventHandler<(double X, double Y)>? PointerMoved;

    public GtkHostWindow(string title, int width, int height)
    {
        _width = width;
        _height = height;

        // Configure environment for GTK/X11
        Environment.SetEnvironmentVariable("GDK_BACKEND", "x11");
        Environment.SetEnvironmentVariable("WEBKIT_DISABLE_SANDBOX_THIS_IS_DANGEROUS", "1");
        Environment.SetEnvironmentVariable("LIBGL_ALWAYS_SOFTWARE", "1");

        int argc = 0;
        IntPtr argv = IntPtr.Zero;
        if (!GtkNative.gtk_init_check(ref argc, ref argv))
        {
            throw new InvalidOperationException("Failed to initialize GTK. Is a display available?");
        }

        _window = GtkNative.gtk_window_new(0);
        if (_window == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create GTK window");
        }

        GtkNative.gtk_window_set_title(_window, title);
        GtkNative.gtk_window_set_default_size(_window, width, height);

        // Create overlay container for layered content
        _overlay = GtkNative.gtk_overlay_new();
        GtkNative.gtk_container_add(_window, _overlay);

        // Create Skia surface as base layer
        _skiaSurface = new GtkSkiaSurfaceWidget(width, height);
        GtkNative.gtk_container_add(_overlay, _skiaSurface.Widget);

        // Create fixed container for WebView overlays
        _webViewLayer = GtkNative.gtk_fixed_new();
        GtkNative.gtk_overlay_add_overlay(_overlay, _webViewLayer);
        GtkNative.gtk_widget_set_can_focus(_webViewLayer, canFocus: false);
        GtkNative.gtk_overlay_set_overlay_pass_through(_overlay, _webViewLayer, passThrough: true);

        // Store delegates to prevent garbage collection
        _deleteEventHandler = OnDeleteEvent;
        _configureEventHandler = OnConfigureEvent;
        _buttonPressHandler = OnButtonPress;
        _buttonReleaseHandler = OnButtonRelease;
        _motionHandler = OnMotion;

        // Connect event handlers
        _deleteSignalId = GtkNative.g_signal_connect_data(_window, "delete-event", Marshal.GetFunctionPointerForDelegate(_deleteEventHandler), IntPtr.Zero, IntPtr.Zero, 0);
        _configureSignalId = GtkNative.g_signal_connect_data(_window, "configure-event", Marshal.GetFunctionPointerForDelegate(_configureEventHandler), IntPtr.Zero, IntPtr.Zero, 0);

        // Add pointer event masks
        GtkNative.gtk_widget_add_events(_window, 772);
        _buttonPressSignalId = GtkNative.g_signal_connect_data(_window, "button-press-event", Marshal.GetFunctionPointerForDelegate(_buttonPressHandler), IntPtr.Zero, IntPtr.Zero, 0);
        _buttonReleaseSignalId = GtkNative.g_signal_connect_data(_window, "button-release-event", Marshal.GetFunctionPointerForDelegate(_buttonReleaseHandler), IntPtr.Zero, IntPtr.Zero, 0);
        _motionSignalId = GtkNative.g_signal_connect_data(_window, "motion-notify-event", Marshal.GetFunctionPointerForDelegate(_motionHandler), IntPtr.Zero, IntPtr.Zero, 0);

        DiagnosticLog.Debug("GtkHostWindow", $"Created GTK window on X11: {width}x{height}");
    }

    private bool OnDeleteEvent(IntPtr widget, IntPtr eventData, IntPtr userData)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        _isRunning = false;
        GtkNative.gtk_main_quit();
        return true;
    }

    private bool OnConfigureEvent(IntPtr widget, IntPtr eventData, IntPtr userData)
    {
        GtkNative.gtk_window_get_size(_window, out var width, out var height);
        if (width != _width || height != _height)
        {
            _width = width;
            _height = height;
            _skiaSurface?.Resize(width, height);
            Resized?.Invoke(this, (_width, _height));
        }
        return false;
    }

    private bool OnButtonPress(IntPtr widget, IntPtr eventData, IntPtr userData)
    {
        var (x, y, button) = ParseButtonEvent(eventData);
        string buttonName = button switch
        {
            3 => "Right",
            2 => "Middle",
            1 => "Left",
            _ => $"Other({button})",
        };
        DiagnosticLog.Debug("GtkHostWindow", $"ButtonPress at ({x:F1}, {y:F1}), button={button} ({buttonName})");
        PointerPressed?.Invoke(this, (x, y, button));
        _skiaSurface?.RaisePointerPressed(x, y, button);
        return false;
    }

    private bool OnButtonRelease(IntPtr widget, IntPtr eventData, IntPtr userData)
    {
        var (x, y, button) = ParseButtonEvent(eventData);
        PointerReleased?.Invoke(this, (x, y, button));
        _skiaSurface?.RaisePointerReleased(x, y, button);
        return false;
    }

    private bool OnMotion(IntPtr widget, IntPtr eventData, IntPtr userData)
    {
        var (x, y) = ParseMotionEvent(eventData);
        PointerMoved?.Invoke(this, (x, y));
        _skiaSurface?.RaisePointerMoved(x, y);
        return false;
    }

    private static (double x, double y, int button) ParseButtonEvent(IntPtr eventData)
    {
        var evt = Marshal.PtrToStructure<GdkEventButton>(eventData);
        return (evt.x, evt.y, (int)evt.button);
    }

    private static (double x, double y) ParseMotionEvent(IntPtr eventData)
    {
        var evt = Marshal.PtrToStructure<GdkEventMotion>(eventData);
        return (evt.x, evt.y);
    }

    public void Show()
    {
        GtkNative.gtk_widget_show_all(_window);
        _isRunning = true;
    }

    public void Hide()
    {
        GtkNative.gtk_widget_hide(_window);
    }

    public void SetTitle(string title)
    {
        GtkNative.gtk_window_set_title(_window, title);
    }

    public void SetIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath) || !File.Exists(iconPath))
        {
            DiagnosticLog.Warn("GtkHostWindow", "Icon file not found: " + iconPath);
            return;
        }
        try
        {
            IntPtr pixbuf = GtkNative.gdk_pixbuf_new_from_file(iconPath, IntPtr.Zero);
            if (pixbuf != IntPtr.Zero)
            {
                GtkNative.gtk_window_set_icon(_window, pixbuf);
                GtkNative.g_object_unref(pixbuf);
                DiagnosticLog.Debug("GtkHostWindow", "Set window icon: " + iconPath);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("GtkHostWindow", "Failed to set icon", ex);
        }
    }

    public void Resize(int width, int height)
    {
        GtkNative.gtk_window_resize(_window, width, height);
    }

    public void AddWebView(IntPtr webViewWidget, int x, int y, int width, int height)
    {
        GtkNative.gtk_widget_set_size_request(webViewWidget, width, height);
        GtkNative.gtk_fixed_put(_webViewLayer, webViewWidget, x, y);
        GtkNative.gtk_widget_show(webViewWidget);
        DiagnosticLog.Debug("GtkHostWindow", $"Added WebView at ({x}, {y}) size {width}x{height}");
    }

    public void MoveResizeWebView(IntPtr webViewWidget, int x, int y, int width, int height)
    {
        GtkNative.gtk_widget_set_size_request(webViewWidget, width, height);
        GtkNative.gtk_fixed_move(_webViewLayer, webViewWidget, x, y);
    }

    public void RemoveWebView(IntPtr webViewWidget)
    {
        GtkNative.gtk_container_remove(_webViewLayer, webViewWidget);
    }

    public void RequestRedraw()
    {
        if (_skiaSurface != null)
        {
            GtkNative.gtk_widget_queue_draw(_skiaSurface.Widget);
        }
    }

    public void Run()
    {
        Show();
        GtkNative.gtk_main();
    }

    public void Stop()
    {
        _isRunning = false;
        GtkNative.gtk_main_quit();
    }

    public void ProcessEvents()
    {
        while (GtkNative.gtk_events_pending())
        {
            GtkNative.gtk_main_iteration_do(blocking: false);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Disconnect signal handlers before destroying the widget
            if (_window != IntPtr.Zero)
            {
                if (_deleteSignalId != 0) GtkNative.g_signal_handler_disconnect(_window, _deleteSignalId);
                if (_configureSignalId != 0) GtkNative.g_signal_handler_disconnect(_window, _configureSignalId);
                if (_buttonPressSignalId != 0) GtkNative.g_signal_handler_disconnect(_window, _buttonPressSignalId);
                if (_buttonReleaseSignalId != 0) GtkNative.g_signal_handler_disconnect(_window, _buttonReleaseSignalId);
                if (_motionSignalId != 0) GtkNative.g_signal_handler_disconnect(_window, _motionSignalId);
            }

            _skiaSurface?.Dispose();
            if (_window != IntPtr.Zero)
            {
                GtkNative.gtk_widget_destroy(_window);
                _window = IntPtr.Zero;
            }
        }
    }
}
