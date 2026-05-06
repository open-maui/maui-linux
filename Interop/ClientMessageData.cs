// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Interop;

[StructLayout(LayoutKind.Explicit)]
public struct ClientMessageData
{
    [FieldOffset(0)]
    public long L0;

    [FieldOffset(8)]
    public long L1;

    [FieldOffset(16)]
    public long L2;

    [FieldOffset(24)]
    public long L3;

    [FieldOffset(32)]
    public long L4;
}
