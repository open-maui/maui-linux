// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Animations;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Hosting;

/// <summary>
/// Linux-specific implementation of IMauiContext.
/// Provides the infrastructure for creating handlers and accessing platform services.
/// </summary>
public class LinuxMauiContext : IMauiContext
{
    private readonly IServiceProvider _services;
    private readonly IMauiHandlersFactory _handlers;
    private readonly LinuxApplication _linuxApp;
    private IAnimationManager? _animationManager;
    private IDispatcher? _dispatcher;

    public LinuxMauiContext(IServiceProvider services, LinuxApplication linuxApp)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _linuxApp = linuxApp ?? throw new ArgumentNullException(nameof(linuxApp));
        _handlers = services.GetRequiredService<IMauiHandlersFactory>();
    }

    /// <inheritdoc />
    public IServiceProvider Services => _services;

    /// <inheritdoc />
    public IMauiHandlersFactory Handlers => _handlers;

    /// <summary>
    /// Gets the Linux application instance.
    /// </summary>
    public LinuxApplication LinuxApp => _linuxApp;

    /// <summary>
    /// Gets the animation manager.
    /// </summary>
    public IAnimationManager AnimationManager
    {
        get
        {
            _animationManager ??= _services.GetService<IAnimationManager>()
                ?? new LinuxAnimationManager(new LinuxTicker());
            return _animationManager;
        }
    }

    /// <summary>
    /// Gets the dispatcher for UI thread operations.
    /// </summary>
    public IDispatcher Dispatcher
    {
        get
        {
            _dispatcher ??= _services.GetService<IDispatcher>()
                ?? new LinuxDispatcher();
            return _dispatcher;
        }
    }
}

/// <summary>
/// Scoped MAUI context for a specific window or view hierarchy.
/// </summary>
public class ScopedLinuxMauiContext : IMauiContext
{
    private readonly LinuxMauiContext _parent;

    public ScopedLinuxMauiContext(LinuxMauiContext parent)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
    }

    public IServiceProvider Services => _parent.Services;
    public IMauiHandlersFactory Handlers => _parent.Handlers;
}

/// <summary>
/// Linux dispatcher for UI thread operations.
/// </summary>
internal class LinuxDispatcher : IDispatcher
{
    private readonly object _lock = new();
    private readonly Queue<Action> _queue = new();
    private bool _isDispatching;

    public bool IsDispatchRequired => false; // Linux uses single-threaded event loop

    public IDispatcherTimer CreateTimer()
    {
        return new LinuxDispatcherTimer();
    }

    public bool Dispatch(Action action)
    {
        if (action == null)
            return false;

        lock (_lock)
        {
            _queue.Enqueue(action);
        }

        ProcessQueue();
        return true;
    }

    public bool DispatchDelayed(TimeSpan delay, Action action)
    {
        if (action == null)
            return false;

        Task.Delay(delay).ContinueWith(_ => Dispatch(action));
        return true;
    }

    private void ProcessQueue()
    {
        if (_isDispatching)
            return;

        _isDispatching = true;
        try
        {
            while (true)
            {
                Action? action;
                lock (_lock)
                {
                    if (_queue.Count == 0)
                        break;
                    action = _queue.Dequeue();
                }
                action?.Invoke();
            }
        }
        finally
        {
            _isDispatching = false;
        }
    }
}

/// <summary>
/// Linux dispatcher timer implementation.
/// </summary>
internal class LinuxDispatcherTimer : IDispatcherTimer
{
    private Timer? _timer;
    private TimeSpan _interval = TimeSpan.FromMilliseconds(16); // ~60fps default
    private bool _isRunning;
    private bool _isRepeating = true;

    public TimeSpan Interval
    {
        get => _interval;
        set => _interval = value;
    }

    public bool IsRunning => _isRunning;

    public bool IsRepeating
    {
        get => _isRepeating;
        set => _isRepeating = value;
    }

    public event EventHandler? Tick;

    public void Start()
    {
        if (_isRunning)
            return;

        _isRunning = true;
        _timer = new Timer(OnTimerCallback, null, _interval, _isRepeating ? _interval : Timeout.InfiniteTimeSpan);
    }

    public void Stop()
    {
        _isRunning = false;
        _timer?.Dispose();
        _timer = null;
    }

    private void OnTimerCallback(object? state)
    {
        Tick?.Invoke(this, EventArgs.Empty);

        if (!_isRepeating)
        {
            Stop();
        }
    }
}

/// <summary>
/// Linux animation manager.
/// </summary>
internal class LinuxAnimationManager : IAnimationManager
{
    private readonly List<Microsoft.Maui.Animations.Animation> _animations = new();
    private readonly ITicker _ticker;

    public LinuxAnimationManager(ITicker ticker)
    {
        _ticker = ticker;
        _ticker.Fire = OnTickerFire;
    }

    public double SpeedModifier { get; set; } = 1.0;
    public bool AutoStartTicker { get; set; } = true;

    public ITicker Ticker => _ticker;

    public void Add(Microsoft.Maui.Animations.Animation animation)
    {
        _animations.Add(animation);

        if (AutoStartTicker && !_ticker.IsRunning)
        {
            _ticker.Start();
        }
    }

    public void Remove(Microsoft.Maui.Animations.Animation animation)
    {
        _animations.Remove(animation);

        if (_animations.Count == 0 && _ticker.IsRunning)
        {
            _ticker.Stop();
        }
    }

    private void OnTickerFire()
    {
        var animations = _animations.ToArray();
        foreach (var animation in animations)
        {
            animation.Tick(16.0 / 1000.0 * SpeedModifier); // ~60fps
            if (animation.HasFinished)
            {
                Remove(animation);
            }
        }
    }
}

/// <summary>
/// Linux ticker for animation timing.
/// </summary>
internal class LinuxTicker : ITicker
{
    private Timer? _timer;
    private bool _isRunning;
    private int _maxFps = 60;

    public bool IsRunning => _isRunning;

    public bool SystemEnabled => true;

    public int MaxFps
    {
        get => _maxFps;
        set => _maxFps = Math.Max(1, Math.Min(120, value));
    }

    public Action? Fire { get; set; }

    public void Start()
    {
        if (_isRunning)
            return;

        _isRunning = true;
        var interval = TimeSpan.FromMilliseconds(1000.0 / _maxFps);
        _timer = new Timer(OnTimerCallback, null, TimeSpan.Zero, interval);
    }

    public void Stop()
    {
        _isRunning = false;
        _timer?.Dispose();
        _timer = null;
    }

    private void OnTimerCallback(object? state)
    {
        Fire?.Invoke();
    }
}
