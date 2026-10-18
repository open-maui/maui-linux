# Versioning, compatibility and support

This document is OpenMaui's stable-contract commitment: how versions are numbered, what may change between them, how that is enforced, and which systems each release line supports.

## Version scheme

Every OpenMaui package version has four parts, `A.B.C.D`:

| Part | Meaning | Example (`10.0.110.1`) |
|------|---------|------------------------|
| `A.B.C` | The .NET MAUI version the release is built on and tested against. The packages depend on exactly this MAUI version as their floor. | MAUI `10.0.110` |
| `D` | OpenMaui's own release counter on that MAUI version, starting at 1 and reset when `A.B.C` changes. | first OpenMaui release on MAUI 10.0.110 |

`A` also names the .NET major (`10` is .NET 10, `net10.0`). A **release line** is everything sharing `A.B.C`: `10.0.101.x` is one line, `10.0.110.x` the next.

All six library packages and the templates ship in lockstep with the same version: `OpenMaui.Controls.Linux`, `OpenMaui.Hosting`, `OpenMaui.Controls.Linux.MediaElement`, `OpenMaui.Controls.Linux.Maps`, `OpenMaui.Controls.Linux.Blazor`, `OpenMaui.Controls.Linux.Syncfusion` and `OpenMaui.Linux.Templates`. Mixing versions across them is not supported.

Because `A.B.C` is taken from MAUI, the version number cannot also carry semantic-versioning meaning in its major part. The rules below give it that meaning instead.

## What counts as a breaking change

The public contract is every `public` and `protected` type and member in the shipped assemblies, plus the documented runtime behaviour (environment variables such as `OPENMAUI_RENDERER`, `OPENMAUI_WEBVIEW`, `MAUI_PREFER_X11`; `LinuxApplicationOptions`; the `UseLinux*()` builder extensions; the MSBuild props/targets in the package).

Breaking (binary or source):

- removing or renaming a public type, member, enum value, or namespace;
- changing a member's signature, return type, parameter list or defaults, or a type's base class;
- adding an abstract member to a public class, or a member without a default implementation to a public interface that consumers may implement;
- making a type or member less visible, `sealed`, or `static`;
- changing the value of a public constant or enum member;
- removing or renaming a documented environment variable, option or builder extension, or changing its meaning.

Not breaking:

- adding types, members, overloads, enum values, interfaces (new interfaces rather than new members on existing ones);
- bug fixes that make behaviour match what MAUI specifies, even when an app relied on the old behaviour. These are called out under **Fixed** in `CHANGELOG.md`, and the ones likely to change what an app looks like or does are also listed in [`MIGRATION.md`](MIGRATION.md);
- changes to `internal` code and to test hooks.

### When breaks are allowed

