# OpenMaui Linux Platform Roadmap

This document outlines the development roadmap for the OpenMaui Linux platform.

## Positioning

> OpenMaui is the Wayland-first, self-rendered, production-grade Linux platform for .NET MAUI, with X11 compatibility rather than GTK as its architectural foundation.

The roadmap below is built around that statement. Wayland is the primary architecture and new capabilities are designed Wayland-first, then backported to X11 where practical; X11/XWayland remains a fully supported compatibility architecture. GTK, WebKit, GStreamer and similar are used as implementation tools where they are the best available technology, but no desktop toolkit is the foundation of the platform:

```text
MAUI
 ↓
OpenMaui
 ↓
Skia + Linux platform APIs
 ↓
Wayland (X11 compatibility)
```

Guiding principle for the next releases: the existing 50+ controls do not need thirty more siblings; they need to run on a rendering, web, and verification foundation that is clearly engineered for where Linux is going.

## Shipped (10.0.50 to 10.0.101.1)

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

### MAUI 10.0.90 alignment + roadmap set *(10.0.90.1)*

| Feature | Description |
|---------|-------------|
| MAUI 10.0.90 alignment | Bumped `Microsoft.Maui.Controls` / `Graphics` / `Graphics.Skia` / `Controls.Maps` 10.0.70 → 10.0.90; no source changes required, all packages to 10.0.90.1 |
| Drag payload types | `DragPayload` (text / files / image) drives `TryStartDrag`; Wayland offers per-payload MIMEs (`text/uri-list`, `image/png`, text); **outgoing X11 INCR** implemented for >64 KB targets; `GestureManager` extracts files + images from MAUI `DataPackage` |
| Maps satellite / hybrid layers | `SkiaMap.LayerType` + MAUI `Map.MapType` wired; `TileSource` abstraction with keyless defaults (OSM, Esri World Imagery, Esri reference overlay); layer-stacking hybrid render; layer-keyed cache; per-layer attribution |
| `Tmds.DBus` migration | Fcitx5 transport moved off the `dbus-monitor` subprocess to typed Tmds.DBus 0.94.2 proxies (`InputMethod1` / `InputContext1`, commit + preedit signals); fixed a latent inverted-key-event bug |
| Live Visual Tree | `Diagnostics/VisualTreeInspector` — read-only tree snapshot, highlight overlay, click-to-pick, text dump; reuses the popup-overlay draw hook; opt-in Ctrl+Shift+D hotkey |
| Hot Reload | `[MetadataUpdateHandler]` → main-thread re-render of the current page (`SkiaShell.ReRenderContentTrees`, root rebuild for non-Shell roots); C# + XAML edits under `dotnet watch` for Shell and non-Shell roots (see `docs/HOT_RELOAD.md`) |

### Multi-window + SkiaSharp 4 *(10.0.101.1)*

