// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Services.Portal;

/// <summary>
/// The xdg-desktop-portal surface the Essentials services use. The production
/// implementation (<see cref="TmdsDesktopPortal"/>) speaks native D-Bus over a
/// shared session-bus connection; tests substitute a fake so option building,
/// response parsing and fallback decisions run without a bus.
/// </summary>
/// <remarks>
/// Contract for every member: when the portal frontend or the interface is
/// missing, request methods throw <see cref="PortalUnavailableException"/>
/// (callers treat it as "take the fallback") and <see cref="GetVersionAsync"/>
/// returns 0. Request methods complete when the Request object's Response
/// signal arrives; cancelling the token closes the request.
/// </remarks>
internal interface IDesktopPortal
{
    /// <summary>
    /// The interface's "version" property, or 0 when the portal or the
    /// interface is unavailable. Results are cached per interface.
    /// </summary>
    Task<uint> GetVersionAsync(string portalInterface, CancellationToken cancellationToken = default);

    Task<PortalResponse> FileChooserOpenFileAsync(string parentWindow, string title, IDictionary<string, object> options, CancellationToken cancellationToken);

    Task<PortalResponse> FileChooserSaveFileAsync(string parentWindow, string title, IDictionary<string, object> options, CancellationToken cancellationToken);

    Task<PortalResponse> OpenUriAsync(string parentWindow, string uri, IDictionary<string, object> options, CancellationToken cancellationToken);

    /// <summary>OpenURI.OpenFile: the file is passed as a file descriptor.</summary>
    Task<PortalResponse> OpenFileAsync(string parentWindow, SafeHandle file, IDictionary<string, object> options, CancellationToken cancellationToken);

    /// <summary>OpenURI.OpenDirectory: shows the file's folder in the file manager.</summary>
    Task<PortalResponse> OpenDirectoryAsync(string parentWindow, SafeHandle file, IDictionary<string, object> options, CancellationToken cancellationToken);

    Task<PortalResponse> ScreenshotAsync(string parentWindow, IDictionary<string, object> options, CancellationToken cancellationToken);

    /// <summary>Secret.RetrieveSecret: the backend writes the app secret to <paramref name="writeEnd"/>.</summary>
    Task<PortalResponse> RetrieveSecretAsync(SafeHandle writeEnd, IDictionary<string, object> options, CancellationToken cancellationToken);

    Task<PortalResponse> RequestBackgroundAsync(string parentWindow, IDictionary<string, object> options, CancellationToken cancellationToken);

    /// <summary>
    /// Inhibit.Inhibit. The returned handle keeps the inhibition alive until
    /// it is disposed (which closes the portal Request object).
    /// </summary>
    Task<IAsyncDisposable> InhibitAsync(string parentWindow, PortalInhibitFlags flags, IDictionary<string, object> options, CancellationToken cancellationToken);

    /// <summary>Settings.ReadOne (Settings.Read on version 1). Null when the key does not exist.</summary>
    Task<object?> ReadSettingAsync(string @namespace, string key, CancellationToken cancellationToken);

    /// <summary>Settings.SettingChanged. Handlers run on a thread-pool thread (never the D-Bus reader thread).</summary>
    Task<IDisposable> WatchSettingChangedAsync(Action<string, string, object> handler);

    Task AddNotificationAsync(string id, IDictionary<string, object> notification, CancellationToken cancellationToken);

    Task RemoveNotificationAsync(string id, CancellationToken cancellationToken);

    /// <summary>Notification.ActionInvoked (id, action). Handlers run on a thread-pool thread (never the D-Bus reader thread).</summary>
    Task<IDisposable> WatchNotificationActionInvokedAsync(Action<string, string> handler);

    /// <summary>
    /// Location: CreateSession + Start, waits for the first LocationUpdated
    /// for that session, then closes the session. Null on timeout/denial.
    /// </summary>
    Task<IReadOnlyDictionary<string, object>?> GetLocationAsync(IDictionary<string, object> sessionOptions, CancellationToken cancellationToken);

    /// <summary>
    /// Location: CreateSession + Start, then every LocationUpdated for that session goes to
    /// <paramref name="onUpdate"/> (on a thread-pool thread) until the returned handle is
    /// disposed, which closes the session. Null when the session could not start (denied).
    /// </summary>
    Task<IAsyncDisposable?> WatchLocationAsync(IDictionary<string, object> sessionOptions, Action<IReadOnlyDictionary<string, object>> onUpdate, CancellationToken cancellationToken);
}

/// <summary>Inhibit flags (bitmask) of org.freedesktop.portal.Inhibit.</summary>
[Flags]
internal enum PortalInhibitFlags : uint
{
    None = 0,
    Logout = 1,
    UserSwitch = 2,
    Suspend = 4,
    Idle = 8,
}

