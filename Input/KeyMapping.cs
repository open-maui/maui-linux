// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Interop;

namespace Microsoft.Maui.Platform.Linux.Input;

/// <summary>
/// Maps X11 keycodes/keysyms to MAUI Key enum.
/// </summary>
public static class KeyMapping
{
    // X11 keysym values
    private const int XK_BackSpace = 0xff08;
    private const int XK_Tab = 0xff09;
    private const int XK_Return = 0xff0d;
    private const int XK_Escape = 0xff1b;
    private const int XK_Delete = 0xffff;
    private const int XK_Home = 0xff50;
    private const int XK_Left = 0xff51;
    private const int XK_Up = 0xff52;
    private const int XK_Right = 0xff53;
    private const int XK_Down = 0xff54;
    private const int XK_Page_Up = 0xff55;
    private const int XK_Page_Down = 0xff56;
    private const int XK_End = 0xff57;
    private const int XK_Insert = 0xff63;
    private const int XK_F1 = 0xffbe;
    private const int XK_Shift_L = 0xffe1;
    private const int XK_Shift_R = 0xffe2;
    private const int XK_Control_L = 0xffe3;
    private const int XK_Control_R = 0xffe4;
    private const int XK_Alt_L = 0xffe9;
    private const int XK_Alt_R = 0xffea;
    private const int XK_Super_L = 0xffeb;
    private const int XK_Super_R = 0xffec;
    private const int XK_Caps_Lock = 0xffe5;
    private const int XK_Num_Lock = 0xff7f;
    private const int XK_Scroll_Lock = 0xff14;

    private static readonly Dictionary<int, Key> KeysymToKey = new()
    {
        // Special keys
        [XK_BackSpace] = Key.Backspace,
        [XK_Tab] = Key.Tab,
        [XK_Return] = Key.Enter,
        [XK_Escape] = Key.Escape,
        [XK_Delete] = Key.Delete,
        [XK_Home] = Key.Home,
        [XK_End] = Key.End,
        [XK_Insert] = Key.Insert,
        [XK_Page_Up] = Key.PageUp,
        [XK_Page_Down] = Key.PageDown,

        // Arrow keys
        [XK_Left] = Key.Left,
        [XK_Up] = Key.Up,
        [XK_Right] = Key.Right,
        [XK_Down] = Key.Down,

        // Modifiers
        [XK_Shift_L] = Key.Shift,
        [XK_Shift_R] = Key.Shift,
        [XK_Control_L] = Key.Control,
        [XK_Control_R] = Key.Control,
        [XK_Alt_L] = Key.Alt,
        [XK_Alt_R] = Key.Alt,
        [XK_Super_L] = Key.Super,
        [XK_Super_R] = Key.Super,
        [XK_Caps_Lock] = Key.CapsLock,
        [XK_Num_Lock] = Key.NumLock,
        [XK_Scroll_Lock] = Key.ScrollLock,

        // Function keys
        [XK_F1] = Key.F1,
        [XK_F1 + 1] = Key.F2,
        [XK_F1 + 2] = Key.F3,
        [XK_F1 + 3] = Key.F4,
        [XK_F1 + 4] = Key.F5,
        [XK_F1 + 5] = Key.F6,
        [XK_F1 + 6] = Key.F7,
        [XK_F1 + 7] = Key.F8,
        [XK_F1 + 8] = Key.F9,
        [XK_F1 + 9] = Key.F10,
        [XK_F1 + 10] = Key.F11,
        [XK_F1 + 11] = Key.F12,

        // Space
        [0x20] = Key.Space,

        // Punctuation
        [','] = Key.Comma,
        ['.'] = Key.Period,
        ['/'] = Key.Slash,
        [';'] = Key.Semicolon,
        ['\''] = Key.Quote,
        ['['] = Key.LeftBracket,
        [']'] = Key.RightBracket,
        ['\\'] = Key.Backslash,
        ['-'] = Key.Minus,
        ['='] = Key.Equals,
        ['`'] = Key.Grave,
    };

    /// <summary>
    /// Converts an X11 keysym to a MAUI Key.
    /// </summary>
    public static Key FromKeysym(ulong keysym)
    {
        // Check direct mapping
        if (KeysymToKey.TryGetValue((int)keysym, out var key))
            return key;

        // Letters (a-z, A-Z)
        if (keysym >= 'a' && keysym <= 'z')
            return Key.A + (int)(keysym - 'a');
        if (keysym >= 'A' && keysym <= 'Z')
            return Key.A + (int)(keysym - 'A');

        // Numbers (0-9)
        if (keysym >= '0' && keysym <= '9')
            return Key.D0 + (int)(keysym - '0');

        // Numpad numbers (0xff[b0-b9])
        if (keysym >= 0xffb0 && keysym <= 0xffb9)
            return Key.NumPad0 + (int)(keysym - 0xffb0);

        return Key.Unknown;
    }

    /// <summary>
    /// Gets the keysym from X11 keycode.
    /// </summary>
    public static ulong GetKeysym(IntPtr display, uint keycode, bool shifted)
    {
        var index = shifted ? 1 : 0;
        return X11.XKeycodeToKeysym(display, (int)keycode, index);
    }

    /// <summary>
    /// Converts X11 modifier state to KeyModifiers.
    /// </summary>
    public static KeyModifiers GetModifiers(uint state)
    {
        var modifiers = KeyModifiers.None;

        if ((state & 0x01) != 0) modifiers |= KeyModifiers.Shift;
        if ((state & 0x04) != 0) modifiers |= KeyModifiers.Control;
        if ((state & 0x08) != 0) modifiers |= KeyModifiers.Alt;
        if ((state & 0x40) != 0) modifiers |= KeyModifiers.Super;
        if ((state & 0x02) != 0) modifiers |= KeyModifiers.CapsLock;
        if ((state & 0x10) != 0) modifiers |= KeyModifiers.NumLock;

        return modifiers;
    }
}
