// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using HarmonyLib;
using Microsoft.Maui.Platform.Linux.Handlers;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// CommunityToolkit.Maui features whose platform-neutral build (the one a Linux app runs) does
/// nothing or throws, given their Linux behaviour: TouchBehavior and ImageTouchBehavior, Toast and
/// Snackbar, Badge, SpeechToText and OfflineSpeechToText, and DrawingView image export. Each part
/// patches only when its toolkit type is in the app; OpenMaui takes no dependency on the toolkit.
/// </summary>
internal static class CommunityToolkitPatches
{
    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        var harmony = new Harmony("com.openmaui.communitytoolkit");
        Run("TouchBehavior", () => ToolkitTouchBehavior.Install(harmony));
        Run("Toast/Snackbar", () => ToolkitAlertsBridge.Install(harmony));
        Run("Badge", () => LauncherBadgeService.InstallToolkitBadge(harmony));
        Run("SpeechToText", () => ToolkitSpeechBridge.Install(harmony));
        Run("DrawingView image export", () => Microsoft.Maui.Platform.DrawingImageExport.Install(harmony));
    }

    private static void Run(string part, Action install)
    {
        try
        {
            install();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("CommunityToolkit", $"Installing the Linux {part} failed", ex);
        }
    }
}
