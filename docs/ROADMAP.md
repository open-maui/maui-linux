# OpenMaui Linux Platform Roadmap

This document outlines the development roadmap for the OpenMaui Linux platform.

## Shipped (10.0.50 → 10.0.70.1)

### Core platform

| Feature | Description |
|---------|-------------|
| Core control library | 50+ controls including Button, Label, Entry, Shapes, CarouselView, RefreshView, SwipeView |
| SkiaSharp rendering | Hardware-accelerated 2D graphics |
| X11 support | Full X11 display server integration (always-available fallback) |
| **Native Wayland support** | xdg-shell + wp_viewporter + fractional-scale-v1 + zxdg_decoration_manager_v1; programmatically selectable via `UseX11()` / `UseWayland()` |
| **CSD for GNOME-Wayland** *(10.0.60.10)* | Custom titlebar drawn in Skia with drag-to-move, edge-resize, min/max/close buttons; auto-activates when Mutter refuses SSD |
| Platform services | File picker, notifications, global hotkeys, drag & drop, secure storage |
| **Native Wayland clipboard** *(10.0.60.11)* | `wl_data_device_manager` binding; no subprocess overhead, no `wl-clipboard` package needed |
| **Native Wayland IME** *(10.0.60.12)* | `zwp_text_input_v3` for compositor-mediated IMEs (GNOME Pinyin/Hangul/Anthy, native Fcitx5); IBus / XIM remain on X11 |
| Accessibility (AT-SPI2) | Screen reader support |
| High DPI support | Automatic scale factor detection; fractional scale via Wayland viewporter for pixel-exact 1.25/1.5/1.75x rendering |
| AppThemeBinding propagation | Live updates across Shell, NavigationPage, CollectionView item trees, and pushed pages |
| Drag and drop | XDND protocol (X11) |
| Global hotkeys | System-wide keyboard shortcuts |
| XAML support | Standard .NET MAUI XAML syntax |
| GTK4 interop layer | `Gtk4InteropService` with GTK3 fallback |
| WebView control | WebKitGTK-backed; GTK mode opt-in via `LinuxApplicationOptions.UseGtk` |
| Project templates | Code and XAML-based (`openmaui-linux`, `openmaui-linux-xaml`) |
| Visual Studio extension | Project templates and launch profiles |
| AppImage packaging | `dotnet appimage` tool (separate `OpenMaui.AppImage` repo) |

### Opt-in sibling packages

| Package | Description |
|---------|-------------|
| **`OpenMaui.Controls.Linux.MediaElement`** *(10.0.60.13)* | Linux backend for `CommunityToolkit.Maui.MediaElement` via GStreamer (playbin + appsink → Skia); auto VAAPI/NVDEC hardware decode when plugins installed |

### MAUI 10.0.70 alignment + default-template fixes *(10.0.70.1)*

| Fix | Description |
|---------|-------------|
| MAUI 10.0.70 alignment | Updated `Microsoft.Maui.Controls` / `Microsoft.Maui.Graphics` / `Microsoft.Maui.Graphics.Skia` references from 10.0.60 to 10.0.70 |
| Default template CS1508 | Replaced the broad `EmbeddedResource Include="Resources\**\*"` (which collided with XAML compiler resource IDs for `Colors.xaml` / `Styles.xaml`) with proper `MauiFont` / `MauiImage` items |
| Wayland shim deployment | `libopenmaui_wl.so` now lands next to the consumer assembly via `.targets`, and the loader also probes `runtimes/<RID>/native/` so framework-dependent builds work without extra setup |
| Essentials registration on MAUI 10 | `EssentialsPatches` now prefers `SetDefault`/`SetCurrent` static setters and tolerates both `defaultImplementation` and `currentImplementation` field naming — Connectivity, AppInfo, DeviceInfo, AppActions, etc. now route through Linux services instead of the portable stubs |

## Planned

### Next release (10.0.70.2 target)

User-prioritized work for the next push. Frame-accurate HTTP scrubbing is *intentionally deferred* — playback works fine, and the backward-seek drift is a deep-layer GStreamer/HTTP byte-range problem that doesn't justify the effort right now.

| Feature | Description |
|---------|-------------|
| Maps integration | OpenStreetMap-based mapping (`Microsoft.Maui.Controls.Maps` Linux backend) |
| Printing support | CUPS printing integration |
| System tray menus | Rich tray icon interactions (StatusNotifierItem / `org.kde.StatusNotifierWatcher` with `_NET_WM_SYSTEM_TRAY` fallback) |
| `IInputContext.DeleteSurrounding` | Wire up `delete_surrounding_text` end-to-end so IMEs can retract text around the caret |
| Primary-selection clipboard | `zwp_primary_selection_v1` for middle-click paste |
| Native `wl_data_device_manager` drag-and-drop | Replace XDND-only path; works on both X11 and Wayland |

### Medium-term

| Feature | Description |
|---------|-------------|
| `set_surrounding_text` for text-input-v3 | Pass focused entry's text + caret to IME for better word suggestions |
| Hardware video acceleration tuning | Currently auto-negotiated via playbin; explicit pipeline construction for direct compositor-surface (zero-copy) playback |
| XAML Hot Reload | Live XAML editing during debugging |
| Live Visual Tree | Debug tool for inspecting UI hierarchy |
| `Tmds.DBus` migration | Replace `dbus-monitor` subprocess in `Fcitx5InputMethodService` |

### Long-term

| Feature | Description |
|---------|-------------|
| Vulkan rendering | Next-gen graphics API support |
| Flatpak packaging | Easy distribution via Flatpak |
| Snap packaging | Ubuntu Snap store support |
| Multi-window support | Multiple top-level windows |
| Frame-accurate HTTP scrubbing | *Deferred.* The 1-2s backward-seek drift on HTTP-streamed video is a byte-range re-request + decode-and-discard latency issue at the GStreamer layer; local-file scrubbing is already frame-accurate, so the user-visible impact is limited to streamed sources |

## Contributing

We welcome contributions! Priority areas:

1. **Wayland Support** - Help complete the Wayland backend
2. **Testing** - Integration tests on various distributions
3. **Documentation** - API docs and tutorials
4. **Controls** - Additional control implementations
5. **Samples** - Real-world demo applications

See [CONTRIBUTING.md](../CONTRIBUTING.md) for details.

## Milestones

| Milestone | .NET / MAUI | Target | Status |
|-----------|-------------|--------|--------|
| v9.0.40 | .NET 9 / MAUI 9.0.40 | Q1 2026 | ✅ Released |
| v9.0.x | .NET 9 / MAUI 9.0.x | Q1-Q2 2026 | 🔧 Maintenance |
| v10.0.41 | .NET 10 / MAUI 10.0.41 | Q1 2026 | ✅ Released |
| v10.0.50.x | .NET 10 / MAUI 10.0.50 | Q1 2026 | ✅ Released |
| v10.0.60.x | .NET 10 / MAUI 10.0.60 | Q2 2026 | ✅ Released |
| v10.0.70.1 | .NET 10 / MAUI 10.0.70 | Q2 2026 | ✅ Released |
| v10.0.70.x | .NET 10 / MAUI 10.0.70 | Q2-Q3 2026 | 🚀 Active |

## Feedback

- Issues: https://github.com/open-maui/maui-linux/issues

---

*Last updated: June 2026*
*Copyright 2025-2026 MarketAlly Pte Ltd*
