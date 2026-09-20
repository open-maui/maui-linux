// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux media picker. Uses the zenity file dialog with image/video filters.
/// Camera capture is not available.
/// </summary>
public class MediaPickerService : IMediaPicker
{
    internal static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".svg" };
    internal static readonly string[] VideoExtensions = { ".mp4", ".mkv", ".webm", ".mov", ".avi", ".m4v", ".ogv" };

    public bool IsCaptureSupported => false; // No camera capture by default

    public async Task<FileResult?> PickPhotoAsync(MediaPickerOptions? options = null)
        => await PickFileAsync(BuildPickerStartInfo(options, ImageExtensions, "Images", "Select a photo"));

    public async Task<FileResult?> CapturePhotoAsync(MediaPickerOptions? options = null)
        => null; // Camera capture not supported yet

    public async Task<FileResult?> PickVideoAsync(MediaPickerOptions? options = null)
        => await PickFileAsync(BuildPickerStartInfo(options, VideoExtensions, "Videos", "Select a video"));

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

    /// <summary>
    /// zenity file-selection invocation with a single named filter built from
    /// the extension list (the same glob mapping the file picker uses).
    /// </summary>
    internal static ProcessStartInfo BuildPickerStartInfo(MediaPickerOptions? options, IEnumerable<string> extensions, string filterName, string defaultTitle)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "zenity",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("--file-selection");
        psi.ArgumentList.Add($"--title={(string.IsNullOrWhiteSpace(options?.Title) ? defaultTitle : options!.Title)}");
        psi.ArgumentList.Add($"--file-filter={PortalFilePickerService.BuildZenityFilter(filterName, extensions)}");
        return psi;
    }

    private static async Task<FileResult?> PickFileAsync(ProcessStartInfo psi)
    {
        try
        {
            using var process = Process.Start(psi);
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
