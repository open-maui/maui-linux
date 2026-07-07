// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Interop;

/// <summary>
/// SelectionNotify — the selection owner's reply to XConvertSelection.
/// Property is None (0) when the conversion was refused.
/// </summary>
public struct XSelectionEvent
{
    public int Type;
    public ulong Serial;
    public int SendEvent;
    public IntPtr Display;
    public IntPtr Requestor;
    public IntPtr Selection;
    public IntPtr Target;
    public IntPtr Property;
    public IntPtr Time;
}
