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

        var harmony = new Harmony("com.openmaui.essentials");

        try { PatchMainThread(harmony); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"MainThread patch failed: {ex.Message}", ex); }

        try { PatchMainThreadInvoke(harmony); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"MainThreadInvoke patch failed: {ex.Message}", ex); }

        try { PatchDeviceDisplay(harmony); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"DeviceDisplay patch failed: {ex.Message}", ex); }

        DiagnosticLog.Debug("EssentialsPatches", "MAUI Essentials patches applied");
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
        // The static DeviceDisplay.Current delegates to an internal implementation.
        // The concrete DeviceDisplayImplementation.GetMainDisplayInfo() throws on Linux.
        // We patch the concrete class's override (not the abstract base).
        var implType = typeof(DeviceDisplay).Assembly.GetType(
            "Microsoft.Maui.Devices.DeviceDisplayImplementation");

        if (implType != null)
        {
            var original = implType.GetMethod("GetMainDisplayInfo",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);

            if (original != null)
            {
                var prefix = typeof(EssentialsPatches).GetMethod(nameof(GetMainDisplayInfo_Prefix),
                    BindingFlags.Static | BindingFlags.NonPublic)!;
                harmony.Patch(original, new HarmonyMethod(prefix));
                DiagnosticLog.Error("EssentialsPatches", $"Patched {implType.Name}.GetMainDisplayInfo");
            }
            else
            {
                DiagnosticLog.Error("EssentialsPatches",
                    $"GetMainDisplayInfo not found (DeclaredOnly) on {implType.Name}, trying all methods");

                // List available methods for debugging
                foreach (var m in implType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    DiagnosticLog.Error("EssentialsPatches", $"  Method: {m.Name}");
            }
        }
        else
        {
            DiagnosticLog.Error("EssentialsPatches", "DeviceDisplayImplementation type not found");

            // List all types with "Display" in the name for debugging
            foreach (var t in typeof(DeviceDisplay).Assembly.GetTypes())
            {
                if (t.Name.Contains("Display"))
                    DiagnosticLog.Error("EssentialsPatches", $"  Type: {t.FullName}");
            }
        }
    }

    /// <summary>
    /// Replacement for DeviceDisplayImplementation.GetMainDisplayInfo.
    /// Returns the display info from our DeviceDisplayService.
    /// </summary>
    private static int _displayInfoCallCount;
    private static bool GetMainDisplayInfo_Prefix(ref DisplayInfo __result)
    {
        var real = DeviceDisplayService.Instance.MainDisplayInfo;
        // Report density=1.0 because our rendering engine already handles DPI scaling
        // via canvas.Scale(DpiScale). If we report the real density (e.g., 2.75),
        // MotionCanvas/LiveCharts would double-scale via Canvas.Scale(density).
        __result = new DisplayInfo(real.Width, real.Height, 1.0, real.Orientation, real.Rotation, real.RefreshRate);
        _displayInfoCallCount++;
        if (_displayInfoCallCount <= 5)
            DiagnosticLog.Error("EssentialsPatches", $"GetMainDisplayInfo #{_displayInfoCallCount}: density={__result.Density} (real={real.Density}), {__result.Width}x{__result.Height}");
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
