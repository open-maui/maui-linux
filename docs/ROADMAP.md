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

Priorities are ordered. Phases 1 and 2 shipped in 10.0.101.2; Phase 3 is the current focus.

### Phase 1: GPU-native presentation

Shipped in 10.0.101.2: the pipeline is now `SkiaView -> Skia GL (GRContext) -> EGL window surface -> eglSwapBuffers -> compositor`, with the raster path (`SkiaView -> CPU raster -> wl_shm / XPutImage`) retained as an automatic fallback.

| Item | Status |
|------|--------|
| `IRenderTarget` boundary | Done. `SkiaRenderingEngine` draws on the canvas a target provides; `RasterRenderTarget` (both backends, via the window's `Present`), `WaylandEglRenderTarget`, `X11EglRenderTarget` |
| EGL on Wayland | Done. `wl_egl_window` + EGL 1.5 platform display + `GRContext` on the default framebuffer; zero-copy submission through `eglSwapBuffers`; the existing wp_viewporter logical/physical split still applies |
| EGL on X11 | Done. EGL config matched to the window's visual; drawable resized by the server |
| Runtime selection and fallback | Done. `LinuxApplicationOptions.Renderer` and `OPENMAUI_RENDERER=gpu|raster|auto`; any GPU failure falls back to raster |
| Resource lifetime | Done for resize and multi-window (one context per window, made current per frame, disposed before the connection closes). Scale change (moving between monitors of different scale) is a runtime path since 10.0.110.1: the Wayland buffer follows `wp_fractional_scale_v1.preferred_scale` per window, X11 follows Xft.dpi changes |
| Frame statistics | Done. `OPENMAUI_RENDER_STATS=1` prints rendered FPS and avg/p50/p95/p99/max frame time per window |
| First numbers | MediaDemo video playback, 1400x1050 (1.75x), Mesa Intel UHD: raster avg 3.55 ms, p99 7-10 ms; egl-wayland avg 1.47 ms, p99 under 4 ms, at the same 32 rendered fps |

Phase 1b (all done in 10.0.110.1):

| Item | Description |
|------|-------------|
| Benchmark suite | Done in 10.0.110.1: `tools/Benchmarks` (startup, idle, memory, 1k/10k scrolling, resize, animation, text, layout; raster and headless GPU), numbers in `docs/PERFORMANCE.md`. Power is not measured (RAPL needs root) |
| Runtime scale change | Done in 10.0.110.1: Wayland follows `wp_fractional_scale_v1.preferred_scale` per window (buffer re-sized, viewport keeps the logical size), X11 follows `RESOURCE_MANAGER`/Xft.dpi changes; engine, input, popups and the WebView use their own window's scale |
| Hardware video zero-copy | Done in 10.0.110.1: VA-API DMA-BUF frames imported as EGL external textures on the GPU target; 1080p VP9 process CPU -63%, frame install 1.1 ms to 0.1 ms (`docs/MEDIAELEMENT.md`). NVDEC and software decoders keep the copy path |
| Explicit DMA-BUF and Vulkan | Vulkan done in 10.0.110.1: `OPENMAUI_RENDERER=vulkan` (opt-in; Wayland and X11 WSI, Skia Vulkan `GRContext`, device matched to the compositor's GPU, fallback Vulkan to EGL to raster), at parity with EGL on Wayland (`docs/VULKAN.md`). Explicit `zwp_linux_dmabuf_v1` submission closed as not needed: EGL and Vulkan WSI already hand DMA-BUFs to the compositor, a hand-rolled path would measure the same |
| Partial-damage submission | Done in 10.0.110.1: per-view physical damage rects, `EGL_EXT_buffer_age` repaint of missed damage, `eglSwapBuffersWithDamage`; whole frames when overlays are up or the damage is large; `OPENMAUI_PARTIAL_DAMAGE=0` to disable |

### Phase 2: WPE WebKit WebView and BlazorWebView

Shipped in 10.0.101.2. WebView was the platform's remaining architectural rough spot (WebKitGTK is a GTK widget that cannot be reparented into a native Wayland window, so the GTK-hosted view only displayed in GTK mode). WPE WebKit 2.54's stable WPEPlatform API renders headlessly and delivers each frame as a `WPEBuffer` through the `buffer-rendered` signal, so the WebView is now an ordinary `SkiaView` drawing web frames inside the platform's own render tree, identical on Wayland and X11, composed with the Phase 1 GPU pipeline.

| Item | Status |
|------|--------|
| WPE availability | Debian testing and sid 2.54.0 (`libwpewebkit-2.0-1`; Debian 13 has 2.48, too old); Ubuntu has no WPE package since 22.04 (WebKitGTK fallback in GTK mode); Fedora via the `philn/wpewebkit` COPR (2.54.0, Fedora 43/44, x86_64 and aarch64). Runtime selection: WPE when the library loads, else WebKitGTK; `OPENMAUI_WEBVIEW` overrides |
| WPEPlatform embedder | Done, without GObject subclassing: headless display on an explicit DRM render node, `buffer-rendered` frames, `wpe_view_event` input, CSS-pixel sizing with the device scale on the toplevel (`docs/WPE-EMBEDDING.md`) |
| Frame import | Done: raster path (`wpe_buffer_import_to_pixels`) and, in 10.0.110.1, DMA-BUF to EGLImage to a Skia texture on the GPU target (import 3.8 ms -> 0.08 ms per frame at 1280x800), with a same-GPU check and automatic fallback |
| Context menus, clipboard | Done: WebKit's menu model through the platform's Skia context menu; in-process WPE clipboard bridged to the system clipboard both ways |
| BlazorWebView | Done: `OpenMaui.Controls.Linux.Blazor` (`app://localhost/` scheme, script-message bridge, embedded `blazor.webview.js`, `UrlLoading`, root components, DI). WPE only, by decision: no new WebKitGTK-specific work |
| Dependency reporting | Done: the AppImage tool's scanner (1.2.3) and `openmaui doctor` (10.0.110.1) report WPE and the per-distro install commands |
| JS dialogs, file chooser, link cursor | Done: `script-dialog` to the platform alert/confirm/prompt dialogs (new prompt dialog with a text field), `run-file-chooser` to the platform file picker, WPE cursor requests to the platform cursor. Permissions, web notifications, downloads, spell checking, submenus and multi-click done too; hardware keycodes (`KeyboardEvent.code`) in 10.0.110.1 |

### Phase 3: Conformance suite and visual regression

Shipped in 10.0.101.3. Every visual defect fixed in 10.0.101.1 (glyph gaps at fractional scale, label heights, wrap overlap, baseline drift) was found by a human screenshot, not by the 700-test suite. Phase 3 makes that impossible to repeat, and turns "runs unmodified" into a measured number: `docs/COMPATIBILITY.md` is generated from the test run by `tools/Scorecard`, an item counts as covered only when every test mapped to it passed, and the categories mirror the table Microsoft publishes for its maui-labs GTK backend so the two compare row for row.

| Item | Status |
|------|--------|
| Golden screenshot tests | Done: `tests/Golden/` renders twelve scenes (labels, buttons, entry/editor, toggles, ranges, pickers, grid and border, shapes, formatted text, table view, collection view) through the real rendering engine at 1.0x, 1.25x, 1.5x, 1.75x and 2.0x against committed baselines with a per-channel tolerance; no display required, mismatches write actual/expected/diff images. sample pages (ShellDemo's Typography, Controls, Pickers and About) rendered as full-page scenes through runtime XAML, handlers and the engine since 10.0.110.1 |
| Compatibility scorecard | Done: 19 categories, 125 items, computed from the TRX (`dotnet run --project tools/Scorecard -- --run`). The gaps it exposed were closed in the same release: AbsoluteLayout, ControlTemplate/ContentPresenter/TemplatedView, TableView and ListView handlers; MAUI 10 alert/action-sheet/prompt dispatch; modal navigation; animations on MAUI's ticker and animation manager; VisualStateManager, triggers and behaviors; FormattedText spans; gesture dispatch per MAUI's rules; context flyouts; fonts (registrar, manager, named sizes, FontImageSource); Essentials services behind a testable process seam. WebView scenarios run out of process because WebKit requires the main thread. The suite went from about 900 to 1,415 tests, run serially in about 12 seconds; the generated scorecard reports 125 of 125 items covered |
| Third-party compatibility as a KPI | Done in 10.0.110.1: `tests/Compat` runs 10 popular libraries unmodified through the real registration path and feeds a "Third-party libraries" section of the scorecard; 9 of 10 run (FFImageLoading.Maui ships no Linux image service); Syncfusion and Telerik are licence-gated and not evaluated. Fixes it drove: Prism startup window, wrapping-label vertical alignment, AbsoluteLayout XAML children, a CommunityToolkit DrawingView handler |
| Performance regression gates | Done in 10.0.110.1: `--compare` against `docs/perf-baseline.json` with per-metric thresholds fails on regression; CI runs it warn-only on shared runners. It already found two platform issues, fixed in the same release (font-fallback GC pauses, unrecycled CollectionView item views) |

### Also planned

| Item | Description |
|------|-------------|
| `openmaui doctor` | Done in 10.0.110.1: `OPENMAUI_DOCTOR=1 ./MyApp` reports .NET, session, compositor and its Wayland globals, EGL/GL and the render target that will be chosen, scale and its source, IME backend, and every optional native dependency with the install command for the detected distribution |
| xdg-desktop-portal layer | Done in 10.0.110.1: native D-Bus (Tmds.DBus) on one shared connection for FileChooser, OpenURI, Notification, Screenshot, Secret, Settings (live dark mode and accent), Inhibit, Background and Location, each with its previous fallback; `OPENMAUI_PORTALS=auto|prefer|off` (`docs/PORTALS.md`) |
| Deployment beyond AppImage | Done in OpenMaui.AppImage 1.3.0: `--format deb|rpm|all` next to AppImage and Flatpak (managed `.deb` writer, `rpmbuild` for `.rpm`), dependencies mapped per distro with WPE only recommended; verified by installing in Debian 13, Ubuntu 24.04 and Fedora 44 containers. Snap remains later |
| Multi-window round-out | Done in 10.0.110.1: live `Window.Page` replacement, Title, Width/Height, X/Y (X11), minimum/maximum size, `Stopped`/`Resumed`, drag-and-drop onto secondary windows, secondary windows in GTK mode |
| ARM64 hardening | Done in 10.0.110.1: the Wayland protocol shim is built and packaged for linux-arm64, and a linux-arm64 publish no longer ships the x86-64 shim; every native asset is available for arm64 (`docs/ARM64.md`). Not yet run on arm64 hardware |
| Stable-contract commitment | Done in 10.0.110.1: package validation against the previous release fails `dotnet pack` on an unrecorded breaking change; versioning, deprecation policy and support matrix in `docs/VERSIONING.md`, upgrade notes in `docs/MIGRATION.md` |
| Frame-accurate HTTP scrubbing | Done in 10.0.110.1: the drift was the KEY_UNIT seek flag, not HTTP; seeks are exact over HTTP and locally, serialised on a worker so rapid scrubbing cannot stall a range request |

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
| v10.0.101.2 | .NET 10 / MAUI 10.0.101 | Q3 2026 | Released: Phase 1 GPU presentation and Phase 2 WPE WebView + Blazor |
| v10.0.101.3 | .NET 10 / MAUI 10.0.101 | Q3 2026 | Released: Phase 3 conformance (scorecard, golden tests, the handlers and services it exposed) |
| v10.0.110.1 | .NET 10 / MAUI 10.0.110 | Q3 2026 | In development: MAUI 10.0.110 and the remaining roadmap: runtime scale, partial damage, Vulkan, zero-copy WebView and video, portals, multi-window round-out, third-party KPI, benchmarks and gates, ARM64, API compatibility gate, `openmaui doctor` |

## Feedback

- Issues: https://github.com/open-maui/maui-linux/issues

---

*Last updated: September 2026*
*Copyright 2025-2026 MarketAlly Pte Ltd*
