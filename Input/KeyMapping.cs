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

    // Linux evdev keycode to Key mapping (used by Wayland)
    private static readonly Dictionary<uint, Key> LinuxKeycodeToKey = new()
    {
        // Top row
        [1] = Key.Escape,
        [2] = Key.D1, [3] = Key.D2, [4] = Key.D3, [5] = Key.D4, [6] = Key.D5,
        [7] = Key.D6, [8] = Key.D7, [9] = Key.D8, [10] = Key.D9, [11] = Key.D0,
        [12] = Key.Minus, [13] = Key.Equals, [14] = Key.Backspace, [15] = Key.Tab,

        // QWERTY row
        [16] = Key.Q, [17] = Key.W, [18] = Key.E, [19] = Key.R, [20] = Key.T,
        [21] = Key.Y, [22] = Key.U, [23] = Key.I, [24] = Key.O, [25] = Key.P,
        [26] = Key.LeftBracket, [27] = Key.RightBracket, [28] = Key.Enter,

        // Control and ASDF row
        [29] = Key.Control,
        [30] = Key.A, [31] = Key.S, [32] = Key.D, [33] = Key.F, [34] = Key.G,
        [35] = Key.H, [36] = Key.J, [37] = Key.K, [38] = Key.L,
        [39] = Key.Semicolon, [40] = Key.Quote, [41] = Key.Grave,

        // Shift and ZXCV row
        [42] = Key.Shift, [43] = Key.Backslash,
        [44] = Key.Z, [45] = Key.X, [46] = Key.C, [47] = Key.V, [48] = Key.B,
        [49] = Key.N, [50] = Key.M,
        [51] = Key.Comma, [52] = Key.Period, [53] = Key.Slash, [54] = Key.Shift,

        // Bottom row
        [55] = Key.NumPadMultiply, [56] = Key.Alt, [57] = Key.Space,
        [58] = Key.CapsLock,

        // Function keys
        [59] = Key.F1, [60] = Key.F2, [61] = Key.F3, [62] = Key.F4,
        [63] = Key.F5, [64] = Key.F6, [65] = Key.F7, [66] = Key.F8,
        [67] = Key.F9, [68] = Key.F10,

        // NumLock and numpad
        [69] = Key.NumLock, [70] = Key.ScrollLock,
        [71] = Key.NumPad7, [72] = Key.NumPad8, [73] = Key.NumPad9, [74] = Key.NumPadSubtract,
        [75] = Key.NumPad4, [76] = Key.NumPad5, [77] = Key.NumPad6, [78] = Key.NumPadAdd,
        [79] = Key.NumPad1, [80] = Key.NumPad2, [81] = Key.NumPad3,
        [82] = Key.NumPad0, [83] = Key.NumPadDecimal,

        // More function keys
        [87] = Key.F11, [88] = Key.F12,

        // Extended keys
        [96] = Key.Enter,      // NumPad Enter
        [97] = Key.Control,    // Right Control
        [98] = Key.NumPadDivide,
        [99] = Key.PrintScreen,
        [100] = Key.Alt,       // Right Alt
        [102] = Key.Home,
        [103] = Key.Up,
        [104] = Key.PageUp,
        [105] = Key.Left,
        [106] = Key.Right,
        [107] = Key.End,
        [108] = Key.Down,
        [109] = Key.PageDown,
        [110] = Key.Insert,
        [111] = Key.Delete,
        [119] = Key.Pause,
        [125] = Key.Super,     // Left Super (Windows key)
        [126] = Key.Super,     // Right Super
        [127] = Key.Menu,
    };

    /// <summary>
    /// Converts a Linux evdev keycode to a MAUI Key.
    /// Used for Wayland input where keycodes are offset by 8 from X11 keycodes.
    /// </summary>
    public static Key FromLinuxKeycode(uint keycode)
    {
        // Wayland uses evdev keycodes, X11 uses keycodes + 8
        // If caller added 8, subtract it
        var evdevCode = keycode >= 8 ? keycode - 8 : keycode;

        if (LinuxKeycodeToKey.TryGetValue(evdevCode, out var key))
            return key;

        return Key.Unknown;
    }

    /// <summary>
    /// Converts a Key to its character representation, if applicable.
    /// </summary>
    public static char? ToChar(Key key, KeyModifiers modifiers)
    {
        bool shift = modifiers.HasFlag(KeyModifiers.Shift);
        bool capsLock = modifiers.HasFlag(KeyModifiers.CapsLock);
        bool upper = shift ^ capsLock;

        // Letters
        if (key >= Key.A && key <= Key.Z)
        {
            char ch = (char)('a' + (key - Key.A));
            return upper ? char.ToUpper(ch) : ch;
        }

        // Numbers (with shift gives symbols)
        if (key >= Key.D0 && key <= Key.D9)
        {
            if (shift)
            {
                return (key - Key.D0) switch
                {
                    0 => ')',
                    1 => '!',
                    2 => '@',
                    3 => '#',
                    4 => '$',
                    5 => '%',
                    6 => '^',
                    7 => '&',
                    8 => '*',
                    9 => '(',
                    _ => null
                };
            }
            return (char)('0' + (key - Key.D0));
        }

        // NumPad numbers
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return (char)('0' + (key - Key.NumPad0));

        // Punctuation
        return key switch
        {
            Key.Space => ' ',
            Key.Comma => shift ? '<' : ',',
            Key.Period => shift ? '>' : '.',
            Key.Slash => shift ? '?' : '/',
            Key.Semicolon => shift ? ':' : ';',
            Key.Quote => shift ? '"' : '\'',
            Key.LeftBracket => shift ? '{' : '[',
            Key.RightBracket => shift ? '}' : ']',
            Key.Backslash => shift ? '|' : '\\',
            Key.Minus => shift ? '_' : '-',
            Key.Equals => shift ? '+' : '=',
            Key.Grave => shift ? '~' : '`',
            Key.NumPadAdd => '+',
            Key.NumPadSubtract => '-',
            Key.NumPadMultiply => '*',
            Key.NumPadDivide => '/',
            Key.NumPadDecimal => '.',
            _ => null
        };
    }
}
