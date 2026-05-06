// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Interop;

public static class XEventMask
{
    public const long KeyPressMask = 1L;
    public const long KeyReleaseMask = 2L;
    public const long ButtonPressMask = 4L;
    public const long ButtonReleaseMask = 8L;
    public const long EnterWindowMask = 16L;
    public const long LeaveWindowMask = 32L;
    public const long PointerMotionMask = 64L;
    public const long ExposureMask = 32768L;
    public const long StructureNotifyMask = 131072L;
    public const long FocusChangeMask = 2097152L;
}
