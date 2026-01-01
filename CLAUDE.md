# CLAUDE.md - OpenMaui XAML Reconstruction

## CURRENT TASK: Reconstruct XAML from Decompiled Code

The sample applications (ShellDemo, TodoApp, XamlBrowser) were recovered from decompiled DLLs. The XAML files were compiled away - we have only the generated `InitializeComponent()` code. **Screenshots will be provided** to help verify visual accuracy.

---

## Project Locations

| What | Path |
|------|------|
| **Main codebase** | `/Users/nible/Documents/GitHub/maui-linux-main/` |
| **Samples (target)** | `/Users/nible/Documents/GitHub/maui-linux-main/samples_temp/` |
| **Decompiled samples** | `/Users/nible/Documents/GitHub/recovered/source/` |

---

## Git Branch

**Work on `final` branch.** Commit frequently.

```bash
git branch  # Should show: * final
```

---

## XAML Reconstruction Overview

### ShellDemo (10 pages + shell + app)

| File | Status | Notes |
|------|--------|-------|
| App.xaml | [ ] | Colors, Styles (ThemedEntry, TitleLabel, etc.) |
| AppShell.xaml | [ ] | Shell with FlyoutHeader, 9 FlyoutItems |
| HomePage.xaml | [ ] | Welcome screen with logo |
| ButtonsPage.xaml | [ ] | Button demos |
| TextInputPage.xaml | [ ] | Entry/Editor demos |
| SelectionPage.xaml | [ ] | CheckBox, Switch, RadioButton demos |
| PickersPage.xaml | [ ] | DatePicker, TimePicker, Picker demos |
| ListsPage.xaml | [ ] | CollectionView demos |
| ProgressPage.xaml | [ ] | ProgressBar, ActivityIndicator demos |
| GridsPage.xaml | [ ] | Grid layout demos |
| AboutPage.xaml | [ ] | About information |
| DetailPage.xaml | [ ] | Navigation detail page |

### TodoApp (app + 3 pages)

| File | Status | Notes |
|------|--------|-------|
| App.xaml | [ ] | Colors, Icon strings |
| TodoListPage.xaml | [ ] | Main list with swipe actions |
| NewTodoPage.xaml | [ ] | Add new todo form |
| TodoDetailPage.xaml | [ ] | Edit todo details |

### XamlBrowser (app + 1 page)

| File | Status | Notes |
|------|--------|-------|
| BrowserApp.xaml | [ ] | Basic app setup |
| MainPage.xaml | [ ] | WebView browser |

---

## How to Reconstruct XAML

### Step 1: Read the decompiled InitializeComponent()

Look for patterns like:
```csharp
// Setting a property
((BindableObject)val8).SetValue(Label.TextProperty, (object)"OpenMaui");

// AppThemeBinding (light/dark mode)
val7.Light = "White";
val7.Dark = "#E0E0E0";

// StaticResource
val.Key = "PrimaryColor";

// Layout hierarchy
((Layout)val12).Children.Add((IView)(object)val6);
```

### Step 2: Convert to XAML

```csharp
// This C#:
((BindableObject)val8).SetValue(Label.TextProperty, (object)"OpenMaui");
((BindableObject)val8).SetValue(Label.FontSizeProperty, (object)22.0);
((BindableObject)val8).SetValue(Label.FontAttributesProperty, (object)(FontAttributes)1);
val7.Light = "White";
val7.Dark = "#E0E0E0";
((BindableObject)val8).SetBinding(Label.TextColorProperty, val74);
```

```xml
<!-- Becomes this XAML: -->
<Label Text="OpenMaui"
       FontSize="22"
       FontAttributes="Bold"
       TextColor="{AppThemeBinding Light=White, Dark=#E0E0E0}" />
```

### Step 3: Verify with screenshots (when provided)

Compare the reconstructed XAML against the actual screenshots to ensure visual fidelity.

---

