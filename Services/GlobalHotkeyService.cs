// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Provides global hotkey registration and handling using X11.
/// </summary>
public partial class GlobalHotkeyService : IDisposable
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
                DiagnosticLog.Warn("GlobalHotkeyService", $"Failed to grab key {key} with modifiers {modifiers}");
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
                DiagnosticLog.Error("GlobalHotkeyService", $"Error: {ex.Message}");
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

    [LibraryImport("libX11.so.6")]
    private static partial nint XOpenDisplay(nint display);

    [LibraryImport("libX11.so.6")]
    private static partial void XCloseDisplay(nint display);

    [LibraryImport("libX11.so.6")]
    private static partial nint XDefaultRootWindow(nint display);

    [LibraryImport("libX11.so.6")]
    private static partial int XKeysymToKeycode(nint display, nint keysym);

    [LibraryImport("libX11.so.6")]
    private static partial int XGrabKey(nint display, int keycode, uint modifiers, nint grabWindow,
        [MarshalAs(UnmanagedType.Bool)] bool ownerEvents, int pointerMode, int keyboardMode);

    [LibraryImport("libX11.so.6")]
    private static partial int XUngrabKey(nint display, int keycode, uint modifiers, nint grabWindow);

    [LibraryImport("libX11.so.6")]
    private static partial int XPending(nint display);

    [DllImport("libX11.so.6")]
    private static extern int XNextEvent(nint display, ref XEvent xevent);

    [LibraryImport("libX11.so.6")]
    private static partial void XFlush(nint display);

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
