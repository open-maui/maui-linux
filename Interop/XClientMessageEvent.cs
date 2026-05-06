// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Interop;

public struct XClientMessageEvent
{
    public int Type;
    public ulong Serial;
    public int SendEvent;
    public IntPtr Display;
    public IntPtr Window;
    public IntPtr MessageType;
    public int Format;
    public ClientMessageData Data;
}
