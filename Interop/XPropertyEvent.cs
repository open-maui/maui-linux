// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Interop;

/// <summary>
/// PropertyNotify — delivered when a property on the window changes.
/// Requires PropertyChangeMask in the window's event mask. Used by the
/// XDND INCR transfer protocol to signal each incoming data chunk.
/// </summary>
public struct XPropertyEvent
{
    public int Type;
    public ulong Serial;
    public int SendEvent;
    public IntPtr Display;
    public IntPtr Window;
    public IntPtr Atom;
    public IntPtr Time;
    public int State; // 0 = PropertyNewValue, 1 = PropertyDelete
}
