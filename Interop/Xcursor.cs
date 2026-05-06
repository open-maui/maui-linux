// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Interop;

internal static partial class Xcursor
{
    private const string LibXcursor = "libXcursor.so.1";

    [LibraryImport(LibXcursor, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr XcursorLibraryLoadCursor(IntPtr display, string name);
}
