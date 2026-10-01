// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Dispatching;
using Microsoft.Maui.Platform.Linux.Interop;
using Microsoft.Maui.Platform.Linux.MediaElement.Native;
using Microsoft.Maui.Platform.Linux.MediaElement.Rendering;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using static Microsoft.Maui.Platform.Linux.MediaElement.Native.GStreamerInterop;

namespace Microsoft.Maui.Platform.Linux.MediaElement.Views;

/// <summary>
/// Skia-rendered video surface backing CommunityToolkit.Maui.MediaElement on Linux.
///
/// Owns a GStreamer playbin pipeline whose video-sink is an appsink configured
/// to deliver BGRA frames. The appsink's new_sample callback runs on a streaming
/// thread; it pulls the sample, extracts the buffer + caps (for width/height),
/// hands the raw bytes off to the main thread via LinuxDispatcher.Dispatch
/// (which uses GLib's idle queue when crossing threads). The main thread copies
/// the bytes into an SKImage and triggers Invalidate so the next frame is drawn.
///
/// Pipeline schematic:
///
///   playbin uri=...
///      ├─ video-sink → appsink (caps: video/x-raw,format=BGRA)  →  callback → SKImage
///      └─ audio-sink → autoaudiosink (system default)
///
/// HW decode auto-negotiates via playbin when vaapi/nvdec plugins are installed.
///
/// Zero-copy (GPU render target): the appsink additionally offers
/// <c>video/x-raw(memory:DMABuf),format=DMA_DRM</c> caps (first, so preferred)
/// restricted to the formats/modifiers the window's EGL display can import,
/// and advertises GstVideoMeta in the allocation query. A decoder that exports
/// DMA-BUF (VA-API <c>va*dec</c>, V4L2) then hands its output surface over
/// unchanged: the sample is kept alive, its planes are imported as one EGLImage
/// (NV12 through GL_TEXTURE_EXTERNAL_OES, the driver doing YUV to RGB) by the
/// shared <see cref="DmaBufTextureImporter"/>, and Skia draws the texture. No
/// videoconvert, no CPU copy, no per-frame upload. Decoders that cannot export
/// DMA-BUF (software, NVDEC) negotiate the BGRA caps and take the CPU path as
/// before. On a raster target, a failed import, a different GPU, or
/// <c>OPENMAUI_VIDEO_ZEROCOPY=0</c>, the pipeline is (re)built with BGRA-only
/// caps and the CPU path is used for the rest of the element's life.
/// </summary>
public class SkiaMediaElement : SkiaView, IDisposable
{
    private IntPtr _playbin;
    private IntPtr _appsink;
    private IntPtr _bus;
    private GStreamerInterop.GstAppSinkCallbacks _callbacks;
    private GStreamerInterop.GstAppSinkNewSampleDelegate? _newSampleDelegate;
    private GStreamerInterop.GstAppSinkEosDelegate? _eosDelegate;

    // Latest decoded frame and its raster info. Replaced on every frame by the
    // main-thread dispatch from the new_sample callback. SKImage is immutable
    // so we just swap references; the old one is GC'd after the next paint.
    private readonly object _frameLock = new();
    private SKImage? _latestFrame;

    /// <summary>
    /// Fires ~4× per second whenever a pipeline is active. The handler
    /// subscribes to push current Position + Duration into the MAUI
    /// MediaElement; without it the toolkit's bound sliders never update
    /// because we don't poll from the managed side otherwise.
    /// </summary>
    public event Action? StatusTick;

    /// <summary>Playback reached the end and stopped (not raised while looping).</summary>
    public event Action? MediaEnded;

    /// <summary>Start again from the beginning at the end, without stopping.</summary>
    public bool ShouldLoopPlayback { get; set; }
    private System.Threading.Timer? _statusTimer;

    // Current state mirror, kept in sync with playbin's state via the bus drain.
    private string? _currentUri;
    private bool _isPlaying;
    private Aspect _aspect = Aspect.AspectFit;
    private bool _disposed;

    public string? CurrentUri => _currentUri;
    public bool IsPlaying => _isPlaying;

    // ---- zero-copy (DMA-BUF) state --------------------------------------
    private enum FramePath
    {
        /// <summary>DMA-BUF caps offered; decided on the first DMA-BUF frame drawn.</summary>
        Undecided,
        /// <summary>DMA-BUF frames imported as EGLImage textures.</summary>
        ZeroCopy,
        /// <summary>System-memory BGRA frames copied into an SKImage.</summary>
        PixelCopy,
    }

    private static int s_framePathLogged;
    private static readonly Lazy<bool> s_drmFormatCaps = new(() =>
    {
        try
        {
            gst_version(out uint major, out uint minor, out _, out _);
            return VideoFrameFormats.SupportsDrmFormatCaps(major, minor);
        }
        catch (EntryPointNotFoundException) { return false; }
    });

