// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Tmds.DBus;

// Tmds.DBus 0.x emits proxy classes into its own dynamic assembly
// ("Tmds.DBus.Emit"). A proxy for an *internal* interface fails at
// CreateProxy with "attempting to implement an inaccessible interface"
// unless that dynamic assembly can see our internals. This covers the
// portal proxies below and the Fcitx5 input-method proxies.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(Tmds.DBus.Connection.DynamicAssemblyName)]

namespace Microsoft.Maui.Platform.Linux.Services.Portal;

// Proxy shapes for the xdg-desktop-portal frontend
// (bus name org.freedesktop.portal.Desktop, object /org/freedesktop/portal/desktop).
// Tmds.DBus 0.x conventions: the D-Bus member name is the C# name minus
// "Async"; a method called GetAsync(string) is the property getter for the
// interface's properties (it must be declared on every interface directly,
// inheritance between proxy interfaces is not supported).

[DBusInterface("org.freedesktop.portal.Request")]
internal interface IPortalRequestProxy : IDBusObject
{
    Task CloseAsync();
    Task<IDisposable> WatchResponseAsync(Action<(uint response, IDictionary<string, object> results)> handler, Action<Exception>? onError = null);
}

[DBusInterface("org.freedesktop.portal.Session")]
internal interface IPortalSessionProxy : IDBusObject
{
    Task CloseAsync();
}

[DBusInterface("org.freedesktop.portal.FileChooser")]
internal interface IFileChooserProxy : IDBusObject
{
    Task<ObjectPath> OpenFileAsync(string parentWindow, string title, IDictionary<string, object> options);
    Task<ObjectPath> SaveFileAsync(string parentWindow, string title, IDictionary<string, object> options);
    Task<object> GetAsync(string prop);
}

[DBusInterface("org.freedesktop.portal.OpenURI")]
internal interface IOpenUriProxy : IDBusObject
{
    Task<ObjectPath> OpenURIAsync(string parentWindow, string uri, IDictionary<string, object> options);
    Task<ObjectPath> OpenFileAsync(string parentWindow, CloseSafeHandle fd, IDictionary<string, object> options);
    Task<ObjectPath> OpenDirectoryAsync(string parentWindow, CloseSafeHandle fd, IDictionary<string, object> options);
    Task<object> GetAsync(string prop);
}

[DBusInterface("org.freedesktop.portal.Notification")]
internal interface INotificationPortalProxy : IDBusObject
{
    Task AddNotificationAsync(string id, IDictionary<string, object> notification);
    Task RemoveNotificationAsync(string id);
    Task<IDisposable> WatchActionInvokedAsync(Action<(string id, string action, object[] parameter)> handler, Action<Exception>? onError = null);
    Task<object> GetAsync(string prop);
}

[DBusInterface("org.freedesktop.portal.Screenshot")]
internal interface IScreenshotProxy : IDBusObject
{
    Task<ObjectPath> ScreenshotAsync(string parentWindow, IDictionary<string, object> options);
    Task<object> GetAsync(string prop);
}

[DBusInterface("org.freedesktop.portal.Secret")]
internal interface ISecretProxy : IDBusObject
{
    Task<ObjectPath> RetrieveSecretAsync(CloseSafeHandle fd, IDictionary<string, object> options);
    Task<object> GetAsync(string prop);
}

[DBusInterface("org.freedesktop.portal.Settings")]
internal interface ISettingsProxy : IDBusObject
{
    Task<object> ReadOneAsync(string @namespace, string key);
    Task<object> ReadAsync(string @namespace, string key);
    Task<IDisposable> WatchSettingChangedAsync(Action<(string @namespace, string key, object value)> handler, Action<Exception>? onError = null);
    Task<object> GetAsync(string prop);
}

[DBusInterface("org.freedesktop.portal.Inhibit")]
internal interface IInhibitProxy : IDBusObject
{
    Task<ObjectPath> InhibitAsync(string window, uint flags, IDictionary<string, object> options);
    Task<object> GetAsync(string prop);
}

[DBusInterface("org.freedesktop.portal.Background")]
internal interface IBackgroundProxy : IDBusObject
{
    Task<ObjectPath> RequestBackgroundAsync(string parentWindow, IDictionary<string, object> options);
    Task<object> GetAsync(string prop);
}

[DBusInterface("org.freedesktop.portal.Location")]
internal interface ILocationProxy : IDBusObject
{
    Task<ObjectPath> CreateSessionAsync(IDictionary<string, object> options);
    Task<ObjectPath> StartAsync(ObjectPath sessionHandle, string parentWindow, IDictionary<string, object> options);
    Task<IDisposable> WatchLocationUpdatedAsync(Action<(ObjectPath sessionHandle, IDictionary<string, object> location)> handler, Action<Exception>? onError = null);
    Task<object> GetAsync(string prop);
}

/// <summary>
/// The desktop notification server (not a portal). Used directly for
/// unsandboxed apps, where it gives numeric ids, close reasons and the
/// full hint set that the portal's Notification interface does not.
/// </summary>
[DBusInterface("org.freedesktop.Notifications")]
internal interface IFreedesktopNotificationsProxy : IDBusObject
{
    Task<uint> NotifyAsync(string appName, uint replacesId, string appIcon, string summary, string body, string[] actions, IDictionary<string, object> hints, int expireTimeout);
    Task CloseNotificationAsync(uint id);
    Task<IDisposable> WatchActionInvokedAsync(Action<(uint id, string actionKey)> handler, Action<Exception>? onError = null);
    Task<IDisposable> WatchNotificationClosedAsync(Action<(uint id, uint reason)> handler, Action<Exception>? onError = null);
}

/// <summary>Mutter's display configuration (GNOME only) for the logical-monitor scale.</summary>
[DBusInterface("org.gnome.Mutter.DisplayConfig")]
internal interface IMutterDisplayConfigProxy : IDBusObject
{
    Task<(uint serial,
          ((string, string, string, string) id, (string, int, int, double, double, double[], IDictionary<string, object>)[] modes, IDictionary<string, object> properties)[] monitors,
          (int x, int y, double scale, uint transform, bool primary, (string, string, string, string)[] monitors, IDictionary<string, object> properties)[] logicalMonitors,
          IDictionary<string, object> properties)> GetCurrentStateAsync();
}
