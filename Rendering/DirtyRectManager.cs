using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

public class DirtyRectManager
{
	private readonly List<SKRect> _dirtyRects = new List<SKRect>();

	private readonly object _lock = new object();

	private bool _fullRedrawNeeded = true;

	private SKRect _bounds;

	private int _maxDirtyRects = 10;

	public int MaxDirtyRects
	{
		get
		{
			return _maxDirtyRects;
		}
		set
		{
			_maxDirtyRects = Math.Max(1, value);
		}
	}

	public bool NeedsFullRedraw => _fullRedrawNeeded;

	public IReadOnlyList<SKRect> DirtyRects
	{
		get
		{
			lock (_lock)
			{
				return _dirtyRects.ToList();
			}
		}
	}

	public bool HasDirtyRegions
	{
		get
		{
			lock (_lock)
			{
				return _fullRedrawNeeded || _dirtyRects.Count > 0;
			}
		}
	}

	public void SetBounds(SKRect bounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		if (_bounds != bounds)
		{
			_bounds = bounds;
			InvalidateAll();
		}
	}

	public void Invalidate(SKRect rect)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		if (((SKRect)(ref rect)).IsEmpty)
		{
			return;
		}
		lock (_lock)
		{
			if (_fullRedrawNeeded)
			{
				return;
			}
			rect = SKRect.Intersect(rect, _bounds);
			if (((SKRect)(ref rect)).IsEmpty)
			{
				return;
			}
			for (int i = 0; i < _dirtyRects.Count; i++)
			{
				SKRect val = _dirtyRects[i];
				if (((SKRect)(ref val)).Contains(rect))
				{
					return;
				}
				if (((SKRect)(ref rect)).Contains(_dirtyRects[i]))
				{
					_dirtyRects[i] = rect;
					MergeDirtyRects();
					return;
				}
				SKRect val2 = SKRect.Intersect(_dirtyRects[i], rect);
				if (!((SKRect)(ref val2)).IsEmpty)
				{
					float num = ((SKRect)(ref val2)).Width * ((SKRect)(ref val2)).Height;
					val = _dirtyRects[i];
					float width = ((SKRect)(ref val)).Width;
					val = _dirtyRects[i];
					float num2 = Math.Min(width * ((SKRect)(ref val)).Height, ((SKRect)(ref rect)).Width * ((SKRect)(ref rect)).Height);
					if (num > num2 * 0.5f)
					{
						_dirtyRects[i] = SKRect.Union(_dirtyRects[i], rect);
						MergeDirtyRects();
						return;
					}
				}
			}
			_dirtyRects.Add(rect);
			if (_dirtyRects.Count > _maxDirtyRects)
			{
				_fullRedrawNeeded = true;
				_dirtyRects.Clear();
			}
		}
	}

	public void InvalidateAll()
	{
		lock (_lock)
		{
			_fullRedrawNeeded = true;
			_dirtyRects.Clear();
		}
	}

	public void Clear()
	{
		lock (_lock)
		{
			_fullRedrawNeeded = false;
			_dirtyRects.Clear();
		}
	}

	public SKRect GetCombinedDirtyRect()
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		lock (_lock)
		{
			if (_fullRedrawNeeded || _dirtyRects.Count == 0)
			{
				return _bounds;
			}
			SKRect val = _dirtyRects[0];
			for (int i = 1; i < _dirtyRects.Count; i++)
			{
				val = SKRect.Union(val, _dirtyRects[i]);
			}
			return val;
		}
	}

	public void ApplyClipping(SKCanvas canvas)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		lock (_lock)
		{
			if (_fullRedrawNeeded || _dirtyRects.Count == 0)
			{
				return;
			}
			SKPath val = new SKPath();
			try
			{
				foreach (SKRect dirtyRect in _dirtyRects)
				{
					val.AddRect(dirtyRect, (SKPathDirection)0);
				}
				canvas.ClipPath(val, (SKClipOperation)1, false);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
	}

	private void MergeDirtyRects()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		bool flag;
		do
		{
			flag = false;
			for (int i = 0; i < _dirtyRects.Count - 1; i++)
			{
				for (int j = i + 1; j < _dirtyRects.Count; j++)
				{
					SKRect val = SKRect.Intersect(_dirtyRects[i], _dirtyRects[j]);
					if (!((SKRect)(ref val)).IsEmpty)
					{
						_dirtyRects[i] = SKRect.Union(_dirtyRects[i], _dirtyRects[j]);
						_dirtyRects.RemoveAt(j);
						flag = true;
						break;
					}
				}
				if (flag)
				{
					break;
				}
			}
		}
		while (flag);
	}
}