## App.xaml Resources Reference

### ShellDemo Colors (extracted from decompiled)

```xml
<!-- Light theme -->
<Color x:Key="PrimaryColor">#2196F3</Color>
<Color x:Key="PrimaryDarkColor">#1976D2</Color>
<Color x:Key="AccentColor">#FF4081</Color>
<Color x:Key="PageBackgroundLight">#F8F8F8</Color>
<Color x:Key="CardBackgroundLight">#FFFFFF</Color>
<Color x:Key="TextPrimaryLight">#212121</Color>
<Color x:Key="TextSecondaryLight">#757575</Color>
<Color x:Key="BorderLight">#E0E0E0</Color>
<Color x:Key="EntryBackgroundLight">#F9F9F9</Color>
<Color x:Key="ShellBackgroundLight">#FFFFFF</Color>
<Color x:Key="FlyoutBackgroundLight">#FFFFFF</Color>
<Color x:Key="ProgressTrackLight">#E0E0E0</Color>

<!-- Dark theme -->
<Color x:Key="PageBackgroundDark">#121212</Color>
<Color x:Key="CardBackgroundDark">#1E1E1E</Color>
<Color x:Key="TextPrimaryDark">#FFFFFF</Color>
<Color x:Key="TextSecondaryDark">#B0B0B0</Color>
<Color x:Key="BorderDark">#424242</Color>
<Color x:Key="EntryBackgroundDark">#2C2C2C</Color>
<Color x:Key="ShellBackgroundDark">#1E1E1E</Color>
<Color x:Key="FlyoutBackgroundDark">#1E1E1E</Color>
<Color x:Key="ProgressTrackDark">#424242</Color>
```

### ShellDemo Styles (extracted from decompiled)

- **ThemedEntry**: BackgroundColor, TextColor, PlaceholderColor with AppThemeBinding
- **ThemedEditor**: BackgroundColor, TextColor, PlaceholderColor with AppThemeBinding
- **TitleLabel**: FontSize=24, FontAttributes=Bold, TextColor with AppThemeBinding
- **SubtitleLabel**: FontSize=16, TextColor with AppThemeBinding
- **ThemedFrame**: BackgroundColor, BorderColor with AppThemeBinding
- **ThemedProgressBar**: ProgressColor=PrimaryColor, BackgroundColor with AppThemeBinding
- **PrimaryButton**: BackgroundColor=PrimaryColor, TextColor=White
- **SecondaryButton**: Light/dark themed background and text

### TodoApp Colors

```xml
<Color x:Key="PrimaryColor">#5C6BC0</Color>
<Color x:Key="PrimaryDarkColor">#3949AB</Color>
<Color x:Key="AccentColor">#26A69A</Color>
<Color x:Key="DangerColor">#EF5350</Color>
<!-- ... plus light/dark theme colors -->
```

### TodoApp Icons (Material Design)

```xml
<x:String x:Key="IconAdd">&#xe145;</x:String>
<x:String x:Key="IconDelete">&#xe872;</x:String>
<x:String x:Key="IconSave">&#xe161;</x:String>
<x:String x:Key="IconCheck">&#xe876;</x:String>
<x:String x:Key="IconEdit">&#xe3c9;</x:String>
```

---

## AppShell.xaml Structure (ShellDemo)

From decompiled code, the shell has:

