// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Interop;
using Microsoft.Maui.Platform.Linux.Input;

namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>
/// X11 window implementation for Linux.
/// </summary>
public class X11Window : IDisposable
{
    private IntPtr _display;
    private IntPtr _window;
    private IntPtr _wmDeleteMessage;
    private int _screen;
    private bool _disposed;
    private bool _isRunning;

    private int _width;
    private int _height;

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
            0xFFFFFF // White background
        );

        if (_window == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create X11 window");

        // Set window title
        X11.XStoreName(_display, _window, title);

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

        // Set up WM_DELETE_WINDOW protocol for proper close handling
        _wmDeleteMessage = X11.XInternAtom(_display, "WM_DELETE_WINDOW", false);

        // Would need XSetWMProtocols here, simplified for now
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
    /// Processes pending X11 events.
    /// </summary>
    public void ProcessEvents()
    {
        while (X11.XPending(_display) > 0)
        {
            X11.XNextEvent(_display, out var xEvent);
            HandleEvent(ref xEvent);
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
                break;
        }
    }

    private void HandleKeyPress(ref XKeyEvent keyEvent)
    {
        var keysym = KeyMapping.GetKeysym(_display, keyEvent.Keycode, (keyEvent.State & 0x01) != 0);
        var key = KeyMapping.FromKeysym(keysym);
        var modifiers = KeyMapping.GetModifiers(keyEvent.State);

        KeyDown?.Invoke(this, new KeyEventArgs(key, modifiers));

        // Generate text input for printable characters
        if (keysym >= 32 && keysym <= 126)
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

    private void HandleConfigure(ref XConfigureEvent configureEvent)
    {
        if (configureEvent.Width != _width || configureEvent.Height != _height)
        {
            _width = configureEvent.Width;
            _height = configureEvent.Height;
            Resized?.Invoke(this, (_width, _height));
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
