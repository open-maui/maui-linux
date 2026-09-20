# WPE WebKit 2.54 embedding spike (2026-09-20)

Goal: confirm that OpenMaui can obtain rendered web frames from WPE WebKit without
subclassing GObject types from managed code, so the Phase 2 WebView can be an
ordinary `SkiaView` that draws web content as an image on X11 and Wayland alike.

Result: confirmed on Fedora 44 with `wpewebkit` 2.54.0 from the `philn/wpewebkit` COPR.
`wpe-headless-probe.c` (this directory) renders an HTML page headlessly and writes the
first frame to a PPM; the frame is pixel-correct (text antialiased, CSS colours exact).

## The path that works

```text
wpe_display_headless_new()                      built-in headless platform, no window
  -> wpe_display_connect()
WebKitWebView (g_object_new, "display", ...)    view is a WPEViewHeadless (final type)
  -> wpe_view_resized(view, w, h)
g_signal_connect(view, "buffer-rendered")       signal (WPEView*, WPEBuffer*)
  -> wpe_buffer_import_to_pixels(buffer)        GBytes, 4 bytes/px BGRA  -> SKBitmap (raster target)
  -> wpe_buffer_import_to_egl_image(buffer)     EGLImage                 -> SKImage  (GPU target, Phase 1)
  -> wpe_view_buffer_released(view, buffer)     REQUIRED after use, else no further frames
```

Signals present in `libWPEWebKit-2.0.so.1`: `buffer-rendered`, `buffer-released`,
`buffers-changed`, `resized`. `render_buffer` is a class vfunc on `WPEViewClass`; the
headless view implements it and re-emits frames via `buffer-rendered`, which is what
removes the need to register a GObject subclass from C#.

Input goes in through `wpe_view_event(view, WPEEvent*)` (constructors in `WPEEvent.h`:
pointer, keyboard, scroll, touch), `wpe_view_focus_in/out`, `wpe_view_resized`.

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
- Release fences exist (`wpe_buffer_get/take_release_fence`); on the GPU path, signal the
  release fence after the draw that samples the texture completes.
- No GIR files ship in the COPR build; bind from the headers (`/usr/include/wpe-webkit-2.0`).
  pkg-config: `wpe-webkit-2.0`, `wpe-platform-2.0` (both 2.54.0). Runtime library:
  `libWPEWebKit-2.0.so.1`.

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
