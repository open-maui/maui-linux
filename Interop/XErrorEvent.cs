// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Interop;

public struct XErrorEvent
{
    public int Type;
    public IntPtr Display;
    public IntPtr ResourceId;
    public ulong Serial;
    public byte ErrorCode;
    public byte RequestCode;
    public byte MinorCode;
}
