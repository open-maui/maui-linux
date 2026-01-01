using System;
using System.Collections.Generic;
using Microsoft.Maui.Platform.Linux.Window;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

public class GpuRenderingEngine : IDisposable
{
	private readonly X11Window _window;

	private GRContext? _grContext;

	private GRBackendRenderTarget? _renderTarget;

	private SKSurface? _surface;

	private SKCanvas? _canvas;

	private bool _disposed;

	private bool _gpuAvailable;

	private int _width;

	private int _height;

	private SKBitmap? _softwareBitmap;

	private SKCanvas? _softwareCanvas;

	private readonly List<SKRect> _dirtyRegions = new List<SKRect>();

	private readonly object _dirtyLock = new object();

	private bool _fullRedrawNeeded = true;

	private const int MaxDirtyRegions = 32;

	public bool IsGpuAccelerated
	{
		get
		{
			if (_gpuAvailable)
			{
				return _grContext != null;
			}
			return false;
		}
	}

	public string BackendName
	{
		get
		{
			if (!IsGpuAccelerated)
			{
				return "Software";
			}
			return "OpenGL";
		}
	}

	public int Width => _width;

	public int Height => _height;

	public GpuRenderingEngine(X11Window window)
	{
		_window = window;
		_width = window.Width;
		_height = window.Height;
		_gpuAvailable = TryInitializeGpu();
		if (!_gpuAvailable)
		{
			Console.WriteLine("[GpuRenderingEngine] GPU not available, using software rendering");
			InitializeSoftwareRendering();
		}
		_window.Resized += OnWindowResized;
		_window.Exposed += OnWindowExposed;
	}

	private bool TryInitializeGpu()
	{
		try
		{
			GRGlInterface val = GRGlInterface.Create();
			if (val == null)
			{
				Console.WriteLine("[GpuRenderingEngine] Failed to create GL interface");
				return false;
			}
			_grContext = GRContext.CreateGl(val);
			if (_grContext == null)
			{
				Console.WriteLine("[GpuRenderingEngine] Failed to create GR context");
				((SKNativeObject)val).Dispose();
				return false;
			}
			CreateGpuSurface();
			Console.WriteLine("[GpuRenderingEngine] GPU acceleration enabled");
			return true;
		}
		catch (Exception ex)
		{
			Console.WriteLine("[GpuRenderingEngine] GPU initialization failed: " + ex.Message);
			return false;
		}
	}

