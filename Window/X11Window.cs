// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Interop;
using Microsoft.Maui.Platform.Linux.Input;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Svg.Skia;

namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>
/// X11 window implementation for Linux.
/// </summary>
public partial class X11Window : Microsoft.Maui.Platform.Linux.Services.IDisplayWindow,
                        Microsoft.Maui.Platform.Linux.Services.IX11Surface,
                        Microsoft.Maui.Platform.Linux.Services.IScaleAwareDisplayWindow,
                        Microsoft.Maui.Platform.Linux.Services.IDesktopWindowControl,
                        Microsoft.Maui.Platform.Linux.Services.IVisibilityAwareDisplayWindow,
                        IClientDecoratedWindow,
                        IX11DecorationHost,
                        Microsoft.Maui.Platform.Linux.Services.IActivatableWindow
{
    private bool _isSuspended;

    /// <inheritdoc />
    public bool IsSuspended => _isSuspended;

    /// <inheritdoc />
    public event EventHandler<bool>? SuspendedChanged;

    private void SetSuspended(bool suspended)
    {
        if (_isSuspended == suspended) return;
        _isSuspended = suspended;
        SuspendedChanged?.Invoke(this, suspended);
    }

    // Runtime scale on X11: the scale is global (Xft.dpi in RESOURCE_MANAGER,
    // or the desktop's own setting), so the window watches the root window's
    // RESOURCE_MANAGER and re-detects when a settings daemon rewrites it.
    private IntPtr _rootWindow;
    private IntPtr _resourceManagerAtom;
    private float _scale = 1f;

    /// <inheritdoc />
    public float Scale => _scale;

    /// <inheritdoc />
    public event EventHandler<float>? ScaleChanged;

    /// <summary>Scale detection used on RESOURCE_MANAGER changes (a seam for tests).</summary>
    internal static Func<float> DetectScale { get; set; } = () =>
    {
        var hiDpi = new Microsoft.Maui.Platform.Linux.Services.HiDpiService();
        hiDpi.DetectScaleFactor();
        return hiDpi.ScaleFactor;
    };

    /// <summary>
    /// Re-detects the desktop scale and raises <see cref="ScaleChanged"/> when
    /// it moved. The window keeps its pixel size: X11 applications re-lay out
    /// at the new logical size rather than being resized.
    /// </summary>
    internal void RefreshScale()
    {
        float detected;
        try { detected = DetectScale(); }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("X11Window", $"Scale re-detection failed: {ex.Message}");
            return;
        }
        if (detected <= 0f || Math.Abs(detected - _scale) < 0.01f) return;
        DiagnosticLog.Info("X11Window", $"Desktop scale {_scale:0.##} -> {detected:0.##}");
        _scale = detected;
        ScaleChanged?.Invoke(this, detected);
    }

    private IntPtr _display;
    private IntPtr _window;
    private IntPtr _wmDeleteMessage;
    private IntPtr _wmSyncRequest;
    private IntPtr _netWmSyncRequestCounter;
    private long _syncCounter;
    private long _syncCounterValue;
    private bool _syncPending;
    private int _screen;
    private bool _disposed;
    private bool _isRunning;

    private int _width;
    private int _height;

    // Cursor handles
    private IntPtr _arrowCursor;
    private IntPtr _handCursor;
    private IntPtr _textCursor;
    private IntPtr _sizeWestEastCursor;
    private IntPtr _sizeNorthSouthCursor;
    private IntPtr _sizeAllCursor;
    private IntPtr _currentCursor;
    private CursorType _currentCursorType = CursorType.Arrow;

    private static int _eventCounter;

    // Client-side decorations (used while the MAUI window has a TitleBar), the window title
    // they draw, the maximized state (_NET_WM_STATE) and the resize-edge cursor shown on hover.
    private readonly X11ClientDecorations _decorations;
    private string _title;
    private bool _isMaximized;
    private IntPtr _netWmStateAtom;
    private CsdResizeEdge _hoverEdge;
    private readonly Dictionary<CsdResizeEdge, IntPtr> _edgeCursors = new();
    private PointerButton _csdSwallowedReleases;
    private readonly Dictionary<string, bool> _wmSupported = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the native display handle.
    /// </summary>
    public IntPtr Display => _display;

    /// <summary>
    /// Gets the native window handle.
    /// </summary>
    public IntPtr Handle => _window;

    /// <summary>
    /// Gets the window width.
    /// </summary>
    public int Width => _width;

    /// <summary>
    /// Gets the window height.
    /// </summary>
    public int Height => _height;

    /// <summary>
    /// Gets whether the window is running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Event raised when a key is pressed.
    /// </summary>
    public event EventHandler<KeyEventArgs>? KeyDown;

    /// <summary>
    /// Event raised when a key is released.
    /// </summary>
    public event EventHandler<KeyEventArgs>? KeyUp;

    /// <summary>
    /// Event raised when text is input.
    /// </summary>
    public event EventHandler<TextInputEventArgs>? TextInput;

    /// <summary>
    /// Event raised when the pointer moves.
    /// </summary>
    public event EventHandler<PointerEventArgs>? PointerMoved;

    /// <summary>
    /// Event raised when a pointer button is pressed.
    /// </summary>
    public event EventHandler<PointerEventArgs>? PointerPressed;

    /// <summary>
    /// Event raised when a pointer button is released.
    /// </summary>
    public event EventHandler<PointerEventArgs>? PointerReleased;

    /// <summary>
    /// Event raised when the mouse wheel is scrolled.
    /// </summary>
    public event EventHandler<ScrollEventArgs>? Scroll;

    /// <summary>
    /// Event raised when the window needs to be redrawn.
    /// </summary>
    public event EventHandler? Exposed;

    /// <summary>
    /// Event raised when the window is resized.
    /// </summary>
    public event EventHandler<(int Width, int Height)>? Resized;

    /// <summary>
    /// Event raised when the window close is requested.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Event raised when the window gains focus.
    /// </summary>
    public event EventHandler? FocusGained;

    /// <summary>
    /// Event raised when the window loses focus.
    /// </summary>
    public event EventHandler? FocusLost;

    /// <summary>
    /// Creates a new X11 window.
    /// </summary>
    public X11Window(string title, int width, int height)
    {
        _width = width;
        _height = height;
        _title = title ?? string.Empty;
        _decorations = new X11ClientDecorations(this);

        // Open display
        _display = X11.XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero)
            throw new InvalidOperationException("Failed to open X11 display. Is X11 running?");

        _screen = X11.XDefaultScreen(_display);
        var rootWindow = X11.XRootWindow(_display, _screen);

        // Create window
        _window = X11.XCreateSimpleWindow(
            _display,
            rootWindow,
            0, 0,
            (uint)width, (uint)height,
            0,
            0,
            0x000000 // Black background (less visible during resize flash)
        );

        if (_window == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create X11 window");

        // Set window title
        X11.XStoreName(_display, _window, title);

        // Set WM_CLASS for desktop integration (taskbar icon matching)
        // Use application name from environment or process path for proper desktop matching
        string? appName = Environment.GetEnvironmentVariable("APPIMAGE_NAME");
        if (string.IsNullOrEmpty(appName))
        {
            appName = System.IO.Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "MauiApp");
        }
        string wmClass = appName.Replace(" ", "").Replace("_", "");
        SetWMClass(wmClass, wmClass);

        // Select input events. PropertyChangeMask is required for XDND INCR
        // transfers (chunk arrival is signalled by PropertyNotify).
        X11.XSelectInput(_display, _window,
            XEventMask.KeyPressMask |
            XEventMask.KeyReleaseMask |
            XEventMask.ButtonPressMask |
            XEventMask.ButtonReleaseMask |
            XEventMask.PointerMotionMask |
            XEventMask.EnterWindowMask |
            XEventMask.LeaveWindowMask |
            XEventMask.ExposureMask |
            XEventMask.StructureNotifyMask |
            XEventMask.PropertyChangeMask |
            XEventMask.FocusChangeMask);

        // Desktop scale changes arrive as a rewrite of RESOURCE_MANAGER (Xft.dpi)
        // on the root window.
        _scale = LinuxApplication.Current is { DpiScale: > 0f } scaleApp ? scaleApp.DpiScale : 1f;
        _rootWindow = X11.XRootWindow(_display, _screen);
        _resourceManagerAtom = X11.XInternAtom(_display, "RESOURCE_MANAGER", false);
        X11.XSelectInput(_display, _rootWindow, XEventMask.PropertyChangeMask);

        // Wire drag-and-drop: interns the XDND atoms and announces XdndAware
        // on the window so drags from other apps are offered to us. XDND
        // events are routed into the service from HandleEvent below.
        DragDropService.Default.RegisterWindow(_display, _window, this);

        // Set up WM protocols
        _wmDeleteMessage = X11.XInternAtom(_display, "WM_DELETE_WINDOW", false);
        _wmSyncRequest = X11.XInternAtom(_display, "_NET_WM_SYNC_REQUEST", false);
        _netWmSyncRequestCounter = X11.XInternAtom(_display, "_NET_WM_SYNC_REQUEST_COUNTER", false);

        // Register WM protocols and sync counter for live resize
        var protocols = new IntPtr[] { _wmDeleteMessage, _wmSyncRequest };
        X11.XSetWMProtocols(_display, _window, protocols, protocols.Length);

        // Create sync counter so the compositor sends ConfigureNotify during resize drag
        if (X11.XSyncInitialize(_display, out _, out _) != 0)
        {
            _syncCounterValue = 0;
            _syncCounter = X11.XSyncCreateCounter(_display, new X11.XSyncValue(0));
            if (_syncCounter != 0)
            {
                // Set _NET_WM_SYNC_REQUEST_COUNTER property on the window
                unsafe
                {
                    long counterValue = _syncCounter;
                    var ptr = (IntPtr)(&counterValue);
                    var xa_cardinal = X11.XInternAtom(_display, "CARDINAL", false);
                    X11.XChangeProperty(_display, _window, _netWmSyncRequestCounter,
                        xa_cardinal, 32, 0 /* PropModeReplace */, ptr, 1);
                }
            }
        }

        // Initialize cursors. Prefer themed cursors via libXcursor (respects the user's
        // cursor theme and XCURSOR_SIZE for HiDPI). Fall back to legacy bitmap font cursors
        // when libXcursor is unavailable or the theme doesn't define a given name.
        _arrowCursor = LoadCursor("default", 68);  // XC_left_ptr
        _handCursor = LoadCursor("pointer", 60);   // XC_hand2
        _textCursor = LoadCursor("text", 152);     // XC_xterm
        _sizeWestEastCursor = LoadCursor("ew-resize", 108);   // XC_sb_h_double_arrow
        _sizeNorthSouthCursor = LoadCursor("ns-resize", 116); // XC_sb_v_double_arrow
        _sizeAllCursor = LoadCursor("move", 52);              // XC_fleur
        _currentCursor = _arrowCursor;

        _netWmStateAtom = X11.XInternAtom(_display, "_NET_WM_STATE", false);

        // Touchscreens through XInput 2.2 (core events stay selected for the mouse), and the
        // launcher's startup id (focus-stealing prevention, busy cursor) before the first map.
        InitializeXInput2();
        ApplyStartupId();
    }

    private IntPtr LoadCursor(string themeName, uint fontShape)
    {
        try
        {
            var cursor = Xcursor.XcursorLibraryLoadCursor(_display, themeName);
            if (cursor != IntPtr.Zero)
                return cursor;
        }
        catch (DllNotFoundException)
        {
            // libXcursor not installed; fall through to font cursor.
        }
        return X11.XCreateFontCursor(_display, fontShape);
    }

    /// <summary>
    /// Sets the cursor type for this window.
    /// </summary>
    public void SetCursor(CursorType cursorType)
    {
        if (_currentCursorType != cursorType)
        {
            _currentCursorType = cursorType;
            IntPtr cursor = cursorType switch
            {
                CursorType.Hand => _handCursor,
                CursorType.Text => _textCursor,
                CursorType.SizeWestEast => _sizeWestEastCursor,
                CursorType.SizeNorthSouth => _sizeNorthSouthCursor,
                CursorType.SizeAll => _sizeAllCursor,
                _ => _arrowCursor,
            };
            if (cursor != _currentCursor)
            {
                _currentCursor = cursor;
                // Over a resize edge of our own decorations the edge's cursor stays; the
                // app's cursor comes back when the pointer leaves the edge.
                if (_hoverEdge == CsdResizeEdge.None)
                {
                    X11.XDefineCursor(_display, _window, _currentCursor);
                    X11.XFlush(_display);
                }
            }
        }
    }

    /// <summary>
    /// Sets the WM_CLASS property for desktop integration.
    /// This allows the desktop to match the window to its .desktop file.
    /// </summary>
    public void SetWMClass(string resName, string resClass)
    {
        IntPtr namePtr = IntPtr.Zero;
        IntPtr classPtr = IntPtr.Zero;
        try
        {
            namePtr = System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(resName);
            classPtr = System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(resClass);

            var classHint = new XClassHint
            {
                res_name = namePtr,
                res_class = classPtr
            };

            X11.XSetClassHint(_display, _window, ref classHint);
            DiagnosticLog.Debug("X11Window", $"Set WM_CLASS: {resName}, {resClass}");
        }
        finally
        {
            if (namePtr != IntPtr.Zero)
                System.Runtime.InteropServices.Marshal.FreeHGlobal(namePtr);
            if (classPtr != IntPtr.Zero)
                System.Runtime.InteropServices.Marshal.FreeHGlobal(classPtr);
        }
    }

    /// <summary>
    /// Sets the window icon from a file. Supports both raster images and SVG.
    /// </summary>
    public unsafe void SetIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath) || !System.IO.File.Exists(iconPath))
        {
            DiagnosticLog.Warn("X11Window", "Icon file not found: " + iconPath);
            return;
        }
        DiagnosticLog.Debug("X11Window", "SetIcon called: " + iconPath);
        try
        {
            SKBitmap? bitmap = null;

            // Handle SVG icons
            if (iconPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                DiagnosticLog.Debug("X11Window", "Loading SVG icon");
                using var svg = new SKSvg();
                svg.Load(iconPath);
                if (svg.Picture != null)
                {
                    var cullRect = svg.Picture.CullRect;
                    float scale = 48f / Math.Max(cullRect.Width, cullRect.Height);
                    int scaledWidth = (int)(cullRect.Width * scale);
                    int scaledHeight = (int)(cullRect.Height * scale);
                    bitmap = new SKBitmap(scaledWidth, scaledHeight, false);
                    using var canvas = new SKCanvas(bitmap);
                    canvas.Clear(SKColors.Transparent);
                    canvas.Scale(scale);
                    canvas.DrawPicture(svg.Picture);
                }
            }
            else
            {
                DiagnosticLog.Debug("X11Window", "Loading raster icon");
                bitmap = SKBitmap.Decode(iconPath);
            }

            if (bitmap == null)
            {
                DiagnosticLog.Warn("X11Window", "Failed to load icon: " + iconPath);
                return;
            }
            DiagnosticLog.Debug("X11Window", $"Loaded bitmap: {bitmap.Width}x{bitmap.Height}");

            // Scale to 48x48 for window manager title bar icon
            int targetSize = 48;
            if (bitmap.Width != targetSize || bitmap.Height != targetSize)
            {
                var scaled = new SKBitmap(targetSize, targetSize);
                // SKFilterQuality.High maps to cubic (Mitchell) resampling in SkiaSharp 4.
                bitmap.ScalePixels(scaled, new SKSamplingOptions(SKCubicResampler.Mitchell));
                bitmap.Dispose();
                bitmap = scaled;
            }

            int width = bitmap.Width;
            int height = bitmap.Height;
            int dataSize = 2 + width * height;
            uint[] iconData = new uint[dataSize];
            iconData[0] = (uint)width;
            iconData[1] = (uint)height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    iconData[2 + y * width + x] = (uint)((pixel.Alpha << 24) | (pixel.Red << 16) | (pixel.Green << 8) | pixel.Blue);
                }
            }
            bitmap.Dispose();

            IntPtr property = X11.XInternAtom(_display, "_NET_WM_ICON", false);
            IntPtr type = X11.XInternAtom(_display, "CARDINAL", false);
            fixed (uint* data = iconData)
            {
                X11.XChangeProperty(_display, _window, property, type, 32, 0, (nint)data, dataSize);
            }
            X11.XFlush(_display);
            DiagnosticLog.Debug("X11Window", $"Set window icon: {width}x{height}");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("X11Window", "Failed to set icon", ex);
        }
    }

    /// <summary>
    /// Shows the window.
    /// </summary>
    public void Show()
    {
        X11.XMapWindow(_display, _window);
        CompleteStartupNotification();
        X11.XFlush(_display);
        _isRunning = true;
    }

    /// <summary>
    /// Hides the window.
    /// </summary>
    public void Hide()
    {
        X11.XUnmapWindow(_display, _window);
        X11.XFlush(_display);
    }

    /// <summary>
    /// Sets the window title.
    /// </summary>
    public void SetTitle(string title)
    {
        _title = title ?? string.Empty;
        X11.XStoreName(_display, _window, title);
        if (_decorations.State.Active)
            Exposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The window title (drawn in OpenMaui's own titlebar while it draws the decorations).</summary>
    public string Title => _title;

    /// <summary>Whether the window manager has the window maximized (both axes in _NET_WM_STATE).</summary>
    public bool IsMaximized => _isMaximized;

    /// <summary>Whether OpenMaui draws the window's decorations (the window has a TitleBar).</summary>
    public bool UseCsd => _decorations.State.Active;

    ClientDecorations IClientDecoratedWindow.Decorations => _decorations.State;

    /// <inheritdoc cref="IClientDecoratedWindow.SetClientDecorationsWanted"/>
    public void SetClientDecorationsWanted(bool wanted)
    {
        if (_display == IntPtr.Zero || _window == IntPtr.Zero)
            return;
        if (!_decorations.SetWanted(wanted))
            return;
        DiagnosticLog.Debug("X11Window", $"Client-side decorations {(wanted ? "on (window manager frame off)" : "off (window manager frame on)")}");
        if (!wanted)
            SetHoverEdge(CsdResizeEdge.None);
        // The titlebar's inset changes the page's area.
        Exposed?.Invoke(this, EventArgs.Empty);
    }

    private float LogicalWidth => _scale > 0f ? _width / _scale : _width;
    private float LogicalHeight => _scale > 0f ? _height / _scale : _height;

    /// <summary>
    /// Resizes the window.
    /// </summary>
    public void Resize(int width, int height)
    {
        X11.XResizeWindow(_display, _window, (uint)width, (uint)height);
        X11.XFlush(_display);
    }

    /// <inheritdoc />
    public void RequestLogicalSize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        Resize((int)Math.Round(width * _scale), (int)Math.Round(height * _scale));
    }

    /// <inheritdoc />
    public void RequestLogicalPosition(int x, int y)
        => SetPosition((int)Math.Round(x * _scale), (int)Math.Round(y * _scale));

    /// <inheritdoc />
    public void SetLogicalSizeLimits(int minWidth, int minHeight, int maxWidth, int maxHeight)
    {
        var hints = new X11.XSizeHints();
        if (minWidth > 0 || minHeight > 0)
        {
            hints.Flags |= (nint)X11.PMinSize;
            hints.MinWidth = (int)Math.Round(Math.Max(1, minWidth) * _scale);
            hints.MinHeight = (int)Math.Round(Math.Max(1, minHeight) * _scale);
        }
        if (maxWidth > 0 || maxHeight > 0)
        {
            hints.Flags |= (nint)X11.PMaxSize;
            hints.MaxWidth = maxWidth > 0 ? (int)Math.Round(maxWidth * _scale) : 32767;
            hints.MaxHeight = maxHeight > 0 ? (int)Math.Round(maxHeight * _scale) : 32767;
        }
        X11.XSetWMNormalHints(_display, _window, ref hints);
        X11.XFlush(_display);
    }

    /// <summary>
    /// Moves the window to the specified position.
    /// </summary>
    public void SetPosition(int x, int y)
    {
        X11.XMoveWindow(_display, _window, x, y);
        X11.XFlush(_display);
    }

    /// <summary>
    /// Maximizes the window.
    /// </summary>
    public void Maximize()
    {
        SendWindowStateEvent(true, "_NET_WM_STATE_MAXIMIZED_VERT", "_NET_WM_STATE_MAXIMIZED_HORZ");
    }

    /// <summary>
    /// Minimizes (iconifies) the window.
    /// </summary>
    public void Minimize()
    {
        X11.XIconifyWindow(_display, _window, _screen);
        X11.XFlush(_display);
    }

    /// <summary>
    /// Restores the window from maximized or minimized state.
    /// </summary>
    public void Restore()
    {
        // Remove maximized state
        SendWindowStateEvent(false, "_NET_WM_STATE_MAXIMIZED_VERT", "_NET_WM_STATE_MAXIMIZED_HORZ");
        // Map window if it was minimized
        X11.XMapWindow(_display, _window);
        X11.XFlush(_display);
    }

    /// <summary>
    /// Sets fullscreen mode.
    /// </summary>
    public void SetFullscreen(bool fullscreen)
    {
        SendWindowStateEvent(fullscreen, "_NET_WM_STATE_FULLSCREEN");
    }

    /// <summary>
    /// Asks the window manager to raise and focus the window (EWMH _NET_ACTIVE_WINDOW,
    /// source "application"), mapping it first in case it was minimized. The window
    /// manager may refuse under its focus-stealing prevention, as Windows may refuse
    /// SetForegroundWindow.
    /// </summary>
    internal void RequestActivation()
    {
        X11.XMapWindow(_display, _window);
        var xev = new XEvent();
        xev.ClientMessageEvent.Type = X11.ClientMessage;
        xev.ClientMessageEvent.Window = _window;
        xev.ClientMessageEvent.MessageType = X11.XInternAtom(_display, "_NET_ACTIVE_WINDOW", false);
        xev.ClientMessageEvent.Format = 32;
        xev.ClientMessageEvent.Data.L0 = 1; // source indication: application
        xev.ClientMessageEvent.Data.L1 = 0; // CurrentTime
        xev.ClientMessageEvent.Data.L2 = 0;
        X11.XSendEvent(_display, X11.XRootWindow(_display, _screen), false,
            X11.SubstructureRedirectMask | X11.SubstructureNotifyMask, ref xev);
        X11.XFlush(_display);
    }

    private void SendWindowStateEvent(bool add, params string[] stateNames)
    {
        var wmState = X11.XInternAtom(_display, "_NET_WM_STATE", false);
        var rootWindow = X11.XRootWindow(_display, _screen);

        foreach (var stateName in stateNames)
        {
            var stateAtom = X11.XInternAtom(_display, stateName, false);

            var xev = new XEvent();
            xev.ClientMessageEvent.Type = X11.ClientMessage;
            xev.ClientMessageEvent.Window = _window;
            xev.ClientMessageEvent.MessageType = wmState;
            xev.ClientMessageEvent.Format = 32;

            // data.l[0] = action (0=remove, 1=add, 2=toggle)
            // data.l[1] = first property
            // data.l[2] = second property (optional)
            // data.l[3] = source indication (1 = normal application)
            xev.ClientMessageEvent.Data.L0 = add ? 1 : 0;
            xev.ClientMessageEvent.Data.L1 = (long)stateAtom;
            xev.ClientMessageEvent.Data.L2 = 0;
            xev.ClientMessageEvent.Data.L3 = 1;

            X11.XSendEvent(_display, rootWindow, false,
                X11.SubstructureRedirectMask | X11.SubstructureNotifyMask,
                ref xev);
        }

        X11.XFlush(_display);
    }

    /// <summary>
    /// Processes pending X11 events.
    /// </summary>
    public void ProcessEvents()
    {
        int pending = X11.XPending(_display);
        if (pending > 0)
        {
            if (_eventCounter % 100 == 0)
            {
                DiagnosticLog.Debug("X11Window", $"ProcessEvents: {pending} pending events");
            }
            _eventCounter++;
            while (X11.XPending(_display) > 0)
            {
                X11.XNextEvent(_display, out var xEvent);
                HandleEvent(ref xEvent);
            }
        }

        // Bound pending XDND drop transfers without blocking the loop — the
        // pump runs every frame, so a dead drag source is aborted (and an
        // honest XdndFinished(false) sent) within the service's timeout.
        DragDropService.Default.CheckPendingDropTimeout();
    }

    /// <summary>
    /// Runs the event loop.
    /// </summary>
    public void Run()
    {
        _isRunning = true;
        while (_isRunning)
        {
            X11.XNextEvent(_display, out var xEvent);
            HandleEvent(ref xEvent);
        }
    }

    /// <summary>
    /// Stops the event loop.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
    }

    private void HandleEvent(ref XEvent xEvent)
    {
        switch (xEvent.Type)
        {
            case XEventType.KeyPress:
                NoteUserTime(xEvent.KeyEvent.Time);
                HandleKeyPress(ref xEvent.KeyEvent);
                break;

            case XEventType.KeyRelease:
                HandleKeyRelease(ref xEvent.KeyEvent);
                break;

            case XEventType.ButtonPress:
                // XDND source ops (selection ownership, grabs, XdndDrop) need
                // a real event timestamp — record it from every input event.
                DragDropService.Default.NoteInputTime(xEvent.ButtonEvent.Time);
                NoteUserTime(xEvent.ButtonEvent.Time);
                if (IsTouchEmulatedButton(xEvent.ButtonEvent.Button))
                    break;
                HandleButtonPress(ref xEvent.ButtonEvent);
                break;

            case XEventType.ButtonRelease:
                // Consumed while an outgoing XDND drag is in progress (drop/cancel).
                if (DragDropService.Default.ProcessSourceButtonRelease(xEvent.ButtonEvent.Time))
                    break;
                if (IsTouchEmulatedButton(xEvent.ButtonEvent.Button))
                    break;
                HandleButtonRelease(ref xEvent.ButtonEvent);
                break;

            case XEventType.MotionNotify:
                // Consumed while an outgoing XDND drag tracks the pointer.
                if (DragDropService.Default.ProcessSourceMotion(
                        xEvent.MotionEvent.XRoot, xEvent.MotionEvent.YRoot, xEvent.MotionEvent.Time))
                    break;
                if (_touch.IsTouchDown)
                    break;
                HandleMotion(ref xEvent.MotionEvent);
                break;

            case XEventType.Expose:
                if (xEvent.ExposeEvent.Count == 0)
                {
                    Exposed?.Invoke(this, EventArgs.Empty);
                }
                break;

            case XEventType.ConfigureNotify:
                HandleConfigure(ref xEvent.ConfigureEvent);
                break;

            case XEventType.FocusIn:
                FocusGained?.Invoke(this, EventArgs.Empty);
                break;

            case XEventType.FocusOut:
                FocusLost?.Invoke(this, EventArgs.Empty);
                break;

            case XEventType.ClientMessage:
                if (xEvent.ClientMessageEvent.Data.L0 == (long)_wmDeleteMessage)
                {
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    _isRunning = false;
                }
                else if (xEvent.ClientMessageEvent.Data.L0 == (long)_wmSyncRequest)
                {
                    // Compositor requests sync — store the counter value to set after render
                    _syncCounterValue = (long)(uint)xEvent.ClientMessageEvent.Data.L2
                        | ((long)xEvent.ClientMessageEvent.Data.L3 << 32);
                    _syncPending = true;
                }
                else
                {
                    // XDND (XdndEnter/Position/Leave/Drop) and other protocol
                    // client messages route to the drag-and-drop service; it
                    // ignores message types it doesn't own.
                    DragDropService.Default.ProcessClientMessage(
                        _window,
                        xEvent.ClientMessageEvent.MessageType,
                        new nint[]
                        {
                            (nint)xEvent.ClientMessageEvent.Data.L0,
                            (nint)xEvent.ClientMessageEvent.Data.L1,
                            (nint)xEvent.ClientMessageEvent.Data.L2,
                            (nint)xEvent.ClientMessageEvent.Data.L3,
                            (nint)xEvent.ClientMessageEvent.Data.L4,
                        });
                }
                break;

            case XEventType.SelectionNotify:
                // Reply to the XConvertSelection issued for an XDND drop.
                DragDropService.Default.ProcessSelectionNotify(
                    xEvent.SelectionEvent.Selection,
                    xEvent.SelectionEvent.Target,
                    xEvent.SelectionEvent.Property);
                break;

            case XEventType.UnmapNotify:
                // ICCCM: iconifying (minimizing) unmaps the toplevel.
                _isMapped = false;
                SetSuspended(true);
                break;

            case XEventType.MapNotify:
                _isMapped = true;
                FocusIfRequestedOnMap();
                SetSuspended(false);
                break;

            case XInput2.GenericEvent:
                HandleGenericEvent(ref xEvent);
                break;

            case XEventType.PropertyNotify:
                if (xEvent.PropertyEvent.Window == _window && xEvent.PropertyEvent.Atom == _netWmStateAtom)
                {
                    RefreshMaximized();
                    break;
                }
                if (xEvent.PropertyEvent.Window == _rootWindow)
                {
                    if (xEvent.PropertyEvent.Atom == _resourceManagerAtom)
                        RefreshScale();
                    break;
                }
                // INCR chunk flow: incoming chunks arrive as new values on our
                // window; outgoing chunk requests arrive as property deletes on
                // the requestor's window. The service disambiguates by window.
                DragDropService.Default.ProcessPropertyNotify(
                    xEvent.PropertyEvent.Window,
                    xEvent.PropertyEvent.Atom,
                    xEvent.PropertyEvent.State);
                break;

            case XEventType.SelectionRequest:
                // A drop target pulling the payload of our outgoing XDND drag.
                DragDropService.Default.ProcessSelectionRequest(
                    xEvent.SelectionRequestEvent.Requestor,
                    xEvent.SelectionRequestEvent.Selection,
                    xEvent.SelectionRequestEvent.Target,
                    xEvent.SelectionRequestEvent.Property,
                    xEvent.SelectionRequestEvent.Time);
                break;

            case XEventType.SelectionClear:
                // We lost the XDND selection — cancels an outgoing drag.
                DragDropService.Default.ProcessSelectionClear(
                    xEvent.SelectionClearEvent.Selection);
                break;
        }
    }

    private void HandleKeyPress(ref XKeyEvent keyEvent)
    {
        DragDropService.Default.NoteInputTime(keyEvent.Time);

        var keysym = KeyMapping.GetKeysym(_display, keyEvent.Keycode, (keyEvent.State & 0x01) != 0);

        // Escape cancels an outgoing XDND drag; the key is consumed.
        if (DragDropService.Default.ProcessSourceKey((ulong)keysym))
            return;

        var key = KeyMapping.FromKeysym(keysym);
        var modifiers = KeyMapping.GetModifiers(keyEvent.State);

        KeyDown?.Invoke(this, new KeyEventArgs(key, modifiers) { HardwareKeycode = KeyMapping.X11ToXkbKeycode(keyEvent.Keycode) });

        // Generate text input for printable characters, but NOT when Control or Alt is held
        // (those are keyboard shortcuts, not text input)
        bool isControlHeld = (keyEvent.State & 0x04) != 0; // ControlMask
        bool isAltHeld = (keyEvent.State & 0x08) != 0;     // Mod1Mask (Alt)

        if (keysym >= 32 && keysym <= 126 && !isControlHeld && !isAltHeld)
        {
            TextInput?.Invoke(this, new TextInputEventArgs(((char)keysym).ToString()));
        }
    }

    private void HandleKeyRelease(ref XKeyEvent keyEvent)
    {
        var keysym = KeyMapping.GetKeysym(_display, keyEvent.Keycode, (keyEvent.State & 0x01) != 0);
        var key = KeyMapping.FromKeysym(keysym);
        var modifiers = KeyMapping.GetModifiers(keyEvent.State);

        KeyUp?.Invoke(this, new KeyEventArgs(key, modifiers) { HardwareKeycode = KeyMapping.X11ToXkbKeycode(keyEvent.Keycode) });
    }

    private void HandleButtonPress(ref XButtonEvent buttonEvent)
    {
        // Buttons 4 and 5 are scroll wheel
        if (buttonEvent.Button == 4)
        {
            Scroll?.Invoke(this, new ScrollEventArgs(buttonEvent.X, buttonEvent.Y, 0, -1, KeyMapping.GetModifiers(buttonEvent.State)));
            return;
        }
        if (buttonEvent.Button == 5)
        {
            Scroll?.Invoke(this, new ScrollEventArgs(buttonEvent.X, buttonEvent.Y, 0, 1, KeyMapping.GetModifiers(buttonEvent.State)));
            return;
        }

        var button = MapButton(buttonEvent.Button);

        // A new press: the release of an earlier one the window manager took over (a move or
        // resize ends inside its grab, so that release may never reach us) is not pending.
        _csdSwallowedReleases &= ~button;

        // Our own decorations take presses on the titlebar, the window buttons and the edges.
        if (_decorations.State.Active && button is PointerButton.Left or PointerButton.Right)
        {
            float scale = _scale > 0f ? _scale : 1f;
            if (_decorations.HandlePress(button, buttonEvent.X / scale, buttonEvent.Y / scale,
                    buttonEvent.XRoot, buttonEvent.YRoot, buttonEvent.Time,
                    LogicalWidth, LogicalHeight, _isMaximized))
            {
                _csdSwallowedReleases |= button;
                return;
            }
        }

        PointerPressed?.Invoke(this, new PointerEventArgs(buttonEvent.X, buttonEvent.Y, button));
    }

    private void HandleButtonRelease(ref XButtonEvent buttonEvent)
    {
        // Ignore scroll wheel releases
        if (buttonEvent.Button == 4 || buttonEvent.Button == 5)
            return;

        var button = MapButton(buttonEvent.Button);
        // The release of a press the decorations took (the views never saw the press).
        if ((_csdSwallowedReleases & button) != 0 && button != PointerButton.None)
        {
            _csdSwallowedReleases &= ~button;
            return;
        }
        PointerReleased?.Invoke(this, new PointerEventArgs(buttonEvent.X, buttonEvent.Y, button));
    }

    private void HandleMotion(ref XMotionEvent motionEvent)
    {
        if (_decorations.State.Active)
        {
            float scale = _scale > 0f ? _scale : 1f;
            SetHoverEdge(ClientDecorations.EdgeAt(motionEvent.X / scale, motionEvent.Y / scale,
                LogicalWidth, LogicalHeight, _isMaximized));
        }
        PointerMoved?.Invoke(this, new PointerEventArgs(motionEvent.X, motionEvent.Y));
    }

    /// <summary>
    /// Shows the resize cursor while the pointer is over an edge our decorations resize, and the
    /// app's cursor again when it leaves.
    /// </summary>
    private void SetHoverEdge(CsdResizeEdge edge)
    {
        if (edge == _hoverEdge || _display == IntPtr.Zero)
            return;
        _hoverEdge = edge;
        X11.XDefineCursor(_display, _window, edge == CsdResizeEdge.None ? _currentCursor : EdgeCursor(edge));
        X11.XFlush(_display);
    }

    private IntPtr EdgeCursor(CsdResizeEdge edge)
    {
        if (_edgeCursors.TryGetValue(edge, out var cursor))
            return cursor;
        cursor = edge switch
        {
            CsdResizeEdge.Top or CsdResizeEdge.Bottom => _sizeNorthSouthCursor,
            CsdResizeEdge.Left or CsdResizeEdge.Right => _sizeWestEastCursor,
            CsdResizeEdge.TopLeft => LoadCursor("nw-resize", 134),     // XC_top_left_corner
            CsdResizeEdge.TopRight => LoadCursor("ne-resize", 136),    // XC_top_right_corner
            CsdResizeEdge.BottomLeft => LoadCursor("sw-resize", 12),   // XC_bottom_left_corner
            CsdResizeEdge.BottomRight => LoadCursor("se-resize", 14),  // XC_bottom_right_corner
            _ => _arrowCursor,
        };
        _edgeCursors[edge] = cursor;
        return cursor;
    }

    /// <summary>Re-reads _NET_WM_STATE: maximized when both maximized axes are listed.</summary>
    private void RefreshMaximized()
    {
        bool maximized = false;
        try
        {
            var atoms = ReadAtomList(_window, _netWmStateAtom);
            var vert = (long)X11.XInternAtom(_display, "_NET_WM_STATE_MAXIMIZED_VERT", false);
            var horz = (long)X11.XInternAtom(_display, "_NET_WM_STATE_MAXIMIZED_HORZ", false);
            maximized = atoms.Contains(vert) && atoms.Contains(horz);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("X11Window", $"Reading _NET_WM_STATE failed: {ex.Message}");
            return;
        }
        if (maximized == _isMaximized)
            return;
        _isMaximized = maximized;
        if (maximized)
            SetHoverEdge(CsdResizeEdge.None);
        // The maximize button's glyph follows the state.
        if (_decorations.State.Active)
            Exposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A format-32 ATOM-list property of a window (empty when absent).</summary>
    private List<long> ReadAtomList(IntPtr window, IntPtr property)
    {
        var result = new List<long>();
        const long XaAtom = 4;
        int status = X11.XGetWindowProperty(_display, window, property, 0, 4096, false, (IntPtr)XaAtom,
            out _, out int format, out IntPtr nitems, out _, out IntPtr data);
        if (status != 0 || data == IntPtr.Zero)
            return result;
        try
        {
            // Format-32 items are C longs in client memory.
            if (format == 32)
            {
                long count = (long)nitems;
                for (long i = 0; i < count; i++)
                    result.Add((long)System.Runtime.InteropServices.Marshal.ReadIntPtr(data, (int)(i * IntPtr.Size)));
            }
        }
        finally
        {
            X11.XFree(data);
        }
        return result;
    }

    #region IX11DecorationHost (Xlib side of the client-side decorations)

    long IX11DecorationHost.Atom(string name) => (long)X11.XInternAtom(_display, name, false);

    unsafe void IX11DecorationHost.SetProperty32(string property, string type, long[] values)
    {
        // Format-32 property data is an array of C longs.
        var data = new nint[values.Length];
        for (int i = 0; i < values.Length; i++)
            data[i] = (nint)values[i];
        var propertyAtom = X11.XInternAtom(_display, property, false);
        var typeAtom = X11.XInternAtom(_display, type, false);
        fixed (nint* ptr = data)
        {
            X11.XChangeProperty(_display, _window, propertyAtom, typeAtom, 32, 0 /* PropModeReplace */, (IntPtr)ptr, data.Length);
        }
    }

    void IX11DecorationHost.SendRootClientMessage(string messageType, long[] data)
    {
        var xev = new XEvent();
        xev.ClientMessageEvent.Type = X11.ClientMessage;
        xev.ClientMessageEvent.Window = _window;
        xev.ClientMessageEvent.MessageType = X11.XInternAtom(_display, messageType, false);
        xev.ClientMessageEvent.Format = 32;
        xev.ClientMessageEvent.Data.L0 = data.Length > 0 ? data[0] : 0;
        xev.ClientMessageEvent.Data.L1 = data.Length > 1 ? data[1] : 0;
        xev.ClientMessageEvent.Data.L2 = data.Length > 2 ? data[2] : 0;
        xev.ClientMessageEvent.Data.L3 = data.Length > 3 ? data[3] : 0;
        xev.ClientMessageEvent.Data.L4 = data.Length > 4 ? data[4] : 0;
        X11.XSendEvent(_display, _rootWindow, false,
            X11.SubstructureRedirectMask | X11.SubstructureNotifyMask, ref xev);
    }

    void IX11DecorationHost.UngrabPointer(ulong time) => X11.XUngrabPointer(_display, time);

    bool IX11DecorationHost.WmSupports(string atom)
    {
        if (_wmSupported.TryGetValue(atom, out var supported))
            return supported;
        try
        {
            var list = ReadAtomList(_rootWindow, X11.XInternAtom(_display, "_NET_SUPPORTED", false));
            supported = list.Contains((long)X11.XInternAtom(_display, atom, false));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("X11Window", $"Reading _NET_SUPPORTED failed: {ex.Message}");
            supported = false;
        }
        _wmSupported[atom] = supported;
        return supported;
    }

    void IX11DecorationHost.Iconify() => Minimize();

    void IX11DecorationHost.RequestClose()
    {
        // As WM_DELETE_WINDOW from the window manager's close button.
        CloseRequested?.Invoke(this, EventArgs.Empty);
        _isRunning = false;
    }

    void IX11DecorationHost.Flush() => X11.XFlush(_display);

    #endregion

    private bool _resizePending;
    private int _pendingWidth, _pendingHeight;

    private void HandleConfigure(ref XConfigureEvent configureEvent)
    {
        if (configureEvent.Width != _width || configureEvent.Height != _height)
        {
            _width = configureEvent.Width;
            _height = configureEvent.Height;
            // Defer resize notification — will be fired after all pending events
            // are drained, so rapid resize events are coalesced into one.
            _resizePending = true;
            _pendingWidth = _width;
            _pendingHeight = _height;
        }
    }

    /// <summary>
    /// Fires any deferred resize event. Call after ProcessEvents().
    /// </summary>
    public void FlushDeferredResize()
    {
        if (_resizePending)
        {
            _resizePending = false;
            Resized?.Invoke(this, (_pendingWidth, _pendingHeight));
        }
    }

    /// <summary>
    /// Acknowledges a pending _NET_WM_SYNC_REQUEST after rendering.
    /// This tells the compositor we've finished drawing at the new size,
    /// allowing it to send the next ConfigureNotify for live resize.
    /// </summary>
    public void AcknowledgeSync()
    {
        if (_syncPending && _syncCounter != 0)
        {
            _syncPending = false;
            X11.XSyncSetCounter(_display, _syncCounter, new X11.XSyncValue(_syncCounterValue));
        }
    }

    private static PointerButton MapButton(uint button) => button switch
    {
        1 => PointerButton.Left,
        2 => PointerButton.Middle,
        3 => PointerButton.Right,
        8 => PointerButton.XButton1,
        9 => PointerButton.XButton2,
        _ => PointerButton.None
    };

    /// <summary>
    /// Gets the X11 file descriptor for use with select/poll.
    /// </summary>
    public int GetFileDescriptor()
    {
        return X11.XConnectionNumber(_display);
    }

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            DragDropService.Default.UnregisterWindow(_window);

            // Free cursor resources before closing the display
            if (_display != IntPtr.Zero)
            {
                if (_arrowCursor != IntPtr.Zero) X11.XFreeCursor(_display, _arrowCursor);
                if (_handCursor != IntPtr.Zero) X11.XFreeCursor(_display, _handCursor);
                if (_textCursor != IntPtr.Zero) X11.XFreeCursor(_display, _textCursor);
                if (_sizeWestEastCursor != IntPtr.Zero) X11.XFreeCursor(_display, _sizeWestEastCursor);
                if (_sizeNorthSouthCursor != IntPtr.Zero) X11.XFreeCursor(_display, _sizeNorthSouthCursor);
                if (_sizeAllCursor != IntPtr.Zero) X11.XFreeCursor(_display, _sizeAllCursor);
                foreach (var (edge, edgeCursor) in _edgeCursors)
                {
                    // Top/bottom/left/right reuse the size cursors freed above.
                    if (edge is CsdResizeEdge.TopLeft or CsdResizeEdge.TopRight or CsdResizeEdge.BottomLeft or CsdResizeEdge.BottomRight
                        && edgeCursor != IntPtr.Zero)
                        X11.XFreeCursor(_display, edgeCursor);
                }
                _edgeCursors.Clear();
                _arrowCursor = IntPtr.Zero;
                _handCursor = IntPtr.Zero;
                _textCursor = IntPtr.Zero;
                _sizeWestEastCursor = IntPtr.Zero;
                _sizeNorthSouthCursor = IntPtr.Zero;
                _sizeAllCursor = IntPtr.Zero;
            }

            if (_syncCounter != 0 && _display != IntPtr.Zero)
            {
                X11.XSyncDestroyCounter(_display, _syncCounter);
                _syncCounter = 0;
            }

            if (_window != IntPtr.Zero)
            {
                X11.XDestroyWindow(_display, _window);
                _window = IntPtr.Zero;
            }

            if (_display != IntPtr.Zero)
            {
                X11.XCloseDisplay(_display);
                _display = IntPtr.Zero;
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Presents an ARGB32 pixel buffer to the window via XPutImage.
    /// </summary>
    public void Present(IntPtr pixels, int width, int height, int stride)
    {
        if (_display == IntPtr.Zero || _window == IntPtr.Zero) return;

        var gc = X11.XDefaultGC(_display, _screen);
        var visual = X11.XDefaultVisual(_display, _screen);
        var depth = X11.XDefaultDepth(_display, _screen);

        // Allocate unmanaged memory and copy the pixel data
        var dataSize = height * stride;
        var unmanagedData = System.Runtime.InteropServices.Marshal.AllocHGlobal(dataSize);
        
        try
        {
            // Copy pixel data to unmanaged memory
            unsafe
            {
                Buffer.MemoryCopy((void*)pixels, (void*)unmanagedData, dataSize, dataSize);
            }

            // Create XImage from the unmanaged pixel data
            var image = X11.XCreateImage(
                _display,
                visual,
                (uint)depth,
                X11.ZPixmap,
                0,
                unmanagedData,
                (uint)width,
                (uint)height,
                32,
                stride);

            if (image != IntPtr.Zero)
            {
                X11.XPutImage(_display, _window, gc, image, 0, 0, 0, 0, (uint)width, (uint)height);
                X11.XDestroyImage(image); // This will free unmanagedData
            }
            else
            {
                // If XCreateImage failed, free the memory ourselves
                System.Runtime.InteropServices.Marshal.FreeHGlobal(unmanagedData);
            }
        }
        catch
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(unmanagedData);
            throw;
        }

        X11.XFlush(_display);
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~X11Window()
    {
        Dispose(false);
    }

    #endregion
}
