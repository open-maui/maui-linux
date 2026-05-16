# OpenMaui Linux Platform Roadmap

This document outlines the development roadmap for the OpenMaui Linux platform.

## Shipped (10.0.50 → 10.0.60.13)

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

## Planned

### Near-term (next 1-2 rev builds)

| Feature | Description |
|---------|-------------|
| Frame-accurate HTTP scrubbing | Reduce 1-2s backward-seek drift via pre-buffering or wider byte-range pre-fetch |
| `set_surrounding_text` for text-input-v3 | Pass focused entry's text + caret to IME for better word suggestions |
| `IInputContext.DeleteSurrounding` | Wire up `delete_surrounding_text` end-to-end so IMEs can retract text around the caret |
| Hardware video acceleration tuning | Currently auto-negotiated via playbin; explicit pipeline construction for direct compositor-surface (zero-copy) playback |
| Primary-selection clipboard | `zwp_primary_selection_v1` for middle-click paste |
| Native `wl_data_device_manager` drag-and-drop | Replace XDND-only path; works on both X11 and Wayland |

### Medium-term

| Feature | Description |
|---------|-------------|
| XAML Hot Reload | Live XAML editing during debugging |
| Live Visual Tree | Debug tool for inspecting UI hierarchy |
| Maps integration | OpenStreetMap-based mapping |
| Printing support | CUPS printing integration |
| `Tmds.DBus` migration | Replace `dbus-monitor` subprocess in `Fcitx5InputMethodService` |

### Long-term

| Feature | Description |
|---------|-------------|
| Vulkan rendering | Next-gen graphics API support |
| Flatpak packaging | Easy distribution via Flatpak |
| Snap packaging | Ubuntu Snap store support |
| Multi-window support | Multiple top-level windows |
| System tray menus | Rich tray icon interactions |

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
| v10.0.x | .NET 10 / MAUI 10.0.x | 2026 | 🚀 Active |

## Feedback

- Issues: https://github.com/open-maui/maui-linux/issues

---

*Last updated: March 2026*
*Copyright 2025-2026 MarketAlly Pte Ltd*
