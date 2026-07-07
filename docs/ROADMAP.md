# OpenMaui Linux Platform Roadmap

This document outlines the development roadmap for the OpenMaui Linux platform.

## Shipped (10.0.50 → 10.0.70.4)

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
| **`OpenMaui.Controls.Linux.Maps`** *(10.0.70.2)* | Linux backend for `Microsoft.Maui.Controls.Maps` — OpenStreetMap raster tile renderer in Skia, pin & polyline overlays, pan/zoom, XDG-cached tile fetch. Also exposes a standalone `SkiaMap` view for code-first map UI |

### MAUI 10.0.70 alignment + default-template fixes *(10.0.70.1)*

| Fix | Description |
|---------|-------------|
| MAUI 10.0.70 alignment | Updated `Microsoft.Maui.Controls` / `Microsoft.Maui.Graphics` / `Microsoft.Maui.Graphics.Skia` references from 10.0.60 to 10.0.70 |
| Default template CS1508 | Replaced the broad `EmbeddedResource Include="Resources\**\*"` (which collided with XAML compiler resource IDs for `Colors.xaml` / `Styles.xaml`) with proper `MauiFont` / `MauiImage` items |
| Wayland shim deployment | `libopenmaui_wl.so` now lands next to the consumer assembly via `.targets`, and the loader also probes `runtimes/<RID>/native/` so framework-dependent builds work without extra setup |
| Essentials registration on MAUI 10 | `EssentialsPatches` now prefers `SetDefault`/`SetCurrent` static setters and tolerates both `defaultImplementation` and `currentImplementation` field naming — Connectivity, AppInfo, DeviceInfo, AppActions, etc. now route through Linux services instead of the portable stubs |

### Wayland / desktop integration round-out *(10.0.70.2)*

| Feature | Description |
|---------|-------------|
| `IInputContext.DeleteSurrounding` | `zwp_text_input_v3.delete_surrounding_text` events now route into `SkiaEntry` / `SkiaEditor` with full UTF-8 byte → UTF-16 char conversion (handles surrogate pairs and mid-codepoint clamping). Also closed a sibling gap where `WaylandTextInputV3Service` was raising events but never calling back into `_context` |
| Primary-selection clipboard | New `zwp_primary_selection_v1` binding in `libopenmaui_wl.so` + `PrimarySelectionService` with subprocess fallbacks (`wl-paste --primary` / `xclip -selection primary` / `xsel --primary`). `SkiaEntry` and `SkiaEditor` push selection on drag-end and paste on middle-click — full standard Linux UX |
| Native `wl_data_device_manager` drag-and-drop | First functional Linux DnD path (the XDND scaffolding in `DragDropService` was never wired). New `WaylandWindow.DragDrop.cs` partial implements the `wl_data_device` enter/leave/motion/drop handlers (previously no-ops), `wl_data_offer.accept`/`set_actions`/`finish` protocol v3, pipe-based drop receive, and outgoing `wl_data_device.start_drag`. File drops auto-decode `file://` URIs onto `DragData.FilePaths` |
| Hardware video acceleration tuning | New `MediaHardwareAcceleration` enum (`Auto` / `Prefer` / `Disable`) for `OpenMaui.Controls.Linux.MediaElement`. `MediaHardwareAccelerationService` boosts GStreamer registry ranks of known HW decoder factories (VA-API, NVDEC, V4L2 stateless, Intel MediaSDK / oneVPL). `EnumerateAvailableHardwareDecoders()` for introspection |
| System tray icons | New `TrayIcon` / `TrayIconService` over libappindicator3 / libayatana-appindicator3 (StatusNotifierItem). Mutable menu, icon/title/tooltip, Activated event. Probe falls back through ayatana → app-indicator → no-op |
| CUPS printing | New `PrintService` with `EnumeratePrinters()`, `PrintFile(...)` (PDF/PS/image/text auto-filtered by CUPS), and `PrintSkiaPagesAsync(...)` for Skia-rendered multi-page jobs via `SKDocument.CreatePdf`. `IsAvailable=false` and graceful failure when libcups is missing |
| Maps (OpenStreetMap) | New sibling package **`OpenMaui.Controls.Linux.Maps`** (mirrors the MediaElement opt-in pattern). `LinuxMapHandler` wires `Microsoft.Maui.Controls.Maps.Map` to a `SkiaMap` view with OSM raster tiles, pin/polyline overlays, pan/zoom, persistent XDG tile cache, swappable tile URL template, and an attribution overlay per the OSM tile usage policy |

### Stability / correctness hardening *(10.0.70.4)*

Deep code review of the 10.0.70.x surfaces; no new features, but several crash-class fixes. See CHANGELOG for the full list.

