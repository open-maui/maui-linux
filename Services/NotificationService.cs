// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux notification service using notify-send (libnotify).
/// </summary>
public class NotificationService
{
    private readonly string _appName;
    private readonly string? _defaultIconPath;

    public NotificationService(string appName = "MAUI Application", string? defaultIconPath = null)
    {
        _appName = appName;
        _defaultIconPath = defaultIconPath;
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

/// <summary>
/// Options for displaying a notification.
/// </summary>
public class NotificationOptions
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? IconPath { get; set; }
    public string? IconName { get; set; } // Standard icon name like "dialog-information"
    public NotificationUrgency Urgency { get; set; } = NotificationUrgency.Normal;
    public int ExpireTimeMs { get; set; } = 5000; // 5 seconds default
    public string? Category { get; set; } // e.g., "email", "im", "transfer"
    public bool IsTransient { get; set; }
    public Dictionary<string, string>? Actions { get; set; }
}

/// <summary>
/// Notification urgency level.
/// </summary>
public enum NotificationUrgency
{
    Low,
    Normal,
    Critical
}
