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
    private Location? _lastKnown;

    public GeolocationService()
        : this(DesktopPortal.Current)
    {
    }

    internal GeolocationService(IDesktopPortal portal)
    {
        _portal = portal;
    }

    /// <summary>Always uses <paramref name="portal"/>, whatever OPENMAUI_PORTALS says (tests).</summary>
    internal GeolocationService(IDesktopPortal portal, bool alwaysUsePortal)
        : this(portal)
    {
        _alwaysUsePortal = alwaysUsePortal;
    }

    private readonly bool _alwaysUsePortal;

    private bool UsePortal => _alwaysUsePortal || DesktopPortal.ShouldTry(PortalUse.Always);

    public Task<Location?> GetLastKnownLocationAsync() => Task.FromResult(Volatile.Read(ref _lastKnown));

    public async Task<Location?> GetLocationAsync(GeolocationRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return null;

        if (!UsePortal)
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

    private readonly object _listenGate = new();
    private IAsyncDisposable? _listening;
    private bool _starting;

    public bool IsListening => IsListeningForeground;

    public bool IsListeningForeground
    {
        get { lock (_listenGate) return _listening != null || _starting; }
    }

    public bool IsEnabled => true;

    /// <summary>
    /// Listens through a long-lived portal Location session: every fix raises
    /// <see cref="LocationChanged"/> (on the UI thread) and becomes the last known location,
    /// as the other platforms' listeners do. False when there is no portal or the user or
    /// system denies location.
    /// </summary>
    public async Task<bool> StartListeningForegroundAsync(GeolocationListeningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_listenGate)
        {
            if (_listening != null || _starting)
                throw new InvalidOperationException("Already listening to location changes.");
            _starting = true;
        }

        IAsyncDisposable? watch = null;
        try
        {
            if (UsePortal)
            {
                var seconds = (uint)Math.Max(0, Math.Round(request.MinimumTime.TotalSeconds));
                var options = PortalOptions.LocationSession(PortalLocationFix.AccuracyFor(request.DesiredAccuracy), PortalRequestPath.NewToken("location"), seconds);
                watch = await _portal.WatchLocationAsync(options, OnPortalUpdate, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("GeolocationService", $"Location portal failed: {ex.Message}");
        }

        lock (_listenGate)
        {
            _starting = false;
            _listening = watch;
        }
        return watch != null;
    }

    public void StopListeningForeground()
    {
        IAsyncDisposable? watch;
        lock (_listenGate)
        {
            watch = _listening;
            _listening = null;
        }
        if (watch != null)
            _ = CloseAsync(watch);
    }

    private static async Task CloseAsync(IAsyncDisposable watch)
    {
        try
        {
            await watch.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("GeolocationService", $"Closing the location session failed: {ex.Message}");
        }
    }

    private void OnPortalUpdate(IReadOnlyDictionary<string, object> raw)
    {
        lock (_listenGate)
        {
            if (_listening == null && !_starting)
                return; // stopped
        }
        var location = ToLocation(PortalLocationFix.Parse(raw));
        if (location == null)
            return;
        Volatile.Write(ref _lastKnown, location);
        OnMainThread(() => LocationChanged?.Invoke(this, new GeolocationLocationChangedEventArgs(location)));
    }

    private static void OnMainThread(Action action)
    {
        var dispatcher = Microsoft.Maui.Platform.Linux.Dispatching.LinuxDispatcher.Main;
        if (dispatcher == null || Microsoft.Maui.Platform.Linux.Dispatching.LinuxDispatcher.IsMainThread)
            action();
        else
            dispatcher.Dispatch(action);
    }

    public event EventHandler<GeolocationLocationChangedEventArgs>? LocationChanged;

#pragma warning disable CS0067 // never raised: a denied session makes StartListeningForegroundAsync return false, and the portal does not report a session ending
    public event EventHandler<GeolocationListeningFailedEventArgs>? ListeningFailed;
#pragma warning restore CS0067
}
