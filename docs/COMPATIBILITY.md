# OpenMaui Linux compatibility scorecard

Generated 2026-09-20 by `tools/Scorecard` from 1415 executed tests (1415 passed). Coverage is computed, not asserted: an item is covered only when at least one mapped test exists and every mapped test passed. The categories mirror the table Microsoft publishes for its maui-labs GTK4 backend so the two can be compared row for row.

## Implementation parity

| Category | Coverage | Items | Tests | Notes |
|----------|----------|-------|-------|-------|
| Core Infrastructure | 100% | 4/4 | 122 | Dispatcher, handler factory, rendering pipeline (raster and GPU targets) |
| Pages | 100% | 5/5 | 71 | ContentPage, NavigationPage, TabbedPage, FlyoutPage, Shell |
| Layouts | 100% | 8/8 | 71 | StackLayout, Grid, FlexLayout, AbsoluteLayout, ScrollView, ContentView, Border, Frame |
| Basic Controls | 100% | 14/14 | 142 | All 14 standard controls |
| Input Controls | 100% | 4/4 | 35 | Picker, DatePicker, TimePicker, SearchBar |
| Collection Controls | 100% | 7/7 | 125 | CollectionView, ListView, TableView, CarouselView, SwipeView, RefreshView, IndicatorView |
| Navigation & Routing | 100% | 4/4 | 28 | Push/pop, modal navigation, Shell routes, query parameters |
| Alerts & Dialogs | 100% | 4/4 | 36 | DisplayAlert, DisplayActionSheet, DisplayPromptAsync, modal pages |
| Gesture Recognizers | 100% | 6/6 | 85 | Tap, Pan, Pinch, Swipe, Pointer, Drag/Drop |
| Graphics & Shapes | 100% | 7/7 | 34 | GraphicsView + all 6 shape types |
| Font Management | 100% | 4/4 | 44 | Registrar, manager, FontImageSource, named sizes, fallback |
| WebView | 100% | 4/4 | 75 | URL, HTML, JavaScript, navigation events (WPE WebKit) |
| Animations | 100% | 3/3 | 48 | ViewExtensions, Animation class, ticker |
| VisualStateManager | 100% | 3/3 | 16 | Visual states, triggers, behaviors |
| ControlTemplate | 100% | 2/2 | 7 | ContentPresenter, TemplatedView |
| Base View Properties | 100% | 5/5 | 62 | Opacity, visibility, transforms, shadow, clip, automation |
| FormattedText | 100% | 3/3 | 29 | Span properties, decorations, fonts, span gestures |
| MenuBar | 100% | 2/2 | 45 | MenuBarItem, MenuFlyoutItem, submenus, separators, accelerators, context menus |
| Essentials | 100% | 36/36 | 141 | 36 services; desktop-inapplicable sensors report IsSupported=false |

**Overall: 125/125 items covered (100%).**

## Detail

### Core Infrastructure

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| Dispatcher | Covered | 7 | `LinuxDispatcherTests` |
| Handler factory | Covered | 29 | `HandlerRegistryTests` |
| Rendering pipeline | Covered | 31 | `RenderContextPropagationTests`, `RenderTargetTests`, `TextRenderingHelperTests` |
| Golden rendering at 1.0x-2.0x | Covered | 55 | `GoldenSceneTests` |

### Pages

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| ContentPage | Covered | 4 | `SkiaPageTests` |
| NavigationPage | Covered | 6 | `SkiaNavigationPageTests` |
| TabbedPage | Covered | 18 | `SkiaTabbedPageTests` |
| FlyoutPage | Covered | 22 | `SkiaFlyoutPageTests` |
| Shell | Covered | 21 | `ShellRoutingTests` |

### Layouts

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| StackLayout (vertical, horizontal) | Covered | 24 | `AlignmentTests`, `SkiaStackLayoutTests` |
| Grid | Covered | 4 | `SkiaGridTests` |
| FlexLayout | Covered | 15 | `SkiaFlexLayoutTests` |
| AbsoluteLayout | Covered | 8 | `SkiaAbsoluteLayoutTests` |
| ScrollView | Covered | 8 | `SkiaScrollViewTests` |
| ContentView | Covered | 3 | `ContentViewTests` |
| Border | Covered | 6 | `SkiaBorderTests` |
| Frame | Covered | 3 | `SkiaFrameTests` |

### Basic Controls

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| Label | Covered | 27 | `LabelPropertyMappingTests`, `SkiaLabelTests` |
| Button | Covered | 9 | `SkiaButtonTests` |
| Entry | Covered | 16 | `SkiaEntryTests` |
| Editor | Covered | 12 | `SkiaEditorTests` |
| CheckBox | Covered | 7 | `SkiaCheckBoxTests` |
| Switch | Covered | 8 | `SkiaSwitchTests` |
| Slider | Covered | 11 | `SkiaSliderTests` |
| Stepper | Covered | 11 | `SkiaStepperTests` |
| ProgressBar | Covered | 8 | `SkiaProgressBarTests` |
| ActivityIndicator | Covered | 5 | `SkiaActivityIndicatorTests` |
| Image | Covered | 7 | `SkiaImageTests` |
| ImageButton | Covered | 9 | `SkiaImageButtonTests` |
| BoxView | Covered | 3 | `SkiaBoxViewTests` |
| RadioButton | Covered | 9 | `SkiaRadioButtonTests` |

