// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui.Devices;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux battery implementation. Reads from /sys/class/power_supply/ on devices with batteries.
/// </summary>
public class BatteryService : IBattery
{
    private const string PowerSupplyPath = "/sys/class/power_supply";

    public double ChargeLevel
    {
        get
        {
            var capacity = ReadSysFile("capacity");
            return double.TryParse(capacity, out var level) ? level / 100.0 : 1.0;
        }
    }

    public BatteryState State
    {
        get
        {
            var status = ReadSysFile("status")?.Trim().ToLowerInvariant();
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
            var status = ReadSysFile("status")?.Trim().ToLowerInvariant();
            return status is "charging" or "full" ? BatteryPowerSource.AC : BatteryPowerSource.Battery;
        }
    }

    public EnergySaverStatus EnergySaverStatus => EnergySaverStatus.Unknown;

    public event EventHandler<BatteryInfoChangedEventArgs>? BatteryInfoChanged;
    public event EventHandler<EnergySaverStatusChangedEventArgs>? EnergySaverStatusChanged;

    private static string? ReadSysFile(string fileName)
    {
        try
        {
            if (!Directory.Exists(PowerSupplyPath)) return null;
            foreach (var dir in Directory.GetDirectories(PowerSupplyPath))
            {
                var typePath = Path.Combine(dir, "type");
                if (File.Exists(typePath) && File.ReadAllText(typePath).Trim() == "Battery")
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