/// <summary>Well-known interface names.</summary>
internal static class PortalInterfaces
{
    public const string FileChooser = "org.freedesktop.portal.FileChooser";
    public const string OpenUri = "org.freedesktop.portal.OpenURI";
    public const string Notification = "org.freedesktop.portal.Notification";
    public const string Screenshot = "org.freedesktop.portal.Screenshot";
    public const string Secret = "org.freedesktop.portal.Secret";
    public const string Settings = "org.freedesktop.portal.Settings";
    public const string Inhibit = "org.freedesktop.portal.Inhibit";
    public const string Background = "org.freedesktop.portal.Background";
    public const string Location = "org.freedesktop.portal.Location";

    public static readonly string[] All =
    {
        FileChooser, OpenUri, Notification, Screenshot, Secret, Settings, Inhibit, Background, Location,
    };
}

/// <summary>
/// A portal that is never there: every version is 0 and every request throws
/// <see cref="PortalUnavailableException"/>. Used when portals are disabled
/// (OPENMAUI_PORTALS=off, no session bus) and by the test assembly.
/// </summary>
internal sealed class NullDesktopPortal : IDesktopPortal
{
    public static readonly NullDesktopPortal Instance = new();

    private NullDesktopPortal()
    {
    }

    private static Exception Unavailable() => new PortalUnavailableException("xdg-desktop-portal is disabled");

    public Task<uint> GetVersionAsync(string portalInterface, CancellationToken cancellationToken = default) => Task.FromResult(0u);

    public Task<PortalResponse> FileChooserOpenFileAsync(string parentWindow, string title, IDictionary<string, object> options, CancellationToken cancellationToken) => Task.FromException<PortalResponse>(Unavailable());

    public Task<PortalResponse> FileChooserSaveFileAsync(string parentWindow, string title, IDictionary<string, object> options, CancellationToken cancellationToken) => Task.FromException<PortalResponse>(Unavailable());

    public Task<PortalResponse> OpenUriAsync(string parentWindow, string uri, IDictionary<string, object> options, CancellationToken cancellationToken) => Task.FromException<PortalResponse>(Unavailable());

    public Task<PortalResponse> OpenFileAsync(string parentWindow, SafeHandle file, IDictionary<string, object> options, CancellationToken cancellationToken) => Task.FromException<PortalResponse>(Unavailable());

    public Task<PortalResponse> OpenDirectoryAsync(string parentWindow, SafeHandle file, IDictionary<string, object> options, CancellationToken cancellationToken) => Task.FromException<PortalResponse>(Unavailable());

    public Task<PortalResponse> ScreenshotAsync(string parentWindow, IDictionary<string, object> options, CancellationToken cancellationToken) => Task.FromException<PortalResponse>(Unavailable());

    public Task<PortalResponse> RetrieveSecretAsync(SafeHandle writeEnd, IDictionary<string, object> options, CancellationToken cancellationToken) => Task.FromException<PortalResponse>(Unavailable());

    public Task<PortalResponse> RequestBackgroundAsync(string parentWindow, IDictionary<string, object> options, CancellationToken cancellationToken) => Task.FromException<PortalResponse>(Unavailable());

    public Task<IAsyncDisposable> InhibitAsync(string parentWindow, PortalInhibitFlags flags, IDictionary<string, object> options, CancellationToken cancellationToken) => Task.FromException<IAsyncDisposable>(Unavailable());

    public Task<object?> ReadSettingAsync(string @namespace, string key, CancellationToken cancellationToken) => Task.FromException<object?>(Unavailable());

    public Task<IDisposable> WatchSettingChangedAsync(Action<string, string, object> handler) => Task.FromException<IDisposable>(Unavailable());

    public Task AddNotificationAsync(string id, IDictionary<string, object> notification, CancellationToken cancellationToken) => Task.FromException(Unavailable());

    public Task RemoveNotificationAsync(string id, CancellationToken cancellationToken) => Task.FromException(Unavailable());

    public Task<IDisposable> WatchNotificationActionInvokedAsync(Action<string, string> handler) => Task.FromException<IDisposable>(Unavailable());

    public Task<IReadOnlyDictionary<string, object>?> GetLocationAsync(IDictionary<string, object> sessionOptions, CancellationToken cancellationToken) => Task.FromException<IReadOnlyDictionary<string, object>?>(Unavailable());

    public Task<IAsyncDisposable?> WatchLocationAsync(IDictionary<string, object> sessionOptions, Action<IReadOnlyDictionary<string, object>> onUpdate, CancellationToken cancellationToken) => Task.FromException<IAsyncDisposable?>(Unavailable());
}
