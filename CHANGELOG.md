# Changelog

All notable changes to this project will be documented in this file.

Version numbers are aligned with .NET / MAUI versions (e.g., OpenMaui 10.0.x targets .NET 10 / MAUI 10).

## [10.0.70.4] - unreleased

> Two-part release. First, stability and correctness hardening across the 10.0.70.x surfaces, driven by a deep code review of the newest subsystems — several crash-class and data-corruption-class bugs fixed. Second, a desktop-integration round-out: full X11 XDND drag-and-drop in both directions, IME surrounding-text, map polygon/circle overlays, a GTK print dialog, and an XEmbed tray fallback.

### Added

- **Outgoing drags on X11, and a backend-agnostic entry point.** `DragDropService.TryStartDrag(text)` routes to the native Wayland data-device drag when available and otherwise runs a full XDND source: XdndSelection ownership, pointer grab, `XdndAware` target discovery under the cursor, Enter/Position/Status negotiation, drop + `SelectionRequest` data delivery (`TARGETS`, `UTF8_STRING`, `text/plain` variants), Escape cancel, and 2 s finish timeouts. (Incoming X11 drops are under Fixed below.)
- **`GetPosition(relativeTo)` returns real view-relative coordinates** on every gesture args type (tap, swipe, pan, pinch, pointer, drag-starting, drag-over, drop). The position resolvers previously ignored the `relativeTo` element and always returned the window point; one shared resolver now translates against the target view's `ScreenBounds` (scroll-offset correct), returns the window point for `null`, and returns `null` for unresolvable elements per the MAUI contract. Also fixed the args-constructor selection that preferred `DragEventArgs`' resolver-less overload, which would have silently dropped positions.
- **MAUI drag/drop gesture recognizers now work.** `DragGestureRecognizer`: press + move past the pan threshold on a `CanDrag` view fires `DragStarting` (honoring `Cancel`), extracts the `DataPackage` text, and starts the native drag — previously drag-gesture detection didn't exist on this platform at all. `DropGestureRecognizer`: native drags are hit-tested to the drop-target view with correct per-view enter/leave/over transitions (repeat-fire `DragOver`, tracked on the recognizer owner so child crossings don't flap), `Drop` delivers text and file paths (`DataPackage.Properties["FilePaths"]`), `AllowDrop` and `AcceptedOperation == None` are honored and fed back to the native layer (`wl_data_offer.accept(NULL)` / `XdndStatus(accepted=0)`). Drop events now carry window coordinates on both backends (X11 XdndPosition root coords translated + DPI-scaled to match pointer dispatch). `DragDropService.Default` events keep firing unchanged — the gesture routing is additive. MAUI-internals access is cached reflection with graceful degradation; a MAUI signature drift logs and disables recognizer delivery without affecting the native protocol or service events. File/image payload *sourcing* for outgoing drags remains a TODO (text only).
- **`set_surrounding_text` — the IME loop is complete.** The focused `SkiaEntry`/`SkiaEditor`'s text, caret, and selection anchor are now pushed to the compositor via `zwp_text_input_v3` (≤4000-byte UTF-8 window centered on the caret, cut on code-point boundaries; spec-correct `set_text_change_cause`; pushes coalesced per frame and deduped; password entries excluded). The same read seam also feeds IBus via `ibus_input_context_set_surrounding_text` (code-point offsets), so X11 IME users benefit too.
- **Map polygon and circle overlays.** `LinuxMapHandler` now routes `IFilledMapElement` (polygons) and `ICircleMapElement` (circles) in addition to polylines — fill + stroke honored, circle radius projected through Mercator meters-per-pixel at the center latitude, world-wrap aware. `SkiaMap` gains `Polygons`/`Circles` collections (new `MapPolygon`/`MapCircle` types) for code-first use.
- **GTK print dialog.** `PrintService.ShowPrintDialogAsync()` shows a `GtkPrintUnixDialog` (printer, copies, page ranges, duplex, collate, scale, number-up, plus the printer's PPD driver options via GTK's CUPS backend) and returns the selection as CUPS-ready options for `PrintFile`. Non-blocking on the GLib loop; returns null with one warning when GTK isn't loadable. The Preview button surfaces as `PreviewRequested = true` for the app to render and open (GTK's automatic preview handoff only exists inside `GtkPrintOperation`). Note: GTK's virtual "Print to File" printer is not a CUPS destination — check the returned name against `EnumeratePrinters()`.
- **`PrintJobResult.Status`** (`Submitted` / `NothingToPrint` / `Failed`) so "nothing to print" is distinguishable from a real failure; `Succeeded` semantics unchanged.
- **XEmbed system-tray fallback.** When no StatusNotifierItem host exists, `TrayIcon` now docks a real XEmbed icon per the freedesktop System Tray Protocol (probe order: ayatana → appindicator → XEmbed → no-op). Left-click raises `Activated` (this backend supports it), right-click shows the menu via GTK (falling back to `Activated`), and icons re-dock automatically after a panel restart. Installs a process-global X error handler (chaining to any prior one) so a dying tray manager can't terminate the app.
- Legacy map tile-cache directories (pre-provider-keyed layout) are deleted by the startup sweep instead of lingering until the size cap reaches them.

