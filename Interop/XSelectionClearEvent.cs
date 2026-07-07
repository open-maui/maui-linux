// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Interop;

/// <summary>
/// SelectionClear — we lost ownership of a selection to another client.
/// The outgoing XDND path treats this as drag cancellation.
/// </summary>
public struct XSelectionClearEvent
{
    public int Type;
    public ulong Serial;
    public int SendEvent;
    public IntPtr Display;
    public IntPtr Window;
    public IntPtr Selection;
    public IntPtr Time;
}
