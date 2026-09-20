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
    public bool IsSupported => File.Exists(Path.Combine(VibrationService.VibratorPath, "trigger"));

    public void Perform(HapticFeedbackType type)
    {
        try
        {
            if (IsSupported)
            {
                File.WriteAllText(Path.Combine(VibrationService.VibratorPath, "duration"), DurationFor(type));
                File.WriteAllText(Path.Combine(VibrationService.VibratorPath, "activate"), "1");
            }
        }
        catch { }
    }

    /// <summary>Pulse length in milliseconds written to the vibrator node.</summary>
    internal static string DurationFor(HapticFeedbackType type)
        => type == HapticFeedbackType.LongPress ? "200" : "50";
}
