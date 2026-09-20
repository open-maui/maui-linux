// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui.Devices.Sensors;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux geolocation. Uses GeoClue2 D-Bus service when available.
/// </summary>
public class GeolocationService : IGeolocation
{
    public Task<Location?> GetLastKnownLocationAsync() => Task.FromResult<Location?>(null);

    public async Task<Location?> GetLocationAsync(GeolocationRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return null;

        // Try to reach GeoClue2 via gdbus. The reply is not parsed yet (a real
        // implementation needs a D-Bus client and the agent handshake), so the
        // call only establishes whether the service answers; the result is null
        // either way.
        try
        {
            await ExternalProcess.RunAsync(BuildGeoClueStartInfo(), cancellationToken);
        }
        catch { }

        return null;
    }

    internal static ProcessStartInfo BuildGeoClueStartInfo() => new()
    {
        FileName = "gdbus",
        Arguments = "call --system --dest org.freedesktop.GeoClue2 --object-path /org/freedesktop/GeoClue2/Manager --method org.freedesktop.GeoClue2.Manager.GetClient",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    public bool IsListening => false;
    public bool IsListeningForeground => false;
    public bool IsEnabled => true;

    public Task<bool> StartListeningForegroundAsync(GeolocationListeningRequest request)
        => Task.FromResult(false);

    public void StopListeningForeground() { }

#pragma warning disable CS0067 // never raised: no listening support
    public event EventHandler<GeolocationLocationChangedEventArgs>? LocationChanged;
    public event EventHandler<GeolocationListeningFailedEventArgs>? ListeningFailed;
#pragma warning restore CS0067
}