```xml
<Shell Title="OpenMaui Controls Demo"
       FlyoutBehavior="Flyout"
       FlyoutBackgroundColor="{AppThemeBinding Light={StaticResource FlyoutBackgroundLight}, Dark={StaticResource FlyoutBackgroundDark}}">

    <!-- FlyoutHeader: Grid with logo and title -->
    <Shell.FlyoutHeader>
        <Grid BackgroundColor="{AppThemeBinding ...}" HeightRequest="140" Padding="15">
            <HorizontalStackLayout VerticalOptions="Center" Spacing="12">
                <Image Source="openmaui_logo.svg" WidthRequest="60" HeightRequest="60" />
                <VerticalStackLayout VerticalOptions="Center">
                    <Label Text="OpenMaui" FontSize="22" FontAttributes="Bold"
                           TextColor="{AppThemeBinding Light=White, Dark=#E0E0E0}" />
                    <Label Text="Controls Demo" FontSize="13" Opacity="0.9"
                           TextColor="{AppThemeBinding Light=White, Dark=#B0B0B0}" />
                </VerticalStackLayout>
            </HorizontalStackLayout>
        </Grid>
    </Shell.FlyoutHeader>

    <!-- FlyoutItems with emoji icons -->
    <FlyoutItem Title="Home" Route="Home">
        <FlyoutItem.Icon><FontImageSource Glyph="🏠" FontFamily="Default" Color="{AppThemeBinding ...}" /></FlyoutItem.Icon>
        <ShellContent ContentTemplate="{DataTemplate local:HomePage}" />
    </FlyoutItem>

    <FlyoutItem Title="Buttons" Route="Buttons">...</FlyoutItem>
    <FlyoutItem Title="Text Input" Route="TextInput">...</FlyoutItem>
    <FlyoutItem Title="Selection" Route="Selection">...</FlyoutItem>
    <FlyoutItem Title="Pickers" Route="Pickers">...</FlyoutItem>
    <FlyoutItem Title="Lists" Route="Lists">...</FlyoutItem>
    <FlyoutItem Title="Progress" Route="Progress">...</FlyoutItem>
    <FlyoutItem Title="Grids" Route="Grids">...</FlyoutItem>
    <FlyoutItem Title="About" Route="About">...</FlyoutItem>
</Shell>
```

---

## Decompiled File Locations

| Sample | Decompiled Path |
|--------|-----------------|
| ShellDemo | `/Users/nible/Documents/GitHub/recovered/source/ShellDemo/ShellDemo/` |
| TodoApp | `/Users/nible/Documents/GitHub/recovered/source/TodoApp/TodoApp/` |
| XamlBrowser | `/Users/nible/Documents/GitHub/recovered/source/XamlBrowser/XamlBrowser/` |

---

## Build Command

```bash
cd /Users/nible/Documents/GitHub/maui-linux-main
dotnet build OpenMaui.Controls.Linux.csproj
```

---

## Key Patterns in Decompiled Code

### 1. Color Values
```csharp
Color val = new Color(11f / 85f, 0.5882353f, 81f / 85f, 1f);
// = Color.FromRgba(0.129, 0.588, 0.953, 1.0) = #2196F3
```

### 2. AppThemeBinding
```csharp
AppThemeBindingExtension val7 = new AppThemeBindingExtension();
val7.Light = "White";
val7.Dark = "#E0E0E0";
```
Becomes: `{AppThemeBinding Light=White, Dark=#E0E0E0}`

### 3. StaticResource
```csharp
val.Key = "PrimaryColor";
```
Becomes: `{StaticResource PrimaryColor}`

### 4. Layout Hierarchy
```csharp
((Layout)val12).Children.Add((IView)(object)val6);
((Layout)val12).Children.Add((IView)(object)val11);
```
The children are added in order - first child is val6, second is val11.

### 5. FontAttributes Enum
```csharp
(FontAttributes)1  // Bold
(FontAttributes)2  // Italic
```

---

## Workflow for Each File

1. **Read decompiled** `InitializeComponent()` method
2. **Extract** all UI elements and their properties
3. **Write XAML** with proper structure
4. **Create code-behind** (usually just constructor calling InitializeComponent)
5. **Verify** against screenshot if available
6. **Update tracking** in this file
7. **Commit** with descriptive message

---

## Notes

- The decompiled code has ALL the information needed - it's just in C# form instead of XAML
- Screenshots will help verify visual accuracy
- Focus on one file at a time
- Commit after each completed file
