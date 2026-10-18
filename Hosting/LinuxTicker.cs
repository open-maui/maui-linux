// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui.Animations;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Hosting;

/// <summary>
/// Monotonic clock a ticker can expose so <see cref="LinuxAnimationManager"/>
/// measures frame deltas with the same time source that schedules the frames
/// (and so a test ticker can drive animations with a fake clock instead of
/// wall time).
/// </summary>
internal interface ITickerClock
{
    /// <summary>Milliseconds elapsed on the ticker's monotonic clock.</summary>
    long Timestamp { get; }
}

/// <summary>
/// The platform <see cref="ITicker"/> that drives MAUI animations
/// (<c>view.FadeTo</c>, <c>Animation.Commit</c>, ...). <see cref="Fire"/> is
/// always invoked on the UI thread; how it gets there depends on which main
/// loop the application runs:
/// <list type="bullet">
/// <item>Native X11/Wayland backends: <c>LinuxApplication.RunEventLoop</c>
/// calls <see cref="PumpAll"/> once per iteration (the loop wakes at least
/// every ~16 ms), and each running ticker fires when its frame interval has
/// elapsed.</item>
/// <item>GTK mode (<c>gtk_main</c> owns the thread): a GLib timeout source at
/// <c>1000 / MaxFps</c> ms fires the ticker from the GLib main context.</item>
/// <item>Headless (no <see cref="LinuxApplication"/>): nothing pumps the ticker;
/// tests call <see cref="Pump"/> or <see cref="Fire"/> directly.</item>
/// </list>
/// Semantics match <see cref="Ticker"/>: <see cref="Start"/> is idempotent,
/// <see cref="Stop"/> tears the source down, <see cref="IsRunning"/> reflects
/// whether a source is installed, and <see cref="SystemEnabled"/> is false when
/// the desktop asks for reduced motion (<c>OPENMAUI_REDUCE_MOTION=1</c>, or the
/// desktop's animations setting turned off, followed while the app runs through
/// <see cref="SystemAnimationsSetting"/>), in which case the animation manager
/// completes animations immediately instead of tweening.
/// </summary>
internal class LinuxTicker : ITicker, ITickerClock
{
    private const int DefaultFps = 60;
    private const int MinFps = 1;
    private const int MaxSupportedFps = 240;

    private static readonly List<LinuxTicker> s_pumped = new();
    private static readonly Lock s_pumpedLock = new();

    private readonly Func<double> _now;
    private readonly SystemAnimationsSetting? _setting;
    private readonly Lock _lock = new();

    private int _maxFps = DefaultFps;
    private bool _isRunning;
    private bool _usesGlibSource;
    private uint _glibSourceId;
    private uint _glibGeneration;
    private bool _inFire;
    private double _nextDue;

    /// <inheritdoc />
    public bool IsRunning => _isRunning;

    /// <summary>
    /// False when the desktop or <c>OPENMAUI_REDUCE_MOTION</c> asks for reduced motion. It
    /// follows the desktop setting while the app runs (<see cref="SystemAnimationsSetting"/>),
    /// as MAUI's Windows ticker follows UISettings.AnimationsEnabledChanged.
    /// </summary>
    public bool SystemEnabled => (_setting ?? SystemAnimationsSetting.Current).Enabled;

    /// <inheritdoc />
    public int MaxFps
    {
        get => _maxFps;
        set
        {
            var clamped = Math.Clamp(value, MinFps, MaxSupportedFps);
            if (clamped == _maxFps)
                return;
            _maxFps = clamped;

            // A live GLib source has its interval baked in; reinstall it.
            if (_isRunning && _usesGlibSource)
            {
                RemoveGlibSource();
                InstallGlibSource();
            }
        }
    }

    /// <inheritdoc />
    public Action? Fire { get; set; }

    /// <summary>
    /// Raised after every animation tick. For views drawn by third-party code
    /// that cannot report its own invalidations (the Syncfusion bridge), so
    /// they repaint while animations run.
    /// </summary>
    internal static event Action? Ticked;

