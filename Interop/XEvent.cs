// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Interop;

[StructLayout(LayoutKind.Explicit, Size = 192)]
public struct XEvent
{
    [FieldOffset(0)]
    public int Type;

    [FieldOffset(0)]
    public XKeyEvent KeyEvent;

    [FieldOffset(0)]
    public XButtonEvent ButtonEvent;

    [FieldOffset(0)]
    public XMotionEvent MotionEvent;

    [FieldOffset(0)]
    public XConfigureEvent ConfigureEvent;

    [FieldOffset(0)]
    public XExposeEvent ExposeEvent;

    [FieldOffset(0)]
    public XClientMessageEvent ClientMessageEvent;

    [FieldOffset(0)]
    public XCrossingEvent CrossingEvent;

    [FieldOffset(0)]
    public XFocusChangeEvent FocusChangeEvent;
}
