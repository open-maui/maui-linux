// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Interop;
using Microsoft.Maui.Platform.Linux.Native;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// Zero-copy import of WPE WebKit frames on the GPU render target: the
/// <c>WPEBufferDMABuf</c> WebKit rendered into is wrapped as an EGLImage on the
/// platform's own EGL display (the one current while the render target draws,
/// not WPE's), bound to a GL texture and handed to Skia as a borrowed
/// <see cref="GRBackendTexture"/>. No pixels are read back or copied. The
/// EGLImage/texture/context handling is the shared
/// <see cref="DmaBufTextureImporter"/> (also used by the MediaElement); this
/// class adapts WPEBuffers to it.
/// </summary>
/// <remarks>
/// <para>Lifetime: the importer holds exactly one buffer (the one on screen)
/// together with its EGLImage and texture. They stay alive until a newer
/// frame replaces them, because the render target may repaint the view at any
/// time without WebKit producing a new frame (static pages render once).</para>
/// <para>Synchronisation: before the texture is first sampled, WebKit's
/// rendering fence (if it attached one) is waited on GPU-side (EGL native
/// fence + <c>eglWaitSync</c>; CPU <c>poll</c> fallback). When a buffer is
/// handed back to WebKit, a release fence covering every GL command issued so
/// far (so every draw that sampled it) is attached with
/// <c>wpe_buffer_set_release_fence</c> when EGL_ANDROID_native_fence_sync is
/// available; otherwise <c>glFinish</c> runs before the release. Either way
/// WebKit never renders into a buffer the GPU may still be reading.</para>
/// <para>Threading/contexts: everything runs on the UI thread. GL objects
/// belong to the context that was current at import; if the view is later
/// drawn in another window's context, or disposed outside a frame, that
/// context is made current (surfaceless) to delete them, then the previous
/// binding is restored.</para>
/// </remarks>
internal sealed class DmaBufFrameImporter : IDisposable
{
    private readonly DmaBufTextureImporter _importer = new();

    /// <summary>True while a frame texture is held and can be drawn.</summary>
    public bool HasFrame => _importer.HasFrame;

    /// <summary>The WPEBuffer currently held (on screen), or zero.</summary>
    public IntPtr Buffer => (_importer.Frame as WpeDmaBufFrame)?.Buffer ?? IntPtr.Zero;

    /// <summary>
    /// Checks whether zero-copy import can work on the EGL display current on
    /// this thread: DMA-BUF import extensions, GL_OES_EGL_image, and the same
    /// GPU as the WPE headless display (<paramref name="wpeDevice"/>, verified
    /// with EGL_EXT_device_query when both sides can tell). Returns null when
    /// supported, else the reason. The result is cached per display.
    /// </summary>
    public static string? CheckCurrentDisplay(string? wpeDevice, out string deviceDescription)
        => DmaBufTextureImporter.CheckCurrentDisplay(wpeDevice, out deviceDescription);

    /// <summary>
    /// True when two DRM nodes (render or primary, e.g. /dev/dri/renderD128
    /// and /dev/dri/card1) belong to the same GPU: their sysfs device links
    /// resolve to the same bus device.
    /// </summary>
    internal static bool SameGpu(string nodeA, string nodeB) => DmaBufTextureImporter.SameGpu(nodeA, nodeB);

    /// <summary>
    /// True for a WPEBufferDMABuf whose layout this importer can express to
    /// Skia (8-bit RGB formats, 1-4 planes).
    /// </summary>
    public static bool IsImportable(IntPtr buffer)
    {
        if (buffer == IntPtr.Zero) return false;
        try
        {
            if (WpeNative.g_type_check_instance_is_a(buffer, WpeNative.wpe_buffer_dma_buf_get_type()) == 0)
                return false;
            return WpeDmaBufFrame.IsRgb(WpeNative.wpe_buffer_dma_buf_get_format(buffer));
        }
        catch (EntryPointNotFoundException)
        {
            return false; // a WPE build without the DMA-BUF buffer API
        }
    }

    /// <summary>
    /// Imports <paramref name="buffer"/> (a WPEBuffer the caller holds a ref
    /// on) as the new on-screen texture in the current context. On success the
    /// importer owns the buffer and hands the previous one back to WebKit
    /// (fenced, see the class remarks); on failure nothing changes, the caller
    /// still owns <paramref name="buffer"/>, and the reason is returned.
    /// </summary>
    public string? Import(IntPtr view, IntPtr buffer, GRContext gr)
        => _importer.Import(new WpeDmaBufFrame(view, buffer), gr);

    /// <summary>
    /// A borrowed-texture image of the current frame for drawing on
    /// <paramref name="gr"/>'s canvas, or null when there is none or the
    /// context differs from the one that imported it. Dispose after drawing.
    /// </summary>
    public SKImage? CreateImage(GRContext gr) => _importer.CreateImage(gr);

    /// <summary>True when the frame's GL objects live in the context current now.</summary>
    public bool IsCurrentContext => _importer.IsCurrentContext;

