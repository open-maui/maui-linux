using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Platform.Linux.Window;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

public class SkiaRenderingEngine : IDisposable
{
	private readonly X11Window _window;

	private SKBitmap? _bitmap;

	private SKBitmap? _backBuffer;

	private SKCanvas? _canvas;

	private SKImageInfo _imageInfo;

	private bool _disposed;

	private bool _fullRedrawNeeded = true;

	private readonly List<SKRect> _dirtyRegions = new List<SKRect>();

	private readonly object _dirtyLock = new object();

	private const int MaxDirtyRegions = 32;

	private const float RegionMergeThreshold = 0.3f;

	public static SkiaRenderingEngine? Current { get; private set; }

	public ResourceCache ResourceCache { get; }

	public int Width => ((SKImageInfo)(ref _imageInfo)).Width;

	public int Height => ((SKImageInfo)(ref _imageInfo)).Height;

	public bool EnableDirtyRegionOptimization { get; set; } = true;

	public int DirtyRegionCount
	{
		get
		{
			lock (_dirtyLock)
			{
				return _dirtyRegions.Count;
			}
		}
	}

	public SkiaRenderingEngine(X11Window window)
	{
		_window = window;
		ResourceCache = new ResourceCache();
		Current = this;
		CreateSurface(window.Width, window.Height);
		_window.Resized += OnWindowResized;
		_window.Exposed += OnWindowExposed;
	}

