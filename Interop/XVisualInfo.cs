// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Interop;

public struct XVisualInfo
{
    public IntPtr Visual;
    public ulong VisualId;
    public int Screen;
    public int Depth;
    public int Class;
    public ulong RedMask;
    public ulong GreenMask;
    public ulong BlueMask;
    public int ColormapSize;
    public int BitsPerRgb;
}
