# OpenMaui Linux Platform API Documentation

## Overview

The OpenMaui Linux Platform provides native Linux desktop support for .NET MAUI applications using SkiaSharp for rendering. All public APIs use standard .NET MAUI types (Color, Rect, Size, Thickness) for full compliance with the MAUI API specification.

## Getting Started

### Installation

```bash
dotnet add package OpenMaui.Controls.Linux
```

Or using the project template:

```bash
dotnet new install OpenMaui.Linux.Templates
dotnet new openmaui-linux-xaml -n MyApp
```

### Basic Application Structure

```csharp
// MauiProgram.cs
using Microsoft.Maui.Hosting;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseOpenMauiLinux();  // Enable Linux platform

        return builder.Build();
    }
}
```

## Core Types

All public APIs use .NET MAUI types for full API compliance:

| MAUI Type | Description |
|-----------|-------------|
| `Color` | Colors (e.g., `Colors.Red`, `Color.FromRgb(255, 0, 0)`) |
| `Rect` | Rectangle bounds (x, y, width, height) |
| `Size` | Size measurements (width, height) |
| `Point` | Point coordinates (x, y) |
| `Thickness` | Padding/margins (left, top, right, bottom) |
| `double` | All numeric properties (not float) |

## View Controls

### Button

A clickable button control implementing `IButton`.

```csharp
public class Button : View, IButton
{
    // Text and appearance
    public string Text { get; set; }
    public Color TextColor { get; set; }
    public Color BackgroundColor { get; set; }
    public int CornerRadius { get; set; }
    public Color BorderColor { get; set; }
    public double BorderWidth { get; set; }

    // Font
    public string FontFamily { get; set; }
    public double FontSize { get; set; }
    public FontAttributes FontAttributes { get; set; }

    // Image
    public ImageSource ImageSource { get; set; }
    public ButtonContentLayout ContentLayout { get; set; }

    // Commands
    public ICommand Command { get; set; }
    public object CommandParameter { get; set; }

    // Events
    public event EventHandler Clicked;
    public event EventHandler Pressed;
    public event EventHandler Released;
}
```

### Entry

A text input control implementing `IEntry`.

```csharp
public class Entry : View, IEntry, ITextInput
{
    // Text
    public string Text { get; set; }
    public string Placeholder { get; set; }
    public Color TextColor { get; set; }
    public Color PlaceholderColor { get; set; }

    // Font
    public string FontFamily { get; set; }
    public double FontSize { get; set; }
    public FontAttributes FontAttributes { get; set; }
    public double CharacterSpacing { get; set; }

    // Behavior
    public bool IsPassword { get; set; }
    public int MaxLength { get; set; }
    public Keyboard Keyboard { get; set; }
    public ReturnType ReturnType { get; set; }
    public ClearButtonVisibility ClearButtonVisibility { get; set; }

    // Selection
    public int CursorPosition { get; set; }
    public int SelectionLength { get; set; }

    // Commands
    public ICommand ReturnCommand { get; set; }

    // Events
    public event EventHandler<TextChangedEventArgs> TextChanged;
    public event EventHandler Completed;
}
```

### Label

A text display control implementing `ILabel`.

```csharp
public class Label : View, ILabel
{
    // Text
    public string Text { get; set; }
    public FormattedString FormattedText { get; set; }
    public Color TextColor { get; set; }

    // Font
    public string FontFamily { get; set; }
    public double FontSize { get; set; }
    public FontAttributes FontAttributes { get; set; }
    public double CharacterSpacing { get; set; }

    // Layout
    public TextAlignment HorizontalTextAlignment { get; set; }
    public TextAlignment VerticalTextAlignment { get; set; }
    public LineBreakMode LineBreakMode { get; set; }
    public int MaxLines { get; set; }
    public double LineHeight { get; set; }

    // Decoration
    public TextDecorations TextDecorations { get; set; }
    public TextTransform TextTransform { get; set; }
}
```

### Slider

A value slider control implementing `ISlider`.

```csharp
public class Slider : View, ISlider
{
    // Value
    public double Value { get; set; }
    public double Minimum { get; set; }  // Default: 0.0
    public double Maximum { get; set; }  // Default: 1.0

    // Colors
    public Color MinimumTrackColor { get; set; }
    public Color MaximumTrackColor { get; set; }
    public Color ThumbColor { get; set; }

    // Events
    public event EventHandler<ValueChangedEventArgs> ValueChanged;
    public event EventHandler DragStarted;
    public event EventHandler DragCompleted;
}
```

### Image

An image display control implementing `IImage`.

```csharp
public class Image : View, IImage
{
    public ImageSource Source { get; set; }
    public Aspect Aspect { get; set; }
    public bool IsOpaque { get; set; }
    public bool IsAnimationPlaying { get; set; }
    public bool IsLoading { get; }
}
```

