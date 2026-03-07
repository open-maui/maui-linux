using System;
using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// GTK drawing area widget that renders Skia content via Cairo.
/// Provides hardware-accelerated 2D rendering for MAUI views.
/// </summary>
public sealed class GtkSkiaSurfaceWidget : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool DrawCallback(IntPtr widget, IntPtr cairoContext, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool ConfigureCallback(IntPtr widget, IntPtr eventData, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool ButtonEventCallback(IntPtr widget, IntPtr eventData, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool MotionEventCallback(IntPtr widget, IntPtr eventData, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool KeyEventCallback(IntPtr widget, IntPtr eventData, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool ScrollEventCallback(IntPtr widget, IntPtr eventData, IntPtr userData);

    private struct GdkEventButton
    {
        public int type;
        public IntPtr window;
        public sbyte send_event;
        public uint time;
        public double x;
        public double y;
        public IntPtr axes;
        public uint state;
        public uint button;
    }

    private struct GdkEventMotion
    {
        public int type;
        public IntPtr window;
        public sbyte send_event;
        public uint time;
        public double x;
        public double y;
    }

    private struct GdkEventKey
    {
        public int type;
        public IntPtr window;
        public sbyte send_event;
        public uint time;
        public uint state;
        public uint keyval;
        public int length;
        public IntPtr str;
        public ushort hardware_keycode;
    }

    private struct GdkEventScroll
    {
        public int type;
        public IntPtr window;
        public sbyte send_event;
        public uint time;
        public double x;
        public double y;
        public uint state;
        public int direction;
        public IntPtr device;
        public double x_root;
        public double y_root;
        public double delta_x;
        public double delta_y;
    }

    private IntPtr _widget;
    private SKImageInfo _imageInfo;
    private SKBitmap? _bitmap;
    private SKCanvas? _canvas;
    private IntPtr _cairoSurface;
    private readonly DrawCallback _drawCallback;
    private readonly ConfigureCallback _configureCallback;
    private ulong _drawSignalId;
    private ulong _configureSignalId;
    private bool _isTransparent;
    private readonly ButtonEventCallback _buttonPressCallback;
    private readonly ButtonEventCallback _buttonReleaseCallback;
    private readonly MotionEventCallback _motionCallback;
    private readonly KeyEventCallback _keyPressCallback;
    private readonly KeyEventCallback _keyReleaseCallback;
    private readonly ScrollEventCallback _scrollCallback;

    public IntPtr Widget => _widget;
    public SKCanvas? Canvas => _canvas;
    public SKImageInfo ImageInfo => _imageInfo;
    public int Width => _imageInfo.Width;
    public int Height => _imageInfo.Height;
    public bool IsTransparent => _isTransparent;

    public event EventHandler? DrawRequested;
    public event EventHandler<(int Width, int Height)>? Resized;
    public event EventHandler<(double X, double Y, int Button)>? PointerPressed;
    public event EventHandler<(double X, double Y, int Button)>? PointerReleased;
    public event EventHandler<(double X, double Y)>? PointerMoved;
    public event EventHandler<(uint KeyVal, uint KeyCode, uint State)>? KeyPressed;
    public event EventHandler<(uint KeyVal, uint KeyCode, uint State)>? KeyReleased;
    public event EventHandler<(double X, double Y, double DeltaX, double DeltaY, uint State)>? Scrolled;
    public event EventHandler<string>? TextInput;

    public GtkSkiaSurfaceWidget(int width, int height)
    {
        _widget = GtkNative.gtk_drawing_area_new();
        if (_widget == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create GTK drawing area");
        }

        GtkNative.gtk_widget_set_size_request(_widget, width, height);
        GtkNative.gtk_widget_add_events(_widget, 10551046);
        GtkNative.gtk_widget_set_can_focus(_widget, canFocus: true);

        CreateBuffer(width, height);

        // Store delegates to prevent garbage collection
        _drawCallback = OnDraw;
        _configureCallback = OnConfigure;
        _buttonPressCallback = OnButtonPress;
        _buttonReleaseCallback = OnButtonRelease;
        _motionCallback = OnMotion;
        _keyPressCallback = OnKeyPress;
        _keyReleaseCallback = OnKeyRelease;
        _scrollCallback = OnScroll;

        // Connect signals
        _drawSignalId = GtkNative.g_signal_connect_data(_widget, "draw", Marshal.GetFunctionPointerForDelegate(_drawCallback), IntPtr.Zero, IntPtr.Zero, 0);
        _configureSignalId = GtkNative.g_signal_connect_data(_widget, "configure-event", Marshal.GetFunctionPointerForDelegate(_configureCallback), IntPtr.Zero, IntPtr.Zero, 0);
        GtkNative.g_signal_connect_data(_widget, "button-press-event", Marshal.GetFunctionPointerForDelegate(_buttonPressCallback), IntPtr.Zero, IntPtr.Zero, 0);
        GtkNative.g_signal_connect_data(_widget, "button-release-event", Marshal.GetFunctionPointerForDelegate(_buttonReleaseCallback), IntPtr.Zero, IntPtr.Zero, 0);
        GtkNative.g_signal_connect_data(_widget, "motion-notify-event", Marshal.GetFunctionPointerForDelegate(_motionCallback), IntPtr.Zero, IntPtr.Zero, 0);
        GtkNative.g_signal_connect_data(_widget, "key-press-event", Marshal.GetFunctionPointerForDelegate(_keyPressCallback), IntPtr.Zero, IntPtr.Zero, 0);
        GtkNative.g_signal_connect_data(_widget, "key-release-event", Marshal.GetFunctionPointerForDelegate(_keyReleaseCallback), IntPtr.Zero, IntPtr.Zero, 0);
        GtkNative.g_signal_connect_data(_widget, "scroll-event", Marshal.GetFunctionPointerForDelegate(_scrollCallback), IntPtr.Zero, IntPtr.Zero, 0);

        DiagnosticLog.Debug("GtkSkiaSurfaceWidget", $"Created with size {width}x{height}");
    }

    private void CreateBuffer(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        _canvas?.Dispose();
        _bitmap?.Dispose();

        if (_cairoSurface != IntPtr.Zero)
        {
            CairoNative.cairo_surface_destroy(_cairoSurface);
            _cairoSurface = IntPtr.Zero;
        }

        _imageInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _bitmap = new SKBitmap(_imageInfo);
        _canvas = new SKCanvas(_bitmap);

        IntPtr pixels = _bitmap.GetPixels();
        _cairoSurface = CairoNative.cairo_image_surface_create_for_data(
            pixels,
            CairoNative.cairo_format_t.CAIRO_FORMAT_ARGB32,
            _imageInfo.Width,
            _imageInfo.Height,
            _imageInfo.RowBytes);

        DiagnosticLog.Debug("GtkSkiaSurfaceWidget", $"Created buffer {width}x{height}, stride={_imageInfo.RowBytes}");
    }

    public void Resize(int width, int height)
    {
        if (width != _imageInfo.Width || height != _imageInfo.Height)
        {
            CreateBuffer(width, height);
            Resized?.Invoke(this, (width, height));
        }
    }

    public void RenderFrame(Action<SKCanvas, SKImageInfo> render)
    {
        if (_canvas != null && _bitmap != null)
        {
            render(_canvas, _imageInfo);
            _canvas.Flush();
            CairoNative.cairo_surface_flush(_cairoSurface);
            CairoNative.cairo_surface_mark_dirty(_cairoSurface);
            GtkNative.gtk_widget_queue_draw(_widget);
        }
    }

    public void Invalidate()
    {
        GtkNative.gtk_widget_queue_draw(_widget);
    }

    public void SetTransparent(bool transparent)
    {
        _isTransparent = transparent;
    }

    private bool OnDraw(IntPtr widget, IntPtr cairoContext, IntPtr userData)
    {
        if (_cairoSurface == IntPtr.Zero || cairoContext == IntPtr.Zero)
        {
            return false;
        }

        if (_isTransparent)
        {
            _canvas?.Clear(SKColors.Transparent);
        }

        DrawRequested?.Invoke(this, EventArgs.Empty);
        _canvas?.Flush();

        CairoNative.cairo_surface_flush(_cairoSurface);
        CairoNative.cairo_surface_mark_dirty(_cairoSurface);
        CairoNative.cairo_set_source_surface(cairoContext, _cairoSurface, 0.0, 0.0);
        CairoNative.cairo_paint(cairoContext);

        return true;
    }

    private bool OnConfigure(IntPtr widget, IntPtr eventData, IntPtr userData)
    {
        GtkNative.gtk_widget_get_allocation(widget, out var allocation);
        if (allocation.Width > 0 && allocation.Height > 0 &&
            (allocation.Width != _imageInfo.Width || allocation.Height != _imageInfo.Height))
        {
            Resize(allocation.Width, allocation.Height);
        }
        return false;
    }

    private bool OnButtonPress(IntPtr widget, IntPtr eventData, IntPtr userData)
    {
        GtkNative.gtk_widget_grab_focus(_widget);
        var (x, y, button, eventType) = ParseButtonEvent(eventData);

        // GTK event types: GDK_BUTTON_PRESS=4, GDK_2BUTTON_PRESS=5, GDK_3BUTTON_PRESS=6
        // Only process single button press events. GTK sends 2BUTTON_PRESS and 3BUTTON_PRESS
        // events after detecting double/triple clicks, but we handle that ourselves in SkiaEntry.
        if (eventType != 4) // GDK_BUTTON_PRESS
            return true;

        PointerPressed?.Invoke(this, (x, y, button));
        return true;
    }

    private bool OnButtonRelease(IntPtr widget, IntPtr eventData, IntPtr userData)
    {
        var (x, y, button, _) = ParseButtonEvent(eventData);
        PointerReleased?.Invoke(this, (x, y, button));
        return true;
    }

    private bool OnMotion(IntPtr widget, IntPtr eventData, IntPtr userData)
    {
        var (x, y) = ParseMotionEvent(eventData);
        PointerMoved?.Invoke(this, (x, y));
        return true;
    }

    public void RaisePointerPressed(double x, double y, int button)
    {
        PointerPressed?.Invoke(this, (x, y, button));
    }

    public void RaisePointerReleased(double x, double y, int button)
    {
        PointerReleased?.Invoke(this, (x, y, button));
    }

    public void RaisePointerMoved(double x, double y)
    {
        PointerMoved?.Invoke(this, (x, y));
    }

    private bool OnKeyPress(IntPtr widget, IntPtr eventData, IntPtr userData)
    {
        var (keyval, keycode, state) = ParseKeyEvent(eventData);
        KeyPressed?.Invoke(this, (keyval, keycode, state));

        uint unicode = GdkNative.gdk_keyval_to_unicode(keyval);
        if (unicode != 0 && unicode < 65536)
        {
            char c = (char)unicode;
            if (!char.IsControl(c) || c == '\r' || c == '\n' || c == '\t')
            {
                string text = c.ToString();
                DiagnosticLog.Debug("GtkSkiaSurfaceWidget", $"TextInput: '{text}' (keyval={keyval}, unicode={unicode})");
                TextInput?.Invoke(this, text);
            }
        }
        return true;
    }

    private bool OnKeyRelease(IntPtr widget, IntPtr eventData, IntPtr userData)
    {
        var (keyval, keycode, state) = ParseKeyEvent(eventData);
        KeyReleased?.Invoke(this, (keyval, keycode, state));
        return true;
    }

    private bool OnScroll(IntPtr widget, IntPtr eventData, IntPtr userData)
    {
        var (x, y, deltaX, deltaY, state) = ParseScrollEvent(eventData);
        Scrolled?.Invoke(this, (x, y, deltaX, deltaY, state));
        return true;
    }

    private static (double x, double y, int button, int eventType) ParseButtonEvent(IntPtr eventData)
    {
        var evt = Marshal.PtrToStructure<GdkEventButton>(eventData);
        return (evt.x, evt.y, (int)evt.button, evt.type);
    }

    private static (double x, double y) ParseMotionEvent(IntPtr eventData)
    {
        var evt = Marshal.PtrToStructure<GdkEventMotion>(eventData);
        return (evt.x, evt.y);
    }

    private static (uint keyval, uint keycode, uint state) ParseKeyEvent(IntPtr eventData)
    {
        var evt = Marshal.PtrToStructure<GdkEventKey>(eventData);
        return (evt.keyval, evt.hardware_keycode, evt.state);
    }

    private static (double x, double y, double deltaX, double deltaY, uint state) ParseScrollEvent(IntPtr eventData)
    {
        var evt = Marshal.PtrToStructure<GdkEventScroll>(eventData);
        double deltaX = 0.0;
        double deltaY = 0.0;

        if (evt.direction == 4) // GDK_SCROLL_SMOOTH
        {
            deltaX = evt.delta_x;
            deltaY = evt.delta_y;
        }
        else
        {
            switch (evt.direction)
            {
                case 0: // GDK_SCROLL_UP
                    deltaY = -1.0;
                    break;
                case 1: // GDK_SCROLL_DOWN
                    deltaY = 1.0;
                    break;
                case 2: // GDK_SCROLL_LEFT
                    deltaX = -1.0;
                    break;
                case 3: // GDK_SCROLL_RIGHT
                    deltaX = 1.0;
                    break;
            }
        }
        return (evt.x, evt.y, deltaX, deltaY, evt.state);
    }

    public void GrabFocus()
    {
        GtkNative.gtk_widget_grab_focus(_widget);
    }

    public void Dispose()
    {
        _canvas?.Dispose();
        _canvas = null;

        _bitmap?.Dispose();
        _bitmap = null;

        if (_cairoSurface != IntPtr.Zero)
        {
            CairoNative.cairo_surface_destroy(_cairoSurface);
            _cairoSurface = IntPtr.Zero;
        }
    }
}
