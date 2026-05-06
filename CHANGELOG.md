# Changelog

All notable changes to this project will be documented in this file.

Version numbers are aligned with .NET / MAUI versions (e.g., OpenMaui 10.0.x targets .NET 10 / MAUI 10).

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
