// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Dispatching;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// CommunityToolkit.Maui's <c>Toast</c> and <c>Snackbar</c> on Linux, as on Windows: a desktop
/// notification. The toolkit's Windows build shows each as an app notification (the Snackbar with
/// its action button), expiring after the alert's duration, one of each at a time; its
/// platform-neutral build, the one a Linux app runs, completed the calls and showed nothing. Here
/// they go to the desktop's notification server (org.freedesktop.Notifications, or the
/// notification portal when sandboxed) through <see cref="NotificationService"/>:
/// <list type="bullet">
/// <item>Toast: its text, transient, gone after its duration (2 s short, 3.5 s long).</item>
/// <item>Snackbar: its text and an action button labelled <c>ActionButtonText</c>; pressing it runs
/// <c>Action</c> on the UI thread. It goes after <c>Duration</c>. <c>Shown</c> is raised when it is
/// shown and <c>Dismissed</c> when it goes (expired, closed by the user, or <c>Dismiss</c>), which
/// the Windows build never raises.</item>
/// </list>
/// As on Windows, a notification cannot take <c>VisualOptions</c> or an <c>Anchor</c>; the desktop
/// draws it. A new alert replaces the one of its kind on screen. A desktop without a notification
/// server shows nothing (no dialog stands in; the calls still complete and Snackbar's events are
/// raised). No compile-time reference: the toolkit is optional.
/// </summary>
internal static class ToolkitAlertsBridge
{
    private const string ToastTypeName = "CommunityToolkit.Maui.Alerts.Toast, CommunityToolkit.Maui";
    private const string SnackbarTypeName = "CommunityToolkit.Maui.Alerts.Snackbar, CommunityToolkit.Maui";
    internal const string SnackbarActionKey = "snackbar-action";

    private static NotificationService? s_notifications;
    private static Session? s_toast;
    private static Session? s_snackbar;
    private static readonly Lock s_gate = new();

    /// <summary>The notification service alerts go through (tests replace it).</summary>
    internal static NotificationService Notifications
    {
        get
        {
            lock (s_gate)
            {
                if (s_notifications == null)
                {
                    var service = new NotificationService(AppName());
                    service.NotificationClosed += OnNotificationClosed;
                    s_notifications = service;
                }
                return s_notifications;
            }
        }
        set
        {
            lock (s_gate)
            {
                if (s_notifications != null)
                    s_notifications.NotificationClosed -= OnNotificationClosed;
                s_notifications = value;
                value.NotificationClosed += OnNotificationClosed;
                // Alerts shown through the old service are forgotten, their expiry included.
                foreach (var session in new[] { s_toast, s_snackbar })
                {
                    if (session != null && Interlocked.Exchange(ref session.Closed, 1) == 0)
                        session.Expiry.Cancel();
                }
                s_toast = s_snackbar = null;
            }
        }
    }

    /// <summary>One alert on screen.</summary>
    private sealed class Session
    {
        public required object Alert;
        public required IDispatcher? Dispatcher;
        public uint NotificationId;
        public int Closed;
        public readonly CancellationTokenSource Expiry = new();
    }

    internal static void Install(Harmony harmony)
    {
        var toast = Resolve(ToastTypeName);
        var snackbar = Resolve(SnackbarTypeName);
        if (toast != null)
        {
            Patch(harmony, toast, "Show", nameof(Toast_Show_Prefix));
            Patch(harmony, toast, "Dismiss", nameof(Toast_Dismiss_Prefix));
        }
        if (snackbar != null)
        {
            Patch(harmony, snackbar, "Show", nameof(Snackbar_Show_Prefix));
            Patch(harmony, snackbar, "Dismiss", nameof(Snackbar_Dismiss_Prefix));
        }
    }

    private static Type? Resolve(string name)
    {
        try
        {
            return Type.GetType(name, throwOnError: false);
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or TypeLoadException)
        {
            return null;
        }
    }

