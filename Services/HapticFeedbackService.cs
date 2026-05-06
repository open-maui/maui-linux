// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Devices;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux haptic feedback. Uses /sys/class/leds/vibrator or input force feedback.
/// Falls back to no-op on desktops.
/// </summary>
public class HapticFeedbackService : IHapticFeedback
{
    public bool IsSupported => File.Exists("/sys/class/leds/vibrator/trigger");

    public void Perform(HapticFeedbackType type)
    {
        try
        {
            if (File.Exists("/sys/class/leds/vibrator/trigger"))
            {
                var duration = type == HapticFeedbackType.LongPress ? "200" : "50";
                File.WriteAllText("/sys/class/leds/vibrator/duration", duration);
                File.WriteAllText("/sys/class/leds/vibrator/activate", "1");
            }
        }
        catch { }
    }
}
