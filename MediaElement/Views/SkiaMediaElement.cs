// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Dispatching;
using Microsoft.Maui.Platform.Linux.MediaElement.Native;
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
    private System.Threading.Timer? _statusTimer;

    // Current state mirror, kept in sync with playbin's state via the bus drain.
    private string? _currentUri;
    private bool _isPlaying;
    private Aspect _aspect = Aspect.AspectFit;
    private bool _disposed;

    public string? CurrentUri => _currentUri;
    public bool IsPlaying => _isPlaying;

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
            DiagnosticLog.Error("SkiaMediaElement", $"Pipeline build failed for '{uri}': {ex.Message}");
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
        Task.Run(DrainBusMessages);
    }

    private void DrainBusMessages()
    {
        if (_bus == IntPtr.Zero) return;
        try
        {
            // Pull up to ~3s of messages, stopping if we see a terminal one.
            for (int i = 0; i < 30; i++)
            {
                var msg = gst_bus_timed_pop_filtered(_bus, 100_000_000UL,
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
            DiagnosticLog.Error("SkiaMediaElement", $"Bus drain failed: {ex.Message}");
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

        long ns = position.Ticks * 100L;
        // FLUSH | KEY_UNIT | ACCURATE.
        // - FLUSH: discard buffered frames so the seek shows immediately.
        // - KEY_UNIT + ACCURATE: land precisely at the requested timestamp
        //   (decode-and-discard from the previous keyframe). KEY_UNIT alone
        //   lands on the nearest keyframe which is fine forward but offsets
        //   backward seeks by several seconds.
        // NOTE: do NOT drain the bus here — the synchronous drain blocks the
        // main thread for up to 3 seconds per seek, freezing the UI and
        // queueing rapid scrubs. Bus errors are surfaced during Play().
        gst_element_seek_simple(_playbin, GstFormat.Time,
            GstSeekFlags.Flush | GstSeekFlags.KeyUnit | GstSeekFlags.Accurate, ns);
    }

    public TimeSpan Position
    {
        get
        {
            if (_playbin == IntPtr.Zero) return TimeSpan.Zero;
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

        // Force the appsink to negotiate BGRA so Skia can consume the bytes
        // without a separate conversion step. The Skia BGRA format matches the
        // SKColorType.Bgra8888 we use for image construction below.
        var caps = gst_caps_from_string("video/x-raw,format=BGRA");
        gst_app_sink_set_caps(_appsink, caps);
        gst_caps_unref(caps);

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

        try
        {
            var buffer = gst_sample_get_buffer(sample);
            var caps = gst_sample_get_caps(sample);
            if (buffer == IntPtr.Zero || caps == IntPtr.Zero) return GstFlowReturn.Ok;

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
            gst_sample_unref(sample);
        }

        return GstFlowReturn.Ok;
    }

    private void OnEos(IntPtr appsink, IntPtr userData)
    {
        // Streaming thread — defer state mutation to main.
        LinuxDispatcher.Main?.Dispatch(() => { _isPlaying = false; });
    }

    private void InstallFrame(byte[] bgra, int width, int height, int stride)
    {
        if (_disposed) return;

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
        Invalidate();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Black background so empty regions (letterbox bars in AspectFit) match
        // the convention of every video player on every platform.
        using (var bg = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill })
            canvas.DrawRect(bounds, bg);

        SKImage? frame;
        lock (_frameLock) frame = _latestFrame;
        if (frame == null) return;

        var srcRect = new SKRect(0, 0, frame.Width, frame.Height);
        var dstRect = ComputeAspectRect(bounds, frame.Width, frame.Height);
        canvas.DrawImage(frame, srcRect, dstRect);
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

    private void DisposePipeline()
    {
        _statusTimer?.Dispose();
        _statusTimer = null;

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
