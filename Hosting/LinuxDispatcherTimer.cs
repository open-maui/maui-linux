using System;
using System.Threading;
using Microsoft.Maui.Dispatching;

namespace Microsoft.Maui.Platform.Linux.Hosting;

internal class LinuxDispatcherTimer : IDispatcherTimer
{
	private Timer? _timer;

	private TimeSpan _interval = TimeSpan.FromMilliseconds(16L, 0L);

	private bool _isRunning;

	private bool _isRepeating = true;

	public TimeSpan Interval
	{
		get
		{
			return _interval;
		}
		set
		{
			_interval = value;
		}
	}

	public bool IsRunning => _isRunning;

	public bool IsRepeating
	{
		get
		{
			return _isRepeating;
		}
		set
		{
			_isRepeating = value;
		}
	}

	public event EventHandler? Tick;

	public void Start()
	{
		if (!_isRunning)
		{
			_isRunning = true;
			_timer = new Timer(OnTimerCallback, null, _interval, _isRepeating ? _interval : Timeout.InfiniteTimeSpan);
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
		this.Tick?.Invoke(this, EventArgs.Empty);
		if (!_isRepeating)
		{
			Stop();
		}
	}
}
