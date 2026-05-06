// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux media picker. Uses portal file picker with image/video MIME filters.
/// Falls back to zenity for photo selection.
/// </summary>
public class MediaPickerService : IMediaPicker
{
    public bool IsCaptureSupported => false; // No camera capture by default

    public async Task<FileResult?> PickPhotoAsync(MediaPickerOptions? options = null)
        => await PickFileAsync("image/*");

    public async Task<FileResult?> CapturePhotoAsync(MediaPickerOptions? options = null)
        => null; // Camera capture not supported yet

    public async Task<FileResult?> PickVideoAsync(MediaPickerOptions? options = null)
        => await PickFileAsync("video/*");

    public async Task<FileResult?> CaptureVideoAsync(MediaPickerOptions? options = null)
        => null; // Video capture not supported yet

    public async Task<List<FileResult>> PickPhotosAsync(MediaPickerOptions? options = null)
    {
        var result = await PickPhotoAsync(options);
        return result != null ? [result] : [];
    }

    public async Task<List<FileResult>> PickVideosAsync(MediaPickerOptions? options = null)
    {
        var result = await PickVideoAsync(options);
        return result != null ? [result] : [];
    }

    private async Task<FileResult?> PickFileAsync(string mimeFilter)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "zenity",
                Arguments = $"--file-selection --file-filter=\"{mimeFilter}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return null;
            var path = (await process.StandardOutput.ReadToEndAsync()).Trim();
            await process.WaitForExitAsync();
            return process.ExitCode == 0 && File.Exists(path) ? new FileResult(path) : null;
        }
        catch
        {
            return null;
        }
    }
}
