// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Tmds.DBus;

namespace Microsoft.Maui.Platform.Linux.Services.Portal;

/// <summary>
/// <see cref="IDesktopPortal"/> over native D-Bus (Tmds.DBus) on the shared
/// <see cref="SessionBus"/> connection.
/// </summary>
internal sealed class TmdsDesktopPortal : IDesktopPortal
{
    public const string BusName = "org.freedesktop.portal.Desktop";
    public static readonly ObjectPath DesktopPath = new("/org/freedesktop/portal/desktop");

    private static readonly Lazy<TmdsDesktopPortal> _shared = new(() => new TmdsDesktopPortal());

    public static TmdsDesktopPortal Shared => _shared.Value;

    private readonly ConcurrentDictionary<string, uint> _versions = new(StringComparer.Ordinal);

    /// <summary>Upper bound for a version query (covers D-Bus activation of the portal).</summary>
    internal static TimeSpan VersionTimeout { get; set; } = TimeSpan.FromSeconds(5);

    private TmdsDesktopPortal()
    {
    }

    internal void ForgetVersions() => _versions.Clear();

    private static async Task<SessionBusConnection> BusAsync(CancellationToken cancellationToken)
        => await SessionBus.GetAsync(cancellationToken).ConfigureAwait(false);

    private static T Proxy<T>(SessionBusConnection bus) where T : IDBusObject
        => bus.Connection.CreateProxy<T>(BusName, DesktopPath);

    #region Version

    public async Task<uint> GetVersionAsync(string portalInterface, CancellationToken cancellationToken = default)
    {
        if (_versions.TryGetValue(portalInterface, out var cached))
            return cached;

        try
        {
            var bus = await BusAsync(cancellationToken).ConfigureAwait(false);
            var get = VersionGetter(bus, portalInterface);
            if (get == null)
                return 0;

            var value = await get().WaitAsync(VersionTimeout, cancellationToken).ConfigureAwait(false);
            var version = PortalVariant.ToUInt32(value) ?? 0;
            _versions[portalInterface] = version;
            return version;
        }
        catch (DBusException ex)
        {
            // ServiceUnknown (no portal frontend), InvalidArgs "No such
            // interface", UnknownInterface...: all a definitive "not here".
            DiagnosticLog.Debug("DesktopPortal", $"{portalInterface} unavailable: {ex.ErrorName}");
            _versions[portalInterface] = 0;
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Timeout, no bus, proxy emission failure: not cached, a later call may succeed.
            DiagnosticLog.Debug("DesktopPortal", $"{portalInterface} version query failed: {ex.Message}");
            return 0;
        }
    }

    private static Func<Task<object>>? VersionGetter(SessionBusConnection bus, string portalInterface) => portalInterface switch
    {
        PortalInterfaces.FileChooser => () => Proxy<IFileChooserProxy>(bus).GetAsync("version"),
        PortalInterfaces.OpenUri => () => Proxy<IOpenUriProxy>(bus).GetAsync("version"),
        PortalInterfaces.Notification => () => Proxy<INotificationPortalProxy>(bus).GetAsync("version"),
        PortalInterfaces.Screenshot => () => Proxy<IScreenshotProxy>(bus).GetAsync("version"),
        PortalInterfaces.Secret => () => Proxy<ISecretProxy>(bus).GetAsync("version"),
        PortalInterfaces.Settings => () => Proxy<ISettingsProxy>(bus).GetAsync("version"),
        PortalInterfaces.Inhibit => () => Proxy<IInhibitProxy>(bus).GetAsync("version"),
        PortalInterfaces.Background => () => Proxy<IBackgroundProxy>(bus).GetAsync("version"),
        PortalInterfaces.Location => () => Proxy<ILocationProxy>(bus).GetAsync("version"),
        _ => null,
    };

    #endregion

    #region Request / Response

