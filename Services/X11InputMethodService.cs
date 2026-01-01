// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// X11 Input Method service using XIM protocol.
/// Provides IME support for CJK and other complex input methods.
/// </summary>
public class X11InputMethodService : IInputMethodService, IDisposable
{
    private nint _display;
    private nint _window;
    private nint _xim;
    private nint _xic;
    private IInputContext? _currentContext;
    private string _preEditText = string.Empty;
    private int _preEditCursorPosition;
    private bool _isActive;
    private bool _disposed;

    // XIM callback delegates (prevent GC)
    private XIMProc? _preeditStartCallback;
    private XIMProc? _preeditDoneCallback;
    private XIMProc? _preeditDrawCallback;
    private XIMProc? _preeditCaretCallback;
    private XIMProc? _commitCallback;

    public bool IsActive => _isActive;
    public string PreEditText => _preEditText;
    public int PreEditCursorPosition => _preEditCursorPosition;

    public event EventHandler<TextCommittedEventArgs>? TextCommitted;
    public event EventHandler<PreEditChangedEventArgs>? PreEditChanged;
    public event EventHandler? PreEditEnded;

    public void Initialize(nint windowHandle)
    {
        _window = windowHandle;

        // Get display from X11 interop
        _display = XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero)
        {
            Console.WriteLine("X11InputMethodService: Failed to open display");
            return;
        }

        // Set locale for proper IME operation
        if (XSetLocaleModifiers("") == IntPtr.Zero)
        {
            XSetLocaleModifiers("@im=none");
        }

        // Open input method
        _xim = XOpenIM(_display, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (_xim == IntPtr.Zero)
        {
            Console.WriteLine("X11InputMethodService: No input method available, trying IBus...");
            TryIBusFallback();
            return;
        }

        CreateInputContext();
    }

    private void CreateInputContext()
    {
        if (_xim == IntPtr.Zero || _window == IntPtr.Zero) return;

        // Create input context with preedit callbacks
        var preeditAttr = CreatePreeditAttributes();

        _xic = XCreateIC(_xim,
            XNClientWindow, _window,
            XNFocusWindow, _window,
            XNInputStyle, XIMPreeditCallbacks | XIMStatusNothing,
            XNPreeditAttributes, preeditAttr,
            IntPtr.Zero);

        if (preeditAttr != IntPtr.Zero)
        {
            XFree(preeditAttr);
        }

        if (_xic == IntPtr.Zero)
        {
            // Fallback to simpler input style
            _xic = XCreateICSimple(_xim,
                XNClientWindow, _window,
                XNFocusWindow, _window,
                XNInputStyle, XIMPreeditNothing | XIMStatusNothing,
                IntPtr.Zero);
        }

        if (_xic != IntPtr.Zero)
        {
            Console.WriteLine("X11InputMethodService: Input context created successfully");
        }
    }

    private nint CreatePreeditAttributes()
    {
        // Set up preedit callbacks for on-the-spot composition
        _preeditStartCallback = PreeditStartCallback;
        _preeditDoneCallback = PreeditDoneCallback;
        _preeditDrawCallback = PreeditDrawCallback;
        _preeditCaretCallback = PreeditCaretCallback;

        // Create callback structures
        // Note: Actual implementation would marshal XIMCallback structures
        return IntPtr.Zero;
    }

    private int PreeditStartCallback(nint xic, nint clientData, nint callData)
    {
        _isActive = true;
        _preEditText = string.Empty;
        _preEditCursorPosition = 0;
        return -1; // No length limit
    }

    private int PreeditDoneCallback(nint xic, nint clientData, nint callData)
    {
        _isActive = false;
        _preEditText = string.Empty;
        _preEditCursorPosition = 0;
        PreEditEnded?.Invoke(this, EventArgs.Empty);
        _currentContext?.OnPreEditEnded();
        return 0;
    }

