# MediaElement on Linux

`OpenMaui.Controls.Linux.MediaElement` backs `CommunityToolkit.Maui.MediaElement` with a GStreamer `playbin` whose video sink is an `appsink`; `SkiaMediaElement` draws each frame inside the Skia render tree. This page covers how the toolkit's events and properties map to the pipeline, how frames reach the screen and how seeking behaves.

## Events and properties

The handler drives the pipeline the way the toolkit's Windows `MediaManager` drives `MediaPlayerElement`. A thread follows the pipeline's bus for its whole life and hands errors, the end of the stream, state changes and buffering to the UI thread.

| Toolkit member | On Linux |
|---|---|
| `MediaOpened`, `Duration` | Setting `Source` opens the media at once: the pipeline prerolls to PAUSED whether or not it plays. `MediaOpened` is raised once per source, after `Duration` is set |
| `MediaFailed` | Raised with GStreamer's error message (a missing file, a codec without a plugin, an HTTP error). The toolkit then sets `CurrentState` to `Failed` |
| `MediaEnded` | Raised at the end unless `ShouldLoopPlayback` is set. Playback holds paused at the end, and `Play` starts again from the beginning |
| `CurrentState`, `StateChanged` | `Opening` when a source is set, `Buffering` while a network source fills its buffer, then `Playing` or `Paused`. `Stopped` comes from `Stop` and from the end, and `None` from clearing `Source` |
| `Position`, `PositionChanged` | Updated about four times a second while `Playing`, `Paused` or `Stopped` |
| `SeekCompleted` | Raised when the last requested seek has landed |
| `MediaWidth`, `MediaHeight` | The decoded video size (0 x 0 for audio only) |
| `Speed` | The playback rate. Audio keeps its pitch through `scaletempo`. Setting `Speed` to 0 pauses; playing again restores rate 1, and `Speed` follows, as on Windows |
| `UriMediaSource.HttpHeaders` | Set on the HTTP source as extra request headers. `User-Agent` goes to the source's `user-agent` property. They are not sent with the segment requests of HLS or DASH streams, which GStreamer's adaptive demuxers make with HTTP sources of their own |
| `ShouldKeepScreenOn` | Inhibits the screen saver while playing: the desktop portal's `Inhibit` (idle) first, then `org.freedesktop.ScreenSaver.Inhibit`. Released on `Pause`, `Stop`, the end, and when the handler disconnects |
| `ShouldShowPlaybackControls` | A controls bar drawn over the bottom of the video: elapsed and remaining time, a seek bar, mute, play/pause, playback rate (0.25, 0.5, Normal, 1.5, 2), repeat and zoom (fit or fill). As with the WinUI transport controls, it shows while the media is not playing and, while it plays, for three seconds after the pointer last moved over the video. Presses on the bar do not reach gestures behind the video. Off by default, as in the toolkit |
| `MetadataTitle`, `MetadataArtist`, `MetadataArtworkUrl` | Published over MPRIS (`org.mpris.MediaPlayer2` on the session bus). The desktop then shows the player in its media widget and routes the media keys to it: play, pause, play/pause and stop, plus seeking, volume, rate and repeat. One player is published per process: the element that last opened or played media with metadata. An element without any metadata, such as a muted background video, is never published. Set `OPENMAUI_MPRIS=0` to turn this off |
| `DisconnectHandler` | Tears the pipeline down, releases the screen-saver inhibition and withdraws the MPRIS player |

Not done yet: the controls bar has no fast-forward, rewind or full-window buttons, the artwork is not shown as a poster before playback, and `StreamMediaSource` is not supported.

## Frame paths

| Path | When | Per frame |
|------|------|-----------|
| Zero-copy | GPU (EGL) render target, a decoder that exports DMA-BUF (VA-API `va*dec`, V4L2), GStreamer 1.24+ | The decoder's output surface is imported as an EGLImage texture and drawn by Skia. No colour conversion on the CPU, no copy, no upload |
| Pixel copy | Raster target, software or NVDEC decoders, import failure, `OPENMAUI_VIDEO_ZEROCOPY=0` | `videoconvert` to BGRA, a copy into managed memory, an `SKImage` copy on the UI thread, and a texture upload on the GPU target |

