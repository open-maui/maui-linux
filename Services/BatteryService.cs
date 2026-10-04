// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Maui.Devices;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux battery implementation. Reads from /sys/class/power_supply/ on devices with batteries.
/// </summary>
public class BatteryService : IBattery
{
    private static string PowerSupplyPath => Sysfs.PathOf("class", "power_supply");

    public double ChargeLevel
    {
        get
        {
            var capacity = ReadBatteryFile("capacity")?.Trim();
            return double.TryParse(capacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var level)
                ? Math.Clamp(level / 100.0, 0.0, 1.0)
                : 1.0;
        }
    }

    public BatteryState State
    {
        get
        {
            var status = ReadBatteryFile("status")?.Trim().ToLowerInvariant();
            return status switch
            {
                "charging" => BatteryState.Charging,
                "discharging" => BatteryState.Discharging,
                "full" => BatteryState.Full,
                "not charging" => BatteryState.NotCharging,
                // No system battery (a desktop): NotPresent, as Windows reports it.
                null => BatteryState.NotPresent,
                _ => BatteryState.Unknown,
            };
        }
    }

    public BatteryPowerSource PowerSource
    {
        get
        {
            var status = ReadBatteryFile("status")?.Trim().ToLowerInvariant();
            if (status is "charging" or "full")
                return BatteryPowerSource.AC;

            // No battery at all (desktop) or a mains supply reporting online:
            // the machine is wall-powered.
            if (status == null || MainsOnline())
                return BatteryPowerSource.AC;

            return BatteryPowerSource.Battery;
        }
    }

    private readonly PowerProfilesMonitor _powerProfiles;

    public BatteryService() : this(PowerProfilesMonitor.Shared)
    {
    }

    internal BatteryService(PowerProfilesMonitor powerProfiles, Action<Action>? dispatch = null)
    {
        _powerProfiles = powerProfiles;
        _dispatch = dispatch;
    }

    /// <summary>How events reach the app: the UI thread, unless a test runs them inline.</summary>
    private readonly Action<Action>? _dispatch;

    /// <summary>On while the desktop's power profile is "power-saver" (power-profiles-daemon), Unknown without the daemon.</summary>
    public EnergySaverStatus EnergySaverStatus => _powerProfiles.Status;

    public event EventHandler<BatteryInfoChangedEventArgs>? BatteryInfoChanged;

    private EventHandler<EnergySaverStatusChangedEventArgs>? _energySaverStatusChanged;

    /// <summary>Raised on the UI thread when the power profile enters or leaves "power-saver".</summary>
    public event EventHandler<EnergySaverStatusChangedEventArgs>? EnergySaverStatusChanged
    {
        add
        {
            if (_energySaverStatusChanged == null)
            {
                _powerProfiles.StatusChanged += OnEnergySaverChanged;
                _ = _powerProfiles.EnsureStarted();
            }
            _energySaverStatusChanged += value;
        }
        remove
        {
            _energySaverStatusChanged -= value;
            if (_energySaverStatusChanged == null)
                _powerProfiles.StatusChanged -= OnEnergySaverChanged;
        }
    }

    private void OnEnergySaverChanged(EnergySaverStatus status)
    {
        void Raise() => _energySaverStatusChanged?.Invoke(this, new EnergySaverStatusChangedEventArgs(status));
        if (_dispatch != null)
        {
            _dispatch(Raise);
            return;
        }
        var dispatcher = Microsoft.Maui.Platform.Linux.Dispatching.LinuxDispatcher.Main;
        if (dispatcher == null || Microsoft.Maui.Platform.Linux.Dispatching.LinuxDispatcher.IsMainThread)
            Raise();
        else
            dispatcher.Dispatch(Raise);
    }

    /// <summary>
    /// Reads a node from the first system battery: a supply of type "Battery" that is not a
    /// peripheral's (scope "Device", e.g. a wireless mouse), which is not the machine's battery.
    /// </summary>
    private static string? ReadBatteryFile(string fileName)
        => ReadSupplyFile("Battery", fileName);

    private static bool MainsOnline()
        => ReadSupplyFile("Mains", "online")?.Trim() == "1";

    private static string? ReadSupplyFile(string supplyType, string fileName)
    {
        try
        {
            var root = PowerSupplyPath;
            if (!Directory.Exists(root)) return null;
            foreach (var dir in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.Ordinal))
            {
                var typePath = Path.Combine(dir, "type");
                if (File.Exists(typePath) && File.ReadAllText(typePath).Trim() == supplyType)
                {
                    var scopePath = Path.Combine(dir, "scope");
                    if (File.Exists(scopePath) && File.ReadAllText(scopePath).Trim() == "Device")
                        continue;
                    var filePath = Path.Combine(dir, fileName);
                    if (File.Exists(filePath))
                        return File.ReadAllText(filePath);
                }
            }
        }
        catch { }
        return null;
    }
}
