# Third-party stubs (what the platform-neutral library builds leave out)

An OpenMaui app builds plain `net10.0`, so it loads each library's platform-neutral build (`lib/net10.0`) instead of a platform one. Many libraries leave work out of that build: a method is empty, returns a default, or throws, or a method the Windows build has is missing. OpenMaui fills most of these holes, in its handlers, services and patches (`Syncfusion/`, `Handlers/`, `Services/`, `MediaElement/`, `Pdf/`). What is left is listed here.

`tools/StubScan` finds the holes by comparing each library's neutral build with its Windows build (`lib/net10.0-windows*`), method by method:

- **stub**: the neutral body is a stub (empty, a constant or `default`, or only a `throw`) while the Windows body does real work;
- **windows-only**: the Windows build declares a method, on a type both builds share, that the neutral build does not have.

Every finding is reviewed in `tools/StubScan/baseline.json`:

- `covered`: OpenMaui bridges it. `by` names the file(s) that do it, and the scan checks that they exist.
- `not-applicable`: it has no meaning on Linux, for example WinUI plumbing that the Skia renderer replaces. `reason` says why.
- `open`: a real parity gap, listed below.

The **Tests** workflow (`.gitea/workflows/tests.yml`) runs the scan. It fails when it finds something that is not in the baseline, which happens when a library update brings in a stub nobody has reviewed. To review new findings, run `dotnet run --project tools/StubScan -- --write-baseline`. That adds them as `unreviewed`, which still fails. Grep the bridge folders for the type, set each finding's status, then commit. Use `--report <file.md>` to list every open member, and `--verbose` to print them all.

## Scan of 2026-10-04

Package versions are the ones `tests/Compat` restores:

| Package | Version | Findings | covered | n/a | open |
|---|---|---:|---:|---:|---:|
| Microsoft.Maui.Core | 10.0.110 | 573 | 462 | 77 | 34 |
| Microsoft.Maui.Controls.Core | 10.0.110 | 317 | 240 | 42 | 35 |
| Microsoft.Maui.Controls.Xaml | 10.0.110 | 0 | 0 | 0 | 0 |
| Microsoft.Maui.Essentials | 10.0.110 | 208 | 124 | 21 | 63 |
| CommunityToolkit.Maui | 15.0.1 | 27 | 14 | 0 | 13 |
| CommunityToolkit.Maui.Core | 15.0.1 | 48 | 24 | 1 | 23 |
| CommunityToolkit.Maui.MediaElement | 10.0.0 | 39 | 20 | 5 | 14 |
| Syncfusion.Maui.Core | 34.2.9 | 668 | 589 | 1 | 78 |
| Syncfusion.Maui.Scheduler | 34.2.9 | 253 | 0 | 0 | 253 |
| Syncfusion.Maui.Inputs | 34.2.9 | 33 | 0 | 0 | 33 |
| Syncfusion.Maui.Popup | 34.2.9 | 23 | 21 | 0 | 2 |
| Syncfusion.Maui.ListView | 34.2.9 | 13 | 12 | 1 | 0 |
| Syncfusion.Maui.TabView | 34.2.9 | 13 | 13 | 0 | 0 |
| Syncfusion.Maui.Buttons | 34.2.9 | 10 | 5 | 0 | 5 |
| Syncfusion.Maui.NavigationDrawer | 34.2.9 | 10 | 10 | 0 | 0 |
| Syncfusion.Maui.TreeView | 34.2.9 | 9 | 7 | 0 | 2 |
| Syncfusion.Maui.PdfToImageConverter | 34.2.9 | 7 | 7 | 0 | 0 |
| Syncfusion.Maui.Carousel | 34.2.9 | 4 | 4 | 0 | 0 |
| Syncfusion.Maui.Calendar | 34.2.8 | 3 | 0 | 0 | 3 |
| Syncfusion.Maui.Picker | 34.2.9 | 2 | 0 | 0 | 2 |
| Syncfusion.Maui.Charts, Sliders | 34.2.9 | 1 each | 1 / 0 | 0 | 0 / 1 |
| Syncfusion.Maui.Cards, DataSource, GridCommon, Rotator, SignaturePad | 34.2.9 | 0 | 0 | 0 | 0 |
| **Total** | | **2262** | **1553** | **148** | **561** |

Counts are per method, so a single gap can be many findings. The Syncfusion Scheduler's 253 open findings, for example, are a handful of desktop features.

## Open gaps, most impactful first

### Microsoft.Maui.Essentials

- **Files:** `FileResult.OpenReadAsync()` and `ContentType` throw, because the neutral `FileBase` is a stub. A picked file is usable only through `FullPath`.
- **Permissions:** `Permissions.CheckStatusAsync` and `RequestAsync` throw, because `BasePlatformPermission` is a stub and nothing on Linux bridges it.
- **Static entry points:**
  - `Launcher.Default`: only `OpenAsync(Uri)` is patched. `CanOpenAsync`, `TryOpenAsync` and `OpenAsync(OpenFileRequest)` throw.
  - `DeviceDisplay.Current`: `KeepScreenOn` and `MainDisplayInfoChanged` do nothing.
  - The DI-registered services work.
- **Media picker:** camera capture returns null. The resize and compression options (`MaximumWidth`, `MaximumHeight`, `CompressionQuality`, `RotateImage`) are ignored.
- **Contacts and geocoding:** the Linux services are stubs that return empty results.
- **Motion and environment sensors:** they report `IsSupported=false`, since there is no iio-sensor-proxy backend. This matters little on desktops.

