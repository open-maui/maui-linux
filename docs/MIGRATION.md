# Migration guide

How to move an app between OpenMaui release lines. The policy behind these notes (what may break, and when) is in [`VERSIONING.md`](VERSIONING.md); the full list of changes is in `CHANGELOG.md`.

## Upgrading in general

1. Move every OpenMaui package to the same new version (`OpenMaui.Controls.Linux`, `OpenMaui.Hosting` and whichever of `.MediaElement`, `.Maps`, `.Blazor` you use). They are released in lockstep and are not supported mixed.
2. Move `Microsoft.Maui.Controls` (and `Microsoft.Maui.Graphics`, `Microsoft.Maui.Controls.Maps`, `Microsoft.AspNetCore.Components.WebView.Maui` if referenced directly) to at least the MAUI version in the first three parts of the OpenMaui version. The OpenMaui packages depend on that version, so an older explicit reference in your project fails restore with `NU1605` (package downgrade).
3. Read the section for your jump below and check the behaviour changes it lists against your app.
4. Run `OPENMAUI_DOCTOR=1 ./YourApp` on your target machines after upgrading; new releases can start using optional system libraries you do not have yet.

## 10.0.110.7 to 10.0.110.8

**API: no breaking changes.** Additions, and deprecations that are removed in 11.0 (below).

### Behaviour changes to check

These are fixes (the new behaviour is what MAUI specifies), but an app that worked around the old behaviour can look or act differently:

- **Handlers your app registers are now used.** `ConfigureMauiHandlers(h => h.AddHandler<Label, MyLabelHandler>())` (or any `AddHandler` for a built-in control: Button, Entry, pages, layouts...) used to have no effect on Linux: views inside pages always got OpenMaui's handler. They now get the registered handler, as on Android, iOS and Windows, including inside NavigationPage, Shell, templates and CollectionView rows, and a registration for a control also applies to its subclasses. If your app (or a library it uses) registers handlers that were silently ignored before, check that they behave on Linux. A registered handler must create a `SkiaView`: derive from OpenMaui's handler for the control (`Microsoft.Maui.Platform.Linux.Handlers.LabelHandler`, `TextButtonHandler`, `EntryHandler`, ...) or customise it through its `Mapper`. A cross-platform handler built on MAUI's portable handler (for example `class MyLabelHandler : Microsoft.Maui.Handlers.LabelHandler`) cannot draw on Linux; OpenMaui logs a warning naming it (with `DiagnosticLog.IsEnabled`) and keeps using its own handler for those views. A handler that is written for Linux but fails (throws, or makes a platform view that is not a `SkiaView`) is logged as an error once and replaced the same way.
- **`UseLinux()` keeps handlers registered before it.** A `ConfigureMauiHandlers` call placed before `UseLinux()` used to be overwritten by OpenMaui's registrations for the same control; the earlier registration now wins, as it does on MAUI's other platforms.
- **Shell tabs are at the top, as on Windows.** A Shell's sections are a row of tabs in a top navigation pane under the navigation bar (they were Android/iOS-style bottom tabs), and a section with several ShellContents opens a drop-down of them from its tab. The page content now runs to the window's bottom edge and starts under the pane. A pushed page keeps the tabs; set `Shell.TabBarIsVisible="False"` on it to hide them. A FlyoutItem holding one Tab with several ShellContents now shows the pane. `SkiaShell.TabBarBounds` reports the pane at the top and `TabBarHeight` defaults to 48 (was 56).
- **A Shell's flyout follows MAUI's effective behaviour.** With no `Shell.FlyoutBehavior` set, a Shell whose root is a TabBar, or one made of a single bare page, has no flyout and no hamburger button, as on MAUI's other platforms. The flyout lists MAUI's flyout items, so a TabBar's sections are no longer listed in it.
- **ListView keeps its place when its items change.** Adding, removing, replacing or moving items updates only those rows; the list no longer jumps back to the top. A Reset keeps the scroll offset.
- **CarouselView snaps as its `ItemsLayout` says.** CarouselView's defaults (`SnapPointsType.MandatorySingle`, `SnapPointsAlignment.Center`) now apply: a swipe moves at most one item and the item settles centred, and a drag past either end bounces (`IsBounceEnabled`). Each side of `PeekAreaInsets` is applied separately. A CarouselView given a bare `LinearItemsLayout` has no snap points (`SnapPointsType.None`), so a flick now coasts freely, as on MAUI's other platforms; use `LinearItemsLayout.CarouselDefault` / `CarouselVertical`, or set `SnapPointsType`, to snap item by item.
- **A touch reorder needs a press-and-hold.** With `CanReorderItems`, a finger must rest on an item for 500 ms before it can be dragged (Wayland; X11 reports touch as a mouse). A mouse drag starts at once, as before.
- **A window with a TitleBar draws its own title bar on X11 and on KDE Wayland.** The window manager's frame is turned off and the TitleBar fills the title bar, as Windows extends content into it. Windows without a TitleBar keep the system frame.
- **SecureStorage uses the Secret Service over D-Bus.** Values stored by earlier releases (through `secret-tool`) are found and replaced in place. `OPENMAUI_SECURESTORAGE=secret-tool` keeps the old process-per-call path; `=file` uses only the encrypted file.
- **A HybridWebView that handles `WebResourceRequested` has its own web storage.** The event now also reports the page's http and https requests, as on Windows. WebKit cannot intercept those directly, so such a view (subscribed when its handler is created) runs on its own WebKit network through a loopback proxy, and its cookies, localStorage and IndexedDB live in `<AppData>/WebKit/hybridwebview-network-N`, not in WebKit's default network. An app that already handled the event (it saw app-origin requests only before) starts that view with empty web storage after upgrading. A handler added after the view exists still sees app-origin requests only.
- **MediaElement headers reach HLS and DASH segments, on the source's host only.** `UriMediaSource.HttpHeaders` were sent with the manifest request only; they now also go with segment requests to the media source's host and port. Segments on another host (a CDN) get none of them, so a stream whose CDN segments needed the app's headers must sign its segment URLs instead.
- **A page's `Shell.SearchHandler` shows a search box in the top pane**, and shows the pane for it, as on Windows. Suggestions appear only while `ShowsResults` is set (MAUI's default is false).
- **`Application.ActivateWindow` raises the window** (X11, Wayland with `xdg_activation_v1`, GTK); the desktop's focus-stealing prevention may only mark it as wanting attention.
- **Contacts are also read from KDE's file-based stores** (Akonadi Personal Contacts, vCard folders and files, KDE Connect), merged after Evolution Data Server's address books.
- **`Application.Quit()` closes the app** (every window); before, it did nothing.

