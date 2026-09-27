# Vulkan render target

OpenMaui can present frames through Vulkan as a third `IRenderTarget`, next to
the EGL targets (`egl-wayland`, `egl-x11`) and the CPU raster target. It is
opt-in: `Auto` still picks EGL.

```
OPENMAUI_RENDERER=vulkan dotnet run            # or: options.Renderer = RendererPreference.Vulkan
```

The startup log names the target, device, driver, present mode and why the
device was chosen:

```
[LinuxApplication] Renderer: vulkan-wayland (Intel(R) UHD Graphics (CML GT2); Mesa 26.1.5; Vulkan 1.4.354; mailbox, 4 images; compositor's DRM device)
```

## Files

| File | Role |
| --- | --- |
| `Interop/Vulkan.cs` | P/Invoke to `libvulkan.so.1` (the Khronos loader): instance, device, WSI, sync, barriers. No Vulkan headers or SDK needed. |
| `Rendering/VulkanContext.cs` | Instance, surface, physical-device selection (`VulkanDeviceSelector`), logical device and queue, Skia `GRContext` from `GRContext.CreateVulkan`. Also usable headless (tests). |
| `Rendering/VulkanRenderTarget.cs` | Swapchain, per-image `SKSurface`s, frame protocol, resize/out-of-date handling. `vulkan-wayland`, `vulkan-x11`. |
| `Rendering/RenderTargetFactory.cs` | `RendererPreference.Vulkan`, `OPENMAUI_RENDERER=vulkan` / `vk`, fallback chain. |
| `tests/Rendering/VulkanRenderTargetTests.cs` | Selection and swapchain policy, plus real Vulkan tests: headless device + GRContext draw/readback, and a real swapchain on `VK_EXT_headless_surface` across resizes. The real tests return early with a message when there is no ICD. |

## Design

**One device per window.** Each window gets its own `VkInstance`, surface,
`VkDevice`, queue and `GRContext`, the same way each EGL window has its own EGL
display connection and context. The engine disposes the target before the
window closes its `wl_display` / X `Display*`, so the swapchain, device,
surface and instance are gone before the display connection closes.

**Surface.** Wayland: `VK_KHR_wayland_surface` on the window's `wl_surface`.
The factory switches the window to `ExternalPresentation` first, as it does for
EGL. That releases the window's `wl_shm` buffer, and the swapchain handles
attach, damage and commit from then on. The window keeps setting the
wp_viewporter destination, so the buffer stays at physical size and the
compositor sees the logical size. X11: `VK_KHR_xlib_surface` on the window's own
`Display*`.

**Queue.** One graphics queue family that can also present to the surface.
Devices with no such family are not candidates, which avoids ownership
transfers between queue families.

**Swapchain.**
- Format: `B8G8R8A8_UNORM`, then `R8G8B8A8_UNORM`, in sRGB non-linear, which is
  what the raster and EGL paths produce.
- Composite alpha: premultiplied where the surface supports it, matching
  ARGB8888 `wl_shm` and the ARGB EGL config. Otherwise opaque.
- Image count: min + 1, at least 3.
- Present mode: MAILBOX, then IMMEDIATE, then FIFO. `OPENMAUI_VULKAN_PRESENT_MODE`
  (`mailbox|immediate|fifo|fifo-relaxed`) overrides this when the surface
  supports the requested mode. This mirrors `eglSwapInterval(0)`. The run loop
  only renders dirty frames, and FIFO can withhold images indefinitely while a
  Wayland surface is hidden.
- `vkAcquireNextImageKHR` uses a 100 ms timeout. A timeout skips the frame and
  never blocks the UI thread.

