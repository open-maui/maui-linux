# Changelog

All notable changes to this project will be documented in this file.

## [1.0.0] - 2026-03-06

### Added
- 35+ Skia-rendered controls: Button, Label, Entry, Editor, CheckBox, Switch, RadioButton, Slider, Stepper, Picker, DatePicker, TimePicker, SearchBar, Image, ImageButton, ProgressBar, ActivityIndicator, BoxView, Border, Frame, ScrollView, CollectionView, CarouselView, IndicatorView, SwipeView, RefreshView, GraphicsView, WebView, MenuBar
- Navigation: NavigationPage, TabbedPage, FlyoutPage, Shell
- Full XAML support with BindableProperty for all controls
- Visual State Manager integration (Normal, PointerOver, Pressed, Focused, Disabled)
- Data binding (OneWay, TwoWay, OneTime) with IValueConverter support
- XAML styles, StaticResource, DynamicResource, merged ResourceDictionaries
- X11 display server support with full input handling
- Wayland support with XWayland fallback
- SkiaSharp hardware-accelerated rendering with dirty region optimization
- AT-SPI2 accessibility support (screen reader integration)
- High contrast mode detection and color palette support
- Input method support (IBus, Fcitx5, XIM)
- HiDPI automatic scale factor detection (GNOME, KDE, X11)
- Platform services: Clipboard, FilePicker, FolderPicker, Notifications, GlobalHotkeys, DragDrop, Launcher, Share, SecureStorage, Preferences, Browser, Email, SystemTray, VersionTracking, AppActions
- Gesture recognition: Tap, Pan, Swipe, Pinch, Pointer, Drag/Drop
- Project templates: `openmaui-linux` (code-based) and `openmaui-linux-xaml` (XAML-based)
- Visual Studio extension with project templates and launch profiles
- DiagnosticLog centralized logging infrastructure (conditional on DEBUG builds)
- Configurable gesture thresholds (SwipeMinDistance, SwipeMaxTime, etc.)
- Exception-safe rendering pipeline

### Fixed
- GestureManager memory leak (view tracking dictionaries now cleaned up on dispose)
- Text binding recursion guard in EntryHandler
- Rendering pipeline crash protection (exceptions in view Draw no longer crash the app)

## [1.0.0-rc.1] - 2026-02-01

### Added
- 100% .NET MAUI API compliance - all public APIs use MAUI types
- 217 passing unit tests

## [1.0.0-preview.1] - 2025-06-01

### Added
- Initial preview release
- Core rendering engine
- Basic control set
