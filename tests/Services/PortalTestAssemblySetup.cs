// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Maui.Platform.Linux.Services.Portal;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// The test run must never reach the real desktop through D-Bus (a portal
/// OpenURI or FileChooser call would open windows on the developer's screen,
/// Inhibit would block the screen saver). Before any test runs, the portal
/// layer is switched off and the notification server replaced, so every
/// service takes exactly the legacy path the existing tests assert. Portal
/// tests inject fakes explicitly instead of flipping these globals.
/// </summary>
internal static class PortalTestAssemblySetup
{
#pragma warning disable CA2255 // ModuleInitializer in a test assembly is intentional
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void DisableRealDesktopBus()
    {
        DesktopPortal.ModeOverride = PortalMode.Off;
        DesktopPortal.Current = NullDesktopPortal.Instance;
        FreedesktopNotificationServer.Current = NullNotificationServer.Instance;
    }
}