### Fixed — Shell page lifecycle

- **`OnAppearing`/`OnDisappearing` now fire under Shell navigation.** They never had: `SkiaShell` swapped page content without sending lifecycle events, and even when sent, MAUI's `Page.SendAppearing` silently no-ops unless the page's parent chain reaches an Application-attached Window — which renderer-created pages never had. Three-part fix: `SkiaShell` tracks the current MAUI page and sends `Appearing`/`Disappearing` on section navigation, push/pop/pop-to-root, and theme-refresh re-renders; `LinuxViewRenderer` parents every page it creates into the real Shell hierarchy via `ShellContent.ContentCache` (which also populates `IShellContentController.Page`); and the initial page's Appearing is re-issued once the shell is fully attached, covering the startup ordering race. Any page that subscribes/unsubscribes event handlers in these overrides — a core MAUI pattern — now works.

### Fixed — Wayland core

- **All Wayland listener callback delegates are now rooted in fields.** Every listener registration (registry, seat, xdg_surface/toplevel, wm_base, buffer, decoration, fractional-scale, text-input, pointer, keyboard, data-device, primary-selection device) previously passed unrooted delegate temporaries to `Marshal.GetFunctionPointerForDelegate`; after any GC the native thunk was freed and the next compositor event jumped into freed memory. Repo-wide sweep; zero unrooted callback sites remain.
- **Drag sources are destroyed on `dnd_finished`.** A successful outgoing drag previously never cleaned up its `wl_data_source` — leaking the proxy and, worse, leaving the clipboard's self-paste short-circuit pointing at the dragged text (copy "A", drag "B", paste → "B"). Drag sources now have their own tracking, fully separate from clipboard selection bookkeeping.
- **`DragEventArgs.Accepted` now defaults to `true`.** Drops were silently rejected unless a `DragEnter` handler explicitly opted in, making DnD inert out of the box on both backends.
- **DnD protocol correctness:** rejection now sends a NULL mime (empty string is a *real* accept on the wire and could make source apps delete "moved" data); enter with a null offer no longer crashes; enter/motion coordinates are converted from `wl_fixed_t` and DPI-scaled (were 256× too large); `set_actions`/`finish` are gated on the actually-bound `wl_data_device_manager` version; `start_drag` uses the button-press serial (stale serials made compositors silently ignore drags); drop processing captures the offer and accepted mime before clearing shared state, closing races with late `leave` events and back-to-back drags.
- **Self-paste deadlock fixes:** `PrimarySelectionService.HasText` and `ClipboardService.HasText` were sync-over-async — freezing the GLib loop and, when our own process owned the selection, deadlocking permanently. Both are now zero-I/O probes of tracked native state. Pipe writes to paste requesters moved off the main thread; `receive()` reads re-validate the offer on the main thread and read on a background thread with an idle-based timeout that returns failure (engaging fallbacks) instead of silently-empty text. The self-paste short-circuits also dispatch pending events first, so a just-lost selection isn't answered with stale text.
- Primary selection can now be **cleared** (`SetTextAsync(null)` → `set_selection(NULL)`), and `TrySetPrimarySelectionText` fails gracefully when protocol symbols are missing.

### Fixed — X11

- **First working XDND drop path.** The XDND handlers existed but were never driven; `RequestDropData` was a stub that returned null while acking success — a source app performing a Move could delete the user's file with nothing delivered. The X11 window now announces `XdndAware`, routes `ClientMessage`/`SelectionNotify` into `DragDropService`, and implements the real transfer: `XConvertSelection` → `SelectionNotify` → property read (INCR-capable for large payloads) → `XdndFinished` reporting success only when data actually arrived.

### Fixed — Maps