    /// <summary>
    /// The Request protocol: pick a handle_token, subscribe to Response on the
    /// predicted request path, then call the method (so a fast reply cannot be
    /// missed). If the portal hands back a different path (frontends older
    /// than 0.9 ignore handle_token) the subscription moves to it. Cancelling
    /// closes the request on the portal side.
    /// </summary>
    internal static async Task<PortalResponse> RunRequestAsync(
        Func<SessionBusConnection, IDictionary<string, object>, Task<ObjectPath>> invoke,
        IDictionary<string, object>? options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bus = await BusAsync(cancellationToken).ConfigureAwait(false);

        var token = PortalRequestPath.NewToken();
        var predicted = new ObjectPath(PortalRequestPath.ForRequest(bus.UniqueName, token));
        var response = new TaskCompletionSource<PortalResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnResponse((uint response, IDictionary<string, object> results) signal)
            => response.TrySetResult(PortalResponse.FromSignal(signal.response, signal.results));
        void OnError(Exception ex)
            => response.TrySetException(new PortalUnavailableException($"Request signal watch failed: {ex.Message}", ex));

        var request = bus.Connection.CreateProxy<IPortalRequestProxy>(BusName, predicted);
        IDisposable watch;
        try
        {
            watch = await request.WatchResponseAsync(OnResponse, OnError).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new PortalUnavailableException($"Cannot subscribe to the request: {ex.Message}", ex);
        }

        try
        {
            ObjectPath handle;
            try
            {
                handle = await invoke(bus, PortalOptions.WithHandleToken(options, token)).ConfigureAwait(false);
            }
            catch (DBusException ex)
            {
                throw new PortalUnavailableException($"{ex.ErrorName}: {ex.ErrorMessage}", ex);
            }

            if (handle != predicted)
            {
                watch.Dispose();
                request = bus.Connection.CreateProxy<IPortalRequestProxy>(BusName, handle);
                watch = await request.WatchResponseAsync(OnResponse, OnError).ConfigureAwait(false);
            }

            var closeTarget = request;
            using (cancellationToken.Register(() =>
            {
                if (response.TrySetCanceled(cancellationToken))
                    _ = CloseQuietlyAsync(closeTarget);
            }))
            {
                return await response.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            watch.Dispose();
        }
    }

    private static async Task CloseQuietlyAsync(IPortalRequestProxy request)
    {
        try
        {
            await request.CloseAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("DesktopPortal", $"Request.Close failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs a signal handler on the thread pool. Tmds.DBus invokes handlers
    /// (and completes call tasks) on its connection reader thread; a handler
    /// that blocked there, for example by waiting synchronously on another
    /// portal call, would stall every D-Bus reply until its timeout (call
    /// replies are covered by the connection's RunContinuationsAsynchronously).
    /// </summary>
    internal static void OffBusThread(Action action)
        => ThreadPool.UnsafeQueueUserWorkItem(static a =>
        {
            try
            {
                a();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("DesktopPortal", $"Signal handler failed: {ex.Message}", ex);
            }
        }, action, preferLocal: false);

    private static PortalUnavailableException Wrap(Exception ex) => ex as PortalUnavailableException
        ?? new PortalUnavailableException(ex is DBusException d ? $"{d.ErrorName}: {d.ErrorMessage}" : ex.Message, ex);

    #endregion

    #region Request-based methods

    public Task<PortalResponse> FileChooserOpenFileAsync(string parentWindow, string title, IDictionary<string, object> options, CancellationToken cancellationToken)
        => RunRequestAsync((bus, o) => Proxy<IFileChooserProxy>(bus).OpenFileAsync(parentWindow, title, o), options, cancellationToken);

    public Task<PortalResponse> FileChooserSaveFileAsync(string parentWindow, string title, IDictionary<string, object> options, CancellationToken cancellationToken)
        => RunRequestAsync((bus, o) => Proxy<IFileChooserProxy>(bus).SaveFileAsync(parentWindow, title, o), options, cancellationToken);

    public Task<PortalResponse> OpenUriAsync(string parentWindow, string uri, IDictionary<string, object> options, CancellationToken cancellationToken)
        => RunRequestAsync((bus, o) => Proxy<IOpenUriProxy>(bus).OpenURIAsync(parentWindow, uri, o), options, cancellationToken);

    public Task<PortalResponse> OpenFileAsync(string parentWindow, SafeHandle file, IDictionary<string, object> options, CancellationToken cancellationToken)
        => WithFd(file, fd => RunRequestAsync((bus, o) => Proxy<IOpenUriProxy>(bus).OpenFileAsync(parentWindow, fd, o), options, cancellationToken));

    public Task<PortalResponse> OpenDirectoryAsync(string parentWindow, SafeHandle file, IDictionary<string, object> options, CancellationToken cancellationToken)
        => WithFd(file, fd => RunRequestAsync((bus, o) => Proxy<IOpenUriProxy>(bus).OpenDirectoryAsync(parentWindow, fd, o), options, cancellationToken));

    public Task<PortalResponse> ScreenshotAsync(string parentWindow, IDictionary<string, object> options, CancellationToken cancellationToken)
        => RunRequestAsync((bus, o) => Proxy<IScreenshotProxy>(bus).ScreenshotAsync(parentWindow, o), options, cancellationToken);

    public Task<PortalResponse> RetrieveSecretAsync(SafeHandle writeEnd, IDictionary<string, object> options, CancellationToken cancellationToken)
        => WithFd(writeEnd, fd => RunRequestAsync((bus, o) => Proxy<ISecretProxy>(bus).RetrieveSecretAsync(fd, o), options, cancellationToken));

    public Task<PortalResponse> RequestBackgroundAsync(string parentWindow, IDictionary<string, object> options, CancellationToken cancellationToken)
        => RunRequestAsync((bus, o) => Proxy<IBackgroundProxy>(bus).RequestBackgroundAsync(parentWindow, o), options, cancellationToken);

    /// <summary>
    /// Tmds.DBus 0.x serialises only its own CloseSafeHandle for 'h'. The
    /// caller's handle stays the owner (AddRef keeps it open for the call;
    /// the kernel duplicates the descriptor when the message is sent).
    /// </summary>
    private static async Task<PortalResponse> WithFd(SafeHandle handle, Func<CloseSafeHandle, Task<PortalResponse>> call)
    {
        var added = false;
        handle.DangerousAddRef(ref added);
        try
        {
            using var fd = new CloseSafeHandle(handle.DangerousGetHandle(), ownsHandle: false);
            return await call(fd).ConfigureAwait(false);
        }
        finally
        {
            if (added)
                handle.DangerousRelease();
        }
    }

    #endregion

    #region Inhibit

    public async Task<IAsyncDisposable> InhibitAsync(string parentWindow, PortalInhibitFlags flags, IDictionary<string, object> options, CancellationToken cancellationToken)
    {
        try
        {
            var bus = await BusAsync(cancellationToken).ConfigureAwait(false);
            var token = PortalRequestPath.NewToken("inhibit");
            // The inhibition lives as long as the Request object; no Response
            // is emitted for Inhibit, so there is nothing to wait for.
            var handle = await Proxy<IInhibitProxy>(bus)
                .InhibitAsync(parentWindow, (uint)flags, PortalOptions.WithHandleToken(options, token))
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            return new InhibitHandle(bus.Connection.CreateProxy<IPortalRequestProxy>(BusName, handle), handle.ToString());
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

    /// <summary>Closes the Inhibit request once.</summary>
    internal sealed class InhibitHandle : IAsyncDisposable
    {
        private IPortalRequestProxy? _request;

        public InhibitHandle(IPortalRequestProxy request, string path)
        {
            _request = request;
            Path = path;
        }

        public string Path { get; }

        public async ValueTask DisposeAsync()
        {
            var request = Interlocked.Exchange(ref _request, null);
            if (request != null)
                await CloseQuietlyAsync(request).ConfigureAwait(false);
        }
    }

    #endregion

    #region Settings

    public async Task<object?> ReadSettingAsync(string @namespace, string key, CancellationToken cancellationToken)
    {
        var version = await GetVersionAsync(PortalInterfaces.Settings, cancellationToken).ConfigureAwait(false);
        if (version == 0)
            throw new PortalUnavailableException("Settings portal unavailable");

        try
        {
            var bus = await BusAsync(cancellationToken).ConfigureAwait(false);
            var settings = Proxy<ISettingsProxy>(bus);
            var value = version >= 2
                ? await settings.ReadOneAsync(@namespace, key).WaitAsync(cancellationToken).ConfigureAwait(false)
                : await settings.ReadAsync(@namespace, key).WaitAsync(cancellationToken).ConfigureAwait(false);
            return value;
        }
        catch (DBusException ex) when (ex.ErrorName == "org.freedesktop.portal.Error.NotFound")
        {
            return null;
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

    public async Task<IDisposable> WatchSettingChangedAsync(Action<string, string, object> handler)
    {
        try
        {
            var bus = await BusAsync(CancellationToken.None).ConfigureAwait(false);
            return await Proxy<ISettingsProxy>(bus).WatchSettingChangedAsync(
                s => OffBusThread(() => handler(s.@namespace, s.key, s.value)),
                ex => DiagnosticLog.Debug("DesktopPortal", $"SettingChanged watch error: {ex.Message}")).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw Wrap(ex);
        }
    }

    #endregion

    #region Notification

    public async Task AddNotificationAsync(string id, IDictionary<string, object> notification, CancellationToken cancellationToken)
    {
        try
        {
            var bus = await BusAsync(cancellationToken).ConfigureAwait(false);
            await Proxy<INotificationPortalProxy>(bus).AddNotificationAsync(id, notification).WaitAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task RemoveNotificationAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var bus = await BusAsync(cancellationToken).ConfigureAwait(false);
            await Proxy<INotificationPortalProxy>(bus).RemoveNotificationAsync(id).WaitAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task<IDisposable> WatchNotificationActionInvokedAsync(Action<string, string> handler)
    {
        try
        {
            var bus = await BusAsync(CancellationToken.None).ConfigureAwait(false);
            return await Proxy<INotificationPortalProxy>(bus).WatchActionInvokedAsync(
                s => OffBusThread(() => handler(s.id, s.action)),
                ex => DiagnosticLog.Debug("DesktopPortal", $"ActionInvoked watch error: {ex.Message}")).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw Wrap(ex);
        }
    }

    #endregion

    #region Location

    public async Task<IReadOnlyDictionary<string, object>?> GetLocationAsync(IDictionary<string, object> sessionOptions, CancellationToken cancellationToken)
    {
        SessionBusConnection bus;
        try
        {
            bus = await BusAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw Wrap(ex);
        }

        var location = Proxy<ILocationProxy>(bus);
        var sessionToken = sessionOptions.TryGetValue("session_handle_token", out var t) && t is string s
            ? s
            : PortalRequestPath.NewToken("location");
        var options = new Dictionary<string, object>(sessionOptions, StringComparer.Ordinal)
        {
            ["session_handle_token"] = sessionToken,
        };

        var expectedSessions = new HashSet<string>(StringComparer.Ordinal)
        {
            PortalRequestPath.ForSession(bus.UniqueName, sessionToken),
        };
        var fix = new TaskCompletionSource<IReadOnlyDictionary<string, object>?>(TaskCreationOptions.RunContinuationsAsynchronously);

        IDisposable watch;
        try
        {
            // Subscribe before the session exists so the first update is not lost.
            watch = await location.WatchLocationUpdatedAsync(update =>
            {
                bool match;
                lock (expectedSessions)
                    match = expectedSessions.Contains(update.sessionHandle.ToString());
                if (match)
                    fix.TrySetResult(new Dictionary<string, object>(update.location, StringComparer.Ordinal));
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw Wrap(ex);
        }

        ObjectPath? session = null;
        try
        {
            try
            {
                session = await location.CreateSessionAsync(options).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DBusException ex)
            {
                throw Wrap(ex);
            }

            lock (expectedSessions)
                expectedSessions.Add(session.Value.ToString());

            var sessionPath = session.Value;
            var started = await RunRequestAsync(
                (b, o) => b.Connection.CreateProxy<ILocationProxy>(BusName, DesktopPath).StartAsync(sessionPath, "", o),
                null,
                cancellationToken).ConfigureAwait(false);
            if (!started.IsSuccess)
                return null;

            return await fix.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            watch.Dispose();
            if (session is { } path)
            {
                try
                {
                    await bus.Connection.CreateProxy<IPortalSessionProxy>(BusName, path).CloseAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Debug("DesktopPortal", $"Location session close failed: {ex.Message}");
                }
            }
        }
    }

    #endregion
}
