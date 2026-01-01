using System;
using System.Threading;
using Microsoft.Maui.Animations;

namespace Microsoft.Maui.Platform.Linux.Hosting;

internal class LinuxTicker : ITicker
{
	private Timer? _timer;

	private bool _isRunning;

	private int _maxFps = 60;

	public bool IsRunning => _isRunning;

	public bool SystemEnabled => true;

	public int MaxFps
	{
		get
		{
			return _maxFps;
		}
		set
		{
			_maxFps = Math.Max(1, Math.Min(120, value));
		}
	}

	public Action? Fire { get; set; }

	public void Start()
	{
		if (!_isRunning)
		{
			_isRunning = true;
			TimeSpan period = TimeSpan.FromMilliseconds(1000.0 / (double)_maxFps);
			_timer = new Timer(OnTimerCallback, null, TimeSpan.Zero, period);
		}
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