### CheckBox

A checkbox control implementing `ICheckBox`.

```csharp
public class CheckBox : View, ICheckBox
{
    public bool IsChecked { get; set; }
    public Color Color { get; set; }

    public event EventHandler<CheckedChangedEventArgs> CheckedChanged;
}
```

### Switch

A toggle switch control implementing `ISwitch`.

```csharp
public class Switch : View, ISwitch
{
    public bool IsOn { get; set; }
    public Color OnColor { get; set; }
    public Color ThumbColor { get; set; }

    public event EventHandler<ToggledEventArgs> Toggled;
}
```

## Layout Controls

### StackLayout

Arranges children in a stack.

```csharp
public class StackLayout : Layout
{
    public StackOrientation Orientation { get; set; }
    public double Spacing { get; set; }
}

public class VerticalStackLayout : StackLayout { }
public class HorizontalStackLayout : StackLayout { }
```

### Grid

Arranges children in a grid.

```csharp
public class Grid : Layout
{
    public RowDefinitionCollection RowDefinitions { get; }
    public ColumnDefinitionCollection ColumnDefinitions { get; }
    public double RowSpacing { get; set; }
    public double ColumnSpacing { get; set; }

    // Attached properties
    public static int GetRow(BindableObject view);
    public static void SetRow(BindableObject view, int row);
    public static int GetColumn(BindableObject view);
    public static void SetColumn(BindableObject view, int column);
    public static int GetRowSpan(BindableObject view);
    public static void SetRowSpan(BindableObject view, int span);
    public static int GetColumnSpan(BindableObject view);
    public static void SetColumnSpan(BindableObject view, int span);
}
```

### FlexLayout

CSS Flexbox-compatible layout.

```csharp
public class FlexLayout : Layout
{
    public FlexDirection Direction { get; set; }
    public FlexWrap Wrap { get; set; }
    public FlexJustify JustifyContent { get; set; }
    public FlexAlignItems AlignItems { get; set; }
    public FlexAlignContent AlignContent { get; set; }

    // Attached properties
    public static int GetOrder(BindableObject view);
    public static float GetGrow(BindableObject view);
    public static float GetShrink(BindableObject view);
    public static FlexBasis GetBasis(BindableObject view);
    public static FlexAlignSelf GetAlignSelf(BindableObject view);
}
```

### ScrollView

A scrollable container.

```csharp
public class ScrollView : Layout, IScrollView
{
    public View Content { get; set; }
    public ScrollOrientation Orientation { get; set; }
    public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; }
    public ScrollBarVisibility VerticalScrollBarVisibility { get; set; }
    public double ScrollX { get; }
    public double ScrollY { get; }
    public Size ContentSize { get; }

    public Task ScrollToAsync(double x, double y, bool animated);
    public Task ScrollToAsync(Element element, ScrollToPosition position, bool animated);

    public event EventHandler<ScrolledEventArgs> Scrolled;
}
```

## Collection Views

### CollectionView

A virtualized list/grid control.

```csharp
public class CollectionView : ItemsView
{
    public IEnumerable ItemsSource { get; set; }
    public DataTemplate ItemTemplate { get; set; }
    public IItemsLayout ItemsLayout { get; set; }
    public SelectionMode SelectionMode { get; set; }
    public object SelectedItem { get; set; }
    public IList<object> SelectedItems { get; }
    public View EmptyView { get; set; }
    public DataTemplate EmptyViewTemplate { get; set; }
    public object Header { get; set; }
    public object Footer { get; set; }

    public event EventHandler<SelectionChangedEventArgs> SelectionChanged;
}
```

### CarouselView

A carousel/pager control.

```csharp
public class CarouselView : ItemsView
{
    public bool Loop { get; set; }
    public bool IsSwipeEnabled { get; set; }
    public int Position { get; set; }
    public Thickness PeekAreaInsets { get; set; }

    public event EventHandler<PositionChangedEventArgs> PositionChanged;
}
```

### RefreshView

Pull-to-refresh container.

```csharp
public class RefreshView : ContentView
{
    public bool IsRefreshing { get; set; }
    public ICommand Command { get; set; }
    public object CommandParameter { get; set; }
    public Color RefreshColor { get; set; }

    public event EventHandler Refreshing;
}
```

### SwipeView

Swipe-to-reveal actions.

```csharp
public class SwipeView : ContentView
{
    public SwipeItems LeftItems { get; set; }
    public SwipeItems RightItems { get; set; }
    public SwipeItems TopItems { get; set; }
    public SwipeItems BottomItems { get; set; }

    public void Open(OpenSwipeItem openSwipeItem);
    public void Close();

    public event EventHandler<SwipeStartedEventArgs> SwipeStarted;
    public event EventHandler<SwipeEndedEventArgs> SwipeEnded;
}
```

