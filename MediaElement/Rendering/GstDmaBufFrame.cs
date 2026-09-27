// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Interop;
using Microsoft.Maui.Platform.Linux.Rendering;
using static Microsoft.Maui.Platform.Linux.MediaElement.Native.GStreamerInterop;

namespace Microsoft.Maui.Platform.Linux.MediaElement.Rendering;

/// <summary>
/// A decoded video frame whose planes are DMA-BUFs (a hardware decoder's
/// output surface), owned through a ref on its <c>GstSample</c>. Holding the
/// sample keeps the buffer out of the decoder's pool, so the decoder cannot
/// write into it while it may be sampled; <see cref="Release"/> gives it back
/// once the GPU is done with it (see <see cref="GstSampleReleaseQueue"/>).
/// </summary>
internal sealed class GstDmaBufFrame : DmaBufFrame
{
    private static readonly Lazy<nuint> s_videoMetaApi = new(gst_video_meta_api_get_type);

    private IntPtr _sample;
    private readonly DmaBufDescriptor _descriptor;
    private readonly GstSampleReleaseQueue _releaseQueue;

    private GstDmaBufFrame(IntPtr sample, DmaBufDescriptor descriptor, GstSampleReleaseQueue releaseQueue)
    {
        _sample = sample;
        _descriptor = descriptor;
        _releaseQueue = releaseQueue;
    }

    public DmaBufDescriptor Descriptor => _descriptor;

    /// <summary>True when the sample's caps carry the memory:DMABuf feature.</summary>
    public static bool IsDmaBufCaps(IntPtr caps)
    {
        var features = gst_caps_get_features(caps, 0);
        return features != IntPtr.Zero && gst_caps_features_contains(features, VideoFrameFormats.DmaBufFeature);
    }

    /// <summary>
    /// Describes a DMA-BUF sample (streaming thread). On success the frame
    /// takes over the caller's ref on <paramref name="sample"/>; on failure
    /// the caller keeps it and <paramref name="error"/> says why.
    /// </summary>
    public static GstDmaBufFrame? TryCreate(IntPtr sample, IntPtr caps, GstSampleReleaseQueue releaseQueue, out string? error)
    {
        error = null;
        var buffer = gst_sample_get_buffer(sample);
        var structure = gst_caps_get_structure(caps, 0);
        if (buffer == IntPtr.Zero || structure == IntPtr.Zero)
        {
            error = "sample without buffer or caps";
            return null;
        }

        var format = StructureGetString(structure, "format");
        var drmFormat = StructureGetString(structure, "drm-format");
        if (!VideoFrameFormats.TryGetDrmFormat(format, drmFormat, out uint fourcc, out ulong modifier))
        {
            error = $"unsupported DMA-BUF format {format}{(drmFormat != null ? $" ({drmFormat})" : "")}";
            return null;
        }
        gst_structure_get_int(structure, "width", out int width);
        gst_structure_get_int(structure, "height", out int height);

        int planeCount = DrmFourcc.PlaneCount(fourcc);
        var meta = gst_buffer_get_meta(buffer, s_videoMetaApi.Value);
        if (meta == IntPtr.Zero)
        {
            // Offsets and strides of a DMA-BUF layout are only known from the meta.
            error = "DMA-BUF buffer without GstVideoMeta";
            return null;
        }
        var layout = ReadVideoMeta(meta);
        if (layout.Planes < planeCount)
        {
            error = $"GstVideoMeta has {layout.Planes} planes, {DrmFourcc.ToCode(fourcc)} needs {planeCount}";
            return null;
        }
        if (layout.Width > 0) width = layout.Width;
        if (layout.Height > 0) height = layout.Height;

        var planes = new DmaBufPlane[planeCount];
        for (int p = 0; p < planeCount; p++)
        {
            if (!gst_buffer_find_memory(buffer, (nuint)layout.Offsets[p], 1, out uint index, out _, out nuint skip))
            {
                error = $"plane {p} offset {layout.Offsets[p]} is outside the buffer";
                return null;
            }
            var memory = gst_buffer_peek_memory(buffer, index);
            if (memory == IntPtr.Zero || !gst_is_dmabuf_memory(memory))
            {
                error = $"plane {p} is not DMA-BUF memory";
                return null;
            }
            int fd = gst_dmabuf_memory_get_fd(memory);
            gst_memory_get_sizes(memory, out nuint memoryOffset, out _);
            if (fd < 0 || layout.Strides[p] <= 0)
            {
                error = $"plane {p} has no fd/stride";
                return null;
            }
            planes[p] = new DmaBufPlane(fd, (uint)(memoryOffset + skip), (uint)layout.Strides[p]);
        }

        var (space, range) = VideoFrameFormats.ParseColorimetry(StructureGetString(structure, "colorimetry"), height);
        var descriptor = new DmaBufDescriptor
        {
            Width = width,
            Height = height,
            Fourcc = fourcc,
            Modifier = modifier,
            Planes = planes,
            ColorSpace = space,
            Range = range,
        };
        return new GstDmaBufFrame(sample, descriptor, releaseQueue);
    }

