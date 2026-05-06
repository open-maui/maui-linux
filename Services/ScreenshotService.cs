// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Media;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux screenshot. Uses gnome-screenshot, grim (Wayland), or import (ImageMagick).
/// </summary>
public class ScreenshotService : IScreenshot
{
    public bool IsCaptureSupported => true;

    public async Task<IScreenshotResult?> CaptureAsync()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"screenshot-{Guid.NewGuid()}.png");
        try
        {
            // Try grim (Wayland) first
            var tool = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
                ? ("grim", tempPath)
                : ("gnome-screenshot", $"-f {tempPath}");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = tool.Item1,
                Arguments = tool.Item2,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode == 0 && File.Exists(tempPath))
                {
                    var bytes = await File.ReadAllBytesAsync(tempPath);
                    return new LinuxScreenshotResult(bytes);
                }
            }
        }
        catch { }
        return null;
    }

    private class LinuxScreenshotResult : IScreenshotResult
    {
        private readonly byte[] _bytes;
        public LinuxScreenshotResult(byte[] bytes) => _bytes = bytes;
        public int Width => 0; // Would need to read PNG header
        public int Height => 0;
        public Task<Stream> OpenReadAsync(ScreenshotFormat format = ScreenshotFormat.Png, int quality = 100)
            => Task.FromResult<Stream>(new MemoryStream(_bytes));
        public Task CopyToAsync(Stream destination, ScreenshotFormat format = ScreenshotFormat.Png, int quality = 100)
            => destination.WriteAsync(_bytes, 0, _bytes.Length);
    }
}