    private FramePath _framePath = FramePath.Undecided;
    private bool _dmaBufOffered;                  // the current pipeline's appsink caps include memory:DMABuf
    private GstDmaBufFrame? _pendingGpuFrame;     // newest DMA-BUF frame, not imported yet (UI thread)
    private DmaBufTextureImporter? _gpuFrame;     // the on-screen DMA-BUF texture
    private readonly GstSampleReleaseQueue _releaseQueue = new();
    private IntPtr _sinkPad;
    private nuint _allocationProbeId;
    private GStreamerInterop.GstPadProbeCallback? _allocationProbe;
    private SKSize _gpuFrameSize;

    // Diagnostics.
    internal long ZeroCopyFrameCount { get; private set; }
    internal long PixelCopyFrameCount { get; private set; }
    /// <summary>Stopwatch ticks spent installing frames on the UI thread (pixel copy: SKImage copy; zero-copy: EGLImage import).</summary>
    internal long FrameImportTicks { get; private set; }
    internal string ActiveFramePath => _framePath.ToString();

    /// <summary>
    /// Set/replace the media source. Tears down the existing pipeline (if any)
    /// and rebuilds it for the new URI. Null URI clears the source.
    /// </summary>
    public void SetSource(string? uri)
    {
        if (_currentUri == uri) return;
        _currentUri = uri;

        DisposePipeline();

        if (string.IsNullOrEmpty(uri)) return;

        try
        {
            EnsureInitialized();
            BuildPipeline(uri);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaMediaElement", $"Pipeline build failed for '{uri}': {ex.Message}", ex);
        }
    }

    public Aspect Aspect
    {
        get => _aspect;
        set
        {
            if (_aspect == value) return;
            _aspect = value;
            Invalidate();
        }
    }

    /// <summary>Start (or resume) playback.</summary>
    public void Play()
    {
        if (_playbin == IntPtr.Zero) return;
        gst_element_set_state(_playbin, GstState.Playing);
        _isPlaying = true;

        // Drain initial bus messages on a background thread so any terminal
        // error (codec missing, network 4xx, etc.) makes it into the log
        // instead of being swallowed. Only runs once per Play() — bus
        // monitoring during the steady state isn't needed; flushing seeks
        // post pre-roll errors are vanishingly rare.
        // The task holds its own ref on the bus: the pipeline may be disposed
        // (new source, pixel-copy rebuild) while it is still popping.
        if (_bus == IntPtr.Zero) return;
        var bus = gst_object_ref(_bus);
        Task.Run(() =>
        {
            try { DrainBusMessages(bus); }
            finally { gst_object_unref(bus); }
        });
    }

