// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// A native window that can ask to be brought to the front and focused
/// (MAUI's <c>Application.ActivateWindow</c>). The window manager or compositor decides:
/// focus-stealing prevention may only mark the window as wanting attention.
/// </summary>
internal interface IActivatableWindow
{
    /// <summary>Asks for the window to be raised and focused; false when nothing could be requested.</summary>
    bool Activate();
}
