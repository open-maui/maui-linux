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
| Microsoft.Maui.Core | 10.0.110 | 573 | 496 | 77 | 0 |
| Microsoft.Maui.Controls.Core | 10.0.110 | 317 | 274 | 43 | 0 |
| Microsoft.Maui.Essentials | 10.0.110 | 208 | 186 | 22 | 0 |
| Microsoft.Maui.Controls.Xaml | 10.0.110 | 0 | 0 | 0 | 0 |
| CommunityToolkit.Maui.Core | 15.0.1 | 48 | 38 | 10 | 0 |
| CommunityToolkit.Maui.MediaElement | 10.0.0 | 39 | 35 | 3 | 1 |
| CommunityToolkit.Maui | 15.0.1 | 27 | 27 | 0 | 0 |
| Syncfusion.Maui.Core | 34.2.9 | 668 | 659 | 9 | 0 |
| Syncfusion.Maui.Scheduler | 34.2.9 | 253 | 113 | 137 | 3 |
| Syncfusion.Maui.Inputs | 34.2.9 | 33 | 30 | 3 | 0 |
| Syncfusion.Maui.DataGrid | 34.2.9 | 24 | 22 | 2 | 0 |
| Syncfusion.Maui.Popup | 34.2.9 | 23 | 22 | 1 | 0 |
| Syncfusion.Maui.PullToRefresh | 34.2.8 | 14 | 14 | 0 | 0 |
| Syncfusion.Maui.ListView | 34.2.9 | 13 | 12 | 1 | 0 |
| Syncfusion.Maui.TabView | 34.2.9 | 13 | 13 | 0 | 0 |
| Syncfusion.Maui.Buttons | 34.2.9 | 10 | 9 | 1 | 0 |
| Syncfusion.Maui.NavigationDrawer | 34.2.9 | 10 | 10 | 0 | 0 |
| Syncfusion.Maui.Toolbar | 34.2.9 | 10 | 10 | 0 | 0 |
| Syncfusion.Maui.TreeView | 34.2.9 | 9 | 9 | 0 | 0 |
| Syncfusion.Maui.PdfToImageConverter | 34.2.9 | 7 | 7 | 0 | 0 |
| Syncfusion.Maui.Carousel | 34.2.9 | 4 | 4 | 0 | 0 |
| Syncfusion.Maui.Calendar | 34.2.8 | 3 | 3 | 0 | 0 |
| Syncfusion.Maui.ImageEditor | 34.2.9 | 3 | 2 | 1 | 0 |
| Syncfusion.Maui.Picker | 34.2.9 | 2 | 2 | 0 | 0 |
| Syncfusion.Maui.Charts | 34.2.9 | 1 | 1 | 0 | 0 |
| Syncfusion.Maui.Sliders | 34.2.9 | 1 | 0 | 1 | 0 |
| Syncfusion.Maui.Cards | 34.2.9 | 0 | 0 | 0 | 0 |
| Syncfusion.Maui.Data | 34.2.9 | 0 | 0 | 0 | 0 |
| Syncfusion.Maui.DataSource | 34.2.9 | 0 | 0 | 0 | 0 |
| Syncfusion.Maui.GridCommon | 34.2.9 | 0 | 0 | 0 | 0 |
| Syncfusion.Maui.Rotator | 34.2.9 | 0 | 0 | 0 | 0 |
| Syncfusion.Maui.SignaturePad | 34.2.9 | 0 | 0 | 0 | 0 |
| **Total** | | **2313** | **1998** | **311** | **4** |

Counts are per method, so a single gap can be many findings. The Syncfusion Scheduler's 75 open findings, for example, are one desktop feature (the horizontal resource view).

## Open gaps, most impactful first

### Microsoft.Maui.Essentials

No open findings. Where Linux still differs from Windows:

- **Contacts** come from Evolution Data Server, where GNOME Contacts, Evolution and GNOME Online Accounts keep them. A desktop without it (KDE keeps contacts in Akonadi) throws `FeatureNotSupportedException`. A sandbox that blocks it throws `PermissionException`.
- **Camera capture** shows OpenMaui's own capture dialog over the app window, where Windows opens its Camera app. Videos are MP4 (H.264 and AAC) when GStreamer has those encoders, and WebM otherwise. A sandboxed app reaches the camera through the xdg-desktop-portal Camera interface.
- **Motion and environment sensors** read the kernel's IIO devices, so they are supported only on machines with the hardware: convertibles, tablets and phones.
- **Geocoding** works only after the app names a Nominatim service (`GeocodingService.ServiceUrl` or `OPENMAUI_GEOCODING_URL`). Windows needs a Bing Maps key in the same way.

### Microsoft.Maui.Core / Controls

