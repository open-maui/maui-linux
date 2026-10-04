// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux vibration. Uses same mechanism as HapticFeedback on supported devices.
/// </summary>
public class VibrationService : IVibration
{
    internal static string VibratorPath => Sysfs.PathOf("class", "leds", "vibrator");

    public bool IsSupported => File.Exists(Path.Combine(VibratorPath, "trigger"));

    /// <summary>Vibrates for 500 ms. Throws <see cref="FeatureNotSupportedException"/> without a vibration motor, as on every platform.</summary>
    public void Vibrate()
    {
        EnsureSupported();
        Start(TimeSpan.FromMilliseconds(500));
    }

    /// <summary>
    /// Vibrates for <paramref name="duration"/>, clamped to 0..5 seconds as MAUI does.
    /// Throws <see cref="FeatureNotSupportedException"/> without a vibration motor.
    /// </summary>
    public void Vibrate(TimeSpan duration)
    {
        EnsureSupported();
        Start(Clamp(duration));
    }

    /// <summary>MAUI's VibrationImplementation: negative becomes zero, more than 5 s becomes 5 s.</summary>
    internal static TimeSpan Clamp(TimeSpan duration)
    {
        if (duration.TotalMilliseconds < 0)
            return TimeSpan.Zero;
        if (duration.TotalSeconds > 5)
            return TimeSpan.FromSeconds(5);
        return duration;
    }

    private void EnsureSupported()
    {
        if (!IsSupported)
            throw new FeatureNotSupportedException();
    }

    private static void Start(TimeSpan duration)
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

    /// <summary>Stops a vibration. Throws <see cref="FeatureNotSupportedException"/> without a vibration motor.</summary>
    public void Cancel()
    {
        EnsureSupported();
        try
        {
            var activatePath = Path.Combine(VibratorPath, "activate");
            if (File.Exists(activatePath))
                File.WriteAllText(activatePath, "0");
        }
        catch { }
    }
}
