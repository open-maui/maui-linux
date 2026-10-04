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
    public bool ShouldLoopPlayback
    {
        get => _shouldLoopPlayback;
        set
        {
            _shouldLoopPlayback = value;
            _controls.IsLooping = value;
        }
    }
    private bool _shouldLoopPlayback;
    private System.Threading.Timer? _statusTimer;

    // ---- playback state (UI thread) --------------------------------------
    // Raised on the UI thread from the bus watcher; the handler forwards them
    // to the toolkit (IMediaElement.MediaOpened / MediaFailed /
    // CurrentStateChanged / MediaWidth / MediaHeight) as MediaManager does on
    // Windows from MediaPlayer's events.

    /// <summary>The source prerolled (MediaPlayer.MediaOpened): duration and video size are known.</summary>
    internal event Action? MediaOpened;

    /// <summary>The pipeline posted an error (MediaPlayer.MediaFailed); the argument is the message.</summary>
    internal event Action<string>? MediaFailed;

    /// <summary>A playback state change (MediaPlaybackSession.PlaybackStateChanged, plus Stop and a cleared source).</summary>
    internal event Action<CommunityToolkit.Maui.Core.MediaElementState>? PlaybackStateChanged;

    /// <summary>The decoded video size changed (MediaPlaybackSession.NaturalVideoSizeChanged); 0 x 0 without video.</summary>
    internal event Action<int, int>? VideoSizeChanged;

    /// <summary>The last requested seek landed (MediaPlaybackSession.SeekCompleted).</summary>
    internal event Action? SeekCompleted;

    private bool _opened;      // MediaOpened raised for the current source
    private bool _failed;      // MediaFailed raised for the current source
    private bool _ended;       // reached the end without looping (pipeline paused at the end)
    private bool _buffering;
    private int _videoWidth, _videoHeight;
    private double _rate = 1.0;
    private bool _muted;
    private KeyValuePair<string, string>[] _httpHeaders = Array.Empty<KeyValuePair<string, string>>();
    private GStreamerInterop.SourceSetupCallback? _sourceSetupDelegate;
    private readonly MediaTransportControls _controls = new();
    private System.Threading.Timer? _controlsHideTimer;

    /// <summary>Width of the decoded video in pixels (0 without video).</summary>
    internal int VideoWidth => _videoWidth;

    /// <summary>Height of the decoded video in pixels (0 without video).</summary>
    internal int VideoHeight => _videoHeight;

    /// <summary>The playback rate applied to the pipeline.</summary>
    internal double Rate => _rate;

    /// <summary>The HTTP headers handed to the network source of the current pipeline.</summary>
    internal IReadOnlyList<KeyValuePair<string, string>> HttpHeaders => _httpHeaders;

    /// <summary>The playback controls overlay (MediaElement.ShouldShowPlaybackControls).</summary>
    internal MediaTransportControls Controls => _controls;

    /// <summary>Raised when the user works the playback controls overlay.</summary>
    internal event Action<MediaTransportCommand, double>? TransportCommand;

    public SkiaMediaElement()
    {
        _controls.Command += (command, value) => TransportCommand?.Invoke(command, value);
        _controls.ZoomRequested += () =>
            Aspect = Aspect == Aspect.AspectFill ? Aspect.AspectFit : Aspect.AspectFill;
    }

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
    public void SetSource(string? uri) => SetSource(uri, null);

    /// <summary>
    /// Set/replace the media source, with HTTP headers for a network source
    /// (UriMediaSource.HttpHeaders): playbin's HTTP source element gets them as
    /// extra request headers ("User-Agent" through its user-agent property).
    /// A new source is opened right away (prerolled to PAUSED), as Windows'
    /// MediaPlayer opens it: <see cref="MediaOpened"/> or
    /// <see cref="MediaFailed"/> follows whether or not it is played.
    /// </summary>
    internal void SetSource(string? uri, IEnumerable<KeyValuePair<string, string>>? httpHeaders)
    {
        var headers = httpHeaders?.Where(h => !string.IsNullOrWhiteSpace(h.Key)).ToArray()
            ?? Array.Empty<KeyValuePair<string, string>>();
        if (_currentUri == uri && headers.SequenceEqual(_httpHeaders)) return;
        _currentUri = uri;
        _httpHeaders = headers;

        DisposePipeline();
        _opened = _failed = _ended = _buffering = false;
        SetVideoSize(0, 0);
        _controls.HasMedia = !string.IsNullOrEmpty(uri);
        _controls.Position = _controls.Duration = TimeSpan.Zero;
        UpdateControlsState();

        if (string.IsNullOrEmpty(uri))
        {
            PlaybackStateChanged?.Invoke(CommunityToolkit.Maui.Core.MediaElementState.None);
            return;
        }

        try
        {
            EnsureInitialized();
            BuildPipeline(uri);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaMediaElement", $"Pipeline build failed for '{uri}': {ex.Message}", ex);
            ReportFailure(ex.Message);
            return;
        }

        PlaybackStateChanged?.Invoke(CommunityToolkit.Maui.Core.MediaElementState.Opening);
        // Open the media (preroll) so MediaOpened, the duration and the video
        // size arrive without Play, as on Windows.
        gst_element_set_state(_playbin, GstState.Paused);
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
        bool restart = _ended;
        _ended = false;
        if (restart)
        {
            // Played again after the end: from the beginning, as MediaPlayer
            // does. The pipeline holds paused at EOS; PLAYING before the seek
            // lands would end again at once, so a worker seeks to 0 first
            // (a flushing seek waits for the streaming thread) and then plays.
            RestartFromBeginning();
            _isPlaying = true;
            UpdateControlsState();
            return;
        }
        gst_element_set_state(_playbin, GstState.Playing);
        _isPlaying = true;
        UpdateControlsState();
        // Errors, the end and state changes reach the element through the bus
        // watcher started with the pipeline (WatchBus).
    }

    private bool _restarting; // a restart after the end is seeking to 0 (UI thread)

    private void RestartFromBeginning()
    {
        lock (_seekLock) _seekTargetNs = null;
        _restarting = true;
        var playbin = gst_object_ref(_playbin);
        int generation = _pipelineGeneration;
        double rate = _rate;
        Task.Run(() =>
        {
            try
            {
                IssueSeek(playbin, 0, rate);
                gst_element_get_state(playbin, out _, out _, 5_000_000_000UL);
                if (generation == Volatile.Read(ref _pipelineGeneration) && Volatile.Read(ref _isPlaying))
                    gst_element_set_state(playbin, GstState.Playing);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("SkiaMediaElement", $"Restart failed: {ex.Message}");
            }
            finally
            {
                gst_object_unref(playbin);
                Post(generation, () => _restarting = false);
            }
        });
    }

    /// <summary>Pause playback (keeps the current frame visible).</summary>
    public void Pause()
    {
        if (_playbin == IntPtr.Zero) return;
        gst_element_set_state(_playbin, GstState.Paused);
        _isPlaying = false;
        UpdateControlsState();
    }

    /// <summary>Stop playback and seek back to the start.</summary>
    public void Stop()
    {
        if (_playbin == IntPtr.Zero) return;
        lock (_seekLock) _seekTargetNs = null; // a pending seek must not undo the stop
        gst_element_set_state(_playbin, GstState.Ready);
        gst_element_seek_simple(_playbin, GstFormat.Time, GstSeekFlags.Flush | GstSeekFlags.KeyUnit, 0);
        _isPlaying = false;
        _ended = false;
        _buffering = false;
        UpdateControlsState();
        // MediaManager.PlatformStop reports Stopped itself on Windows.
        PlaybackStateChanged?.Invoke(CommunityToolkit.Maui.Core.MediaElementState.Stopped);
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
        _ended = false; // a seek after the end leaves the end (Play then plays from here)
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
                        bool landed = _seekInFlightNs != null && generation == _pipelineGeneration;
                        _seekTargetNs = null;
                        _seekInFlightNs = null;
                        _seekWorkerRunning = false;
                        if (landed)
                            Post(generation, () => SeekCompleted?.Invoke());
                        return;
                    }
                    ns = target;
                    _seekTargetNs = null;
                    _seekInFlightNs = ns;
                }
                IssueSeek(playbin, ns, Volatile.Read(ref _rate));
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

    /// <summary>
    /// A flushing, accurate seek to <paramref name="ns"/> that keeps the
    /// playback rate (gst_element_seek_simple resets it to 1). At rate 1 it is
    /// the plain seek_simple used before Speed was supported.
    /// </summary>
    private static void IssueSeek(IntPtr playbin, long ns, double rate)
    {
        if (rate == 1.0)
            gst_element_seek_simple(playbin, GstFormat.Time, AccurateSeekFlags, ns);
        else if (rate > 0)
            gst_element_seek(playbin, rate, GstFormat.Time, AccurateSeekFlags, GstSeekType.Set, ns, GstSeekType.None, -1);
        else
            gst_element_seek(playbin, rate, GstFormat.Time, AccurateSeekFlags, GstSeekType.Set, 0, GstSeekType.Set, ns);
    }

    /// <summary>
    /// Sets the playback rate (MediaElement.Speed; MediaPlayer.PlaybackRate on
    /// Windows). Applied to an opened pipeline at once and to a new one when
    /// it opens. A rate of 0 is the handler's business (it pauses); here it is
    /// ignored.
    /// </summary>
    internal void SetRate(double rate)
    {
        if (rate == 0 || double.IsNaN(rate) || double.IsInfinity(rate)) return;
        if (rate == _rate) return;
        Volatile.Write(ref _rate, rate);
        _controls.Rate = rate;
        Invalidate();
        if (_playbin == IntPtr.Zero || !_opened) return;
        ApplyRate();
    }

    private void ApplyRate()
    {
        // A flushing seek at the current position (on the seek worker) carries
        // the new rate. GStreamer's instant rate change (no flush) is not used:
        // on playbin 1.28 it blocks its caller indefinitely.
        SeekTo(Position);
    }

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
        _muted = mute;
        _controls.IsMuted = mute;
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

        // Speed: keep the pitch when the rate changes, as MediaPlayer does.
        // scaletempo passes audio through untouched at rate 1.
        var scaleTempo = gst_element_factory_make("scaletempo", null);
        if (scaleTempo != IntPtr.Zero)
            g_object_set(_playbin, "audio-filter", scaleTempo, IntPtr.Zero);

        // UriMediaSource.HttpHeaders: set on the HTTP source when playbin creates it.
        if (_httpHeaders.Length > 0)
        {
            _sourceSetupDelegate ??= OnSourceSetup;
            g_signal_connect_data(_playbin, "source-setup",
                Marshal.GetFunctionPointerForDelegate(_sourceSetupDelegate), IntPtr.Zero, IntPtr.Zero, 0);
        }

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
        StartBusWatch();

        // Start the status pump (250ms cadence). System.Threading.Timer runs
        // on a ThreadPool thread; we marshal each tick onto the main thread
        // via LinuxDispatcher.Main so handler callbacks can touch UI state
        // safely. 250ms is responsive enough for a smooth slider without
        // burning CPU.
        _statusTimer?.Dispose();
        _statusTimer = new System.Threading.Timer(_ =>
        {
            LinuxDispatcher.Main?.Dispatch(() =>
            {
                if (_disposed) return;
                if (_controls.IsVisible)
                {
                    _controls.Position = Position;
                    _controls.Duration = Duration;
                    Invalidate();
                }
                StatusTick?.Invoke();
            });
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
        if (_disposed || _playbin == IntPtr.Zero || _ended || _restarting) return;
        if (ShouldLoopPlayback && _isPlaying)
        {
            SeekTo(TimeSpan.Zero);
            return;
        }
        _isPlaying = false;
        _ended = true;
        // Hold at the end paused, as MediaPlayer does: a seek then stays put
        // and Play starts again from the beginning. (_ended keeps this pause
        // from being reported over the toolkit's Stopped.)
        gst_element_set_state(_playbin, GstState.Paused);
        UpdateControlsState();
        MediaEnded?.Invoke();
    }

    // ---- bus watcher -------------------------------------------------------

    private const uint WatchedMessages = MessageBits.Error | MessageBits.Eos | MessageBits.StateChanged
        | MessageBits.Buffering | MessageBits.AsyncDone;

    /// <summary>
    /// Follows the pipeline's bus for its whole life on a background thread
    /// (one per pipeline; it ends when the pipeline is disposed) and hands
    /// errors, the end of the stream, playbin state changes and buffering to
    /// the UI thread. Without it nothing popped the bus after the first
    /// seconds of Play, so later errors were lost and messages piled up.
    /// </summary>
    private void StartBusWatch()
    {
        if (_bus == IntPtr.Zero) return;
        var bus = gst_object_ref(_bus);
        var playbin = _playbin;
        int generation = _pipelineGeneration;
        var thread = new Thread(() => WatchBus(bus, playbin, generation))
        {
            IsBackground = true,
            Name = "openmaui-media-bus",
        };
        thread.Start();
    }

    private void WatchBus(IntPtr bus, IntPtr playbin, int generation)
    {
        bool buffering = false;
        try
        {
            while (!_disposed && Volatile.Read(ref _pipelineGeneration) == generation)
            {
                var msg = gst_bus_timed_pop_filtered_bits(bus, 100_000_000UL, WatchedMessages);
                if (msg == IntPtr.Zero) continue;
                try
                {
                    var type = MessageTypeBits(msg);
                    if ((type & MessageBits.Error) != 0)
                    {
                        gst_message_parse_error(msg, out var err, out var dbg);
                        var text = GErrorGetMessage(err) ?? "Unknown GStreamer error";
                        var debug = dbg != IntPtr.Zero ? Marshal.PtrToStringUTF8(dbg) : null;
                        if (err != IntPtr.Zero) g_error_free(err);
                        if (dbg != IntPtr.Zero) g_free(dbg);
                        DiagnosticLog.Error("SkiaMediaElement", $"GStreamer error: {text}{(debug != null ? $" ({debug})" : "")}");
                        Post(generation, () => ReportFailure(text));
                    }
                    else if ((type & MessageBits.Eos) != 0)
                    {
                        Post(generation, OnBusEos);
                    }
                    else if ((type & MessageBits.StateChanged) != 0)
                    {
                        if (MessageSource(msg) != playbin) continue;
                        gst_message_parse_state_changed(msg, out var oldState, out var newState, out var pending);
                        Post(generation, () => OnPlaybinStateChanged(oldState, newState, pending));
                    }
                    else if ((type & MessageBits.Buffering) != 0)
                    {
                        gst_message_parse_buffering(msg, out int percent);
                        bool now = percent < 100;
                        if (now != buffering)
                        {
                            buffering = now;
                            Post(generation, () => OnBuffering(now));
                        }
                    }
                    else if ((type & MessageBits.AsyncDone) != 0)
                    {
                        Post(generation, QueryVideoSize);
                    }
                }
                finally
                {
                    gst_message_unref(msg);
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaMediaElement", $"Bus watch failed: {ex.Message}", ex);
        }
        finally
        {
            gst_object_unref(bus);
        }
    }

    /// <summary>Runs <paramref name="action"/> on the UI thread if the pipeline it came from is still current.</summary>
    private void Post(int generation, Action action)
    {
        LinuxDispatcher.Main?.Dispatch(() =>
        {
            if (_disposed || generation != _pipelineGeneration || _playbin == IntPtr.Zero) return;
            action();
        });
    }

    private void ReportFailure(string message)
    {
        if (_failed) return;
        _failed = true;
        _isPlaying = false;
        UpdateControlsState();
        MediaFailed?.Invoke(message);
    }

    /// <summary>
    /// The end of the whole pipeline. A source with video ends through the
    /// appsink's EOS callback (<see cref="OnEos"/>, which the looping hero
    /// videos have always used); one without video (audio only) has no
    /// linked appsink, so its end comes from here.
    /// </summary>
    private void OnBusEos()
    {
        if (_videoWidth > 0) return;
        OnEnded();
    }

    private void OnPlaybinStateChanged(GstState oldState, GstState newState, GstState pending)
    {
        if (oldState == GstState.Ready && newState == GstState.Paused && !_opened)
        {
            _opened = true;
            QueryVideoSize();
            if (_rate != 1.0)
                ApplyRate();
            _controls.Duration = Duration;
            MediaOpened?.Invoke();
        }

        if (newState == GstState.Playing)
        {
            if (!_buffering)
                PlaybackStateChanged?.Invoke(CommunityToolkit.Maui.Core.MediaElementState.Playing);
        }
        else if (newState == GstState.Paused && pending == GstState.VoidPending && !_ended && !_buffering)
        {
            // A settled pause (by Pause, or a source opened without autoplay).
            // Transitional pauses (pending PLAYING: preroll before playing, the
            // lost-state pause of a flushing seek) are not reported.
            PlaybackStateChanged?.Invoke(CommunityToolkit.Maui.Core.MediaElementState.Paused);
        }
    }

    private void OnBuffering(bool buffering)
    {
        if (_buffering == buffering) return;
        _buffering = buffering;
        if (buffering)
            PlaybackStateChanged?.Invoke(CommunityToolkit.Maui.Core.MediaElementState.Buffering);
        else if (_opened && !_ended)
            PlaybackStateChanged?.Invoke(_isPlaying
                ? CommunityToolkit.Maui.Core.MediaElementState.Playing
                : CommunityToolkit.Maui.Core.MediaElementState.Paused);
    }

    /// <summary>Reads the negotiated video size from the appsink's caps (none for audio only).</summary>
    private void QueryVideoSize()
    {
        if (_appsink == IntPtr.Zero) return;
        var pad = gst_element_get_static_pad(_appsink, "sink");
        if (pad == IntPtr.Zero) return;
        try
        {
            var caps = gst_pad_get_current_caps(pad);
            if (caps == IntPtr.Zero) return;
            try
            {
                if (TryGetCapsSize(caps, out int width, out int height))
                    SetVideoSize(width, height);
            }
            finally
            {
                gst_caps_unref(caps);
            }
        }
        finally
        {
            gst_object_unref(pad);
        }
    }

    private void SetVideoSize(int width, int height)
    {
        if (width == _videoWidth && height == _videoHeight) return;
        _videoWidth = width;
        _videoHeight = height;
        VideoSizeChanged?.Invoke(width, height);
    }

    /// <summary>
    /// playbin's source-setup (streaming thread): hands UriMediaSource.HttpHeaders
    /// to an HTTP source (souphttpsrc: extra-headers and user-agent).
    /// </summary>
    private void OnSourceSetup(IntPtr playbin, IntPtr source, IntPtr userData)
    {
        try
        {
            var headers = Volatile.Read(ref _httpHeaders);
            if (headers.Length == 0 || source == IntPtr.Zero) return;
            ApplyHttpHeaders(source, headers);
        }
        catch (Exception ex)
        {
            // Never let an exception reach native code.
            DiagnosticLog.Warn("SkiaMediaElement", $"HTTP headers not applied: {ex.Message}");
        }
    }

    internal static void ApplyHttpHeaders(IntPtr source, IReadOnlyList<KeyValuePair<string, string>> headers)
    {
        var extra = new List<KeyValuePair<string, string>>();
        foreach (var header in headers)
        {
            if (string.Equals(header.Key, "User-Agent", StringComparison.OrdinalIgnoreCase)
                && HasProperty(source, "user-agent"))
                g_object_set_string(source, "user-agent", header.Value ?? string.Empty, IntPtr.Zero);
            else
                extra.Add(new(header.Key, header.Value ?? string.Empty));
        }
        if (extra.Count == 0) return;
        if (!HasProperty(source, "extra-headers"))
        {
            DiagnosticLog.Warn("SkiaMediaElement", "HttpHeaders ignored: the source element has no extra-headers property");
            return;
        }
        var structure = BuildHeaderStructure(extra);
        try
        {
            g_object_set(source, "extra-headers", structure, IntPtr.Zero);
        }
        finally
        {
            gst_structure_free(structure);
        }
    }

    // ---- playback controls overlay -----------------------------------------

    /// <summary>Pushes play / mute / position into the controls and keeps their auto-hide timer.</summary>
    private void UpdateControlsState()
    {
        _controls.IsPlaying = _isPlaying;
        _controls.IsMuted = _muted;
        ScheduleControlsHide();
        if (_controls.Enabled)
            Invalidate();
    }

    /// <summary>Shows or hides the playback controls (MediaElement.ShouldShowPlaybackControls).</summary>
    internal bool ShowPlaybackControls
    {
        get => _controls.Enabled;
        set
        {
            if (_controls.Enabled == value) return;
            _controls.Enabled = value;
            Invalidate();
        }
    }

    private void ScheduleControlsHide()
    {
        var due = _controls.TimeUntilHide;
        if (due is not TimeSpan delay)
        {
            _controlsHideTimer?.Dispose();
            _controlsHideTimer = null;
            return;
        }
        _controlsHideTimer ??= new System.Threading.Timer(_ =>
            LinuxDispatcher.Main?.Dispatch(() =>
            {
                if (_disposed) return;
                Invalidate();
                ScheduleControlsHide();
            }), null, Timeout.Infinite, Timeout.Infinite);
        _controlsHideTimer.Change(delay + TimeSpan.FromMilliseconds(20), Timeout.InfiniteTimeSpan);
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (_controls.Enabled)
        {
            bool wasVisible = _controls.IsVisible;
            _controls.PointerMoved(ViewRect, e.X, e.Y);
            ScheduleControlsHide();
            if (wasVisible || _controls.IsVisible)
                Invalidate();
        }
        base.OnPointerMoved(e);
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        if (_controls.Enabled)
        {
            _controls.PointerExited();
            Invalidate();
        }
        base.OnPointerExited(e);
    }

    private bool _controlsHavePress;

    /// <summary>The view's bounds in window space (where pointer positions and OnDraw's bounds are).</summary>
    private SKRect ViewRect => new((float)Bounds.Left, (float)Bounds.Top, (float)Bounds.Right, (float)Bounds.Bottom);

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (_controls.Enabled)
        {
            bool wasVisible = _controls.IsVisible;
            _controlsHavePress = _controls.PointerPressed(ViewRect, e.X, e.Y);
            ScheduleControlsHide();
            if (wasVisible || _controls.IsVisible)
                Invalidate();
            if (_controlsHavePress)
            {
                // The controls take the press; it does not reach gestures behind them.
                e.Handled = true;
                return;
            }
        }
        base.OnPointerPressed(e);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (_controlsHavePress)
        {
            _controlsHavePress = false;
            _controls.PointerReleased(ViewRect, e.X, e.Y);
            ScheduleControlsHide();
            Invalidate();
            e.Handled = true;
            return;
        }
        base.OnPointerReleased(e);
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
        SetVideoSize(width, height);
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
            if (_controls.IsVisible)
            {
                _controls.Position = Position;
                _controls.Duration = Duration;
                _controls.Draw(canvas, bounds);
            }
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
        SetVideoSize(frame.Descriptor.Width, frame.Descriptor.Height);
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
            IssueSeek(_playbin, position, _rate);
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
        _restarting = false;
        _dmaBufOffered = false;
        ReleaseGpuFrames();
        if (_framePath == FramePath.ZeroCopy)
            _framePath = FramePath.Undecided; // decided again for the next source
    }

    /// <summary>Tears the pipeline down and releases the last frame.</summary>
    public new void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
            DisposePipeline();
            _controlsHideTimer?.Dispose();
            _controlsHideTimer = null;
            SKImage? frame;
            lock (_frameLock)
            {
                frame = _latestFrame;
                _latestFrame = null;
            }
            frame?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>True once the element was disposed (its pipeline is gone for good).</summary>
    internal bool IsDisposed => _disposed;
}
