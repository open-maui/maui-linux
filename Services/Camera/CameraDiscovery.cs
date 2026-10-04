// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Platform.Linux.Services.Portal;
using Tmds.DBus;

namespace Microsoft.Maui.Platform.Linux.Services.Camera;

/// <summary>
/// Finds the camera to capture from. An unsandboxed app opens the first V4L2 capture node
/// (<c>/sys/class/video4linux/videoN</c> with index 0: a webcam's metadata node has index 1); a
/// sandboxed one (or OPENMAUI_PORTALS=prefer) asks the xdg-desktop-portal Camera interface,
/// which asks the user, and captures from the PipeWire remote it hands back.
/// </summary>
internal static class CameraDiscovery
{
    /// <summary>The V4L2 class directory; replaceable by tests.</summary>
    internal static string Video4LinuxRoot { get; set; } = "/sys/class/video4linux";

    /// <summary>The V4L2 capture devices (/dev/videoN), first first.</summary>
    internal static IReadOnlyList<string> CaptureDevices()
    {
        var result = new List<(int Number, string Device)>();
        try
        {
            if (!Directory.Exists(Video4LinuxRoot))
                return Array.Empty<string>();
            foreach (var dir in Directory.GetFileSystemEntries(Video4LinuxRoot, "video*"))
            {
                var name = Path.GetFileName(dir);
                if (!int.TryParse(name.AsSpan(5), out var number))
                    continue;
                var index = ReadText(Path.Combine(dir, "index"));
                if (index != null && index != "0")
                    continue;
                result.Add((number, "/dev/" + name));
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return result.OrderBy(r => r.Number).Select(r => r.Device).ToList();
    }

    /// <summary>Whether the portal is the way to the camera (sandboxed, or portals preferred).</summary>
    internal static bool UsePortal => DesktopPortal.ShouldTry(PortalUse.SandboxedOrPreferred);

    /// <summary>The portal's IsCameraPresent, false when there is no Camera portal.</summary>
    internal static async Task<bool> PortalHasCameraAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var bus = await SessionBus.GetAsync(cancellationToken).ConfigureAwait(false);
            var proxy = bus.Connection.CreateProxy<ICameraPortalProxy>(TmdsDesktopPortal.BusName, TmdsDesktopPortal.DesktopPath);
            var value = await proxy.GetAsync("IsCameraPresent").WaitAsync(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
            return value is bool present && present;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("Camera", $"Camera portal unavailable: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Camera.AccessCamera (the user is asked, once per app) and Camera.OpenPipeWireRemote. The
    /// returned handle owns the remote's descriptor; close it after the pipeline. Throws
    /// <see cref="PermissionException"/> when the user refuses.
    /// </summary>
    internal static async Task<SafeHandle> OpenPortalRemoteAsync(CancellationToken cancellationToken)
    {
        var response = await TmdsDesktopPortal.RunRequestAsync(
            (bus, options) => bus.Connection.CreateProxy<ICameraPortalProxy>(TmdsDesktopPortal.BusName, TmdsDesktopPortal.DesktopPath).AccessCameraAsync(options),
            new Dictionary<string, object>(),
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess)
            throw new PermissionException("Access to the camera was denied.");

        var bus = await SessionBus.GetAsync(cancellationToken).ConfigureAwait(false);
        var proxy = bus.Connection.CreateProxy<ICameraPortalProxy>(TmdsDesktopPortal.BusName, TmdsDesktopPortal.DesktopPath);
        return await proxy.OpenPipeWireRemoteAsync(new Dictionary<string, object>()).ConfigureAwait(false);
    }

    private static string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
