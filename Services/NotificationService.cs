// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Maui.Platform.Linux.Services.Portal;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux notification service. Unsandboxed apps talk to the
/// org.freedesktop.Notifications server over native D-Bus (real server ids,
/// actions and close reasons); sandboxed apps (or OPENMAUI_PORTALS=prefer)
/// use the xdg-desktop-portal Notification interface. notify-send and zenity
/// remain the fallbacks when no bus is reachable.
/// Supports interactive notifications with action callbacks.
/// </summary>
public class NotificationService
{
    private readonly string _appName;
    private readonly string? _defaultIconPath;
    private readonly IDesktopPortal _portal;
    private readonly INotificationServer _server;
    private readonly ConcurrentDictionary<uint, NotificationContext> _activeNotifications = new();
    private readonly ConcurrentDictionary<uint, uint> _serverIdToLocal = new();
    private readonly ConcurrentDictionary<uint, uint> _localToServerId = new();
    private readonly ConcurrentDictionary<string, uint> _portalIdToLocal = new(StringComparer.Ordinal);
    private static uint _notificationIdCounter = 1;
    private readonly SemaphoreSlim _watchGate = new(1, 1);
    private IDisposable? _serverWatch;
    private IDisposable? _portalWatch;
    private bool _monitoringActions;

    /// <summary>
    /// Event raised when a notification action is invoked.
    /// </summary>
    public event EventHandler<NotificationActionEventArgs>? ActionInvoked;

    /// <summary>
    /// Event raised when a notification is closed.
    /// </summary>
    public event EventHandler<NotificationClosedEventArgs>? NotificationClosed;

    public NotificationService(string appName = "MAUI Application", string? defaultIconPath = null)
        : this(appName, defaultIconPath, DesktopPortal.Current, FreedesktopNotificationServer.Current)
    {
    }

    internal NotificationService(string appName, string? defaultIconPath, IDesktopPortal portal, INotificationServer server)
    {
        _appName = appName;
        _defaultIconPath = defaultIconPath;
        _portal = portal;
        _server = server;
    }

    /// <summary>
    /// Starts monitoring for notification action callbacks via D-Bus.
    /// Call this once at application startup if you want to receive action callbacks.
    /// </summary>
    public void StartActionMonitoring()
    {
        if (_monitoringActions) return;
        _monitoringActions = true;

        _ = EnsureWatchingAsync();
    }

    /// <summary>
    /// Stops monitoring for notification action callbacks.
    /// </summary>
    public void StopActionMonitoring()
    {
        _monitoringActions = false;
        try
        {
            Interlocked.Exchange(ref _serverWatch, null)?.Dispose();
            Interlocked.Exchange(ref _portalWatch, null)?.Dispose();
        }
        catch (Exception ex) { DiagnosticLog.Debug("NotificationService", "D-Bus signal watch cleanup failed", ex); }
    }

    private async Task EnsureWatchingAsync()
    {
        await _watchGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_monitoringActions)
                return;

            if (_serverWatch == null)
            {
                try
                {
                    _serverWatch = await _server.WatchAsync(OnServerActionInvoked, OnServerNotificationClosed).ConfigureAwait(false);
                }
                catch (PortalUnavailableException ex)
                {
                    DiagnosticLog.Debug("NotificationService", $"Notification server signals unavailable: {ex.Message}");
                }
            }

