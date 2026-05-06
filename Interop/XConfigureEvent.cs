// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Interop;

public struct XConfigureEvent
{
    public int Type;
    public ulong Serial;
    public int SendEvent;
    public IntPtr Display;
    public IntPtr Event;
    public IntPtr Window;
    public int X;
    public int Y;
    public int Width;
    public int Height;
    public int BorderWidth;
    public IntPtr Above;
    public int OverrideRedirect;
}
