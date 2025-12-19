# .NET MAUI Linux Platform API Documentation

## Overview

The .NET MAUI Linux Platform provides native Linux desktop support for .NET MAUI applications using SkiaSharp for rendering. It supports both X11 and Wayland display servers.

## Getting Started

### Installation

```bash
dotnet add package Microsoft.Maui.Controls.Linux
```

Or using the project template:

```bash
dotnet new install Microsoft.Maui.Linux.Templates
dotnet new maui-linux -n MyApp
```

### Basic Application Structure

```csharp
using Microsoft.Maui.Platform.Linux;

public class Program
{
    public static void Main(string[] args)
    {
        var app = LinuxApplication.CreateBuilder()
            .UseApp<App>()
            .Build();

        app.Run();
    }
}
```

## Core Components

### LinuxApplication

Entry point for Linux MAUI applications.

```csharp
public class LinuxApplication
{
    // Creates a new application builder
    public static LinuxApplicationBuilder CreateBuilder();

    // Gets the current application instance
    public static LinuxApplication Current { get; }

    // Gets the main window
    public IWindow MainWindow { get; }

    // Runs the application
    public void Run();

    // Quits the application
    public void Quit();
}
```

### LinuxApplicationBuilder

```csharp
public class LinuxApplicationBuilder
{
    // Sets the MAUI application type
    public LinuxApplicationBuilder UseApp<TApp>() where TApp : Application;

    // Configures the window
    public LinuxApplicationBuilder ConfigureWindow(Action<WindowOptions> configure);

    // Forces a specific display server
    public LinuxApplicationBuilder UseDisplayServer(DisplayServerType type);

    // Builds the application
    public LinuxApplication Build();
}
```

## View Controls

### SkiaButton

A clickable button control.

```csharp
public class SkiaButton : SkiaView
{
    public string Text { get; set; }
    public SKColor TextColor { get; set; }
    public SKColor BackgroundColor { get; set; }
    public float CornerRadius { get; set; }
    public float FontSize { get; set; }
    public event EventHandler? Clicked;
}
```

### SkiaEntry

A text input control.

```csharp
public class SkiaEntry : SkiaView, IInputContext
{
    public string Text { get; set; }
    public string Placeholder { get; set; }
    public SKColor TextColor { get; set; }
    public SKColor PlaceholderColor { get; set; }
    public float FontSize { get; set; }
    public bool IsPassword { get; set; }
    public int MaxLength { get; set; }
    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public event EventHandler? Completed;
}
```

### SkiaSlider

A value slider control.

```csharp
public class SkiaSlider : SkiaView
{
    public double Value { get; set; }
    public double Minimum { get; set; }
    public double Maximum { get; set; }
    public SKColor TrackColor { get; set; }
    public SKColor ThumbColor { get; set; }
    public event EventHandler<ValueChangedEventArgs>? ValueChanged;
}
```

### SkiaScrollView

A scrollable container.

```csharp
public class SkiaScrollView : SkiaView
{
    public SkiaView? Content { get; set; }
    public float HorizontalScrollOffset { get; set; }
    public float VerticalScrollOffset { get; set; }
    public ScrollOrientation Orientation { get; set; }
    public event EventHandler? Scrolled;
}
```

### SkiaImage

An image display control.

```csharp
public class SkiaImage : SkiaView
{
    public SKBitmap? Source { get; set; }
    public ImageAspect Aspect { get; set; }
    public void LoadFromFile(string path);
    public void LoadFromStream(Stream stream);
}
```

## Layout Controls

### SkiaStackLayout

Arranges children in a stack.

```csharp
public class SkiaStackLayout : SkiaLayoutView
{
    public StackOrientation Orientation { get; set; }
    public float Spacing { get; set; }
}
```

### SkiaGrid

Arranges children in a grid.

```csharp
public class SkiaGrid : SkiaLayoutView
{
    public List<GridLength> RowDefinitions { get; }
    public List<GridLength> ColumnDefinitions { get; }
    public float RowSpacing { get; set; }
    public float ColumnSpacing { get; set; }

    public static void SetRow(SkiaView view, int row);
    public static void SetColumn(SkiaView view, int column);
    public static void SetRowSpan(SkiaView view, int span);
    public static void SetColumnSpan(SkiaView view, int span);
}
```

## Page Controls

### SkiaTabbedPage

A page with tab navigation.

```csharp
public class SkiaTabbedPage : SkiaLayoutView
{
    public int SelectedIndex { get; set; }
    public void AddTab(string title, SkiaView content, string? iconPath = null);
    public void RemoveTab(int index);
    public void ClearTabs();
    public event EventHandler? SelectedIndexChanged;
}
```

### SkiaFlyoutPage

A page with flyout/drawer navigation.

```csharp
public class SkiaFlyoutPage : SkiaLayoutView
{
    public SkiaView? Flyout { get; set; }
    public SkiaView? Detail { get; set; }
    public bool IsPresented { get; set; }
    public float FlyoutWidth { get; set; }
    public bool GestureEnabled { get; set; }
    public FlyoutLayoutBehavior FlyoutLayoutBehavior { get; set; }
    public event EventHandler? IsPresentedChanged;
}
```

### SkiaShell

Full navigation container with flyout, tabs, and URI routing.