- **`D` bumps (same MAUI version):** no breaking changes. Fixes and additions only.
- **`A.B.C` bumps (new MAUI version):** breaking changes are allowed only when they follow the deprecation policy below, or when MAUI itself forces them (for example a type MAUI now provides replaces ours, or a dependency major version is forced by MAUI's dependency floor, as SkiaSharp 3 to 4 was in 10.0.101.1). Every break is listed in `CHANGELOG.md` under **Removed** or **Changed** and in `MIGRATION.md` with the replacement.
- **`A` bumps (new .NET major):** a new major line; breaks follow the same listing rule.

## Deprecation policy

A public API is removed only after it has carried `[Obsolete("... use X instead")]` for at least one MAUI minor release line. In practice: marked obsolete in the `10.0.110.x` line, removable in the next MAUI line at the earliest. The obsolete message names the replacement. `[Obsolete(error: true)]` may be used in the intermediate line when keeping the old API working would be unsafe.

Exceptions, stated in `CHANGELOG.md` when used: APIs that never worked (their removal cannot break a working app), and security fixes.

### Deliberate break: 10.0.110.8

10.0.110.8 breaks the rules above on purpose, once. OpenMaui's public types in `Microsoft.Maui.Platform` whose names were also MAUI types (`StackOrientation`, `GridLength`, `ShellSection`, `TextChangedEventArgs` and 37 others) made every file that imports `Microsoft.Maui.Platform` together with `Microsoft.Maui.Controls` fail with CS0104 "ambiguous reference", and the concrete-handler `Map*` overloads stopped MAUI-style mapper code from compiling. Deprecating them for a release line would have kept the ambiguity, because an obsolete type still takes part in name lookup. They were removed in 10.0.110.8 instead of 11.0: duplicates of MAUI types give way to MAUI's own (the members keep their names and take MAUI's types), OpenMaui's own types keep only their `Skia`/`Linux` names, and the `Map*` methods keep only MAUI's interface-typed signatures. Code that used the removed names must be recompiled against 10.0.110.8, and an app compiled against 10.0.110.7 that used them does not load against it. The full list and the replacements are in `CHANGELOG.md` (**Removed**) and [`MIGRATION.md`](MIGRATION.md).

## How API compatibility is enforced

Each packable library project sets `<EnablePackageValidation>true</EnablePackageValidation>`, so `dotnet pack` runs the .NET SDK's package validation on the package it builds (consistency of the package's own assemblies and target frameworks). No `PackageValidationBaselineVersion` is set: the packages are published from this repository, so a comparison against the previous published package adds nothing. The rules above are kept by review, the `[Obsolete]` deprecation policy, and the `CHANGELOG.md` and `MIGRATION.md` entries for every change to the public surface.

## Support matrix

### Release lines and lifetime

| Line | .NET | MAUI | SkiaSharp | Status |
|------|------|------|-----------|--------|
| `10.0.110.x` | 10 (`net10.0`) | 10.0.110 | 4.152.1 | Current: fixes and features |
| `10.0.101.x` | 10 (`net10.0`) | 10.0.101 | 4.152.1 | Previous: critical fixes until the next MAUI minor line ships |
| `10.0.90.x` and older 10.0 lines | 10 | 10.0.90 and older | 3.x (10.0.41 to 10.0.90) | Unsupported; upgrade within the 10.0 family |
| `9.0.40` | 9 (`net9.0`) | 9.0.40 | 2.88 | Unsupported |
| `1.0.0` | - | - | - | Deprecated (renumbered to 9.0.40) |

Lifetime rule: the latest line is fully supported. The previous line receives critical fixes (crashes, data loss, security) until the next MAUI minor version is released and OpenMaui has shipped a line on it. Nothing older is patched. The whole .NET 10 family follows Microsoft's .NET 10 (LTS) and MAUI 10 servicing: when MAUI 10 leaves support, so does OpenMaui 10.

### Operating systems

OpenMaui needs a glibc-based Linux with the .NET 10 runtime. "Verified" means the maintainers run the application and the test suite on it during development of a release; "Supported" means it is expected to work and bugs found on it are accepted and fixed, but it is not exercised on every release.

| Distribution | Status | Notes |
|--------------|--------|-------|
| Fedora 44 | Verified | Primary development system (KDE Plasma, Intel GPU, native Wayland). WPE WebKit via the `philn/wpewebkit` COPR. |
| Fedora 43 | Supported | Same package names as 44; WPE via the same COPR. |
| Ubuntu 26.04 | Supported | No WPE WebKit package (Ubuntu has published none since 22.04): the WebView uses WebKitGTK in GTK mode (`options.UseGtk = true`) and BlazorWebView needs a WPE 2.54 from elsewhere. |
| Ubuntu 24.04 | Supported | No WPE WebKit package: the WebView falls back to WebKitGTK in GTK mode (`options.UseGtk = true`); everything else is unaffected. |
| Debian 13 | Supported | Ships WPE 2.48, older than the 2.54 the WebView needs: WebKitGTK/GTK mode unless 2.54 comes from testing (forky). Debian testing and sid carry 2.54. |
| Other glibc distributions (Arch, openSUSE, ...) | Best effort | Expected to work with the same native libraries; `openmaui doctor` prints install hints for apt, dnf and pacman. |
| musl (Alpine) | Not supported | The Wayland shim and the templates target glibc RIDs only. |

CI builds, tests and packs on a single Linux runner; it does not run a distribution or desktop matrix. A public multi-distro, multi-desktop CI matrix is open to contributors (see `docs/ROADMAP.md`).

Run `OPENMAUI_DOCTOR=1 ./YourApp` on any machine to see exactly what the platform will select there and what is missing.

### Desktops and display servers

| Environment | Status | Window decorations |
|-------------|--------|--------------------|
| GNOME (Mutter), Wayland | Supported | Client-side titlebar drawn by OpenMaui (Mutter does not offer server-side decorations) |
| KDE Plasma (KWin), Wayland | Verified | Server-side via `xdg-decoration` |
| wlroots compositors (Sway and others), Wayland | Supported | Server-side via `xdg-decoration` |
| X11 sessions and XWayland (`MAUI_PREFER_X11=1`) | Supported | Window manager decorations |

Native Wayland needs `wl_compositor`, `xdg_wm_base` and `wl_shm`; fractional scaling uses `wp_fractional_scale_v1` + `wp_viewporter` when the compositor has them; input methods use `zwp_text_input_v3`.

### Architectures

| RID | Status | Notes |
|-----|--------|-------|
| `linux-x64` | Verified | |
| `linux-arm64` | Supported | Every native asset OpenMaui ships or depends on has an aarch64 build (see [`ARM64.md`](ARM64.md)). Built and packaged on every release; not yet run on arm64 hardware by the maintainers. |
| `linux-arm` (32-bit), `linux-musl-*`, others | Not supported | The Wayland protocol shim is not built for them. |

### Native dependencies

Required: `libwayland-client` and `libxkbcommon` (native Wayland) or `libX11` (X11/XWayland), GTK 3 and GLib (`libgtk-3.so.0`, loaded at startup; also the file chooser and print dialog), fontconfig. Optional, each enabling one feature and each reported by `openmaui doctor` when missing:

| Library | Enables | Minimum |
|---------|---------|---------|
| `libEGL.so.1` + GLES (Mesa or vendor), `libwayland-egl.so.1` on Wayland | GPU render target (otherwise CPU raster, automatically) | EGL 1.5, GLES 2 |
| WPE WebKit (`libWPEWebKit-2.0.so.1`) | `WebView` and `BlazorWebView` in native Wayland/X11 mode | 2.54 |
| WebKitGTK (`libwebkit2gtk-4.1.so.0`, or 4.0) | `WebView` fallback in GTK mode | |
| GStreamer 1.x (`libgstreamer-1.0`, `libgstapp-1.0`) + plugin sets | `MediaElement` (`OpenMaui.Controls.Linux.MediaElement`) | |
| `libcups.so.2` | Printing | |
| `libatspi.so.0` (AT-SPI2) | Screen readers | |
| `libayatana-appindicator3` or `libappindicator3` | System tray | |
| CJK and emoji fonts | Fallback glyphs | |
