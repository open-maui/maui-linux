# Fix Fuckup Recovery Plan

## What Happened
Code was stored in /tmp directory which got cleared on restart. Recovered code from decompiled VM binaries.

## What Was Lost
The decompiled code has all the **logic** but:
1. **XAML files are gone** - they were compiled to C# code
2. **AppThemeBinding additions** - dark/light mode XAML bindings
3. **Original formatting/comments** - decompiler output is messy

## Recovery Order

### Step 1: Fix maui-linux Library First
The library code is recovered and functional. Build and verify:

```bash
cd ~/Documents/GitHub/maui-linux-main
dotnet build
```

### Step 2: Recreate Sample XAML with AppThemeBinding

#### ShellDemo XAML to Recreate
All pages had AppThemeBinding added for dark/light mode:

- [ ] **AppShell.xaml** - FlyoutHeader with:
  - VerticalStackLayout (logo above text)
  - Image with AspectFit
  - BackgroundColor: `{AppThemeBinding Light=#F0F0F0, Dark=#2A2A2A}`
  - TextColor bindings for labels

- [ ] **HomePage.xaml** - AppThemeBinding for:
  - BackgroundColor
  - TextColor
  - Button colors

- [ ] **ButtonsPage.xaml** - AppThemeBinding colors
- [ ] **TextInputPage.xaml** - Entry/Editor theme colors
- [ ] **PickersPage.xaml** - Picker theme colors
- [ ] **ProgressPage.xaml** - ProgressBar theme colors
- [ ] **SelectionPage.xaml** - CheckBox/Switch theme colors
- [ ] **ListsPage.xaml** - CollectionView theme colors
- [ ] **GridsPage.xaml** - Grid theme colors
- [ ] **AboutPage.xaml** - Links with tap gestures, theme colors
- [ ] **DetailPage.xaml** - Theme colors

#### TodoApp XAML to Recreate
- [ ] **TodoListPage.xaml** - AppThemeBinding for:
  - Page background
  - List item colors
  - Button colors

- [ ] **TodoDetailPage.xaml** - Theme colors
- [ ] **NewTodoPage.xaml** - Theme colors

#### XamlBrowser XAML to Recreate
- [ ] **MainPage.xaml** - WebView container with theme

## AppThemeBinding Pattern
All XAML used this pattern:
```xml
<Label TextColor="{AppThemeBinding Light=#333333, Dark=#E0E0E0}" />
<Grid BackgroundColor="{AppThemeBinding Light=#FFFFFF, Dark=#1E1E1E}" />
<Button BackgroundColor="{AppThemeBinding Light=#2196F3, Dark=#1976D2}" />
```

## FlyoutHeader Specifics
The FlyoutHeader had this structure:
```xml
<Shell.FlyoutHeader>
    <Grid BackgroundColor="{AppThemeBinding Light=#F0F0F0, Dark=#2A2A2A}"
          HeightRequest="160"
          Padding="15">
        <VerticalStackLayout VerticalOptions="Center"
                             HorizontalOptions="Center"
                             Spacing="8">
            <Image Source="openmaui_logo.svg"
                   WidthRequest="70"
                   HeightRequest="70"
                   Aspect="AspectFit"/>
            <Label Text="OpenMaui"
                   FontSize="20"
                   FontAttributes="Bold"
                   HorizontalOptions="Center"
                   TextColor="{AppThemeBinding Light=#333333, Dark=#E0E0E0}"/>
            <Label Text="Controls Demo"
                   FontSize="12"
                   HorizontalOptions="Center"
                   TextColor="{AppThemeBinding Light=#666666, Dark=#B0B0B0}"/>
        </VerticalStackLayout>
    </Grid>
</Shell.FlyoutHeader>
```

## Screenshots Needed
User can take screenshots of running app to recreate XAML:

1. **ShellDemo Flyout open** - Light mode
2. **ShellDemo Flyout open** - Dark mode
3. **Each page** - Light and dark mode
4. **TodoApp** - Light and dark mode

## Key Features Recovered in Library

### SkiaShell (1325 lines)
- [x] FlyoutHeaderView, FlyoutHeaderHeight
- [x] FlyoutFooterText, FlyoutFooterHeight
- [x] Flyout scrolling
- [x] All BindableProperties for theming

### X11Window
- [x] Cursor support (XCreateFontCursor, XDefineCursor)
- [x] CursorType enum

### Theme Support
- [x] SystemThemeService
- [x] UserAppTheme detection
- [x] Theme-aware handlers

## File Locations

| Item | Path |
|------|------|
| Library | `~/Documents/GitHub/maui-linux-main` |
| Samples | `~/Documents/GitHub/maui-linux-samples-main` |
| Recovered backup | `~/Documents/GitHub/recovered/` |

## Build & Deploy Commands

```bash
# Build library
cd ~/Documents/GitHub/maui-linux-main
dotnet build

# Build ShellDemo
cd ~/Documents/GitHub/maui-linux-samples-main/ShellDemo
dotnet publish -c Release -r linux-arm64 --self-contained

# Deploy
sshpass -p Basilisk scp -r bin/Release/net9.0/linux-arm64/publish/* marketally@172.16.1.128:~/shelltest/

# Run
sshpass -p Basilisk ssh marketally@172.16.1.128 "cd ~/shelltest && DISPLAY=:0 XAUTHORITY=/run/user/1000/.mutter-Xwaylandauth.* ./ShellDemo"
```

## CRITICAL RULES

1. **NEVER use /tmp** - always use ~/Documents/GitHub/
2. **Commit and push after EVERY significant change**
3. **Only push to dev branch** - main has CI/CD actions