```csharp
public class SkiaShell : SkiaLayoutView
{
    public bool FlyoutIsPresented { get; set; }
    public ShellFlyoutBehavior FlyoutBehavior { get; set; }
    public float FlyoutWidth { get; set; }
    public string Title { get; set; }
    public bool NavBarIsVisible { get; set; }
    public bool TabBarIsVisible { get; set; }

    public void AddSection(ShellSection section);
    public void NavigateToSection(int sectionIndex, int itemIndex = 0);
    public void GoToAsync(string route);

    public event EventHandler? FlyoutIsPresentedChanged;
    public event EventHandler<ShellNavigationEventArgs>? Navigated;
}
```

## Services

### Input Method Service (IME)

Provides international text input support.

```csharp
public interface IInputMethodService
{
    bool IsActive { get; }
    string PreEditText { get; }

    void Initialize(nint windowHandle);
    void SetFocus(IInputContext? context);
    void SetCursorLocation(int x, int y, int width, int height);
    bool ProcessKeyEvent(uint keyCode, KeyModifiers modifiers, bool isKeyDown);
    void Reset();

    event EventHandler<TextCommittedEventArgs>? TextCommitted;
    event EventHandler<PreEditChangedEventArgs>? PreEditChanged;
}

// Factory
var imeService = InputMethodServiceFactory.Instance;
```

### Accessibility Service (AT-SPI2)

Provides screen reader support.

```csharp
public interface IAccessibilityService
{
    bool IsEnabled { get; }

    void Initialize();
    void Register(IAccessible accessible);
    void Unregister(IAccessible accessible);
    void NotifyFocusChanged(IAccessible? accessible);
    void NotifyPropertyChanged(IAccessible accessible, AccessibleProperty property);
    void NotifyStateChanged(IAccessible accessible, AccessibleState state, bool value);
    void Announce(string text, AnnouncementPriority priority = AnnouncementPriority.Polite);
}

// Factory
var accessibilityService = AccessibilityServiceFactory.Instance;
```

## Rendering Optimization

### DirtyRectManager

Tracks invalidated regions for efficient redraw.

```csharp
public class DirtyRectManager
{
    public int MaxDirtyRects { get; set; }
    public bool NeedsFullRedraw { get; }
    public bool HasDirtyRegions { get; }

    public void SetBounds(SKRect bounds);
    public void Invalidate(SKRect rect);
    public void InvalidateAll();
    public void Clear();
    public SKRect GetCombinedDirtyRect();
    public void ApplyClipping(SKCanvas canvas);
}
```

### RenderCache

Caches rendered content for static views.

```csharp
public class RenderCache : IDisposable
{
    public long MaxCacheSize { get; set; }
    public long CurrentCacheSize { get; }

    public bool TryGet(string key, out SKBitmap? bitmap);
    public void Set(string key, SKBitmap bitmap);
    public void Invalidate(string key);
    public void InvalidatePrefix(string prefix);
    public void Clear();
    public SKBitmap GetOrCreate(string key, int width, int height, Action<SKCanvas> render);
}
```

### TextRenderCache

Caches rendered text for performance.

```csharp
public class TextRenderCache : IDisposable
{
    public int MaxEntries { get; set; }
    public SKBitmap GetOrCreate(string text, SKPaint paint);
    public void Clear();
}
```

## Event Args

### TextChangedEventArgs

```csharp
public class TextChangedEventArgs : EventArgs
{
    public string OldTextValue { get; }
    public string NewTextValue { get; }
}
```

### ValueChangedEventArgs

```csharp
public class ValueChangedEventArgs : EventArgs
{
    public double OldValue { get; }
    public double NewValue { get; }
}
```

### PointerEventArgs

```csharp
public class PointerEventArgs : EventArgs
{
    public float X { get; }
    public float Y { get; }
    public PointerButton Button { get; }
    public bool Handled { get; set; }
}
```

## Enumerations

### DisplayServerType

```csharp
public enum DisplayServerType
{
    Auto,
    X11,
    Wayland
}
```

### FlyoutLayoutBehavior

```csharp
public enum FlyoutLayoutBehavior
{
    Default,
    Popover,
    Split,
    SplitOnLandscape,
    SplitOnPortrait
}
```

### ShellFlyoutBehavior

```csharp
public enum ShellFlyoutBehavior
{
    Disabled,
    Flyout,
    Locked
}
```

### AccessibleRole

```csharp
public enum AccessibleRole
{
    Unknown, Window, Application, Panel, Frame, Button,
    CheckBox, RadioButton, ComboBox, Entry, Label,
    List, ListItem, Menu, MenuItem, ScrollBar,
    Slider, StatusBar, Tab, Text, ProgressBar,
    // ... and more
}
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `MAUI_DISPLAY_SERVER` | Force display server: `x11`, `wayland`, or `auto` |
| `MAUI_INPUT_METHOD` | Force IME: `ibus`, `xim`, or `none` |
| `GTK_A11Y` | Set to `none` to disable accessibility |

## System Requirements

- .NET 8.0 or .NET 9.0
- Linux with X11 or Wayland
- libX11 (for X11 support)
- libwayland-client (for Wayland support)
- libibus-1.0 (optional, for IBus IME)
- libatspi (optional, for accessibility)
