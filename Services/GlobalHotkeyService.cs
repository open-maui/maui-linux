// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Provides global hotkey registration and handling using X11.
/// </summary>
public class GlobalHotkeyService : IDisposable
{
    private nint _display;
    private nint _rootWindow;
    private readonly ConcurrentDictionary<int, HotkeyRegistration> _registrations = new();
    private int _nextId = 1;
    private bool _disposed;
    private Thread? _eventThread;
    private bool _isListening;

    /// <summary>
    /// Event raised when a registered hotkey is pressed.
    /// </summary>
    public event EventHandler<HotkeyEventArgs>? HotkeyPressed;

    /// <summary>
    /// Initializes the global hotkey service.
    /// </summary>
    public void Initialize()
    {
        _display = XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to open X display");
        }

        _rootWindow = XDefaultRootWindow(_display);

        // Start listening for hotkeys in background
        _isListening = true;
        _eventThread = new Thread(ListenForHotkeys)
        {
            IsBackground = true,
            Name = "GlobalHotkeyListener"
        };
        _eventThread.Start();
    }

    /// <summary>
    /// Registers a global hotkey.
    /// </summary>
    /// <param name="key">The key code.</param>
    /// <param name="modifiers">The modifier keys.</param>
    /// <returns>A registration ID that can be used to unregister.</returns>
    public int Register(HotkeyKey key, HotkeyModifiers modifiers)
    {
        if (_display == IntPtr.Zero)
        {
            throw new InvalidOperationException("Service not initialized");
        }

        int keyCode = XKeysymToKeycode(_display, (nint)key);
        if (keyCode == 0)
        {
            throw new ArgumentException($"Invalid key: {key}");
        }

        uint modifierMask = GetModifierMask(modifiers);

        // Register for all modifier combinations (with/without NumLock, CapsLock)
        uint[] masks = GetModifierCombinations(modifierMask);

        foreach (var mask in masks)
        {
            int result = XGrabKey(_display, keyCode, mask, _rootWindow, true, GrabModeAsync, GrabModeAsync);
            if (result == 0)
            {
                Console.WriteLine($"Failed to grab key {key} with modifiers {modifiers}");
            }
        }

        int id = _nextId++;
        _registrations[id] = new HotkeyRegistration
        {
            Id = id,
            KeyCode = keyCode,
            Modifiers = modifierMask,
            Key = key,
            ModifierKeys = modifiers
        };

        XFlush(_display);
        return id;
    }

    /// <summary>
    /// Unregisters a global hotkey.
    /// </summary>
    /// <param name="id">The registration ID.</param>
    public void Unregister(int id)
    {
        if (_registrations.TryRemove(id, out var registration))
        {
            uint[] masks = GetModifierCombinations(registration.Modifiers);

            foreach (var mask in masks)
            {
                XUngrabKey(_display, registration.KeyCode, mask, _rootWindow);
            }

            XFlush(_display);
        }
    }

    /// <summary>
    /// Unregisters all global hotkeys.
    /// </summary>
    public void UnregisterAll()
    {
        foreach (var id in _registrations.Keys.ToList())
        {
            Unregister(id);
        }
    }

    private void ListenForHotkeys()
    {
        while (_isListening && _display != IntPtr.Zero)
        {
            try
            {
                if (XPending(_display) > 0)
                {
                    var xevent = new XEvent();
                    XNextEvent(_display, ref xevent);

                    if (xevent.type == KeyPress)
                    {
                        var keyEvent = xevent.KeyEvent;
                        ProcessKeyEvent(keyEvent.keycode, keyEvent.state);
                    }
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GlobalHotkeyService error: {ex.Message}");
            }
        }
    }

    private void ProcessKeyEvent(int keyCode, uint state)
    {
        // Remove NumLock and CapsLock from state for comparison
        uint cleanState = state & ~(NumLockMask | CapsLockMask | ScrollLockMask);

        foreach (var registration in _registrations.Values)
        {
            if (registration.KeyCode == keyCode &&
                (registration.Modifiers == cleanState ||
                 registration.Modifiers == (cleanState & ~Mod2Mask))) // Mod2 is often NumLock
            {
                OnHotkeyPressed(registration);
                break;
            }
        }
    }

    private void OnHotkeyPressed(HotkeyRegistration registration)
    {
        HotkeyPressed?.Invoke(this, new HotkeyEventArgs(
            registration.Id,
            registration.Key,
            registration.ModifierKeys));
    }

    private uint GetModifierMask(HotkeyModifiers modifiers)
    {
        uint mask = 0;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) mask |= ShiftMask;
        if (modifiers.HasFlag(HotkeyModifiers.Control)) mask |= ControlMask;
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) mask |= Mod1Mask;
        if (modifiers.HasFlag(HotkeyModifiers.Super)) mask |= Mod4Mask;
        return mask;
    }

    private uint[] GetModifierCombinations(uint baseMask)
    {
        // Include combinations with NumLock and CapsLock
        return new uint[]
        {
            baseMask,
            baseMask | NumLockMask,
            baseMask | CapsLockMask,
            baseMask | NumLockMask | CapsLockMask
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _isListening = false;

        UnregisterAll();

        if (_display != IntPtr.Zero)
        {
            XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }
    }

    #region X11 Interop

    private const int KeyPress = 2;
    private const int GrabModeAsync = 1;

    private const uint ShiftMask = 1 << 0;
    private const uint LockMask = 1 << 1;     // CapsLock
    private const uint ControlMask = 1 << 2;
    private const uint Mod1Mask = 1 << 3;     // Alt
    private const uint Mod2Mask = 1 << 4;     // NumLock
    private const uint Mod4Mask = 1 << 6;     // Super

    private const uint NumLockMask = Mod2Mask;
    private const uint CapsLockMask = LockMask;
    private const uint ScrollLockMask = 0; // Usually not used

    [StructLayout(LayoutKind.Explicit)]
    private struct XEvent
    {
        [FieldOffset(0)] public int type;
        [FieldOffset(0)] public XKeyEvent KeyEvent;
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
        public int keycode;
        public bool same_screen;
    }

    [DllImport("libX11.so.6")]
    private static extern nint XOpenDisplay(nint display);

    [DllImport("libX11.so.6")]
    private static extern void XCloseDisplay(nint display);

    [DllImport("libX11.so.6")]
    private static extern nint XDefaultRootWindow(nint display);

    [DllImport("libX11.so.6")]
    private static extern int XKeysymToKeycode(nint display, nint keysym);

    [DllImport("libX11.so.6")]
    private static extern int XGrabKey(nint display, int keycode, uint modifiers, nint grabWindow,
        bool ownerEvents, int pointerMode, int keyboardMode);

    [DllImport("libX11.so.6")]
    private static extern int XUngrabKey(nint display, int keycode, uint modifiers, nint grabWindow);

    [DllImport("libX11.so.6")]
    private static extern int XPending(nint display);

    [DllImport("libX11.so.6")]
    private static extern int XNextEvent(nint display, ref XEvent xevent);

    [DllImport("libX11.so.6")]
    private static extern void XFlush(nint display);

    #endregion

    private class HotkeyRegistration
    {
        public int Id { get; set; }
        public int KeyCode { get; set; }
        public uint Modifiers { get; set; }
        public HotkeyKey Key { get; set; }
        public HotkeyModifiers ModifierKeys { get; set; }
    }
}

