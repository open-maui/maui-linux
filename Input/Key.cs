// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform;

/// <summary>
/// Keyboard key enumeration.
/// </summary>
public enum Key
{
    Unknown = 0,

    // Letters
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    // Numbers
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,

    // Numpad
    NumPad0, NumPad1, NumPad2, NumPad3, NumPad4,
    NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
    NumPadMultiply, NumPadAdd, NumPadSubtract,
    NumPadDecimal, NumPadDivide, NumPadEnter,

    // Function keys
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

    // Navigation
    Left, Up, Right, Down,
    Home, End, PageUp, PageDown,
    Insert, Delete,

    // Modifiers
    Shift, Control, Alt, Super,
    CapsLock, NumLock, ScrollLock,

    // Editing
    Backspace, Tab, Enter, Escape, Space,

    // Punctuation
    Comma, Period, Slash, Semicolon, Quote,
    LeftBracket, RightBracket, Backslash,
    Minus, Equals, Grave,

    // System
    PrintScreen, Pause, Menu,
}
