using System.Collections.Generic;
using Microsoft.Maui.Animations;

namespace Microsoft.Maui.Platform.Linux.Hosting;

internal class LinuxAnimationManager : IAnimationManager
{
	private readonly List<Animation> _animations = new List<Animation>();

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
		Animation[] array = _animations.ToArray();
		foreach (Animation val in array)
		{
			val.Tick(0.016 * SpeedModifier);
			if (val.HasFinished)
			{
				Remove(val);
			}
		}
	}
}
