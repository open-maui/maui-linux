// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Platform.Linux.Dispatching;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Patches MAUI Essentials static APIs that throw NotImplementedInReferenceAssemblyException
/// on Linux. The portable net10.0 DLL has stubs that throw; this uses Harmony to redirect
/// them to working Linux implementations.
/// </summary>
internal static class EssentialsPatches
{
    private static bool _applied;

    /// <summary>
    /// Apply all Essentials patches. Safe to call multiple times (idempotent).
    /// Must be called before any MAUI Essentials static APIs are used.
    /// </summary>
    internal static void Apply()
    {
        if (_applied) return;
        _applied = true;

        try
        {
            var harmony = new Harmony("com.openmaui.essentials");

            PatchMainThread(harmony);
            PatchMainThreadInvoke(harmony);
            PatchDeviceDisplay(harmony);

            DiagnosticLog.Debug("EssentialsPatches", "MAUI Essentials patches applied successfully");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("EssentialsPatches", $"Failed to apply Essentials patches: {ex.Message}", ex);
        }
    }

    private static void PatchMainThread(Harmony harmony)
    {
        var mainThreadType = typeof(Microsoft.Maui.ApplicationModel.MainThread);

        var original = mainThreadType.GetProperty("PlatformIsMainThread",
            BindingFlags.Static | BindingFlags.NonPublic)?.GetMethod;

        if (original != null)
        {
            var prefix = typeof(EssentialsPatches).GetMethod(nameof(PlatformIsMainThread_Prefix),
                BindingFlags.Static | BindingFlags.NonPublic)!;
            harmony.Patch(original, new HarmonyMethod(prefix));
        }
    }

    private static void PatchMainThreadInvoke(Harmony harmony)
    {
        var mainThreadType = typeof(Microsoft.Maui.ApplicationModel.MainThread);

        var original = mainThreadType.GetMethod("PlatformBeginInvokeOnMainThread",
            BindingFlags.Static | BindingFlags.NonPublic);

        if (original != null)
        {
            var prefix = typeof(EssentialsPatches).GetMethod(nameof(PlatformBeginInvokeOnMainThread_Prefix),
                BindingFlags.Static | BindingFlags.NonPublic)!;
            harmony.Patch(original, new HarmonyMethod(prefix));
        }
    }

    private static void PatchDeviceDisplay(Harmony harmony)
    {
        // The static DeviceDisplay.Current delegates to DeviceDisplayImplementation
        // which extends DeviceDisplayImplementationBase. The base class's
        // GetMainDisplayInfo() throws on Linux. Patch it to use our service.
        var implBaseType = typeof(DeviceDisplay).Assembly.GetType(
            "Microsoft.Maui.Devices.DeviceDisplayImplementationBase");
        if (implBaseType == null)
        {
            // Try the concrete implementation type
            implBaseType = typeof(DeviceDisplay).Assembly.GetType(
                "Microsoft.Maui.Devices.DeviceDisplayImplementation");
        }

        if (implBaseType != null)
        {
            var original = implBaseType.GetMethod("GetMainDisplayInfo",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (original != null)
            {
                var prefix = typeof(EssentialsPatches).GetMethod(nameof(GetMainDisplayInfo_Prefix),
                    BindingFlags.Static | BindingFlags.NonPublic)!;
                harmony.Patch(original, new HarmonyMethod(prefix));
                DiagnosticLog.Debug("EssentialsPatches", $"Patched {implBaseType.Name}.GetMainDisplayInfo");
            }
            else
            {
                DiagnosticLog.Error("EssentialsPatches", $"GetMainDisplayInfo not found on {implBaseType.Name}");
            }
        }
        else
        {
            DiagnosticLog.Error("EssentialsPatches", "DeviceDisplayImplementationBase type not found");
        }
    }

    /// <summary>
    /// Replacement for DeviceDisplayImplementationBase.GetMainDisplayInfo.
    /// Returns the display info from our DeviceDisplayService.
    /// </summary>
    private static bool GetMainDisplayInfo_Prefix(ref DisplayInfo __result)
    {
        __result = DeviceDisplayService.Instance.MainDisplayInfo;
        return false; // Skip original (which throws)
    }

    /// <summary>
    /// Replacement for MainThread.PlatformIsMainThread.
    /// Returns true if called from the main thread (thread ID captured at startup).
    /// </summary>
    private static bool PlatformIsMainThread_Prefix(ref bool __result)
    {
        __result = LinuxDispatcher.IsMainThread;
        return false; // Skip original (which throws)
    }

    /// <summary>
    /// Replacement for MainThread.PlatformBeginInvokeOnMainThread.
    /// Dispatches the action to the GTK main loop.
    /// </summary>
    private static int _dispatchCount;

    private static bool PlatformBeginInvokeOnMainThread_Prefix(Action action)
    {
        _dispatchCount++;
        if (_dispatchCount <= 20)
        {
            DiagnosticLog.Error("EssentialsPatches", $"BeginInvokeOnMainThread #{_dispatchCount}: isMain={LinuxDispatcher.IsMainThread}, dispatcher={LinuxDispatcher.Main != null}");
        }

        if (LinuxDispatcher.IsMainThread)
        {
            action();
        }
        else
        {
            var dispatcher = LinuxDispatcher.Main;
            if (dispatcher != null)
            {
                dispatcher.Dispatch(action);
            }
            else
            {
                DiagnosticLog.Error("EssentialsPatches", "DROPPED: No dispatcher available for background thread dispatch!");
            }
        }
        return false; // Skip original
    }
}
