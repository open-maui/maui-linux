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
  -> WPEBufferDMABuf fds/strides/modifier       eglCreateImage(EGL_LINUX_DMA_BUF_EXT) on the PLATFORM's
                                                EGL display -> GL texture -> SKImage (GPU target, zero copy)
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
  `EGLImage` and wrap it as a GL texture / `SKImage` (done; see "Zero-copy frames" below).
  `wpe_buffer_import_to_egl_image` is not usable for this: it creates the image on WPE's
  EGL display, not the render target's.
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

## Zero-copy frames on the GPU render target

`Rendering/DmaBufFrameImporter.cs`, driven from `WpeWebView.OnDraw` when the canvas has a
`GRContext` (the EGL render target is current):

- The EGLImage is created on the display that is current while the render target draws
  (`eglGetCurrentDisplay`), never on WPE's own display, and bound with
  `glEGLImageTargetTexture2DOES` (`GL_TEXTURE_2D`, `GL_TEXTURE_EXTERNAL_OES` fallback). Skia gets a
  borrowed `GRBackendTexture` (`SKImage.FromTexture`, top-left origin, RGBA8; AR24/AB24 premul,
  XR24/XB24 opaque) and `GRContext.ResetContext()` after every raw GL call.
- Prerequisites, probed once per EGL display: `EGL_EXT_image_dma_buf_import`
  (`_modifiers` for explicit non-linear modifiers), `GL_OES_EGL_image`, and the same GPU as
  WPE's render node (EGL_EXT_device_query node vs `WpeWebView.RenderNode`, compared through
  `/sys/class/drm/<node>/device`). Any miss, a raster target, or
  `OPENMAUI_WEBVIEW_ZEROCOPY=0` keeps the pixel copy; the chosen path is logged once.
- Until the first draw shows which target the view is on, a frame is copied AND held, so the
  raster target behaves exactly as before and the GPU target can still import it.
- Lifetime: one buffer (the one on screen) is held with its EGLImage and texture until the
  next frame is imported; a frame that arrives while an undrawn one is pending sends the older
  one straight back. GL objects are deleted in the context that made them (made current
  surfaceless when another window's context is current, skipped when that render target is gone).
- Sync: WebKit's rendering fence (none on Fedora 44 / Mesa iris, but handled) becomes a
  GPU-side `eglWaitSync`. When a sampled buffer goes back, an `EGL_ANDROID_native_fence_sync`
  fence covering all GL work issued so far is attached with `wpe_buffer_set_release_fence`;
  without that extension `glFinish` runs before `wpe_view_buffer_released`.
- Measured with `WebViewHost frames-delivered-gpu` (1280x800, Mesa Intel UHD Graphics CML GT2): frame
  import 0.08 ms and draw+flush 1.26 ms per frame zero-copy, against 3.8 ms import and
  2.5 ms draw+flush (texture upload) with the copy path.

## Keyboard: hardware keycodes

`KeyEventArgs.HardwareKeycode` carries the XKB keycode (Wayland `wl_keyboard.key` evdev + 8, X11
and GDK keycodes as-is). The WebView passes it to `wpe_event_keyboard_new`, which is where
WebKit gets DOM `KeyboardEvent.code`. Printable keys reach WebKit through the text-input
event; `WpeKeycodeTracker` gives that event the keycode of the key press that produced it
(single character only, so IME commits keep code 0).

## Distribution

- Debian sid and testing (forky) 2.54.0, Debian 13 (trixie) 2.48.3. Binary: `libwpewebkit-2.0-1`. Ubuntu has published no wpewebkit package since 22.04 (jammy had only the 1.0 API, 2.36), so Ubuntu users get the WebKitGTK fallback. Checked against packages.debian.org and packages.ubuntu.com on 2026-09-26.
- Fedora: not in the official repositories. `dnf copr enable philn/wpewebkit` provides
  2.54.0 for Fedora 43/44 on x86_64 and aarch64 (maintained by an Igalia WPE developer).
- Backend selection at runtime: WPE when `libWPEWebKit-2.0.so.1` loads, otherwise the
  existing WebKitGTK path.

## Reproduce

```bash
gcc -O1 -o probe wpe-headless-probe.c $(pkg-config --cflags --libs wpe-webkit-2.0 wpe-platform-2.0)
WPE_DRM_DEVICE=/dev/dri/renderD128 ./probe frame.ppm
```