    /// <summary>
    /// Drops the held frame: deletes the GL objects in their own context and
    /// hands the buffer back to WebKit (after <c>glFinish</c>, since it was
    /// sampled). Returns the held WPEBuffer instead of releasing it when
    /// <paramref name="keepBuffer"/> is set (ownership passes to the caller).
    /// </summary>
    public IntPtr Reset(bool keepBuffer = false)
    {
        var frame = _importer.Reset(keepBuffer);
        return (frame as WpeDmaBufFrame)?.Buffer ?? IntPtr.Zero;
    }

    public void Dispose() => Reset();

    /// <summary>A WPEBufferDMABuf (plus the WPEView to hand it back to) as a <see cref="DmaBufFrame"/>.</summary>
    private sealed class WpeDmaBufFrame : DmaBufFrame
    {
        private static int s_syncLogged;

        private readonly IntPtr _view;

        public WpeDmaBufFrame(IntPtr view, IntPtr buffer)
        {
            _view = view;
            Buffer = buffer;
        }

        public IntPtr Buffer { get; }

        public static bool IsRgb(uint fourcc) => DrmFourcc.AlphaType(fourcc) != null && !DrmFourcc.IsYuv(fourcc);

        public override DmaBufDescriptor? Describe(out string? error)
        {
            uint fourcc = WpeNative.wpe_buffer_dma_buf_get_format(Buffer);
            if (!IsRgb(fourcc))
            {
                error = $"unsupported DRM format 0x{fourcc:X8}";
                return null;
            }

            uint planeCount = Math.Clamp(WpeNative.wpe_buffer_dma_buf_get_n_planes(Buffer), 1u, 4u);
            var planes = new DmaBufPlane[planeCount];
            for (uint p = 0; p < planeCount; p++)
            {
                planes[p] = new DmaBufPlane(
                    WpeNative.wpe_buffer_dma_buf_get_fd(Buffer, p),
                    WpeNative.wpe_buffer_dma_buf_get_offset(Buffer, p),
                    WpeNative.wpe_buffer_dma_buf_get_stride(Buffer, p));
            }

            error = null;
            return new DmaBufDescriptor
            {
                Width = WpeNative.wpe_buffer_get_width(Buffer),
                Height = WpeNative.wpe_buffer_get_height(Buffer),
                Fourcc = fourcc,
                Modifier = WpeNative.wpe_buffer_dma_buf_get_modifier(Buffer),
                Planes = planes,
            };
        }

        /// <summary>
        /// WebKit may hand over the buffer before the GPU finished rendering it and
        /// attach a sync_file saying when it will be. Queue a GPU-side wait so the
        /// texture is not sampled early (CPU wait when native fences are missing).
        /// </summary>
        public override unsafe void WaitForRendering(IntPtr display, bool nativeFence)
        {
            int fd = WpeNative.wpe_buffer_take_rendering_fence(Buffer);
            if (Interlocked.Exchange(ref s_syncLogged, 1) == 0)
            {
                Services.DiagnosticLog.Info("DmaBufFrameImporter",
                    $"rendering fence from WebKit: {(fd >= 0 ? "yes" : "none")}; release sync: {(nativeFence ? "native fence fd" : "glFinish")}");
            }
            if (fd < 0) return;

            if (nativeFence)
            {
                var sync = Egl.eglCreateSync(display, Egl.EGL_SYNC_NATIVE_FENCE_ANDROID,
                    new nint[] { Egl.EGL_SYNC_NATIVE_FENCE_FD_ANDROID, fd, Egl.EGL_NONE });
                if (sync != IntPtr.Zero)
                {
                    // The sync owns fd now.
                    Egl.eglWaitSync(display, sync, 0);
                    Egl.eglDestroySync(display, sync);
                    return;
                }
            }

            var pfd = new PollFd { Fd = fd, Events = 1 /* POLLIN */ };
            poll_native(&pfd, 1, 1000);
            close_native(fd);
        }

        /// <summary>
        /// Hands the buffer back to WebKit. When it was sampled, a release fence
        /// (or a full <c>glFinish</c>) guarantees the GPU is done reading it first.
        /// </summary>
        public override void Release(in DmaBufReleaseInfo info)
        {
            if (info.Sampled)
            {
                int fd = -1;
                if (info.NativeFence)
                {
                    var sync = Egl.eglCreateSync(info.Display, Egl.EGL_SYNC_NATIVE_FENCE_ANDROID, new nint[] { Egl.EGL_NONE });
                    if (sync != IntPtr.Zero)
                    {
                        Gles.Flush(); // the fence fd exists only once the sync is flushed
                        fd = Egl.DupNativeFenceFd(info.Display, sync);
                        Egl.eglDestroySync(info.Display, sync);
                    }
                }
                if (fd >= 0)
                    WpeNative.wpe_buffer_set_release_fence(Buffer, fd);
                else
                    Gles.Finish();
            }

            if (_view != IntPtr.Zero)
                WpeNative.wpe_view_buffer_released(_view, Buffer);
            WpeNative.g_object_unref(Buffer);
        }

        [DllImport("libc", EntryPoint = "close")]
        private static extern int close_native(int fd);

        [DllImport("libc", EntryPoint = "poll")]
        private static extern unsafe int poll_native(PollFd* fds, nuint nfds, int timeout);

        [StructLayout(LayoutKind.Sequential)]
        private struct PollFd
        {
            public int Fd;
            public short Events;
            public short Revents;
        }
    }
}
