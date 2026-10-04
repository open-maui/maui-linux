# MAUI handler conformance (dotnet/maui's own tests against the Linux handlers)

Run of 2026-10-04 against dotnet/maui tag `10.0.110`. **1475 MAUI tests ported: 795 passed, 574 failed, 106 skipped** (of the skips, 27 are MAUI's own, 71 are wrapper-view tests that do not apply, 8 are blocked; see below).

The suite compiles dotnet/maui's shared handler device tests (`src/Core/tests/DeviceTests/Handlers/*/<X>HandlerTests.cs` and the shared bases in `src/Core/tests/DeviceTests.Shared`) unchanged, against OpenMaui's handlers, so the assertions are MAUI's, not a reading of MAUI. Only the per-platform helper partials (MAUI's `*.Windows.cs` / `*.Android.cs`, "read the value back from the native view") are written for Linux, and they read the Skia platform view.

Every failing test below is still failing in the project: nothing is suppressed. A failure is a candidate parity bug; the diagnoses were checked against the handler source, and the few that are a harness limitation rather than a product gap are marked as such.

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
| ActivityIndicatorHandlerTests | 42 | 21 | 17 | 4 |
| BorderHandlerTests | 43 | 25 | 14 | 4 |
| ButtonHandlerTests | 49 | 29 | 16 | 4 |
| ButtonHandlerTests+ButtonTextStyleTests | 22 | 4 | 18 | 0 |
| CheckBoxHandlerTests | 36 | 18 | 14 | 4 |
| ContentViewTests | 33 | 15 | 14 | 4 |
| DatePickerHandlerTests | 34 | 16 | 14 | 4 |
| DatePickerHandlerTests+DatePickerTextStyleTests | 22 | 14 | 8 | 0 |
| EditorHandlerTests | 125 | 52 | 69 | 4 |
| EditorHandlerTests+EditorFocusTests | 1 | 0 | 1 | 0 |
| EditorHandlerTests+EditorTextInputTests | 17 | 14 | 3 | 0 |
| EditorHandlerTests+EditorTextStyleTests | 22 | 22 | 0 | 0 |
| EntryHandlerTests | 148 | 112 | 32 | 4 |
| EntryHandlerTests+EntryFocusTests | 1 | 0 | 1 | 0 |
| EntryHandlerTests+EntryTextInputTests | 17 | 16 | 1 | 0 |
| EntryHandlerTests+EntryTextStyleTests | 22 | 22 | 0 | 0 |
| GraphicsViewHandlerTests | 44 | 26 | 14 | 4 |
| ImageButtonHandlerTests | 33 | 14 | 15 | 4 |
| ImageButtonHandlerTests+ImageButtonImageHandlerTests | 55 | 24 | 22 | 9 |
| ImageHandlerTests | 55 | 23 | 23 | 9 |
| LabelHandlerTests | 60 | 42 | 14 | 4 |
| LabelHandlerTests+LabelTextStyleTests | 22 | 22 | 0 | 0 |
| LayoutHandlerTests | 48 | 21 | 20 | 7 |
| PickerHandlerTests | 42 | 24 | 14 | 4 |
| PickerHandlerTests+PickerTextStyleTests | 22 | 14 | 8 | 0 |
| ProgressBarHandlerTests | 39 | 21 | 14 | 4 |
| RadioButtonHandlerTests | 36 | 18 | 14 | 4 |
| RadioButtonHandlerTests+RadioButtonTextStyleTests | 22 | 6 | 16 | 0 |
| ScrollViewHandlerTests | 31 | 12 | 15 | 4 |
| SearchBarHandlerTests | 118 | 26 | 88 | 4 |
| SearchBarHandlerTests+SearchBarTextInputTests | 18 | 15 | 3 | 0 |
| SearchBarHandlerTests+SearchBarTextStyleTests | 22 | 14 | 8 | 0 |
| SliderHandlerTests | 47 | 29 | 14 | 4 |
| StepperHandlerTests | 35 | 17 | 14 | 4 |
| SwitchHandlerTests | 37 | 18 | 14 | 5 |
| TimePickerHandlerTests | 33 | 15 | 14 | 4 |
| TimePickerHandlerTests+TimePickerTextStyleTests | 22 | 14 | 8 | 0 |
| **Total** | **1475** | **795** | **574** | **106** |