/// <summary>
/// Event args for hotkey pressed events.
/// </summary>
public class HotkeyEventArgs : EventArgs
{
    /// <summary>
    /// Gets the registration ID.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the key.
    /// </summary>
    public HotkeyKey Key { get; }

    /// <summary>
    /// Gets the modifier keys.
    /// </summary>
    public HotkeyModifiers Modifiers { get; }

    public HotkeyEventArgs(int id, HotkeyKey key, HotkeyModifiers modifiers)
    {
        Id = id;
        Key = key;
        Modifiers = modifiers;
    }
}

/// <summary>
/// Hotkey modifier keys.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Shift = 1 << 0,
    Control = 1 << 1,
    Alt = 1 << 2,
    Super = 1 << 3
}

/// <summary>
/// Hotkey keys (X11 keysyms).
/// </summary>
public enum HotkeyKey : uint
{
    // Letters
    A = 0x61, B = 0x62, C = 0x63, D = 0x64, E = 0x65,
    F = 0x66, G = 0x67, H = 0x68, I = 0x69, J = 0x6A,
    K = 0x6B, L = 0x6C, M = 0x6D, N = 0x6E, O = 0x6F,
    P = 0x70, Q = 0x71, R = 0x72, S = 0x73, T = 0x74,
    U = 0x75, V = 0x76, W = 0x77, X = 0x78, Y = 0x79,
    Z = 0x7A,

    // Numbers
    D0 = 0x30, D1 = 0x31, D2 = 0x32, D3 = 0x33, D4 = 0x34,
    D5 = 0x35, D6 = 0x36, D7 = 0x37, D8 = 0x38, D9 = 0x39,

    // Function keys
    F1 = 0xFFBE, F2 = 0xFFBF, F3 = 0xFFC0, F4 = 0xFFC1,
    F5 = 0xFFC2, F6 = 0xFFC3, F7 = 0xFFC4, F8 = 0xFFC5,
    F9 = 0xFFC6, F10 = 0xFFC7, F11 = 0xFFC8, F12 = 0xFFC9,

    // Special keys
    Escape = 0xFF1B,
    Tab = 0xFF09,
    Return = 0xFF0D,
    Space = 0x20,
    BackSpace = 0xFF08,
    Delete = 0xFFFF,
    Insert = 0xFF63,
    Home = 0xFF50,
    End = 0xFF57,
    PageUp = 0xFF55,
    PageDown = 0xFF56,

    // Arrow keys
    Left = 0xFF51,
    Up = 0xFF52,
    Right = 0xFF53,
    Down = 0xFF54,

    // Media keys
    AudioPlay = 0x1008FF14,
    AudioStop = 0x1008FF15,
    AudioPrev = 0x1008FF16,
    AudioNext = 0x1008FF17,
    AudioMute = 0x1008FF12,
    AudioRaiseVolume = 0x1008FF13,
    AudioLowerVolume = 0x1008FF11,

    // Print screen
    Print = 0xFF61
}
