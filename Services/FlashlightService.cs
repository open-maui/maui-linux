// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Devices;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux flashlight stub. Reads/writes /sys/class/leds/ on supported devices.
/// </summary>
public class FlashlightService : IFlashlight
{
    private const string LedsPath = "/sys/class/leds";

    public Task<bool> IsSupportedAsync() => Task.FromResult(FindTorch() != null);

    public async Task TurnOnAsync()
    {
        var torchPath = FindTorch();
        if (torchPath != null)
            await File.WriteAllTextAsync(Path.Combine(torchPath, "brightness"), "1");
    }

    public async Task TurnOffAsync()
    {
        var torchPath = FindTorch();
        if (torchPath != null)
            await File.WriteAllTextAsync(Path.Combine(torchPath, "brightness"), "0");
    }

    private static string? FindTorch()
    {
        try
        {
            if (!Directory.Exists(LedsPath)) return null;
            foreach (var dir in Directory.GetDirectories(LedsPath))
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