- **HybridWebView:** `WebResourceRequested` is raised for requests to the app origin (`app://0.0.0.1/`) only, as on iOS and Mac Catalyst. WebKit gives an embedder no way to answer http and https requests, which WebView2 on Windows intercepts too. (MAUI 10.0.110's `WebView` has no `WebResourceRequested`; the `PlatformWebViewWebResourceRequestedEventArgs` findings belong to HybridWebView.)
- **Items views:** CollectionView's `IsGrouped`, `CanReorderItems`, `ItemsUpdatingScrollMode` and horizontal grids, and CarouselView's `EmptyView`, scroll bars, `Loop` and vertical layout, are covered. What is left:
  - A reorder drag starts as soon as the pointer moves, by mouse or touch. On Windows a touch drag needs a press-and-hold first; OpenMaui's pointer events do not say which device sent them.
  - CarouselView ignores `IsBounceEnabled` and the snap points of its `ItemsLayout`, and uses the leading side of `PeekAreaInsets` on both sides. A looping carousel of two items with a peek shows one neighbour.
  - The classic ListView still rebuilds its rows and returns to the top on every change of its items.
- **Window.TitleBar without client-side decorations:** with client-side decorations (Wayland, when the compositor asks for them) the TitleBar fills the decoration's title bar, its leading, main and trailing content take presses and the rest of it moves the window, as on Windows. Under X11, or when the compositor draws the decorations, the system title bar stays and the TitleBar is shown as a strip at the top of the window's client area; dragging that strip does not move the window.
- Most of these gaps are also listed in `tests/Hosting/MapperParityTests.cs` (`KnownGaps`).

### CommunityToolkit.Maui

- **MediaElement:** `UriMediaSource.HttpHeaders` are sent with the request for the source URI (a media file, or an HLS or DASH manifest), but not with the segment requests of an adaptive stream. GStreamer's adaptive demuxers fetch segments with HTTP sources of their own, which playbin never exposes.
- **Platform differences that are not gaps** (they follow the toolkit's Windows build):
  - Toast and Snackbar are desktop notifications, so `SnackbarOptions` and `Anchor` do not apply.
  - `DrawingView.GetImageStream` ignores the requested size.
  - `SpeechToText` and `OfflineSpeechToText` throw `FeatureNotSupportedException`, because Linux desktops have no speech-recognition service. The toolkit does the same on an Android device that has no recognizer.

### Syncfusion

- **SfToolbar:** hover, tool tips on hover, the "more" menu's hover highlight, and items as tab stops that Enter activates are bridged (`Syncfusion/SfToolbarPatches.cs`). What is left: the navigation and "more" buttons are tab stops but draw no focus rectangle, where Windows shows its system focus visual on them.
- **SfComboBox and SfAutocomplete:** measure as on Windows (`Syncfusion/SfDropdownEntryPatches.cs`). With single selection, an unconstrained width or height is measured as 0, and the box is never shorter than 32 (the Windows `SfDropdownEntry` sets `MinimumHeightRequest`). So in a vertical stack it fills the width and is 32 high, while a horizontal stack, an Auto grid column or anything else that offers unlimited width gives it no width unless it has a `WidthRequest`, as on Windows. Inside a FlexLayout item it is clamped to that item's current size.
- **SfScheduler:** the AI assist button of a smart scheduler's header (`SfSmartScheduler`) is desktop-only (Windows and Mac). The neutral SmartScheduler build also leaves the button's wiring empty, and the package is not restored by `tests/Compat`, so it is not bridged. An app can open the assist view with `SfSmartScheduler.OpenAssistView()`. The desktop horizontal resource view is bridged (`Syncfusion/SfSchedulerResourceView*.cs`): resources side by side in the day, week, work-week and month views, with the resource header, time ruler, all-day expander and the Windows build's render virtualization. Drags between the time slots and the all-day panel, in both directions, work too, because a layout that takes the press keeps the pointer.
- **Not reported by the scan:** some Windows-only differences sit inside methods both builds have, so the scan does not see them. These are known and not bridged:
  - SfToolbar: the overlay toolbar's width and the navigation buttons' state after a measure (`SfToolbar.MeasureContent`, `SfOverlayToolbar`), and the tool tip offset in right-to-left layouts.
  - SfComboBox and SfAutocomplete with multiple selection: the chip area's size requests in `SfDropdownEntry.MeasureContent`. Also the rounded clip the Windows build sets in `OnDraw`.
  - SfPdfViewer is not part of the scan or the compat tests. Its stamp annotations' base view (`StampView`, in Syncfusion.Maui.Core) is bridged and tested.

## Limits of the scan

- It compares against the Windows build only. Behaviour that Android or iOS implement but Windows does not is not reported.
- It recognises a stub only by its shape: empty, a constant or `default`, or a short straight-line `throw`. A neutral body that returns a placeholder object (for example `ValueTask.FromResult(Stream.Null)`) is not reported as a stub. For that reason the CommunityToolkit `DrawingViewService` gap (now covered) was found by review, and is recorded under its windows-only members.
- Nor does it see a body the neutral build strips down to discards. SfDataGrid's keyboard navigation (`VisualContainer.OnKeyDown`), right-click (`DataGridCell.OnRightTap`), tool tip delay and row-header long press, and SfImageEditor's `GetImageStream`, `Save` and effects, were found by comparing the decompiled builds method by method, and are bridged (`Syncfusion/SfDataGridPatches.cs`, `Syncfusion/SfImageEditorPatches.cs`).
- Windows-only methods are matched by name, so an overload that exists only on Windows is not reported when the neutral build has another overload with the same name.
- Source-generated binding interceptors (`Microsoft.Maui.Controls.Generated.*`) are skipped. Their names carry per-build hashes, and they contain no platform code.
