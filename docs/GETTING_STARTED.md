# Getting Started with .NET MAUI on Linux

This guide will help you get started with building .NET MAUI applications for Linux.

## Prerequisites

- .NET 9.0 SDK or later
- Linux distribution (Ubuntu 22.04+, Fedora 38+, Arch Linux, etc.)
- X11 or Wayland display server

### Installing Dependencies

**Ubuntu/Debian:**
```bash
sudo apt-get install libx11-dev libxrandr-dev libxcursor-dev libxi-dev libgl1-mesa-dev libfontconfig1-dev
```

**Fedora:**
```bash
sudo dnf install libX11-devel libXrandr-devel libXcursor-devel libXi-devel mesa-libGL-devel fontconfig-devel
```

**Arch Linux:**
```bash
sudo pacman -S libx11 libxrandr libxcursor libxi mesa fontconfig
```

## Creating a New Project

### Using the Template (Recommended)

1. Install the template:
```bash
dotnet new install OpenMaui.Linux.Templates
```

2. Create a new project:
```bash
dotnet new maui-linux -n MyApp
cd MyApp
```

3. Run your application:
```bash
dotnet run
```

### Manual Setup

1. Create a new console application:
```bash
dotnet new console -n MyMauiLinuxApp
cd MyMauiLinuxApp
```

2. Add the NuGet package:
```bash
dotnet add package OpenMaui.Controls.Linux --prerelease
```

3. Update your `Program.cs`:
```csharp
using Microsoft.Maui.Platform;
using OpenMaui.Platform.Linux;

var app = new LinuxApplication();

app.MainPage = new ContentPage
{
    Content = new VerticalStackLayout
    {
        Children =
        {
            new Label { Text = "Hello, MAUI on Linux!" },
            new Button { Text = "Click Me" }
        }
    }
};

app.Run();
```

## Project Structure

A typical MAUI Linux project has this structure:

```
MyApp/
├── App.cs              # Application entry and configuration
├── MainPage.cs         # Main page of your app
├── Program.cs          # Application bootstrap
├── MyApp.csproj        # Project file
└── Resources/          # Images, fonts, and other assets
    ├── Images/
    └── Fonts/
```

## Basic Controls

### Labels
```csharp
var label = new SkiaLabel
{
    Text = "Hello World",
    TextColor = new SKColor(33, 33, 33),
    FontSize = 16f
};
```

### Buttons
```csharp
var button = new SkiaButton
{
    Text = "Click Me",
    BackgroundColor = new SKColor(33, 150, 243)
};
button.Clicked += (s, e) => Console.WriteLine("Clicked!");
```

### Text Input
```csharp
var entry = new SkiaEntry
{
    Placeholder = "Enter text...",
    MaxLength = 100
};
entry.TextChanged += (s, e) => Console.WriteLine($"Text: {e.NewValue}");
```

### Layouts
```csharp
// Vertical stack
var vstack = new SkiaStackLayout
{
    Orientation = StackOrientation.Vertical,
    Spacing = 10
};
vstack.AddChild(new SkiaLabel { Text = "Item 1" });
vstack.AddChild(new SkiaLabel { Text = "Item 2" });

// Horizontal stack
var hstack = new SkiaStackLayout
{
    Orientation = StackOrientation.Horizontal,
    Spacing = 8
};
```

## Advanced Controls

### CarouselView
```csharp
var carousel = new SkiaCarouselView
{
    Loop = true,
    PeekAreaInsets = 20f,
    ShowIndicators = true
};
carousel.AddItem(new SkiaLabel { Text = "Page 1" });
carousel.AddItem(new SkiaLabel { Text = "Page 2" });
carousel.PositionChanged += (s, e) =>
    Console.WriteLine($"Position: {e.CurrentPosition}");
```

### RefreshView
```csharp
var refreshView = new SkiaRefreshView
{
    Content = myScrollableContent,
    RefreshColor = SKColors.Blue
};
refreshView.Refreshing += async (s, e) =>
{
    await LoadDataAsync();
    refreshView.IsRefreshing = false;
};
```

### SwipeView
```csharp
var swipeView = new SkiaSwipeView
{
    Content = new SkiaLabel { Text = "Swipe me" }
};
swipeView.RightItems.Add(new SwipeItem
{
    Text = "Delete",
    BackgroundColor = SKColors.Red
});
```

### MenuBar
```csharp
var menuBar = new SkiaMenuBar();
var fileMenu = new MenuBarItem { Text = "File" };
fileMenu.Items.Add(new MenuItem { Text = "New", Shortcut = "Ctrl+N" });
fileMenu.Items.Add(new MenuItem { Text = "Open", Shortcut = "Ctrl+O" });
fileMenu.Items.Add(new MenuItem { IsSeparator = true });
fileMenu.Items.Add(new MenuItem { Text = "Exit" });
menuBar.Items.Add(fileMenu);
```

## Platform Services

### Clipboard
```csharp
var clipboard = new ClipboardService();
await clipboard.SetTextAsync("Copied text");
var text = await clipboard.GetTextAsync();
```

### File Picker
```csharp
var picker = new FilePickerService();
var result = await picker.PickAsync(new PickOptions
{
    FileTypes = new[] { ".txt", ".md" }
});
```

### Notifications
```csharp
var notifications = new NotificationService();
notifications.Show("Title", "Message body", "app-icon");
```

### Global Hotkeys
```csharp
var hotkeys = new GlobalHotkeyService();
hotkeys.Initialize();
int id = hotkeys.Register(HotkeyKey.F1, HotkeyModifiers.Control);
hotkeys.HotkeyPressed += (s, e) =>
{
    if (e.Id == id) Console.WriteLine("Ctrl+F1 pressed!");
};
```

## Accessibility

### High Contrast Mode
```csharp
var highContrast = new HighContrastService();
highContrast.Initialize();
if (highContrast.IsHighContrastEnabled)
{
    var colors = highContrast.GetColors();
    // Apply high contrast colors to your UI
}
```

### HiDPI Support
```csharp
var hidpi = new HiDpiService();
hidpi.Initialize();
float scale = hidpi.ScaleFactor;
// Scale your UI elements accordingly
```

## Building for Release

```bash
dotnet publish -c Release -r linux-x64 --self-contained
```

Or for ARM64:
```bash
dotnet publish -c Release -r linux-arm64 --self-contained
```

## Troubleshooting

### Display Issues
- Ensure X11 or Wayland is running
- Check that SkiaSharp native libraries are installed
- Verify graphics drivers are up to date

### Font Rendering
- Install `fontconfig` and common fonts
- Set the `FONTCONFIG_PATH` environment variable if needed

### Input Method (IME)
- For CJK input, ensure IBus or Fcitx is installed and configured
- Set `GTK_IM_MODULE=ibus` or `QT_IM_MODULE=ibus`

## Next Steps

- Explore the [API Documentation](API.md)
- Check out the [Sample Applications](../samples/)
- Read the [Contributing Guide](../CONTRIBUTING.md)