## Navigation

### NavigationPage

Stack-based navigation.

```csharp
public class NavigationPage : Page
{
    public Page CurrentPage { get; }
    public Page RootPage { get; }
    public Color BarBackgroundColor { get; set; }
    public Color BarTextColor { get; set; }
    public bool HasNavigationBar { get; set; }

    public Task PushAsync(Page page, bool animated = true);
    public Task<Page> PopAsync(bool animated = true);
    public Task PopToRootAsync(bool animated = true);

    public event EventHandler<NavigationEventArgs> Pushed;
    public event EventHandler<NavigationEventArgs> Popped;
    public event EventHandler<NavigationEventArgs> PoppedToRoot;
}
```

### TabbedPage

Tab-based navigation.

```csharp
public class TabbedPage : Page
{
    public IList<Page> Children { get; }
    public Page CurrentPage { get; set; }
    public Color BarBackgroundColor { get; set; }
    public Color SelectedTabColor { get; set; }
    public Color UnselectedTabColor { get; set; }

    public event EventHandler CurrentPageChanged;
}
```

### FlyoutPage

Flyout/drawer navigation.

```csharp
public class FlyoutPage : Page
{
    public Page Flyout { get; set; }
    public Page Detail { get; set; }
    public bool IsPresented { get; set; }
    public FlyoutLayoutBehavior FlyoutLayoutBehavior { get; set; }
    public bool IsGestureEnabled { get; set; }

    public event EventHandler IsPresentedChanged;
}
```

### Shell

Comprehensive navigation with URI routing.

```csharp
public class Shell : Page
{
    public IList<ShellItem> Items { get; }
    public ShellItem CurrentItem { get; set; }
    public ShellFlyoutBehavior FlyoutBehavior { get; set; }
    public bool FlyoutIsPresented { get; set; }
    public Color FlyoutBackgroundColor { get; set; }
    public object FlyoutHeader { get; set; }
    public object FlyoutFooter { get; set; }

    public static void RegisterRoute(string route, Type pageType);
    public Task GoToAsync(string route);
    public Task GoToAsync(ShellNavigationState state);

    public event EventHandler<ShellNavigatedEventArgs> Navigated;
    public event EventHandler<ShellNavigatingEventArgs> Navigating;
}
```

## Platform Services

### IClipboard

```csharp
public interface IClipboard
{
    bool HasText { get; }
    Task<string> GetTextAsync();
    Task SetTextAsync(string text);
    event EventHandler<EventArgs> ClipboardContentChanged;
}
```

### IFilePicker

```csharp
public interface IFilePicker
{
    Task<FileResult> PickAsync(PickOptions options = null);
    Task<IEnumerable<FileResult>> PickMultipleAsync(PickOptions options = null);
}
```

### IShare

```csharp
public interface IShare
{
    Task RequestAsync(ShareTextRequest request);
    Task RequestAsync(ShareFileRequest request);
    Task RequestAsync(ShareMultipleFilesRequest request);
}
```

### ILauncher

```csharp
public interface ILauncher
{
    Task<bool> CanOpenAsync(Uri uri);
    Task<bool> OpenAsync(Uri uri);
    Task<bool> TryOpenAsync(Uri uri);
}
```

### IBrowser

```csharp
public interface IBrowser
{
    Task OpenAsync(Uri uri, BrowserLaunchOptions options);
}
```

### IEmail

```csharp
public interface IEmail
{
    bool IsComposeSupported { get; }
    Task ComposeAsync(EmailMessage message);
}
```

### IPreferences

```csharp
public interface IPreferences
{
    bool ContainsKey(string key, string sharedName = null);
    void Remove(string key, string sharedName = null);
    void Clear(string sharedName = null);
    T Get<T>(string key, T defaultValue, string sharedName = null);
    void Set<T>(string key, T value, string sharedName = null);
}
```

### ISecureStorage

```csharp
public interface ISecureStorage
{
    Task<string> GetAsync(string key);
    Task SetAsync(string key, string value);
    bool Remove(string key);
    void RemoveAll();
}
```

## Accessibility

### IAccessible

Interface for accessible UI elements.

```csharp
public interface IAccessible
{
    string AccessibleId { get; }
    string AccessibleName { get; }
    string AccessibleDescription { get; }
    AccessibleRole Role { get; }
    AccessibleStates States { get; }
    IAccessible Parent { get; }
    IReadOnlyList<IAccessible> Children { get; }
    AccessibleRect Bounds { get; }
    IReadOnlyList<AccessibleAction> Actions { get; }

    bool DoAction(string actionName);
}
```

### IAccessibilityService