            if (_portalWatch == null && DesktopPortal.ShouldTry(PortalUse.SandboxedOrPreferred)
                && await _portal.GetVersionAsync(PortalInterfaces.Notification).ConfigureAwait(false) > 0)
            {
                try
                {
                    _portalWatch = await _portal.WatchNotificationActionInvokedAsync(OnPortalActionInvoked).ConfigureAwait(false);
                }
                catch (PortalUnavailableException ex)
                {
                    DiagnosticLog.Debug("NotificationService", $"Portal ActionInvoked unavailable: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("NotificationService", $"D-Bus signal watch error: {ex.Message}", ex);
        }
        finally
        {
            _watchGate.Release();
        }
    }

    internal void OnServerActionInvoked(uint serverId, string actionKey)
    {
        if (_serverIdToLocal.TryGetValue(serverId, out var localId))
            DispatchAction(localId, actionKey);
    }

    internal void OnServerNotificationClosed(uint serverId, uint reason)
    {
        if (!_serverIdToLocal.TryRemove(serverId, out var localId))
            return;
        _localToServerId.TryRemove(localId, out _);
        _activeNotifications.TryRemove(localId, out var context);
        try
        {
            NotificationClosed?.Invoke(this, new NotificationClosedEventArgs(localId, (NotificationCloseReason)reason, context?.Tag));
        }
        catch (Exception ex) { DiagnosticLog.Debug("NotificationService", "Notification closed processing failed", ex); }
    }

    internal void OnPortalActionInvoked(string portalId, string action)
    {
        if (_portalIdToLocal.TryGetValue(portalId, out var localId))
            DispatchAction(localId, PortalOptions.ActionKeyFromPortal(action));
    }

    private void DispatchAction(uint localId, string actionKey)
    {
        try
        {
            if (_activeNotifications.TryGetValue(localId, out var context))
            {
                // Invoke callback if registered
                if (context.ActionCallbacks?.TryGetValue(actionKey, out var callback) == true)
                {
                    callback?.Invoke();
                }

                ActionInvoked?.Invoke(this, new NotificationActionEventArgs(localId, actionKey, context.Tag));
            }
        }
        catch (Exception ex) { DiagnosticLog.Debug("NotificationService", "Action invoked processing failed", ex); }
    }

    /// <summary>
    /// Shows a simple notification.
    /// </summary>
    public async Task ShowAsync(string title, string message)
    {
        await ShowAsync(new NotificationOptions
        {
            Title = title,
            Message = message
        });
    }

    /// <summary>
    /// Shows a notification with action buttons and callbacks.
    /// </summary>
    /// <param name="title">Notification title.</param>
    /// <param name="message">Notification message.</param>
    /// <param name="actions">List of action buttons with callbacks.</param>
    /// <param name="tag">Optional tag to identify the notification in events.</param>
    /// <returns>The notification ID.</returns>
    public async Task<uint> ShowWithActionsAsync(
        string title,
        string message,
        IEnumerable<NotificationAction> actions,
        string? tag = null)
    {
        var notificationId = NextLocalId();
        var actionList = actions.ToList();

        // Store context for callbacks
        var context = new NotificationContext
        {
            Tag = tag,
            ActionCallbacks = actionList.ToDictionary(a => a.Key, a => a.Callback)
        };
        _activeNotifications[notificationId] = context;

        // Build actions dictionary for options
        var actionDict = actionList.ToDictionary(a => a.Key, a => a.Label);

        // Action callbacks need the signal watch; start it on first use.
        if (actionList.Count > 0 && !_monitoringActions)
            StartActionMonitoring();
        else if (_monitoringActions)
            await EnsureWatchingAsync().ConfigureAwait(false);

        await ShowCoreAsync(new NotificationOptions
        {
            Title = title,
            Message = message,
            Actions = actionDict
        }, notificationId);

        return notificationId;
    }

    private static uint NextLocalId() => Interlocked.Increment(ref _notificationIdCounter) - 1;

    /// <summary>
    /// Cancels/closes an active notification.
    /// </summary>
    public async Task CancelAsync(uint notificationId)
    {
        try
        {
            if (_portalIdToLocal.FirstOrDefault(kv => kv.Value == notificationId) is { Key: { } portalId })
            {
                _portalIdToLocal.TryRemove(portalId, out _);
                await _portal.RemoveNotificationAsync(portalId, CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                // Our own notifications map to the server's id; anything else
                // is taken as a server id the caller already holds.
                var serverId = _localToServerId.TryRemove(notificationId, out var mapped) ? mapped : notificationId;
                _serverIdToLocal.TryRemove(serverId, out _);
                await _server.CloseNotificationAsync(serverId, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex) { DiagnosticLog.Debug("NotificationService", "Notification cancel failed", ex); }
        finally
        {
            _activeNotifications.TryRemove(notificationId, out _);
        }
    }

    /// <summary>
    /// Shows a notification with options.
    /// </summary>
    public async Task ShowAsync(NotificationOptions options)
    {
        await ShowCoreAsync(options, NextLocalId());
    }

    /// <summary>Which transport a notification goes through (see the class remarks).</summary>
    internal enum Transport
    {
        Portal,
        Server,
        NotifySend,
    }

    /// <summary>
    /// Transport order: the portal first only when the policy says so and the
    /// interface exists; then the notification server; notify-send last.
    /// </summary>
    internal static IReadOnlyList<Transport> TransportOrder(bool tryPortal, uint portalNotificationVersion)
        => tryPortal && portalNotificationVersion > 0
            ? new[] { Transport.Portal, Transport.Server, Transport.NotifySend }
            : new[] { Transport.Server, Transport.NotifySend };

    private async Task ShowCoreAsync(NotificationOptions options, uint localId)
    {
        var tryPortal = DesktopPortal.ShouldTry(PortalUse.SandboxedOrPreferred);
        var portalVersion = tryPortal ? await _portal.GetVersionAsync(PortalInterfaces.Notification).ConfigureAwait(false) : 0u;

        foreach (var transport in TransportOrder(tryPortal, portalVersion))
        {
            switch (transport)
            {
                case Transport.Portal:
                    try
                    {
                        var portalId = PortalNotificationId(localId);
                        await _portal.AddNotificationAsync(portalId, PortalOptions.Notification(options, options.Actions), CancellationToken.None).ConfigureAwait(false);
                        _portalIdToLocal[portalId] = localId;
                        return;
                    }
                    catch (PortalUnavailableException ex)
                    {
                        DiagnosticLog.Debug("NotificationService", $"Portal notification failed: {ex.Message}");
                    }
                    break;

                case Transport.Server:
                    try
                    {
                        var icon = options.IconPath ?? _defaultIconPath;
                        if (string.IsNullOrEmpty(icon))
                            icon = options.IconName ?? "";
                        var serverId = await _server.NotifyAsync(
                            _appName,
                            0,
                            icon,
                            options.Title ?? "",
                            options.Message ?? "",
                            PortalOptions.NotifyActions(options.Actions),
                            PortalOptions.NotifyHints(options),
                            options.ExpireTimeMs > 0 ? options.ExpireTimeMs : -1,
                            CancellationToken.None).ConfigureAwait(false);
                        _serverIdToLocal[serverId] = localId;
                        _localToServerId[localId] = serverId;
                        return;
                    }
                    catch (PortalUnavailableException ex)
                    {
                        DiagnosticLog.Debug("NotificationService", $"Notification server unavailable: {ex.Message}");
                    }
                    break;

                case Transport.NotifySend:
                    await ShowWithNotifySendAsync(options).ConfigureAwait(false);
                    return;
            }
        }
    }

    internal string PortalNotificationId(uint localId)
        => $"openmaui-{Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)}-{localId.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    private async Task ShowWithNotifySendAsync(NotificationOptions options)
    {
        try
        {
            var args = BuildNotifyArgs(options);

            var startInfo = new ProcessStartInfo
            {
                FileName = "notify-send",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (await ExternalProcess.RunAsync(startInfo).ConfigureAwait(false) == null)
                await TryZenityNotification(options);
        }
        catch (Exception)
        {
            // Fall back to zenity notification
            await TryZenityNotification(options);
        }
    }

    private string BuildNotifyArgs(NotificationOptions options)
    {
        var args = new List<string>();

        // App name
        args.Add($"--app-name=\"{EscapeArg(_appName)}\"");

        // Urgency
        args.Add($"--urgency={options.Urgency.ToString().ToLower()}");

        // Expire time (milliseconds, 0 = never expire)
        if (options.ExpireTimeMs > 0)
        {
            args.Add($"--expire-time={options.ExpireTimeMs}");
        }

        // Icon
        var icon = options.IconPath ?? _defaultIconPath;
        if (!string.IsNullOrEmpty(icon))
        {
            args.Add($"--icon=\"{EscapeArg(icon)}\"");
        }
        else if (!string.IsNullOrEmpty(options.IconName))
        {
            args.Add($"--icon={options.IconName}");
        }

        // Category
        if (!string.IsNullOrEmpty(options.Category))
        {
            args.Add($"--category={options.Category}");
        }

        // Hint for transient notifications
        if (options.IsTransient)
        {
            args.Add("--hint=int:transient:1");
        }

        // Actions (if supported)
        if (options.Actions?.Count > 0)
        {
            foreach (var action in options.Actions)
            {
                args.Add($"--action=\"{action.Key}={EscapeArg(action.Value)}\"");
            }
        }

        // Title and message
        args.Add($"\"{EscapeArg(options.Title)}\"");
        args.Add($"\"{EscapeArg(options.Message)}\"");

        return string.Join(" ", args);
    }

    private async Task TryZenityNotification(NotificationOptions options)
    {
        try
        {
            var iconArg = "";
            if (!string.IsNullOrEmpty(options.IconPath))
            {
                iconArg = $"--window-icon=\"{options.IconPath}\"";
            }

            var typeArg = options.Urgency == NotificationUrgency.Critical ? "--error" : "--info";

            var startInfo = new ProcessStartInfo
            {
                FileName = "zenity",
                Arguments = $"{typeArg} {iconArg} --title=\"{EscapeArg(options.Title)}\" --text=\"{EscapeArg(options.Message)}\" --timeout=5",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
            }
        }
        catch
        {
            // Silently fail if no notification method available
        }
    }

    /// <summary>
    /// Checks if notifications are available on this system.
    /// </summary>
    public static bool IsAvailable()
    {
        // A notification server on the session bus is enough (native D-Bus);
        // otherwise notify-send must be installed.
        if (PortalSync.TryRun(ct => FreedesktopNotificationServer.Current.IsAvailableAsync(ct), TimeSpan.FromSeconds(2), out var onBus) && onBus)
            return true;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "notify-send",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string EscapeArg(string arg)
    {
        return arg?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
    }
}
