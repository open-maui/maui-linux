// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using Microsoft.Maui.Platform.Linux.Services.Portal;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// The desktop's "show animations" setting, followed while the app runs, as MAUI's Windows
/// ticker follows <c>UISettings.AnimationsEnabled</c> through its AnimationsEnabledChanged event.
/// <see cref="Hosting.LinuxTicker.SystemEnabled"/> reads <see cref="Enabled"/>, and the animation
/// manager reads that on every animation and every frame, so a change applies to the next one.
/// <list type="bullet">
/// <item><c>OPENMAUI_REDUCE_MOTION</c> wins: <c>1</c>/<c>true</c>/<c>yes</c>/<c>on</c> turns
/// animations off, <c>0</c>/<c>false</c>/<c>no</c>/<c>off</c> on. It is read each time, so a
/// change made by the app itself applies at once.</item>
/// <item>Otherwise the xdg-desktop-portal Settings interface, read at start and kept current
/// through its SettingChanged signal: <c>org.freedesktop.appearance reduced-motion</c> (1 asks
/// for reduced motion), else GNOME's <c>org.gnome.desktop.interface enable-animations</c>, else
/// KDE's <c>org.kde.kdeglobals.KDE AnimationDurationFactor</c> (0 turns animations off).</item>
/// <item>Without the portal, GNOME's <c>enable-animations</c> through gsettings, read once.</item>
/// </list>
/// Every other case leaves animations on.
/// </summary>
internal sealed class SystemAnimationsSetting
{
    public const string EnvironmentVariable = "OPENMAUI_REDUCE_MOTION";
    public const string ReducedMotionKey = "reduced-motion";
    public const string GnomeEnableAnimationsKey = "enable-animations";
    public const string KdeGlobalsNamespace = "org.kde.kdeglobals.KDE";
    public const string KdeAnimationDurationFactorKey = "AnimationDurationFactor";

    private static readonly TimeSpan PortalStartupWait = TimeSpan.FromMilliseconds(500);
    private static readonly Lazy<SystemAnimationsSetting> s_current = new(CreateCurrent);

    private readonly Func<string?> _environment;
    private readonly Lock _gate = new();
    private bool? _portalReducedMotion;
    private bool? _portalGnomeEnabled;
    private double? _portalKdeFactor;
    private bool _probed;
    private IDisposable? _watch;
    private volatile bool _desktopEnabled;
    private bool _lastReported;

    /// <summary>The process-wide setting, started at first use.</summary>
    public static SystemAnimationsSetting Current => s_current.Value;

    /// <param name="environment">Reads <c>OPENMAUI_REDUCE_MOTION</c>.</param>
    /// <param name="probe">The desktop value without the portal (GNOME's gsettings); null when unknown.</param>
    internal SystemAnimationsSetting(Func<string?> environment, bool? probe)
    {
        _environment = environment;
        _probed = probe ?? true;
        _desktopEnabled = _probed;
        _lastReported = Enabled;
    }

    /// <summary>True when animations run; false when the user or the desktop asks for reduced motion.</summary>
    public bool Enabled => ParseOverride(_environment()) ?? _desktopEnabled;

    /// <summary>True while the portal's SettingChanged subscription is active.</summary>
    public bool IsLive => _watch != null;

    /// <summary>Raised (on a thread-pool thread for a desktop change) when <see cref="Enabled"/> changes.</summary>
    public event Action<bool>? Changed;

