// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Tmds.DBus;

namespace Microsoft.Maui.Platform.Linux.Services.Portal;

/// <summary>
/// The org.freedesktop.Notifications server (not a portal), reached over the
/// shared session-bus connection. Replaces the notify-send / gdbus /
/// dbus-monitor subprocesses for unsandboxed apps.
/// </summary>
internal interface INotificationServer
{
    /// <summary>Notify; returns the server-assigned id. Throws <see cref="PortalUnavailableException"/> without a server.</summary>
    Task<uint> NotifyAsync(string appName, uint replacesId, string appIcon, string summary, string body, string[] actions, IDictionary<string, object> hints, int expireTimeout, CancellationToken cancellationToken);

    Task CloseNotificationAsync(uint id, CancellationToken cancellationToken);

    /// <summary>True when a server owns (or can be activated for) org.freedesktop.Notifications.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    /// <summary>ActionInvoked(id, key) and NotificationClosed(id, reason). Handlers run on a thread-pool thread (never the D-Bus reader thread).</summary>
    Task<IDisposable> WatchAsync(Action<uint, string> actionInvoked, Action<uint, uint> closed);
}

/// <summary>Always-unavailable server (tests, no session bus).</summary>
internal sealed class NullNotificationServer : INotificationServer
{
    public static readonly NullNotificationServer Instance = new();

    private NullNotificationServer()
    {
    }

    private static Exception Unavailable() => new PortalUnavailableException("Notification server disabled");

    public Task<uint> NotifyAsync(string appName, uint replacesId, string appIcon, string summary, string body, string[] actions, IDictionary<string, object> hints, int expireTimeout, CancellationToken cancellationToken)
        => Task.FromException<uint>(Unavailable());

    public Task CloseNotificationAsync(uint id, CancellationToken cancellationToken) => Task.FromException(Unavailable());

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<IDisposable> WatchAsync(Action<uint, string> actionInvoked, Action<uint, uint> closed) => Task.FromException<IDisposable>(Unavailable());
}

/// <summary>Tmds.DBus client for org.freedesktop.Notifications.</summary>
internal sealed class FreedesktopNotificationServer : INotificationServer
{
    private const string BusName = "org.freedesktop.Notifications";
    private static readonly ObjectPath ObjectPath = new("/org/freedesktop/Notifications");

    private static INotificationServer? _current;

    /// <summary>The server every NotificationService uses; tests swap in a fake.</summary>
    internal static INotificationServer Current
    {
        get => Volatile.Read(ref _current) ?? Interlocked.CompareExchange(ref _current, new FreedesktopNotificationServer(), null) ?? _current!;
        set => Volatile.Write(ref _current, value);
    }

    private static async Task<IFreedesktopNotificationsProxy> ProxyAsync(CancellationToken cancellationToken)
    {
        var bus = await SessionBus.GetAsync(cancellationToken).ConfigureAwait(false);
        return bus.Connection.CreateProxy<IFreedesktopNotificationsProxy>(BusName, ObjectPath);
    }

    private static PortalUnavailableException Wrap(Exception ex) => ex as PortalUnavailableException
        ?? new PortalUnavailableException(ex is DBusException d ? $"{d.ErrorName}: {d.ErrorMessage}" : ex.Message, ex);

    public async Task<uint> NotifyAsync(string appName, uint replacesId, string appIcon, string summary, string body, string[] actions, IDictionary<string, object> hints, int expireTimeout, CancellationToken cancellationToken)
    {
        try
        {
            var proxy = await ProxyAsync(cancellationToken).ConfigureAwait(false);
            return await proxy.NotifyAsync(appName, replacesId, appIcon, summary, body, actions, hints, expireTimeout).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw Wrap(ex);
        }
    }

    public async Task CloseNotificationAsync(uint id, CancellationToken cancellationToken)
    {
        try
        {
            var proxy = await ProxyAsync(cancellationToken).ConfigureAwait(false);
            await proxy.CloseNotificationAsync(id).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw Wrap(ex);
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var bus = await SessionBus.GetAsync(cancellationToken).ConfigureAwait(false);
            if (await bus.Connection.IsServiceActiveAsync(BusName).WaitAsync(cancellationToken).ConfigureAwait(false))
                return true;
            var activatable = await bus.Connection.ListActivatableServicesAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            return activatable.Contains(BusName);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("NotificationServer", $"Availability check failed: {ex.Message}");
            return false;
        }
    }

    public async Task<IDisposable> WatchAsync(Action<uint, string> actionInvoked, Action<uint, uint> closed)
    {
        try
        {
            var proxy = await ProxyAsync(CancellationToken.None).ConfigureAwait(false);
            var onError = (Action<Exception>)(ex => DiagnosticLog.Debug("NotificationServer", $"Signal watch error: {ex.Message}"));
            var a = await proxy.WatchActionInvokedAsync(s => TmdsDesktopPortal.OffBusThread(() => actionInvoked(s.id, s.actionKey)), onError).ConfigureAwait(false);
            try
            {
                var c = await proxy.WatchNotificationClosedAsync(s => TmdsDesktopPortal.OffBusThread(() => closed(s.id, s.reason)), onError).ConfigureAwait(false);
                return new Both(a, c);
            }
            catch
            {
                a.Dispose();
                throw;
            }
        }
        catch (Exception ex)
        {
            throw Wrap(ex);
        }
    }

    private sealed class Both : IDisposable
    {
        private IDisposable? _a;
        private IDisposable? _b;

        public Both(IDisposable a, IDisposable b)
        {
            _a = a;
            _b = b;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _a, null)?.Dispose();
            Interlocked.Exchange(ref _b, null)?.Dispose();
        }
    }
}
