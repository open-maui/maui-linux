using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Dispatching;

namespace Microsoft.Maui.Platform.Linux.Hosting;

internal class LinuxDispatcher : IDispatcher
{
	private readonly object _lock = new object();

	private readonly Queue<Action> _queue = new Queue<Action>();

	private bool _isDispatching;

	public bool IsDispatchRequired => false;

	public IDispatcherTimer CreateTimer()
	{
		return (IDispatcherTimer)(object)new LinuxDispatcherTimer();
	}

	public bool Dispatch(Action action)
	{
		if (action == null)
		{
			return false;
		}
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
		{
			return false;
		}
		Task.Delay(delay).ContinueWith((Task _) => Dispatch(action));
		return true;
	}

	private void ProcessQueue()
	{
		if (_isDispatching)
		{
			return;
		}
		_isDispatching = true;
		try
		{
			while (true)
			{
				Action action;
				lock (_lock)
				{
					if (_queue.Count == 0)
					{
						break;
					}
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
