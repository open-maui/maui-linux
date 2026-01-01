using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

public class LayeredRenderer : IDisposable
{
	private readonly Dictionary<int, RenderLayer> _layers = new Dictionary<int, RenderLayer>();

	private readonly object _lock = new object();

	private bool _disposed;

	public RenderLayer GetLayer(int zIndex)
	{
		lock (_lock)
		{
			if (!_layers.TryGetValue(zIndex, out RenderLayer value))
			{
				value = new RenderLayer(zIndex);
				_layers[zIndex] = value;
			}
			return value;
		}
	}

	public void RemoveLayer(int zIndex)
	{
		lock (_lock)
		{
			if (_layers.TryGetValue(zIndex, out RenderLayer value))
			{
				value.Dispose();
				_layers.Remove(zIndex);
			}
		}
	}

	public void Composite(SKCanvas canvas, SKRect bounds)
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		lock (_lock)
		{
			foreach (RenderLayer item in _layers.Values.OrderBy((RenderLayer l) => l.ZIndex))
			{
				item.DrawTo(canvas, bounds);
			}
		}
	}

	public void InvalidateAll()
	{
		lock (_lock)
		{
			foreach (RenderLayer value in _layers.Values)
			{
				value.Invalidate();
			}
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		lock (_lock)
		{
			foreach (RenderLayer value in _layers.Values)
			{
				value.Dispose();
			}
			_layers.Clear();
		}
	}
}
