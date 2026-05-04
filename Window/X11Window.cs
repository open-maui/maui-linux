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
public class X11Window : IDisposable
{
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
    private IntPtr _currentCursor;
    private CursorType _currentCursorType = CursorType.Arrow;

    private static int _eventCounter;

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

        // Select input events
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
            XEventMask.FocusChangeMask);

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

        // Initialize cursors
        _arrowCursor = X11.XCreateFontCursor(_display, 68); // XC_left_ptr
        _handCursor = X11.XCreateFontCursor(_display, 60);  // XC_hand2
        _textCursor = X11.XCreateFontCursor(_display, 152); // XC_xterm
        _currentCursor = _arrowCursor;
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
                _ => _arrowCursor,
            };
            if (cursor != _currentCursor)
            {
                _currentCursor = cursor;
                X11.XDefineCursor(_display, _window, _currentCursor);
                X11.XFlush(_display);
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
                bitmap.ScalePixels(scaled, SKFilterQuality.High);
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
        X11.XStoreName(_display, _window, title);
    }

    /// <summary>
    /// Resizes the window.
    /// </summary>
    public void Resize(int width, int height)
    {
        X11.XResizeWindow(_display, _window, (uint)width, (uint)height);
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
                HandleKeyPress(ref xEvent.KeyEvent);
                break;

            case XEventType.KeyRelease:
                HandleKeyRelease(ref xEvent.KeyEvent);
                break;

            case XEventType.ButtonPress:
                HandleButtonPress(ref xEvent.ButtonEvent);
                break;

            case XEventType.ButtonRelease:
                HandleButtonRelease(ref xEvent.ButtonEvent);
                break;

            case XEventType.MotionNotify:
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
                break;
        }
    }

    private void HandleKeyPress(ref XKeyEvent keyEvent)
    {
        var keysym = KeyMapping.GetKeysym(_display, keyEvent.Keycode, (keyEvent.State & 0x01) != 0);
        var key = KeyMapping.FromKeysym(keysym);
        var modifiers = KeyMapping.GetModifiers(keyEvent.State);

        KeyDown?.Invoke(this, new KeyEventArgs(key, modifiers));

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

        KeyUp?.Invoke(this, new KeyEventArgs(key, modifiers));
    }

    private void HandleButtonPress(ref XButtonEvent buttonEvent)
    {
        // Buttons 4 and 5 are scroll wheel
        if (buttonEvent.Button == 4)
        {
            Scroll?.Invoke(this, new ScrollEventArgs(buttonEvent.X, buttonEvent.Y, 0, -1));
            return;
        }
        if (buttonEvent.Button == 5)
        {
            Scroll?.Invoke(this, new ScrollEventArgs(buttonEvent.X, buttonEvent.Y, 0, 1));
            return;
        }

        var button = MapButton(buttonEvent.Button);
        PointerPressed?.Invoke(this, new PointerEventArgs(buttonEvent.X, buttonEvent.Y, button));
    }

    private void HandleButtonRelease(ref XButtonEvent buttonEvent)
    {
        // Ignore scroll wheel releases
        if (buttonEvent.Button == 4 || buttonEvent.Button == 5)
            return;

        var button = MapButton(buttonEvent.Button);
        PointerReleased?.Invoke(this, new PointerEventArgs(buttonEvent.X, buttonEvent.Y, button));
    }

    private void HandleMotion(ref XMotionEvent motionEvent)
    {
        PointerMoved?.Invoke(this, new PointerEventArgs(motionEvent.X, motionEvent.Y));
    }

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
            // Free cursor resources before closing the display
            if (_display != IntPtr.Zero)
            {
                if (_arrowCursor != IntPtr.Zero) X11.XFreeCursor(_display, _arrowCursor);
                if (_handCursor != IntPtr.Zero) X11.XFreeCursor(_display, _handCursor);
                if (_textCursor != IntPtr.Zero) X11.XFreeCursor(_display, _textCursor);
                _arrowCursor = IntPtr.Zero;
                _handCursor = IntPtr.Zero;
                _textCursor = IntPtr.Zero;
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
    /// Draws pixel data to the window.
    /// </summary>
    /// <summary>
    /// Draws pixel data to the window.
    /// </summary>
    public void DrawPixels(IntPtr pixels, int width, int height, int stride)
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