**Resize and scale change.** `Resize` (fed from the window's `Resized`, which
also fires on a scale change) only marks the swapchain stale. It is rebuilt at
the next `BeginFrame`, outside any active frame. The rebuild does
`vkDeviceWaitIdle`, disposes the per-image `SKSurface`s, runs a synchronous Skia
flush so Skia releases its views and framebuffers of the old images, then
creates the new swapchain with `oldSwapchain` set. On Wayland the extent is the
requested physical size. On X11 it is the surface's `currentExtent`.

The frame is dropped and the swapchain rebuilt in these cases:
- `VK_ERROR_OUT_OF_DATE_KHR` from acquire: rebuild and retry once.
- `VK_SUBOPTIMAL_KHR` from acquire or present: the frame is used, then the
  swapchain is rebuilt before the next one.
- A zero extent (minimised window): no swapchain is built and frames are
  skipped.

**Frame protocol.** SkiaSharp 4.152 exposes `GRContext.CreateVulkan`,
`GRBackendRenderTarget(w, h, GRVkImageInfo)` and `Flush(submit, synchronous)`.
It does **not** expose Skia's flush-with-semaphores or mutable-state (layout)
APIs, so Skia cannot be handed the acquire and present semaphores. Instead,
each swapchain image gets two command buffers. Each holds a single image
barrier, is recorded once and uses `SIMULTANEOUS_USE`.

1. `BeginFrame`:
   1. Wait for the frame slot's fence. There are 2 frames in flight.
   2. Acquire an image. This signals the slot's acquire semaphore.
   3. Submit the image's *pre* buffer. It waits on that semaphore and
      transitions the image `UNDEFINED -> COLOR_ATTACHMENT_OPTIMAL`, so the old
      contents are discarded (`PreservesContents = false`).
   4. Return the canvas of the image's cached `SKSurface`. Skia wrapped that
      image with `ImageLayout = COLOR_ATTACHMENT_OPTIMAL`.
2. `EndFrame`:
   1. `GRContext.Flush(submit: true)` submits Skia's work.
   2. Submit the *post* buffer. It transitions
      `COLOR_ATTACHMENT_OPTIMAL -> PRESENT_SRC_KHR`, signals the image's
      render-done semaphore and signals the slot's fence.
   3. `vkQueuePresentKHR` waits on render-done.

This is correct because barriers, semaphore signals and fence signals order
against everything earlier in queue submission order. Skia's own submissions
land between *pre* and *post* on the same queue. Render-done semaphores are
per image, not per slot, so a semaphore is never reused while a present may
still be waiting on it.

The one invariant this depends on: Skia must end every frame with the image in
`COLOR_ATTACHMENT_OPTIMAL`, so that Skia's tracked layout and the real layout
agree on the next frame. That holds for two reasons:
- The swapchain images have no `INPUT_ATTACHMENT` usage, so Skia never uses
  `GENERAL`-layout dst reads.
- The engine's frame always ends with a draw, never a copy or readback. A
  dst-read copy inside the frame is followed by another render pass, which
  transitions the image back.

If SkiaSharp ever binds `GrFlushInfo` semaphores and `GrBackendSurfaceMutableState`,
Skia could do both transitions itself.

**Device features.** Only `dualSrcBlend` and `sampleRateShading` are enabled,
and only when supported. Everything else stays off; `robustBufferAccess`, for
example, costs performance for nothing here.

## Device selection

The policy is `VulkanDeviceSelector.Select`, which is pure and unit-tested. Only
devices at Vulkan 1.1 or later, with `VK_KHR_swapchain` and a graphics+present
queue for this window, are candidates.

1. **`OPENMAUI_VULKAN_DEVICE`** can be any of:
   - an enumeration index (`1`)
   - a type (`integrated`, `discrete`, `virtual`, `cpu`)
   - a DRM node (`renderD129`, `/dev/dri/card0`)
   - a case-insensitive substring of the name (`nvidia`)

   A value that matches nothing is noted in the reason, and selection continues.
