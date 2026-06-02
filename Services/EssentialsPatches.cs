// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
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

        // Register AppInfo and DeviceInfo FIRST — other services (e.g. Preferences,
        // FilePicker) read AppInfo.Current.Name in their constructors. If we leave
        // the portable AppInfoImplementation in place, those getters throw
        // NotImplementedInReferenceAssemblyException before we get a chance to
        // replace them.
        try { RegisterEssential<Microsoft.Maui.ApplicationModel.IAppInfo>("com.openmaui.essentials.appinfo", "Microsoft.Maui.ApplicationModel.AppInfo", "Microsoft.Maui.ApplicationModel.AppInfoImplementation", AppInfoService.Instance); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"AppInfo registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.Devices.IDeviceInfo>("com.openmaui.essentials.deviceinfo", "Microsoft.Maui.Devices.DeviceInfo", "Microsoft.Maui.Devices.DeviceInfoImplementation", DeviceInfoService.Instance); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"DeviceInfo registration failed: {ex.Message}", ex); }

        try { RegisterPreferences(); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Preferences registration failed: {ex.Message}", ex); }

        try { RegisterSecureStorage(); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"SecureStorage registration failed: {ex.Message}", ex); }

        try { RegisterClipboard(); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Clipboard registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.ApplicationModel.DataTransfer.IShare>("com.openmaui.essentials.share", "Microsoft.Maui.ApplicationModel.DataTransfer.Share", "Microsoft.Maui.ApplicationModel.DataTransfer.ShareImplementation", new ShareService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Share registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.ApplicationModel.IBrowser>("com.openmaui.essentials.browser", "Microsoft.Maui.ApplicationModel.Browser", "Microsoft.Maui.ApplicationModel.BrowserImplementation", new BrowserService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Browser registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.ApplicationModel.Communication.IEmail>("com.openmaui.essentials.email", "Microsoft.Maui.ApplicationModel.Communication.Email", "Microsoft.Maui.ApplicationModel.Communication.EmailImplementation", new EmailService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Email registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.Storage.IFilePicker>("com.openmaui.essentials.filepicker", "Microsoft.Maui.Storage.FilePicker", "Microsoft.Maui.Storage.FilePickerImplementation", new FilePickerService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"FilePicker registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.Networking.IConnectivity>("com.openmaui.essentials.connectivity", "Microsoft.Maui.Networking.Connectivity", "Microsoft.Maui.Networking.ConnectivityImplementation", ConnectivityService.Instance); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Connectivity registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.ApplicationModel.IVersionTracking>("com.openmaui.essentials.versiontracking", "Microsoft.Maui.ApplicationModel.VersionTracking", "Microsoft.Maui.ApplicationModel.VersionTrackingImplementation", new VersionTrackingService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"VersionTracking registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.ApplicationModel.IAppActions>("com.openmaui.essentials.appactions", "Microsoft.Maui.ApplicationModel.AppActions", "Microsoft.Maui.ApplicationModel.AppActionsImplementation", new AppActionsService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"AppActions registration failed: {ex.Message}", ex); }

        // Hardware/sensor services — functional on Linux phones, graceful fallback on desktops
        try { RegisterEssential<Microsoft.Maui.Devices.IBattery>("com.openmaui.essentials.battery", "Microsoft.Maui.Devices.Battery", "Microsoft.Maui.Devices.BatteryImplementation", new BatteryService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Battery registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.Devices.IFlashlight>("com.openmaui.essentials.flashlight", "Microsoft.Maui.Devices.Flashlight", "Microsoft.Maui.Devices.FlashlightImplementation", new FlashlightService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Flashlight registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.Devices.IHapticFeedback>("com.openmaui.essentials.haptic", "Microsoft.Maui.Devices.HapticFeedback", "Microsoft.Maui.Devices.HapticFeedbackImplementation", new HapticFeedbackService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"HapticFeedback registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.Devices.IVibration>("com.openmaui.essentials.vibration", "Microsoft.Maui.Devices.Vibration", "Microsoft.Maui.Devices.VibrationImplementation", new VibrationService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Vibration registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.Devices.Sensors.IGeolocation>("com.openmaui.essentials.geolocation", "Microsoft.Maui.Devices.Sensors.Geolocation", "Microsoft.Maui.Devices.Sensors.GeolocationImplementation", new GeolocationService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Geolocation registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.Devices.Sensors.IGeocoding>("com.openmaui.essentials.geocoding", "Microsoft.Maui.Devices.Sensors.Geocoding", "Microsoft.Maui.Devices.Sensors.GeocodingImplementation", new GeocodingService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Geocoding registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.ApplicationModel.Communication.IPhoneDialer>("com.openmaui.essentials.phonedialer", "Microsoft.Maui.ApplicationModel.Communication.PhoneDialer", "Microsoft.Maui.ApplicationModel.Communication.PhoneDialerImplementation", new PhoneDialerService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"PhoneDialer registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.ApplicationModel.Communication.ISms>("com.openmaui.essentials.sms", "Microsoft.Maui.ApplicationModel.Communication.Sms", "Microsoft.Maui.ApplicationModel.Communication.SmsImplementation", new SmsService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Sms registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.ApplicationModel.Communication.IContacts>("com.openmaui.essentials.contacts", "Microsoft.Maui.ApplicationModel.Communication.Contacts", "Microsoft.Maui.ApplicationModel.Communication.ContactsImplementation", new ContactsService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Contacts registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.Media.IMediaPicker>("com.openmaui.essentials.mediapicker", "Microsoft.Maui.Media.MediaPicker", "Microsoft.Maui.Media.MediaPickerImplementation", new MediaPickerService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"MediaPicker registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.Media.IScreenshot>("com.openmaui.essentials.screenshot", "Microsoft.Maui.Media.Screenshot", "Microsoft.Maui.Media.ScreenshotImplementation", new ScreenshotService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Screenshot registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.Media.ITextToSpeech>("com.openmaui.essentials.tts", "Microsoft.Maui.Media.TextToSpeech", "Microsoft.Maui.Media.TextToSpeechImplementation", new TextToSpeechService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"TextToSpeech registration failed: {ex.Message}", ex); }

        try { RegisterEssential<Microsoft.Maui.ApplicationModel.IMap>("com.openmaui.essentials.map", "Microsoft.Maui.ApplicationModel.Map", "Microsoft.Maui.ApplicationModel.MapImplementation", new MapService()); }
        catch (Exception ex) { DiagnosticLog.Error("EssentialsPatches", $"Map registration failed: {ex.Message}", ex); }

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
        var asm = typeof(Microsoft.Maui.Storage.IPreferences).Assembly;
        TrySetEssential(asm, "Microsoft.Maui.Storage.Preferences", new PreferencesService());
    }

    // -----------------------------------------------------------------------
    // SecureStorage — same approach
    // -----------------------------------------------------------------------

    private static void RegisterSecureStorage()
    {
        var asm = typeof(Microsoft.Maui.Storage.ISecureStorage).Assembly;
        TrySetEssential(asm, "Microsoft.Maui.Storage.SecureStorage", new SecureStorageService());
    }

    // -----------------------------------------------------------------------
    // Clipboard — set the static backing field to our Linux implementation
    // -----------------------------------------------------------------------

    private static void RegisterClipboard()
    {
        var asm = typeof(Microsoft.Maui.ApplicationModel.DataTransfer.IClipboard).Assembly;
        TrySetEssential(asm, "Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard", new ClipboardService());
    }

    // -----------------------------------------------------------------------
    // Generic Essentials registration helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Registers a Linux Essentials implementation on the static facade type.
    /// <paramref name="harmonyId"/> and <paramref name="implTypeName"/> are
    /// accepted for API symmetry but unused — the portable *Implementation
    /// constructors are empty no-ops; only the methods throw, and once the
    /// facade's backing field points at our Linux service, those methods are
    /// never invoked.
    /// </summary>
    private static void RegisterEssential<TInterface>(string harmonyId, string staticTypeName, string implTypeName, object linuxInstance)
    {
        var asm = typeof(TInterface).Assembly;
        TrySetEssential(asm, staticTypeName, linuxInstance);
    }

    /// <summary>
    /// Sets the static backing implementation on a MAUI Essentials facade type.
    /// MAUI 10 uses two naming conventions side-by-side: <c>SetDefault</c> /
    /// <c>defaultImplementation</c> (Preferences, Browser, Launcher, ...) and
    /// <c>SetCurrent</c> / <c>currentImplementation</c> (Connectivity, AppInfo,
    /// DeviceInfo, AppActions). Prefer the public setter — it tolerates future
    /// renames of the private field — then fall back to reflecting either field
    /// name directly so we still work on older Essentials assemblies.
    /// </summary>
    private static void TrySetEssential(Assembly essentialsAsm, string staticTypeName, object linuxInstance)
    {
        var staticType = essentialsAsm.GetType(staticTypeName);
        if (staticType == null)
        {
            DiagnosticLog.Error("EssentialsPatches", $"{staticTypeName} type not found");
            return;
        }

        // Preferred: public SetDefault(T) / SetCurrent(T)
        foreach (var setterName in new[] { "SetDefault", "SetCurrent" })
        {
            var setter = staticType
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == setterName
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType.IsInstanceOfType(linuxInstance));

            if (setter != null)
            {
                setter.Invoke(null, new[] { linuxInstance });
                DiagnosticLog.Debug("EssentialsPatches", $"Registered {staticTypeName} via {setterName} (Linux)");
                return;
            }
        }

        // Fallback: private static field, either naming convention.
        foreach (var fieldName in new[] { "defaultImplementation", "currentImplementation" })
        {
            var field = staticType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, linuxInstance);
                DiagnosticLog.Debug("EssentialsPatches", $"Registered {staticTypeName} via {fieldName} field (Linux)");
                return;
            }
        }

        DiagnosticLog.Error("EssentialsPatches",
            $"{staticTypeName}: no SetDefault/SetCurrent method and no defaultImplementation/currentImplementation field");
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