### Input Controls

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| Picker | Covered | 9 | `SkiaPickerTests` |
| DatePicker | Covered | 10 | `SkiaDatePickerTests` |
| TimePicker | Covered | 7 | `SkiaTimePickerTests` |
| SearchBar | Covered | 9 | `SkiaSearchBarTests` |

### Collection Controls

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| CollectionView | Covered | 11 | `SkiaCollectionViewTests` |
| ListView | Covered | 13 | `ListViewTests` |
| TableView | Covered | 21 | `SkiaTableViewTests` |
| CarouselView | Covered | 21 | `SkiaCarouselViewTests` |
| SwipeView | Covered | 19 | `SkiaSwipeViewTests` |
| RefreshView | Covered | 15 | `SkiaRefreshViewTests` |
| IndicatorView | Covered | 25 | `SkiaIndicatorViewTests` |

### Navigation & Routing

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| Push / pop | Covered | 6 | `SkiaNavigationPageTests` |
| Modal navigation | Covered | 13 | `ModalNavigationTests` |
| Shell routes | Covered | 5 | `ShellRoutingTests` |
| Query parameters | Covered | 4 | `ShellRoutingTests` |

### Alerts & Dialogs

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| DisplayAlert | Covered | 9 | `DialogBridgeTests` |
| DisplayActionSheet | Covered | 9 | `DialogBridgeTests` |
| DisplayPromptAsync | Covered | 5 | `DialogBridgeTests` |
| Modal pages | Covered | 13 | `ModalNavigationTests` |

### Gesture Recognizers

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| Tap | Covered | 16 | `GestureManagerPositionResolverTests`, `GestureRecognizerTests` |
| Pan | Covered | 4 | `GestureRecognizerTests` |
| Pinch | Covered | 4 | `GestureRecognizerTests` |
| Swipe | Covered | 10 | `GestureRecognizerTests` |
| Pointer | Covered | 3 | `GestureRecognizerTests` |
| Drag and drop | Covered | 48 | `DragPayloadTests`, `DropTargetTrackerTests`, `GestureRecognizerTests` |

### Graphics & Shapes

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| GraphicsView | Covered | 3 | `ShapeTests` |
| Rectangle | Covered | 6 | `ShapeTests` |
| Ellipse | Covered | 3 | `ShapeTests` |
| Line | Covered | 8 | `ShapeTests` |
| Path | Covered | 8 | `ShapeTests` |
| Polygon | Covered | 3 | `ShapeTests` |
| Polyline | Covered | 3 | `ShapeTests` |

### Font Management

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| Font registrar (AddFont) | Covered | 24 | `LinuxFontRegistrarTests` |
| Font manager / resolution | Covered | 2 | `FontFallbackManagerTests` |
| FontImageSource | Covered | 6 | `FontImageSourceTests` |
| Named sizes | Covered | 12 | `LinuxFontNamedSizeServiceTests` |

### WebView

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| URL and HTML sources | Covered | 46 | `WebViewHandlerTests`, `WpeWebViewSupportTests` |
| JavaScript evaluation | Covered | 23 | `WebViewHandlerTests`, `WpeWebViewSupportTests` |
| Navigation events | Covered | 2 | `WebViewHandlerTests` |
| Backend selection | Covered | 4 | `WpeWebViewSupportTests` |

### Animations

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| ViewExtensions (FadeTo, TranslateTo, ScaleTo, RotateTo) | Covered | 13 | `MauiViewAnimationTests`, `SkiaViewAnimationExtensionsTests` |
| Animation class and Easing | Covered | 26 | `EasingTests`, `LinuxAnimationManagerTests`, `MauiViewAnimationTests` |
| Ticker | Covered | 9 | `LinuxTickerTests` |

### VisualStateManager

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| Visual states (CommonStates) | Covered | 7 | `VisualStateBridgeTests` |
| Triggers | Covered | 6 | `TriggerPropagationTests` |
| Behaviors | Covered | 3 | `BehaviorAttachTests` |

### ControlTemplate

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| ControlTemplate on ContentView / ContentPage | Covered | 4 | `ControlTemplateHandlerTests` |
| ContentPresenter and TemplateBinding | Covered | 3 | `ControlTemplateHandlerTests` |

### Base View Properties

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| Opacity and IsVisible | Covered | 32 | `SkiaViewEdgeCaseTests`, `SkiaViewTests`, `ViewPropertyRenderingTests` |
| Transforms (rotation, scale, translation, anchor) | Covered | 7 | `ViewPropertyRenderingTests` |
| Shadow | Covered | 3 | `ViewPropertyRenderingTests` |
| Clip | Covered | 4 | `ViewPropertyRenderingTests` |
| AutomationId and semantics | Covered | 16 | `SemanticMapperTests`, `ViewPropertyRenderingTests` |