| Area | Highlights |
|---------|-------------|
| Wayland core | All listener callback delegates rooted (GC could previously free native thunks → segfault); drag sources destroyed on `dnd_finished` (leak + clipboard corruption); `DragEventArgs.Accepted` defaults to accept; NULL-mime rejection, wl_fixed coordinate scaling, version-gated v3 requests, drop-race fixes; self-paste deadlock fixes in `HasText` for both clipboard and primary selection; primary selection now clearable |
| X11 | First working XDND drop path — `XdndAware` announced, `ClientMessage`/`SelectionNotify` routed, real `XConvertSelection` data transfer with INCR support, honest `XdndFinished` reporting |
| Maps | Tile-cache dispose race fixed; viewport-aware `MoveToRegion` zoom; `VisibleRegion` write-back; marker vs info-window click semantics; OSM tile-policy compliance (2-connection cap, negative caching); true LRU + provider-keyed + size-bounded caches; HiDPI tiles (zoom+1 at scale ≥ 1.5); world-wrap for overlays; live pin mutation |
| Tray icons | Correct 3-arg `set_icon_full`/`set_label` bindings (was undefined behavior on every update); GTK-owned callback lifetime; indicator unref on remove; main-thread marshaling |
| Printing | CUPS submit off the UI thread + async API variants; page-commit contract via `SKPicture`; owner-only temp PDFs |
| MediaElement | `gst_is_initialized` guard; decoder rank snapshot/restore makes `Auto` a true reset |

### Desktop integration round-out II *(10.0.70.4)*

| Feature | Description |
|---------|-------------|
| Full X11 XDND drag-and-drop | Incoming drops (async `SelectionNotify` transfer, INCR-capable) and outgoing drags via backend-agnostic `DragDropService.TryStartDrag` (native Wayland first, XDND source fallback: selection ownership, target discovery, Status negotiation, `SelectionRequest` delivery, Escape cancel) |
| MAUI drag/drop gesture recognizers | `DragGestureRecognizer` starts native drags (drag-gesture detection added to `GestureManager`; `DragStarting`/`Cancel` honored); `DropGestureRecognizer` receives native drops with per-view enter/leave/over transitions, text + file paths, and `AllowDrop`/`AcceptedOperation` feedback to the native accept on both backends. Additive to the `DragDropService.Default` events |
| IME surrounding text | `zwp_text_input_v3.set_surrounding_text` + `set_text_change_cause` — focused entry text/caret/anchor windowed to ≤4000 UTF-8 bytes, coalesced per frame, password fields excluded; same seam feeds IBus (`set_surrounding_text`, code-point offsets). Completes the Wayland IME loop |
| Map polygon / circle overlays | `IFilledMapElement` / `ICircleMapElement` routed end-to-end; Mercator meters-per-pixel radius projection; world-wrap aware; `SkiaMap.Polygons` / `Circles` for code-first use |
| GTK print dialog | `PrintService.ShowPrintDialogAsync` — GtkPrintUnixDialog with printer/copies/ranges/duplex/PPD options returned CUPS-ready; graceful null when GTK missing; `PrintJobStatus` adds a `NothingToPrint` state |
| Tray icon XEmbed fallback | freedesktop System Tray Protocol backend when no SNI host exists (probe: ayatana → appindicator → XEmbed → no-op); left-click `Activated`, GTK right-click menu, auto re-dock on panel restart |

## Planned

### Medium-term

| Feature | Description |
|---------|-------------|
| Hardware video acceleration zero-copy | Explicit pipeline construction for direct compositor-surface (zero-copy) playback — `Prefer` mode already covers decoder selection |
| XAML Hot Reload | Live XAML editing during debugging |
| Live Visual Tree | Debug tool for inspecting UI hierarchy |
| Maps satellite / hybrid layers | OSM raster only renders a single style today; satellite + hybrid would need a secondary tile source and a layer-stacking renderer |
| Drag payload types | Outgoing drags source text only today; file/image `DataPackage` payloads (and outgoing INCR on X11) are the follow-up |
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
| v10.0.70.2 | .NET 10 / MAUI 10.0.70 | Q2 2026 | ✅ Released (Maps sibling missing — see 10.0.70.3) |
| v10.0.70.3 | .NET 10 / MAUI 10.0.70 | Q2 2026 | ✅ Released |
| v10.0.70.4 | .NET 10 / MAUI 10.0.70 | Q3 2026 | 🚀 Pending release |
| v10.0.70.x | .NET 10 / MAUI 10.0.70 | Q2-Q3 2026 | 🚀 Active |

## Feedback

- Issues: https://github.com/open-maui/maui-linux/issues

---

*Last updated: July 2026*
*Copyright 2025-2026 MarketAlly Pte Ltd*
