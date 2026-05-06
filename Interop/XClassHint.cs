// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Interop;

[StructLayout(LayoutKind.Sequential)]
public struct XClassHint
{
    public IntPtr res_name;
    public IntPtr res_class;
}
