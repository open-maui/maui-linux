# .NET MAUI Linux Platform

A comprehensive Linux platform implementation for .NET MAUI using SkiaSharp rendering.

[![Build Status](https://github.com/open-maui/maui-linux/actions/workflows/ci.yml/badge.svg)](https://github.com/open-maui/maui-linux/actions)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Maui.Controls.Linux)](https://www.nuget.org/packages/Microsoft.Maui.Controls.Linux)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Overview

This project brings .NET MAUI to Linux desktops with native X11/Wayland support, hardware-accelerated Skia rendering, and full platform service integration.

### Key Features

- **Full Control Library**: 35+ controls including Button, Label, Entry, CarouselView, RefreshView, SwipeView, and more
- **Native Integration**: X11 and Wayland display server support
- **Accessibility**: AT-SPI2 screen reader support and high contrast mode
- **Platform Services**: Clipboard, file picker, notifications, global hotkeys, drag & drop
- **Input Methods**: IBus and XIM support for international text input
- **High DPI**: Automatic scale factor detection for GNOME, KDE, and X11

## Quick Start

### Installation

```bash
# Install the template
dotnet new install Microsoft.Maui.Linux.Templates

# Create a new project
dotnet new maui-linux -n MyApp
cd MyApp

# Run
dotnet run
```

### Manual Installation

```bash
dotnet add package Microsoft.Maui.Controls.Linux --prerelease
```

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

- .NET 9.0 SDK or later
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
- [API Reference](docs/API.md)
- [Contributing Guide](CONTRIBUTING.md)

## Sample Application

```csharp
using Microsoft.Maui.Platform;

var app = new LinuxApplication();

app.MainPage = new ContentPage
{
    Content = new VerticalStackLayout
    {
        Spacing = 10,
        Children =
        {
            new Label
            {
                Text = "Welcome to MAUI on Linux!",
                FontSize = 24
            },
            new Button
            {
                Text = "Click Me"
            },
            new Entry
            {
                Placeholder = "Enter your name"
            }
        }
    }
};

app.Run();
```

## Building from Source

```bash
git clone https://github.com/open-maui/maui-linux.git
cd maui-linux
dotnet build
dotnet test
```

## Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

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

## Roadmap

- [x] Core control library (35+ controls)
- [x] Platform services integration
- [x] Accessibility (AT-SPI2)
- [x] Input method support (IBus/XIM)
- [x] High DPI support
- [x] Drag and drop
- [x] Global hotkeys
- [ ] Complete Wayland support
- [ ] Hardware video acceleration
- [ ] GTK4 interop layer

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [SkiaSharp](https://github.com/mono/SkiaSharp) - 2D graphics library
- [.NET MAUI](https://github.com/dotnet/maui) - Cross-platform UI framework
- The .NET community
