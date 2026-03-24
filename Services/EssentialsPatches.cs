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

        try { PatchPreferences(harmony); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Preferences patch failed: {ex.Message}", ex); }

        try { PatchSecureStorage(harmony); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"SecureStorage patch failed: {ex.Message}", ex); }

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
    // Preferences patch
    // -----------------------------------------------------------------------

    private static PreferencesService? _preferencesInstance;
    private static PreferencesService PreferencesInstance => _preferencesInstance ??= new PreferencesService();

    private static void PatchPreferences(Harmony harmony)
    {
        // Preferences.Default delegates to PreferencesImplementation which throws on Linux.
        // Patch the concrete implementation's methods.
        var implType = typeof(Microsoft.Maui.Storage.Preferences).Assembly.GetType(
            "Microsoft.Maui.Storage.PreferencesImplementation");

        if (implType == null)
        {
            DiagnosticLog.Error("EssentialsPatches", "PreferencesImplementation type not found");
            return;
        }

        PatchMethod(harmony, implType, "PlatformGet",
            new[] { typeof(string), typeof(string), typeof(string) },
            nameof(PlatformPreferencesGet_Prefix));

        PatchMethod(harmony, implType, "PlatformSet",
            new[] { typeof(string), typeof(string), typeof(string) },
            nameof(PlatformPreferencesSet_Prefix));

        PatchMethod(harmony, implType, "PlatformContainsKey",
            new[] { typeof(string), typeof(string) },
            nameof(PlatformPreferencesContainsKey_Prefix));

        PatchMethod(harmony, implType, "PlatformRemove",
            new[] { typeof(string), typeof(string) },
            nameof(PlatformPreferencesRemove_Prefix));

        PatchMethod(harmony, implType, "PlatformClear",
            new[] { typeof(string) },
            nameof(PlatformPreferencesClear_Prefix));

        DiagnosticLog.Debug("EssentialsPatches", "Patched Preferences");
    }

    private static bool PlatformPreferencesGet_Prefix(string key, string? defaultValue, string? sharedName, ref string? __result)
    {
        __result = PreferencesInstance.Get(key, defaultValue, sharedName);
        return false;
    }

    private static bool PlatformPreferencesSet_Prefix(string key, string? value, string? sharedName)
    {
        PreferencesInstance.Set(key, value, sharedName);
        return false;
    }

    private static bool PlatformPreferencesContainsKey_Prefix(string key, string? sharedName, ref bool __result)
    {
        __result = PreferencesInstance.ContainsKey(key, sharedName);
        return false;
    }

    private static bool PlatformPreferencesRemove_Prefix(string key, string? sharedName)
    {
        PreferencesInstance.Remove(key, sharedName);
        return false;
    }

    private static bool PlatformPreferencesClear_Prefix(string? sharedName)
    {
        PreferencesInstance.Clear(sharedName);
        return false;
    }

    // -----------------------------------------------------------------------
    // SecureStorage patch
    // -----------------------------------------------------------------------

    private static SecureStorageService? _secureStorageInstance;
    private static SecureStorageService SecureStorageInstance => _secureStorageInstance ??= new SecureStorageService();

    private static void PatchSecureStorage(Harmony harmony)
    {
        var implType = typeof(Microsoft.Maui.Storage.SecureStorage).Assembly.GetType(
            "Microsoft.Maui.Storage.SecureStorageImplementation");

        if (implType == null)
        {
            DiagnosticLog.Error("EssentialsPatches", "SecureStorageImplementation type not found");
            return;
        }

        PatchMethod(harmony, implType, "PlatformGetAsync",
            new[] { typeof(string) },
            nameof(PlatformSecureStorageGetAsync_Prefix));

        PatchMethod(harmony, implType, "PlatformSetAsync",
            new[] { typeof(string), typeof(string) },
            nameof(PlatformSecureStorageSetAsync_Prefix));

        PatchMethod(harmony, implType, "PlatformRemove",
            new[] { typeof(string) },
            nameof(PlatformSecureStorageRemove_Prefix));

        PatchMethod(harmony, implType, "PlatformRemoveAll",
            Type.EmptyTypes,
            nameof(PlatformSecureStorageRemoveAll_Prefix));

        DiagnosticLog.Debug("EssentialsPatches", "Patched SecureStorage");
    }

    private static bool PlatformSecureStorageGetAsync_Prefix(string key, ref Task<string?> __result)
    {
        __result = SecureStorageInstance.GetAsync(key);
        return false;
    }

    private static bool PlatformSecureStorageSetAsync_Prefix(string key, string value, ref Task __result)
    {
        __result = SecureStorageInstance.SetAsync(key, value);
        return false;
    }

    private static bool PlatformSecureStorageRemove_Prefix(string key, ref bool __result)
    {
        __result = SecureStorageInstance.Remove(key);
        return false;
    }

    private static bool PlatformSecureStorageRemoveAll_Prefix()
    {
        SecureStorageInstance.RemoveAll();
        return false;
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    private static void PatchMethod(Harmony harmony, Type type, string methodName, Type[] paramTypes, string prefixName)
    {
        var original = type.GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null, paramTypes, null);

        if (original != null)
        {
            var prefix = typeof(EssentialsPatches).GetMethod(prefixName,
                BindingFlags.Static | BindingFlags.NonPublic)!;
            harmony.Patch(original, new HarmonyMethod(prefix));
        }
        else
        {
            DiagnosticLog.Error("EssentialsPatches", $"{type.Name}.{methodName} not found");
        }
    }

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
