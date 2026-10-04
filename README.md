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
- **WebView**: WPE WebKit composited inside the Skia tree (no GTK widget, no reparenting) on native Wayland and X11, with context menus, clipboard, JavaScript dialogs, file chooser, permissions, web notifications, downloads, spell checking, link cursors, `EvaluateJavaScriptAsync` results and a backend-neutral WebKit content API; WebKitGTK remains the GTK-mode fallback. Libraries derive from the public `LinuxWebViewHandler` for what WebView2 and WKWebView give them elsewhere (`ILinuxWebView`: navigation and new-window decisions, HTTP status, response and download handling with the desktop's Save dialog, zoom, page capture and save as PDF)
- **Blazor Hybrid**: Opt-in `OpenMaui.Controls.Linux.Blazor` package backs `BlazorWebView` (Microsoft.AspNetCore.Components.WebView.Maui) on the WPE WebView
- **Maps**: Opt-in `OpenMaui.Controls.Linux.Maps` package backs `Microsoft.Maui.Controls.Maps` with OpenStreetMap raster tiles in Skia — pan/zoom, pin & polyline overlays, persistent XDG tile cache. Plus a standalone `SkiaMap` view for code-first map UI
- **PDF**: Opt-in `OpenMaui.Controls.Linux.Pdf` package renders PDF pages with Google's PDFium (bundled for x64 and arm64): `SkiaPdfView` for a scrolling, zoomable document view, `PdfiumDocument` for pages as bitmaps and their text, and `.UseLinuxPdf()` makes Syncfusion's `PdfToImageConverter` (and so `SfPdfViewer`) render
- **Syncfusion**: Opt-in `OpenMaui.Controls.Linux.Syncfusion` package runs Syncfusion .NET MAUI controls (lists and trees, tabs, charts, inputs and drop-downs, Carousel, Rotator, SignaturePad, SfPopup): call `.UseLinuxSyncfusion()` instead of `.ConfigureSyncfusionCore()`. Bring your own Syncfusion license
- **Effects**: MAUI's effects pipeline (`ConfigureEffects`, `RoutingEffect` to `PlatformEffect` with the Skia view as `Control`), with per-view routed pointer, key and bounds events on `SkiaView` for a platform effect to hook
- **Third-party libraries**: popular MAUI libraries (CommunityToolkit.Maui, CommunityToolkit.Mvvm, LiveCharts2, ReactiveUI, Prism, SkiaSharp views) run unmodified, measured by a separate compatibility suite and published in the [scorecard](docs/COMPATIBILITY.md)

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
                 gstreamer1-plugins-ugly-free gstreamer1-plugin-libav gstreamer1-vaapi

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

### Optional: Syncfusion controls

Linux apps build plain `net10.0` and so get Syncfusion's platform-neutral assemblies, which have no native drawing or touch. The opt-in bridge package supplies them on OpenMaui's renderer:

```bash
dotnet add package OpenMaui.Controls.Linux.Syncfusion
```

Then call `UseLinuxSyncfusion()` in place of `ConfigureSyncfusionCore()` (on the other platforms it calls `ConfigureSyncfusionCore()` for you):

```csharp
builder
    .UseMauiApp<App>()
    .UseLinux()
    .UseLinuxSyncfusion();
```

`SfView`-based controls (ListView, TreeView, TabView, Charts, the inputs and the rest) lay out, draw their text and graphics, and receive touch, tap, double-tap, right-tap, long-press, drag, wheel, ctrl+wheel pinch and keyboard input. SfCarousel, SfRotator, SfSignaturePad and SfInteractiveScrollView get their native views from the package; SfComboBox and SfAutocomplete drop-downs and SfPopup (and the controls that open one) show as overlays. Not covered yet: ImageEditor and Syncfusion's MediaElement (dedicated native views). The package does not include Syncfusion's assemblies; you need your own Syncfusion license.

### Optional: PDF (PDFium)

Showing PDFs on Linux uses the opt-in sibling package, which bundles Google's PDFium (Apache-2.0) for linux-x64 and linux-arm64, so the host needs nothing installed:

```bash
dotnet add package OpenMaui.Controls.Linux.Pdf
```

```csharp
builder
    .UseMauiApp<App>()
    .UseLinux()
    .UseLinuxPdf();   // also makes Syncfusion's PdfToImageConverter render on Linux
```

- `SkiaPdfView` shows a document's pages in a vertical or horizontal strip at 96 DPI times its zoom, rendered in the background at the screen's density: wheel, scrollbar and keys scroll, Ctrl+wheel and Ctrl +/-/0 zoom, and `PageIndex`/`PageChanged` follow the page in view. A library's PDF control maps onto it (MarketAlly.ViewEngine's PdfView does).
- `PdfiumDocument` opens a PDF from bytes or a stream (with a password) and renders a page, or a region of it, to an `SKBitmap` or PNG, and reads its text.
- With `.UseLinuxPdf()`, Syncfusion's `PdfToImageConverter`, whose platform-neutral build has no renderer, renders with PDFium at the Windows build's sizes, so `SfPdfViewer` works.

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
# Debian testing / sid (Debian 13 carries 2.48, which is too old)
sudo apt install libwpewebkit-2.0-1