The first path used is logged once: `[SkiaMediaElement] Video frames: zero-copy (DMA-BUF 1920x1080 NV12 modifier 0x100000000000002 planes 2 -> EGLImage external texture on /dev/dri/renderD128)` or `pixel copy (<reason>)`.

### How zero-copy negotiates

- The appsink caps are `video/x-raw(memory:DMABuf),format=DMA_DRM,drm-format={...}; video/x-raw,format=BGRA`. The `drm-format` list holds every NV12, P010 and 8-bit RGB format/modifier pair the window's EGL display reports through `eglQueryDmaBufModifiersEXT`, so the decoder only chooses a layout EGL can import (on Mesa Intel: linear, X-tiled and Y-tiled). When the query is unavailable the list is left open and the import validates the buffer instead. The BGRA alternative keeps every other decoder on the pixel-copy path through playsink's converter.
- A pad probe on the appsink adds `GstVideoMeta` to the allocation query. The appsink does not do this itself, and VA decoders refuse DMA-BUF caps without it ("DMABuf caps negotiated without the mandatory support of VideoMeta").
- For each sample, the plane offsets and strides come from `GstVideoMeta`. Each plane's fd comes from `gst_buffer_find_memory` plus `gst_dmabuf_memory_get_fd`, with the memory's own offset added. The colour matrix and range come from the caps `colorimetry`, which becomes the `EGL_YUV_COLOR_SPACE_HINT_EXT` and `EGL_SAMPLE_RANGE_HINT_EXT` hints.
- Before GStreamer 1.24 (no `DMA_DRM`), legacy `memory:DMABuf` caps with a plain pixel format and an implicit modifier are offered.

### Why NV12 is imported as one external texture

YUV buffers are imported as a single multi-plane EGLImage (`DRM_FORMAT_NV12`, two planes with the colour hints) and bound to `GL_TEXTURE_EXTERNAL_OES`. The driver's sampler does the YUV to RGB conversion, and Skia draws the texture like any other read-only texture: `GRGlTextureInfo` with the external target, drawn with linear filtering. The other option was to import each plane as an R8 or GR88 texture and convert with an `SKRuntimeEffect`. That needs two imports per frame, a shader to maintain with the matrix and range maths, and a Skia texture-to-shader binding. The EGL route works on Mesa Intel (iris, UHD 630 here) with the VA-API Y-tiled NV12 surfaces: colours match the `videoconvert` path within frame-to-frame noise. It is also the route every Wayland compositor uses for video planes. A driver without `GL_OES_EGL_image_external` reports a clear reason and falls back to the pixel-copy path.

The import code is shared with the WebView: `Rendering/DmaBufTextureImporter.cs` holds the EGLImage, texture and owning context, and deletes them in the right context. `DmaBufFrameImporter` (WPE buffers) and `GstDmaBufFrame` (GStreamer samples) adapt their producers to it.

### Lifetime and synchronisation

- The element keeps the `GstSample` referenced while its texture may be sampled, so the buffer stays out of the decoder's pool.
- When a newer frame is imported, the previous sample gets an EGL fence (`EGL_SYNC_FENCE`) covering every GL command issued so far. It goes back to GStreamer once the fence signals. The queue is polled on every draw and waits (100 ms at most) only when more than three samples are pending. Without fence syncs, `glFinish` runs before the release, as on the WebView path.
- Decode completion before sampling relies on the kernel's implicit DMA-BUF synchronisation between the VA driver and Mesa.

### Fallbacks

- `OPENMAUI_VIDEO_ZEROCOPY=0`: DMA-BUF is never offered. `OPENMAUI_VIDEO_ZEROCOPY=force` offers it even when the render target is not known to be GPU (for testing the fallback).
- Raster render target: DMA-BUF is not offered. A raster *snapshot* of a view that normally draws on the GPU target (such as `Screenshot.CaptureAsync`) reads the current texture back for that one draw and stays on the zero-copy path.
- Other failures (import rejected, different GPU, missing extension, a DMA-BUF sample without `GstVideoMeta`): the element leaves the zero-copy path for good. It rebuilds its pipeline with BGRA-only caps at the same position and play state. Renegotiating the live pipeline does not work here: after a reconfigure, playsink's `videoconvert` still advertises DMA-BUF caps upstream, so the VA decoder picks them again and negotiation fails (GStreamer 1.28, `vavp9dec`).

