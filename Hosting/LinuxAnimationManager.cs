// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Animations;
using Animation = Microsoft.Maui.Animations.Animation;

namespace Microsoft.Maui.Platform.Linux.Hosting;

internal class LinuxAnimationManager : IAnimationManager
{
    private readonly List<Animation> _animations = new();
    private readonly ITicker _ticker;

    public double SpeedModifier { get; set; } = 1.0;

    public bool AutoStartTicker { get; set; } = true;

    public ITicker Ticker => _ticker;

    public LinuxAnimationManager(ITicker ticker)
    {
        _ticker = ticker;
        _ticker.Fire = OnTickerFire;
    }

    public void Add(Animation animation)
    {
        _animations.Add(animation);
        if (AutoStartTicker && !_ticker.IsRunning)
        {
            _ticker.Start();
        }
    }

    public void Remove(Animation animation)
    {
        _animations.Remove(animation);
        if (_animations.Count == 0 && _ticker.IsRunning)
        {
            _ticker.Stop();
        }
    }

    private void OnTickerFire()
    {
        var animationsArray = _animations.ToArray();
        foreach (var animation in animationsArray)
        {
            animation.Tick(0.016 * SpeedModifier);
            if (animation.HasFinished)
            {
                Remove(animation);
            }
        }
    }
}
