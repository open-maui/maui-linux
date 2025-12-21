# ShellDemo Sample

A comprehensive control showcase application demonstrating all OpenMaui Linux controls with Shell navigation and flyout menu.

## Features

- **Shell Navigation** - Flyout menu with multiple pages
- **Route-Based Navigation** - Push navigation with registered routes
- **All Core Controls** - Button, Entry, Editor, CheckBox, Switch, Slider, Picker, etc.
- **CollectionView** - Lists with selection and data binding
- **Progress Indicators** - ProgressBar and ActivityIndicator with animations
- **Grid Layouts** - Complex multi-column/row layouts
- **Event Logging** - Real-time event feedback panel

## Pages

| Page | Controls Demonstrated |
|------|----------------------|
| **Home** | Welcome screen, navigation overview |
| **Buttons** | Button styles, colors, states, click/press/release events |
| **Text Input** | Entry, Editor, SearchBar, password fields, keyboard types |
| **Selection** | CheckBox, Switch, Slider with colors and states |
| **Pickers** | Picker, DatePicker, TimePicker with styling |
| **Lists** | CollectionView with selection, custom items |
| **Progress** | ProgressBar, ActivityIndicator, animated demos |
| **Grids** | Grid layouts with row/column definitions |
| **About** | App information |

## Architecture

```
ShellDemo/
├── App.cs                      # AppShell definition with flyout
├── Program.cs                  # Linux platform bootstrap
├── MauiProgram.cs              # MAUI app builder
└── Pages/
    ├── HomePage.cs             # Welcome page
    ├── ButtonsPage.cs          # Button demonstrations
    ├── TextInputPage.cs        # Entry, Editor, SearchBar
    ├── SelectionPage.cs        # CheckBox, Switch, Slider
    ├── PickersPage.cs          # Picker, DatePicker, TimePicker
    ├── ListsPage.cs            # CollectionView demos
    ├── ProgressPage.cs         # ProgressBar, ActivityIndicator
    ├── GridsPage.cs            # Grid layout demos
    ├── DetailPage.cs           # Push navigation target
    └── AboutPage.cs            # About information
```

## Shell Configuration

```csharp
public class AppShell : Shell
{
    public AppShell()
    {
        FlyoutBehavior = FlyoutBehavior.Flyout;
        Title = "OpenMaui Controls Demo";

        // Register routes for push navigation
        Routing.RegisterRoute("detail", typeof(DetailPage));

        // Add flyout items
        Items.Add(CreateFlyoutItem("Home", typeof(HomePage)));
        Items.Add(CreateFlyoutItem("Buttons", typeof(ButtonsPage)));
        // ...more items
    }
}
```

## Control Demonstrations

### Buttons Page
- Default, styled, and transparent buttons
- Color variations (Primary, Success, Warning, Danger)
- Enabled/disabled state toggling
- Wide, tall, and round button shapes
- Pressed, clicked, released event handling

### Text Input Page
- Entry with placeholder and text change events
- Password entry with hidden text
- Email keyboard type
- SearchBar with search button
- Multi-line Editor
- Keyboard shortcuts guide

### Selection Page
- CheckBox with colors and disabled state
- Switch with OnColor customization
- Slider with min/max range and track colors

### Pickers Page
- Picker with items and selection events
- DatePicker with date range limits
- TimePicker with time selection
- Styled pickers with custom colors

### Lists Page
- CollectionView with string items
- CollectionView with custom data types (ColorItem, ContactItem)
- Selection handling and event feedback

### Progress Page
- ProgressBar at various percentages
- Colored progress bars
- ActivityIndicator running/stopped states
- Colored activity indicators
- Interactive slider-controlled progress
- Animated progress simulation

## Building and Running

```bash
# From the maui-linux-push directory
cd samples/ShellDemo
dotnet publish -c Release -r linux-arm64

# Run on Linux
./bin/Release/net9.0/linux-arm64/publish/ShellDemo
```

## Event Logging

Each page features an event log panel that displays control interactions in real-time:

```
[14:32:15] 3. Button clicked: Primary
[14:32:12] 2. Slider value: 75
[14:32:08] 1. CheckBox: Checked
```

## Controls Reference

| Control | Properties Demonstrated |
|---------|------------------------|
| Button | Text, BackgroundColor, TextColor, CornerRadius, IsEnabled, WidthRequest, HeightRequest |
| Entry | Placeholder, Text, IsPassword, Keyboard, FontSize |
| Editor | Placeholder, Text, HeightRequest |
| SearchBar | Placeholder, Text, SearchButtonPressed |
| CheckBox | IsChecked, Color, IsEnabled |
| Switch | IsToggled, OnColor, IsEnabled |
| Slider | Minimum, Maximum, Value, MinimumTrackColor, MaximumTrackColor, ThumbColor |
| Picker | Title, Items, SelectedIndex, TextColor, TitleColor |
| DatePicker | Date, MinimumDate, MaximumDate, TextColor |
| TimePicker | Time, TextColor |
| CollectionView | ItemsSource, SelectionMode, SelectionChanged, HeightRequest |
| ProgressBar | Progress, ProgressColor |
| ActivityIndicator | IsRunning, Color |
| Label | Text, FontSize, FontAttributes, TextColor |
| Frame | CornerRadius, Padding, BackgroundColor |
| Grid | RowDefinitions, ColumnDefinitions, RowSpacing, ColumnSpacing |
| StackLayout | Spacing, Padding, Orientation |
| ScrollView | Content scrolling |

## License

MIT License - See repository root for details.