    private int PreeditDrawCallback(nint xic, nint clientData, nint callData)
    {
        // Parse XIMPreeditDrawCallbackStruct
        // Update preedit text and cursor position
        // This would involve marshaling the callback data structure

        PreEditChanged?.Invoke(this, new PreEditChangedEventArgs(_preEditText, _preEditCursorPosition));
        _currentContext?.OnPreEditChanged(_preEditText, _preEditCursorPosition);
        return 0;
    }

    private int PreeditCaretCallback(nint xic, nint clientData, nint callData)
    {
        // Handle caret movement in preedit text
        return 0;
    }

    private void TryIBusFallback()
    {
        // Try to connect to IBus via D-Bus
        // This provides a more modern IME interface
        Console.WriteLine("X11InputMethodService: IBus fallback not yet implemented");
    }

    public void SetFocus(IInputContext? context)
    {
        _currentContext = context;

        if (_xic != IntPtr.Zero)
        {
            if (context != null)
            {
                XSetICFocus(_xic);
            }
            else
            {
                XUnsetICFocus(_xic);
            }
        }
    }

    public void SetCursorLocation(int x, int y, int width, int height)
    {
        if (_xic == IntPtr.Zero) return;

        // Set the spot location for candidate window positioning
        var spotLocation = new XPoint { x = (short)x, y = (short)y };

        var attr = XVaCreateNestedList(0,
            XNSpotLocation, ref spotLocation,
            IntPtr.Zero);

        if (attr != IntPtr.Zero)
        {
            XSetICValues(_xic, XNPreeditAttributes, attr, IntPtr.Zero);
            XFree(attr);
        }
    }

    public bool ProcessKeyEvent(uint keyCode, KeyModifiers modifiers, bool isKeyDown)
    {
        if (_xic == IntPtr.Zero) return false;

        // Convert to X11 key event
        var xEvent = new XKeyEvent
        {
            type = isKeyDown ? KeyPress : KeyRelease,
            display = _display,
            window = _window,
            state = ConvertModifiers(modifiers),
            keycode = keyCode
        };

        // Filter through XIM
        if (XFilterEvent(ref xEvent, _window))
        {
            return true; // Event consumed by IME
        }

        // If not filtered and key down, try to get committed text
        if (isKeyDown)
        {
            var buffer = new byte[64];
            var keySym = IntPtr.Zero;
            var status = IntPtr.Zero;

            int len = Xutf8LookupString(_xic, ref xEvent, buffer, buffer.Length, ref keySym, ref status);

            if (len > 0)
            {
                string text = Encoding.UTF8.GetString(buffer, 0, len);
                OnTextCommit(text);
                return true;
            }
        }

        return false;
    }

    private void OnTextCommit(string text)
    {
        _preEditText = string.Empty;
        _preEditCursorPosition = 0;

        TextCommitted?.Invoke(this, new TextCommittedEventArgs(text));
        _currentContext?.OnTextCommitted(text);
    }

    private uint ConvertModifiers(KeyModifiers modifiers)
    {
        uint state = 0;
        if (modifiers.HasFlag(KeyModifiers.Shift)) state |= ShiftMask;
        if (modifiers.HasFlag(KeyModifiers.Control)) state |= ControlMask;
        if (modifiers.HasFlag(KeyModifiers.Alt)) state |= Mod1Mask;
        if (modifiers.HasFlag(KeyModifiers.Super)) state |= Mod4Mask;
        if (modifiers.HasFlag(KeyModifiers.CapsLock)) state |= LockMask;
        if (modifiers.HasFlag(KeyModifiers.NumLock)) state |= Mod2Mask;
        return state;
    }

    public void Reset()
    {
        if (_xic != IntPtr.Zero)
        {
            XmbResetIC(_xic);
        }

        _preEditText = string.Empty;
        _preEditCursorPosition = 0;
        _isActive = false;

        PreEditEnded?.Invoke(this, EventArgs.Empty);
        _currentContext?.OnPreEditEnded();
    }