### Microsoft.Maui.Core / Controls

- **HybridWebView:** there is no Linux handler, so no JavaScript bridge, raw messaging or request interception.
- **WebView:** `WebResourceRequested` (MAUI 10) is never raised.
- **NavigationPage:** the navigation bar does not support `TitleView`, `TitleIconImageSource`, `BackButtonTitle`, `HasBackButton`, `IconColor` or `HasNavigationBar`.
- **CollectionView:**
  - `CanReorderItems` (drag to reorder) is not implemented, and neither is `IsGrouped` (group headers and footers).
  - `ItemsUpdatingScrollMode` is not implemented.
  - CarouselView's `EmptyView` and scroll-bar visibility are not mapped.
- **Window overlays:** `Window.AddOverlay` and the visual diagnostics overlay never draw. `Window.TitleBar` (custom title-bar content) is not supported.
- **Shapes:**
  - `Aspect`, `StrokeDashPattern`, `StrokeLineCap`, `StrokeLineJoin` and `StrokeMiterLimit` are not mapped.
  - `Path.RenderTransform` is not applied.
  - `Polyline.FillRule` is not mapped.
  - RoundRectangle has no handler.
- **Pickers:** `IsOpen` (opening and closing a picker from code) is not mapped.
- **RefreshView:** `IsRefreshEnabled` is not mapped. `Button.LineBreakMode` is not mapped either.
- Most of these gaps are also listed in `tests/Hosting/MapperParityTests.cs` (`KnownGaps`).

### CommunityToolkit.Maui

- **TouchBehavior and ImageTouchBehavior:** they never attach, so there are no pressed or hover states and no touch commands.
- **MediaElement:**
  - `MediaOpened`, `MediaFailed` and `CurrentState` changes are not raised.
  - `MediaWidth` and `MediaHeight` are not reported.
  - `Speed`, `HttpHeaders`, `ShouldKeepScreenOn` and `ShouldShowPlaybackControls` are ignored.
  - Metadata is not published (no MPRIS).
  - The handler does not tear down its pipeline in `DisconnectHandler`.
- **DrawingView:** `GetImageStream` returns an empty stream. A custom `IDrawingLineAdapter` is ignored.
- **Speech:** `SpeechToText` and `OfflineSpeechToText` throw.
- **Toast and Snackbar:** they draw nothing.
- **Smaller gaps:** `Badge.SetCount` throws, and `SemanticOrderView` does not change the AT-SPI reading order.

### Syncfusion

- **SfImageEditor:** it has no Linux platform view, and neither has Syncfusion's media element. `UseLinuxSyncfusion` says both are not covered.
- **SfScheduler:**
  - The desktop layouts are Windows-only: horizontal resource view, timeline widths and view updates.
  - Mouse drag-and-drop and resizing of appointments (WinUI drag-and-drop with timers) are missing. Linux has the touch and long-press path, with positions from `SfInputPositionPatches`.
  - There is no hover highlight, no resize cursor, no header right-tap, and the accessibility node positions are missing.
- **SfNumericEntry and SfMaskedEntry:** the key-level handling is missing:
  - SfNumericEntry: Up, Down, PageUp and PageDown, and wheel increments.
  - SfMaskedEntry: masked typing, paste and cut.
  - Both: hover.

  The neutral build handles text changes only.
- **SfComboBox and SfAutocomplete:**
  - Backspace and Delete on multi-selection chips do nothing, and the chip area width is not updated.
  - A drop-down inside a template does not close when the control loses focus.
  - Some sizing and RTL helpers are Windows-only.
- **SfTextInputLayout:** the inner Entry keeps its own border, background and padding, which Windows strips. There is no hover effect.
- **SfDataGrid:** its scroll handler (scroll bars, manipulation, wheel state) has no bridge. SfDataGrid is not verified on Linux.
- **Keyboard and focus:**
  - Syncfusion buttons and segmented controls are not Tab stops; they get focus only on a click.
  - SfInteractiveScrollView ignores scroll and zoom keys, and picker columns ignore the keyboard.
- **Smaller gaps:**
  - No inertia after a pan.
  - Picker columns cannot be panned with a mouse drag.
  - RTL is not applied to SfCalendar `FlowDirection` changes, SfPopup or the range slider's touch points.
  - `PopupExtension.GetStatusBarHeight` returns 0.
  - SfCarousel ignores `EnableVirtualization`.
  - The TreeView drag popup has no shadow.
  - SfView accessibility semantics are not refreshed.

## Limits of the scan

- It compares against the Windows build only. Behaviour that Android or iOS implement but Windows does not is not reported.
- It recognises a stub only by its shape: empty, a constant or `default`, or a short straight-line `throw`. A neutral body that returns a placeholder object (for example `ValueTask.FromResult(Stream.Null)`) is not reported as a stub. For that reason the CommunityToolkit `DrawingViewService` gap above was found by review and is recorded under its windows-only members.
- Windows-only methods are matched by name, so an overload that exists only on Windows is not reported when the neutral build has another overload with the same name.
- Source-generated binding interceptors (`Microsoft.Maui.Controls.Generated.*`) are skipped. Their names carry per-build hashes, and they contain no platform code.
