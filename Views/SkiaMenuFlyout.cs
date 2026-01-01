using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaMenuFlyout : SkiaView
{
	private int _hoveredIndex = -1;

	private SKRect _bounds;

	public List<MenuItem> Items { get; set; } = new List<MenuItem>();

	public SKPoint Position { get; set; }

	public new SKColor BackgroundColor { get; set; } = SKColors.White;

	public SKColor TextColor { get; set; } = new SKColor((byte)33, (byte)33, (byte)33);

	public SKColor DisabledTextColor { get; set; } = new SKColor((byte)160, (byte)160, (byte)160);

	public SKColor HoverBackgroundColor { get; set; } = new SKColor((byte)230, (byte)230, (byte)230);

	public SKColor SeparatorColor { get; set; } = new SKColor((byte)220, (byte)220, (byte)220);

	public float FontSize { get; set; } = 13f;

	public float ItemHeight { get; set; } = 28f;

	public float SeparatorHeight { get; set; } = 9f;

	public float MinWidth { get; set; } = 180f;

	public event EventHandler<MenuItemClickedEventArgs>? ItemClicked;

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Expected O, but got Unknown
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Expected O, but got Unknown
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Expected O, but got Unknown
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Expected O, but got Unknown
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Expected O, but got Unknown
		//IL_0300: Unknown result type (might be due to invalid IL or missing references)
		//IL_0305: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02df: Expected O, but got Unknown
		//IL_038b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0383: Unknown result type (might be due to invalid IL or missing references)
		//IL_0318: Unknown result type (might be due to invalid IL or missing references)
		//IL_0310: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0322: Unknown result type (might be due to invalid IL or missing references)
		//IL_032e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0337: Expected O, but got Unknown
		//IL_03d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e5: Unknown result type (might be due to invalid IL or missing references)
		if (Items.Count == 0)
		{
			return;
		}
		float num = MinWidth;
		float num2 = 0f;
		SKPaint val = new SKPaint
		{
			TextSize = FontSize,
			IsAntialias = true
		};
		try
		{
			foreach (MenuItem item in Items)
			{
				if (item.IsSeparator)
				{
					num2 += SeparatorHeight;
					continue;
				}
				num2 += ItemHeight;
				SKRect val2 = default(SKRect);
				val.MeasureText(item.Text, ref val2);
				float num3 = ((SKRect)(ref val2)).Width + 50f;
				if (!string.IsNullOrEmpty(item.Shortcut))
				{
					val.MeasureText(item.Shortcut, ref val2);
					num3 += ((SKRect)(ref val2)).Width + 20f;
				}
				num = Math.Max(num, num3);
			}
			SKPoint position = Position;
			float x = ((SKPoint)(ref position)).X;
			position = Position;
			float y = ((SKPoint)(ref position)).Y;
			position = Position;
			float num4 = ((SKPoint)(ref position)).X + num;
			position = Position;
			_bounds = new SKRect(x, y, num4, ((SKPoint)(ref position)).Y + num2);
			SKPaint val3 = new SKPaint
			{
				ImageFilter = SKImageFilter.CreateDropShadow(0f, 2f, 8f, 8f, new SKColor((byte)0, (byte)0, (byte)0, (byte)40))
			};
			try
			{
				canvas.DrawRect(_bounds, val3);
				SKPaint val4 = new SKPaint
				{
					Color = BackgroundColor,
					Style = (SKPaintStyle)0
				};
				try
				{
					canvas.DrawRect(_bounds, val4);
					SKPaint val5 = new SKPaint
					{
						Color = new SKColor((byte)200, (byte)200, (byte)200),
						Style = (SKPaintStyle)1,
						StrokeWidth = 1f
					};
					try
					{
						canvas.DrawRect(_bounds, val5);
						float num5 = ((SKRect)(ref _bounds)).Top;
						val.Color = TextColor;
						SKRect val7 = default(SKRect);
						for (int i = 0; i < Items.Count; i++)
						{
							MenuItem menuItem = Items[i];
							if (menuItem.IsSeparator)
							{
								float num6 = num5 + SeparatorHeight / 2f;
								SKPaint val6 = new SKPaint
								{
									Color = SeparatorColor,
									StrokeWidth = 1f
								};
								try
								{
									canvas.DrawLine(((SKRect)(ref _bounds)).Left + 8f, num6, ((SKRect)(ref _bounds)).Right - 8f, num6, val6);
									num5 += SeparatorHeight;
								}
								finally
								{
									((IDisposable)val6)?.Dispose();
								}
								continue;
							}
							((SKRect)(ref val7))._002Ector(((SKRect)(ref _bounds)).Left, num5, ((SKRect)(ref _bounds)).Right, num5 + ItemHeight);
							if (i == _hoveredIndex && menuItem.IsEnabled)
							{
								SKPaint val8 = new SKPaint
								{
									Color = HoverBackgroundColor,
									Style = (SKPaintStyle)0
								};
								try
								{
									canvas.DrawRect(val7, val8);
								}
								finally
								{
									((IDisposable)val8)?.Dispose();
								}
							}
							if (menuItem.IsChecked)
							{
								SKPaint val9 = new SKPaint
								{
									Color = (menuItem.IsEnabled ? TextColor : DisabledTextColor),
									TextSize = FontSize,
									IsAntialias = true
								};
								try
								{
									canvas.DrawText("✓", ((SKRect)(ref _bounds)).Left + 8f, num5 + ItemHeight / 2f + 5f, val9);
								}
								finally
								{
									((IDisposable)val9)?.Dispose();
								}
							}
							val.Color = (menuItem.IsEnabled ? TextColor : DisabledTextColor);
							canvas.DrawText(menuItem.Text, ((SKRect)(ref _bounds)).Left + 28f, num5 + ItemHeight / 2f + 5f, val);
							if (!string.IsNullOrEmpty(menuItem.Shortcut))
							{
								val.Color = DisabledTextColor;
								SKRect val10 = default(SKRect);
								val.MeasureText(menuItem.Shortcut, ref val10);
								canvas.DrawText(menuItem.Shortcut, ((SKRect)(ref _bounds)).Right - ((SKRect)(ref val10)).Width - 12f, num5 + ItemHeight / 2f + 5f, val);
							}
							if (menuItem.SubItems.Count > 0)
							{
								canvas.DrawText("▸", ((SKRect)(ref _bounds)).Right - 16f, num5 + ItemHeight / 2f + 5f, val);
							}
							num5 += ItemHeight;
						}
					}
					finally
					{
						((IDisposable)val5)?.Dispose();
					}
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public override SkiaView? HitTest(float x, float y)
	{
		if (((SKRect)(ref _bounds)).Contains(x, y))
		{
			return this;
		}
		return null;
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		if (!((SKRect)(ref _bounds)).Contains(e.X, e.Y))
		{
			_hoveredIndex = -1;
			Invalidate();
			return;
		}
		float num = ((SKRect)(ref _bounds)).Top;
		int num2 = -1;
		for (int i = 0; i < Items.Count; i++)
		{
			MenuItem menuItem = Items[i];
			float num3 = (menuItem.IsSeparator ? SeparatorHeight : ItemHeight);
			if (e.Y >= num && e.Y < num + num3 && !menuItem.IsSeparator)
			{
				num2 = i;
				break;
			}
			num += num3;
		}
		if (num2 != _hoveredIndex)
		{
			_hoveredIndex = num2;
			Invalidate();
		}
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		if (_hoveredIndex >= 0 && _hoveredIndex < Items.Count)
		{
			MenuItem menuItem = Items[_hoveredIndex];
			if (menuItem.IsEnabled && !menuItem.IsSeparator)
			{
				menuItem.OnClicked();
				this.ItemClicked?.Invoke(this, new MenuItemClickedEventArgs(menuItem));
				e.Handled = true;
			}
		}
	}
}
