// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Devices;
using Tmds.DBus;

namespace Microsoft.Maui.Platform.Linux.Services;

[DBusInterface("org.freedesktop.UPower.PowerProfiles")]
internal interface IPowerProfilesProxy : IDBusObject
{
    Task<object> GetAsync(string prop);
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}

[DBusInterface("net.hadess.PowerProfiles")]
internal interface ILegacyPowerProfilesProxy : IDBusObject
{
    Task<object> GetAsync(string prop);
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}

/// <summary>
/// The desktop's power profile from power-profiles-daemon (or tuned-ppd) on the system bus:
/// "power-saver" is MAUI's energy saver. Read once and then followed through PropertiesChanged.
/// Without the daemon (or a system bus) the status stays Unknown.
/// </summary>
internal sealed class PowerProfilesMonitor
{
    private static readonly TimeSpan InitialReadTimeout = TimeSpan.FromSeconds(1);

    private readonly object _gate = new();
    private Task? _starting;
    private EnergySaverStatus _status = EnergySaverStatus.Unknown;
    private Connection? _connection;
    private IDisposable? _watch;

    public static PowerProfilesMonitor Shared { get; } = new();

    private readonly bool _useDaemon;

    public PowerProfilesMonitor() : this(useDaemon: true)
    {
    }

    /// <summary>Without the daemon only <see cref="Report"/> changes the status (tests).</summary>
    internal PowerProfilesMonitor(bool useDaemon)
    {
        _useDaemon = useDaemon;
    }

    /// <summary>Raised (on a thread-pool thread) when the status changes.</summary>
    public event Action<EnergySaverStatus>? StatusChanged;

    /// <summary>The current status; the first call waits briefly for the daemon's answer.</summary>
    public EnergySaverStatus Status
    {
        get
        {
            var start = EnsureStarted();
            if (!start.IsCompleted)
            {
                try { start.Wait(InitialReadTimeout); }
                catch (AggregateException) { }
            }
            lock (_gate) return _status;
        }
    }

    internal static EnergySaverStatus FromProfile(string? profile) => profile switch
    {
        null or "" => EnergySaverStatus.Unknown,
        "power-saver" => EnergySaverStatus.On,
        _ => EnergySaverStatus.Off,
    };

    public Task EnsureStarted()
    {
        lock (_gate)
            return _starting ??= _useDaemon ? Task.Run(StartAsync) : Task.CompletedTask;
    }

    private async Task StartAsync()
    {
        try
        {
            var address = Address.System;
            if (string.IsNullOrEmpty(address))
                return;
            var connection = new Connection(new ClientConnectionOptions(address) { RunContinuationsAsynchronously = true });
            await connection.ConnectAsync().ConfigureAwait(false);
            _connection = connection;

            // The freedesktop name first (power-profiles-daemon 0.20+, tuned-ppd), then the older one.
            if (!await TryAsync(connection.CreateProxy<IPowerProfilesProxy>("org.freedesktop.UPower.PowerProfiles", "/org/freedesktop/UPower/PowerProfiles"),
                    p => p.GetAsync("ActiveProfile"), (p, h) => p.WatchPropertiesAsync(h)).ConfigureAwait(false))
            {
                await TryAsync(connection.CreateProxy<ILegacyPowerProfilesProxy>("net.hadess.PowerProfiles", "/net/hadess/PowerProfiles"),
                    p => p.GetAsync("ActiveProfile"), (p, h) => p.WatchPropertiesAsync(h)).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("PowerProfiles", $"Power profiles unavailable: {ex.Message}");
        }
    }

    private async Task<bool> TryAsync<T>(T proxy, Func<T, Task<object>> get, Func<T, Action<PropertyChanges>, Task<IDisposable>> watch)
    {
        try
        {
            var profile = await get(proxy).ConfigureAwait(false) as string;
            Set(FromProfile(profile));
            _watch = await watch(proxy, changes =>
            {
                foreach (var change in changes.Changed)
                    if (change.Key == "ActiveProfile")
                        Set(FromProfile(change.Value as string));
            }).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("PowerProfiles", $"{typeof(T).Name}: {ex.Message}");
            return false;
        }
    }

    private void Set(EnergySaverStatus status)
    {
        lock (_gate)
        {
            if (_status == status)
                return;
            _status = status;
        }
        try
        {
            StatusChanged?.Invoke(status);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("PowerProfiles", "An energy saver handler failed", ex);
        }
    }

    /// <summary>Applies a profile as if the daemon reported it (tests).</summary>
    internal void Report(string? profile) => Set(FromProfile(profile));
}