    public void Shutdown()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_xic != IntPtr.Zero)
        {
            XDestroyIC(_xic);
            _xic = IntPtr.Zero;
        }

        if (_xim != IntPtr.Zero)
        {
            XCloseIM(_xim);
            _xim = IntPtr.Zero;
        }

        // Note: Don't close display here if shared with window
    }

    #region X11 Interop

    private const int KeyPress = 2;
    private const int KeyRelease = 3;

    private const uint ShiftMask = 1 << 0;
    private const uint LockMask = 1 << 1;
    private const uint ControlMask = 1 << 2;
    private const uint Mod1Mask = 1 << 3;  // Alt
    private const uint Mod2Mask = 1 << 4;  // NumLock
    private const uint Mod4Mask = 1 << 6;  // Super

    private const long XIMPreeditNothing = 0x0008L;
    private const long XIMPreeditCallbacks = 0x0002L;
    private const long XIMStatusNothing = 0x0400L;

    private static readonly nint XNClientWindow = Marshal.StringToHGlobalAnsi("clientWindow");
    private static readonly nint XNFocusWindow = Marshal.StringToHGlobalAnsi("focusWindow");
    private static readonly nint XNInputStyle = Marshal.StringToHGlobalAnsi("inputStyle");
    private static readonly nint XNPreeditAttributes = Marshal.StringToHGlobalAnsi("preeditAttributes");
    private static readonly nint XNSpotLocation = Marshal.StringToHGlobalAnsi("spotLocation");

    private delegate int XIMProc(nint xic, nint clientData, nint callData);

    [StructLayout(LayoutKind.Sequential)]
    private struct XPoint
    {
        public short x;
        public short y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XKeyEvent
    {
        public int type;
        public ulong serial;
        public bool send_event;
        public nint display;
        public nint window;
        public nint root;
        public nint subwindow;
        public ulong time;
        public int x, y;
        public int x_root, y_root;
        public uint state;
        public uint keycode;
        public bool same_screen;
    }

    [DllImport("libX11.so.6")]
    private static extern nint XOpenDisplay(nint display);

    [DllImport("libX11.so.6")]
    private static extern nint XSetLocaleModifiers(string modifiers);

    [DllImport("libX11.so.6")]
    private static extern nint XOpenIM(nint display, nint db, nint res_name, nint res_class);

    [DllImport("libX11.so.6")]
    private static extern void XCloseIM(nint xim);

    [DllImport("libX11.so.6", EntryPoint = "XCreateIC")]
    private static extern nint XCreateIC(nint xim, nint name1, nint value1, nint name2, nint value2,
        nint name3, long value3, nint name4, nint value4, nint terminator);

    [DllImport("libX11.so.6", EntryPoint = "XCreateIC")]
    private static extern nint XCreateICSimple(nint xim, nint name1, nint value1, nint name2, nint value2,
        nint name3, long value3, nint terminator);

    [DllImport("libX11.so.6")]
    private static extern void XDestroyIC(nint xic);

    [DllImport("libX11.so.6")]
    private static extern void XSetICFocus(nint xic);

    [DllImport("libX11.so.6")]
    private static extern void XUnsetICFocus(nint xic);

    [DllImport("libX11.so.6")]
    private static extern nint XSetICValues(nint xic, nint name, nint value, nint terminator);

    [DllImport("libX11.so.6")]
    private static extern nint XVaCreateNestedList(int unused, nint name, ref XPoint value, nint terminator);

    [DllImport("libX11.so.6")]
    private static extern bool XFilterEvent(ref XKeyEvent xevent, nint window);

    [DllImport("libX11.so.6")]
    private static extern int Xutf8LookupString(nint xic, ref XKeyEvent xevent,
        byte[] buffer, int bytes, ref nint keySym, ref nint status);

    [DllImport("libX11.so.6")]
    private static extern nint XmbResetIC(nint xic);

    [DllImport("libX11.so.6")]
    private static extern void XFree(nint ptr);

    #endregion
}