### Deprecated: types renamed to stop clashing with MAUI (removed in 11.0)

Some public OpenMaui types in `Microsoft.Maui.Platform` had the same names as MAUI types, so a file
importing `Microsoft.Maui.Platform` with `Microsoft.Maui.Controls` (or `.Layouts`, `.Primitives`)
got CS0104 "ambiguous reference" as soon as it named one. They are now `[Obsolete]`; the warning
names the replacement. Nothing breaks in 10.0.110.x, and compiled apps keep working. The types
still exist until 11.0, so the ambiguity remains until then; code that moves to the replacements
now compiles unchanged when they are removed.

- Enums and structs that duplicated MAUI's: use MAUI's type and the `Maui`-prefixed member
  (`stack.MauiOrientation = StackOrientation.Horizontal;` with `Microsoft.Maui.Controls.StackOrientation`,
  `grid.MauiRowDefinitions.Add(GridLength.Star)` with `Microsoft.Maui.GridLength`,
  `scroll.MauiOrientation = ScrollOrientation.Both`, ...). Methods take MAUI's enums through new
  overloads (`ScrollToAsync(view, Microsoft.Maui.Controls.ScrollToPosition.Center, true)`).
- OpenMaui's own models: construct `SkiaMenuItem`, `SkiaMenuBarItem`, `SkiaShellSection`,
  `SkiaShellContent`, `SkiaSwipeItem`; the collections still hold the old types, so read them with `var`.
- Event handlers: declare the parameter with the Skia name
  (`void OnTextChanged(object? sender, SkiaTextChangedEventArgs e)`); `entry.TextChanged += OnTextChanged`
  still binds. Swipe args expose MAUI's `SwipeDirection` as `e.SwipeDirection`.
- `SkiaIndicatorView.IndicatorShape` -> `IndicatorsShape` (`SkiaIndicatorShape`);
  `IButtonController` -> `ISkiaButtonController`; `FontExtensions` -> `SkiaFontExtensions`
  (`font.ToSKFont()` is unchanged); `Microsoft.Maui.Platform.Linux.Services.Browser` ->
  `Microsoft.Maui.ApplicationModel.Browser`; `DragEventArgs`/`DropEventArgs` (Linux services) ->
  `LinuxDragEventArgs`/`LinuxDropEventArgs`.
