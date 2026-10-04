# MAUI handler conformance (dotnet/maui's own tests against the Linux handlers)

Run of 2026-10-04 against dotnet/maui tag `10.0.110`. **1475 MAUI tests ported: 1369 passed, 0 failed, 106 skipped** (of the skips, 27 are MAUI's own, 71 are wrapper-view tests that do not apply, 8 are blocked; see below). The first run failed 574; the gaps it found, and their fixes, are listed under "Gaps found and fixed".

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
- `src/Core/tests/DeviceTests/`: `Handlers/CoreHandlerTestBase.cs`, `Handlers/CoreHandlerTestBaseOfT.cs`, `Stubs/*.cs` (cross-platform files; minus `CountedImageHandler.cs`, `CountedImageSourceServiceStub.cs`, `WebView.cs`, `ButtonWithContainerStub.cs`, `StubBaseHandler.cs`, `WindowHandlerProxyStub.cs`, which need platform-only API), `Services/ImageSource/BaseImageSourceServiceTests.cs`, `Resources/Images/*`, `Resources/Raw/*.png` (pictures the image tests load by name)
- Handler tests: `ActivityIndicator`, `Border`, `Button`, `CheckBox`, `ContentView` (`ContentViewTests.cs`), `DatePicker`, `Editor`, `Entry`, `GraphicsView`, `Image`, `ImageButton`, `Label`, `Layout`, `Picker`, `ProgressBar`, `RadioButton`, `ScrollView`, `SearchBar`, `Slider`, `Stepper`, `Switch`, `TimePicker` (each `Handlers/<X>/<X>HandlerTests.cs`)

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
| ImageButtonHandlerTests+ImageButtonImageHandlerTests | 55 | 46 | 0 | 9 |
| ImageHandlerTests | 55 | 46 | 0 | 9 |
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
| SliderHandlerTests | 47 | 43 | 0 | 4 |
| StepperHandlerTests | 35 | 31 | 0 | 4 |
| SwitchHandlerTests | 37 | 32 | 0 | 5 |
| TimePickerHandlerTests | 33 | 29 | 0 | 4 |
| TimePickerHandlerTests+TimePickerTextStyleTests | 22 | 22 | 0 | 0 |
| **Total** | **1475** | **1369** | **0** | **106** |

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

Two diagnoses were the test port's, not the product's, and were corrected in the Linux helpers: MAUI's `Semantics.Description` is OpenMaui's accessible name (`SemanticName`), as it is `AutomationProperties.Name` on Windows; and the image helpers now observe a picture being cleared.

## Skipped

| Reason | Tests |
|---|---|
| MAUI's own skips (`View Renders To Image` on non-Android, `Shadow Initializes Correctly` on Layout, `ThumbColor Initializes Correctly` on Switch, `InvalidSourceFailsToLoad` on images) | 27 |
| **No wrapper view** (`Clip Initializes ContainerView Correctly`, `ContainerView Remains If Shadow Mapper Runs Again`, `ContainerView Adds And Removes` for every handler; Layout `ContainerViewAddedToLayout`, `ContainerViewDifferentThanPlatformView`): Skia views apply Clip and Shadow while drawing, there is no native wrapper to add, and MAUI's platform-neutral ViewHandler has no container implementation | 71 |
| **BLOCKED** (counted with the gaps, skipped only because they would hang): Image and ImageButton `ImageLoadSequenceIsCorrect`, `InterruptingLoadCancelsAndStartsOver` and their `WithChecks` variants wait without a timeout for MAUI's `IImageSourceService` to start a load. The Linux image handlers load file, URI, stream and font sources themselves and never resolve an `IImageSourceService` (the platform-neutral `IImageSourceService` has no load method; a Linux one would be new API), so a custom source such as the tests' `CountedImageSourceStub` reports LoadingFailed | 8 |

## API surface findings (needed scaffolding to compile MAUI's tests)

These are not test failures, but each is a place where OpenMaui's public API differs from MAUI's and code written against MAUI's handler API does not compile or behaves differently:

- **`Microsoft.Maui.Platform.LayoutAlignment`**: OpenMaui declares a public enum with this name (different ordinals) in `Microsoft.Maui.Platform`, which MAUI code imports routinely. Any file that also imports `Microsoft.Maui.Primitives` gets `CS0104` (MAUI's `StubBase.cs` does). The suite works around it with a global alias.
- **No `I<Control>Handler` interfaces**: the Linux handlers do not implement `IEntryHandler`, `IEditorHandler`, `ISearchBarHandler`, `IImageHandler`, `ILayoutHandler`, ... MAUI's `PropertyMapper<IEntry, IEntryHandler>` customizations, `IImageHandler.SourceLoader` and `ILayoutHandler.Add/Insert/Remove/Clear/Update/UpdateZIndex` are unavailable. The aliases add them as adapters (the layout methods send the commands `Controls.Layout` sends; `SourceLoader` throws).
- **Static `Map*` methods take the concrete Linux handler** (`MapIsPassword(EntryHandler, IEntry)`), not MAUI's interface (`(IEntryHandler, IEntry)`), and some MAUI ones do not exist (`EntryHandler.MapKeyboard`), so `[nameof(IEntry.Keyboard)] = EntryHandler.MapKeyboard` does not compile.
- **`IPlatformViewHandler` does not exist** on the platform-neutral TFM OpenMaui builds on; the suite declares a marker interface.

## Not ported, and why

- **ShapeView / BoxView**: MAUI's `ShapeViewHandlerTests` drive `ShapeViewHandler` with an `IShapeView` stub. OpenMaui has no `IShapeView` handler: `RectangleHandler`, `EllipseHandler`, `BoxViewHandler`, ... are typed to the Controls classes (`LinuxViewHandler<Rectangle, ...>`), so MAUI's stubs cannot be handled. Blocked (and itself a parity finding: a core `IShapeView` has no Linux handler).
- **RefreshView, IndicatorView, SwipeView** (MAUI Core tests exist): same reason, the Linux handlers are typed to `Controls.RefreshView` / `IndicatorView` / `SwipeView`, not `IRefreshView` / `IIndicatorView` / `ISwipeView`.
- **View, Element, Window, Page, Navigation, WebView core tests**: `ViewHandlerTests` / `ElementTests` test MAUI's own `ViewHandler`/`ElementHandler` stubs, not a Linux handler; window, page and navigation tests need MAUI's platform window plumbing; WebView needs the out-of-process WebKit host.
- **`src/Controls/tests/DeviceTests`** (Controls-level tests: real `Label`, `Entry`, ... with Controls handlers): the next step. They need a Linux port of `ControlsHandlerTestBase`'s window hosting (`CreateHandlerAndAddToWindow`, about 200 lines per platform partial), which can be built on the headless `WindowContext` the core suite already uses. Because OpenMaui's mapping is Controls-centric (G1, G2), that suite would show which of the gaps above an app actually hits.
- Platform-only tests in MAUI's `*.Android.cs` / `*.iOS.cs` / `*.Windows.cs` partials (transforms, input transparency, native font objects, ...) are not ported: they assert native API and live in platform files.