```csharp
public interface IAccessibilityService
{
    bool IsEnabled { get; }

    void Initialize();
    void Register(IAccessible accessible);
    void Unregister(IAccessible accessible);
    void NotifyFocusChanged(IAccessible accessible);
    void NotifyPropertyChanged(IAccessible accessible, AccessibleProperty property);
    void NotifyStateChanged(IAccessible accessible, AccessibleStates state, bool value);
    void Announce(string text, AnnouncementPriority priority = AnnouncementPriority.Polite);
    void Shutdown();
}
```

## Input Method (IME)

### IInputMethodService

```csharp
public interface IInputMethodService
{
    bool IsActive { get; }
    string PreEditText { get; }

    void Initialize(IntPtr windowHandle);
    void SetFocus(IInputContext context);
    void SetCursorLocation(int x, int y, int width, int height);
    bool ProcessKeyEvent(uint keyCode, KeyModifiers modifiers, bool isKeyDown);
    void Reset();
    void Shutdown();

    event EventHandler<TextCommittedEventArgs> TextCommitted;
    event EventHandler<PreEditChangedEventArgs> PreEditChanged;
}
```

## Event Arguments

```csharp
public class TextChangedEventArgs : EventArgs
{
    public string OldTextValue { get; }
    public string NewTextValue { get; }
}

public class ValueChangedEventArgs : EventArgs
{
    public double OldValue { get; }
    public double NewValue { get; }
}

public class CheckedChangedEventArgs : EventArgs
{
    public bool Value { get; }
}

public class ToggledEventArgs : EventArgs
{
    public bool Value { get; }
}

public class SelectionChangedEventArgs : EventArgs
{
    public IReadOnlyList<object> PreviousSelection { get; }
    public IReadOnlyList<object> CurrentSelection { get; }
}

public class PositionChangedEventArgs : EventArgs
{
    public int PreviousPosition { get; }
    public int CurrentPosition { get; }
}

public class ScrolledEventArgs : EventArgs
{
    public double ScrollX { get; }
    public double ScrollY { get; }
}
```

## Enumerations

### Common Enums

```csharp
public enum Aspect { AspectFit, AspectFill, Fill, Center }
public enum TextAlignment { Start, Center, End }
public enum LineBreakMode { NoWrap, WordWrap, CharacterWrap, HeadTruncation, TailTruncation, MiddleTruncation }
public enum FontAttributes { None, Bold, Italic }
public enum TextTransform { None, Default, Lowercase, Uppercase }
public enum TextDecorations { None, Underline, Strikethrough }
public enum ReturnType { Default, Done, Go, Next, Search, Send }
public enum Keyboard { Default, Chat, Email, Numeric, Telephone, Text, Url }
public enum ClearButtonVisibility { Never, WhileEditing }
public enum SelectionMode { None, Single, Multiple }
public enum ScrollOrientation { Vertical, Horizontal, Both, Neither }
public enum ScrollBarVisibility { Default, Always, Never }
public enum StackOrientation { Vertical, Horizontal }
public enum FlyoutLayoutBehavior { Default, Popover, Split }
public enum ShellFlyoutBehavior { Disabled, Flyout, Locked }
```

### Accessibility Enums

```csharp
public enum AccessibleRole
{
    Unknown, Window, Application, Panel, Frame, Button,
    CheckBox, RadioButton, ComboBox, Entry, Label,
    List, ListItem, Menu, MenuItem, ScrollBar,
    Slider, StatusBar, Tab, TabPanel, Text, ProgressBar,
    SpinButton, Table, TableCell, TableRow, ToolBar,
    TreeItem, TreeView, // ... and more
}

[Flags]
public enum AccessibleStates
{
    None = 0,
    Active = 1 << 0,
    Checked = 1 << 1,
    Collapsed = 1 << 2,
    Enabled = 1 << 3,
    Expanded = 1 << 4,
    Focusable = 1 << 5,
    Focused = 1 << 6,
    Selected = 1 << 7,
    Visible = 1 << 8,
    // ... and more
}

public enum AnnouncementPriority { Polite, Assertive }
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `MAUI_DISPLAY_SERVER` | Force display server: `x11`, `wayland`, or `auto` |
| `MAUI_INPUT_METHOD` | Force IME: `ibus`, `fcitx5`, `xim`, or `none` |
| `GTK_A11Y` | Set to `none` to disable accessibility |
| `DISPLAY` | X11 display to connect to |
| `WAYLAND_DISPLAY` | Wayland display to connect to |

## System Requirements

- .NET 9.0 SDK or later
- Linux (kernel 5.4+)
- X11 or Wayland display server
- SkiaSharp native libraries (included via NuGet)

### Optional Dependencies

| Package | Purpose |
|---------|---------|
| libibus-1.0 | IBus input method support |
| libatspi | AT-SPI2 accessibility support |
| libnotify | Desktop notification support |
| xclip/xsel | Clipboard support |
| zenity/kdialog | Native file dialogs |
