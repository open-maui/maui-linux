// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Interop;

/// <summary>
/// SelectionRequest — another client asked us (the selection owner) to convert
/// the selection to a target and place it on their window. We reply with a
/// SelectionNotify. Used by the outgoing XDND path when the drop target reads
/// the dragged payload.
/// </summary>
public struct XSelectionRequestEvent
{
    public int Type;
    public ulong Serial;
    public int SendEvent;
    public IntPtr Display;
    public IntPtr Owner;
    public IntPtr Requestor;
    public IntPtr Selection;
    public IntPtr Target;
    public IntPtr Property;
    public IntPtr Time;
}
