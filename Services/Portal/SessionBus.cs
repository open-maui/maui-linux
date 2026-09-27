// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Tmds.DBus;

namespace Microsoft.Maui.Platform.Linux.Services.Portal;

/// <summary>A connected session bus and this process's unique name on it.</summary>
internal sealed record SessionBusConnection(Connection Connection, string UniqueName);

/// <summary>
/// The one session-bus connection the portal layer (and the notification
/// server client) share. Created lazily on first use; if the bus drops, the
/// next caller reconnects.
/// </summary>
internal static class SessionBus
{
    private static readonly Lock _gate = new();
    private static Task<SessionBusConnection>? _connecting;

    /// <summary>How long connecting to the bus may take before callers give up.</summary>
    internal static TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The shared connection. Throws <see cref="PortalUnavailableException"/>
    /// when there is no session bus or it cannot be reached.
    /// </summary>
    public static async Task<SessionBusConnection> GetAsync(CancellationToken cancellationToken = default)
    {
        Task<SessionBusConnection> task;
        lock (_gate)
        {
            if (_connecting == null || _connecting.IsFaulted || _connecting.IsCanceled)
                _connecting = ConnectAsync();
            task = _connecting;
        }

        try
        {
            return await task.WaitAsync(ConnectTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            throw new PortalUnavailableException("Timed out connecting to the session bus", ex);
        }
    }

    private static async Task<SessionBusConnection> ConnectAsync()
    {
        var address = Address.Session;
        if (string.IsNullOrEmpty(address))
            throw new PortalUnavailableException("No session bus address (DBUS_SESSION_BUS_ADDRESS unset)");

        // Tmds.DBus completes call tasks on its receive loop; by default the
        // awaiting code then runs inline there, so any caller that later
        // blocked (sync-over-async with a timeout, as the theme and HiDPI
        // services do at startup) would stall every D-Bus reply. Asynchronous
        // continuations keep the receive loop free.
        var connection = new Connection(new ClientConnectionOptions(address)
        {
            RunContinuationsAsynchronously = true,
        });
        try
        {
            var info = await connection.ConnectAsync().ConfigureAwait(false);
            connection.StateChanged += OnStateChanged;
            DiagnosticLog.Debug("SessionBus", $"Connected as {info.LocalName}");
            return new SessionBusConnection(connection, info.LocalName);
        }
        catch (Exception ex) when (ex is not PortalUnavailableException)
        {
            connection.Dispose();
            throw new PortalUnavailableException($"Cannot connect to the session bus: {ex.Message}", ex);
        }
    }

    private static void OnStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        if (e.State is ConnectionState.Disconnected)
        {
            DiagnosticLog.Debug("SessionBus", $"Session bus connection {e.State}; next use reconnects");
            lock (_gate)
                _connecting = null;
            TmdsDesktopPortal.Shared.ForgetVersions();
        }
    }
}
