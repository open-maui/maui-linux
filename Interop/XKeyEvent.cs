// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Interop;

public struct XKeyEvent
{
    public int Type;
    public ulong Serial;
    public int SendEvent;
    public IntPtr Display;
    public IntPtr Window;
    public IntPtr Root;
    public IntPtr Subwindow;
    public ulong Time;
    public int X;
    public int Y;
    public int XRoot;
    public int YRoot;
    public uint State;
    public uint Keycode;
    public int SameScreen;
}
