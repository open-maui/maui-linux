// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// A native window that knows where it is on the screen (X11: its position relative to the root
/// window). A Wayland client is never told its window's position, so Wayland windows do not
/// implement it.
/// </summary>
internal interface IScreenPositionedWindow
{
    /// <summary>The top-left of the window's drawing surface on the screen, in physical pixels.</summary>
    bool TryGetScreenOrigin(out int x, out int y);
}
