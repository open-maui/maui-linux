# xdg-desktop-portal layer

OpenMaui talks to [xdg-desktop-portal](https://flatpak.github.io/xdg-desktop-portal/) over native D-Bus (Tmds.DBus), on one shared session-bus connection. No `gdbus`, `dbus-monitor` or `busctl` subprocesses are involved. Portals and native desktop APIs sit side by side: every service keeps its previous non-portal path, and a missing portal is a normal outcome, never an error.

## Portals in use

| Portal | Used by | When it is tried first | Fallback |
|---|---|---|---|
| FileChooser (OpenFile, SaveFile) | `PortalFilePickerService` | Always | zenity, kdialog, yad |
| OpenURI (OpenURI, OpenFile by file descriptor, OpenDirectory) | `LauncherService`, `BrowserService`, `ShareService`, `Launcher.Default` | Sandboxed, or `OPENMAUI_PORTALS=prefer` | `xdg-open` (Share: zenity notice, then the file manager) |
| Notification | `NotificationService` | Sandboxed, or `OPENMAUI_PORTALS=prefer` | `org.freedesktop.Notifications` over D-Bus, then `notify-send`, then zenity |
| Settings (`org.freedesktop.appearance`) | `SystemThemeService` (color-scheme, accent-color, live via `SettingChanged`) | Always | Desktop-specific detection (gsettings, kdeglobals, GTK settings) and polling |
| Settings (`org.gnome.desktop.interface`) | `HiDpiService` (scaling-factor, text-scaling-factor) | Always | gsettings |
| Inhibit (idle) | `DeviceDisplay.KeepScreenOn` | Always | `xdg-screensaver` (X11 only) |
| Location | `Geolocation.GetLocationAsync` | Always | none (returns null, as before) |
| Secret | `SecureStorage` file store key | Sandboxed, or `OPENMAUI_PORTALS=prefer` | Legacy machine-derived key |
| Background | `LinuxBackgroundService.RequestAsync` (Linux-specific API) | Always (explicit call) | none (`PortalAvailable` is false) |
| Screenshot | internal `PortalScreenshot` helper | Only when called | none |

"Sandboxed" means Flatpak (`/.flatpak-info` or `$FLATPAK_ID`) or Snap (`$SNAP`). The launch-style portals are not tried first for unsandboxed apps so that an ordinary desktop install behaves exactly as before: `xdg-open` and the notification server give the same result without the portal's extra layer (app choosers, portal app-id rules).

## Switch

`OPENMAUI_PORTALS` controls the layer:

| Value | Effect |
|---|---|
| unset / `auto` | The table above |
| `prefer` (or `on`, `1`, `true`) | Every portal first, also unsandboxed |
| `off` (or `0`, `false`) | Never touch the portal; legacy paths only |

## How a call works

1. Version check: the interface's `version` property is read (and cached) through `org.freedesktop.DBus.Properties`. A missing `org.freedesktop.portal.Desktop` or a missing interface reads as version 0, which means "take the fallback". Features that need a newer version check it (FileChooser `directory` needs 3, OpenURI `OpenDirectory` needs 3, Settings uses `ReadOne` from version 2 and `Read` before that).
2. Request/Response: a fresh `handle_token` is put in the options, the client subscribes to `org.freedesktop.portal.Request.Response` on the predicted path `/org/freedesktop/portal/desktop/request/SENDER/TOKEN` (the unique bus name without `:` and with `.` as `_`) **before** calling the method, then awaits the response. Frontends older than 0.9 return a different path; the subscription moves to it. Cancelling closes the request on the portal side.
3. Outcome: response 0 is success, 1 is a user cancel (final: no second dialog), anything else is a failure (the fallback runs).

Inhibit returns a request handle that represents the inhibition itself; closing it (`KeepScreenOn = false`) lifts the inhibition. Location uses a short-lived session: `CreateSession`, `Start`, the first `LocationUpdated` for that session, then `Session.Close`.

## Threading

Tmds.DBus completes call tasks and runs signal handlers on its connection receive loop. The shared connection is created with `RunContinuationsAsynchronously`, and every signal handler (Settings changes, notification actions, closes) is queued to the thread pool. Without that, code that awaited a portal call and later blocked on another one (the theme and HiDPI services do a bounded synchronous read at startup) would stall all D-Bus traffic until its timeout. `SystemThemeService.ThemeChanged` and the notification events are raised on a thread-pool thread, as they were with the old polling and `dbus-monitor` code.

## Secure storage and the Secret portal

Stored values use the libsecret keyring through `secret-tool` when it is present. The encrypted-file fallback has two formats:

- **v1 (legacy)**: `[16-byte IV][AES-CBC ciphertext]`, key derived from machine-id, user name and service name. Always readable.
- **v2**: `"OMSS" 0x02 | 12-byte nonce | 16-byte tag | AES-256-GCM ciphertext`, key = HKDF-SHA256 of the per-application secret from `Secret.RetrieveSecret`.

v2 is written only when the Secret portal supplies a secret (by default only inside a sandbox, where each app gets its own). Reads accept both formats, so data stored before this change stays readable and moves to v2 the next time the key is set. Limitation: a v2 value cannot be read in a session where the Secret portal is unavailable (for example, a locked keyring); it reads as missing until the portal is back. Unsandboxed apps keep v1 unless `OPENMAUI_PORTALS=prefer` is set, because all unsandboxed apps share one portal secret.

## Background and autostart

```csharp
var result = await LinuxBackgroundService.RequestAsync(
    reason: "Keep syncing while the window is closed",
    autostart: true);
if (result.PortalAvailable && result.Autostart) { /* registered to start at login */ }
```

The portal writes the autostart entry itself, which is the only option inside Flatpak. `commandLine` defaults to the running process's command line. Unsandboxed apps may instead ship an XDG autostart `.desktop` file (the packaging tool's desktop entry can be copied to `~/.config/autostart`); xdg-desktop-portal's Background support for unsandboxed apps depends on the frontend version and on the app having an app id.

## Not covered

- Wayland dialog parenting: portals need an `xdg-foreign` export handle (`wayland:HANDLE`), which is not wired yet, so dialogs are unparented on Wayland. X11 passes `x11:XID`.
- `activation_token` (xdg-activation) for OpenURI is supported in the options builder but not supplied by the launcher yet.
- There is no share portal. `ShareService` uses OpenURI.OpenFile with the "Open with" chooser inside a sandbox.
- The Essentials `Screenshot` service keeps capturing the app's own window; the portal Screenshot helper takes whole-desktop screenshots and is internal.
- Fcitx5 input method stays on its own D-Bus connection.
