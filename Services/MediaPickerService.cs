// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using Microsoft.Maui.Platform.Linux.Services.Camera;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux media picker, behaving as MAUI's Windows build does:
/// <list type="bullet">
/// <item>Picking: the zenity file dialog with an image or video filter (the app's file picker when
/// zenity is missing). <c>PickPhotosAsync</c>/<c>PickVideosAsync</c> allow several files unless
/// <c>SelectionLimit</c> is 1; a larger limit is not enforced (as on Windows).</item>
/// <item>Capture: the camera capture dialog (<see cref="SkiaCameraCaptureDialog"/>) over a V4L2
/// webcam or, in a sandbox, the xdg-desktop-portal Camera. <see cref="IsCaptureSupported"/> is
/// true when there is a camera and GStreamer to read it; without, the capture methods throw
/// <see cref="FeatureNotSupportedException"/>, as on Android and iOS. Photos are JPEG, videos MP4
/// (H.264/AAC) or, without those encoders, WebM.</item>
/// <item>Photos, picked or captured, get the <c>RotateImage</c>, <c>MaximumWidth</c>,
/// <c>MaximumHeight</c> and <c>CompressionQuality</c> processing (<see cref="MediaImageProcessor"/>).</item>
/// </list>
/// </summary>
public class MediaPickerService : IMediaPicker
{
    internal static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".svg" };
    internal static readonly string[] VideoExtensions = { ".mp4", ".mkv", ".webm", ".mov", ".avi", ".m4v", ".ogv" };

    private readonly ICameraCapture _camera;
    private readonly Func<MediaPickerOptions?, bool, bool, Task<IReadOnlyList<string>>>? _pickPaths;

    public MediaPickerService()
        : this(LinuxCameraCapture.Instance, null)
    {
    }

    /// <summary>A picker over <paramref name="camera"/> and a path picker (photo?, multiple?) (tests).</summary>
    internal MediaPickerService(ICameraCapture camera, Func<MediaPickerOptions?, bool, bool, Task<IReadOnlyList<string>>>? pickPaths)
    {
        _camera = camera;
        _pickPaths = pickPaths;
    }

    public bool IsCaptureSupported => _camera.IsSupported;

    public Task<FileResult?> PickPhotoAsync(MediaPickerOptions? options = null)
        => PickAsync(options, photo: true);

    public Task<FileResult?> PickVideoAsync(MediaPickerOptions? options = null)
        => PickAsync(options, photo: false);

    public Task<List<FileResult>> PickPhotosAsync(MediaPickerOptions? options = null)
        => PickMultipleAsync(options, photo: true);

    public Task<List<FileResult>> PickVideosAsync(MediaPickerOptions? options = null)
        => PickMultipleAsync(options, photo: false);

    public Task<FileResult?> CapturePhotoAsync(MediaPickerOptions? options = null)
        => CaptureAsync(options, photo: true);

    public Task<FileResult?> CaptureVideoAsync(MediaPickerOptions? options = null)
        => CaptureAsync(options, photo: false);

    private async Task<FileResult?> PickAsync(MediaPickerOptions? options, bool photo)
    {
        var paths = await PickPathsAsync(options, photo, multiple: false).ConfigureAwait(false);
        if (paths.Count == 0)
            return null;
        return await ResultForAsync(paths[0], options, photo).ConfigureAwait(false);
    }

    private async Task<List<FileResult>> PickMultipleAsync(MediaPickerOptions? options, bool photo)
    {
        // Windows: a limit of 1 is the single picker; any other limit is not enforced.
        if (options?.SelectionLimit == 1)
        {
            var single = await PickAsync(options, photo).ConfigureAwait(false);
            return single is null ? [] : [single];
        }

        var paths = await PickPathsAsync(options, photo, multiple: true).ConfigureAwait(false);
        var results = new List<FileResult>(paths.Count);
        foreach (var path in paths)
            results.Add(await ResultForAsync(path, options, photo).ConfigureAwait(false));
        return results;
    }

    private async Task<FileResult?> CaptureAsync(MediaPickerOptions? options, bool photo)
    {
        if (!_camera.IsSupported)
            throw new FeatureNotSupportedException("No camera is available (or GStreamer is not installed).");
        if (!photo && !_camera.IsVideoSupported)
            throw new FeatureNotSupportedException("No video encoder is installed (GStreamer openh264enc or x264enc with mp4mux, or vp8enc with webmmux).");

        var path = await _camera.CaptureAsync(photo, options?.Title).ConfigureAwait(false);
        if (path == null)
            return null;
        return await ResultForAsync(path, options, photo).ConfigureAwait(false);
    }

    /// <summary>
    /// The file result for a picked or captured file: a photo processed as the options ask
    /// (MAUI's Windows order: rotation alone gives the rotated file; resizing or compression
    /// starts again from the original, rotating as part of it).
    /// </summary>
    internal static Task<FileResult> ResultForAsync(string path, MediaPickerOptions? options, bool photo)
    {
        if (!photo)
            return Task.FromResult(new FileResult(path));
        return Task.Run(() =>
        {
            var result = path;
            if (MediaImageProcessor.IsRotationNeeded(options))
            {
                try
                {
                    result = MediaImageProcessor.Rotate(path) ?? path;
                }
                catch (Exception ex)
                {
                    // As on Windows: a failed rotation keeps the original.
                    DiagnosticLog.Warn("MediaPickerService", $"Failed to rotate image: {ex.Message}");
                }
            }

            var quality = options?.CompressionQuality ?? 100;
            if (MediaImageProcessor.IsProcessingNeeded(options?.MaximumWidth, options?.MaximumHeight, quality))
            {
                try
                {
                    var processed = MediaImageProcessor.Process(path, options?.MaximumWidth, options?.MaximumHeight, quality, options?.RotateImage ?? false);
                    if (processed != null)
                        result = processed;
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Warn("MediaPickerService", $"Failed to process image: {ex.Message}");
                }
            }
            return new FileResult(result);
        });
    }

    private Task<IReadOnlyList<string>> PickPathsAsync(MediaPickerOptions? options, bool photo, bool multiple)
        => _pickPaths != null ? _pickPaths(options, photo, multiple) : PickWithDialogAsync(options, photo, multiple);

    private static async Task<IReadOnlyList<string>> PickWithDialogAsync(MediaPickerOptions? options, bool photo, bool multiple)
    {
        var psi = photo
            ? BuildPickerStartInfo(options, ImageExtensions, "Images", multiple ? "Select photos" : "Select a photo", multiple)
            : BuildPickerStartInfo(options, VideoExtensions, "Videos", multiple ? "Select videos" : "Select a video", multiple);
        try
        {
            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = (await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false)).Trim();
                await process.WaitForExitAsync().ConfigureAwait(false);
                return process.ExitCode == 0 ? ParseOutput(output) : Array.Empty<string>();
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No zenity: the app's file picker (kdialog, portal...) with the same types.
        }

        var pickOptions = new PickOptions
        {
            PickerTitle = options?.Title,
            FileTypes = photo ? FilePickerFileType.Images : FilePickerFileType.Videos,
        };
        if (multiple)
            return (await FilePicker.Default.PickMultipleAsync(pickOptions).ConfigureAwait(false) ?? Enumerable.Empty<FileResult>()).Select(f => f.FullPath).ToList();
        var single = await FilePicker.Default.PickAsync(pickOptions).ConfigureAwait(false);
        return single == null ? Array.Empty<string>() : new[] { single.FullPath };
    }

    /// <summary>The existing files zenity printed (separated by '|' for a multiple selection).</summary>
    internal static IReadOnlyList<string> ParseOutput(string output)
        => output.Split(new[] { '|', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(File.Exists)
            .ToList();

    /// <summary>
    /// zenity file-selection invocation with a single named filter built from
    /// the extension list (the same glob mapping the file picker uses).
    /// </summary>
    internal static ProcessStartInfo BuildPickerStartInfo(MediaPickerOptions? options, IEnumerable<string> extensions, string filterName, string defaultTitle, bool multiple = false)
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
        if (multiple)
        {
            psi.ArgumentList.Add("--multiple");
            psi.ArgumentList.Add("--separator=|");
        }
        return psi;
    }
}