Every `<X>HandlerTests` row includes the generic tests (about 30) MAUI runs for every handler (`HandlerTestBaseOfT.Tests.cs`: automation id, flow direction, opacity, visibility, semantics, bounds, transforms, constructors, container view).

## Failures by root cause

Each failure belongs to exactly one group; counts are failing test cases (theory rows count separately).

### G1. Base IView properties are not mapped by the Linux handlers (330 failures, every handler)

Failing in every class: `Opacity is set correctly` (4 rows), `Visibility is set correctly` (Collapsed, Hidden), `FlowDirection is set correctly` (RightToLeft), `Automation Id is set correctly`, `Null Semantics Doesnt throw exception` (automation id), `Semantic Description/Hint/Heading is set correctly`, `Setting Semantic Description/Hint makes element accessible` (14 per class). Also the ActivityIndicator visibility tests (`IsRunning Should Respect Visibility At Init`, `Setting IsRunning After Init Should Respect Hidden/Collapsed Visibility`) and the five Layout `*ZIndexOrder` tests.

Example: `Assert.Equal() Failure: Expected: 0.25 Actual: 1` (Opacity); `Expected: "TestId" Actual: ""` (AutomationId).

Diagnosis: MAUI maps these through `ViewHandler.ViewMapper` (`MapOpacity`, `MapVisibility`, `MapFlowDirection`, `MapAutomationId`, `MapSemantics`, ZIndex via the layout). On the platform-neutral net10.0 build those mappers call no-op extensions, and the Linux handlers chain `ViewMapper` without replacing them. Instead, OpenMaui copies them from a **Controls** `View` only: `MauiHandlerExtensions.ToHandler` syncs `IsVisible`/`Opacity`/`InputTransparent` and subscribes to `PropertyChanged`, `SemanticMapper.Apply` copies semantics, and the `SkiaView` getters (`Opacity`, `FlowDirection`, `AutomationId`, `ZIndex`) read through `SkiaView.MauiView`. So a Controls app works, but `handler.UpdateValue(nameof(IView.Opacity))` does nothing, any `IView` that is not a `Controls.View` (MAUI's stubs, third-party core views, a handler created through MAUI's own factory instead of OpenMaui's `ToHandler`) gets none of these, and `Visibility.Hidden` (keep layout space, draw nothing) has no platform mapping at all (`IsVisible` is a bool). Fix direction: register Linux mappers for these keys into `ViewHandler.ViewMapper` (as `FocusCommands` does for the Focus command) that read the `IView` interface.

The Layout z-order tests fail for the same reason: the layout stacks children by `SkiaView.ZIndex` (`SkiaLayoutView.ChildrenInZOrder`, used for drawing and hit-testing, which the Linux helper compares), and that getter only reads `Controls.View.ZIndex`, never `IView.ZIndex`.

### G2. ButtonHandler maps no text for IButton (20 failures)

`ButtonHandlerTests`: `Text Initializes Correctly` (`Expected: Test Actual: ""`), `Text Color Initializes Correctly` (`Actual: null`); `ButtonTextStyleTests`: all font size, auto-scaling, family/weight and attribute rows (`Font Size Initializes Correctly(fontSize: 20)`: `Expected: 20 Actual: 14`).

Diagnosis: the Linux `ButtonHandler.Mapper` maps only stroke, corner radius, background, padding and IsEnabled. Text, TextColor, Font and CharacterSpacing are mapped by `TextButtonHandler`, which OpenMaui registers for `Controls.Button` only. MAUI's `ButtonHandler` maps `ITextButton`/`ITextStyle` itself, so any `IButton` that is not a `Controls.Button` (and anything resolving `ButtonHandler` directly) shows no text.

### G3. RadioButton: no font family or attributes (16 failures)

`RadioButtonTextStyleTests`: every `Font Family and Weight` and `Font Attributes` row: `SkiaRadioButton exposes no FontAttributes`.

Diagnosis: `SkiaRadioButton` has `FontSize` only; `RadioButtonHandler.MapFont` copies `Font.Size` and drops family, weight and slant.

### G4. Picker, DatePicker, TimePicker, SearchBar ignore italic (32 failures)

`<X>TextStyleTests`: the italic rows of `Font Family and Weight Initializes Correctly` and `Font Attributes Initialize Correctly` (`Expected: True Actual: False` for isItalic); bold passes.

Diagnosis: their `MapFont` builds `FontAttributes` from `Font.Weight` only (`DatePickerHandler.cs` even notes "Font.Slant for italic would require checking FontSlant"). Label, Entry and Editor handle slant and pass all 22 text-style tests.

### G5. Keyboard is not carried to the platform view (102 failures)

- Entry (6): `Validates Numeric/Email/Telephone/Url/Text/Chat Keyboard` for the matching keyboard (`Expected: True Actual: False`). `EntryHandler.Mapper` has no `IEntry.Keyboard` entry, although `SkiaEntry.Keyboard` exists.
- Editor (48) and SearchBar (48): every row: `SkiaEditor exposes no Keyboard` / `SkiaSearchBar exposes no Keyboard`. `EditorHandler.MapKeyboard` is an empty method ("Virtual keyboard type not applicable to desktop"), SearchBar has no mapping.

Note: on the desktop the keyboard is the input-method hint (text-input-v3 / IBus content purpose: digits, email, phone, URL), so it is not inapplicable; these stay counted as gaps.

### G6. MaxLength does not clip text set from code (18 failures)

Entry `MaxLength Initializes Correctly` and `MaxLength Clips Native Text Correctly` (10), Editor `MaxLength Initializes Correctly` (4), SearchBar `MaxLength Initializes Correctly` (4): `Expected: "Lorem" Actual: "Lorem ipsum dolor sit amet"`.

Diagnosis: `SkiaEntry`/`SkiaEditor` apply `MaxLength` only to typed/pasted input, so text assigned from code (or present when MaxLength is set) is never truncated, and the virtual view's Text is not written back clipped. `MaxLength == 0` is treated as unlimited (`MaxLength > 0 &&` guards); in MAUI 0 means no text, -1 unlimited. SearchBar has no MaxLength mapping at all.

### G7. null vs "" text write-back (12 failures)

Entry/Editor `Text Updates Correctly(setValue: "Hello", unsetValue: null)` (`Expected: null Actual: ""`, the virtual view's value), SearchBar `Query Text Updates Correctly`, `TextChanged Events Fire Correctly(initialText: null, newText: null, eventExpected: False)` (`Expected: 0 Actual: 1`) on Entry and SearchBar, Editor `TextChanged Events Fire Correctly(..., newText: null)` (`e.NewValue` is null, MAUI expects ""), Editor `PlaceholderText Initializes Correctly` (null placeholder reads back as "").

Diagnosis: when the virtual view's Text is null, the Linux handler echoes the platform's "" back into `ITextInput.Text`, which rewrites the app's null and raises a TextChanged that MAUI does not raise (MAUI treats null and "" as equal). The Editor goes the other way: `SkiaEditor.Text` stores null and the user-edit path reports null where MAUI reports "".

### G8. SearchBar is missing text-input properties (22 failures)

`IsTextPredictionEnabled` / `IsSpellCheckEnabled` (init, update, "differs from") and `IsReadOnlyInitializesCorrectly`: `SkiaSearchBar exposes no IsTextPredictionEnabled` (etc.); `CursorPositionDoesntResetWhenNativeTextValueChanges` (`exposes no CursorPosition`); `CursorPosition Updates After Typing Via Native Input` (times out). The search field has no cursor/selection, read-only, prediction or spell-check state, and `SearchBarHandler.Mapper` maps none of them.

### G9. Focus is not reported back to the virtual view (2 failures)

`EntryFocusTests` / `EditorFocusTests` `FocusAndIsFocusedIsWorking`: `Assert.True(inputControl1.IsFocused)` fails after the platform view did take focus (`handler.Invoke(Focus)` returned true and `SkiaView.IsFocused` became true). MAUI handlers set `IView.IsFocused` when the native view gains/loses focus; the Linux handlers only update `Controls.VisualElement`'s focus state.

### G10. Image loading events are not raised (7 failures)

Image and ImageButton `SourceInitializesCorrectly` (3 rows each): `Expected ["LoadingStarted", "LoadingCompleted(True)"] Actual []`; ImageButton `LoadingCompleted event fires`. The picture itself loads and renders (the colour assertion before it passes).

Diagnosis: MAUI's contract is `IImageSourcePartEvents` (LoadingStarted / LoadingCompleted / LoadingFailed), raised by `ImageSourcePartLoader`. The Linux image handlers load through their own `ImageSourceServiceResultManager` and call only `IImageSourcePart.UpdateIsLoading`; `ImageHandler` does not even do that.

### G11. A failed or null source leaves the old picture (4 failures)

Image and ImageButton `UpdatingSourceToNullClearsImage` and `UpdatingSourceToNonexistentSourceClearsImage`: `Color Red was found ... top colors: #ffff0000 x2500`. Setting Source to null calls `LoadFromData(Array.Empty<byte>())`, and a source that fails to load leaves the previous bitmap; MAUI clears the image in both cases.

### G12. IsAnimationPlaying is not mapped (1 failure)

`ImageHandlerTests.AnimatedSourceInitializesCorrectly(animated_heart.gif, isAnimating: True)`: `Expected: True Actual: False`. `ImageHandler.Mapper` has no `IImage.IsAnimationPlaying` entry (`SkiaImage.IsAnimationPlaying` exists).

### G13. Inconclusive, harness limitation (6 failures)

Image and ImageButton `InitializingNullSourceOnlyUpdatesNull` (3 rows each): `Assert.NotEmpty() Failure: Collection was empty`. MAUI's `CountedImageHandler` logs every native image assignment; the Linux port can only observe `SkiaImage.ImageLoaded`, and `SkiaImage.Bitmap` has no change notification, so a null assignment cannot be seen. Not counted as a product gap until `SkiaImage` exposes a way to observe it.

### G14. Layout DisconnectHandler keeps its platform children (1 failure)

`DisconnectHandler removes child from native layout`: `Expected: 0 Actual: 1`. MAUI's LayoutHandler clears the platform children on disconnect; the Linux one leaves them attached to the old `SkiaLayoutView`.

### G15. ScrollViewHandler has no (mapper, commandMapper) constructor (1 failure)

`HandlersHaveAllExpectedContructors`: `Missing constructor with IPropertyMapper and ICommandMapper`. `ScrollViewHandler` only has `(IPropertyMapper?)`, so a subclass cannot supply its own command mapper. Every other Linux handler in the set has the two-argument constructor.

## Skipped

| Reason | Tests |
|---|---|
| MAUI's own skips (`View Renders To Image` on non-Android, `Shadow Initializes Correctly` on Layout, `ThumbColor Initializes Correctly` on Switch, `InvalidSourceFailsToLoad` on images) | 27 |
| **No wrapper view** (`Clip Initializes ContainerView Correctly`, `ContainerView Remains If Shadow Mapper Runs Again`, `ContainerView Adds And Removes` for every handler; Layout `ContainerViewAddedToLayout`, `ContainerViewDifferentThanPlatformView`): Skia views apply Clip and Shadow while drawing, there is no native wrapper to add, and MAUI's platform-neutral ViewHandler has no container implementation | 71 |
| **BLOCKED** (counted with the gaps, skipped only because they would hang): Image and ImageButton `ImageLoadSequenceIsCorrect`, `InterruptingLoadCancelsAndStartsOver` and their `WithChecks` variants wait without a timeout for MAUI's `IImageSourceService` to start a load, which the Linux image handlers never call (same root cause as G10) | 8 |

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