### FormattedText

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| Spans (colour, size, attributes, family) | Covered | 23 | `FormattedTextTests` |
| Span decorations | Covered | 2 | `FormattedTextTests` |
| Span gestures | Covered | 4 | `FormattedTextTests` |

### MenuBar

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| MenuBar and items | Covered | 35 | `MenuItemClickedEventArgsTests`, `SkiaMenuBarTests`, `SkiaMenuFlyoutTests` |
| Context menus (MenuFlyout) | Covered | 10 | `ContextFlyoutTests` |

### Essentials

| Item | Status | Tests | Backing tests |
|------|--------|-------|---------------|
| AppInfo | Covered | 2 | `EssentialsTests` |
| AppActions | Covered | 3 | `EssentialsTests` |
| Battery | Covered | 7 | `EssentialsTests` |
| Browser | Covered | 5 | `EssentialsTests` |
| Clipboard | Covered | 4 | `EssentialsTests`, `PrimarySelectionServiceTests` |
| Connectivity | Covered | 1 | `EssentialsTests` |
| DeviceDisplay | Covered | 18 | `EssentialsTests`, `HiDpiServiceTests` |
| DeviceInfo | Covered | 1 | `EssentialsTests` |
| Email | Covered | 4 | `EssentialsTests` |
| FilePicker | Covered | 6 | `EssentialsTests` |
| FileSystem | Covered | 8 | `EssentialsTests` |
| Launcher | Covered | 4 | `EssentialsTests` |
| Map | Covered | 13 | `EssentialsTests`, `MapTypeRoutingTests` |
| MediaPicker | Covered | 2 | `EssentialsTests` |
| Preferences | Covered | 4 | `EssentialsTests` |
| Screenshot | Covered | 3 | `EssentialsTests` |
| SecureStorage | Covered | 3 | `EssentialsTests` |
| SemanticScreenReader | Covered | 5 | `EssentialsTests` |
| Share | Covered | 5 | `EssentialsTests` |
| TextToSpeech | Covered | 3 | `EssentialsTests` |
| VersionTracking | Covered | 2 | `EssentialsTests` |
| Geolocation | Covered | 3 | `EssentialsTests` |
| WebAuthenticator | Covered | 9 | `EssentialsTests` |
| Accelerometer | Covered | 1 | `EssentialsTests` |
| Barometer | Covered | 1 | `EssentialsTests` |
| Compass | Covered | 2 | `EssentialsTests` |
| Contacts | Covered | 1 | `EssentialsTests` |
| Flashlight | Covered | 2 | `EssentialsTests` |
| Geocoding | Covered | 1 | `EssentialsTests` |
| Gyroscope | Covered | 1 | `EssentialsTests` |
| HapticFeedback | Covered | 3 | `EssentialsTests` |
| Magnetometer | Covered | 1 | `EssentialsTests` |
| OrientationSensor | Covered | 1 | `EssentialsTests` |
| PhoneDialer | Covered | 7 | `EssentialsTests` |
| SMS | Covered | 3 | `EssentialsTests` |
| Vibration | Covered | 2 | `EssentialsTests` |

## Tests not mapped to any category

259 tests are not attributed to a category above (infrastructure, diagnostics, platform-only features such as tray, printing, IME, drag payloads). They still run in the suite.

`ButtonPropertyMappingTests`, `CheckBoxPropertyMappingTests`, `DialogBridgeTests`, `DirtyRegionPerformanceTests`, `DragDataTests`, `DragDropServiceTests`, `DrawPerformanceTests`, `DropEventArgsCoordinateTests`, `EntryPropertyMappingTests`, `EssentialsPatchesTests`, `EssentialsTests`, `GestureRecognizerTests`, `GlobalHotkeyServiceTests`, `GridIntegrationTests`, `HighContrastChangedEventArgsTests`, `HighContrastColorsTests`, `HighContrastServiceTests`, `HitTestPerformanceTests`, `HotkeyEventArgsTests`, `HotkeyKeyTests`, `HotkeyModifiersTests`, `IndicatorShapeTests`, `LinuxDragEventArgsTests`, `LinuxDropEventArgsTests`, `MauiViewAnimationTests`, `MeasureArrangePerformanceTests`, `MeasureArrangePipelineTests`, `MercatorProjectionTests`, `MultiWindowTests`, `PositionChangedEventArgsTests`, `PrintServiceTests`, `ResourceCachePerformanceTests`, `ScaleChangedEventArgsTests`, `ShapeTests`, `SkiaEntryEdgeCaseTests`, `SkiaEntryTextTheoryTests`, `SkiaLabelEdgeCaseTests`, `SkiaLabelTheoryTests`, `SkiaMapTests`, `SkiaSliderEdgeCaseTests`, `SkiaSliderTheoryTests`, `SkiaStackLayoutEdgeCaseTests`, `SkiaViewVisibilityTheoryTests`, `SliderPropertyMappingTests`, `StackLayoutIntegrationTests`, `TileSourceTests`, `TrayIconServiceTests`, `VisualTreeInspectorTests`, `WaylandDragDropServiceTests`, `WaylandTextInputV3ServiceTests`

