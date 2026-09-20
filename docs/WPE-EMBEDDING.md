# WPE WebKit embedding reference

How OpenMaui embeds WPE WebKit 2.54+ (WPEPlatform API) as a composited `SkiaView`
(`Views/WpeWebView.cs`, `Native/WpeNative.cs`, `Native/WebKitContentApi.cs`), and
the facts that were established by probing rather than documentation. Started as
the 2026-09-20 spike; kept current with the shipped implementation. The C probes
in `docs/spikes/` reproduce each finding without the platform:

- `wpe-headless-probe.c` - headless display, `buffer-rendered`, pixel import
- `wpe-click-probe.c` - the pointer event sequence WebKit accepts as a click

Result: confirmed on Fedora 44 with `wpewebkit` 2.54.0 from the `philn/wpewebkit` COPR;
frames are pixel-correct (text antialiased, CSS colours exact).

## The path that works

```text
wpe_display_headless_new()                      built-in headless platform, no window
  -> wpe_display_connect()
WebKitWebView (g_object_new, "display", ...)    view is a WPEViewHeadless (final type)
  -> wpe_view_resized(view, w, h)
g_signal_connect(view, "buffer-rendered")       signal (WPEView*, WPEBuffer*)
  -> wpe_buffer_import_to_pixels(buffer)        GBytes (transfer NONE, owned by the buffer; do not unref), 4 bytes/px BGRA -> SKBitmap
  -> wpe_buffer_import_to_egl_image(buffer)     EGLImage                 -> SKImage  (GPU target, Phase 1)
  -> wpe_view_buffer_released(view, buffer)     REQUIRED after use, else no further frames
```

Signals present in `libWPEWebKit-2.0.so.1`: `buffer-rendered`, `buffer-released`,
`buffers-changed`, `resized`. `render_buffer` is a class vfunc on `WPEViewClass`; the
headless view implements it and re-emits frames via `buffer-rendered`, which is what
removes the need to register a GObject subclass from C#.

Input goes in through `wpe_view_event(view, WPEEvent*)` (constructors in `WPEEvent.h`:
pointer, keyboard, scroll, touch), `wpe_view_focus_in/out`, `wpe_view_resized`.
`wpe_event_pointer_button_new` asserts `pressCount == 0` for POINTER_UP (1 on DOWN); a
violated assertion returns NULL and the release is silently dropped, so links never click.
No ENTER/MOVE is required before a DOWN/UP pair (verified with `wpe-click-probe.c`).

## Facts that shape the implementation

- Buffers arrive as **DMA-BUF** on this machine (GPU-rendered by WebKit); SHM is the
  fallback WPE picks when no usable DRM device exists. Treat both: `WPE_IS_BUFFER_DMA_BUF`
  vs `WPE_IS_BUFFER_SHM`.
- With more than one GPU, WPE infers `/dev/dri/card0` and warns; `gbm_bo_map` (used by
  `import_to_pixels`) then fails on the wrong device. `WPE_DRM_DEVICE=/dev/dri/renderD128`
  fixed it. The platform must pick the device itself via
  `wpe_display_headless_new_for_device(name)`: prefer the render node of the GPU the
  compositor uses, else the first `renderD*`.
- On the GPU render target, do not read pixels back at all: import the DMA-BUF as an
  `EGLImage` (`wpe_buffer_import_to_egl_image`) and wrap it as a GL texture / `SKImage`.
- A static page renders exactly once; there is no steady frame stream. Repaints happen on
  content change, resize (`wpe_view_resized`) or input. The Skia view must keep the last
  frame and invalidate only on `buffer-rendered`.
- `wpe_view_buffer_released` must be called for every buffer, including on import failure,
  or the view stalls after the first frame.
- The `GBytes` from `wpe_buffer_import_to_pixels` belongs to the buffer (cached across frames):
  unreffing it triggers `g_atomic_ref_count_dec` assertions and a later copy from freed memory.
- Release fences exist (`wpe_buffer_get/take_release_fence`); on the GPU path, signal the
  release fence after the draw that samples the texture completes.
- No GIR files ship in the COPR build; bind from the headers (`/usr/include/wpe-webkit-2.0`).
  pkg-config: `wpe-webkit-2.0`, `wpe-platform-2.0` (both 2.54.0). Runtime library:
  `libWPEWebKit-2.0.so.1`.

## Facts established while shipping the view

- Pointer: `wpe_event_pointer_button_new` requires `pressCount == 0` on POINTER_UP and
  a real click count (1, 2, 3 within 400 ms / 5 px) on POINTER_DOWN; without the count,
  double-click word selection never happens.
- Scale: size the view in CSS pixels and call `wpe_toplevel_scale_changed(toplevel, scale)`
  on the toplevel the headless view already owns; the buffer comes back at logical x scale
  and event coordinates are logical. Passing physical pixels lays the page out too small.
- Clipboard: the headless platform's `WPEClipboard` is in-process; the view syncs it with
  the system clipboard (change-count pull after Copy/Cut, push on focus/right-click/Ctrl+V).
- UI the embedder must provide (WPE has none): context menus (`context-menu` gives the
  model; items activate via their `GAction`), script dialogs (`script-dialog`, answered
  asynchronously with `webkit_script_dialog_ref`/`close`/`unref`), file chooser
  (`run-file-chooser`), permission requests (`permission-request`, type-checked with
  `g_type_check_instance_is_a`), web notifications (`show-notification`), downloads
  (`download-started` on the network session, then `decide-destination`/`finished`/`failed`).
- Cursor: WebKit reports the pointer shape only through the `set_cursor_from_name` class
  vfunc (offset 168 in the 2.54 `WPEViewClass`); the slot is patched once per process and
  mapped onto the platform cursor.
- Spell checking is off by default: `webkit_web_context_set_spell_checking_enabled` plus
  the locale's languages; needs enchant and a dictionary on the host.
- Custom schemes: `app://0.0.0.0/` is refused ("Not allowed to use restricted network port");
  `app://localhost/` works. Schemes are per context, so requests are routed by
  `webkit_uri_scheme_request_get_web_view`.
- Blazor: `blazor.webview.js` is a static web asset (no pipeline on Linux; embedded from the
  package via `GeneratePathProperty`), `autostart="false"` host pages need `Blazor.start()`
  from the bridge script, and a missing `_framework/blazor.modules.json` must be served as `[]`.

## Distribution

- Debian sid 2.54.0, testing 2.52.6, stable 2.48; Ubuntu inherits. Binary: `libwpewebkit-2.0-1`.
- Fedora: not in the official repositories. `dnf copr enable philn/wpewebkit` provides
  2.54.0 for Fedora 43/44 on x86_64 and aarch64 (maintained by an Igalia WPE developer).
- Backend selection at runtime: WPE when `libWPEWebKit-2.0.so.1` loads, otherwise the
  existing WebKitGTK path.

## Reproduce

```bash
gcc -O1 -o probe wpe-headless-probe.c $(pkg-config --cflags --libs wpe-webkit-2.0 wpe-platform-2.0)
WPE_DRM_DEVICE=/dev/dri/renderD128 ./probe frame.ppm
```