- In 11.0 the old types are removed and the Skia views' members are retyped to the new types.

### Deprecated: concrete-handler `Map*` overloads (removed in 11.0)

Each Linux handler's static `Map*` methods now have MAUI's interface-typed signatures (`EntryHandler.MapText(IEntryHandler, IEntry)`). The concrete-typed overloads (`MapText(EntryHandler, IEntry)`) are `[Obsolete]`; calls that pass the concrete handler bind to the new overload, so recompiled code gets no warning.

## 10.0.101.x to 10.0.110.x

**API: no breaking changes.** All five library packages pass package validation against 10.0.101.3 with no suppressions. Code that compiled against 10.0.101.3 compiles and runs against 10.0.110.1 unchanged.

**Dependencies:** MAUI 10.0.101 to 10.0.110 (`Microsoft.Maui.Controls`, `Graphics`, `Graphics.Skia`, `Controls.Maps`, `Components.WebView.Maui`). SkiaSharp (4.152.1) and HarfBuzzSharp (14.2.1.201) are unchanged.

### Behaviour changes to check

These are fixes (the new behaviour is what MAUI specifies), but an app that worked around the old behaviour can look or act differently:

- **`ContentPage.Content` honours its layout options.** A view set directly as a page's content was always stretched over the whole page. Now `HorizontalOptions`/`VerticalOptions` of `Start`, `Center` and `End` place it at its desired size. If a page's content should fill the page, leave the options at `Fill` (the default). A press on the page area beside such content no longer reaches the content.
- **The MAUI `Window` is live.** Replacing `Window.Page` (or `Application.MainPage`) now renders the new page; before, the window kept showing the old one. `Window.Title`, `Width`/`Height`, `X`/`Y` (X11 only), and `Minimum*`/`Maximum*` sizes are now applied to the native window in logical pixels. Values an app set before and that were silently ignored now take effect.
- **Minimizing raises `Stopped`/`Resumed`.** Minimizing or fully hiding a window now raises `Window.Stopped` and `Application.OnSleep`; restoring raises `Resumed`/`OnResume`, for every window. Work done in `OnSleep` (saving state, pausing media) now also happens on minimize, as on other platforms.
- **Scale can change at runtime.** Moving a window to a monitor with a different scale, or changing the desktop scale, re-renders the window at the new density. `LinuxApplication.DpiScale` follows the primary window and raises the new `DpiScaleChanged` event; code that read `DpiScale` once at startup and cached it should subscribe to the event (or read the per-window `IScaleAwareDisplayWindow.Scale`).
- **Scale detection is culture-invariant.** `GDK_SCALE`, `QT_SCALE_FACTOR`, gsettings, KDE and `Xft.dpi` values were parsed with the current culture, so `1.5` became 15 under German or French locales. Apps that compensated for this should remove the workaround.
- **WebView frames are zero-copy on the GPU target.** When the preconditions hold (see `docs/WPE-EMBEDDING.md`) WPE frames are imported as EGLImages. `OPENMAUI_WEBVIEW_ZEROCOPY=0` restores the copy path if a driver misbehaves. Pages now receive `KeyboardEvent.code`.
- **xdg-shell is bound up to version 6** on Wayland (was lower), for the `suspended` state.

### Packaging changes

- `OpenMaui.Controls.Linux` now ships the Wayland protocol shim for **linux-arm64** as well as linux-x64 (`runtimes/linux-arm64/native/libopenmaui_wl.so`). A `dotnet publish -r linux-arm64` output used to contain the x64 shim, so native Wayland failed to start on arm64; it now contains the aarch64 one. See [`ARM64.md`](ARM64.md).
- The package's MSBuild targets copy the shim next to the app only for builds without a `RuntimeIdentifier` (and then for the build machine's architecture). RID-specific builds and publishes take it from NuGet's normal `runtimes/<rid>/native` asset. Nothing changes in your project file.

### New APIs you may want

