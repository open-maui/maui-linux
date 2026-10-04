// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Platform.Linux.Services.Portal;
using Tmds.DBus;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// The app's launcher badge (a count on its dock or task-manager icon) through the Unity
/// LauncherEntry D-Bus API (<c>com.canonical.Unity.LauncherEntry</c>), which KDE Plasma's task
/// manager, Dash to Dock / Ubuntu Dock, Plank, Latte and other docks read: an <c>Update</c> signal
/// carries the app's desktop-file URI and its <c>count</c> / <c>count-visible</c> properties.
/// Where no dock listens (or there is no session bus) setting the count does nothing, as on
/// Windows when the taskbar shows no badge. CommunityToolkit.Maui's <c>Badge.SetCount</c>, which
/// threw <see cref="NotSupportedException"/> in the toolkit's platform-neutral build, goes here.
/// </summary>
internal static class LauncherBadgeService
{
    internal const string InterfaceName = "com.canonical.Unity.LauncherEntry";

    private static readonly Lock s_gate = new();
    private static LauncherEntryObject? s_exported;
    private static Task? s_exporting;
    private static uint s_count;

    /// <summary>Where badge updates go (the session bus; tests replace it).</summary>
    internal static Action<string, IDictionary<string, object>> Emitter { get; set; } = EmitOnSessionBus;

    /// <summary>The count last set.</summary>
    internal static uint Count => Volatile.Read(ref s_count);

    /// <summary>
    /// Shows <paramref name="count"/> on the app's launcher icon; 0 clears the badge, as the
    /// toolkit's Windows build clears the taskbar badge. Never throws.
    /// </summary>
    internal static void SetCount(uint count)
    {
        Volatile.Write(ref s_count, count);
        try
        {
            Emitter(AppUri(), Properties(count));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("LauncherBadge", "Updating the launcher badge failed", ex);
        }
    }

    /// <summary>The LauncherEntry properties for a count: hidden when 0.</summary>
    internal static IDictionary<string, object> Properties(uint count) => new Dictionary<string, object>(StringComparer.Ordinal)
    {
        ["count"] = (long)count,
        ["count-visible"] = count > 0,
    };

    /// <summary>The app's LauncherEntry URI: <c>application://</c> and its desktop file's name.</summary>
    internal static string AppUri() => "application://" + DesktopFileId();

    /// <summary>
    /// The desktop file the app was launched from (GLib exports its path to the processes it
    /// launches), else the Flatpak app id's, else the one OpenMaui writes for the app at start-up
    /// (<c>~/.local/share/applications/&lt;name&gt;.desktop</c>, see LinuxApplication).
    /// </summary>
    internal static string DesktopFileId()
    {
        var launched = Environment.GetEnvironmentVariable("GIO_LAUNCHED_DESKTOP_FILE");
        if (!string.IsNullOrEmpty(launched))
            return Path.GetFileName(launched);
        var flatpak = Environment.GetEnvironmentVariable("FLATPAK_ID");
        if (!string.IsNullOrEmpty(flatpak))
            return flatpak + ".desktop";
        var appName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "MauiApp");
        return appName.Replace(" ", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal).ToLowerInvariant() + ".desktop";
    }

    private static void EmitOnSessionBus(string appUri, IDictionary<string, object> properties)
    {
        Task exporting;
        lock (s_gate)
        {
            if (s_exporting == null || s_exporting.IsFaulted || s_exporting.IsCanceled)
                s_exporting = ExportAsync(appUri);
            exporting = s_exporting;
        }
        _ = exporting.ContinueWith(t =>
        {
            // The latest count, so updates racing on the thread pool cannot leave a stale one.
            if (t.IsCompletedSuccessfully)
                s_exported?.Emit(appUri, Properties(Count));
            else
                DiagnosticLog.Debug("LauncherBadge", $"No session bus for the launcher badge: {t.Exception?.GetBaseException().Message}");
        }, TaskScheduler.Default);
    }

    private static async Task ExportAsync(string appUri)
    {
        var bus = await SessionBus.GetAsync().ConfigureAwait(false);
        var entry = new LauncherEntryObject(appUri);
        await bus.Connection.RegisterObjectAsync(entry).ConfigureAwait(false);
        s_exported = entry;
    }

    /// <summary>CommunityToolkit.Maui's <c>BadgeImplementation.SetCount</c>, when the toolkit is part of the app.</summary>
    internal static void InstallToolkitBadge(Harmony harmony)
    {
        Type? badge;
        try
        {
            badge = Type.GetType("CommunityToolkit.Maui.ApplicationModel.BadgeImplementation, CommunityToolkit.Maui.Core", throwOnError: false);
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or TypeLoadException)
        {
            badge = null;
        }
        var setCount = badge?.GetMethod("SetCount", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(uint) }, null);
        if (setCount == null)
            return;
        harmony.Patch(setCount, prefix: new HarmonyMethod(typeof(LauncherBadgeService).GetMethod(nameof(SetCount_Prefix), BindingFlags.Static | BindingFlags.NonPublic)));
    }

    private static bool SetCount_Prefix(uint count)
    {
        SetCount(count);
        return false;
    }
}

/// <summary>The LauncherEntry object the app exports: its Update signal and Query method.</summary>
[DBusInterface(LauncherBadgeService.InterfaceName)]
internal interface ILauncherEntry : IDBusObject
{
    Task<IDisposable> WatchUpdateAsync(Action<(string appUri, IDictionary<string, object> properties)> handler, Action<Exception>? onError = null);

    Task<(string appUri, IDictionary<string, object> properties)> QueryAsync();
}

internal sealed class LauncherEntryObject : ILauncherEntry
{
    private readonly string _appUri;

    public LauncherEntryObject(string appUri)
    {
        _appUri = appUri;
        // libunity's path: one object per app, under /com/canonical/unity/launcherentry.
        ObjectPath = new ObjectPath("/com/canonical/unity/launcherentry/" + ((uint)StableHash(appUri)).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public ObjectPath ObjectPath { get; }

    public event Action<(string appUri, IDictionary<string, object> properties)>? Update;

    public void Emit(string appUri, IDictionary<string, object> properties) => Update?.Invoke((appUri, properties));

    public Task<IDisposable> WatchUpdateAsync(Action<(string appUri, IDictionary<string, object> properties)> handler, Action<Exception>? onError = null)
        => SignalWatcher.AddAsync(this, nameof(Update), handler);

    public Task<(string appUri, IDictionary<string, object> properties)> QueryAsync()
        => Task.FromResult((_appUri, LauncherBadgeService.Properties(LauncherBadgeService.Count)));

    private static int StableHash(string s)
    {
        unchecked
        {
            int hash = 23;
            foreach (var ch in s)
                hash = hash * 31 + ch;
            return hash;
        }
    }
}
