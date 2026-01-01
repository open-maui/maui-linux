using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Maui.Platform.Linux.Services;

public class VirtualizationManager<T> where T : SkiaView
{
	private readonly Dictionary<int, T> _activeViews = new Dictionary<int, T>();

	private readonly Queue<T> _recyclePool = new Queue<T>();

	private readonly Func<T> _viewFactory;

	private readonly Action<T>? _viewRecycler;

	private readonly int _maxPoolSize;

	private int _firstVisibleIndex = -1;

	private int _lastVisibleIndex = -1;

	public int ActiveViewCount => _activeViews.Count;

	public int PooledViewCount => _recyclePool.Count;

	public (int First, int Last) VisibleRange => (First: _firstVisibleIndex, Last: _lastVisibleIndex);

	public VirtualizationManager(Func<T> viewFactory, Action<T>? viewRecycler = null, int maxPoolSize = 20)
	{
		_viewFactory = viewFactory ?? throw new ArgumentNullException("viewFactory");
		_viewRecycler = viewRecycler;
		_maxPoolSize = maxPoolSize;
	}

	public void UpdateVisibleRange(int firstVisible, int lastVisible)
	{
		if (firstVisible == _firstVisibleIndex && lastVisible == _lastVisibleIndex)
		{
			return;
		}
		List<int> list = new List<int>();
		foreach (KeyValuePair<int, T> activeView in _activeViews)
		{
			if (activeView.Key < firstVisible || activeView.Key > lastVisible)
			{
				list.Add(activeView.Key);
			}
		}
		foreach (int item in list)
		{
			RecycleView(item);
		}
		_firstVisibleIndex = firstVisible;
		_lastVisibleIndex = lastVisible;
	}

	public T GetOrCreateView(int index, Action<T> bindData)
	{
		if (_activeViews.TryGetValue(index, out var value))
		{
			return value;
		}
		T val = ((_recyclePool.Count <= 0) ? _viewFactory() : _recyclePool.Dequeue());
		bindData(val);
		_activeViews[index] = val;
		return val;
	}

	public T? GetActiveView(int index)
	{
		if (!_activeViews.TryGetValue(index, out var value))
		{
			return null;
		}
		return value;
	}

	private void RecycleView(int index)
	{
		if (_activeViews.TryGetValue(index, out var value))
		{
			_activeViews.Remove(index);
			_viewRecycler?.Invoke(value);
			if (_recyclePool.Count < _maxPoolSize)
			{
				_recyclePool.Enqueue(value);
			}
			else
			{
				value.Dispose();
			}
		}
	}

	public void Clear()
	{
		foreach (T value in _activeViews.Values)
		{
			value.Dispose();
		}
		_activeViews.Clear();
		while (_recyclePool.Count > 0)
		{
			_recyclePool.Dequeue().Dispose();
		}
		_firstVisibleIndex = -1;
		_lastVisibleIndex = -1;
	}

	public void RemoveItem(int index)
	{
		RecycleView(index);
		foreach (KeyValuePair<int, T> item in (from kvp in _activeViews
			where kvp.Key > index
			orderby kvp.Key
			select kvp).ToList())
		{
			_activeViews.Remove(item.Key);
			_activeViews[item.Key - 1] = item.Value;
		}
	}

	public void InsertItem(int index)
	{
		foreach (KeyValuePair<int, T> item in (from kvp in _activeViews
			where kvp.Key >= index
			orderby kvp.Key descending
			select kvp).ToList())
		{
			_activeViews.Remove(item.Key);
			_activeViews[item.Key + 1] = item.Value;
		}
	}
}