2. **The compositor's device.** The target reads the primary node's
   `major:minor` of the GPU that sysfs marks `boot_vga` (the GPU driving the
   display; the iGPU on hybrid laptops). It matches that against
   `VK_EXT_physical_device_drm` (primary or render node), so buffers never cross
   GPUs.
3. **Integrated before discrete.** On hybrid laptops this leaves the dGPU asleep.
4. Then discrete, virtual and other. A CPU rasteriser (llvmpipe or lavapipe)
   comes last.

The compositor hint is a heuristic. The exact answer is the
`zwp_linux_dmabuf_v1` feedback `main_device`, which would need a protocol
binding. Mesa's `VK_LAYER_MESA_device_select` layer does that lookup already and
sorts the compositor's device first, and the override covers the rest.

On this machine (Intel CML GT2 + NVIDIA RTX 2060 Max-Q + llvmpipe, KDE Wayland)
the selector picks Intel with the reason "compositor's DRM device".
`OPENMAUI_VULKAN_DEVICE=discrete` selects the NVIDIA device, which also works:
the NVIDIA WSI does the PRIME copy to the Intel-driven display.

## Selection and fallback

`RenderTargetFactory.ResolvePreference`: `OPENMAUI_RENDERER=vulkan` or `vk`
returns `RendererPreference.Vulkan`. `LinuxApplicationOptions.Renderer =
RendererPreference.Vulkan` does the same. `Auto` never picks Vulkan for now.

When Vulkan is requested and anything fails, the factory falls back in this
order:
- **EGL**, if the loader, ICD, instance extension, surface, device, swapchain or
  `GRContext` fails.
- **Raster**, if EGL fails too.

The Vulkan failure is logged **once per process** as a warning (every window
would hit the same missing driver). Later windows log it at debug level. On
Wayland the window's `ExternalPresentation` is restored before EGL is tried.
Verified with `VK_ICD_FILENAMES=/nonexistent.json OPENMAUI_RENDERER=vulkan`:

```
[RenderTargetFactory] WARNING: Vulkan renderer requested but unavailable (Vulkan loader lacks VK_KHR_surface, VK_KHR_wayland_surface); falling back to EGL
[LinuxApplication] Renderer: egl-wayland (EGL 1.5 Mesa Project; Mesa Intel(R) UHD Graphics (CML GT2))
```

A request for Vulkan on a window with no native surface (tests, fakes) gets
the raster target.

## Numbers

Test setup: Fedora 44, KDE Plasma Wayland, Mesa 26.1.5 on an Intel UHD (CML GT2)
iGPU, 2240x1400 physical (1280x800 logical at 1.75x), `OPENMAUI_RENDER_STATS=1`.
Frame ms is the engine's region selection plus draw, flush and submit. The
scene has 400 animated anti-aliased rounded rects and 100 text runs, repainted
fully every frame. The harness is a throwaway windowed loop over
`RenderTargetFactory` and is not in the tree. The machine was shared with
parallel builds, so single samples are noisy; stable intervals are shown.

| Target | Paced (~55 fps) avg / p50 / p95 / p99 ms | Unthrottled fps (8 s) |
| --- | --- | --- |
| `vulkan-wayland` (mailbox) | 2.08-2.20 / 2.04-2.13 / 2.57-2.68 / 2.90-3.09 | 342-353 |
| `egl-wayland` (swap interval 0) | 2.13-2.18 / 1.89-1.94 / 3.04-3.07 / 3.26-3.44 | 230-324 |
| `vulkan-wayland`, `OPENMAUI_VULKAN_DEVICE=discrete` (NVIDIA, PRIME) | 2.01 / 1.95 / 2.59 / 2.91 (after GPU wake-up) | - |
| `vulkan-x11` (XWayland, mailbox) | 4.9-7.9 / 3.3-7.3 / 10-16 / 12-66 | - |
| `egl-x11` (XWayland) | 1.86-2.96 / 1.74-2.45 / 2.6-7.4 / 3.3-11 | - |

