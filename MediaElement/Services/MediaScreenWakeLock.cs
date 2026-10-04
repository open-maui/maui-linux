// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Services.Portal;
using Tmds.DBus;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(Tmds.DBus.Connection.DynamicAssemblyName)]

namespace Microsoft.Maui.Platform.Linux.MediaElement.Services;

/// <summary>Keeps the screen awake (MediaElement.ShouldKeepScreenOn).</summary>
internal interface IScreenWakeLock
{
    /// <summary>Requests the screen stay on (Windows: DisplayRequest.RequestActive).</summary>
    void Acquire();

    /// <summary>Releases the request (Windows: DisplayRequest.RequestRelease).</summary>
    void Release();
}

[DBusInterface("org.freedesktop.ScreenSaver")]
internal interface IScreenSaverProxy : IDBusObject
{
    Task<uint> InhibitAsync(string applicationName, string reason);
    Task UnInhibitAsync(uint cookie);
}

/// <summary>
/// The screen-saver inhibition behind MediaElement.ShouldKeepScreenOn: the
/// desktop portal's Inhibit (idle) first, which works on Wayland and X11 and
/// inside Flatpak; without it, org.freedesktop.ScreenSaver.Inhibit on the
/// session bus (GNOME, KDE, Xfce, Cinnamon, MATE). Requests are applied in
/// order on a background chain, so play/pause toggles never block the UI.
/// </summary>
internal sealed class MediaScreenWakeLock : IScreenWakeLock
{
    internal const string Reason = "Playing media";

    private readonly Lock _gate = new();
    private Task _chain = Task.CompletedTask;
    private PortalIdleInhibitor? _portal;
    private uint? _cookie;

    /// <summary>The factory the handler uses; tests replace it.</summary>
    internal static Func<IScreenWakeLock> Factory { get; set; } = () => new MediaScreenWakeLock();

    public void Acquire() => Enqueue(true);

    public void Release() => Enqueue(false);

    /// <summary>Waits for queued requests (tests).</summary>
    internal Task WhenSettled() { lock (_gate) return _chain; }

    private void Enqueue(bool inhibit)
    {
        lock (_gate)
        {
            _chain = _chain.ContinueWith(_ => ApplyAsync(inhibit), TaskScheduler.Default).Unwrap();
        }
    }

    private async Task ApplyAsync(bool inhibit)
    {
        try
        {
            if (DesktopPortal.ShouldTry(PortalUse.Always))
            {
                _portal ??= new PortalIdleInhibitor(DesktopPortal.Current);
                bool held = _portal.IsInhibiting;
                if (await _portal.SetAsync(inhibit, Reason).ConfigureAwait(false) || (!inhibit && held))
                    return;
            }
            await ApplyScreenSaverAsync(inhibit).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("MediaElement", $"Screen saver inhibit failed: {ex.Message}");
        }
    }

    private async Task ApplyScreenSaverAsync(bool inhibit)
    {
        if (inhibit == _cookie.HasValue) return;
        var bus = await SessionBus.GetAsync().ConfigureAwait(false);
        var proxy = bus.Connection.CreateProxy<IScreenSaverProxy>("org.freedesktop.ScreenSaver", new ObjectPath("/org/freedesktop/ScreenSaver"));
        if (inhibit)
        {
            _cookie = await proxy.InhibitAsync(ApplicationName, Reason).ConfigureAwait(false);
        }
        else
        {
            var cookie = _cookie!.Value;
            _cookie = null;
            await proxy.UnInhibitAsync(cookie).ConfigureAwait(false);
        }
    }

    internal static string ApplicationName
    {
        get
        {
            try
            {
                var name = AppInfoService.Instance.Name;
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
            catch (Exception)
            {
                // AppInfo unavailable (tests): fall back to the process name.
            }
            return Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "OpenMaui";
        }
    }
}