    public override DmaBufDescriptor? Describe(out string? error)
    {
        error = _sample == IntPtr.Zero ? "frame already released" : null;
        return _sample == IntPtr.Zero ? null : _descriptor;
    }

    public override void Release(in DmaBufReleaseInfo info)
    {
        var sample = Interlocked.Exchange(ref _sample, IntPtr.Zero);
        if (sample == IntPtr.Zero) return;
        if (info.Sampled)
            _releaseQueue.Enqueue(sample, info.Display);
        else
            gst_sample_unref(sample);
    }

    /// <summary>Drops a frame that was never imported (nothing sampled it).</summary>
    public void Discard()
    {
        var sample = Interlocked.Exchange(ref _sample, IntPtr.Zero);
        if (sample != IntPtr.Zero)
            gst_sample_unref(sample);
    }
}

/// <summary>
/// Samples whose textures were sampled by GPU commands that may still be in
/// flight. Each gets an EGL fence (covering every GL command issued before its
/// release) and goes back to GStreamer only once the fence has signalled, so
/// the decoder never overwrites a surface the GPU is still reading. Nothing
/// blocks in the common case: the queue is polled every draw and only waits
/// (briefly) when it grows past a few entries. Without fence syncs, the release
/// falls back to <c>glFinish</c>, as the WebView path does.
/// </summary>
internal sealed class GstSampleReleaseQueue
{
    private const int MaxQueued = 3;
    private const ulong WaitTimeoutNs = 100_000_000; // 100 ms

    private readonly Queue<(IntPtr Sample, IntPtr Display, IntPtr Sync)> _queue = new();

    public int Count => _queue.Count;

    /// <summary>Fences and queues <paramref name="sample"/> (UI thread, importing context current).</summary>
    public void Enqueue(IntPtr sample, IntPtr display)
    {
        var sync = display != IntPtr.Zero
            ? Egl.eglCreateSync(display, Egl.EGL_SYNC_FENCE, new nint[] { Egl.EGL_NONE })
            : IntPtr.Zero;
        if (sync == IntPtr.Zero)
        {
            if (display != IntPtr.Zero) Gles.Finish();
            gst_sample_unref(sample);
            return;
        }
        Gles.Flush(); // the fence must reach the GPU for a later poll to see it signal
        _queue.Enqueue((sample, display, sync));
        Drain(wait: _queue.Count > MaxQueued);
    }

    /// <summary>
    /// Releases every queued sample whose fence has signalled (in order).
    /// With <paramref name="wait"/>, waits (bounded) for the oldest ones until
    /// at most <see cref="MaxQueued"/> remain; <paramref name="all"/> drains
    /// everything, waiting as needed (teardown).
    /// </summary>
    public void Drain(bool wait = false, bool all = false)
    {
        while (_queue.Count > 0)
        {
            var (sample, display, sync) = _queue.Peek();
            bool mustWait = all || (wait && _queue.Count > MaxQueued);
            int status = Egl.eglClientWaitSync(display, sync,
                mustWait ? Egl.EGL_SYNC_FLUSH_COMMANDS_BIT : 0,
                mustWait ? WaitTimeoutNs : 0);
            if (status == Egl.EGL_TIMEOUT_EXPIRED && !mustWait)
                break;
            // Signalled, failed (display gone: nothing can read it any more) or
            // timed out while forced: release.
            _queue.Dequeue();
            Egl.eglDestroySync(display, sync);
            gst_sample_unref(sample);
        }
    }
}