- **Tile-cache images can no longer be disposed while the renderer is drawing them** — eviction/replacement disposal is deferred to a future main-loop iteration.
- **`MoveToRegion` now lands on the requested span.** The span→zoom conversion ignored viewport size (correct only for a 256-px-wide map; a 1280-px window under-zoomed by ~2.3 levels). Pre-layout calls are deferred until first layout.
- **`Map.VisibleRegion` is now written back** after pan/zoom (was permanently null), with reentrancy guards; `VisibleRegion` removed from the input property mapper.
- Marker vs info-window click semantics now match other platforms (first tap selects + `MarkerClicked`; tapping the selected pin fires `InfoWindowClicked`); pin hit-testing works inside scrolled containers; pins/polylines render (and hit-test) in wrapped world copies across the antimeridian; pan clamps at the Mercator latitude limit instead of sticking past the pole; mutating a `Pin` in place (Location/Label) now updates its marker.
- **Tile service hardening:** downloads capped at 2 concurrent connections per the OSM tile policy; failed fetches negatively cached (15 s retry window) instead of re-requested every frame; at most one redraw per in-flight tile; inflight dedup via `Lazy` (no double downloads/leaked images); true LRU memory cache; disk cache keyed by tile-server template (switching providers can't serve stale styles — note: tiles cached under the old layout are orphaned until swept) and bounded at 256 MB with oldest-first pruning; `Dispose` on the shared `Default` singleton is a safe no-op; cache-dir failure degrades to memory-only.
- **HiDPI:** at device scale ≥ 1.5, tiles are fetched one zoom level deeper and rendered at half logical size — crisp maps on HiDPI displays.

### Fixed — Input robustness

- **A view exception in an input handler no longer crashes the app.** Pointer, key, text-input, and scroll handlers run synchronously inside the native Wayland/X11 event callback; an exception thrown by view code unwound through the native dispatch frame and aborted the whole process (SIGABRT). All input subscriptions now route through a `Guarded` wrapper that logs the exception instead. (Same protection applied to the opt-in GTK-surface input path.)
- **Maps: scrolling to minimum zoom no longer crashes.** On a tall HiDPI viewport at zoom 0 the world is smaller than the window, so the `VisibleRegion` write-back could build a `MapSpan` from an out-of-range longitude (the map pans horizontally without bound) — MAUI's `Location` constructor rejects longitudes outside [-180, 180] and threw. `LinuxMapHandler.PushVisibleRegion` now normalizes longitude into range, clamps latitude and the span degrees, and bails on any non-finite value.

### Changed — Tray icons

- **Hide/Show now toggles the indicator's status instead of destroying and recreating it.** `TrayIcon.Hide` sets the StatusNotifierItem to `Passive` (GNOME's AppIndicator host hides it entirely; KDE Plasma moves it to the hidden/expander area) and keeps the object alive so `Show` re-activates it instantly; real teardown happens on `Dispose`. Destroying per-hide was worse: libappindicator + KDE Plasma only drop a tray item when the owning **process exits**, so a destroyed indicator left a dead, still-cached entry in Plasma's overflow and blocked a clean re-show. Note for KDE users: getting a hidden icon out of the overflow/expander entirely is a Plasma tray-configuration choice (right-click tray → Configure → set the entry to "Shown"/"Hidden"), not something an SNI app controls at runtime. The XEmbed fallback backend (non-SNI desktops) still removes the icon fully on Hide.

### Fixed — Tray icons

