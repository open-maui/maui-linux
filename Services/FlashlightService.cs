// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux flashlight stub. Reads/writes /sys/class/leds/ on supported devices.
/// </summary>
public class FlashlightService : IFlashlight
{
    private static string LedsPath => Sysfs.PathOf("class", "leds");

    public Task<bool> IsSupportedAsync() => Task.FromResult(FindTorch() != null);

    public async Task TurnOnAsync()
    {
        var torchPath = FindTorch();
        if (torchPath == null)
            throw new FeatureNotSupportedException("No torch LED found under /sys/class/leds.");
        await File.WriteAllTextAsync(Path.Combine(torchPath, "brightness"), "1");
    }

    public async Task TurnOffAsync()
    {
        var torchPath = FindTorch();
        if (torchPath == null)
            throw new FeatureNotSupportedException("No torch LED found under /sys/class/leds.");
        await File.WriteAllTextAsync(Path.Combine(torchPath, "brightness"), "0");
    }

    /// <summary>
    /// First LED directory whose name mentions "torch" or "flash", in ordinal
    /// order so the choice is stable across runs.
    /// </summary>
    internal static string? FindTorch()
    {
        try
        {
            var root = LedsPath;
            if (!Directory.Exists(root)) return null;
            foreach (var dir in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.Ordinal))
            {
                var name = Path.GetFileName(dir).ToLowerInvariant();
                if (name.Contains("torch") || name.Contains("flash"))
                    return dir;
            }
        }
        catch { }
        return null;
    }
}
