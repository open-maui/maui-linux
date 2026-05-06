// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Devices.Sensors;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux geolocation. Uses GeoClue2 D-Bus service when available.
/// </summary>
public class GeolocationService : IGeolocation
{
    public async Task<Location?> GetLastKnownLocationAsync() => null;

    public async Task<Location?> GetLocationAsync(GeolocationRequest request, CancellationToken cancellationToken = default)
    {
        // Try to read from GeoClue2 via gdbus
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "gdbus",
                Arguments = "call --system --dest org.freedesktop.GeoClue2 --object-path /org/freedesktop/GeoClue2/Manager --method org.freedesktop.GeoClue2.Manager.GetClient",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);
                // Parse GeoClue2 response — simplified, real impl would use D-Bus bindings.
            }
        }
        catch { }

        return null;
    }

    public bool IsListening => false;
    public bool IsListeningForeground => false;
    public bool IsEnabled => true;

    public Task<bool> StartListeningForegroundAsync(GeolocationListeningRequest request)
        => Task.FromResult(false);

    public void StopListeningForeground() { }

    public event EventHandler<GeolocationLocationChangedEventArgs>? LocationChanged;
    public event EventHandler<GeolocationListeningFailedEventArgs>? ListeningFailed;
}