	private void CreateGpuSurface()
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Expected O, but got Unknown
		if (_grContext != null)
		{
			GRBackendRenderTarget? renderTarget = _renderTarget;
			if (renderTarget != null)
			{
				((SKNativeObject)renderTarget).Dispose();
			}
			SKSurface? surface = _surface;
			if (surface != null)
			{
				((SKNativeObject)surface).Dispose();
			}
			int num = Math.Max(1, _width);
			int num2 = Math.Max(1, _height);
			GRGlFramebufferInfo val = default(GRGlFramebufferInfo);
			((GRGlFramebufferInfo)(ref val))._002Ector(0u, SkiaExtensions.ToGlSizedFormat((SKColorType)4));
			_renderTarget = new GRBackendRenderTarget(num, num2, 0, 8, val);
			_surface = SKSurface.Create(_grContext, _renderTarget, (GRSurfaceOrigin)1, (SKColorType)4);
			if (_surface == null)
			{
				Console.WriteLine("[GpuRenderingEngine] Failed to create GPU surface, falling back to software");
				_gpuAvailable = false;
				InitializeSoftwareRendering();
			}
			else
			{
				_canvas = _surface.Canvas;
			}
		}
	}

	private void InitializeSoftwareRendering()
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected O, but got Unknown
		int num = Math.Max(1, _width);
		int num2 = Math.Max(1, _height);
		SKBitmap? softwareBitmap = _softwareBitmap;
		if (softwareBitmap != null)
		{
			((SKNativeObject)softwareBitmap).Dispose();
		}
		SKCanvas? softwareCanvas = _softwareCanvas;
		if (softwareCanvas != null)
		{
			((SKNativeObject)softwareCanvas).Dispose();
		}
		SKImageInfo val = default(SKImageInfo);
		((SKImageInfo)(ref val))._002Ector(num, num2, (SKColorType)6, (SKAlphaType)2);
		_softwareBitmap = new SKBitmap(val);
		_softwareCanvas = new SKCanvas(_softwareBitmap);
		_canvas = _softwareCanvas;
	}

	private void OnWindowResized(object? sender, (int Width, int Height) size)
	{
		(_width, _height) = size;
		if (_gpuAvailable && _grContext != null)
		{
			CreateGpuSurface();
		}
		else
		{
			InitializeSoftwareRendering();
		}
		_fullRedrawNeeded = true;
	}

	private void OnWindowExposed(object? sender, EventArgs e)
	{
		_fullRedrawNeeded = true;
	}

	public void InvalidateRegion(SKRect region)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
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
			}
			else
			{
				_dirtyRegions.Add(region);
			}
		}
	}

	public void InvalidateAll()
	{
		_fullRedrawNeeded = true;
	}

	public void Render(SkiaView rootView)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		if (_canvas == null)
		{
			return;
		}
		SKSize availableSize = default(SKSize);
		((SKSize)(ref availableSize))._002Ector((float)Width, (float)Height);
		rootView.Measure(availableSize);
		rootView.Arrange(new SKRect(0f, 0f, (float)Width, (float)Height));
		bool flag;
		List<SKRect> list;
		lock (_dirtyLock)
		{
			flag = _fullRedrawNeeded || _dirtyRegions.Count == 0;
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
				list = new List<SKRect>(_dirtyRegions);
				_dirtyRegions.Clear();
			}
		}
		foreach (SKRect item in list)
		{
			_canvas.Save();
			if (!flag)
			{
				_canvas.ClipRect(item, (SKClipOperation)1, false);
			}
			_canvas.Clear(SKColors.White);
			rootView.Draw(_canvas);
			_canvas.Restore();
		}
		SkiaView.DrawPopupOverlays(_canvas);
		if (LinuxDialogService.HasActiveDialog)
		{
			LinuxDialogService.DrawDialogs(_canvas, new SKRect(0f, 0f, (float)Width, (float)Height));
		}
		_canvas.Flush();
		if (_gpuAvailable && _grContext != null)
		{
			_grContext.Submit(false);
		}
		else if (_softwareBitmap != null)
		{
			IntPtr pixels = _softwareBitmap.GetPixels();
			if (pixels != IntPtr.Zero)
			{
				_window.DrawPixels(pixels, Width, Height, _softwareBitmap.RowBytes);
			}
		}
	}

	public GpuStats GetStats()
	{
		if (_grContext == null)
		{
			return new GpuStats
			{
				IsGpuAccelerated = false
			};
		}
		int num = default(int);
		long resourceCacheLimitBytes = default(long);
		_grContext.GetResourceCacheLimits(ref num, ref resourceCacheLimitBytes);
		return new GpuStats
		{
			IsGpuAccelerated = true,
			MaxTextureSize = 4096,
			ResourceCacheUsedBytes = 0L,
			ResourceCacheLimitBytes = resourceCacheLimitBytes
		};
	}

	public void PurgeResources()
	{
		GRContext? grContext = _grContext;
		if (grContext != null)
		{
			grContext.PurgeResources();
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
			SKSurface? surface = _surface;
			if (surface != null)
			{
				((SKNativeObject)surface).Dispose();
			}
			GRBackendRenderTarget? renderTarget = _renderTarget;
			if (renderTarget != null)
			{
				((SKNativeObject)renderTarget).Dispose();
			}
			GRContext? grContext = _grContext;
			if (grContext != null)
			{
				((SKNativeObject)grContext).Dispose();
			}
			SKBitmap? softwareBitmap = _softwareBitmap;
			if (softwareBitmap != null)
			{
				((SKNativeObject)softwareBitmap).Dispose();
			}
			SKCanvas? softwareCanvas = _softwareCanvas;
			if (softwareCanvas != null)
			{
				((SKNativeObject)softwareCanvas).Dispose();
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