### Measured (MediaDemo, 1400x1050 at 1.75x, egl-wayland, Mesa Intel UHD 630, GStreamer 1.28.5)

Big Buck Bunny 1080p30 VP9 (`vavp9dec`, NV12 Y-tiled), five alternating 25 s runs per path on a machine with other load:

| | Zero-copy | Pixel copy (`OPENMAUI_VIDEO_ZEROCOPY=0`) |
|---|---|---|
| Frame time (render stats avg of runs; noisy under the machine's other load) | 1.3-2.3 ms, mean 1.8 ms | 2.4-3.7 ms, mean 3.0 ms |
| UI-thread frame install | 0.09-0.12 ms (EGLImage import) | 1.1-1.2 ms (`SKImage` copy of 8 MB) |
| Process CPU over 25 s (user+sys) | 3.9-6.5 s, mean 5.4 s | 13.8-15.6 s, mean 14.7 s |

The H.264 720p clip MediaDemo loads by default decodes on NVDEC on this machine. NVDEC outputs CUDA or system memory, not DMA-BUF, so the clip stays on the pixel-copy path with the same numbers as before (1.4-2.0 ms frame time, as in the Phase 1 baseline of 1.47 ms). Fedora's `gstreamer1-plugins-bad-free` has no `vah264dec`; with it installed (freeworld), H.264 takes the zero-copy path the same way.

## Seeking

`SeekTo` issues `FLUSH | ACCURATE`: the demuxer starts at the keyframe before the target, and the decoder decodes and discards up to it. `KEY_UNIT` is deliberately absent. Combined with `ACCURATE` it wins, and every seek lands on the previous keyframe. With the test clip's keyframes at 0 s and 8.33 s, a seek to 7.3 s landed on 0.0 s, over HTTP and from a local file alike. This, not HTTP, was the "1-2 s backward-seek drift" on HTTP streams.

Measured with `souphttpsrc` (test-videos.co.uk, H.264 720p MP4 and VP9 1080p WebM), requested position against the position reported after the seek:

| Flags | Result |
|-------|--------|
| `FLUSH \| KEY_UNIT \| ACCURATE` (before) | 7.3 to 0.000, 2.4 to 0.000, 5.1 to 0.000, 8.8 to 8.333 |
| `FLUSH \| ACCURATE` (now) | exact for every target, forward and backward, HTTP and local |

Each accurate seek costs 50-470 ms on hardware decode (HTTP, 720p/1080p), mostly the decode and discard from the previous keyframe. On software H.264 decode (openh264) it can reach about 2 s for a target just before a distant keyframe. `souphttpsrc` is seekable (range requests), so no download buffering is needed for progressive HTTP.

Seeks run on a worker thread, one at a time, and the latest target wins. Each seek waits for its preroll before the next one is issued. There are two reasons:

- A flushing seek blocks its caller until the source's streaming thread lets go. On the UI thread, a slow range request froze the UI for the whole request.
- Back-to-back flushing seeks on `souphttpsrc` can leave a range request stalled indefinitely. Twelve seeks 50 ms apart on the HTTP VP9 stream hung the seeking thread in 3 of 5 runs. With the waits in between, 0 of 5 hung.

In the app, a scrub of twelve targets 50 ms apart issues about four seeks and settles on the last target exactly (9 of 9 rounds, paused and playing). While a seek is pending or prerolling, `Position` reports its target.

A residual difference remains for the H.264 MP4 only: after an accurate seek, the first frame *displayed* is up to two frames (67 ms at 30 fps) after the reported position. The same offset appears for local files, so it comes from that file's composition timestamps, not the network. The VP9 WebM shows the exact frame.
