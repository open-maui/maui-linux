using System;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaIndicatorView : SkiaView
{
	private int _count;

	private int _position;

	public int Count
	{
		get
		{
			return _count;
		}
		set
		{
			if (_count != value)
			{
				_count = Math.Max(0, value);
				if (_position >= _count)
				{
					_position = Math.Max(0, _count - 1);
				}
				InvalidateMeasure();
				Invalidate();
			}
		}
	}

	public int Position
	{
		get
		{
			return _position;
		}
		set
		{
			int num = Math.Clamp(value, 0, Math.Max(0, _count - 1));
			if (_position != num)
			{
				_position = num;
				Invalidate();
			}
		}
	}

	public SKColor IndicatorColor { get; set; } = new SKColor((byte)180, (byte)180, (byte)180);

	public SKColor SelectedIndicatorColor { get; set; } = new SKColor((byte)33, (byte)150, (byte)243);

	public float IndicatorSize { get; set; } = 10f;

	public float SelectedIndicatorSize { get; set; } = 10f;

	public float IndicatorSpacing { get; set; } = 8f;

	public IndicatorShape IndicatorShape { get; set; }

	public bool ShowBorder { get; set; }

	public SKColor BorderColor { get; set; } = new SKColor((byte)100, (byte)100, (byte)100);

	public float BorderWidth { get; set; } = 1f;

	public int MaximumVisible { get; set; } = 10;

	public bool HideSingle { get; set; } = true;

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		if (_count <= 0 || (HideSingle && _count <= 1))
		{
			return SKSize.Empty;
		}
		int num = Math.Min(_count, MaximumVisible);
		float num2 = (float)num * IndicatorSize + (float)(num - 1) * IndicatorSpacing;
		float num3 = Math.Max(IndicatorSize, SelectedIndicatorSize);
		return new SKSize(num2, num3);
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Expected O, but got Unknown
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Expected O, but got Unknown
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Expected O, but got Unknown
		if (_count <= 0 || (HideSingle && _count <= 1))
		{
			return;
		}
		canvas.Save();
		canvas.ClipRect(base.Bounds, (SKClipOperation)1, false);
		int num = Math.Min(_count, MaximumVisible);
		float num2 = (float)num * IndicatorSize + (float)(num - 1) * IndicatorSpacing;
		SKRect bounds2 = base.Bounds;
		float num3 = ((SKRect)(ref bounds2)).MidX - num2 / 2f + IndicatorSize / 2f;
		bounds2 = base.Bounds;
		float midY = ((SKRect)(ref bounds2)).MidY;
		int num4 = 0;
		int num5 = num;
		if (_count > MaximumVisible)
		{
			int num6 = MaximumVisible / 2;
			num4 = Math.Max(0, _position - num6);
			num5 = Math.Min(_count, num4 + MaximumVisible);
			if (num5 == _count)
			{
				num4 = _count - MaximumVisible;
			}
		}
		SKPaint val = new SKPaint
		{
			Color = IndicatorColor,
			Style = (SKPaintStyle)0,
			IsAntialias = true
		};
		try
		{
			SKPaint val2 = new SKPaint
			{
				Color = SelectedIndicatorColor,
				Style = (SKPaintStyle)0,
				IsAntialias = true
			};
			try
			{
				SKPaint val3 = new SKPaint
				{
					Color = BorderColor,
					Style = (SKPaintStyle)1,
					StrokeWidth = BorderWidth,
					IsAntialias = true
				};
				try
				{
					for (int i = num4; i < num5; i++)
					{
						int num7 = i - num4;
						float x = num3 + (float)num7 * (IndicatorSize + IndicatorSpacing);
						bool num8 = i == _position;
						SKPaint fillPaint = (num8 ? val2 : val);
						float size = (num8 ? SelectedIndicatorSize : IndicatorSize);
						DrawIndicator(canvas, x, midY, size, fillPaint, val3);
					}
					canvas.Restore();
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void DrawIndicator(SKCanvas canvas, float x, float y, float size, SKPaint fillPaint, SKPaint borderPaint)
	{
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Expected O, but got Unknown
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		float num = size / 2f;
		switch (IndicatorShape)
		{
		case IndicatorShape.Circle:
			canvas.DrawCircle(x, y, num, fillPaint);
			if (ShowBorder)
			{
				canvas.DrawCircle(x, y, num, borderPaint);
			}
			break;
		case IndicatorShape.Square:
		{
			SKRect val3 = default(SKRect);
			((SKRect)(ref val3))._002Ector(x - num, y - num, x + num, y + num);
			canvas.DrawRect(val3, fillPaint);
			if (ShowBorder)
			{
				canvas.DrawRect(val3, borderPaint);
			}
			break;
		}
		case IndicatorShape.RoundedSquare:
		{
			SKRect val2 = default(SKRect);
			((SKRect)(ref val2))._002Ector(x - num, y - num, x + num, y + num);
			float num2 = num * 0.3f;
			canvas.DrawRoundRect(val2, num2, num2, fillPaint);
			if (ShowBorder)
			{
				canvas.DrawRoundRect(val2, num2, num2, borderPaint);
			}
			break;
		}
		case IndicatorShape.Diamond:
		{
			SKPath val = new SKPath();
			try
			{
				val.MoveTo(x, y - num);
				val.LineTo(x + num, y);
				val.LineTo(x, y + num);
				val.LineTo(x - num, y);
				val.Close();
				canvas.DrawPath(val, fillPaint);
				if (ShowBorder)
				{
					canvas.DrawPath(val, borderPaint);
				}
				break;
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		}
	}

	public override SkiaView? HitTest(float x, float y)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		if (base.IsVisible)
		{
			SKRect bounds = base.Bounds;
			if (((SKRect)(ref bounds)).Contains(x, y))
			{
				if (_count > 0)
				{
					int num = Math.Min(_count, MaximumVisible);
					float num2 = (float)num * IndicatorSize + (float)(num - 1) * IndicatorSpacing;
					bounds = base.Bounds;
					float num3 = ((SKRect)(ref bounds)).MidX - num2 / 2f;
					if (_count > MaximumVisible)
					{
						int num4 = MaximumVisible / 2;
						if (Math.Max(0, _position - num4) + MaximumVisible > _count)
						{
							_ = _count;
							_ = MaximumVisible;
						}
					}
					for (int i = 0; i < num; i++)
					{
						float num5 = num3 + (float)i * (IndicatorSize + IndicatorSpacing);
						if (x >= num5 && x <= num5 + IndicatorSize)
						{
							return this;
						}
					}
				}
				return null;
			}
		}
		return null;
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (!base.IsEnabled || _count <= 0)
		{
			return;
		}
		int num = Math.Min(_count, MaximumVisible);
		float num2 = (float)num * IndicatorSize + (float)(num - 1) * IndicatorSpacing;
		SKRect bounds = base.Bounds;
		float num3 = ((SKRect)(ref bounds)).MidX - num2 / 2f;
		int num4 = 0;
		if (_count > MaximumVisible)
		{
			int num5 = MaximumVisible / 2;
			num4 = Math.Max(0, _position - num5);
			if (num4 + MaximumVisible > _count)
			{
				num4 = _count - MaximumVisible;
			}
		}
		int num6 = (int)((e.X - num3) / (IndicatorSize + IndicatorSpacing));
		if (num6 >= 0 && num6 < num)
		{
			Position = num4 + num6;
			e.Handled = true;
		}
		base.OnPointerPressed(e);
	}
}
