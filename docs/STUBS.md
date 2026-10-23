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

## Scan of 2026-10-23

Package versions are the ones `tests/Compat` restores:

| Package | Version | Findings | covered | n/a | open |
|---|---|---:|---:|---:|---:|
| Microsoft.Maui.Core | 10.0.120 | 586 | 507 | 79 | 0 |
| Microsoft.Maui.Controls.Core | 10.0.120 | 345 | 302 | 43 | 0 |
| Microsoft.Maui.Essentials | 10.0.120 | 208 | 186 | 22 | 0 |
| Microsoft.Maui.Controls.Xaml | 10.0.120 | 0 | 0 | 0 | 0 |
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
| **Total** | | **2354** | **2037** | **313** | **4** |

Counts are per method, so a single gap can be many findings. The Syncfusion Scheduler's 75 open findings, for example, are one desktop feature (the horizontal resource view).

## Open gaps, most impactful first

### Microsoft.Maui.Essentials

No open findings. Where Linux still differs from Windows:

- **Contacts** come from Evolution Data Server (GNOME Contacts, Evolution, GNOME Online Accounts) and from KDE's file-based contact stores: Akonadi's Personal Contacts folder (`~/.local/share/contacts`), vCard folder and vCard file resources, and KPeople's vCard folder (KDE Connect). Contacts Akonadi keeps only in its database (CardDAV, Google, EWS, Kolab) are not read, because Akonadi serves items over its own socket protocol, not D-Bus. A desktop with neither throws `FeatureNotSupportedException`. A sandbox that blocks Evolution Data Server throws `PermissionException` unless the app can read the KDE folders (a Flatpak needs read access to `~/.config/akonadi/agentsrc`, the resources' `~/.config/akonadi_*_resource_*rc` files and their folders, e.g. `--filesystem=xdg-config/akonadi:ro --filesystem=xdg-data/contacts:ro`; the rc files are only needed for a resource whose path is not the default). Phone and e-mail types are read but not carried, since MAUI's `ContactPhone` and `ContactEmail` have no type.
- **Camera capture** shows OpenMaui's own capture dialog over the app window, where Windows opens its Camera app. Videos are MP4 (H.264 and AAC) when GStreamer has those encoders, and WebM otherwise. A sandboxed app reaches the camera through the xdg-desktop-portal Camera interface.
- **Motion and environment sensors** read the kernel's IIO devices, so they are supported only on machines with the hardware: convertibles, tablets and phones.
- **Geocoding** works only after the app names a Nominatim service (`GeocodingService.ServiceUrl` or `OPENMAUI_GEOCODING_URL`). Windows needs a Bing Maps key in the same way.

### Microsoft.Maui.Core / Controls

- **HybridWebView:** `WebResourceRequested` is raised for requests to the app origin (`app://0.0.0.1/`), as on iOS and Mac Catalyst. It is also raised for http and https requests, as on Windows, when the app handles the event at the time the handler creates the view; see `docs/WPE-EMBEDDING.md`, "Request interception". What is left:
  - A handler added after the view was created gets app-origin requests only. WebKit fixes a view's network when the view is created.
  - When a server's TLS certificate does not verify, the load fails as a network error. WebKit's `load-failed-with-tls-errors` is not raised for it, so the app cannot allow that certificate.
  - Requests the app leaves alone use the proxy settings from the `http_proxy`/`https_proxy`/`no_proxy` environment variables (.NET's default). They do not use the desktop's (GNOME) proxy settings. WebSocket tunnels connect directly.
  - (MAUI 10.0.120's `WebView` has no `WebResourceRequested`; the `PlatformWebViewWebResourceRequestedEventArgs` findings belong to HybridWebView.)
- **Items views:** CollectionView's `IsGrouped`, `CanReorderItems` (a touch drag waits for a press-and-hold, as on Windows), `ItemsUpdatingScrollMode` and horizontal grids; CarouselView's `EmptyView`, scroll bars, `Loop`, vertical layout, `IsBounceEnabled`, snap points and per-side `PeekAreaInsets`; and ListView's in-place updates are covered. What is left:
  - The touch press-and-hold works only where the backend reports touch as touch. The Wayland backend does (`wl_touch`), and so does the X11 backend when the X server has XInput 2.2 (it selects XI2 touch events; an older server, or `OPENMAUI_X11_NO_XI2=1`, gives emulated mouse events again). The GTK host window passes no source device, so under GTK a touch reorder still starts as soon as the finger moves.
  - A touch reorder starts on the first move after the hold. Windows also lifts the item the moment the hold ends, before any move.
- **Window.TitleBar when the compositor keeps its decorations:** a window with a TitleBar draws its own decorations (X11, and Wayland when the compositor agrees to client-side mode), with the TitleBar in the title bar as on Windows. A Wayland compositor that answers `server_side` keeps its own title bar, and the TitleBar is then a strip at the top of the client area; dragging that strip does not move the window. The GTK host mode (`UseGtk`) also keeps the strip. No drop shadow is drawn around a client-decorated window, so `_GTK_FRAME_EXTENTS` is not set on X11.
- Most of these gaps are also listed in `tests/Hosting/MapperParityTests.cs` (`KnownGaps`).

### CommunityToolkit.Maui

- **MediaElement:** `UriMediaSource.HttpHeaders` are sent with the request for the source URI and with the segment requests of HLS and DASH streams (including DASH init segments). They are not sent with the request for an HLS master playlist's media playlist, or its reloads for a live stream. GStreamer 1.28's `hlsdemux` fetches it with an internal downloader that no signal exposes, and it sends a `Referer` header but not the app's headers. The headers go only to the media source's host and port; a segment on another host (a CDN) is fetched without them.
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
