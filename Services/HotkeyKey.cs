// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// X11 keysym values for global hotkey registration.
/// </summary>
public enum HotkeyKey : uint
{
    None = 0,

    // Letters (lowercase X11 keysyms)
    A = 0x61, B = 0x62, C = 0x63, D = 0x64, E = 0x65,
    F = 0x66, G = 0x67, H = 0x68, I = 0x69, J = 0x6A,
    K = 0x6B, L = 0x6C, M = 0x6D, N = 0x6E, O = 0x6F,
    P = 0x70, Q = 0x71, R = 0x72, S = 0x73, T = 0x74,
    U = 0x75, V = 0x76, W = 0x77, X = 0x78, Y = 0x79,
    Z = 0x7A,

    // Digits
    D0 = 0x30, D1 = 0x31, D2 = 0x32, D3 = 0x33, D4 = 0x34,
    D5 = 0x35, D6 = 0x36, D7 = 0x37, D8 = 0x38, D9 = 0x39,

    // Function keys
    F1 = 0xFFBE, F2 = 0xFFBF, F3 = 0xFFC0, F4 = 0xFFC1,
    F5 = 0xFFC2, F6 = 0xFFC3, F7 = 0xFFC4, F8 = 0xFFC5,
    F9 = 0xFFC6, F10 = 0xFFC7, F11 = 0xFFC8, F12 = 0xFFC9,

    // Special keys
    Space = 0x20,
    Enter = 0xFF0D,
    Escape = 0xFF1B,
    Tab = 0xFF09,
    Backspace = 0xFF08,
    Delete = 0xFFFF,
    Insert = 0xFF63,

    // Navigation keys
    Home = 0xFF50,
    End = 0xFF57,
    PageUp = 0xFF55,
    PageDown = 0xFF56,

    // Arrow keys
    Left = 0xFF51,
    Right = 0xFF53,
    Up = 0xFF52,
    Down = 0xFF54,

    // Lock keys
    PrintScreen = 0xFF61,
    Pause = 0xFF13,
    NumLock = 0xFF7F,
    ScrollLock = 0xFF14,
    CapsLock = 0xFFE5
}
