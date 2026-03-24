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

        try { PatchLauncher(harmony); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Launcher patch failed: {ex.Message}", ex); }

        try { RegisterPreferences(); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Preferences registration failed: {ex.Message}", ex); }

        try { RegisterSecureStorage(); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"SecureStorage registration failed: {ex.Message}", ex); }

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
                DiagnosticLog.Debug("EssentialsPatches", $"Patched {implType.Name}.GetMainDisplayInfo");
            }
            else
            {
                DiagnosticLog.Error("EssentialsPatches",
                    $"GetMainDisplayInfo not found on {implType.Name}");
            }
        }
        else
        {
            DiagnosticLog.Error("EssentialsPatches", "DeviceDisplayImplementation type not found");
        }
    }

    private static void PatchLauncher(Harmony harmony)
    {
        // Launcher.Default delegates to LauncherImplementation which throws on Linux.
        // Patch PlatformOpenAsync(Uri) to use xdg-open instead.
        var implType = typeof(Microsoft.Maui.ApplicationModel.Launcher).Assembly.GetType(
            "Microsoft.Maui.ApplicationModel.LauncherImplementation");

        if (implType != null)
        {
            var original = implType.GetMethod("PlatformOpenAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null, new[] { typeof(Uri) }, null);

            if (original != null)
            {
                var prefix = typeof(EssentialsPatches).GetMethod(nameof(PlatformOpenAsync_Prefix),
                    BindingFlags.Static | BindingFlags.NonPublic)!;
                harmony.Patch(original, new HarmonyMethod(prefix));
                DiagnosticLog.Debug("EssentialsPatches", "Patched LauncherImplementation.PlatformOpenAsync");
            }
            else
            {
                DiagnosticLog.Error("EssentialsPatches", "PlatformOpenAsync(Uri) not found on LauncherImplementation");
            }
        }
        else
        {
            DiagnosticLog.Error("EssentialsPatches", "LauncherImplementation type not found");
        }
    }

    private static bool PlatformOpenAsync_Prefix(Uri uri, ref Task<bool> __result)
    {
        __result = Task.Run(() =>
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = uri.ToString(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var process = System.Diagnostics.Process.Start(psi);
                return process != null;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("EssentialsPatches", $"xdg-open failed: {ex.Message}");
                return false;
            }
        });
        return false; // Skip original (which throws)
    }

    /// <summary>
    /// Replacement for DeviceDisplayImplementation.GetMainDisplayInfo.
    /// Returns the display info from our DeviceDisplayService.
    /// </summary>
    private static bool GetMainDisplayInfo_Prefix(ref DisplayInfo __result)
    {
        var real = DeviceDisplayService.Instance.MainDisplayInfo;
        // Report density=1.0 because our rendering engine already handles DPI scaling
        // via canvas.Scale(DpiScale). If we report the real density (e.g., 2.75),
        // MotionCanvas/LiveCharts would double-scale via Canvas.Scale(density).
        __result = new DisplayInfo(real.Width, real.Height, 1.0, real.Orientation, real.Rotation, real.RefreshRate);
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

    // -----------------------------------------------------------------------
    // Preferences — set the static backing field to our Linux implementation
    // -----------------------------------------------------------------------

    private static void RegisterPreferences()
    {
        // Patch the PreferencesImplementation constructor to not throw on Linux.
        // Then set the defaultImplementation field to our Linux service.
        var harmony = new Harmony("com.openmaui.essentials.preferences");
        var asm = typeof(Microsoft.Maui.Storage.IPreferences).Assembly;

        var implType = asm.GetType("Microsoft.Maui.Storage.PreferencesImplementation");
        if (implType != null)
        {
            // Patch the constructor so it doesn't throw NotImplementedInReferenceAssemblyException.
            var ctor = implType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (ctor != null)
            {
                harmony.Patch(ctor, prefix: new HarmonyMethod(typeof(EssentialsPatches), nameof(SuppressConstructor_Prefix)));
            }
        }

        // Now safely access the Preferences type — the cctor will create
        // PreferencesImplementation without throwing.
        var prefsType = asm.GetType("Microsoft.Maui.Storage.Preferences");
        var field = prefsType?.GetField("defaultImplementation",
            BindingFlags.Static | BindingFlags.NonPublic);

        if (field != null)
        {
            field.SetValue(null, new PreferencesService());
            DiagnosticLog.Debug("EssentialsPatches", "Registered Preferences (Linux)");
        }
        else
        {
            DiagnosticLog.Error("EssentialsPatches", "Preferences.defaultImplementation field not found");
        }
    }

    // -----------------------------------------------------------------------
    // SecureStorage — same approach
    // -----------------------------------------------------------------------

    private static void RegisterSecureStorage()
    {
        var harmony = new Harmony("com.openmaui.essentials.securestorage");
        var asm = typeof(Microsoft.Maui.Storage.ISecureStorage).Assembly;

        var implType = asm.GetType("Microsoft.Maui.Storage.SecureStorageImplementation");
        if (implType != null)
        {
            var ctor = implType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (ctor != null)
            {
                harmony.Patch(ctor, prefix: new HarmonyMethod(typeof(EssentialsPatches), nameof(SuppressConstructor_Prefix)));
            }
        }

        var secType = asm.GetType("Microsoft.Maui.Storage.SecureStorage");
        var field = secType?.GetField("defaultImplementation",
            BindingFlags.Static | BindingFlags.NonPublic);

        if (field != null)
        {
            field.SetValue(null, new SecureStorageService());
            DiagnosticLog.Debug("EssentialsPatches", "Registered SecureStorage (Linux)");
        }
        else
        {
            DiagnosticLog.Error("EssentialsPatches", "SecureStorage.defaultImplementation field not found");
        }
    }

    /// <summary>
    /// Suppresses the constructor of *Implementation types that throw
    /// NotImplementedInReferenceAssemblyException on unsupported platforms.
    /// The constructor body is skipped; the instance is created but never used
    /// because we immediately replace it with our Linux implementation.
    /// </summary>
    private static bool SuppressConstructor_Prefix() => false;

    /// <summary>
    /// Replacement for MainThread.PlatformBeginInvokeOnMainThread.
    /// Dispatches the action to the GTK main loop.
    /// </summary>
    private static bool PlatformBeginInvokeOnMainThread_Prefix(Action action)
    {
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