    private static void Patch(Harmony harmony, Type type, string method, string prefix)
    {
        var target = type.GetMethod(method, BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(CancellationToken) }, null);
        if (target == null)
        {
            DiagnosticLog.Warn("ToolkitAlerts", $"{type.Name}.{method}(CancellationToken) not found in this toolkit release; it is not bridged");
            return;
        }
        harmony.Patch(target, prefix: new HarmonyMethod(typeof(ToolkitAlertsBridge).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic)));
    }

    // ---- Toast ----------------------------------------------------------------------

    private static bool Toast_Show_Prefix(object __instance, CancellationToken token, ref Task __result)
    {
        __result = ShowToastAsync(__instance, token);
        return false;
    }

    private static bool Toast_Dismiss_Prefix(CancellationToken token, ref Task __result)
    {
        __result = DismissAsync(Volatile.Read(ref s_toast), token);
        return false;
    }

    /// <summary>The toolkit's toast durations (its private GetDuration): short 2 s, long 3.5 s.</summary>
    internal static TimeSpan ToastDuration(object toast)
    {
        var duration = Convert.ToInt32(toast.GetType().GetProperty("Duration")?.GetValue(toast), System.Globalization.CultureInfo.InvariantCulture);
        return duration switch
        {
            0 => TimeSpan.FromSeconds(2),
            1 => TimeSpan.FromSeconds(3.5),
            _ => throw new System.ComponentModel.InvalidEnumArgumentException("Duration", duration, toast.GetType().GetProperty("Duration")!.PropertyType),
        };
    }

    private static async Task ShowToastAsync(object toast, CancellationToken token)
    {
        await DismissAsync(Volatile.Read(ref s_toast), token).ConfigureAwait(true);
        token.ThrowIfCancellationRequested();

        var duration = ToastDuration(toast);
        var session = new Session { Alert = toast, Dispatcher = CurrentDispatcher() };
        s_toast = session;
        session.NotificationId = await Notifications.ShowAsync(new NotificationOptions
        {
            Title = Text(toast),
            Message = string.Empty,
            ExpireTimeMs = ToMilliseconds(duration),
            IsTransient = true,
        }, new Dictionary<string, Action?>(), toolFallbacks: false).ConfigureAwait(true);
        ExpireAfter(session, duration);
    }

    // ---- Snackbar ---------------------------------------------------------------------

    private static bool Snackbar_Show_Prefix(object __instance, CancellationToken token, ref Task __result)
    {
        __result = ShowSnackbarAsync(__instance, token);
        return false;
    }

    private static bool Snackbar_Dismiss_Prefix(CancellationToken token, ref Task __result)
    {
        __result = DismissAsync(Volatile.Read(ref s_snackbar), token);
        return false;
    }

    private static async Task ShowSnackbarAsync(object snackbar, CancellationToken token)
    {
        await DismissAsync(Volatile.Read(ref s_snackbar), token).ConfigureAwait(true);
        token.ThrowIfCancellationRequested();

        var type = snackbar.GetType();
        var duration = type.GetProperty("Duration")?.GetValue(snackbar) is TimeSpan d ? d : TimeSpan.FromSeconds(3);
        var buttonText = type.GetProperty("ActionButtonText")?.GetValue(snackbar) as string ?? "OK";
        var action = type.GetProperty("Action")?.GetValue(snackbar) as Action;
        var session = new Session { Alert = snackbar, Dispatcher = CurrentDispatcher() };
        s_snackbar = session;

        session.NotificationId = await Notifications.ShowAsync(new NotificationOptions
        {
            Title = Text(snackbar),
            Message = string.Empty,
            ExpireTimeMs = ToMilliseconds(duration),
            IsTransient = true,
            Actions = new Dictionary<string, string> { [SnackbarActionKey] = buttonText },
        }, new Dictionary<string, Action?>
        {
            [SnackbarActionKey] = action == null ? null : () => OnUiThread(session, action),
        }, toolFallbacks: false).ConfigureAwait(true);

        InvokePrivate(snackbar, "OnShown");
        ExpireAfter(session, duration);
    }

    // ---- Shared -----------------------------------------------------------------------

    private static string Text(object alert) => alert.GetType().GetProperty("Text")?.GetValue(alert) as string ?? string.Empty;

    private static int ToMilliseconds(TimeSpan duration)
        => duration <= TimeSpan.Zero ? 1 : (int)Math.Min(int.MaxValue, Math.Ceiling(duration.TotalMilliseconds));

    private static string AppName()
    {
        try
        {
            var name = AppInfoService.Instance.Name;
            return string.IsNullOrWhiteSpace(name) ? "MAUI Application" : name;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("ToolkitAlerts", "App name unavailable for notifications", ex);
            return "MAUI Application";
        }
    }

    private static IDispatcher? CurrentDispatcher()
        => Dispatcher.GetForCurrentThread() ?? Microsoft.Maui.Controls.Application.Current?.Dispatcher;

    private static void OnUiThread(Session session, Action action)
    {
        if (session.Dispatcher is { IsDispatchRequired: true } dispatcher)
            dispatcher.Dispatch(() => Run(action));
        else
            Run(action);

        static void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("ToolkitAlerts", "The Snackbar action failed", ex);
            }
        }
    }

    /// <summary>Takes the alert down after its duration, as the Windows notification's Expiration does.</summary>
    private static void ExpireAfter(Session session, TimeSpan duration)
    {
        _ = Task.Delay(duration, session.Expiry.Token).ContinueWith(
            t => Close(session, cancelNotification: true),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
    }

    private static Task DismissAsync(Session? session, CancellationToken token)
    {
        if (session == null)
            return Task.CompletedTask;
        token.ThrowIfCancellationRequested();
        return Close(session, cancelNotification: true);
    }

    /// <summary>The server closed a notification (it expired, or the user closed it).</summary>
    private static void OnNotificationClosed(object? sender, NotificationClosedEventArgs e)
    {
        foreach (var session in new[] { s_toast, s_snackbar })
        {
            if (session != null && session.NotificationId == e.NotificationId)
                _ = Close(session, cancelNotification: false);
        }
    }

    /// <summary>Closes an alert once: takes the notification down and raises the Snackbar's Dismissed.</summary>
    private static async Task Close(Session session, bool cancelNotification)
    {
        if (Interlocked.Exchange(ref session.Closed, 1) == 1)
            return;
        session.Expiry.Cancel();
        Interlocked.CompareExchange(ref s_toast, null, session);
        Interlocked.CompareExchange(ref s_snackbar, null, session);
        if (cancelNotification && session.NotificationId != 0)
        {
            try
            {
                await Notifications.CancelAsync(session.NotificationId).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Debug("ToolkitAlerts", "Closing the notification failed", ex);
            }
        }
        if (session.Alert.GetType().GetMethod("OnDismissed", BindingFlags.Instance | BindingFlags.NonPublic) != null)
            OnUiThread(session, () => InvokePrivate(session.Alert, "OnDismissed"));
    }

    private static void InvokePrivate(object target, string method)
        => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
}