# Fedora (not in the official repositories; maintained by an Igalia WPE developer)
sudo dnf copr enable philn/wpewebkit && sudo dnf install wpewebkit
```

Ubuntu has shipped no WPE WebKit package since 22.04, and Debian 13 only has 2.48. There, `WebView` uses the GTK-hosted WebKitGTK view (`options.UseGtk = true`), and `BlazorWebView` needs a WPE 2.54 installed from elsewhere. `OPENMAUI_DOCTOR=1 ./MyApp` reports which engine a machine will use.

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

### Diagnosing your setup

Any OpenMaui app can print an environment report instead of starting. Set `OPENMAUI_DOCTOR=1` (or pass `--openmaui-doctor`); the app writes the report to stdout and exits before creating a window, with exit code 1 when a required item is missing:

```bash
OPENMAUI_DOCTOR=1 ./MyApp
dotnet run -- --openmaui-doctor
```

The report runs inside the platform, so it shows what the app will actually select: display server and compositor, the Wayland globals it advertises, the EGL/GL renderer and the render target `RenderTargetFactory` will pick, the scale factor and which detection source won, the IME backend, and every optional native dependency and fallback font. Anything missing comes with the install command for your distro (apt, dnf or pacman). Trimmed output from a Fedora 44 KDE session on an Intel GPU:

```text
OpenMaui doctor
===============

Runtime
  [ok]      .NET                         .NET 10.0.10 (X64, fedora.44-x64)
  [info]    OS                           Fedora Linux 44 (KDE Plasma Desktop Edition), kernel
                                         7.1.5-201.fc44.x86_64; install hints for: Fedora (dnf)

Wayland compositor
  [ok]      libopenmaui_wl.so            /home/me/MyApp/bin/Debug/net10.0/libopenmaui_wl.so
  [ok]      Compositor                   kwin_wayland_wr (pid 14908)
  [ok]      zwp_text_input_manager_v3    v2: native IME (zwp_text_input_v3)
  [ok]      zwp_linux_dmabuf_v1          v5: dmabuf buffer sharing

GPU and renderer
  [ok]      EGL                          EGL 1.5 Mesa Project (client APIs: OpenGL OpenGL_ES)
  [ok]      GL renderer                  Mesa Intel(R) UHD Graphics (CML GT2) (OpenGL ES 3.2 Mesa 26.1.5)
  [ok]      RenderTargetFactory          egl-wayland (GPU)

Display scale
  [info]    Startup scale                1.75 (168 dpi)
  [info]    Detected by                  HiDpiService: X11 (Xft.dpi / .Xresources / X server DPI)

Native dependencies
  [ok]      GStreamer                    libgstreamer-1.0.so.0, libgstapp-1.0.so.0: MediaElement playback
  [ok]      WPE WebKit 2.54+             libWPEWebKit-2.0.so.1: composited WebView / BlazorWebView
  [MISSING] libcups                      libcups.so.2 not found; printing unavailable
                                         fix: sudo dnf install cups-libs

Summary: 31 ok, 0 warning(s), 1 missing (0 required).
```

(The `libcups` row above is illustrative of how a missing dependency is shown.)

### Checking your app's pages

Set `OPENMAUI_INVARIANTS=1` and use the app: each page is checked a second after it appears (and every five seconds while it stays), and what is broken is printed to stderr. The rules hold for every MAUI page, so they catch whole classes of problems in OpenMaui or in the app without a test written for that page:

| Rule | Reports |
|---|---|
| `type-name-text` | text that is a .NET type name (an object drawn through its `ToString`) |
| `draws-outside-bounds` | an image or video that draws outside its frame |
| `unreachable-control` | a visible, enabled control that a click at its centre does not reach |
| `binding-failed` | a binding that failed (MAUI's binding diagnostics are turned on for this) |
| `error-logged` | an error OpenMaui logged while the page was shown |

```text
[Invariant] checked LibraryPage: 0 violation(s)
[Invariant] checked EconomyPage: 1 violation(s)
[Invariant] binding-failed: 'IsOffline' property not found on 'EconomyViewModel', target property: 'ChatPane.IsOffline' (binding)
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
git clone https://git.marketally.com/open-maui/maui-linux.git

# Or from the GitHub mirror
git clone https://github.com/open-maui/maui-linux.git

cd maui-linux
dotnet build
dotnet test tests/OpenMaui.Controls.Linux.Tests.csproj
dotnet test tests/Compat/OpenMaui.Compat.Tests.csproj   # third-party libraries
```

The suite runs serially (the platform has process-wide state) and needs no display: golden-screenshot scenes render offscreen through the real engine at 1.0x to 2.0x (`OPENMAUI_UPDATE_GOLDENS=1` re-records baselines after an intended change), and the WebView scenarios run in a small out-of-process host because WebKit binds itself to the process main thread. Regenerate the scorecard with `dotnet run --project tools/Scorecard -- --run`, which runs both suites.

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

## License

Copyright (c) 2025-2026 MarketAlly Pte Ltd. Licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [MarketAlly Pte Ltd](https://marketally.ai) - Project development and maintenance
- [SkiaSharp](https://github.com/mono/SkiaSharp) - 2D graphics library
- [.NET MAUI](https://github.com/dotnet/maui) - Cross-platform UI framework
- [PDFium](https://pdfium.googlesource.com/pdfium/) - PDF rendering in `OpenMaui.Controls.Linux.Pdf` (Apache-2.0), packaged by [bblanchon/pdfium-binaries](https://github.com/bblanchon/pdfium-binaries)
- The .NET community
- A very special thank you to the [Anthropic](https://anthropic.com) team for delivering on the promise I hold most dear — that an individual with enough energy and persistence can still make a difference
 
 
 
 
