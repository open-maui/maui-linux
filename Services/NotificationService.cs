// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;
using System.Collections.Concurrent;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux notification service using notify-send (libnotify) or D-Bus directly.
/// Supports interactive notifications with action callbacks.
/// </summary>
public class NotificationService
{
    private readonly string _appName;
    private readonly string? _defaultIconPath;
    private readonly ConcurrentDictionary<uint, NotificationContext> _activeNotifications = new();
    private static uint _notificationIdCounter = 1;
    private Process? _dBusMonitor;
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
    {
        _appName = appName;
        _defaultIconPath = defaultIconPath;
    }

    /// <summary>
    /// Starts monitoring for notification action callbacks via D-Bus.
    /// Call this once at application startup if you want to receive action callbacks.
    /// </summary>
    public void StartActionMonitoring()
    {
        if (_monitoringActions) return;
        _monitoringActions = true;

        // Start D-Bus monitor for notification signals
        Task.Run(MonitorNotificationSignals);
    }

    /// <summary>
    /// Stops monitoring for notification action callbacks.
    /// </summary>
    public void StopActionMonitoring()
    {
        _monitoringActions = false;
        try
        {
            _dBusMonitor?.Kill();
            _dBusMonitor?.Dispose();
            _dBusMonitor = null;
        }
        catch { }
    }

    private async Task MonitorNotificationSignals()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dbus-monitor",
                Arguments = "--session \"interface='org.freedesktop.Notifications'\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _dBusMonitor = Process.Start(startInfo);
            if (_dBusMonitor == null) return;

            var reader = _dBusMonitor.StandardOutput;
            var buffer = new StringBuilder();

            while (_monitoringActions && !_dBusMonitor.HasExited)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;

                buffer.AppendLine(line);

                // Look for ActionInvoked or NotificationClosed signals
                if (line.Contains("ActionInvoked"))
                {
                    await ProcessActionInvoked(reader);
                }
                else if (line.Contains("NotificationClosed"))
                {
                    await ProcessNotificationClosed(reader);
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("NotificationService", $"D-Bus monitor error: {ex.Message}");
        }
    }

    private async Task ProcessActionInvoked(StreamReader reader)
    {
        try
        {
            // Read the signal data (notification id and action key)
            uint notificationId = 0;
            string? actionKey = null;

            for (int i = 0; i < 10; i++) // Read a few lines to get the data
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;

                if (line.Contains("uint32"))
                {
                    var idMatch = System.Text.RegularExpressions.Regex.Match(line, @"uint32\s+(\d+)");
                    if (idMatch.Success)
                    {
                        notificationId = uint.Parse(idMatch.Groups[1].Value);
                    }
                }
                else if (line.Contains("string"))
                {
                    var strMatch = System.Text.RegularExpressions.Regex.Match(line, @"string\s+""([^""]*)""");
                    if (strMatch.Success && actionKey == null)
                    {
                        actionKey = strMatch.Groups[1].Value;
                    }
                }

                if (notificationId > 0 && actionKey != null) break;
            }

            if (notificationId > 0 && actionKey != null)
            {
                if (_activeNotifications.TryGetValue(notificationId, out var context))
                {
                    // Invoke callback if registered
                    if (context.ActionCallbacks?.TryGetValue(actionKey, out var callback) == true)
                    {
                        callback?.Invoke();
                    }

                    ActionInvoked?.Invoke(this, new NotificationActionEventArgs(notificationId, actionKey, context.Tag));
                }
            }
        }
        catch { }
    }

    private async Task ProcessNotificationClosed(StreamReader reader)
    {
        try
        {
            uint notificationId = 0;
            uint reason = 0;

            for (int i = 0; i < 5; i++)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;

                if (line.Contains("uint32"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"uint32\s+(\d+)");
                    if (match.Success)
                    {
                        if (notificationId == 0)
                            notificationId = uint.Parse(match.Groups[1].Value);
                        else
                            reason = uint.Parse(match.Groups[1].Value);
                    }
                }
            }

            if (notificationId > 0)
            {
                _activeNotifications.TryRemove(notificationId, out var context);
                NotificationClosed?.Invoke(this, new NotificationClosedEventArgs(
                    notificationId,
                    (NotificationCloseReason)reason,
                    context?.Tag));
            }
        }
        catch { }
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
        var notificationId = _notificationIdCounter++;

        // Store context for callbacks
        var context = new NotificationContext
        {
            Tag = tag,
            ActionCallbacks = actions.ToDictionary(a => a.Key, a => a.Callback)
        };
        _activeNotifications[notificationId] = context;

        // Build actions dictionary for options
        var actionDict = actions.ToDictionary(a => a.Key, a => a.Label);

        await ShowAsync(new NotificationOptions
        {
            Title = title,
            Message = message,
            Actions = actionDict
        });

        return notificationId;
    }

    /// <summary>
    /// Cancels/closes an active notification.
    /// </summary>
    public async Task CancelAsync(uint notificationId)
    {
        try
        {
            // Use gdbus to close the notification
            var startInfo = new ProcessStartInfo
            {
                FileName = "gdbus",
                Arguments = $"call --session --dest org.freedesktop.Notifications " +
                           $"--object-path /org/freedesktop/Notifications " +
                           $"--method org.freedesktop.Notifications.CloseNotification {notificationId}",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
            }

            _activeNotifications.TryRemove(notificationId, out _);
        }
        catch { }
    }

    /// <summary>
    /// Shows a notification with options.
    /// </summary>
    public async Task ShowAsync(NotificationOptions options)
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

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
            }
        }
        catch (Exception ex)
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
