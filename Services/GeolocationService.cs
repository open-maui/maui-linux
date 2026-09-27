// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Platform.Linux.Services.Portal;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux geolocation through the xdg-desktop-portal Location interface
/// (native D-Bus; the portal talks to GeoClue and applies the desktop's
/// location permission). Without a portal, or when the user or system denies
/// location, the result is null as before.
/// </summary>
public class GeolocationService : IGeolocation
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly IDesktopPortal _portal;
    private static Location? _lastKnown;

    public GeolocationService()
        : this(DesktopPortal.Current)
    {
    }

    internal GeolocationService(IDesktopPortal portal)
    {
        _portal = portal;
    }

    public Task<Location?> GetLastKnownLocationAsync() => Task.FromResult(Volatile.Read(ref _lastKnown));

    public async Task<Location?> GetLocationAsync(GeolocationRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return null;

        if (!DesktopPortal.ShouldTry(PortalUse.Always))
            return null;

        request ??= new GeolocationRequest();
        try
        {
            var timeout = request.Timeout > TimeSpan.Zero ? request.Timeout : DefaultTimeout;
            var fix = await new PortalGeolocation(_portal)
                .GetFixAsync(PortalLocationFix.AccuracyFor(request.DesiredAccuracy), timeout, cancellationToken)
                .ConfigureAwait(false);
            var location = ToLocation(fix);
            if (location != null)
                Volatile.Write(ref _lastKnown, location);
            return location;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("GeolocationService", $"Location portal failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>MAUI Location for a portal fix (null stays null).</summary>
    internal static Location? ToLocation(PortalLocationFix? fix)
    {
        if (fix == null)
            return null;
        var location = new Location(fix.Latitude, fix.Longitude)
        {
            Altitude = fix.Altitude,
            Accuracy = fix.Accuracy,
            Speed = fix.Speed,
            Course = fix.Heading,
            Timestamp = fix.Timestamp ?? DateTimeOffset.UtcNow,
        };
        return location;
    }

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
