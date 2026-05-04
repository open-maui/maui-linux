// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Devices;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux vibration. Uses same mechanism as HapticFeedback on supported devices.
/// </summary>
public class VibrationService : IVibration
{
    public bool IsSupported => File.Exists("/sys/class/leds/vibrator/trigger");

    public void Vibrate() => Vibrate(TimeSpan.FromMilliseconds(500));

    public void Vibrate(TimeSpan duration)
    {
        try
        {
            if (File.Exists("/sys/class/leds/vibrator/duration"))
            {
                File.WriteAllText("/sys/class/leds/vibrator/duration", ((int)duration.TotalMilliseconds).ToString());
                File.WriteAllText("/sys/class/leds/vibrator/activate", "1");
            }
        }
        catch { }
    }

    public void Cancel()
    {
        try
        {
            if (File.Exists("/sys/class/leds/vibrator/activate"))
                File.WriteAllText("/sys/class/leds/vibrator/activate", "0");
        }
        catch { }
    }
}
