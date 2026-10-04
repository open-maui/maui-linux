// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services.Camera;

/// <summary>Camera capture as the media picker uses it (tests substitute one).</summary>
internal interface ICameraCapture
{
    /// <summary>A camera and the means to capture from it (GStreamer, a source element).</summary>
    bool IsSupported { get; }

    /// <summary>Also a video encoder and container to record into.</summary>
    bool IsVideoSupported { get; }

    /// <summary>Shows the capture UI; the captured file, or null when the user cancelled.</summary>
    Task<string?> CaptureAsync(bool photo, string? title);
}

/// <summary>
/// The Linux camera capture: GStreamer pipelines (<see cref="CameraPipelines"/>) from a V4L2
/// webcam, or from the xdg-desktop-portal Camera's PipeWire remote in a sandbox, shown in
/// <see cref="SkiaCameraCaptureDialog"/> over the app window.
/// </summary>
internal sealed class LinuxCameraCapture : ICameraCapture
{
    public static readonly LinuxCameraCapture Instance = new();

    private static readonly TimeSpan PortalCacheTime = TimeSpan.FromSeconds(10);
    private readonly object _gate = new();
    private (bool Present, DateTime At)? _portalCamera;
    private CameraVideoProfile? _profile;
    private bool _profileResolved;

    private LinuxCameraCapture()
    {
    }

    private static bool HasPipelineElements =>
        GstCameraNative.EnsureInitialized()
        && GstCameraNative.HasElement("appsink")
        && GstCameraNative.HasElement("videoconvert")
        && GstCameraNative.HasElement("decodebin");

    private bool UseDirectDevice => !CameraDiscovery.UsePortal && GstCameraNative.HasElement("v4l2src") && CameraDiscovery.CaptureDevices().Count > 0;

    public bool IsSupported
    {
        get
        {
            try
            {
                if (!HasPipelineElements)
                    return false;
                if (UseDirectDevice)
                    return true;
                return GstCameraNative.HasElement("pipewiresrc") && PortalHasCamera();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Debug("Camera", $"Capture support check failed: {ex.Message}");
                return false;
            }
        }
    }

    public bool IsVideoSupported => IsSupported && Profile != null;

    private CameraVideoProfile? Profile
    {
        get
        {
            lock (_gate)
            {
                if (!_profileResolved)
                {
                    _profile = CameraPipelines.SelectProfile(GstCameraNative.HasElement);
                    _profileResolved = true;
                }
                return _profile;
            }
        }
    }

    private bool PortalHasCamera()
    {
        lock (_gate)
        {
            if (_portalCamera is { } cached && DateTime.UtcNow - cached.At < PortalCacheTime)
                return cached.Present;
        }
        var present = Task.Run(() => CameraDiscovery.PortalHasCameraAsync()).GetAwaiter().GetResult();
        lock (_gate)
            _portalCamera = (present, DateTime.UtcNow);
        return present;
    }

    public async Task<string?> CaptureAsync(bool photo, string? title)
    {
        SafeHandle? remote = null;
        CameraSource source;
        if (UseDirectDevice)
        {
            source = CameraSource.ForDevice(CameraDiscovery.CaptureDevices()[0]);
        }
        else
        {
            remote = await CameraDiscovery.OpenPortalRemoteAsync(CancellationToken.None).ConfigureAwait(false);
            source = CameraSource.ForPipeWire((int)remote.DangerousGetHandle());
        }

        var controller = new CameraCaptureController(source, photo, FileSystem.Current.CacheDirectory,
            photo ? null : Profile, description => GstCameraSession.Start(description));
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                LinuxDialogService.Show(new SkiaCameraCaptureDialog(controller, title))).ConfigureAwait(false);
            // Opening the camera can take a moment: off the UI thread, behind "Starting the camera".
            await Task.Run(controller.Start).ConfigureAwait(false);
            return await controller.Result.ConfigureAwait(false);
        }
        finally
        {
            // On the UI thread, after any draw of the (now closed) dialog that used its frames.
            MainThread.BeginInvokeOnMainThread(controller.Dispose);
            remote?.Dispose();
        }
    }
}