On native Wayland, Vulkan and EGL are at parity. Vulkan has a slightly tighter
p95/p99 and slightly higher unthrottled throughput, which is within the noise of
a shared machine. Both are GPU-bound on Skia's work, which is the same whichever
backend submits it. On XWayland, Mesa's X11 Vulkan WSI costs noticeably more
than EGL/DRI3, so prefer EGL there.

ShellDemo (`OPENMAUI_RENDERER=vulkan`) starts, renders correctly (checked with a
screenshot) and logs no errors. It is idle after startup, so it renders too few
frames for a 2 s `[RenderStats]` interval under either renderer. Hence the
animated harness above.

## Explicit DMA-BUF (linux-dmabuf) - not implemented, by design

The roadmap item also named "explicit DMA-BUF": submitting frames by allocating
buffers ourselves (GBM), exporting them as DMA-BUFs and attaching them through
`zwp_linux_dmabuf_v1`. That would add nothing measurable, for three reasons:
- **The drivers already do it.** Mesa's Wayland EGL platform and Mesa's Vulkan
  Wayland WSI both allocate the buffers as DMA-BUFs, negotiate format and
  modifiers through `zwp_linux_dmabuf_v1`, and attach them to our `wl_surface`.
  Where supported, they also use dmabuf feedback to choose scanout-capable
  modifiers. The NVIDIA driver's WSI does the same. Both GPU paths are already
  zero-copy DMA-BUF submission.
- **A hand-rolled path would duplicate all of that.** It would need GBM
  allocation, modifier negotiation, buffer release tracking, frame pacing and
  explicit-sync plumbing, all of which WSI does already. There would be no
  extra copy removed and no latency saved; it only adds places for driver
  differences to break.
- **The one real gap is solved elsewhere.** Explicit sync
  (`wp_linux_drm_syncobj_v1`) is handled inside Mesa and NVIDIA WSI when the
  compositor offers it.

The only place OpenMaui touches DMA-BUFs directly is the WPE WebView zero-copy
import. There DMA-BUFs *come in* from WebKit and are imported as EGLImages
(`Rendering/DmaBufFrameImporter.cs`). That is a different problem and is
already done. The roadmap item can close with Vulkan delivered and
linux-dmabuf submission declined.

## Limitations

- **Opt-in only.** `Auto` stays on EGL. There is no performance win on Wayland
  to justify the switch yet, and EGL has more mileage.
- **XWayland / X11** works but is slower than EGL (see Numbers).
- **WPE WebView zero-copy** needs Skia's GL backend. On a Vulkan target the
  WebView takes the pixel-copy path, and logs the render target's Skia backend
  as the reason. Importing WebKit's DMA-BUFs as `VkImage`s
  (`VK_EXT_image_drm_format_modifier` + external memory) would be the Vulkan
  equivalent and is not done.
- **Validation:** the Khronos validation layer is not installed on the
  reference machine, so the barrier and semaphore protocol was checked by
  reasoning and by real runs: Wayland, X11, Intel, NVIDIA, resizes, and the
  headless swapchain test. Run once with `VK_INSTANCE_LAYERS=VK_LAYER_KHRONOS_validation`
  where it is available.
- **The layout invariant** described in the frame protocol depends on the
  engine never ending a frame with a readback or copy of the window surface.
  It holds today, but a feature that reads back the swapchain image
  mid-frame would have to end with a draw, or the protocol would need
  Skia's mutable-state API.
- **Memory:** each window has its own `VkInstance` and `VkDevice`. That costs a
  few MB of driver state and startup time per window. The headless test
  brings up instance, device and `GRContext` in about 200 ms on the reference
  machine, including loader and ICD load on first use. That is the same kind of
  cost as the per-window EGL display. A shared device across windows would need a
  thread and queue ownership story first.
- **No HDR or wide gamut.** Only 8-bit sRGB swapchain formats are used.