    /// <summary>Frame interval implied by <see cref="MaxFps"/>, in milliseconds.</summary>
    internal double IntervalMilliseconds => 1000.0 / _maxFps;

    /// <inheritdoc />
    public long Timestamp => (long)_now();

    public LinuxTicker()
    {
        var clock = Stopwatch.StartNew();
        _now = () => clock.Elapsed.TotalMilliseconds;
    }

    /// <summary>
    /// Creates a ticker that reads time from <paramref name="clock"/>
    /// (milliseconds, monotonic) instead of a stopwatch; lets tests pump frames
    /// against a fake clock.
    /// </summary>
    internal LinuxTicker(Func<double> clock, SystemAnimationsSetting? setting = null)
    {
        _now = clock ?? throw new ArgumentNullException(nameof(clock));
        _setting = setting;
    }

    /// <inheritdoc />
    public void Start()
    {
        lock (_lock)
        {
            if (_isRunning)
                return;
            _isRunning = true;
            _usesGlibSource = LinuxApplication.IsGtkMode;
            _nextDue = _now();
        }

        if (_usesGlibSource)
        {
            InstallGlibSource();
        }
        else
        {
            lock (s_pumpedLock)
            {
                if (!s_pumped.Contains(this))
                    s_pumped.Add(this);
            }
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning)
                return;
            _isRunning = false;
        }

        if (_usesGlibSource)
        {
            RemoveGlibSource();
        }
        else
        {
            lock (s_pumpedLock)
            {
                s_pumped.Remove(this);
            }
        }
    }

    /// <summary>
    /// Fires every running loop-pumped ticker whose frame interval has elapsed.
    /// Called by the native run loop once per iteration on the UI thread.
    /// </summary>
    internal static void PumpAll()
    {
        LinuxTicker[] tickers;
        lock (s_pumpedLock)
        {
            if (s_pumped.Count == 0)
                return;
            tickers = s_pumped.ToArray();
        }

        foreach (var ticker in tickers)
            ticker.Pump();
    }

    /// <summary>
    /// Fires this ticker if it is running and at least one frame interval has
    /// elapsed since the previous fire. Returns true when it fired.
    /// </summary>
    internal bool Pump()
    {
        if (!_isRunning)
            return false;

        var now = _now();
        if (now < _nextDue)
            return false;

        var interval = IntervalMilliseconds;
        _nextDue += interval;
        // Fell more than a frame behind (loop stalled): resynchronise rather
        // than firing a burst of catch-up frames.
        if (_nextDue < now)
            _nextDue = now + interval;

        InvokeFire();
        return true;
    }

    private void InvokeFire()
    {
        var fire = Fire;
        if (fire is null)
            return;

        _inFire = true;
        try
        {
            fire();
            Ticked?.Invoke();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("LinuxTicker", "Unhandled exception in animation tick", ex);
        }
        finally
        {
            _inFire = false;
        }
    }

    private void InstallGlibSource()
    {
        var interval = (uint)Math.Max(1, Math.Round(IntervalMilliseconds));
        // Each source carries its generation so a source superseded while its
        // own callback is on the stack (Stop or a MaxFps change from inside
        // Fire) retires itself by returning false instead of double-firing
        // alongside its replacement.
        var generation = ++_glibGeneration;
        _glibSourceId = GLibNative.TimeoutAdd(interval, () => OnGlibTimeout(generation));
    }

    private void RemoveGlibSource()
    {
        var id = _glibSourceId;
        _glibSourceId = 0;
        _glibGeneration++;
        // Inside the callback the source is torn down by returning false;
        // removing it here as well would trip a GLib critical.
        if (id != 0 && !_inFire)
            GLibNative.SourceRemove(id);
    }

    private bool OnGlibTimeout(uint generation)
    {
        if (!_isRunning || generation != _glibGeneration)
            return false;

        InvokeFire();

        // Stop() or a MaxFps change during Fire retired this source: let GLib
        // drop it now that the dispatch is over.
        return _isRunning && generation == _glibGeneration;
    }
}