| Feature | Description |
|---------|-------------|
| MAUI 10.0.101 / SkiaSharp 4 | MAUI 10.0.101 forces SkiaSharp 3 → 4: ~400 call sites migrated off the error-obsolete `SKPaint` text APIs to `SKFont`; new `SkiaFontFactory` (Subpixel + LinearMetrics) is the mandatory font construction path — fixes HiDPI intra-word glyph gaps |
| Multi-window support | `Application.OpenWindow` / `CloseWindow` with per-window native toplevel, render engine, and input routing (X11 + Wayland parity); `IWindow` lifecycle per the MAUI contract; last-window-close exits, primary-close survives; per-window routing for clipboard/dialogs/popups/theme documented in code. V1 scope: DnD targets primary, GTK mode single-window |
| Non-Shell-root Hot Reload | Raw `ContentPage`/`NavigationPage` window pages rebuild and re-swap on `dotnet watch` edits with proper lifecycle; nav stacks reset to root (matches MAUI's own structural reload) |
| Async drag image sourcing | `StreamImageSource` drag payloads resolve in-flight with format sniffing and bounded honest-fail; X11 defers `SelectionNotify` instead of blocking the pump |
| Text measurement correctness | Wrapped labels measure by the draw path's wrap math (no more sibling overdraw); single-line label heights are line-height based, not ink-bounds based (glyph-independent spacing) |

## Planned

Priorities are ordered. Phase 1 and Phase 2 are the current focus; Phase 3 follows them.

### Phase 1: GPU-native presentation

Shipped in 10.0.101.2: the pipeline is now `SkiaView -> Skia GL (GRContext) -> EGL window surface -> eglSwapBuffers -> compositor`, with the raster path (`SkiaView -> CPU raster -> wl_shm / XPutImage`) retained as an automatic fallback.

| Item | Status |
|------|--------|
| `IRenderTarget` boundary | Done. `SkiaRenderingEngine` draws on the canvas a target provides; `RasterRenderTarget` (both backends, via the window's `Present`), `WaylandEglRenderTarget`, `X11EglRenderTarget` |
| EGL on Wayland | Done. `wl_egl_window` + EGL 1.5 platform display + `GRContext` on the default framebuffer; zero-copy submission through `eglSwapBuffers`; the existing wp_viewporter logical/physical split still applies |
| EGL on X11 | Done. EGL config matched to the window's visual; drawable resized by the server |
| Runtime selection and fallback | Done. `LinuxApplicationOptions.Renderer` and `OPENMAUI_RENDERER=gpu|raster|auto`; any GPU failure falls back to raster |
| Resource lifetime | Done for resize and multi-window (one context per window, made current per frame, disposed before the connection closes). **Scale change** (moving between monitors of different scale) is still not a runtime path on either target; `wp_fractional_scale_v1.preferred_scale` is received but not applied |
| Frame statistics | Done. `OPENMAUI_RENDER_STATS=1` prints rendered FPS and avg/p50/p95/p99/max frame time per window |
| First numbers | MediaDemo video playback, 1400x1050 (1.75x), Mesa Intel UHD: raster avg 3.55 ms, p99 7-10 ms; egl-wayland avg 1.47 ms, p99 under 4 ms, at the same 32 rendered fps |

Remaining (Phase 1b):

| Item | Description |
|------|-------------|
| Benchmark suite | Turn the frame statistics into a repeatable suite: startup, idle CPU, memory, scrolling FPS, resize latency, animation smoothness, 1,000 and 10,000 item virtualisation, text rendering, power, on both targets |
| Runtime scale change | Apply `preferred_scale` (Wayland) and monitor changes (X11) at runtime: resize the EGL window / shm buffer, update `DpiScale`, re-layout |
| Hardware video zero-copy | MediaElement frames imported as GPU textures (DMA-BUF via VA-API/NVDEC where available) instead of the CPU `SKBitmap` upload the GPU target still performs per frame |
| Explicit DMA-BUF and Vulkan | `zwp_linux_dmabuf_v1` buffer submission and a Vulkan `GRContext` backend once the EGL path has soaked |
| Partial-damage submission | `eglSwapBuffersWithDamageKHR` / `wl_surface_damage_buffer` from the engine's dirty rects (the rects' logical-versus-physical coordinate handling needs fixing first) |

### Phase 2: WPE WebKit WebView and BlazorWebView

WebView is the platform's remaining architectural rough spot: WebKitGTK is a GTK widget, so on Wayland it cannot simply be reparented into a native OpenMaui window the way it can on X11.

WPE WebKit 2.54 (released 2026-09-16) makes the WPEPlatform API stable and enabled by default (`wpe-platform-2.0`), deprecates the libwpe API, and moves WebKit's compositor to Skia. Under WPEPlatform the embedder subclasses `WPEDisplay`/`WPEToplevel`/`WPEView` and receives each rendered frame as a `WPEBuffer`, DMA-BUF (`wpe_buffer_import_to_egl_image`) with a shared-memory fallback (`wpe_buffer_import_to_pixels`), and feeds input as `WPEEvent`s. No GTK, no window of its own.

That fits OpenMaui exactly: the WebView becomes an ordinary `SkiaView` that draws web frames as textures inside the platform's own render tree, identical on Wayland and X11, and it composes directly with the Phase 1 GPU pipeline (EGLImage to `SKImage`).

| Item | Description |
|------|-------------|
| BlazorWebView on the existing WebKitGTK WebView | First, and independent of WPE: `BlazorWebView` needs a custom URI scheme handler (`app://`) and a message bridge, both available in WebKitGTK. Delivers Blazor Hybrid parity on Linux on every distro that ships WebKitGTK today, on both backends |
| WPE availability (verified 2026-09-20) | Debian sid ships 2.54.0, testing 2.52.6, stable 2.48; Ubuntu inherits from Debian. Fedora's own repositories do not package WPE WebKit, but the `philn/wpewebkit` COPR (maintained by an Igalia WPE developer) ships `wpewebkit` 2.54.0, `libwpe` 1.16.3 and `wpebackend-fdo` 1.16.1 for Fedora 43/44 on x86_64 and aarch64, built on release day. Platform work is therefore detection plus guidance (`openmaui doctor` and the AppImage dependency scanner print `dnf copr enable philn/wpewebkit && dnf install wpewebkit` on Fedora, `apt install libwpewebkit-2.0-1` on Debian/Ubuntu); an optional bundled WPE in the AppImage/Flatpak remains the answer for end users who cannot add repositories |
| WPEPlatform embedder | `WPEDisplay`/`WPEView` subclasses registered from managed code via the GObject type system (the platform already manages GClosure and GObject lifetimes); frames arrive through `render_buffer`, input is translated from OpenMaui pointer/keyboard events to `WPEEvent` |
| Frame import | DMA-BUF to `EGLImage` to `SKImage` on the GPU target; `wpe_buffer_import_to_pixels` to `SKBitmap` on the raster target |
| Backend selection | WPE when `libWPEWebKit-2.0` is present, WebKitGTK otherwise; the X11 WebKitGTK path remains the compatibility fallback. `openmaui doctor` reports which backend will be used |
| BlazorWebView on WPE | Same scheme handler and bridge, now composited natively on Wayland |
| Dependency reporting | AppImage tool's dependency scanner learns the WPE package names per distro and the COPR instructions |

### Phase 3: Conformance suite and visual regression

Every visual defect fixed in 10.0.101.1 (glyph gaps at fractional scale, label heights, wrap overlap, baseline drift) was found by a human screenshot, not by the 700-test suite. Phase 3 makes that impossible to repeat, and turns "runs unmodified" into a measured number.

| Item | Description |
|------|-------------|
| Golden screenshot tests | Offscreen Skia rendering of every control and the sample pages at 1.0x, 1.25x, 1.5x, 1.75x and 2.0x with pixel-diff comparison; no display required, runs in the existing test project |
| Compatibility scorecard | Automated pass/fail per area, published per release: MAUI Controls, Navigation (Shell, NavigationPage, multi-window), Essentials, Community Toolkit, Blazor Hybrid, XAML (bindings, styles, triggers, VisualStateManager, animations), input (gestures, IME, clipboard, drag and drop), accessibility, localisation and RTL, theming. Real tests behind every cell, no marketing percentages |
| Third-party compatibility as a KPI | How many existing MAUI applications and libraries run without modification: CommunityToolkit.Maui, MediaElement, SkiaSharp.Views.Maui, LiveCharts2, Maps, popular MVVM and DI frameworks, ReactiveUI. Tracked in the scorecard |
| Performance regression gates | The Phase 1 benchmark suite runs per release and fails on regression beyond a threshold |

### Also planned

| Item | Description |
|------|-------------|
| `openmaui doctor` | One command that reports .NET version, session type, compositor, available Wayland globals (fractional-scale, text-input-v3, dmabuf), GPU and renderer selection, scale factor, and the presence of GStreamer, CUPS, AT-SPI2, WebKitGTK and WPE with the exact packages to install. Builds on the AppImage tool's dependency scanner |
| xdg-desktop-portal layer | Portal calls move from `gdbus` subprocesses to native D-Bus (Tmds.DBus is already a dependency) and expand from FileChooser to OpenURI, Notification, Screenshot, Secret, Settings, Inhibit and Background; native compositor APIs and portals side by side, which is the Flatpak-ready shape |
| Deployment beyond AppImage | `deb` and `rpm` output from the packaging tool alongside AppImage and Flatpak; Snap later |
| Multi-window round-out | Per-window `WindowHandler` (live title/page changes on secondaries), DnD onto secondary windows, `IWindow.Stopped`/`Resumed`, window positioning, GTK-mode secondaries |
| ARM64 hardening | Keep `linux-arm64` boringly reliable (templates already publish both RIDs); WPE plus DRM/KMS opens embedded and kiosk targets with no desktop environment later |
| Stable-contract commitment | Semantic versioning, API compatibility checks between releases, migration guides and a documented support matrix across the MAUI 10 lifecycle |
| Frame-accurate HTTP scrubbing | Deferred. The 1-2s backward-seek drift on HTTP-streamed video is a byte-range re-request + decode-and-discard latency issue at the GStreamer layer; local-file scrubbing is already frame-accurate |

### Not prioritised

A public multi-distro, multi-desktop CI matrix (GNOME/KDE/Sway across Ubuntu and Fedora, on x64 and ARM64, at every scale factor) is a good idea and remains open to contributors, but it is not on the core team's list: the platform is developed and used daily on real desktops, and Phase 3 puts the verification effort into offscreen golden tests that run anywhere.

## Contributing

We welcome contributions! Priority areas:

1. **GPU presentation** - EGL/Vulkan render targets and benchmarks (Phase 1)
2. **WPE WebKit** - WPEPlatform embedding, Fedora packaging (COPR), BlazorWebView (Phase 2)
3. **Conformance** - golden screenshot tests and the compatibility scorecard (Phase 3)
4. **Distribution** - CI matrices across distros and desktops, deb/rpm output
5. **Documentation** - API docs and tutorials

See [CONTRIBUTING.md](../CONTRIBUTING.md) for details.

## Milestones

| Milestone | .NET / MAUI | Target | Status |
|-----------|-------------|--------|--------|
| v9.0.40 | .NET 9 / MAUI 9.0.40 | Q1 2026 | Released |
| v9.0.x | .NET 9 / MAUI 9.0.x | Q1-Q2 2026 | Maintenance |
| v10.0.41 | .NET 10 / MAUI 10.0.41 | Q1 2026 | Released |
| v10.0.50.x | .NET 10 / MAUI 10.0.50 | Q1 2026 | Released |
| v10.0.60.x | .NET 10 / MAUI 10.0.60 | Q2 2026 | Released |
| v10.0.70.1 | .NET 10 / MAUI 10.0.70 | Q2 2026 | Released |
| v10.0.70.2 | .NET 10 / MAUI 10.0.70 | Q2 2026 | Released (Maps sibling missing — see 10.0.70.3) |
| v10.0.70.3 | .NET 10 / MAUI 10.0.70 | Q2 2026 | Released |
| v10.0.70.4 | .NET 10 / MAUI 10.0.70 | Q3 2026 | Released |
| v10.0.90.1 | .NET 10 / MAUI 10.0.90 | Q3 2026 | Released |
| v10.0.101.1 | .NET 10 / MAUI 10.0.101 | Q3 2026 | Released |
| v10.0.101.2 | .NET 10 / MAUI 10.0.101 | Q3 2026 | In development: Phase 1 GPU presentation (shipped in tree), Phase 2 next |

## Feedback

- Issues: https://github.com/open-maui/maui-linux/issues

---

*Last updated: September 2026*
*Copyright 2025-2026 MarketAlly Pte Ltd*
