# MAUI handler conformance (dotnet/maui's own tests against the Linux handlers)

Run of 2026-10-04 against dotnet/maui tag `10.0.110`. **1475 MAUI tests ported: 1377 passed, 0 failed, 98 skipped** (of the skips, 27 are MAUI's own and 71 are wrapper-view tests that do not apply; see below). The first run failed 574; the gaps it found, and their fixes, are listed under "Gaps found and fixed".

The suite compiles dotnet/maui's shared handler device tests (`src/Core/tests/DeviceTests/Handlers/*/<X>HandlerTests.cs` and the shared bases in `src/Core/tests/DeviceTests.Shared`) unchanged, against OpenMaui's handlers, so the assertions are MAUI's, not a reading of MAUI. Only the per-platform helper partials (MAUI's `*.Windows.cs` / `*.Android.cs`, "read the value back from the native view") are written for Linux, and they read the Skia platform view.

Nothing is suppressed: a test that fails stays failing in the project, and a failure is a candidate parity bug.

## How it is wired

| | |
|---|---|
| Project | `tests/Conformance/OpenMaui.Conformance.Tests.csproj` (net10.0, xunit 2.9), not part of `tests/OpenMaui.Controls.Linux.Tests.csproj` (that project now excludes `Conformance\**`) |
| MAUI sources | a shallow, sparse clone of dotnet/maui at tag 10.0.110, by default in `~/.cache/openmaui-build/maui-src`; `tests/Conformance/get-maui-sources.sh [dest] [tag]` creates it |
| Build property | `MauiSrc` (default `$(HOME)/.cache/openmaui-build/maui-src`), e.g. `-p:MauiSrc=/path/to/maui` |
| Without the clone | the project builds with a warning and contains one skipped test (`MauiSourcesMissing`) that says how to get the sources |
| Run | `dotnet test tests/Conformance/OpenMaui.Conformance.Tests.csproj` (filter by handler with `--filter Category=Label`, MAUI's category trait) |
| Report | `python3 tests/Conformance/conformance-report.py <results.trx> [--failures]` prints the per-class table below and every failure's first message line |
| License | MAUI's files are MIT and are compiled in place from the clone (`<Compile Include="$(MauiSrc)/..." Link=...>`), headers untouched; nothing of MAUI is copied into this repository |

The assembly is named `Microsoft.Maui.Core.DeviceTests`, the name of MAUI's own Core device-test app: the platform-neutral `Microsoft.Maui.dll` grants that name `InternalsVisibleTo`, which MAUI's test code relies on (`InvokeWithResult`, `GetDispatcher`, `FocusRequest`, ...).

### MAUI files compiled in place

- `src/TestUtils/src/DeviceTests/`: `AssertHelpers.cs`, `AssertionExtensions.cs`
- `src/Core/tests/DeviceTests.Shared/`: `GlobalNamespaces.cs`, `HandlerTests/{TestBase, HandlerTestBasement, HandlerTestBasementOfT, HandlerTestBase, HandlerTestBaseOfT, HandlerTestBaseOfT.Tests}.cs`, `HandlerTests/TextStyle/TextStyleHandlerTests.cs`, `HandlerTests/TextInput/TextInputHandlerTests.cs`, `HandlerTests/Focus/FocusHandlerTests.cs`, `Stubs/*.cs`, `ImageAnalysis/*.cs`
- `src/Core/tests/DeviceTests/`: `Handlers/CoreHandlerTestBase.cs`, `Handlers/CoreHandlerTestBaseOfT.cs`, `Stubs/*.cs` (cross-platform files; minus `CountedImageHandler.cs`, `WebView.cs`, `ButtonWithContainerStub.cs`, `StubBaseHandler.cs`, `WindowHandlerProxyStub.cs`, which need platform-only API), `Services/ImageSource/BaseImageSourceServiceTests.cs`, `Resources/Images/*`, `Resources/Raw/*.png` (pictures the image tests load by name)
- Handler tests: `ActivityIndicator`, `Border`, `Button`, `CheckBox`, `ContentView` (`ContentViewTests.cs`), `DatePicker`, `Editor`, `Entry`, `GraphicsView`, `Image`, `ImageButton`, `Label`, `Layout`, `Picker`, `ProgressBar`, `RadioButton`, `ScrollView`, `SearchBar`, `ShapeView`, `Slider`, `Stepper`, `Switch`, `TimePicker` (each `Handlers/<X>/<X>HandlerTests.cs`)

### Linux side (`tests/Conformance`)

- `Infrastructure/HandlerAliases.cs`: `LabelHandler` etc. declared in the tests' namespace as empty subclasses of the Linux handlers, so every handler name in MAUI's files binds to the Linux handler; they forward exactly the constructors the Linux handler has, and implement the MAUI handler interfaces the tests cast to (see "API surface" below).
- `Infrastructure/CoreDeviceTestExtensions.Linux.cs`: the test app is a Linux app (`UseLinux`), plus stub-to-handler registrations; MAUI's text-style, text-input and focus test bases get the same app (`SharedHandlerTestBases.Linux.cs`).
- `Infrastructure/TestRunner.Linux.cs`: `TestDispatcher`/`TestServices` (MAUI's device runner pieces); the "main thread" is the calling thread, as in the main OpenMaui suite.
- `Infrastructure/AssertionExtensions.Linux.cs`, `HandlerTestBasementOfT.Linux.cs`: MAUI's platform assertion helpers for Skia views. Pixels come from `SkiaView.Draw` into a raster surface; "attached" means the view is the root of a headless window context of a `LinuxApplication` (no display), laid out at its size.
- `Infrastructure/LinuxTestFramework.cs` + `KnownSkips.cs`: xunit's framework plus a skip list for tests of mechanisms Linux does not have. Everything else runs.
- `Handlers/*.Linux.cs`: the per-handler helpers (`GetNativeText`, `GetNativeIsChecked`, ...). Where the Skia view has no counterpart of the native property, the helper fails with "SkiaX exposes no Y" instead of inventing a value.

## Results per test class

| Test class | Ported | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| ActivityIndicatorHandlerTests | 42 | 38 | 0 | 4 |
| BorderHandlerTests | 43 | 39 | 0 | 4 |
| ButtonHandlerTests | 49 | 45 | 0 | 4 |
| ButtonHandlerTests+ButtonTextStyleTests | 22 | 22 | 0 | 0 |
| CheckBoxHandlerTests | 36 | 32 | 0 | 4 |
| ContentViewTests | 33 | 29 | 0 | 4 |
| DatePickerHandlerTests | 34 | 30 | 0 | 4 |
| DatePickerHandlerTests+DatePickerTextStyleTests | 22 | 22 | 0 | 0 |
| EditorHandlerTests | 125 | 121 | 0 | 4 |
| EditorHandlerTests+EditorFocusTests | 1 | 1 | 0 | 0 |
| EditorHandlerTests+EditorTextInputTests | 17 | 17 | 0 | 0 |
| EditorHandlerTests+EditorTextStyleTests | 22 | 22 | 0 | 0 |
| EntryHandlerTests | 148 | 144 | 0 | 4 |
| EntryHandlerTests+EntryFocusTests | 1 | 1 | 0 | 0 |
| EntryHandlerTests+EntryTextInputTests | 17 | 17 | 0 | 0 |
| EntryHandlerTests+EntryTextStyleTests | 22 | 22 | 0 | 0 |
| GraphicsViewHandlerTests | 44 | 40 | 0 | 4 |
| ImageButtonHandlerTests | 33 | 29 | 0 | 4 |
| ImageButtonHandlerTests+ImageButtonImageHandlerTests | 55 | 50 | 0 | 5 |
| ImageHandlerTests | 55 | 50 | 0 | 5 |
| LabelHandlerTests | 60 | 56 | 0 | 4 |
| LabelHandlerTests+LabelTextStyleTests | 22 | 22 | 0 | 0 |
| LayoutHandlerTests | 48 | 41 | 0 | 7 |
| PickerHandlerTests | 42 | 38 | 0 | 4 |
| PickerHandlerTests+PickerTextStyleTests | 22 | 22 | 0 | 0 |
| ProgressBarHandlerTests | 39 | 35 | 0 | 4 |
| RadioButtonHandlerTests | 36 | 32 | 0 | 4 |
| RadioButtonHandlerTests+RadioButtonTextStyleTests | 22 | 22 | 0 | 0 |
| ScrollViewHandlerTests | 31 | 27 | 0 | 4 |
| SearchBarHandlerTests | 118 | 114 | 0 | 4 |
| SearchBarHandlerTests+SearchBarTextInputTests | 18 | 18 | 0 | 0 |
| SearchBarHandlerTests+SearchBarTextStyleTests | 22 | 22 | 0 | 0 |
| ShapeViewHandlerTests | 47 | 42 | 0 | 5 |
| SliderHandlerTests | 47 | 43 | 0 | 4 |
| StepperHandlerTests | 35 | 31 | 0 | 4 |
| SwitchHandlerTests | 37 | 32 | 0 | 5 |
| TimePickerHandlerTests | 33 | 29 | 0 | 4 |
| TimePickerHandlerTests+TimePickerTextStyleTests | 22 | 22 | 0 | 0 |
| **Total** | **1522** | **1419** | **0** | **103** |

Every `<X>HandlerTests` row includes the generic tests (about 30) MAUI runs for every handler (`HandlerTestBaseOfT.Tests.cs`: automation id, flow direction, opacity, visibility, semantics, bounds, transforms, constructors, container view).

## Gaps found and fixed (10.0.110.7)

The first run (795 passed, 574 failed) grouped its failures by root cause; all are fixed:

- **G1, base view properties (330 failures, every handler)**: Opacity, Visibility (with Hidden kept distinct from Collapsed), FlowDirection, AutomationId, semantics, InputTransparent and ZIndex were copied from a Controls view when OpenMaui created the handler, never mapped; they are now mapped in `ViewHandler.ViewMapper` (`Handlers/LinuxViewMappers.cs`). Layouts handle `UpdateZIndex`; an ActivityIndicator spins only while visible.
- **G2, ButtonHandler (20)**: the plain handler mapped no text, colour or font; it now maps them for any `ITextButton`.
- **G3, RadioButton fonts (16)**: font family, weight and slant, character spacing, content and border now reach `SkiaRadioButton`.
- **G4, italic (32)**: Picker, DatePicker, TimePicker and SearchBar map the font slant.
- **G5, Keyboard (102)**: Entry, Editor and SearchBar map it, and it becomes the input method's content type (text-input-v3, IBus).
- **G6, MaxLength (18)**: text set from code is cut, 0 means no text, -1 is unlimited; SearchBar maps it.
- **G7, null and empty text (12)**: the handlers no longer write "" over an app's null text.
- **G8, SearchBar text-input properties (22)**: cursor, selection, read-only, prediction, spell check, alignment, return type, icon colour.
- **G9, focus (2)**: platform focus is reported to `IView.IsFocused`.
- **G10 to G15, images, layout, ScrollView (23)**: image loading notifications, clearing a null or failed source, `IsAnimationPlaying`, layout disconnect, the ScrollViewHandler constructor.

- **G16, image-source services (8, previously BLOCKED)**: Image and ImageButton `ImageLoadSequenceIsCorrect`, `InterruptingLoadCancelsAndStartsOver` and their `WithChecks` variants waited for MAUI's `IImageSourceService` to start a load, which the Linux handlers never resolved (the platform-neutral `IImageSourceService` has no load method). Linux now has one, `ILinuxImageSourceService.GetImageAsync`, with built-in File, Uri, Stream and Font services registered through `ConfigureImageSources` by `UseLinux`; the handlers resolve the service for the source's type, cancel a replaced load and never show it. The suite's `CountedImageSourceServiceStub` gets its Linux partial in `Handlers/ImageHandlerTests.Linux.cs`, as it has `.Android.cs` / `.iOS.cs` ones in MAUI.

Two diagnoses were the test port's, not the product's, and were corrected in the Linux helpers: MAUI's `Semantics.Description` is OpenMaui's accessible name (`SemanticName`), as it is `AutomationProperties.Name` on Windows; and the image helpers now observe a picture being cleared.

## Skipped

| Reason | Tests |
|---|---|
| MAUI's own skips (`View Renders To Image` on non-Android, `Shadow Initializes Correctly` on Layout, `ThumbColor Initializes Correctly` on Switch, `InvalidSourceFailsToLoad` on images) | 27 |
| **No wrapper view** (`Clip Initializes ContainerView Correctly`, `ContainerView Remains If Shadow Mapper Runs Again`, `ContainerView Adds And Removes` for every handler; Layout `ContainerViewAddedToLayout`, `ContainerViewDifferentThanPlatformView`): Skia views apply Clip and Shadow while drawing, there is no native wrapper to add, and MAUI's platform-neutral ViewHandler has no container implementation | 71 |

## API surface findings (needed scaffolding to compile MAUI's tests)

These are not test failures, but each is a place where OpenMaui's public API differs from MAUI's and code written against MAUI's handler API does not compile or behaves differently:

- **`Microsoft.Maui.Platform.LayoutAlignment`**: OpenMaui declares a public enum with this name (different ordinals) in `Microsoft.Maui.Platform`, which MAUI code imports routinely. Any file that also imports `Microsoft.Maui.Primitives` gets `CS0104` (MAUI's `StubBase.cs` does). The suite works around it with a global alias.
- **No `I<Control>Handler` interfaces**: the Linux handlers do not implement `IEntryHandler`, `IEditorHandler`, `ISearchBarHandler`, `IImageHandler`, `ILayoutHandler`, ... MAUI's `PropertyMapper<IEntry, IEntryHandler>` customizations, `IImageHandler.SourceLoader` and `ILayoutHandler.Add/Insert/Remove/Clear/Update/UpdateZIndex` are unavailable. The aliases add them as adapters (the layout methods send the commands `Controls.Layout` sends; `SourceLoader` throws).
- **Static `Map*` methods take the concrete Linux handler** (`MapIsPassword(EntryHandler, IEntry)`), not MAUI's interface (`(IEntryHandler, IEntry)`), and some MAUI ones do not exist (`EntryHandler.MapKeyboard`), so `[nameof(IEntry.Keyboard)] = EntryHandler.MapKeyboard` does not compile.
- **`IPlatformViewHandler` does not exist** on the platform-neutral TFM OpenMaui builds on; the suite declares a marker interface.

## Not ported, and why

- **ShapeView / BoxView**: now ported. MAUI's `ShapeViewHandlerTests` drive `ShapeViewHandler` with an `IShapeView` stub; OpenMaui had no `IShapeView` handler (its shape handlers were typed to the Controls classes). It now has one (`Handlers/ShapeViewHandler.cs`, platform view `SkiaShapeView` drawing MAUI's `ShapeDrawable`), aliased in `Infrastructure/HandlerAliases.cs`, with `Handlers/ShapeViewHandlerTests.Linux.cs` as its platform partial: 42 passed, 0 failed, 5 skipped (the generic container-view, shadow and render-to-image skips every handler has, and MAUI's own skip of the shadow test).
- **RefreshView, IndicatorView, SwipeView** (MAUI Core tests exist): same reason, the Linux handlers are typed to `Controls.RefreshView` / `IndicatorView` / `SwipeView`, not `IRefreshView` / `IIndicatorView` / `ISwipeView`.
- **View, Element, Window, Page, Navigation, WebView core tests**: `ViewHandlerTests` / `ElementTests` test MAUI's own `ViewHandler`/`ElementHandler` stubs, not a Linux handler; window, page and navigation tests need MAUI's platform window plumbing; WebView needs the out-of-process WebKit host.
- **`src/Controls/tests/DeviceTests`** (Controls-level tests: real `Label`, `Entry`, ... with Controls handlers): now ported, see "Controls device tests" below. Original note: the next step. They need a Linux port of `ControlsHandlerTestBase`'s window hosting (`CreateHandlerAndAddToWindow`, about 200 lines per platform partial), which can be built on the headless `WindowContext` the core suite already uses. Because OpenMaui's mapping is Controls-centric (G1, G2), that suite would show which of the gaps above an app actually hits.
- Platform-only tests in MAUI's `*.Android.cs` / `*.iOS.cs` / `*.Windows.cs` partials (transforms, input transparency, native font objects, ...) are not ported: they assert native API and live in platform files.

# Controls device tests (dotnet/maui's Controls tests against OpenMaui)

Run of 2026-10-04 against dotnet/maui tag `10.0.110`. **598 MAUI tests ported. Before the fixes of this round: 337 passed, 257 failed, 4 skipped. After: 519 passed, 75 failed, 4 skipped** (all 4 skips are mechanisms Linux does not have; nothing is BLOCKED).

These are MAUI's `src/Controls/tests/DeviceTests`: real Controls views (`Label`, `Entry`, `CollectionView`, `Shell`, `NavigationPage`, `TabbedPage`, `FlyoutPage`, modal pages, ...) put in a window by MAUI's own `ControlsHandlerTestBase.CreateHandlerAndAddToWindow`, so they exercise the Controls layer the way an app does: page hosting, navigation, Loaded/Unloaded, layout through pages, Controls-only properties (TextTransform, LineBreakMode, MaxLines, AutoSize, ...).

## How it is wired

| | |
|---|---|
| Project | `tests/Conformance/Controls/OpenMaui.Conformance.Controls.Tests.csproj` (net10.0, xunit 2.9). A second project rather than more files in the Core one: MAUI's `Microsoft.Maui.Controls.dll` grants `InternalsVisibleTo` only to the assembly name `Microsoft.Maui.Controls.DeviceTests` (the tests use those internals), and the two suites host views differently (below). The Core project excludes `Controls/**`. |
| MAUI sources | the same sparse clone the Core suite uses (`tests/Conformance/get-maui-sources.sh`, property `MauiSrc`); without it the project builds with a warning and one skipped test |
| Run | `dotnet test tests/Conformance/Controls/OpenMaui.Conformance.Controls.Tests.csproj` (about 2 minutes); `--filter Category=Shell` etc. for one area |
| Report | `python3 tests/Conformance/conformance-report.py <results.trx> [--failures]` |
| License | MAUI's files are compiled in place from the clone (`<Compile Include="$(MauiSrc)/..." Link=...>`), untouched; nothing of MAUI is copied into this repository |

**Main thread.** The suite runs a real OpenMaui main thread (`Infrastructure/TestRunner.Controls.Linux.cs`): a thread that owns GLib's default main context, with `LinuxDispatcher.Initialize()` (the dispatcher and `SynchronizationContext` an app runs on) and `g_main_context_iteration` as its loop. `TestDispatcher` is `LinuxDispatcherProvider`, so dispatches, dispatcher timers and await continuations go through GLib as in an app; tests reach the thread through MAUI's `InvokeOnMainThreadAsync`, as on MAUI's device runners.

**Window.** `Infrastructure/HeadlessWindowHost.cs` opens a test's window the way `LinuxApplication`'s startup opens an app's first window, minus the native toplevel: a fresh primary `WindowContext` adopts the MAUI window (attaching the Linux `WindowHandler` and its modal presentation), the page is rendered by `LinuxViewRenderer.RenderPage`, `IWindow.Created` is raised as the bootstrap raises it, the window reports its 800x600 size to MAUI (as a native window does when the context adopts it), and the services come from a window-scoped `LinuxMauiContext`. Every 16 ms the main thread renders a frame as `SkiaRenderingEngine.Render` does: `LinuxTicker.PumpAll` (animations), measure/arrange of the tree and its modal layers at 800x600 until no view asks for another pass, then a draw into a raster surface. Loaded therefore comes with the first frame, and layout changes are applied by frames, as in an app. Closing mirrors `ReapClosedContexts` (Destroying, context disposed, window handler disconnected). `ControlsHandlerTestBase.Linux.cs` (MAUI's `SetupWindowForTests` partial) opens and closes this host.

**AttachAndRun.** MAUI's Windows runner puts a lone platform view in a window, centred at its own size, and waits for Loaded. Here a Controls view in no window is put in one as an app shows a view: content of a `ContentPage` in a `Window`, inside a centred `VerticalStackLayout` (`AssertionExtensions.Attach.Controls.cs`); a view already in an open window runs as is.

**Handler names.** In this suite MAUI's handler names are global `using` aliases of the exact Linux handler types (`Infrastructure/HandlerAliases.Controls.cs`: `LabelHandler` is `Microsoft.Maui.Platform.Linux.Handlers.LabelHandler`, `PageHandler` is `ContentPageHandler`, `NavigationViewHandler` is `NavigationPageHandler`, `ButtonHandler` is `TextButtonHandler`, ...), and `IPlatformViewHandler` is `IViewHandler`. The Core suite's approach (subclasses under MAUI's names) cannot work for views in a page: OpenMaui realizes a page's views through its own Controls-type map, never a registered subclass. MAUI names with no Linux handler (`MenuBarItemHandler`, ...) are left as MAUI's platform-neutral handlers, so a test that needs one fails visibly.

**Per-element helpers.** `Elements/*.Linux.cs` are the counterparts of MAUI's `*.Windows.cs` partials (read the value back from the Skia view; where the Skia view has no counterpart the helper fails with "SkiaX exposes no Y"). The navigation-bar helpers (`IsBackButtonVisible`, `IsNavigationBarVisible`, `GetToolbarTitle`, `ToolbarItemsMatch`) read the bar the window shows: the bar is drawn by the page that owns it (`SkiaShell`, or the current `SkiaPage` in a `SkiaNavigationPage`). `GetPlatformToolbar` returns the window's `SkiaWindow.Toolbar` (the platform element of MAUI's toolbar) when the window shows a bar, `GetTitleView` the Shell bar's TitleView. Platform-only tests inside MAUI's `*.Windows.cs` / `*.Android.cs` files are not ported.

**Shared infrastructure.** `tests/Conformance/Infrastructure` is linked by both projects where the meaning is the same (xunit framework and skip list mechanism, Skia pixel assertions, `LinuxInput`); the suite-specific parts are split out (`AssemblyName.Core.cs`, `HandlerAliases.Core.cs`, `AssertionExtensions.Attach.Core.cs` for the Core suite). `LinuxTestFramework` can now also skip single theory rows (`KnownSkips.ReasonForCase`). The Core suite's results are unchanged (1369 passed, 0 failed, 106 skipped).

**Build-time compile fixes.** Five MAUI files do not compile, or have no branch for this TFM, as they are; they are compiled from a copy under `obj/MauiPatched` with literal replacements listed in `tests/Conformance/Controls/MauiPatches.props` (every replacement must match or the build fails; none changes an assertion): `TextInputTests.cs` (a constraint naming `IPlatformViewHandler` next to `IViewHandler`, a duplicate here), `LabelTests.cs` (two platform `#if` chains with no fallback branch get a Linux branch reading `SkiaLabel`), `ShellFlyoutTests.cs` (`GetSafeArea` returns `Thickness.Zero` on Linux, as on Windows and Android: a desktop window has no safe area), `TestClasses/NestingView.cs` (its platform view is a `SkiaStackLayout`; its `#else` branch is UIKit), `DeviceTests.Shared/ImageAnalysis/RawBitmap.cs` (its capture throws `PlatformNotSupportedException("TODO")` off the platform TFMs; the Linux branch draws the Skia view into BGRA pixels, `Infrastructure/RawBitmap.Linux.cs`). A patch key may climb out of the Controls tests (`../../../Core/tests/DeviceTests.Shared/...`).

### MAUI files compiled in place

- `src/TestUtils/src/DeviceTests/`: `AssertHelpers.cs`, `AssertionExtensions.cs`; `src/Core/tests/DeviceTests.Shared/`: `GlobalNamespaces.cs`, `HandlerTests/{TestBase, HandlerTestBasement, HandlerTestBase}.cs`, `Stubs/*.cs`, `ImageAnalysis/*.cs`
- `src/Controls/tests/DeviceTests/`: `ControlsHandlerTestBase.cs`, `Extensions.cs`, `TestCategory.cs`, `TextTransformCases.cs`, `MapperTests.cs`, `DispatchingTests.cs`, `Stubs/{ApplicationStub, FrameStub}.cs`, `TestClasses/{LifeCycleTrackingPage, FlyoutPageAlwaysSplit, NestingView}.cs`, `TestCases/{ControlsPageTypesTestCases, ControlsViewTypesTestCases}.cs`, `Memory/MemoryTests.cs`
- `Elements/`: `FormattedStringTests`, `PlatformBehaviorTests`, `VisualElementTests`, `Accessibility/AccessibilityTests`, `Application/ApplicationTests`, `Border/BorderTests`, `BoxView/BoxViewTests`, `Button/ButtonTests`, `CarouselView/CarouselViewTests`, `CheckBox/CheckBoxTests`, `CollectionView/{CollectionViewTests, CollectionViewSizingTestCase}`, `ContentView/ContentViewTests`, `DatePicker/DatePickerTests`, `Editor/EditorTests`, `Entry/EntryTests`, `FlyoutPage/{FlyoutPageTests, FlyoutPageLayoutBehaviorTestCases}`, `Frame/{FrameTests, FrameHandlerTest}`, `Image/ImageTests`, `Label/LabelTests`, `Layout/LayoutTests`, `Modal/ModalTests`, `NavigationPage/NavigationPageTests`, `Page/PageTests`, `Path/PathTests`, `Picker/PickerTests`, `RadioButton/RadioButtonTests`, `RefreshView/RefreshViewTests`, `ScrollView/ScrollViewTests`, `SearchBar/SearchBarTests`, `Shape/ShapeTests`, `Shell/{ShellTests, ShellFlyoutTests, ShellTabBarTests, ShellFlyoutItemTextColorTests and the Shell*TestCases}`, `Slider/SliderTests`, `SwipeView/SwipeViewTests`, `TabbedPage/TabbedPageTests`, `TemplatedView/TemplatedViewTests`, `TextInput/TextInputTests`, `Toolbar/ToolbarTests`, `View/ViewTests`, `VisualElementTree/{VisualElementTreeTests, FindVisualTreeElementInsideTestCase}`, `Window/{WindowTests, WindowOverlayTests, WindowPageSwapTestCases, ChangingToNewMauiContextDoesntCrashTestCases}` (each `.cs` in that folder)

Several of these (`RadioButtonTests`, `SliderTests`, `PathTests`, `ApplicationTests`) compile but contain only platform-conditional tests, so they contribute no Linux tests.

Not compiled: `HybridWebView/*`, `WebView/*` (OpenMaui's web views run in an out-of-process WebKit host a headless test process cannot start), `Map/*` (Maps is a separate package), `TitleBar`, `ContextFlyout`, `MenuFlyoutItem`, `AlertDialog`, `Compatibility/VisualElementRendererTests` (platform renderers), `Xaml/*` (needs XAML compilation of MAUI's `RadioButtonUsing.xaml`).

## Results per test class

"Before" is the first complete run against the unfixed product (the harness was the same except for three translation fixes made later, noted under "Harness corrections").

| Test class | Ported | Passed (before) | Failed (before) | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|---:|
| AccessibilityTests | 4 | 3 | 1 | 4 | 0 | 0 |
| AccessibilityTests+InNewWindowCollection | 1 | 0 | 0 | 0 | 0 | 1 |
| BorderTests | 5 | 4 | 1 | 5 | 0 | 0 |
| BoxViewTests | 7 | 2 | 5 | 7 | 0 | 0 |
| ButtonTests | 14 | 9 | 5 | 14 | 0 | 0 |
| CarouselViewTests | 3 | 2 | 1 | 2 | 1 | 0 |
| CheckBoxTests | 5 | 2 | 3 | 5 | 0 | 0 |
| CollectionViewTests | 25 | 11 | 14 | 11 | 14 | 0 |
| ContentViewTests | 2 | 2 | 0 | 2 | 0 | 0 |
| DatePickerTests | 2 | 1 | 1 | 2 | 0 | 0 |
| DispatchingTests | 1 | 1 | 0 | 1 | 0 | 0 |
| EditorTests | 12 | 11 | 1 | 12 | 0 | 0 |
| EditorTests+EditorTextInputTests | 40 | 35 | 5 | 40 | 0 | 0 |
| EntryTests | 10 | 10 | 0 | 10 | 0 | 0 |
| EntryTests+EntryTextInputTests | 40 | 35 | 5 | 40 | 0 | 0 |
| FlyoutPageTests | 12 | 3 | 9 | 12 | 0 | 0 |
| FormattedStringTests | 2 | 1 | 0 | 1 | 0 | 1 |
| FrameTests | 19 | 13 | 6 | 18 | 1 | 0 |
| ImageTests | 2 | 2 | 0 | 2 | 0 | 0 |
| LabelTests | 50 | 26 | 24 | 50 | 0 | 0 |
| LayoutTests | 35 | 29 | 6 | 35 | 0 | 0 |
| MapperTests | 2 | 2 | 0 | 2 | 0 | 0 |
| MemoryTests | 59 | 43 | 15 | 49 | 9 | 1 |
| ModalTests | 40 | 1 | 39 | 40 | 0 | 0 |
| NavigationPageTests | 16 | 0 | 16 | 16 | 0 | 0 |
| PageTests | 13 | 13 | 0 | 13 | 0 | 0 |
| PickerTests | 4 | 2 | 2 | 4 | 0 | 0 |
| PlatformBehaviorTests | 1 | 0 | 1 | 1 | 0 | 0 |
| RefreshViewTests | 8 | 8 | 0 | 8 | 0 | 0 |
| ScrollViewTests | 11 | 7 | 4 | 10 | 1 | 0 |
| SearchBarTests | 8 | 8 | 0 | 8 | 0 | 0 |
| ShapeTests | 3 | 0 | 3 | 3 | 0 | 0 |
| ShellTests | 49 | 3 | 46 | 49 | 0 | 0 |
| SwipeViewTests | 2 | 0 | 2 | 0 | 2 | 0 |
| TabbedPageTests | 12 | 3 | 9 | 12 | 0 | 0 |
| TemplatedViewTests | 2 | 2 | 0 | 2 | 0 | 0 |
| ToolbarTests | 12 | 0 | 12 | 12 | 0 | 0 |
| ViewTests | 37 | 35 | 1 | 36 | 0 | 1 |
| VisualElementTests | 2 | 2 | 0 | 2 | 0 | 0 |
| VisualElementTests+NewWindowCollection | 4 | 0 | 4 | 4 | 0 | 0 |
| VisualElementTreeTests | 9 | 1 | 8 | 9 | 0 | 0 |
| WindowOverlayTests | 1 | 1 | 0 | 1 | 0 | 0 |
| WindowTests | 12 | 4 | 8 | 12 | 0 | 0 |
| **Total** | **598** | **337** | **257** | **542** | **52** | **4** |

## Product gaps fixed in this round

Ordered by how many tests they unblocked. Each is MAUI's behaviour on its platforms (read from MAUI's source and tests); the device tests cover them end to end, and the navigation and measure fixes also have focused tests in the main suite (`Views/SkiaNavigationPageTransitionTests.cs`, `Handlers/NavigationPageParityTests.cs`, `Handlers/DesiredSizeConstraintTests.cs`).

- **C1, pages inside a NavigationPage had no frame (about 70 tests: every NavigationPage, Modal and Toolbar test, most Window, Flyout and Memory page tests)**. `SkiaNavigationPage` drew its pages at its bounds but never arranged them, so the MAUI page's `Frame` stayed -1 (`Width`/`Height` -1, no `SizeChanged`/`OnSizeAllocated`) and MAUI's window setup waited for a layout that never came. It now arranges its current (and incoming) page; NavigationPage, TabbedPage and FlyoutPage views also give their own MAUI page its frame (`SkiaView.HostedPage`).
- **C2, NavigationPage navigation (16 NavigationPage tests, Modal, Window, Toolbar)**. `PushAsync`/`PopAsync` completed before the platform transition finished, and a push or pop arriving during a transition was dropped (an app that pushed again as soon as `PushAsync` returned lost the page). Only pushes and pops by count were understood: pages inserted or removed beneath the current one, and stack swaps, were ignored. `NavigationPage.HasNavigationBar` and `HasBackButton` were not applied, the back arrow popped the platform stack without MAUI's (the two stacks disagreed), pages that were not `ContentPage` (a TabbedPage pushed onto the stack) were not shown, and popped pages stayed referenced by the handler. Now: `SkiaNavigationPage.SetNavigationStack` shows exactly MAUI's stack, a running transition is finished instead of dropping the next navigation, `NavigationFinished` is reported after the transition (`TransitionCompleted`), the bar and back arrow follow the attached properties, the back arrow and Escape pop MAUI's NavigationPage (`BackRequested`), other pages are hosted in a bar-carrying `SkiaPage`, and pages leaving the stack are released.
- **C3, the Shell an app shows had no ShellHandler (28 Shell tests)**. `LinuxViewRenderer.RenderShell` gave the Shell an internal placeholder handler, so `shell.Handler is ShellHandler` was false (MAUI code and libraries look for it). The rendered Shell now gets a `ShellHandler` that adopts its `SkiaShell` and maps nothing the renderer drives. A `ShellContent` whose page has no `Content` (yet) was not rendered at all (no handler, no Loaded/Appearing); it now shows as an empty page, and a non-`ContentPage` page in a ShellContent renders through its handler. `Shell.BackButtonBehavior` (`IsVisible`, `Command`) is honoured.
- **C4, `WindowHandler` did not implement MAUI's `IWindowHandler`** (every Shell, Modal and Window test that asks MAUI's base class for the window's handler). Now it does.
- **C5, Controls-level mappings that only reach MAUI's own handlers (Label 21, Button 5, Editor 1)**. Controls remaps `TextTransform`, `TextType`, `LineBreakMode`, `MaxLines`, `AutoSize` on MAUI's handler mappers (`Label.Mapper.cs`, `Button.Mapper.cs`, ...), which OpenMaui's handlers do not chain to. Label now maps `TextTransform` and `TextType`, a span with the default transform takes the label's (MAUI's FormattedString conversion), and the platform line limit follows MAUI's rule (NoWrap, HeadTruncation and MiddleTruncation are one line; an unset MaxLines is one line for TailTruncation and unlimited otherwise). Button maps `LineBreakMode` (stored; SkiaButton still draws one line). Editor maps `AutoSize` (TextChanges grows with the text).
- **C6, modal pages (4; the other Modal failures were C1 and C4)**. Modal pages pushed before the window was shown (in a page constructor, or before the app's window opened) were never presented; the window now presents its pending modal stack when it adopts the MAUI window.
- **C7, caret and selection of Entry and Editor (10)**. A CursorPosition or SelectionLength beyond the text was not capped (and the capped value not reported to the view), and text replaced after the view was shown did not move the caret to its end. Fixed in the handlers, following `TextInputExtensions` (initial values are kept).
- **C8, Loaded/Unloaded of a closed window (5: VisualElement, PlatformBehavior)**. Closing a window never unloaded its views, and MAUI's platform-neutral wiring loaded them again on the next window handler change. A disposed window context now sends Unloaded to its tree (`LoadedEventPatches.SendUnloadedIfLoaded`) and keeps a closed window's views unloaded until it is shown again. PlatformBehaviors detach on close as a result.
- **C9, a view's desired size exceeded its constraint (Label truncation and others, 12)**. `LinuxViewMeasure` returned the Skia view's natural size even when MAUI offered less (a no-wrap label in a 100-wide stack was 3800 wide); like Android's AT_MOST measure and WinUI's DesiredSize it is now capped to the constraint unless the view has an explicit size.
- **C10, smaller fixes**: CheckBox `Background` painted the box instead of the view's background (3); Picker cleared the view's `SelectedIndex` while refilling its items when it got a handler (2); `DatePicker.Date = null` showed a date (MAUI 10: no date) and was written back as today (`SkiaDatePicker.HasDate`, `Text`) (1); Frame drew no border for `BorderColor` and its size left the border out (5); FlyoutPage ignored right-to-left (2); ScrollView content smaller than the viewport ignored its alignment (`TestContentHorizontalOptionsChanged` still fails, see below).
- **C11, Shell tab bar (7: the TabBar colour tests; was R2)**. MAUI shows the current ShellItem's sections (the Tabs of a TabBar or FlyoutItem, and the implicit section around each bare ShellContent) as bottom tabs; `SkiaShell` drew a bottom bar only when its own `TabBarIsVisible` was set, which nothing set, so a Shell app had no tab bar at all. With a MAUI Shell attached the bar now shows those sections when MAUI's `IShellItemController.ShowTabs` says so (more than one section, `Shell.TabBarIsVisible` not false for the page), lays the page out above it, draws each section's icon (loaded through its image-source service, tinted) and title in the colours of the Shell's appearance for the presented page (MAUI's `IAppearanceObserver`: `TabBarForegroundColor` for the selected icon, `TabBarTitleColor` for the selected title, each falling back to the other, `TabBarUnselectedColor`, `TabBarDisabledColor`, `TabBarBackgroundColor`), and a click selects the section through MAUI (`ProposeSection`). The suite's `white_tab.png` is MAUI's own `MauiImage` link of `white.png`, now copied the same way.
- **C12, Shell.TitleView (3; was R4)**. The navigation bar now shows the TitleView in effect for the presented page (the page's, else the nearest set on its ShellContent, section, item or the Shell, as MAUI's ShellToolbar resolves it) in the title's place, the bar's full height between the navigation icon and the toolbar items, takes its input, and follows page changes and the property (`SkiaShell.TitleView`, `ViewRenderer`).
- **C13, Window.Toolbar had no handler (3 MainPageSwap; was R6)**. MAUI's toolbar element (the NavigationPageToolbar a NavigationPage puts on its window or FlyoutPage, the Shell's ShellToolbar) now gets a Linux `ToolbarHandler` whose platform element (`SkiaToolbar`: title, visibility, back button) the window keeps as `SkiaWindow.Toolbar`, re-resolved when the window's page or any of these toolbars changes (`WindowHandler`, `FlyoutPageHandler` and `ShellHandler` map `Toolbar`), as MAUI's navigation root keeps the platform toolbar. The bar on screen is still drawn by the page that owns it.
- **C14, Shell flyout geometry (4 FlyoutHeaderMinimumHeight; was R7)**. `SkiaShell.FlyoutBounds` is the flyout panel's place (the header, content and footer views are arranged inside it), and `Shell.FlyoutHeaderBehavior` reaches the platform: a CollapseOnScroll header is at least 56 tall, as MAUI keeps it on every platform.
- **C15, Shell lifecycle and layout (5; was R8)**. *Appearing twice* and *the Shell modal*: SkiaShell sent Appearing to the page its mirror presented, on top of MAUI's ShellSection, which sends it itself; the mirror lags MAUI (it follows Navigated), so a page MAUI had already shown and moved past (a push from its NavigatedTo) or that a modal covered got a second Appearing. It now sends Appearing only to the page MAUI's Shell presents with no modal over the window. *Logical children*: a flyout row template is realized for the element MAUI lists (for a section or content added straight to `Shell.Items`, that section or content, not the implicit ShellItem around it) and added as that element's logical child, as MAUI's flyout item view does. *Page leak*: the toolbar hit areas of the last frame kept the popped page's ToolbarItems (whose Parent is the page) until the bar was drawn again; they are dropped with the items. *Window bounds*: the startup window never told MAUI its size (only secondary windows did), so `Window.Width`/`Height` stayed NaN for an app's whole life; every window now reports its size when it adopts the MAUI window and on every resize (`WindowContext.ReportFrame`; MAUI applies a platform frame without echoing it to the handler). The headless test window reports the size its frames are laid out at.
- **C16, TabbedPage (2; was in R10)**. `SkiaTabbedPage` draws each tab's icon (`Page.IconImageSource` through its image-source service, tinted with the selected or unselected colour) above its title, and a tab follows its page's Title and IconImageSource. `TabbedPage.BarBackground` (a brush, gradients included) paints the bar; a gradient is followed while the page is shown and released when it disappears or its handler disconnects, as MAUI's TabbedPageManager does, so a shared brush keeps no subscriber after a modal TabbedPage is popped.

Public API added (additions only): `SkiaNavigationPage.IsTransitioning`, `TransitionCompleted`, `IsBackButtonVisible`, `BackRequested`, `SetNavigationStack`; `SkiaPage.HasBackButton`; `SkiaShell.IsBackButtonVisible`; `SkiaDatePicker.HasDate`, `Text`; `LabelHandler.MapTextTransform`, `MapTextType`; `TextButtonHandler.MapLineBreakMode`; `EditorHandler.MapAutoSize`; `WindowHandler` implements `IWindowHandler`; `EntryHandler`/`EditorHandler.SetVirtualView` overrides. Shell, TabbedPage and Window round: `ToolbarHandler` and `SkiaToolbar`; `SkiaWindow.Toolbar`; `WindowHandler.MapToolbar`, `ShellHandler.MapToolbar`, `FlyoutPageHandler.MapToolbar`, `TabbedPageHandler.MapBarBackground`; `SkiaShell.TabBarSections`, `TabBarBounds`, `TitleView`, `ViewRenderer`, `FlyoutBounds`, `FlyoutHeaderBehavior`; `SkiaTabbedPage.TabBarBackground` and its explicit parameterless constructor; `TabItem.IconSource`. `MapperParityTests` no longer lists `Button.LineBreakMode` and `Label.TextTransform`/`TextType` as gaps.

Public API added by the shape, visual-tree, Label and accessibility fixes (R3, R5, R9, R10; additions only, package validation against 10.0.110.6 passes): `ShapeViewHandler` (with `Mapper`, `CommandMapper` and MAUI's `Map*` methods) and `RoundRectangleHandler` (`Mapper`, `MapCornerRadius`); `SkiaShapeView` (`Drawable`), now the base class of `SkiaRectangle`, `SkiaEllipse`, `SkiaLine`, `SkiaPolygon`, `SkiaPolyline`, `SkiaShapePath` and `SkiaBoxView`; `IShapeViewHandler` on `RectangleHandler`, `EllipseHandler`, `LineHandler`, `PolygonHandler`, `PolylineHandler`, `ShapePathHandler` and `BoxViewHandler`; `PolylineHandler.MapFillRule`; `ShapePathHandler.MapShape`, `MapRenderTransform`; `BorderHandler.MapShape`, `MapStrokeDashPattern`; `SkiaView.IsExcludedWithChildren`.

## Failures left, by root cause

Most impactful first. Counts are of the 75 failures after the first round; R2, R4, R6, R7, R8 and the TabbedPage part of R10 were fixed in the Shell, TabbedPage and Window round (C11 to C16).

- **R1, CollectionView (23: 14 CollectionView, 9 Memory: the 7 gesture rows and 2 CollectionView leaks)**. Header/Footer (structural items) are not views of the list; a horizontal CollectionView does not size to its content (500 instead of 50); `ScrollTo` group/item does not reach the item; clearing `ItemsSource` keeps items' BindingContext; item views have no container whose bounds include the item's margin; the handler, and with it every item view (the `Gesture Does Not Leak` rows put a Label with a gesture recognizer in a CollectionView), is not collected after its page is popped. Left: `SkiaItemsView`, `SkiaCollectionView` and `CollectionViewHandler` are being reworked in parallel (Header/Footer); recorded for that work.
- **R2, Shell tab bar**: fixed (C11). Left, not covered by a device test: a section with several ShellContents shows no top tabs; TabBar sections are still listed in the flyout (MAUI lists none), and a Shell whose root is a TabBar keeps its flyout (MAUI's effective FlyoutBehavior is Disabled then).
- **R3, VisualElementTree hit testing and platform-view lookup (8): fixed.** MAUI's `VisualTreeElementExtensions.GetVisualTreeElement(platformView)` and `GetVisualTreeElements(point)` are platform code: the platform-neutral build tests a point against each view's `Frame` (relative to its parent, so nothing below the top level was found) and has no platform parents to walk (`GetParent` returns null). `Handlers/VisualTreeElementPatches.cs` (Harmony, installed with the Linux services) runs MAUI's algorithms on the Skia tree: a view's bounds are its Skia view's window bounds (`ScreenBounds`), and the platform-view lookup walks `SkiaView.Parent` to the nearest Skia view that knows its element (`MauiView`, or a window's root view: its window) and back down the element tree along that path. A Skia view that knows its own element returns it directly, which also finds a CollectionView item (OpenMaui's item views are not logical children of the CollectionView, so MAUI's walk down from the list stops at the list; recorded for the CollectionView work). Main-suite tests: `Handlers/VisualTreeElementLookupTests.cs`.
- **R4, TitleView**: Shell.TitleView fixed (C12). Left: `NavigationPage.TitleView` is not drawn in a SkiaNavigationPage's bar (no device test exercises it; `GetTitleView` fails visibly for it).
- **R5, no IShapeView handler (10: BoxView 5, Shape 3, RoundRectangle 2): fixed.** MAUI handles BoxView and the shapes with one `ShapeViewHandler` over `IShapeView` whose platform view draws a `ShapeDrawable`. OpenMaui now has `ShapeViewHandler` (any `IShapeView`, including a library's core shape view, which MAUI's factory used to give MAUI's platform-neutral handler) and `RoundRectangleHandler`, with MAUI's mapper (Shape, Aspect, Fill, Stroke, StrokeThickness, StrokeDashPattern/Offset, StrokeLineCap/Join, StrokeMiterLimit, Background, and Controls' StrokeDashArray). Its platform view `SkiaShapeView` draws MAUI's `ShapeDrawable` on a Skia canvas (Microsoft.Maui.Graphics.Skia), so gradients and the line cap/join/miter limit now reach the pixels. The per-type Controls handlers (`RectangleHandler`, `EllipseHandler`, `LineHandler`, `PolygonHandler`, `PolylineHandler`, `ShapePathHandler`, `BoxViewHandler`) keep their types and Map methods but now chain `ShapeViewHandler.Mapper`, implement `IShapeViewHandler`, and their Skia views derive from `SkiaShapeView` and draw the same `ShapeDrawable` when a handler set one (a Skia shape view used on its own draws as before). Two pieces of MAUI's platform code are reproduced: Controls' `Shape.TransformPathForBounds` (the Aspect stretch and the half-stroke inset, compiled for platform TFMs only, so `PathForBounds` returns the raw path on net10.0) and the winding/render-transform handling of Path, Polygon and Polyline (`FillRule`, `RenderTransform`). Polygon and Polyline redraw when a point is added to their `Points` (MAUI's handlers subscribe to the collection). `MapperParityTests` no longer lists gaps for Border, BoxView, Ellipse, Line, Path, Polygon, Polyline and Rectangle (Border maps `Shape` and `StrokeDashPattern` now). Left as it was: the per-type Skia views keep their own measure (explicit size, else the available space or 40) instead of MAUI's "no intrinsic size" (`SkiaShapeView`, and so `ShapeViewHandler`/`RoundRectangleHandler`, measure 0 without an explicit size, as MAUI's ShapeViewHandler does), so layouts of existing apps do not move; a shape's FlowDirection mirroring (an internal mapping on MAUI's platforms) is not done. Main-suite tests: `Handlers/ShapeViewHandlerTests.cs`.
- **R6, Window.Toolbar has no handler**: fixed (C13). The bar is still drawn by the page that owns it; `SkiaToolbar` carries the toolbar's state for code that reads `Toolbar.Handler.PlatformView`.
- **R7, Shell flyout geometry**: fixed (C14). Left: a Scroll or CollapseOnScroll header does not scroll away or collapse as the items scroll (only the minimum height is applied).
- **R8, Shell lifecycle and layout details**: fixed (C15).
- **R9, formatted Label rendering (3): fixed.** A label with `TextType.Html` drew its FormattedText spans; MAUI's platforms show the Html text then and ignore the spans (`SkiaLabel.ShowsFormattedText`). Center/End-aligned plain text was placed by its ink bounds, formatted text by its advance width, so the two differed by a pixel or two; plain text is now aligned by its advance width too, as a platform text layout aligns a line (the `labels-wrap` golden's centred and right-aligned lines moved by that much and were re-recorded).
- **R10, smaller ones (1 to 2 each)**: SwipeView items leak / logical children (2); `FrameResizesItsContents` sets `Window.Width = 200` and expects the frame to shrink with the window: the headless test window stays 800x600 (`HeadlessWindowHost` lays the tree out at its fixed size; the frame itself re-measures correctly), so this is the window size not following `Window.Width`, not Frame (left: Window/test-host area); `ScrollView` content does not move when its HorizontalOptions change after it is shown (the arrange honours alignment now, but the change does not re-arrange the content); `AutomationProperties.ExcludedWithChildren` (fixed: `SemanticMapper` maps it to the new `SkiaView.IsExcludedWithChildren`, and the accessible tree leaves such a view and its subtree out); a disconnected CarouselView keeps its collection subscription; `Border` "renders the expected size" (fixed: it used MAUI's `PlatformNotSupportedException("TODO")` capture path, now patched with a Linux branch; the border itself was already right).

## Skipped

| Reason | Tests |
|---|---|
| **No WebKit host in a headless process**: the WebView/HybridWebView rows of the view-type theories (`ViewWithMarginSetsFrameAndDesiredSizeCorrectly`, `HandlerDoesNotLeak`) and `ValidateIsImportantForAccessibility` (puts every control, WebView included, in one page). OpenMaui's web views run in an out-of-process WebKit (WPE/WebKitGTK) host; creating one in the test process takes the test host down | 3 |
| `NativeFormattedStringContainsSpan`: tests the conversion to a platform-native attributed string type (Spannable, NSAttributedString, WinUI Runs); OpenMaui draws spans itself and has none | 1 |

## Harness corrections (not product)

Three early failures were the port's, corrected in the Linux helpers: a layout's `InputTransparent` on Linux already means what Windows achieves by keeping the panel hit-test visible (SkiaView hit-tests the children of an input-transparent view), so the layout check compares the flag (2 tests); MAUI calls such as `view.GetBoundingBox()` bind to the object-taking platform helpers here, which now resolve an element to its platform view; a capture of a view in an open window first renders a frame, so pending layout is applied as a platform applies it before a capture (4 tests).

## API surface findings (Controls)

- **34 public OpenMaui types in `Microsoft.Maui.Platform` share a name with a MAUI Controls or Core type** (`ShellContent`, `ShellSection`, `MenuItem`, `MenuBarItem`, `SwipeItem`, `ItemsLayoutOrientation`, `FlyoutLayoutBehavior`, `TextChangedEventArgs`, `ToggledEventArgs`, `CheckedChangedEventArgs`, `PositionChangedEventArgs`, `NavigationEventArgs`, `ScrolledEventArgs`, `ScrollToPosition`, `StackOrientation`, `IndicatorShape`, `GridLength`, `GridUnitType`, `ScrollOrientation`, `ScrollBarVisibility`, `SwipeDirection`, `SwipeMode`, `LayoutAlignment`, ...). MAUI code imports `Microsoft.Maui.Platform` routinely, so any file that also imports `Microsoft.Maui.Controls` gets CS0104 (ambiguous) or CS0176; the suite aliases them back (`GlobalUsings.Controls.cs`). Renaming them would be a breaking change; recorded.
- **OpenMaui realizes a page's views through its own map** (`MauiHandlerExtensions.ToHandler`): for the built-in Controls types it never consults the app's handler registrations, so `handlers.AddHandler<Label, MyLabelHandler>()` has no effect on labels inside pages (a registration for a subclass type is honoured when the handler derives from an OpenMaui handler).
- **No platform toolbar or flyout view**: MAUI's `Window.Toolbar` now has a Linux handler (`ToolbarHandler`, its platform element the window's `SkiaToolbar`), but the bar itself is drawn by `SkiaShell` / the current `SkiaPage`, and the Shell flyout panel is not a view (its bounds are `SkiaShell.FlyoutBounds`).
- **No public platform extensions** such as MAUI's `UpdateLineBreakMode(platformLabel, label)`; the suite routes these through the handler's mapper.

