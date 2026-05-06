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
- **Native Integration**: First-class X11 *and* native Wayland support (xdg-shell, wp_viewporter, fractional-scale-v1, zxdg_decoration_manager_v1) — programmatically selectable or auto-detected
- **Accessibility**: AT-SPI2 screen reader support and high contrast mode
- **Platform Services**: Clipboard, file picker, notifications, global hotkeys, drag & drop
- **Input Methods**: IBus and XIM support for international text input
- **High DPI**: Automatic scale factor detection for GNOME, KDE, and X11; fractional scale handled via Wayland viewporter for pixel-exact rendering at non-integer scales (1.25x, 1.5x, 1.75x)
- **Theming**: AppThemeBinding live propagation across the entire view tree — CollectionView items, pushed pages, Shell content, and flyout regions all flip on theme toggle

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

## Supported Controls

| Category | Controls |
|----------|----------|
| **Basic** | Button, Label, Entry, Editor, CheckBox, Switch, RadioButton |
| **Layout** | StackLayout, ScrollView, Border, Page |
| **Selection** | Picker, DatePicker, TimePicker, Slider, Stepper |
| **Display** | Image, ImageButton, ActivityIndicator, ProgressBar |
| **Collection** | CollectionView, CarouselView, IndicatorView |
| **Gesture** | SwipeView, RefreshView |
| **Navigation** | NavigationPage, TabbedPage, FlyoutPage, Shell |
| **Menu** | MenuBar, MenuFlyout, MenuItem |
| **Shapes** | Ellipse, Line, Rectangle, Polygon, Polyline, Path |
| **Graphics** | GraphicsView, Border |

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
- [Contributing Guide](CONTRIBUTING.md)

## Sample Applications

Full sample applications are available in the [maui-linux-samples](https://github.com/open-maui/maui-linux-samples) repository:

| Sample | Description |
|--------|-------------|
| **[TodoApp](https://github.com/open-maui/maui-linux-samples/tree/main/TodoApp)** | Task manager with NavigationPage, XAML data binding, CollectionView |
| **[ShellDemo](https://github.com/open-maui/maui-linux-samples/tree/main/ShellDemo)** | Control showcase with Shell navigation and flyout menu |
| **[WebViewDemo](https://github.com/open-maui/maui-linux-samples/tree/main/WebViewDemo)** | Web browser with WebView, navigation controls, and XAML UI |

## Distribution

Package your OpenMaui app as a portable AppImage with a single command:

```bash
dotnet tool install --global OpenMaui.AppImage
dotnet appimage
```

Auto-detects your executable and icon, generates a `.desktop` file, and produces a self-contained AppImage that runs on most Linux distributions. See the [OpenMaui.AppImage](https://github.com/open-maui/appimage) repository for details.

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
dotnet test
```

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
- [ ] Client-side decorations for GNOME-Wayland sessions
- [ ] Native `wl_data_device_manager` clipboard (replaces wl-copy/wl-paste subprocess)
- [ ] text-input-v3 IME support on Wayland
- [ ] Hardware video acceleration
- [ ] GTK4 interop layer

## License

Copyright (c) 2025-2026 MarketAlly Pte Ltd. Licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [MarketAlly Pte Ltd](https://marketally.ai) - Project development and maintenance
- [SkiaSharp](https://github.com/mono/SkiaSharp) - 2D graphics library
- [.NET MAUI](https://github.com/dotnet/maui) - Cross-platform UI framework
- The .NET community
- A very special thank you to the [Anthropic](https://anthropic.com) team for delivering on the promise I hold most dear — that an individual with enough energy and persistence can still make a difference
 
 
 
 
