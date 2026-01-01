using System;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

public class RenderLayer : IDisposable
{
	private SKBitmap? _bitmap;

	private SKCanvas? _canvas;

	private bool _isDirty = true;

	private SKRect _bounds;

	private bool _disposed;

	public int ZIndex { get; }

	public bool IsDirty => _isDirty;

	public bool IsVisible { get; set; } = true;

	public float Opacity { get; set; } = 1f;

	public RenderLayer(int zIndex)
	{
		ZIndex = zIndex;
	}

	public SKCanvas BeginDraw(SKRect bounds)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		if (_bitmap == null || _bounds != bounds)
		{
			SKBitmap? bitmap = _bitmap;
			if (bitmap != null)
			{
				((SKNativeObject)bitmap).Dispose();
			}
			SKCanvas? canvas = _canvas;
			if (canvas != null)
			{
				((SKNativeObject)canvas).Dispose();
			}
			int num = Math.Max(1, (int)((SKRect)(ref bounds)).Width);
			int num2 = Math.Max(1, (int)((SKRect)(ref bounds)).Height);
			_bitmap = new SKBitmap(num, num2, (SKColorType)4, (SKAlphaType)2);
			_canvas = new SKCanvas(_bitmap);
			_bounds = bounds;
		}
		_canvas.Clear(SKColors.Transparent);
		_isDirty = false;
		return _canvas;
	}

	public void Invalidate()
	{
		_isDirty = true;
	}

	public void DrawTo(SKCanvas canvas, SKRect bounds)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Expected O, but got Unknown
		if (!IsVisible || _bitmap == null)
		{
			return;
		}
		SKPaint val = new SKPaint
		{
			Color = ((SKColor)(ref SKColors.White)).WithAlpha((byte)(Opacity * 255f))
		};
		try
		{
			canvas.DrawBitmap(_bitmap, ((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top, val);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
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
		}
	}
}
