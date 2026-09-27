// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// A window that knows when it is minimized or otherwise not visible at all:
/// the xdg-shell <c>suspended</c> state on Wayland, <c>_NET_WM_STATE_HIDDEN</c>
/// or unmapping on X11. Drives MAUI's <c>IWindow.Stopped</c> / <c>Resumed</c>.
/// </summary>
public interface IVisibilityAwareDisplayWindow
{
    /// <summary>True while the window is minimized or fully hidden.</summary>
    bool IsSuspended { get; }

    /// <summary>Raised with the new state when it changes.</summary>
    event EventHandler<bool>? SuspendedChanged;
}