- `app_indicator_set_icon_full` and `app_indicator_set_label` were bound with a missing third parameter, passing a garbage pointer on every icon/tooltip update (undefined behavior on all desktops). Correct 3-arg signatures now.
- Menu-item callback lifetime is now GTK-owned via `GClosureNotify` — the old code freed delegate `GCHandle`s on menu rebuild while a popped-open old menu could still invoke them (use-after-free), and `Remove` leaked the indicator (now `g_object_unref`'d, also fixing duplicate StatusNotifierItem registrations on Hide/Show cycles).
- All GTK/appindicator calls marshal to the GLib main thread; `TrayIcon.Activated` docs now state plainly that the AppIndicator backend cannot raise it (SNI left-click opens the menu by design).

### Fixed — Printing

- The blocking CUPS submit no longer runs on the UI thread; new `EnumeratePrintersAsync`/`PrintFileAsync` variants, and the sync APIs warn once when called on the main thread.
- `PrintSkiaPagesAsync` pages are committed only after `renderPage` confirms them (via `SKPicture` replay), so returning `false` on the first page prints nothing instead of a blank page; the temp PDF is created owner-only (0600).

### Fixed — MediaElement

- `MediaHardwareAccelerationService.Apply` guards on `gst_is_initialized()` (an empty pre-init registry previously produced misleading "install gstreamer1-vaapi" advice and silently did nothing).
- Original decoder ranks are snapshotted before modification and `Apply(Auto)` restores them — toggling `Prefer`/`Disable` at runtime is now fully reversible. Rank changes remain process-global (documented).

## [10.0.70.3] - 2026-06-03

> Re-release of the 10.0.70.2 work to include the `OpenMaui.Controls.Linux.Maps` sibling package, which was authored alongside the rest of 10.0.70.2 but missing from the release workflow's pack step. Added pack steps for Maps in `.gitea/workflows/release.yml` + `.gitea/workflows/ci.yml`, and brought the `.github` workflows in line (they previously packed only the main package — Hosting, MediaElement, Maps, and Templates are now explicit pack steps there too). No code changes vs. 10.0.70.2.

## [10.0.70.2] - 2026-06-02

> Roadmap follow-up: complete the Wayland IME/text loop, ship the Linux primary-selection clipboard, native Wayland drag-and-drop, hardware video acceleration tuning, system tray menus, CUPS printing, and an OpenStreetMap-backed maps backend.

### Added
- **`IInputContext.DeleteSurrounding(int beforeChars, int afterChars)`.** Completes the Wayland `zwp_text_input_v3` IME loop — `WaylandTextInputV3Service` now applies `delete_surrounding_text` batches before commits and routes them into the focused `SkiaEntry` / `SkiaEditor`. The Wayland protocol counts in UTF-8 bytes; the service does the byte→char-count conversion (clamping on multi-byte boundaries, handling surrogate pairs) so the input control's implementation just works in UTF-16 indices. Also fixed a sibling gap: the service previously raised events but never called back into `_context` — now matches `IBusInputMethodService`.
- **`PrimarySelectionService`** — public Linux service for the X11/Wayland *primary selection* (middle-click paste), backed by a fresh `zwp_primary_selection_v1` Wayland binding (new partial `WaylandWindow.PrimarySelection.cs`, parallels the `wl_data_device_manager` clipboard) with `wl-paste --primary` / `xclip -selection primary` / `xsel --primary` subprocess fallbacks for older compositors and X11. `SkiaEntry` and `SkiaEditor` now push the selected range to the primary on drag-end, and middle-click pastes at the click position — matches every native Linux toolkit's behavior.

### Changed
- Generated `primary-selection-unstable-v1` bindings into the `libopenmaui_wl.so` shim (`native/build.sh`). The four new `zwp_primary_selection_*_interface` symbols are dlsym'd alongside the existing protocols at startup.
- **First functional Linux DnD path.** `DragDropService` was scaffolding only — its X11/XDND code was never wired and `Initialize()` was never called. Made the service a process-wide singleton (`DragDropService.Default`) and added internal `Raise*` push hooks so any backend can drive the same protocol-agnostic `DragEnter`/`DragOver`/`DragLeave`/`Drop` events. New `WaylandWindow.DragDrop.cs` partial turns the previously-no-op `wl_data_device` DnD handlers into a real implementation: per-offer MIME negotiation (preferring `text/uri-list` for file drops, then text MIME variants), `wl_data_offer.accept`/`set_actions`/`finish` per protocol v3, pipe-based drop receive, and outgoing `wl_data_device.start_drag` via `WaylandWindow.TryStartDrag`. File drops auto-decode `file://` URIs onto `DragData.FilePaths` so consumers don't have to.
- **Hardware video acceleration tuning** for `OpenMaui.Controls.Linux.MediaElement`. New `MediaHardwareAcceleration` enum (`Auto` / `Prefer` / `Disable`) and `MediaHardwareAccelerationService` that bumps the GStreamer registry rank of known HW decoder factories (VA-API, NVDEC, V4L2-stateless, Intel MediaSDK / oneVPL). Wired into the builder as a new `UseLinuxMediaElement(MediaHardwareAcceleration)` overload; the existing no-arg call still defaults to `Auto`. Silently no-ops when no HW decoder plugin is installed; debug-logs the list of bumped factories when one is. Also exposes `MediaHardwareAccelerationService.EnumerateAvailableHardwareDecoders()` so apps can introspect what's installed.
- **System tray icons** via `TrayIcon` / `TrayIconService`. Backed by libappindicator3 / libayatana-appindicator3 (StatusNotifierItem over the session bus), which gets the icon rendered on every modern desktop that ships an SNI host — GNOME w/ AppIndicator extension, KDE, MATE, Cinnamon, XFCE. Supports icon, title, tooltip, an `Activated` event, and a mutable `MenuItems` collection (label + Action + IsEnabled + IsSeparator). Probe at startup picks `libayatana-appindicator3.so.1` first (Ubuntu/Debian and most modern installs), then `libappindicator3.so.1` (Fedora, older distros); falls back to a `NullTrayBackend` (Show/Hide no-op) if neither is installed.
- **CUPS printing** via `PrintService`. Three capabilities: `EnumeratePrinters()` lists every printer queue the host knows about (including system default and per-user `lpoptions` instances), `PrintFile(printer, path, jobTitle, options)` submits an existing PDF/PS/image/text file (CUPS picks the matching filter chain via MIME sniff), and `PrintSkiaPagesAsync(printer, renderPage, pageSize)` renders Skia draw operations into a multi-page PDF via `SKDocument.CreatePdf` and prints the result — the high-level path most apps want. Reports `IsAvailable=false` and returns empty/failed results without throwing when libcups isn't installed.
- **Maps integration** via new sibling package `OpenMaui.Controls.Linux.Maps` (mirrors the MediaElement opt-in pattern). Ships:
  - `LinuxMapHandler` — `Microsoft.Maui.Controls.Maps.Map` works in Linux apps with the same XAML used on iOS/Android/Windows. Property mappers cover `VisibleRegion`, `IsScrollEnabled`, `IsZoomEnabled`, `Pins`, and `Elements` (polyline subset of `IMapElement`).
  - `SkiaMap` standalone view — drop into any code-first Skia hierarchy. Pan with mouse drag, zoom with scroll wheel (toward cursor), pin & polyline overlays, OSM attribution overlay.
  - `OsmTileService` — HTTP fetch from `tile.openstreetmap.org` + persistent disk cache under `$XDG_CACHE_HOME/openmaui/osm-tiles`, in-memory SKImage LRU on top. URL template is swappable for self-hosted / commercial tile servers.
  - `MercatorProjection` — geographic ↔ tile-grid ↔ pixel math, with the standard ±85.05° latitude clamp.
  - `UseLinuxMaps()` builder extension — opt-in registration, no-op on non-Linux platforms so the same `MauiProgram.cs` works cross-platform.

## [10.0.70.1] - 2026-06-02

> MAUI 10.0.70 alignment + default-template fixes (gitea #26).

### Changed
- Bumped `Microsoft.Maui.Controls` / `Microsoft.Maui.Graphics` / `Microsoft.Maui.Graphics.Skia` references from 10.0.60 to 10.0.70.

### Fixed
- **Default Linux template no longer fails with CS1508.** The XAML template's csproj had `<EmbeddedResource Include="Resources\**\*" />` alongside `<MauiXaml Update="**/*.xaml" />`, which re-embedded `Resources/Styles/Colors.xaml` and `Styles.xaml` under the same manifest resource ID that the XAML compiler had already produced. Replaced the broad embedded-resource glob with proper `MauiFont` / `MauiImage` item groups in both `openmaui-linux-app` and `openmaui-linux-xaml-app` templates.
- **Wayland protocol shim now loads in framework-dependent builds.** The NuGet runtime asset graph places `libopenmaui_wl.so` under `runtimes/linux-x64/native/`, which services DllImport probing but not the explicit `dlopen()` the Wayland window does. Two-pronged fix: (a) `build/OpenMaui.Controls.Linux.targets` now declares the native asset as `<None CopyToOutputDirectory="PreserveNewest">` so it lands next to `OpenMaui.Controls.Linux.dll` in the consumer's output, and (b) `WaylandWindow.TryLoadProtocols()` additionally probes `runtimes/linux-x64/native/` and `runtimes/linux-arm64/native/` under the assembly directory, so it succeeds in either deployment layout.
- **MAUI Essentials platform implementations now register correctly on Linux.** MAUI 10 uses two static-field/setter naming conventions side-by-side — `SetDefault` / `defaultImplementation` (Preferences, SecureStorage, Browser, Launcher, Battery, …) and `SetCurrent` / `currentImplementation` (Connectivity, AppInfo, DeviceInfo, AppActions). `EssentialsPatches` only checked `defaultImplementation` and silently failed for the second set, leaving the portable stubs in place. Replaced the registration helpers with a `TrySetEssential` that prefers the public `Set*` setter (renames-tolerant) and falls back to either field name.
- **`PreferencesService` constructor no longer throws.** It used to call `AppInfo.Current?.Name` to derive the XDG config path, which on the portable Essentials assembly returns a stub whose property getters throw `NotImplementedInReferenceAssemblyException`. Made the path lazy and reordered `EssentialsPatches.Apply()` to register AppInfo/DeviceInfo before Preferences and FilePicker.

## [10.0.60.13] - 2026-05-16

> MediaElement / video playback via opt-in `OpenMaui.Controls.Linux.MediaElement` sibling package.

### Added
- New NuGet package **`OpenMaui.Controls.Linux.MediaElement`** that adds a Linux backend for `CommunityToolkit.Maui.MediaElement`. Backed by GStreamer (playbin + appsink → BGRA → SKImage). Opt-in: install only when your app uses MediaElement.
- `UseLinuxMediaElement()` builder extension (no-op on non-Linux for cross-platform `MauiProgram.cs` parity).
- `MediaDemo` sample in `maui-linux-samples` demonstrating play/pause/stop/seek/volume/mute and arbitrary URL/file loading.
- Hardware-accelerated decode auto-negotiated via VAAPI (Intel/AMD) or NVDEC (NVIDIA) when the corresponding GStreamer plugin packages are installed.
- Frame backpressure on the appsink callback — only one decoded frame is in flight to the main thread at a time, so seeks don't flood the renderer with stale frames.

### Known limitations
- HTTP-streamed backward seeks can land 1-2 seconds off the requested position (byte-range re-request + decode-and-discard latency). Local files seek frame-accurately. Documented in the MediaDemo README.
- Variable playback rate (`Speed`) and `ShouldShowPlaybackControls` are stubbed for v1.

## [10.0.60.12] - 2026-05-16

> Native `zwp_text_input_v3` IME for the Wayland-native path.

### Added
- `WaylandTextInputV3Service` implements `IInputMethodService` over the `zwp_text_input_v3` protocol. Routes compositor-mediated IMEs (GNOME Pinyin/Hangul/Anthy, native Fcitx5) directly without going through IBus over DBus.
- `InputMethodServiceFactory` prefers `text-input-v3` when on a native Wayland session with the protocol bindable; falls back to IBus/Fcitx5/XIM otherwise.
- `MAUI_INPUT_METHOD=wayland` env-var override forces the native path.

### Changed
- `LinuxApplication.Lifecycle`: skip setting `XCURSOR_SIZE` on native Wayland — modern compositors handle cursor scaling via `wp_cursor_shape_v1` natively and the pre-set caused visibly oversized cursors at fractional scale.

### Fixed
- `WaylandWindow.Keyboard.HandleModifiers`: xkb modifier bitmask was raw-cast to MAUI `KeyModifiers`, which has different numeric values. Ctrl appeared as Alt across the entire Wayland keyboard path (Ctrl+C / Ctrl+V did nothing). Now uses `xkb_state_mod_name_is_active` to map by canonical name.

## [10.0.60.11] - 2026-05-16

> Native `wl_data_device_manager` clipboard.

### Added
- `Window/WaylandWindow.Clipboard.cs`: binds `wl_data_device_manager` + per-seat `wl_data_device`. `TrySetClipboardText` / `TryGetClipboardTextAsync` for native copy/paste. No more `wl-copy` / `wl-paste` subprocess overhead and no requirement on the `wl-clipboard` system package.
- `ClipboardService` and `SystemClipboard` prefer the native path on Wayland; fall back to wl-paste/wl-copy/xclip/xsel subprocesses when the native path isn't ready.
- Self-paste short-circuit: when our own source owns the clipboard, return buffered text directly instead of round-tripping through wayland (avoids a deadlock with the main thread).
- Per-seat input-serial capture from `wl_keyboard.key` events alongside the existing pointer-button capture so `set_selection` works for Ctrl+C as well as menu-driven copy.

### Architectural invariant
- Owned `wl_data_source` instances are kept alive in a dictionary until the compositor fires `cancelled()`. Destroying them synchronously when replacing the selection races with the new `set_selection` and the compositor clears the clipboard (`selection: NULL`).

## [10.0.60.10] - 2026-05-16

> Client-side decorations for GNOME-Wayland (and other compositors that refuse SSD).

### Added
- `Window/WaylandCsdRenderer.cs`: draws a 32px logical titlebar in Skia with theme-aware background, window title, and right-aligned minimize / maximize-or-restore / close buttons. Cached button bounds for hit-test.
- `WaylandWindow.Decoration.cs` listens for `zxdg_toplevel_decoration_v1.configure` and flips `_useCsd` true when the compositor returns `client_side` (Mutter / GNOME) or when the decoration manager isn't advertised at all.
- `LinuxApplication.Input` adjusts pointer Y by `CsdTitlebarHeightLogical` when CSD is active, so view-tree hit-tests stay in their own coordinate space.
- `xdg_toplevel.move` / `xdg_toplevel.resize` (with all 8 edge enums) / `xdg_toplevel.set_maximized` / `xdg_toplevel.unset_maximized` / `xdg_toplevel.set_minimized` request methods.
- `MAUI_PREFER_CSD=1` env var forces CSD on for testing under compositors that would otherwise honor SSD.

### Validated
- KDE-Wayland (SSD unchanged), GNOME-Wayland (CSD active automatically), X11 (unchanged). Theme toggle redraws CSD bg/text correctly.

## [10.0.60.9] - 2026-05-05

> Major rev. Aligned with MAUI 10.0.60 and added native Wayland support alongside the existing X11/XWayland path.

### Added — Wayland-native main window
- Runtime detection: Wayland is preferred when `WAYLAND_DISPLAY` is set; falls back to X11/XWayland when `MAUI_PREFER_X11=1` is set or only `DISPLAY` is available.
- `LinuxApplicationOptions.DisplayServer` programmatic override for forcing a specific server (`Auto`, `Wayland`, `X11`).
- `wl_pointer.set_cursor` + libwayland-cursor: themed cursor support reading from `XCURSOR_THEME` / `XCURSOR_SIZE`. Re-applies on every pointer enter so the requested cursor persists across surface re-entry.
- Keyboard via xkbcommon: proper keymap parsing, modifier tracking via `xkb_state`, keysym translation for the MAUI `Key` enum, and UTF-8 text input (control characters filtered out). Supports layout, dead keys, and keymap reloads.
- `zxdg_decoration_manager_v1`: requests server-side decorations on compositors that honor it (KDE, Sway, wlroots-based). GNOME-Wayland still needs the X11 fallback until CSD lands.
- `wp_fractional_scale_v1`: receives the compositor's preferred scale; full HiDpiService propagation is wired but `LinuxApplication.DpiScale` updates are a follow-up for live changes.
- `IDisplayWindow` cross-platform contract; `IX11Surface` interface for X11-only consumers (WebView reparenting, RandR queries) — pattern-match where needed.

### Added — Cursor scaling fix (issue #23)
- X11 path now uses `XcursorLibraryLoadCursor` (themed PNG cursors) instead of `XCreateFontCursor` (legacy bitmap glyphs). `XCURSOR_SIZE` is computed from detected HiDPI scale before `XOpenDisplay`, so cursors render correctly on fractional-scaling sessions (notably GNOME Wayland via XWayland).

### Changed — MAUI alignment
- Microsoft.Maui.Controls: 10.0.50 → 10.0.60
- Microsoft.Maui.Graphics: 10.0.50 → 10.0.60
- Microsoft.Maui.Graphics.Skia: 10.0.50 → 10.0.60

### Changed — Architecture
- **`SkiaView` slim-down** (architecturally significant): 31 `BindableProperty` declarations that duplicated `VisualElement` / `View` / `Element` properties were removed. Each property now reads live from the bound `MauiView` (with backing-field fallback for direct-XAML scenarios where no MauiView is set). `MauiView.PropertyChanged` is subscribed to fire `Invalidate` / `InvalidateMeasure` / property-specific callbacks. **`AppThemeBinding`, `VisualStateManager`, `Triggers`, `Behaviors`, `Style` inheritance, and `ControlTemplate` now flow through the cross-platform tree without proxy code.** `RefreshPageForThemeChange` and the old tree-walking refresh helpers are deleted.
- **`SkiaRenderingEngine.Current` static replaced with `IRenderContext`**: each view tree gets its own render context, propagated through `AddChild` / `InsertChild`. Views now look up resources via `RenderContext?.Resources` instead of a process-global. Unblocks unit-testable views and multi-window scenarios.
- **Two `Run` paths consolidated**: `LinuxProgramHost.Run<TApp>` collapsed into a thin wrapper that builds the `MauiApp` and delegates to `LinuxApplication.Run`. Single canonical bootstrap path; theme handling, GTK init, `CreateWindow` handshake, and event loop all live in one place.
- **Multi-window safety in `LinuxViewRenderer`**: `OnShellNavigated` is now an instance method capturing its own shell pair, eliminating cross-pollination when multiple renderers exist.

### Removed — Dead code (~503 LOC)
- `Rendering/GpuRenderingEngine.cs`, `Rendering/LayeredRenderer.cs`, `Rendering/GpuStats.cs`, `Rendering/RenderLayer.cs`: never instantiated, never wired to GPU presentation.
- `LinuxApplicationOptions.UseHardwareAcceleration`, `ForceDemo`: read by nothing.
- `Services/X11DisplayWindow.cs`, `Services/WaylandDisplayWindow.cs`: vestigial event-forwarding wrappers; concrete window classes implement `IDisplayWindow` directly now.

### Fixed — Threading and lifecycle
- `Hosting/LinuxTicker`: animations dispatched through `LinuxDispatcher.Main` instead of firing on a thread-pool thread (matches the contract documented in `docs/THREADING.md`; eliminates a class of latent races between Tick handlers and the render loop).
- `LinuxApplication._isRedrawing`: read-modify-write race fixed with `Interlocked.CompareExchange`.
- X11 event loop: replaced `while (running) { ... Thread.Sleep(1); }` busy-loop with `poll()` on the X display fd at 16ms timeout. Idle CPU drops from ~100% of one core to near zero.
- `GDK_BACKEND=x11` is no longer set unconditionally; respects user override (e.g. for native-Wayland GTK testing).
- `XCURSOR_SIZE` configured before `gtk_init_check` so GTK's own cursor loading sees the right size.

### Fixed — Other
- `SystemThemeService` DI registration was creating a fresh instance instead of returning `Instance` (would have failed at runtime since the constructor is private). Now returns the singleton.
- Removed unused `HighContrastService` DI registration; consumers construct directly.

### Known follow-ups
- Client-side decoration on GNOME-Wayland (custom titlebar with min/max/close, drag-to-move, edge resize). Until then, GNOME-Wayland users may want `MAUI_PREFER_X11=1`.
- `wp_fractional_scale_v1` event propagation through `HiDpiService` for live scale changes.
- `text-input-v3` IME on the Wayland-native path. X11 IBus/Fcitx5 services continue to cover the X11 fallback.
- `wl_data_device_manager` native clipboard. Existing `wl-copy`/`wl-paste` subprocess path is retained and works correctly.
- `Tmds.DBus` migration for `Fcitx5InputMethodService` to replace the `dbus-monitor` subprocess.
- Handler property-mapper cleanup: many handlers still set MauiView-equivalent properties on SkiaView. With Stage 7's slim-down those writes are no-ops in handler-bound scenarios; cleanup is a low-risk follow-up.

## [10.0.50.1] - 2026-03-23

> Upgraded to MAUI 10.0.50. Version aligned with Microsoft.Maui.Controls 10.0.50.

### Changed
- Microsoft.Maui.Controls: 10.0.41 → 10.0.50
- Microsoft.Maui.Graphics: 10.0.41 → 10.0.50

### Added
- MAUI Shapes support: Ellipse, Line, Rectangle, Polygon, Polyline, Path
- SkiaEllipse: filled/stroked ellipse with Brush support
- SkiaLine: line between two points with configurable stroke
- SkiaRectangle: rectangle with optional RadiusX/RadiusY rounded corners
- SkiaPolygon: closed polygon with EvenOdd/Winding fill rules
- SkiaPolyline: open connected points with stroke and optional fill
- SkiaPath: arbitrary geometry via PathGeometry-to-SVG conversion with support for Line, Bezier, QuadraticBezier, Arc, PolyLine, PolyBezier, and PolyQuadraticBezier segments
- Handlers: EllipseHandler, LineHandler, RectangleHandler, PolygonHandler, PolylineHandler, PathHandler
- All shape handlers registered in LinuxMauiAppBuilderExtensions

### Fixed
- Harmony patches for Preferences (MAUI 10.0.50 removed Platform prefix, Get/Set are now generic)
- Harmony patches for SecureStorage (PlatformSetAsync parameter renamed from value to data)
- Both patches now discover methods dynamically instead of hardcoding signatures

## [10.0.41] - 2026-03-08

> Upgraded to .NET 10 / MAUI 10.0.41. Version aligned with MAUI 10.0.41.

### Changed
- Target framework: net9.0 → net10.0
- Microsoft.Maui.Controls: 9.0.40 → 10.0.41
- SkiaSharp: 2.88.9 → 3.119.2
- HarfBuzzSharp: 7.3.0.3 → 8.3.1.3
- Svg.Skia: 1.0.0 → 3.4.1

### Fixed
- GRContext.GetResourceCacheLimits replaced with GetResourceCacheLimit (SkiaSharp 3.x API)
- DatePicker handler updated for nullable DateTime properties (MAUI 10 IDatePicker change)
- TimePicker handler updated for nullable TimeSpan property (MAUI 10 ITimePicker change)
- FontFallbackManager thread-safety: Dictionary → ConcurrentDictionary

## [9.0.40] - 2026-03-07

> Version aligned with MAUI 9.0.40. Previously released as 1.0.0.

### Added
- 35+ Skia-rendered controls: Button, Label, Entry, Editor, CheckBox, Switch, RadioButton, Slider, Stepper, Picker, DatePicker, TimePicker, SearchBar, Image, ImageButton, ProgressBar, ActivityIndicator, BoxView, Border, Frame, ScrollView, CollectionView, CarouselView, IndicatorView, SwipeView, RefreshView, GraphicsView, WebView, MenuBar
- Navigation: NavigationPage, TabbedPage, FlyoutPage, Shell
- Full XAML support with BindableProperty for all controls
- Visual State Manager integration (Normal, PointerOver, Pressed, Focused, Disabled)
- Data binding (OneWay, TwoWay, OneTime) with IValueConverter support
- XAML styles, StaticResource, DynamicResource, merged ResourceDictionaries
- X11 display server support with full input handling
- Wayland support with XWayland fallback
- SkiaSharp hardware-accelerated rendering with dirty region optimization
- AT-SPI2 accessibility support (screen reader integration)
- High contrast mode detection and color palette support
- Input method support (IBus, Fcitx5, XIM)
- HiDPI automatic scale factor detection (GNOME, KDE, X11)
- Platform services: Clipboard, FilePicker, FolderPicker, Notifications, GlobalHotkeys, DragDrop, Launcher, Share, SecureStorage, Preferences, Browser, Email, SystemTray, VersionTracking, AppActions
- Gesture recognition: Tap, Pan, Swipe, Pinch, Pointer, Drag/Drop
- Project templates: `openmaui-linux` (code-based) and `openmaui-linux-xaml` (XAML-based)
- Visual Studio extension with project templates and launch profiles
- DiagnosticLog centralized logging infrastructure (conditional on DEBUG builds)
- Configurable gesture thresholds (SwipeMinDistance, SwipeMaxTime, etc.)
- Exception-safe rendering pipeline
- SafeHandle wrappers for native interop (GTK, X11, GObject)
- Performance benchmarks for rendering pipeline (541 passing tests)
- Threading model and DI migration documentation

### Fixed
- Native resource leaks: GTK signal disconnection, X11 cursor freeing, CSS provider unref, WebKit dlclose
- 27 empty catch blocks replaced with DiagnosticLog for debuggability
- GestureManager memory leak (view tracking dictionaries now cleaned up on dispose)
- Text binding recursion guard in EntryHandler
- Rendering pipeline crash protection (exceptions in view Draw no longer crash the app)

## [1.0.0] - 2026-03-06 [DEPRECATED]

> Superseded by 9.0.40. Identical codebase, version renumbered to align with MAUI versioning.

## [1.0.0-rc.1] - 2026-02-01

### Added
- 100% .NET MAUI API compliance - all public APIs use MAUI types
- 217 passing unit tests

## [1.0.0-preview.1] - 2025-06-01

### Added
- Initial preview release
- Core rendering engine
- Basic control set
