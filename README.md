# OpenMaui Linux Platform

A comprehensive Linux platform implementation for .NET MAUI using SkiaSharp rendering.

[![NuGet](https://img.shields.io/nuget/v/OpenMaui.Controls.Linux)](https://www.nuget.org/packages/OpenMaui.Controls.Linux)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-open--maui-181717?logo=github)](https://github.com/open-maui/maui-linux)

**Developed by [MarketAlly Pte Ltd](https://marketally.ai)**
**Lead Architect: David H. Friedel Jr.**

## Overview

This project brings .NET MAUI to Linux desktops with native X11/Wayland support, hardware-accelerated Skia rendering, and full platform service integration.

### Key Features

- **Full Control Library**: 50+ controls including Button, Label, Entry, Shapes, CarouselView, RefreshView, SwipeView, and more
- **Native Integration**: First-class X11 *and* native Wayland support (xdg-shell, wp_viewporter, fractional-scale-v1, zxdg_decoration_manager_v1, zwp_primary_selection_v1) — programmatically selectable or auto-detected
- **Accessibility**: AT-SPI2 screen reader support and high contrast mode
- **Platform Services**: Native `wl_data_device_manager` clipboard *and* drag-and-drop (no `wl-clipboard` subprocess required), `zwp_primary_selection_v1` middle-click paste, file picker, notifications, global hotkeys, CUPS printing, system tray icons via StatusNotifierItem
- **Input Methods**: IBus / XIM on X11 + native `zwp_text_input_v3` on Wayland with full `delete_surrounding_text` round-trip for compositor-integrated IMEs (GNOME Pinyin/Hangul/Anthy, native Fcitx5)
- **High DPI**: Automatic scale factor detection for GNOME, KDE, and X11; fractional scale handled via Wayland viewporter for pixel-exact rendering at non-integer scales (1.25x, 1.5x, 1.75x)
- **Theming**: AppThemeBinding live propagation across the entire view tree — CollectionView items, pushed pages, Shell content, and flyout regions all flip on theme toggle
- **Window decorations**: Server-side decorations (KDE/Sway) or client-side titlebar drawn in Skia with full drag/resize/close/maximize/minimize (GNOME/Mutter)
- **MediaElement**: Opt-in `OpenMaui.Controls.Linux.MediaElement` package backs `CommunityToolkit.Maui.MediaElement` with GStreamer (playbin + appsink → Skia). `MediaHardwareAcceleration.Prefer` boosts VA-API / NVDEC / V4L2 / MediaSDK decoder ranks when those plugins are installed
- **WebView**: WPE WebKit composited inside the Skia tree (no GTK widget, no reparenting) on native Wayland and X11, with context menus, clipboard, JavaScript dialogs, file chooser, permissions, web notifications, downloads, spell checking, link cursors, `EvaluateJavaScriptAsync` results and a backend-neutral WebKit content API; WebKitGTK remains the GTK-mode fallback
- **Blazor Hybrid**: Opt-in `OpenMaui.Controls.Linux.Blazor` package backs `BlazorWebView` (Microsoft.AspNetCore.Components.WebView.Maui) on the WPE WebView
- **Maps**: Opt-in `OpenMaui.Controls.Linux.Maps` package backs `Microsoft.Maui.Controls.Maps` with OpenStreetMap raster tiles in Skia — pan/zoom, pin & polyline overlays, persistent XDG tile cache. Plus a standalone `SkiaMap` view for code-first map UI

## Quick Start

### Installation

```bash
# Install the templates
dotnet new install OpenMaui.Linux.Templates

# Create a new project (choose one):
dotnet new openmaui-linux -n MyApp           # Code-based UI
dotnet new openmaui-linux-xaml -n MyApp      # XAML-based UI (recommended)

cd MyApp
dotnet run
```

### Manual Installation

```bash
dotnet add package OpenMaui.Controls.Linux
```

### Optional: MediaElement (video / audio playback)

`CommunityToolkit.Maui.MediaElement` on Linux requires the opt-in sibling package that adds the GStreamer-backed handler:

```bash
dotnet add package CommunityToolkit.Maui.MediaElement
dotnet add package OpenMaui.Controls.Linux.MediaElement
```

Then in your `MauiProgram.cs`:

```csharp
builder
    .UseMauiApp<App>()
    .UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false)
    .UseLinux()
    .UseLinuxMediaElement();   // Linux backend; no-op on Windows/Android/iOS/macCatalyst
```

System dependencies (GStreamer + plugin sets):

```bash
# Fedora
sudo dnf install gstreamer1-plugins-good gstreamer1-plugins-bad-free \
                 gstreamer1-plugins-ugly-free gstreamer1-libav gstreamer1-vaapi

# Ubuntu/Debian
sudo apt install gstreamer1.0-plugins-good gstreamer1.0-plugins-bad \
                 gstreamer1.0-plugins-ugly gstreamer1.0-libav gstreamer1.0-vaapi
```

`gstreamer1-vaapi` enables hardware-accelerated decode on Intel/AMD GPUs; substitute the nvdec plugin for NVIDIA. Software decode works without either.

To explicitly bias playbin toward the hardware decoders when they are installed:

```csharp
using Microsoft.Maui.Platform.Linux.MediaElement.Services;

builder.UseLinuxMediaElement(MediaHardwareAcceleration.Prefer);
```

`Auto` keeps the default behavior, `Prefer` bumps HW decoder factory ranks above SW, and `Disable` demotes them.

### Optional: Maps (OpenStreetMap)

`Microsoft.Maui.Controls.Maps` on Linux uses the opt-in sibling package that adds an OSM raster-tile renderer:

```bash
dotnet add package Microsoft.Maui.Controls.Maps
dotnet add package OpenMaui.Controls.Linux.Maps
```

Then in your `MauiProgram.cs`:

```csharp
builder
    .UseMauiApp<App>()
    .UseMauiMaps()
    .UseLinux()
    .UseLinuxMaps();   // Linux backend; no-op on Windows/Android/iOS/macCatalyst
```

Tiles are fetched on first view and cached under `$XDG_CACHE_HOME/openmaui/osm-tiles`.

`Map.MapType` selects the layer style:

- **Street** — OpenStreetMap standard raster (`© OpenStreetMap contributors`).
- **Satellite** — Esri "World Imagery" (keyless aerial basemap).
- **Hybrid** — the satellite base with an Esri transparent labels/boundaries overlay.

The active layer's attribution is drawn in the on-map overlay. The Esri layers are keyless but governed by [Esri's terms of use](https://www.esri.com/en-us/legal/terms/full-master-agreement), not the OSM tile policy — confirm those terms for production use, or redirect the sources at your own imagery.

Each layer is a `TileSource` with a settable URL template (the `{z}/{x}/{y}` placeholder position sets the axis order — OSM uses `{z}/{x}/{y}`, ArcGIS uses `{z}/{y}/{x}`). To use a self-hosted or commercial tile server, redirect a layer at startup:

```csharp
using Microsoft.Maui.Platform.Linux.Maps.Services;

MapTileLayers.Street.UrlTemplate = "https://my-tiles.example.com/{z}/{x}/{y}.png";
// (OsmTileService.Default.UrlTemplate still works as a shortcut for the Street layer.)
```

For code-first map UI without `Microsoft.Maui.Controls.Maps`, the package also exposes a standalone `SkiaMap` view that subclasses `SkiaView`:

```csharp
using Microsoft.Maui.Platform.Linux.Maps.Views;

var map = new SkiaMap { CenterLatitude = 35.68, CenterLongitude = 139.65, ZoomLevel = 11 };
map.Pins.Add(new MapPin { Latitude = 35.68, Longitude = 139.65, Label = "Tokyo" });
```

OSM's tile usage policy requires displaying attribution; `SkiaMap` renders the credit overlay automatically (toggle with `ShowAttribution`).

### WebView (WPE WebKit)

`WebView` renders through WPE WebKit 2.54+ when it is installed, composited in the Skia tree like any other control, in native Wayland/X11 mode. Without WPE the GTK-hosted WebKitGTK view is used (requires `options.UseGtk = true`). `OPENMAUI_WEBVIEW=wpe|webkitgtk|auto` overrides the choice.

```bash
# Debian / Ubuntu
sudo apt install libwpewebkit-2.0-1

# Fedora (not in the official repositories; maintained by an Igalia WPE developer)
sudo dnf copr enable philn/wpewebkit && sudo dnf install wpewebkit
```

### Optional: Blazor Hybrid (BlazorWebView)

`BlazorWebView` on Linux uses the opt-in sibling package on top of the WPE WebView:

```bash
dotnet add package Microsoft.AspNetCore.Components.WebView.Maui
dotnet add package OpenMaui.Controls.Linux.Blazor
```

```csharp
builder
    .UseMauiApp<App>()
    .UseLinux()
    .UseLinuxBlazorWebView();   // Blazor WebView services + WPE-backed handler; no-op off Linux
```

Use `HostPage="wwwroot/index.html"` and `RootComponent` exactly as on the other platforms; `wwwroot` is served from the app's output directory (the sample marks it `CopyToOutputDirectory`). External links open in the system browser through `UrlLoading`. See the `BlazorDemo` sample.

## XAML Support

OpenMaui fully supports standard .NET MAUI XAML syntax. Use the familiar XAML workflow:

```xml
<!-- MainPage.xaml -->
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="MyApp.MainPage">
    <VerticalStackLayout>
        <Label Text="Hello, OpenMaui!" FontSize="32" />
        <Button Text="Click me" Clicked="OnButtonClicked" />
        <Entry Placeholder="Enter text..." />
        <Slider Minimum="0" Maximum="100" />
    </VerticalStackLayout>
</ContentPage>
```

```csharp
// MauiProgram.cs
var builder = MauiApp.CreateBuilder();
builder
    .UseMauiApp<App>()
    .UseLinux();  // Enable Linux with XAML support (auto-detects X11/Wayland)
```

## Backend Selection (X11 / Wayland)

Pick exactly one — calls are no-ops on Windows/Android/iOS so they're safe in cross-platform `MauiProgram.cs`:

```csharp
builder
    .UseMauiApp<App>()
    .UseLinux();      // auto-detect from session (default)
    // .UseX11();     // force X11/XWayland — most stable, recommended for WebView-heavy apps
    // .UseWayland(); // prefer native Wayland; auto-falls back to X11 if Wayland unavailable
```

Native Wayland uses xdg-shell + wp_viewporter for fractional-scale rendering and ssd via zxdg_decoration_manager_v1. The X11 path remains the default fallback and is fully supported. Environment overrides (`MAUI_PREFER_X11=1`, `GDK_BACKEND=x11`) still work and take effect before the builder runs.

## Rendering (GPU / raster)

Frames are rasterised by Skia's OpenGL ES backend and presented through EGL: zero-copy `eglSwapBuffers` on native Wayland, DRI3/Present on X11. When EGL cannot be initialised (no `libEGL`, software-only drivers, headless CI) the platform falls back to the CPU raster path automatically, so an app always renders.

```csharp
builder.UseLinux(options => options.Renderer = RendererPreference.Raster); // Auto (default) | Gpu | Raster
```

Environment overrides, useful for A/B checks and bug reports:

```bash
OPENMAUI_RENDERER=raster ./MyApp      # force the CPU path (gpu | raster | auto)
OPENMAUI_RENDER_STATS=1 ./MyApp       # print rendered FPS and avg/p50/p95/p99/max frame time every 2 s
```

The chosen renderer is logged at startup, e.g. `Renderer: egl-wayland (EGL 1.5 Mesa Project; Mesa Intel(R) UHD Graphics)`. Measured on video playback at 1400x1050 on Mesa/Intel, the GPU path renders a frame in 1.47 ms average (p99 under 4 ms) against 3.55 ms (p99 7-10 ms) for raster.

## Supported Controls

| Category | Controls |
|----------|----------|
| **Basic** | Button, Label (incl. FormattedText), Entry, Editor, CheckBox, Switch, RadioButton, SearchBar |
| **Layout** | StackLayout, Grid, FlexLayout, AbsoluteLayout, ScrollView, ContentView, Border, Frame, ControlTemplate / ContentPresenter / TemplatedView |
| **Selection** | Picker, DatePicker, TimePicker, Slider, Stepper |
| **Display** | Image (incl. FontImageSource), ImageButton, ActivityIndicator, ProgressBar |
| **Collection** | CollectionView, ListView, TableView, CarouselView, IndicatorView |
| **Gesture** | SwipeView, RefreshView; Tap, Pan, Pinch, Swipe, Pointer and Drag/Drop recognizers |
| **Navigation** | NavigationPage, TabbedPage, FlyoutPage, Shell, modal pages |
| **Dialogs** | DisplayAlert, DisplayActionSheet, DisplayPromptAsync |
| **Menu** | MenuBar, MenuFlyout, context flyouts (`FlyoutBase.ContextFlyout`), MenuItem |
| **Shapes** | Ellipse, Line, Rectangle, Polygon, Polyline, Path |
| **Graphics** | GraphicsView, Border |
| **Web** | WebView (WPE WebKit), BlazorWebView |

The measured picture is in [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md): a scorecard generated from the test run (`dotnet run --project tools/Scorecard -- --run`) in the same categories Microsoft publishes for its maui-labs GTK backend. An item counts as covered only when every test mapped to it passed, so the percentages are computed, not claimed.

## Platform Services

| Service | Description |
|---------|-------------|
| `ClipboardService` | System clipboard access |
| `FilePickerService` | Native file open dialogs |
| `FolderPickerService` | Folder selection dialogs |
| `NotificationService` | Desktop notifications (libnotify) |
| `GlobalHotkeyService` | System-wide keyboard shortcuts |
| `DragDropService` | XDND drag and drop protocol |
| `LauncherService` | Open URLs and files |
| `ShareService` | Share content with other apps |
| `SecureStorageService` | Encrypted credential storage |
| `PreferencesService` | Application settings |
| `BrowserService` | Open URLs in default browser |
| `EmailService` | Compose emails |
| `SystemTrayService` | System tray icons |

## Accessibility

- **AT-SPI2**: Screen reader support for ORCA and other assistive technologies
- **High Contrast**: Automatic detection and color palette support
- **Keyboard Navigation**: Full keyboard accessibility

## Requirements

- .NET 10.0 SDK or later
- Linux (kernel 5.4+)
- X11 or Wayland
- SkiaSharp native libraries

### System Dependencies

**Ubuntu/Debian:**
```bash
sudo apt-get install libx11-dev libxrandr-dev libxcursor-dev libxi-dev libgl1-mesa-dev libfontconfig1-dev
```

**Fedora:**
```bash
sudo dnf install libX11-devel libXrandr-devel libXcursor-devel libXi-devel mesa-libGL-devel fontconfig-devel
```

## Documentation

- [Getting Started Guide](docs/GETTING_STARTED.md)
- [FAQ - Visual Studio Integration](docs/FAQ.md)
- [API Reference](docs/API.md)
- [Compatibility scorecard](docs/COMPATIBILITY.md) (generated per release)
- [Roadmap](docs/ROADMAP.md)
- [Contributing Guide](CONTRIBUTING.md)

## Sample Applications

Full sample applications are available in the [maui-linux-samples](https://github.com/open-maui/maui-linux-samples) repository:

| Sample | Description |
|--------|-------------|
| **[TodoApp](https://github.com/open-maui/maui-linux-samples/tree/main/TodoApp)** | Task manager with NavigationPage, XAML data binding, CollectionView |
| **[ShellDemo](https://github.com/open-maui/maui-linux-samples/tree/main/ShellDemo)** | Control showcase with Shell navigation and flyout menu |
| **[WebViewDemo](https://github.com/open-maui/maui-linux-samples/tree/main/WebViewDemo)** | Web browser with WebView (WPE WebKit in native mode), navigation controls, and XAML UI |
| **[BlazorDemo](https://github.com/open-maui/maui-linux-samples/tree/main/BlazorDemo)** | Blazor Hybrid: `BlazorWebView` with Razor components, counter, two-way binding, DI-injected service, external links via `UrlLoading` |
| **[MediaDemo](https://github.com/open-maui/maui-linux-samples/tree/main/MediaDemo)** | Video/audio player with `CommunityToolkit.Maui.MediaElement` on Linux (GStreamer backend); play/pause/seek/volume/mute, HTTP streams and local files |
| **[MapsDemo](https://github.com/open-maui/maui-linux-samples/tree/main/MapsDemo)** | OpenStreetMap map view with `Microsoft.Maui.Controls.Maps` on Linux; pins, route polyline, pan/zoom, cached OSM raster tiles |

## Distribution

Package your OpenMaui app as a portable AppImage with a single command:

```bash
dotnet tool install --global OpenMaui.AppImage
openmaui-appimage --project ./MyApp
```

Publishes the project, auto-detects the executable and icon, generates the `.desktop` file and installer, and produces a self-contained AppImage that runs on most Linux distributions. Full reference (options, CI recipes, host-dependency checks, signing, self-update): [OpenMaui.AppImage packaging guide](https://github.com/open-maui/appimage/blob/main/docs/PACKAGING-GUIDE.md) — written to be followed verbatim by humans or AI assistants.

## Quick Example

```csharp
// MauiProgram.cs
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Hosting;

var builder = MauiApp.CreateBuilder();
builder
    .UseMauiApp<App>()
    .UseLinux();   // or .UseX11() / .UseWayland() to force a backend

var app = builder.Build();
LinuxApplication.Run(app, args);
```

## Building from Source

```bash
# Primary repository
git clone https://github.com/open-maui/maui-linux.git

# Or from GitHub mirror
git clone https://github.com/open-maui/maui-linux.git

cd maui-linux
dotnet build
dotnet test tests/OpenMaui.Controls.Linux.Tests.csproj
```

The suite runs serially (the platform has process-wide state) and needs no display: golden-screenshot scenes render offscreen through the real engine at 1.0x to 2.0x (`OPENMAUI_UPDATE_GOLDENS=1` re-records baselines after an intended change), and the WebView scenarios run in a small out-of-process host because WebKit binds itself to the process main thread. Regenerate the scorecard with `dotnet run --project tools/Scorecard -- --run`.

## Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

> **Note:** Please submit issues and pull requests on [GitHub](https://github.com/open-maui/maui-linux).

## Architecture

```
┌─────────────────────────────────────────────────┐
│                  .NET MAUI                       │
│              (Virtual Views)                     │
├─────────────────────────────────────────────────┤
│                  Handlers                        │
│         (Platform Abstraction)                   │
├─────────────────────────────────────────────────┤
│              Skia Views                          │
│        (SkiaButton, SkiaLabel, etc.)            │
├─────────────────────────────────────────────────┤
│           SkiaSharp Rendering                    │
│         (Hardware Accelerated)                   │
├─────────────────────────────────────────────────┤
│              X11 / Wayland                       │
│          (Display Server)                        │
└─────────────────────────────────────────────────┘
```

## Styling and Data Binding

OpenMaui supports the full MAUI styling and data binding infrastructure:

### XAML Styles
```xml
<ContentPage.Resources>
    <ResourceDictionary>
        <Color x:Key="PrimaryColor">#5C6BC0</Color>
        <Style TargetType="Button">
            <Setter Property="BackgroundColor" Value="{StaticResource PrimaryColor}" />
            <Setter Property="TextColor" Value="White" />
        </Style>
    </ResourceDictionary>
</ContentPage.Resources>
```

### Data Binding
```xml
<Label Text="{Binding Title}" />
<Entry Text="{Binding Username, Mode=TwoWay}" />
<Button Command="{Binding SaveCommand}" IsEnabled="{Binding CanSave}" />
```

### Visual State Manager
All interactive controls support VSM states: Normal, PointerOver, Pressed, Focused, Disabled.

```xml
<Button Text="Hover Me">
    <VisualStateManager.VisualStateGroups>
        <VisualStateGroup x:Name="CommonStates">
            <VisualState x:Name="Normal">
                <VisualState.Setters>
                    <Setter Property="BackgroundColor" Value="#2196F3"/>
                </VisualState.Setters>
            </VisualState>
            <VisualState x:Name="PointerOver">
                <VisualState.Setters>
                    <Setter Property="BackgroundColor" Value="#42A5F5"/>
                </VisualState.Setters>
            </VisualState>
        </VisualStateGroup>
    </VisualStateManager.VisualStateGroups>
</Button>
```

## Roadmap

- [x] Core control library (50+ controls)
- [x] Platform services integration
- [x] Accessibility (AT-SPI2)
- [x] Input method support (IBus/XIM)
- [x] High DPI support
- [x] Drag and drop
- [x] Global hotkeys
- [x] BindableProperty for all controls
- [x] Visual State Manager integration
- [x] XAML styles and StaticResource
- [x] Data binding (OneWay, TwoWay, IValueConverter)
- [x] App icon support (MauiIcon build targets, .desktop integration)
- [x] Dark mode for all picker popups
- [x] DPI-aware popup rendering with edge detection
- [x] MAUI Shapes (Ellipse, Line, Rectangle, Polygon, Polyline, Path)
- [x] Native Wayland backend (xdg-shell, wp_viewporter, fractional-scale-v1, decoration-manager)
- [x] Programmatic backend selection (`UseX11()` / `UseWayland()`)
- [x] AppThemeBinding live propagation through Shell, NavigationPage, and CollectionView item trees
- [x] GTK4 interop layer (`Gtk4InteropService` with GTK3 fallback)
- [x] Client-side decorations for GNOME-Wayland sessions (10.0.60.10)
- [x] Native `wl_data_device_manager` clipboard — zero subprocess overhead, works without `wl-clipboard` (10.0.60.11)
- [x] `zwp_text_input_v3` IME for native Wayland (Fcitx5 / GNOME Pinyin) (10.0.60.12)
- [x] MediaElement / video support via GStreamer — opt-in `OpenMaui.Controls.Linux.MediaElement` sibling package (10.0.60.13)
- [x] MAUI 10.0.70 alignment — default-template CS1508 fix, Wayland-shim deployment fix, Essentials registration fix for MAUI 10's split `SetDefault`/`SetCurrent` naming (10.0.70.1)
- [x] `IInputContext.DeleteSurrounding` — `zwp_text_input_v3.delete_surrounding_text` round-trips into SkiaEntry / SkiaEditor with full UTF-8 byte → UTF-16 char conversion (10.0.70.2)
- [x] Primary-selection clipboard — `zwp_primary_selection_v1` binding + `PrimarySelectionService`, SkiaEntry / SkiaEditor push on drag-end and paste on middle-click (10.0.70.2)
- [x] Native `wl_data_device_manager` drag-and-drop — first functional Linux DnD path, file-drop URI decoding, source-side `start_drag` (10.0.70.2)
- [x] Hardware video acceleration tuning — `MediaHardwareAcceleration.Prefer` boosts VA-API / NVDEC / V4L2 / MediaSDK decoder ranks (10.0.70.2)
- [x] System tray icons — `TrayIcon` over libappindicator3 / libayatana-appindicator3 (StatusNotifierItem on the session bus) (10.0.70.2)
- [x] CUPS printing — `PrintService` enumerates printers, submits files, renders Skia pages to PDF via `SKDocument` and prints (10.0.70.2)
- [x] Maps integration (OpenStreetMap) — opt-in `OpenMaui.Controls.Linux.Maps` sibling package backs `Microsoft.Maui.Controls.Maps` with OSM raster tiles, pin / polyline overlays, persistent XDG tile cache (10.0.70.2)
- [x] Stability / correctness hardening from deep code review — Wayland listener-delegate rooting (crash-class), DnD protocol fixes with default-accept drops, self-paste deadlock fixes, first working X11 XDND drop path (INCR-capable), Maps tile-cache race + `MoveToRegion` zoom + `VisibleRegion` write-back + OSM tile-policy compliance + HiDPI tiles, tray binding/lifetime fixes, off-UI-thread printing, reversible HW-decode ranking (10.0.70.4)
- [x] Full X11 XDND drag-and-drop — outgoing drags via backend-agnostic `DragDropService.TryStartDrag` (Wayland-first, XDND source fallback) (10.0.70.4)
- [x] MAUI `DragGestureRecognizer` / `DropGestureRecognizer` wired to the native drag paths — `DragStarting` starts real drags; drops route to recognizer views with `AllowDrop`/`AcceptedOperation` feedback (10.0.70.4)
- [x] `set_surrounding_text` for `text-input-v3` — focused entry text + caret + anchor pushed to the IME (also feeds IBus); completes the Wayland IME loop (10.0.70.4)
- [x] Map polygon / circle overlays — `IFilledMapElement` / `ICircleMapElement` routed end-to-end with Mercator-correct circle radii (10.0.70.4)
- [x] GTK print dialog — `PrintService.ShowPrintDialogAsync` (GtkPrintUnixDialog: printer, copies, ranges, duplex, PPD options → CUPS-ready) (10.0.70.4)
- [x] Tray icon XEmbed fallback — freedesktop System Tray Protocol backend for desktops without an SNI host; left-click `Activated` works here (10.0.70.4)
- [x] MAUI 10.0.90 alignment — bumped Controls/Graphics/Graphics.Skia/Controls.Maps 10.0.70 → 10.0.90 (10.0.90.1)
- [x] Drag payload types — `DragPayload` (text/files/image) drives `TryStartDrag`; per-payload MIMEs, outgoing X11 INCR, `DataPackage` file/image extraction (10.0.90.1)
- [x] Maps satellite / hybrid layers — `SkiaMap.LayerType` + MAUI `Map.MapType`; `TileSource` abstraction with keyless OSM/Esri defaults, layer-stacking hybrid, layer-keyed cache (10.0.90.1)
- [x] `Tmds.DBus` migration — Fcitx5 transport off the `dbus-monitor` subprocess to typed Tmds.DBus proxies (10.0.90.1)
- [x] Live Visual Tree — `Diagnostics/VisualTreeInspector`: tree snapshot, highlight overlay, click-to-pick, text dump; opt-in Ctrl+Shift+D (10.0.90.1)
- [x] Hot Reload — `dotnet watch` C#/XAML edits re-render the current page (Shell-rooted apps); see `docs/HOT_RELOAD.md` (10.0.90.1)

- [x] MAUI 10.0.101 alignment + SkiaSharp 3 → 4 migration — ~400 call sites to the `SKFont` API; `SkiaFontFactory` (Subpixel + LinearMetrics) fixes HiDPI glyph gaps; label measurement is wrap-aware and glyph-independent (10.0.101.1)
- [x] Multi-window support — `Application.OpenWindow`/`CloseWindow`, per-window render/input/focus, X11 + Wayland parity, MAUI `IWindow` lifecycle, last-window-close exits (10.0.101.1)
- [x] Non-Shell-root XAML Hot Reload — raw `ContentPage`/`NavigationPage` roots rebuild and re-swap under `dotnet watch` (10.0.101.1)
- [x] Async drag image sourcing — `StreamImageSource` payloads resolve in-flight with format sniffing and bounded honest-fail (10.0.101.1)

### Up next

OpenMaui is the Wayland-first, self-rendered Linux platform for .NET MAUI, with X11 compatibility rather than GTK as its architectural foundation. The next releases build on that (full detail in [docs/ROADMAP.md](docs/ROADMAP.md)):

- [x] **Phase 1: GPU-native presentation** — `IRenderTarget` boundary with EGL-backed `GRContext` surfaces on Wayland (`wl_egl_window`) and X11, automatic raster fallback, `OPENMAUI_RENDERER` override, `OPENMAUI_RENDER_STATS` frame timing (10.0.101.2). Remaining: runtime scale change, hardware video zero-copy, explicit DMA-BUF and Vulkan, the full benchmark suite
- [x] **Phase 2: WPE WebKit WebView and BlazorWebView** — WPEPlatform (WPE WebKit 2.54) embedder compositing web frames inside the platform's own render tree, identical on Wayland and X11; context menus, clipboard bridge, backend selection; JS dialogs, file chooser and link cursors through the platform; `OpenMaui.Controls.Linux.Blazor` for Blazor Hybrid (10.0.101.2). Remaining: DMA-BUF zero-copy frames, hardware keycodes
- [x] **Phase 3: Conformance suite** — golden screenshot tests at every scale factor, a compatibility scorecard computed from the test run ([docs/COMPATIBILITY.md](docs/COMPATIBILITY.md)), and the handlers and services it exposed as missing (AbsoluteLayout, ControlTemplate, TableView, ListView, MAUI 10 dialogs, modal navigation, animations on MAUI's pipeline, VisualStateManager/triggers/behaviors, FormattedText, context flyouts, Essentials). Remaining: third-party library compatibility as a KPI, performance regression gates
- [ ] `openmaui doctor`, native D-Bus xdg-desktop-portal layer, deb/rpm output, multi-window round-out

## License

Copyright (c) 2025-2026 MarketAlly Pte Ltd. Licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [MarketAlly Pte Ltd](https://marketally.ai) - Project development and maintenance
- [SkiaSharp](https://github.com/mono/SkiaSharp) - 2D graphics library
- [.NET MAUI](https://github.com/dotnet/maui) - Cross-platform UI framework
- The .NET community
- A very special thank you to the [Anthropic](https://anthropic.com) team for delivering on the promise I hold most dear — that an individual with enough energy and persistence can still make a difference
 
 
 
 
