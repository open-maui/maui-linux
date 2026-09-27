# Migration guide

How to move an app between OpenMaui release lines. The policy behind these notes (what may break, and when) is in [`VERSIONING.md`](VERSIONING.md); the full list of changes is in `CHANGELOG.md`.

## Upgrading in general

1. Move every OpenMaui package to the same new version (`OpenMaui.Controls.Linux`, `OpenMaui.Hosting` and whichever of `.MediaElement`, `.Maps`, `.Blazor` you use). They are released in lockstep and are not supported mixed.
2. Move `Microsoft.Maui.Controls` (and `Microsoft.Maui.Graphics`, `Microsoft.Maui.Controls.Maps`, `Microsoft.AspNetCore.Components.WebView.Maui` if referenced directly) to at least the MAUI version in the first three parts of the OpenMaui version. The OpenMaui packages depend on that version, so an older explicit reference in your project fails restore with `NU1605` (package downgrade).
3. Read the section for your jump below and check the behaviour changes it lists against your app.
4. Run `OPENMAUI_DOCTOR=1 ./YourApp` on your target machines after upgrading; new releases can start using optional system libraries you do not have yet.

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
