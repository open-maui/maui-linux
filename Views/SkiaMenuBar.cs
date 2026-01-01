using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaMenuBar : SkiaView
{
	private readonly List<MenuBarItem> _items = new List<MenuBarItem>();

	private int _hoveredIndex = -1;

	private int _openIndex = -1;

	private SkiaMenuFlyout? _openFlyout;

	public IList<MenuBarItem> Items => _items;

	public new SKColor BackgroundColor { get; set; } = new SKColor((byte)240, (byte)240, (byte)240);

	public SKColor TextColor { get; set; } = new SKColor((byte)33, (byte)33, (byte)33);

	public SKColor HoverBackgroundColor { get; set; } = new SKColor((byte)220, (byte)220, (byte)220);

	public SKColor ActiveBackgroundColor { get; set; } = new SKColor((byte)200, (byte)200, (byte)200);

	public float BarHeight { get; set; } = 28f;

	public float FontSize { get; set; } = 13f;

	public float ItemPadding { get; set; } = 12f;

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return new SKSize(((SKSize)(ref availableSize)).Width, BarHeight);
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Expected O, but got Unknown
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Expected O, but got Unknown
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Expected O, but got Unknown
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Expected O, but got Unknown
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		canvas.Save();
		SKPaint val = new SKPaint
		{
			Color = BackgroundColor,
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRect(base.Bounds, val);
			SKPaint val2 = new SKPaint
			{
				Color = new SKColor((byte)200, (byte)200, (byte)200),
				Style = (SKPaintStyle)1,
				StrokeWidth = 1f
			};
			try
			{
				SKRect bounds2 = base.Bounds;
				float left = ((SKRect)(ref bounds2)).Left;
				bounds2 = base.Bounds;
				float bottom = ((SKRect)(ref bounds2)).Bottom;
				bounds2 = base.Bounds;
				float right = ((SKRect)(ref bounds2)).Right;
				bounds2 = base.Bounds;
				canvas.DrawLine(left, bottom, right, ((SKRect)(ref bounds2)).Bottom, val2);
				SKPaint val3 = new SKPaint
				{
					Color = TextColor,
					TextSize = FontSize,
					IsAntialias = true
				};
				try
				{
					bounds2 = base.Bounds;
					float num = ((SKRect)(ref bounds2)).Left;
					SKRect val5 = default(SKRect);
					for (int i = 0; i < _items.Count; i++)
					{
						MenuBarItem menuBarItem = _items[i];
						SKRect val4 = default(SKRect);
						val3.MeasureText(menuBarItem.Text, ref val4);
						float num2 = ((SKRect)(ref val4)).Width + ItemPadding * 2f;
						float num3 = num;
						bounds2 = base.Bounds;
						float top = ((SKRect)(ref bounds2)).Top;
						float num4 = num + num2;
						bounds2 = base.Bounds;
						((SKRect)(ref val5))._002Ector(num3, top, num4, ((SKRect)(ref bounds2)).Bottom);
						if (i == _openIndex)
						{
							SKPaint val6 = new SKPaint
							{
								Color = ActiveBackgroundColor,
								Style = (SKPaintStyle)0
							};
							try
							{
								canvas.DrawRect(val5, val6);
							}
							finally
							{
								((IDisposable)val6)?.Dispose();
							}
						}
						else if (i == _hoveredIndex)
						{
							SKPaint val7 = new SKPaint
							{
								Color = HoverBackgroundColor,
								Style = (SKPaintStyle)0
							};
							try
							{
								canvas.DrawRect(val5, val7);
							}
							finally
							{
								((IDisposable)val7)?.Dispose();
							}
						}
						float num5 = num + ItemPadding;
						bounds2 = base.Bounds;
						float num6 = ((SKRect)(ref bounds2)).MidY - ((SKRect)(ref val4)).MidY;
						canvas.DrawText(menuBarItem.Text, num5, num6, val3);
						menuBarItem.Bounds = val5;
						num += num2;
					}
					_openFlyout?.Draw(canvas);
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

	public override SkiaView? HitTest(float x, float y)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if (!base.IsVisible)
		{
			return null;
		}
		if (_openFlyout != null)
		{
			SkiaView skiaView = _openFlyout.HitTest(x, y);
			if (skiaView != null)
			{
				return skiaView;
			}
		}
		SKRect bounds = base.Bounds;
		if (((SKRect)(ref bounds)).Contains(x, y))
		{
			return this;
		}
		if (_openFlyout != null)
		{
			CloseFlyout();
		}
		return null;
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		if (!base.IsEnabled)
		{
			return;
		}
		int num = -1;
		for (int i = 0; i < _items.Count; i++)
		{
			SKRect bounds = _items[i].Bounds;
			if (((SKRect)(ref bounds)).Contains(e.X, e.Y))
			{
				num = i;
				break;
			}
		}
		if (num != _hoveredIndex)
		{
			_hoveredIndex = num;
			if (_openIndex >= 0 && num >= 0 && num != _openIndex)
			{
				OpenFlyout(num);
			}
			Invalidate();
		}
		base.OnPointerMoved(e);
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		if (!base.IsEnabled)
		{
			return;
		}
		if (_openFlyout != null)
		{
			_openFlyout.OnPointerPressed(e);
			if (e.Handled)
			{
				CloseFlyout();
				return;
			}
		}
		for (int i = 0; i < _items.Count; i++)
		{
			SKRect bounds = _items[i].Bounds;
			if (((SKRect)(ref bounds)).Contains(e.X, e.Y))
			{
				if (_openIndex == i)
				{
					CloseFlyout();
				}
				else
				{
					OpenFlyout(i);
				}
				e.Handled = true;
				return;
			}
		}
		if (_openFlyout != null)
		{
			CloseFlyout();
			e.Handled = true;
		}
		base.OnPointerPressed(e);
	}

	private void OpenFlyout(int index)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		if (index >= 0 && index < _items.Count)
		{
			MenuBarItem menuBarItem = _items[index];
			_openIndex = index;
			_openFlyout = new SkiaMenuFlyout
			{
				Items = menuBarItem.Items
			};
			SKRect bounds = menuBarItem.Bounds;
			float left = ((SKRect)(ref bounds)).Left;
			bounds = menuBarItem.Bounds;
			float bottom = ((SKRect)(ref bounds)).Bottom;
			_openFlyout.Position = new SKPoint(left, bottom);
			_openFlyout.ItemClicked += OnFlyoutItemClicked;
			Invalidate();
		}
	}

	private void CloseFlyout()
	{
		if (_openFlyout != null)
		{
			_openFlyout.ItemClicked -= OnFlyoutItemClicked;
			_openFlyout = null;
		}
		_openIndex = -1;
		Invalidate();
	}

	private void OnFlyoutItemClicked(object? sender, MenuItemClickedEventArgs e)
	{
		CloseFlyout();
	}
}
