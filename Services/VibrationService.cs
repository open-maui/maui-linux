// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Maui.Devices;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux vibration. Uses same mechanism as HapticFeedback on supported devices.
/// </summary>
public class VibrationService : IVibration
{
    internal static string VibratorPath => Sysfs.PathOf("class", "leds", "vibrator");

    public bool IsSupported => File.Exists(Path.Combine(VibratorPath, "trigger"));

    public void Vibrate() => Vibrate(TimeSpan.FromMilliseconds(500));

    public void Vibrate(TimeSpan duration)
    {
        try
        {
            var durationPath = Path.Combine(VibratorPath, "duration");
            if (File.Exists(durationPath))
            {
                var ms = Math.Max(0, (int)duration.TotalMilliseconds);
                File.WriteAllText(durationPath, ms.ToString(CultureInfo.InvariantCulture));
                File.WriteAllText(Path.Combine(VibratorPath, "activate"), "1");
            }
        }
        catch { }
    }

    public void Cancel()
    {
        try
        {
            var activatePath = Path.Combine(VibratorPath, "activate");
            if (File.Exists(activatePath))
                File.WriteAllText(activatePath, "0");
        }
        catch { }
    }
}