	private void CreateSurface(int width, int height)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Expected O, but got Unknown
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Expected O, but got Unknown
		SKBitmap? bitmap = _bitmap;
		if (bitmap != null)
		{
			((SKNativeObject)bitmap).Dispose();
		}
		SKBitmap? backBuffer = _backBuffer;
		if (backBuffer != null)
		{
			((SKNativeObject)backBuffer).Dispose();
		}
		SKCanvas? canvas = _canvas;
		if (canvas != null)
		{
			((SKNativeObject)canvas).Dispose();
		}
		_imageInfo = new SKImageInfo(Math.Max(1, width), Math.Max(1, height), (SKColorType)6, (SKAlphaType)2);
		_bitmap = new SKBitmap(_imageInfo);
		_backBuffer = new SKBitmap(_imageInfo);
		_canvas = new SKCanvas(_bitmap);
		_fullRedrawNeeded = true;
		lock (_dirtyLock)
		{
			_dirtyRegions.Clear();
		}
	}

	private void OnWindowResized(object? sender, (int Width, int Height) size)
	{
		CreateSurface(size.Width, size.Height);
	}

	private void OnWindowExposed(object? sender, EventArgs e)
	{
		_fullRedrawNeeded = true;
	}

	public void InvalidateAll()
	{
		_fullRedrawNeeded = true;
	}

	public void InvalidateRegion(SKRect region)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		if (((SKRect)(ref region)).IsEmpty || ((SKRect)(ref region)).Width <= 0f || ((SKRect)(ref region)).Height <= 0f)
		{
			return;
		}
		region = SKRect.Intersect(region, new SKRect(0f, 0f, (float)Width, (float)Height));
		if (((SKRect)(ref region)).IsEmpty)
		{
			return;
		}
		lock (_dirtyLock)
		{
			if (_dirtyRegions.Count >= 32)
			{
				_fullRedrawNeeded = true;
				_dirtyRegions.Clear();
				return;
			}
			for (int i = 0; i < _dirtyRegions.Count; i++)
			{
				SKRect val = _dirtyRegions[i];
				if (ShouldMergeRegions(val, region))
				{
					_dirtyRegions[i] = SKRect.Union(val, region);
					return;
				}
			}
			_dirtyRegions.Add(region);
		}
	}

	private bool ShouldMergeRegions(SKRect a, SKRect b)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		SKRect val = SKRect.Intersect(a, b);
		if (((SKRect)(ref val)).IsEmpty)
		{
			SKRect val2 = default(SKRect);
			((SKRect)(ref val2))._002Ector(((SKRect)(ref a)).Left - 4f, ((SKRect)(ref a)).Top - 4f, ((SKRect)(ref a)).Right + 4f, ((SKRect)(ref a)).Bottom + 4f);
			return ((SKRect)(ref val2)).IntersectsWith(b);
		}
		float num = ((SKRect)(ref val)).Width * ((SKRect)(ref val)).Height;
		float val3 = ((SKRect)(ref a)).Width * ((SKRect)(ref a)).Height;
		float val4 = ((SKRect)(ref b)).Width * ((SKRect)(ref b)).Height;
		float num2 = Math.Min(val3, val4);
		return num / num2 >= 0.3f;
	}

	public void Render(SkiaView rootView)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		if (_canvas == null || _bitmap == null)
		{
			return;
		}
		SKSize availableSize = default(SKSize);
		((SKSize)(ref availableSize))._002Ector((float)Width, (float)Height);
		rootView.Measure(availableSize);
		rootView.Arrange(new SKRect(0f, 0f, (float)Width, (float)Height));
		bool flag = _fullRedrawNeeded || !EnableDirtyRegionOptimization;
		List<SKRect> list;
		lock (_dirtyLock)
		{
			if (flag)
			{
				list = new List<SKRect>
				{
					new SKRect(0f, 0f, (float)Width, (float)Height)
				};
				_dirtyRegions.Clear();
				_fullRedrawNeeded = false;
			}
			else
			{
				if (_dirtyRegions.Count == 0)
				{
					return;
				}
				list = MergeOverlappingRegions(_dirtyRegions.ToList());
				_dirtyRegions.Clear();
			}
		}
		foreach (SKRect item in list)
		{
			RenderRegion(rootView, item, flag);
		}
		SkiaView.DrawPopupOverlays(_canvas);
		if (LinuxDialogService.HasActiveDialog)
		{
			LinuxDialogService.DrawDialogs(_canvas, new SKRect(0f, 0f, (float)Width, (float)Height));
		}
		_canvas.Flush();
		PresentToWindow();
	}

	private void RenderRegion(SkiaView rootView, SKRect region, bool isFullRedraw)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		if (_canvas == null)
		{
			return;
		}
		_canvas.Save();
		if (!isFullRedraw)
		{
			_canvas.ClipRect(region, (SKClipOperation)1, false);
		}
		SKPaint val = new SKPaint
		{
			Color = SKColors.White,
			Style = (SKPaintStyle)0
		};
		try
		{
			_canvas.DrawRect(region, val);
			rootView.Draw(_canvas);
			_canvas.Restore();
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private List<SKRect> MergeOverlappingRegions(List<SKRect> regions)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		if (regions.Count <= 1)
		{
			return regions;
		}
		List<SKRect> list = new List<SKRect>();
		bool[] array = new bool[regions.Count];
		for (int i = 0; i < regions.Count; i++)
		{
			if (array[i])
			{
				continue;
			}
			SKRect val = regions[i];
			array[i] = true;
			bool flag;
			do
			{
				flag = false;
				for (int j = i + 1; j < regions.Count; j++)
				{
					if (!array[j] && ShouldMergeRegions(val, regions[j]))
					{
						val = SKRect.Union(val, regions[j]);
						array[j] = true;
						flag = true;
					}
				}
			}
			while (flag);
			list.Add(val);
		}
		return list;
	}

	private void PresentToWindow()
	{
		if (_bitmap != null)
		{
			IntPtr pixels = _bitmap.GetPixels();
			if (pixels != IntPtr.Zero)
			{
				_window.DrawPixels(pixels, ((SKImageInfo)(ref _imageInfo)).Width, ((SKImageInfo)(ref _imageInfo)).Height, ((SKImageInfo)(ref _imageInfo)).RowBytes);
			}
		}
	}

	public SKCanvas? GetCanvas()
	{
		return _canvas;
	}

	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}
		if (disposing)
		{
			_window.Resized -= OnWindowResized;
			_window.Exposed -= OnWindowExposed;
			SKCanvas? canvas = _canvas;
			if (canvas != null)
			{
				((SKNativeObject)canvas).Dispose();
			}
			SKBitmap? bitmap = _bitmap;
			if (bitmap != null)
			{
				((SKNativeObject)bitmap).Dispose();
			}
			SKBitmap? backBuffer = _backBuffer;
			if (backBuffer != null)
			{
				((SKNativeObject)backBuffer).Dispose();
			}
			ResourceCache.Dispose();
			if (Current == this)
			{
				Current = null;
			}
		}
		_disposed = true;
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