    /// <summary>The <c>OPENMAUI_REDUCE_MOTION</c> override: false to turn animations off, true on, null none.</summary>
    internal static bool? ParseOverride(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => false,
            "0" or "false" or "no" or "off" => true,
            _ => null,
        };
    }

    /// <summary>
    /// Reads the portal's settings and follows their changes. Returns false when the Settings
    /// portal is not there (the gsettings value stays).
    /// </summary>
    public async Task<bool> StartAsync(IDesktopPortal portal, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await portal.GetVersionAsync(PortalInterfaces.Settings, cancellationToken).ConfigureAwait(false) == 0)
                return false;

            // Subscribe first so a change between the read and the watch is not lost.
            var watch = await portal.WatchSettingChangedAsync(OnSettingChanged).ConfigureAwait(false);
            lock (_gate)
                _watch = watch;

            await ReadAsync(portal, PortalAppearance.Namespace, ReducedMotionKey, cancellationToken).ConfigureAwait(false);
            await ReadAsync(portal, PortalAppearance.GnomeInterfaceNamespace, GnomeEnableAnimationsKey, cancellationToken).ConfigureAwait(false);
            await ReadAsync(portal, KdeGlobalsNamespace, KdeAnimationDurationFactorKey, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (PortalUnavailableException ex)
        {
            DiagnosticLog.Debug("SystemAnimationsSetting", $"Settings portal unavailable: {ex.Message}");
            return false;
        }
    }

    /// <summary>Stops following the portal.</summary>
    public void Stop()
    {
        IDisposable? watch;
        lock (_gate)
        {
            watch = _watch;
            _watch = null;
        }
        watch?.Dispose();
    }

    private async Task ReadAsync(IDesktopPortal portal, string @namespace, string key, CancellationToken cancellationToken)
    {
        object? value;
        try
        {
            value = await portal.ReadSettingAsync(@namespace, key, cancellationToken).ConfigureAwait(false);
        }
        catch (PortalUnavailableException)
        {
            // A key the portal backend does not have reads as unset.
            return;
        }
        if (value != null)
            OnSettingChanged(@namespace, key, value);
    }

    /// <summary>A portal setting value (from a read or SettingChanged).</summary>
    internal void OnSettingChanged(string @namespace, string key, object value)
    {
        lock (_gate)
        {
            if (@namespace == PortalAppearance.Namespace && key == ReducedMotionKey)
                _portalReducedMotion = PortalVariant.ToUInt32(value) is { } u ? u == 1 : null;
            else if (@namespace == PortalAppearance.GnomeInterfaceNamespace && key == GnomeEnableAnimationsKey)
                _portalGnomeEnabled = ToBoolean(value);
            else if (@namespace == KdeGlobalsNamespace && key == KdeAnimationDurationFactorKey)
                _portalKdeFactor = ToFactor(value);
            else
                return;

            _desktopEnabled = _portalReducedMotion is { } reduced ? !reduced
                : _portalGnomeEnabled is { } gnome ? gnome
                : _portalKdeFactor is { } factor ? factor > 0
                : _probed;
        }
        Report();
    }

    /// <summary>Raises <see cref="Changed"/> when <see cref="Enabled"/> differs from the last value reported.</summary>
    internal void Report()
    {
        bool enabled = Enabled;
        lock (_gate)
        {
            if (enabled == _lastReported)
                return;
            _lastReported = enabled;
        }
        DiagnosticLog.Debug("SystemAnimationsSetting", $"System animations {(enabled ? "enabled" : "disabled")}");
        try
        {
            Changed?.Invoke(enabled);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SystemAnimationsSetting", "A Changed handler failed", ex);
        }
    }

    private static bool? ToBoolean(object? value) => value switch
    {
        bool b => b,
        string s when bool.TryParse(s.Trim(), out var parsed) => parsed,
        _ => PortalVariant.ToUInt32(value) is { } u ? u != 0 : null,
    };

    /// <summary>KDE hands kdeglobals entries over as strings; a number is taken too.</summary>
    private static double? ToFactor(object? value) => value switch
    {
        string s when double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d >= 0 => d,
        _ => PortalVariant.ToDouble(value) is { } n && n >= 0 ? n : null,
    };

    private static SystemAnimationsSetting CreateCurrent()
    {
        static string? Env() => Environment.GetEnvironmentVariable(EnvironmentVariable);

        // With the override set at start there is nothing to probe; the portal is still
        // followed, so clearing the variable later falls back to the desktop's value.
        var setting = new SystemAnimationsSetting(Env, ParseOverride(Env()) is null ? ProbeGsettings() : null);

        if (DesktopPortal.ShouldTry(PortalUse.Always))
        {
            try
            {
                var start = Task.Run(() => setting.StartAsync(DesktopPortal.Current));
                // The first animation should see the desktop's value: wait briefly for the read.
                start.Wait(PortalStartupWait);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Debug("SystemAnimationsSetting", "Settings portal init failed", ex);
            }
        }
        return setting;
    }

    /// <summary>GNOME's enable-animations through gsettings, on GNOME only; null elsewhere or on failure.</summary>
    private static bool? ProbeGsettings()
    {
        try
        {
            var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "";
            if (!desktop.Contains("gnome", StringComparison.OrdinalIgnoreCase))
                return null;

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "gsettings",
                Arguments = "get org.gnome.desktop.interface enable-animations",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(1000))
                return null;

            return !string.Equals(output.Trim(), "false", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }
    }
}
