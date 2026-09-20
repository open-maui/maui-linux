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

    public EnergySaverStatus EnergySaverStatus => EnergySaverStatus.Unknown;

    public event EventHandler<BatteryInfoChangedEventArgs>? BatteryInfoChanged;
    public event EventHandler<EnergySaverStatusChangedEventArgs>? EnergySaverStatusChanged;

    /// <summary>Reads a node from the first supply whose type is "Battery".</summary>
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
