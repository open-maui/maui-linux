// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Native;

internal static partial class LibcNative
{
    private const string Libc = "libc.so.6";

    public const short POLLIN = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    public struct PollFd
    {
        public int Fd;
        public short Events;
        public short Revents;
    }

    [LibraryImport(Libc, EntryPoint = "poll", SetLastError = true)]
    public static partial int Poll(ref PollFd fds, nuint nfds, int timeout);
}
