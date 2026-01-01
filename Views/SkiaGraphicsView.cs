using System;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Skia;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaGraphicsView : SkiaView
{
	private IDrawable? _drawable;

	public IDrawable? Drawable
	{
		get
		{
			return _drawable;
		}
		set
		{
			_drawable = value;
			Invalidate();
		}
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Expected O, but got Unknown
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		if (base.BackgroundColor != SKColors.Transparent)
		{
			SKPaint val = new SKPaint
			{
				Color = base.BackgroundColor,
				Style = (SKPaintStyle)0
			};
			try
			{
				canvas.DrawRect(bounds, val);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		if (_drawable != null)
		{
			RectF val2 = default(RectF);
			((RectF)(ref val2))._002Ector(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Width, ((SKRect)(ref bounds)).Height);
			SkiaCanvas val3 = new SkiaCanvas();
			try
			{
				val3.Canvas = canvas;
				_drawable.Draw((ICanvas)(object)val3, val2);
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		if (((SKSize)(ref availableSize)).Width < float.MaxValue && ((SKSize)(ref availableSize)).Height < float.MaxValue)
		{
			return availableSize;
		}
		return new SKSize((((SKSize)(ref availableSize)).Width < float.MaxValue) ? ((SKSize)(ref availableSize)).Width : 100f, (((SKSize)(ref availableSize)).Height < float.MaxValue) ? ((SKSize)(ref availableSize)).Height : 100f);
	}
}
