// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Animations;
using Animation = Microsoft.Maui.Animations.Animation;

namespace Microsoft.Maui.Platform.Linux.Hosting;

/// <summary>
/// The platform <see cref="IAnimationManager"/>. Mirrors
/// <see cref="Microsoft.Maui.Animations.AnimationManager"/> (start the ticker
/// on the first animation, stop it when the last one finishes, honour
/// <see cref="SpeedModifier"/>, complete everything immediately when the
/// ticker reports <see cref="ITicker.SystemEnabled"/> false) but measures
/// frame deltas with the ticker's own clock when it exposes one
/// (<see cref="ITickerClock"/>), so animations advance by exactly the time the
/// ticker says has passed and headless tests can drive them deterministically.
/// </summary>
internal class LinuxAnimationManager : IAnimationManager, IDisposable
{
    private readonly List<Animation> _animations = new();
    private readonly ITicker _ticker;
    private readonly ITickerClock? _clock;
    private long _lastUpdate;
    private bool _disposed;

    /// <inheritdoc />
    public double SpeedModifier { get; set; } = 1.0;

    /// <inheritdoc />
    public bool AutoStartTicker { get; set; } = true;

    /// <inheritdoc />
    public ITicker Ticker => _ticker;

    /// <summary>Animations currently registered (not yet finished or removed).</summary>
    internal int Count => _animations.Count;

    public LinuxAnimationManager(ITicker ticker)
    {
        _ticker = ticker ?? throw new ArgumentNullException(nameof(ticker));
        _clock = ticker as ITickerClock;
        _lastUpdate = Now;
        _ticker.Fire = OnTickerFire;
    }

    private long Now => _clock?.Timestamp ?? Environment.TickCount64;

    /// <inheritdoc />
    public void Add(Animation animation)
    {
        ArgumentNullException.ThrowIfNull(animation);

        if (!_ticker.SystemEnabled)
        {
            // Reduced motion: jump straight to the end state so callers still
            // observe the final value and the Finished callback.
            ForceFinish(animation);
            return;
        }

        if (!_animations.Contains(animation))
            _animations.Add(animation);

        if (AutoStartTicker && !_ticker.IsRunning)
            Start();
    }

    /// <inheritdoc />
    public void Remove(Animation animation)
    {
        _animations.Remove(animation);
        if (_animations.Count == 0)
            End();
    }

    private void Start()
    {
        _lastUpdate = Now;
        _ticker.Start();
    }

    private void End()
    {
        if (_ticker.IsRunning)
            _ticker.Stop();
    }

    private void OnTickerFire()
    {
        if (!_ticker.SystemEnabled)
        {
            foreach (var animation in _animations.ToArray())
                ForceFinish(animation);
            End();
            return;
        }

        var now = Now;
        var elapsed = Math.Max(0, now - _lastUpdate) * SpeedModifier;
        _lastUpdate = now;

        foreach (var animation in _animations.ToArray())
        {
            if (!animation.HasFinished)
                animation.Tick(elapsed);

            if (animation.HasFinished)
            {
                _animations.Remove(animation);
                animation.RemoveFromParent();
            }
        }

        if (_animations.Count == 0)
            End();
    }

    private void ForceFinish(Animation animation)
    {
        if (!animation.HasFinished)
        {
            // Animation.ForceFinish is internal to MAUI. A tick of long.MaxValue
            // milliseconds is the observable equivalent: a plain Animation clamps
            // its progress to 100%, and the Controls tweener treats a
            // long.MaxValue step as "finish immediately".
            animation.Tick((double)long.MaxValue);
            if (!animation.HasFinished)
                animation.Update(1.0);
        }

        _animations.Remove(animation);
        animation.RemoveFromParent();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _animations.Clear();
        End();
        if (_ticker is IDisposable disposable)
            disposable.Dispose();
    }
}