    private static void DrainBusMessages(IntPtr bus)
    {
        try
        {
            // Pull up to ~3s of messages, stopping if we see a terminal one.
            for (int i = 0; i < 30; i++)
            {
                var msg = gst_bus_timed_pop_filtered(bus, 100_000_000UL,
                    GstMessageType.Error | GstMessageType.Eos);
                if (msg == IntPtr.Zero) continue;
                try
                {
                    var t = GstMessageGetType(msg);
                    if (t == GstMessageType.Error)
                    {
                        gst_message_parse_error(msg, out var err, out var dbg);
                        var errMsg = GErrorGetMessage(err);
                        DiagnosticLog.Error("SkiaMediaElement", $"GStreamer error: {errMsg}");
                        if (err != IntPtr.Zero) g_error_free(err);
                        if (dbg != IntPtr.Zero) g_free(dbg);
                        return;
                    }
                    if (t == GstMessageType.Eos) return;
                }
                finally { gst_message_unref(msg); }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaMediaElement", $"Bus drain failed: {ex.Message}", ex);
        }
    }

    /// <summary>Pause playback (keeps the current frame visible).</summary>
    public void Pause()
    {
        if (_playbin == IntPtr.Zero) return;
        gst_element_set_state(_playbin, GstState.Paused);
        _isPlaying = false;
    }

    /// <summary>Stop playback and seek back to the start.</summary>
    public void Stop()
    {
        if (_playbin == IntPtr.Zero) return;
        lock (_seekLock) _seekTargetNs = null; // a pending seek must not undo the stop
        gst_element_set_state(_playbin, GstState.Ready);
        gst_element_seek_simple(_playbin, GstFormat.Time, GstSeekFlags.Flush | GstSeekFlags.KeyUnit, 0);
        _isPlaying = false;
    }

    /// <summary>Seek to a specific position (ticks of GStreamer's nanosecond clock).</summary>
    public void SeekTo(TimeSpan position)
    {
        if (_playbin == IntPtr.Zero) return;

        // seek_simple silently returns true even when the pipeline is in NULL
        // or READY, so confirm we're at least PAUSED before issuing. If not,
        // transition to PAUSED first (up to 5s) so the seek has a frame to
        // land on. Without this guard early seeks (before pre-roll completes)
        // succeed-on-paper but have no visible effect.
        gst_element_get_state(_playbin, out var current, out _, 0);
        if (current != GstState.Paused && current != GstState.Playing)
        {
            gst_element_set_state(_playbin, GstState.Paused);
            gst_element_get_state(_playbin, out current, out _, 5_000_000_000UL);
        }

        long ns = Math.Max(0, position.Ticks * 100L);
        // NOTE: do NOT drain the bus here — the synchronous drain blocks the
        // main thread for up to 3 seconds per seek, freezing the UI and
        // queueing rapid scrubs. Bus errors are surfaced during Play().
        //
        // The seek itself runs on a worker, one at a time, latest target wins:
        // a flushing seek blocks its caller until the source's streaming thread
        // lets go, and back-to-back flushing seeks on souphttpsrc can leave a
        // range request stalled indefinitely (measured: 3 of 5 runs of twelve
        // seeks 50 ms apart on an HTTP VP9 stream hung the seeking thread;
        // waiting for each seek's preroll before the next, 0 of 5 did, and a
        // scrub of twelve targets issued four seeks and settled in 0.8 s).
        lock (_seekLock)
        {
            _seekTargetNs = ns;
            if (_seekWorkerRunning) return;
            _seekWorkerRunning = true;
        }
        var playbin = gst_object_ref(_playbin);
        int generation = _pipelineGeneration;
        Task.Run(() => RunSeeks(playbin, generation));
    }

    private readonly object _seekLock = new();
    private long? _seekTargetNs;      // newest requested target not yet issued (or in flight), under _seekLock
    private long? _seekInFlightNs;    // target being issued/prerolled, under _seekLock
    private bool _seekWorkerRunning;  // under _seekLock
    private int _pipelineGeneration;  // bumped when the pipeline is disposed

    private void RunSeeks(IntPtr playbin, int generation)
    {
        try
        {
            while (true)
            {
                long ns;
                lock (_seekLock)
                {
                    if (_seekTargetNs is not long target || generation != _pipelineGeneration)
                    {
                        _seekTargetNs = null;
                        _seekInFlightNs = null;
                        _seekWorkerRunning = false;
                        return;
                    }
                    ns = target;
                    _seekTargetNs = null;
                    _seekInFlightNs = ns;
                }
                gst_element_seek_simple(playbin, GstFormat.Time, AccurateSeekFlags, ns);
                // Wait for the seek to preroll before issuing the next one.
                gst_element_get_state(playbin, out _, out _, 5_000_000_000UL);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaMediaElement", $"Seek failed: {ex.Message}");
            lock (_seekLock)
            {
                _seekTargetNs = null;
                _seekInFlightNs = null;
                _seekWorkerRunning = false;
            }
        }
        finally
        {
            gst_object_unref(playbin);
        }
    }

    /// <summary>
    /// FLUSH | ACCURATE, deliberately without KEY_UNIT.
    /// - FLUSH: discard buffered frames so the seek shows immediately.
    /// - ACCURATE: the demuxer starts at the keyframe before the target and the
    ///   decoder decodes and discards up to the exact timestamp.
    /// KEY_UNIT must not be added: combined with ACCURATE it wins, and the
    /// segment starts at the previous keyframe instead (measured: a 10 s H.264
    /// clip with keyframes at 0 and 8.33 s landed every seek below 8.33 s on
    /// 0.0, local file and HTTP alike; the "1-2 s HTTP drift" was this). With
    /// ACCURATE alone, position after the seek equals the request over HTTP
    /// (souphttpsrc is seekable; qtdemux/matroskademux issue one range
    /// request), at 50-470 ms per seek on HW decode for the decode-and-discard.
    /// </summary>
    internal const GstSeekFlags AccurateSeekFlags = GstSeekFlags.Flush | GstSeekFlags.Accurate;

    public TimeSpan Position
    {
        get
        {
            if (_playbin == IntPtr.Zero) return TimeSpan.Zero;
            // While a seek is pending or prerolling, report where it is going
            // (the pipeline still reports the old position until it lands).
            lock (_seekLock)
            {
                if ((_seekTargetNs ?? _seekInFlightNs) is long target)
                    return TimeSpan.FromTicks(target / 100L);
            }
            return gst_element_query_position(_playbin, GstFormat.Time, out long ns)
                ? TimeSpan.FromTicks(ns / 100L)
                : TimeSpan.Zero;
        }
    }

    public TimeSpan Duration
    {
        get
        {
            if (_playbin == IntPtr.Zero) return TimeSpan.Zero;
            return gst_element_query_duration(_playbin, GstFormat.Time, out long ns)
                ? TimeSpan.FromTicks(ns / 100L)
                : TimeSpan.Zero;
        }
    }

    public void SetVolume(double volume)
    {
        if (_playbin == IntPtr.Zero) return;
        // playbin's volume range is 0.0 - 1.0; the MediaElement contract is the same.
        g_object_set_double(_playbin, "volume", Math.Clamp(volume, 0.0, 1.0), IntPtr.Zero);
    }

    public void SetMute(bool mute)
    {
        if (_playbin == IntPtr.Zero) return;
        g_object_set_bool(_playbin, "mute", mute, IntPtr.Zero);
    }

    private void BuildPipeline(string uri)
    {
        // playbin: auto-builds the decoder graph for any URI scheme it knows
        // (file://, http(s)://, rtsp://, ...). Picks HW decoders when their
        // plugin packages are installed.
        _playbin = gst_element_factory_make("playbin", "openmaui-playbin");
        if (_playbin == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create playbin element — is gstreamer1-plugins-base installed?");

        // appsink: receives raw BGRA frames we hand to Skia. Setting "emit-signals"
        // false (default) means we use the set_callbacks API instead of signal
        // handlers, which avoids unnecessary GObject signal overhead per frame.
        _appsink = gst_element_factory_make("appsink", "openmaui-appsink");
        if (_appsink == IntPtr.Zero)
        {
            gst_object_unref(_playbin);
            _playbin = IntPtr.Zero;
            throw new InvalidOperationException("Failed to create appsink element — is gstreamer1-plugins-base installed?");
        }

        // CPU path: force the appsink to negotiate BGRA so Skia can consume the
        // bytes without a separate conversion step (SKColorType.Bgra8888).
        // Zero-copy: DMA-BUF caps first (see the class remarks), BGRA after.
        var capsString = ChooseAppSinkCaps();
        var caps = gst_caps_from_string(capsString);
        if (caps == IntPtr.Zero && _dmaBufOffered)
        {
            DiagnosticLog.Warn("SkiaMediaElement", $"Invalid appsink caps '{capsString}'; using system memory");
            _dmaBufOffered = false;
            caps = gst_caps_from_string(VideoFrameFormats.SystemMemoryCaps);
        }
        gst_app_sink_set_caps(_appsink, caps);
        gst_caps_unref(caps);
        if (_dmaBufOffered)
            AddAllocationProbe();

        // Important: max-buffers and drop control how the sink behaves when the
        // app falls behind. With max-buffers=1 + drop=true, the sink keeps only
        // the freshest frame; we never queue history (which would smear seeks
        // and waste memory).
        g_object_set_int(_appsink, "max-buffers", 1, IntPtr.Zero);
        g_object_set_bool(_appsink, "drop", true, IntPtr.Zero);
        g_object_set_bool(_appsink, "sync", true, IntPtr.Zero);

        // Wire the appsink in as playbin's video-sink. playbin will route the
        // decoded video stream into it; audio still goes to autoaudiosink.
        g_object_set(_playbin, "video-sink", _appsink, IntPtr.Zero);

        // Hand the source URI to playbin and prime to Ready (the state where
        // caps are negotiated but no data has flowed yet).
        g_object_set_string(_playbin, "uri", uri, IntPtr.Zero);

        // Set up the streaming-thread callbacks. The delegate fields must stay
        // alive for the lifetime of the appsink (GC would otherwise collect them
        // mid-stream and crash).
        _newSampleDelegate = OnNewSample;
        _eosDelegate = OnEos;
        _callbacks = new GStreamerInterop.GstAppSinkCallbacks
        {
            Eos = Marshal.GetFunctionPointerForDelegate(_eosDelegate),
            NewSample = Marshal.GetFunctionPointerForDelegate(_newSampleDelegate),
        };
        gst_app_sink_set_callbacks(_appsink, ref _callbacks, IntPtr.Zero, IntPtr.Zero);

        _bus = gst_element_get_bus(_playbin);
        gst_element_set_state(_playbin, GstState.Ready);

        // Start the status pump (250ms cadence). System.Threading.Timer runs
        // on a ThreadPool thread; we marshal each tick onto the main thread
        // via LinuxDispatcher.Main so handler callbacks can touch UI state
        // safely. 250ms is responsive enough for a smooth slider without
        // burning CPU.
        _statusTimer?.Dispose();
        _statusTimer = new System.Threading.Timer(_ =>
        {
            LinuxDispatcher.Main?.Dispatch(() => StatusTick?.Invoke());
        }, null, 250, 250);
    }

    // Backpressure: only one frame is allowed in the main-thread queue at a
    // time. After a seek the streaming thread can emit dozens of buffered
    // frames in milliseconds; without this gate they all queue via Dispatch
    // and the main thread spends the next several seconds catching up on
    // already-stale frames. Dropping them is correct — the user only ever
    // sees the most recent one.
    private int _framePending; // 0 = main thread idle, 1 = pending install
    private GstFlowReturn OnNewSample(IntPtr appsink, IntPtr userData)
    {
        // Called on a GStreamer streaming thread — keep work minimal:
        // pull → map → copy bytes → unmap → dispatch to main thread.
        var sample = gst_app_sink_pull_sample(appsink);
        if (sample == IntPtr.Zero) return GstFlowReturn.Error;

        // If a previous frame is still queued for install on the main thread,
        // drop this one. The next sample will be picked up shortly anyway.
        if (System.Threading.Interlocked.CompareExchange(ref _framePending, 1, 0) != 0)
        {
            gst_sample_unref(sample);
            return GstFlowReturn.Ok;
        }

        bool sampleOwnedElsewhere = false;
        try
        {
            var buffer = gst_sample_get_buffer(sample);
            var caps = gst_sample_get_caps(sample);
            if (buffer == IntPtr.Zero || caps == IntPtr.Zero) return GstFlowReturn.Ok;

            if (GstDmaBufFrame.IsDmaBufCaps(caps))
            {
                if (!_dmaBufOffered)
                {
                    // Stale DMA-BUF frame of a pipeline leaving the zero-copy path: never map it.
                    System.Threading.Interlocked.Exchange(ref _framePending, 0);
                    return GstFlowReturn.Ok;
                }


                // Zero-copy: keep the decoder's surface (via the sample) and
                // import it on the UI thread. Mapping it would give tiled or
                // YUV bytes the BGRA path cannot use.
                var frame = GstDmaBufFrame.TryCreate(sample, caps, _releaseQueue, out var error);
                if (frame == null)
                {
                    LinuxDispatcher.Main?.Dispatch(() =>
                    {
                        try { FallBackToPixelCopy(error ?? "unusable DMA-BUF sample"); }
                        finally { System.Threading.Interlocked.Exchange(ref _framePending, 0); }
                    });
                    return GstFlowReturn.Ok;
                }
                sampleOwnedElsewhere = true;
                if (LinuxDispatcher.Main is not { } dispatcher)
                {
                    frame.Discard();
                    System.Threading.Interlocked.Exchange(ref _framePending, 0);
                    return GstFlowReturn.Ok;
                }
                dispatcher.Dispatch(() =>
                {
                    try { InstallGpuFrame(frame); }
                    finally { System.Threading.Interlocked.Exchange(ref _framePending, 0); }
                });
                return GstFlowReturn.Ok;
            }

            var structure = gst_caps_get_structure(caps, 0);
            if (!gst_structure_get_int(structure, "width", out int width)) return GstFlowReturn.Ok;
            if (!gst_structure_get_int(structure, "height", out int height)) return GstFlowReturn.Ok;
            if (width <= 0 || height <= 0) return GstFlowReturn.Ok;

            if (!gst_buffer_map(buffer, out var info, GstMapFlags.Read)) return GstFlowReturn.Ok;
            try
            {
                // Copy the BGRA bytes into a managed array. We could try to
                // wrap the GstBuffer's memory directly (zero-copy) but
                // GStreamer reuses the buffer after we return from the
                // callback, so anything Skia retains needs its own storage.
                var stride = (int)(info.Size / (nuint)height);
                var bytes = new byte[(int)info.Size];
                Marshal.Copy(info.Data, bytes, 0, bytes.Length);

                LinuxDispatcher.Main?.Dispatch(() =>
                {
                    try { InstallFrame(bytes, width, height, stride); }
                    finally { System.Threading.Interlocked.Exchange(ref _framePending, 0); }
                });
            }
            finally
            {
                gst_buffer_unmap(buffer, ref info);
            }
        }
        finally
        {
            if (!sampleOwnedElsewhere)
                gst_sample_unref(sample);
        }

        return GstFlowReturn.Ok;
    }

    private void OnEos(IntPtr appsink, IntPtr userData)
    {
        // Streaming thread — defer state mutation to main.
        LinuxDispatcher.Main?.Dispatch(OnEnded);
    }

    /// <summary>
    /// The end of the stream: a looping element starts again from the beginning and keeps
    /// playing (the pipeline is still PLAYING at EOS, so a flushing seek restarts it), as the
    /// toolkit's other backends loop; otherwise playback stops and <see cref="MediaEnded"/> fires.
    /// </summary>
    private void OnEnded()
    {
        if (_disposed || _playbin == IntPtr.Zero) return;
        if (ShouldLoopPlayback && _isPlaying)
        {
            SeekTo(TimeSpan.Zero);
            return;
        }
        _isPlaying = false;
        MediaEnded?.Invoke();
    }

    private void InstallFrame(byte[] bgra, int width, int height, int stride)
    {
        if (_disposed) return;
        long start = System.Diagnostics.Stopwatch.GetTimestamp();

        // Pin the byte array, build an SKImage from a raster-direct snapshot.
        // SKImage.FromPixelCopy copies internally so we can release the pinned
        // memory immediately and the SKImage owns its own backing store.
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        SKImage? image = null;
        unsafe
        {
            fixed (byte* p = bgra)
            {
                using var pixmap = new SKPixmap(info, (IntPtr)p, stride);
                image = SKImage.FromPixelCopy(pixmap);
            }
        }

        if (image == null) return;

        SKImage? old;
        lock (_frameLock)
        {
            old = _latestFrame;
            _latestFrame = image;
        }
        old?.Dispose();
        if (PixelCopyFrameCount++ == 0 && _framePath == FramePath.Undecided)
            LogFramePath("pixel copy (the decoder negotiated system-memory frames, not DMA-BUF)");
        FrameImportTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
        Invalidate();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // AspectFill scales the picture past the view on one side; the excess is cropped, as a
        // native player crops it. Unclipped it drew over the views around it.
        canvas.Save();
        canvas.ClipRect(bounds);
        try
        {
            DrawVideo(canvas, bounds);
        }
        finally
        {
            canvas.Restore();
        }
    }

    private void DrawVideo(SKCanvas canvas, SKRect bounds)
    {
        // Black background so empty regions (letterbox bars in AspectFit) match
        // the convention of every video player on every platform.
        using (var bg = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill })
            canvas.DrawRect(bounds, bg);

        // Samples whose GPU reads have finished go back to the decoder.
        _releaseQueue.Drain();

        if (_framePath != FramePath.PixelCopy && (_pendingGpuFrame != null || _gpuFrame?.HasFrame == true))
        {
            if (canvas.Context is GRContext gr)
            {
                try
                {
                    if (TryDrawZeroCopy(canvas, bounds, gr))
                        return;
                }
                catch (Exception ex)
                {
                    // Missing entry points on an old EGL/GStreamer, driver errors: never fatal.
                    FallBackToPixelCopy($"{ex.GetType().Name}: {ex.Message}");
                }
            }
            else if (_framePath == FramePath.ZeroCopy || PrimaryTargetIsGpu())
            {
                // A raster snapshot (Screenshot, offscreen render) of a view that
                // normally draws on the GPU target: read the current texture back
                // for this one draw rather than leaving the zero-copy path.
                if (TryDrawSnapshot(canvas, bounds))
                    return;
            }
            else
            {
                FallBackToPixelCopy("raster render target");
            }
        }

        SKImage? frame;
        lock (_frameLock) frame = _latestFrame;
        if (frame == null) return;

        var srcRect = new SKRect(0, 0, frame.Width, frame.Height);
        var dstRect = ComputeAspectRect(bounds, frame.Width, frame.Height);
        // SKSamplingOptions.Default matches what the sampling-less overload used
        // (nearest); explicit to avoid the SkiaSharp 4 deprecation warning.
        canvas.DrawImage(frame, srcRect, dstRect, SKSamplingOptions.Default);
    }

    private SKRect ComputeAspectRect(SKRect bounds, int srcW, int srcH)
    {
        switch (_aspect)
        {
            case Aspect.Fill:
                return bounds;
            case Aspect.AspectFill:
                {
                    float scale = Math.Max(bounds.Width / srcW, bounds.Height / srcH);
                    float w = srcW * scale, h = srcH * scale;
                    float x = bounds.Left + (bounds.Width - w) * 0.5f;
                    float y = bounds.Top + (bounds.Height - h) * 0.5f;
                    return new SKRect(x, y, x + w, y + h);
                }
            case Aspect.AspectFit:
            default:
                {
                    float scale = Math.Min(bounds.Width / srcW, bounds.Height / srcH);
                    float w = srcW * scale, h = srcH * scale;
                    float x = bounds.Left + (bounds.Width - w) * 0.5f;
                    float y = bounds.Top + (bounds.Height - h) * 0.5f;
                    return new SKRect(x, y, x + w, y + h);
                }
        }
    }

    protected override Size MeasureOverride(Size availableSize) => availableSize;

    // ---- zero-copy path ----------------------------------------------------

    /// <summary>
    /// The appsink caps for a new pipeline, deciding whether DMA-BUF is offered
    /// (sets <see cref="_dmaBufOffered"/>).
    /// </summary>
    private string ChooseAppSinkCaps()
    {
        _dmaBufOffered = false;
        if (_framePath == FramePath.PixelCopy)
            return VideoFrameFormats.SystemMemoryCaps;

        bool? gpuTarget = LinuxApplication.Current?.PrimaryContext?.RenderingEngine?.RenderTarget.IsGpuAccelerated;
        var reason = VideoFrameFormats.ZeroCopyUnavailableReason(
            Environment.GetEnvironmentVariable(VideoFrameFormats.EnvironmentVariable), gpuTarget);
        if (reason != null)
        {
            _framePath = FramePath.PixelCopy;
            LogFramePath($"pixel copy ({reason})");
            return VideoFrameFormats.SystemMemoryCaps;
        }

        _dmaBufOffered = true;
        return VideoFrameFormats.BuildAppSinkCaps(true, s_drmFormatCaps.Value, QueryImportableFormats());
    }

    /// <summary>
    /// Format/modifier pairs the current EGL display imports (when an EGL
    /// context is current on the UI thread, which it is between frames on the
    /// GPU target), so the decoder only picks an importable layout. Null when
    /// unknown: the caps then stay open and the import validates.
    /// </summary>
    private static List<(uint Fourcc, ulong Modifier)>? QueryImportableFormats()
    {
        try
        {
            var display = Egl.eglGetCurrentDisplay();
            if (display == Egl.EGL_NO_DISPLAY)
                return null;
            var result = new List<(uint, ulong)>();
            foreach (var fourcc in new[] { DrmFourcc.NV12, DrmFourcc.P010, DrmFourcc.XRGB8888, DrmFourcc.ARGB8888, DrmFourcc.XBGR8888, DrmFourcc.ABGR8888 })
            {
                var modifiers = Egl.QueryDmaBufModifiers(display, fourcc);
                if (modifiers == null)
                    return null; // query unavailable: leave the caps open
                foreach (var (modifier, _) in modifiers)
                    result.Add((fourcc, modifier));
            }
            return result.Count > 0 ? result : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// DMA-BUF import needs the buffer layout from GstVideoMeta; appsink does
    /// not advertise it in the allocation query itself, and VA decoders refuse
    /// DMA-BUF caps without it ("DMABuf caps negotiated without the mandatory
    /// support of VideoMeta"). The probe adds it before appsink answers.
    /// </summary>
    private void AddAllocationProbe()
    {
        try
        {
            _sinkPad = gst_element_get_static_pad(_appsink, "sink");
            if (_sinkPad == IntPtr.Zero) return;
            _allocationProbe = OnAllocationQuery;
            _allocationProbeId = gst_pad_add_probe(_sinkPad, GStreamerInterop.GstPadProbeType.QueryDownstream,
                Marshal.GetFunctionPointerForDelegate(_allocationProbe), IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            DiagnosticLog.Warn("SkiaMediaElement", $"Allocation probe unavailable ({ex.Message}); DMA-BUF caps will not negotiate");
        }
    }

    private static readonly Lazy<nuint> s_videoMetaApi = new(gst_video_meta_api_get_type);

    private static GStreamerInterop.GstPadProbeReturn OnAllocationQuery(IntPtr pad, IntPtr info, IntPtr userData)
    {
        try
        {
            var query = gst_pad_probe_info_get_query(info);
            if (query != IntPtr.Zero && GstQueryGetType(query) == GStreamerInterop.GST_QUERY_ALLOCATION)
            {
                var api = s_videoMetaApi.Value;
                if (!gst_query_find_allocation_meta(query, api, out _))
                    gst_query_add_allocation_meta(query, api, IntPtr.Zero);
            }
        }
        catch
        {
            // Streaming thread: never let an exception reach native code.
        }
        return GStreamerInterop.GstPadProbeReturn.Ok;
    }

    /// <summary>A new DMA-BUF frame arrived (UI thread): it replaces any frame not yet drawn.</summary>
    private void InstallGpuFrame(GstDmaBufFrame frame)
    {
        if (_disposed || _framePath == FramePath.PixelCopy || !_dmaBufOffered)
        {
            frame.Discard();
            return;
        }
        _pendingGpuFrame?.Discard();
        _pendingGpuFrame = frame;
        Invalidate();
    }

    /// <summary>
    /// Imports the pending frame (if any) and draws the current texture.
    /// False to fall through to the CPU image.
    /// </summary>
    private bool TryDrawZeroCopy(SKCanvas canvas, SKRect bounds, GRContext gr)
    {
        var unsupported = DmaBufTextureImporter.CheckCurrentDisplay(null, out var device);
        if (unsupported != null)
        {
            FallBackToPixelCopy(unsupported);
            return false;
        }

        _gpuFrame ??= new DmaBufTextureImporter();
        if (_pendingGpuFrame == null && _gpuFrame.HasFrame && !_gpuFrame.IsCurrentContext)
        {
            // Drawn by another window's context now: re-import the held frame here.
            _pendingGpuFrame = _gpuFrame.Reset(keepFrame: true) as GstDmaBufFrame;
        }

        if (_pendingGpuFrame is { } pending)
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            var error = _gpuFrame.Import(pending, gr);
            FrameImportTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            if (error != null)
            {
                FallBackToPixelCopy(error);
                return false;
            }
            _pendingGpuFrame = null; // owned by the importer now
            ZeroCopyFrameCount++;
            _gpuFrameSize = new SKSize(pending.Descriptor.Width, pending.Descriptor.Height);
            if (_framePath == FramePath.Undecided)
            {
                _framePath = FramePath.ZeroCopy;
                LogFramePath($"zero-copy (DMA-BUF {pending.Descriptor} -> EGLImage " +
                    $"{(_gpuFrame.IsExternalTexture ? "external " : "")}texture on {device})");
            }
        }

        using var image = _gpuFrame.CreateImage(gr);
        if (image == null)
            return false;

        var srcRect = new SKRect(0, 0, _gpuFrameSize.Width, _gpuFrameSize.Height);
        var dstRect = ComputeAspectRect(bounds, (int)_gpuFrameSize.Width, (int)_gpuFrameSize.Height);
        canvas.DrawImage(image, srcRect, dstRect, new SKSamplingOptions(SKFilterMode.Linear));
        return true;
    }

    private static bool PrimaryTargetIsGpu()
        => LinuxApplication.Current?.PrimaryContext?.RenderingEngine?.RenderTarget.IsGpuAccelerated == true;

    /// <summary>
    /// Draws the on-screen texture into a raster canvas via a GPU readback
    /// (snapshots only; costs a full-frame read). False when there is nothing
    /// to read back in the current context.
    /// </summary>
    private bool TryDrawSnapshot(SKCanvas canvas, SKRect bounds)
    {
        if (_gpuFrame is not { HasFrame: true, IsCurrentContext: true, Context: { } gr })
            return false;
        try
        {
            using var texture = _gpuFrame.CreateImage(gr);
            using var raster = texture?.ToRasterImage();
            if (raster == null)
                return false;
            var srcRect = new SKRect(0, 0, raster.Width, raster.Height);
            var dstRect = ComputeAspectRect(bounds, raster.Width, raster.Height);
            canvas.DrawImage(raster, srcRect, dstRect, new SKSamplingOptions(SKFilterMode.Linear));
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("SkiaMediaElement", $"Snapshot readback failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Leaves the zero-copy path for good on this element: drops every held
    /// DMA-BUF frame and rebuilds the pipeline with system-memory BGRA caps
    /// at the current position and play state.
    /// </summary>
    private void FallBackToPixelCopy(string reason)
    {
        if (_framePath == FramePath.PixelCopy)
            return;
        bool wasZeroCopy = _framePath == FramePath.ZeroCopy;
        _framePath = FramePath.PixelCopy;
        if (wasZeroCopy)
            DiagnosticLog.Warn("SkiaMediaElement", $"Video frames: zero-copy disabled for this element ({reason}); copying pixels");
        else
            LogFramePath($"pixel copy ({reason})");

        _pendingGpuFrame?.Discard();
        _pendingGpuFrame = null;
        _gpuFrame?.Dispose();
        _gpuFrame = null;
        _releaseQueue.Drain(all: true);

        if (!_dmaBufOffered || _appsink == IntPtr.Zero)
            return;
        _dmaBufOffered = false;

        // Outside the current draw (state changes wait for the streaming thread).
        var playbin = _playbin;
        LinuxDispatcher.Main?.Dispatch(() =>
        {
            if (_disposed || _playbin != playbin || _playbin == IntPtr.Zero) return;
            RebuildPipelineForPixelCopy();
        });
    }

    /// <summary>
    /// Rebuilds the pipeline with system-memory caps at the current position.
    /// Renegotiating the live pipeline is not reliable: after a reconfigure,
    /// playsink's videoconvert still advertises the DMA-BUF caps upstream (it
    /// transforms any caps feature), the VA decoder picks them again and
    /// negotiation fails (measured on GStreamer 1.28 with vavp9dec). A rebuild
    /// is a one-time cost per element.
    /// </summary>
    private void RebuildPipelineForPixelCopy()
    {
        var uri = _currentUri;
        if (string.IsNullOrEmpty(uri)) return;
        bool wasPlaying = _isPlaying;
        long position = gst_element_query_position(_playbin, GstFormat.Time, out long ns) ? ns : 0;

        DisposePipeline();
        try
        {
            BuildPipeline(uri);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaMediaElement", $"Pipeline rebuild for pixel copy failed: {ex.Message}");
            return;
        }

        gst_element_set_state(_playbin, GstState.Paused);
        if (position > 0)
        {
            gst_element_get_state(_playbin, out _, out _, 2_000_000_000UL);
            gst_element_seek_simple(_playbin, GstFormat.Time, AccurateSeekFlags, position);
        }
        if (wasPlaying)
            Play();
    }

    private static void LogFramePath(string message)
    {
        if (System.Threading.Interlocked.Exchange(ref s_framePathLogged, 1) == 0)
            DiagnosticLog.Info("SkiaMediaElement", $"Video frames: {message}");
    }

    /// <summary>Drops GPU frames when the pipeline goes away (UI thread).</summary>
    private void ReleaseGpuFrames()
    {
        _pendingGpuFrame?.Discard();
        _pendingGpuFrame = null;
        _gpuFrame?.Dispose();
        _gpuFrame = null;
        _releaseQueue.Drain(all: true);
    }

    private void DisposePipeline()
    {
        _statusTimer?.Dispose();
        _statusTimer = null;
        lock (_seekLock)
        {
            _pipelineGeneration++;
            _seekTargetNs = null;
            _seekInFlightNs = null;
        }

        if (_sinkPad != IntPtr.Zero)
        {
            if (_allocationProbeId != 0)
                gst_pad_remove_probe(_sinkPad, _allocationProbeId);
            gst_object_unref(_sinkPad);
            _sinkPad = IntPtr.Zero;
            _allocationProbeId = 0;
        }

        if (_playbin != IntPtr.Zero)
        {
            gst_element_set_state(_playbin, GstState.Null);
            gst_object_unref(_playbin);
            _playbin = IntPtr.Zero;
        }
        if (_bus != IntPtr.Zero)
        {
            gst_object_unref(_bus);
            _bus = IntPtr.Zero;
        }
        // _appsink was owned by playbin (we set it as video-sink); destroying
        // playbin already unref'd it.
        _appsink = IntPtr.Zero;
        _newSampleDelegate = null;
        _eosDelegate = null;
        _isPlaying = false;
        _dmaBufOffered = false;
        ReleaseGpuFrames();
        if (_framePath == FramePath.ZeroCopy)
            _framePath = FramePath.Undecided; // decided again for the next source
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposePipeline();
        SKImage? frame;
        lock (_frameLock)
        {
            frame = _latestFrame;
            _latestFrame = null;
        }
        frame?.Dispose();
        GC.SuppressFinalize(this);
    }
}