- `Microsoft.Maui.Platform.Linux.Diagnostics.OpenMauiDoctor` (`Run()`, `Format(report)`, `IsRequested(args)`) with `DoctorReport`/`DoctorSection`/`DoctorCheck`/`DoctorStatus`: the `openmaui doctor` report, for apps that want to show it themselves.
- `LinuxApplication.DpiScaleChanged`.
- `Microsoft.Maui.Platform.Linux.Services.IScaleAwareDisplayWindow` (`Scale`, `ScaleChanged`), `IVisibilityAwareDisplayWindow` (`IsSuspended`, `SuspendedChanged`) and `IDesktopWindowControl` (`RequestLogicalSize`, `RequestLogicalPosition`, `SetLogicalSizeLimits`), implemented by `WaylandWindow` and `X11Window`. They are new interfaces, so custom `IDisplayWindow` implementations do not need to change.
- `KeyEventArgs.HardwareKeycode` (XKB keycode) and `KeyMapping.EvdevToXkbKeycode`/`X11ToXkbKeycode`.
- `XEventType.MapNotify`/`UnmapNotify`.

## Earlier breaking changes

For apps upgrading across several lines at once. Each entry names the release that made the change and what to use instead.

### 10.0.101.3

- **Removed `Microsoft.Maui.Platform.Linux.Easing` and `Microsoft.Maui.Platform.Linux.AnimationManager`.** Animations now run on MAUI's own `ITicker`/`IAnimationManager`. Use `Microsoft.Maui.Easing` and the standard view extensions (`view.FadeTo(...)`, `TranslateTo`, `ScaleTo`, `RotateTo`, `Animation.Commit`, ...). The `SkiaViewAnimationExtensions` methods for standalone `SkiaView`s keep their names but take `Microsoft.Maui.Easing` instead of the removed type.
- **Removed `Handlers.PathHandler` and `SkiaPath`.** `Path` is drawn by `ShapePathHandler` with the other shapes; no change is needed for XAML or code using `Microsoft.Maui.Controls.Shapes.Path`.
- **`LinuxDialogService.ShowPromptAsync`** gained optional `maxLength` and `placeholder` parameters (the five-parameter method was replaced; source calls still compile, binaries built against 10.0.101.2 must be rebuilt), and `LinuxDialogService.TopDialog` now returns `SkiaModalDialog`, the new base of alert, prompt and action-sheet dialogs. `DisplayAlert`/`DisplayActionSheet`/`DisplayPromptAsync` from MAUI pages are the supported entry points and are unaffected.

### 10.0.101.1

- **SkiaSharp 3 to 4** (forced by MAUI 10.0.101's dependency floor). The legacy `SKPaint` text members (`TextSize`, `Typeface`, `TextAlign`, `MeasureText`, `FakeBoldText`, `FilterQuality`, paint-only `DrawText`) are `[Obsolete(error: true)]` in SkiaSharp 4. Custom drawing code (`GraphicsView` drawables written against SkiaSharp directly, custom `SkiaView`s) moves to `SKFont`-based measuring and drawing and `SKSamplingOptions`. Inside the platform, create fonts through `SkiaFontFactory` so measuring and drawing agree at fractional scales.
- `TextRenderCache.GetOrCreate` and `TextRenderingHelper.DrawPreEditUnderline` take an `SKFont` alongside the `SKPaint`.

### 10.0.90.1 and 10.0.70.x

- `DragDropService.ProcessPropertyNotify` gained a window parameter (10.0.90.1).
- `GestureManager.ProcessDrop` takes the dropped text and file list, `GestureManager.StartDrag` returns whether a drag started, and `SkiaShell.PushAsync` takes an optional MAUI `Page` being pushed (10.0.70.x; source-compatible, binary-breaking). These are platform plumbing; app code built on MAUI's gesture recognizers and Shell navigation is unaffected.

### 10.0.60.9

- `SkiaView` no longer declares 31 `BindableProperty` fields that duplicated `VisualElement`/`View` properties; they read from the bound MAUI view. Set these properties on the MAUI view, not on the `SkiaView`.
- `SkiaRenderingEngine.Current` was replaced by the per-tree `IRenderContext` (`RenderContext?.Resources`).
- Removed `LinuxApplicationOptions.UseHardwareAcceleration` and `ForceDemo` (they were never read), and the unused `GpuRenderingEngine`, `LayeredRenderer`, `X11DisplayWindow` and `WaylandDisplayWindow` types.

### 10.0.41

- Moved from .NET 9 / MAUI 9 to .NET 10 / MAUI 10 (`net10.0`), SkiaSharp 2.88 to 3.x. Picker handlers follow MAUI 10's nullable `DateTime`/`TimeSpan` on `IDatePicker`/`ITimePicker`.

### 9.0.40

- `1.0.0` was renumbered to `9.0.40` to align with MAUI; `1.0.0` is deprecated on NuGet. Reference `9.0.40` or later.
