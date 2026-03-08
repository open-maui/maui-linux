# OpenMaui Linux Platform

A comprehensive Linux platform implementation for .NET MAUI using SkiaSharp rendering.

[![NuGet](https://img.shields.io/nuget/v/OpenMaui.Controls.Linux)](https://www.nuget.org/packages/OpenMaui.Controls.Linux)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![GitHub Mirror](https://img.shields.io/badge/GitHub-mirror-181717?logo=github)](https://github.com/open-maui/maui-linux)

**Developed by [MarketAlly Pte Ltd](https://marketally.ai)**
**Lead Architect: David H. Friedel Jr.**

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
    .UseOpenMauiLinux();  // Enable Linux with XAML support
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
- [FAQ - Visual Studio Integration](docs/FAQ.md)
- [API Reference](docs/API.md)
- [Contributing Guide](CONTRIBUTING.md)

## Sample Applications

Full sample applications are available in the [maui-linux-samples](https://git.marketally.ai/open-maui/maui-linux-samples) repository:

| Sample | Description |
|--------|-------------|
| **[TodoApp](https://git.marketally.ai/open-maui/maui-linux-samples/src/branch/main/TodoApp)** | Task manager with NavigationPage, XAML data binding, CollectionView |
| **[ShellDemo](https://git.marketally.ai/open-maui/maui-linux-samples/src/branch/main/ShellDemo)** | Control showcase with Shell navigation and flyout menu |

## Quick Example

```csharp
using OpenMaui.Platform.Linux;

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
# Primary repository
git clone https://git.marketally.ai/open-maui/maui-linux.git

# Or from GitHub mirror
git clone https://github.com/open-maui/maui-linux.git

cd maui-linux
dotnet build
dotnet test
```

## Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

> **Note:** The primary repository is hosted at [git.marketally.ai](https://git.marketally.ai/open-maui/maui-linux). The [GitHub repository](https://github.com/open-maui/maui-linux) is a mirror. Please submit issues and pull requests to the primary repository.

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

- [x] Core control library (35+ controls)
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
- [ ] Complete Wayland support
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
 
 
 
 
