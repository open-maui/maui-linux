using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaContextMenu : SkiaView
{
	private readonly List<ContextMenuItem> _items;

	private readonly float _x;

	private readonly float _y;

	private int _hoveredIndex = -1;

	private SKRect[] _itemBounds = Array.Empty<SKRect>();

	private static readonly SKColor MenuBackground = new SKColor(byte.MaxValue, byte.MaxValue, byte.MaxValue);

	private static readonly SKColor MenuBackgroundDark = new SKColor((byte)48, (byte)48, (byte)48);

	private static readonly SKColor ItemHoverBackground = new SKColor((byte)227, (byte)242, (byte)253);

	private static readonly SKColor ItemHoverBackgroundDark = new SKColor((byte)80, (byte)80, (byte)80);

	private static readonly SKColor ItemTextColor = new SKColor((byte)33, (byte)33, (byte)33);

	private static readonly SKColor ItemTextColorDark = new SKColor((byte)224, (byte)224, (byte)224);

	private static readonly SKColor DisabledTextColor = new SKColor((byte)158, (byte)158, (byte)158);

	private static readonly SKColor SeparatorColor = new SKColor((byte)224, (byte)224, (byte)224);

	private static readonly SKColor ShadowColor = new SKColor((byte)0, (byte)0, (byte)0, (byte)40);

	private const float MenuPadding = 4f;

	private const float ItemHeight = 32f;

	private const float ItemPaddingH = 16f;

	private const float SeparatorHeight = 9f;

	private const float CornerRadius = 4f;

	private const float MinWidth = 120f;

	private bool _isDarkTheme;

	public SkiaContextMenu(float x, float y, List<ContextMenuItem> items, bool isDarkTheme = false)
	{
		_x = x;
		_y = y;
		_items = items;
		_isDarkTheme = isDarkTheme;
		base.IsFocusable = true;
	}

	public override void Draw(SKCanvas canvas)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Expected O, but got Unknown
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Expected O, but got Unknown
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Expected O, but got Unknown
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Expected O, but got Unknown
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_028b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0295: Unknown result type (might be due to invalid IL or missing references)
		//IL_023c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d4: Expected O, but got Unknown
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_025b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0264: Expected O, but got Unknown
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		float num = CalculateMenuWidth();
		float num2 = CalculateMenuHeight();
		float num3 = _x;
		float num4 = _y;
		SKRectI val = default(SKRectI);
		canvas.GetDeviceClipBounds(ref val);
		if (num3 + num > (float)((SKRectI)(ref val)).Right)
		{
			num3 = (float)((SKRectI)(ref val)).Right - num - 4f;
		}
		if (num4 + num2 > (float)((SKRectI)(ref val)).Bottom)
		{
			num4 = (float)((SKRectI)(ref val)).Bottom - num2 - 4f;
		}
		SKRect val2 = default(SKRect);
		((SKRect)(ref val2))._002Ector(num3, num4, num3 + num, num4 + num2);
		SKPaint val3 = new SKPaint
		{
			Color = ShadowColor,
			MaskFilter = SKMaskFilter.CreateBlur((SKBlurStyle)0, 4f)
		};
		try
		{
			canvas.DrawRoundRect(((SKRect)(ref val2)).Left + 2f, ((SKRect)(ref val2)).Top + 2f, num, num2, 4f, 4f, val3);
			SKPaint val4 = new SKPaint
			{
				Color = (_isDarkTheme ? MenuBackgroundDark : MenuBackground),
				IsAntialias = true
			};
			try
			{
				canvas.DrawRoundRect(val2, 4f, 4f, val4);
				SKPaint val5 = new SKPaint
				{
					Color = SeparatorColor,
					Style = (SKPaintStyle)1,
					StrokeWidth = 1f,
					IsAntialias = true
				};
				try
				{
					canvas.DrawRoundRect(val2, 4f, 4f, val5);
					_itemBounds = (SKRect[])(object)new SKRect[_items.Count];
					float num5 = num4 + 4f;
					SKRect val7 = default(SKRect);
					for (int i = 0; i < _items.Count; i++)
					{
						ContextMenuItem contextMenuItem = _items[i];
						if (contextMenuItem.IsSeparator)
						{
							float num6 = num5 + 4.5f;
							SKPaint val6 = new SKPaint
							{
								Color = SeparatorColor,
								StrokeWidth = 1f
							};
							try
							{
								canvas.DrawLine(num3 + 8f, num6, num3 + num - 8f, num6, val6);
								_itemBounds[i] = new SKRect(num3, num5, num3 + num, num5 + 9f);
								num5 += 9f;
							}
							finally
							{
								((IDisposable)val6)?.Dispose();
							}
							continue;
						}
						((SKRect)(ref val7))._002Ector(num3 + 4f, num5, num3 + num - 4f, num5 + 32f);
						_itemBounds[i] = val7;
						if (i == _hoveredIndex && contextMenuItem.IsEnabled)
						{
							SKPaint val8 = new SKPaint
							{
								Color = (_isDarkTheme ? ItemHoverBackgroundDark : ItemHoverBackground),
								IsAntialias = true
							};
							try
							{
								canvas.DrawRoundRect(val7, 4f, 4f, val8);
							}
							finally
							{
								((IDisposable)val8)?.Dispose();
							}
						}
						SKPaint val9 = new SKPaint
						{
							Color = ((!contextMenuItem.IsEnabled) ? DisabledTextColor : (_isDarkTheme ? ItemTextColorDark : ItemTextColor)),
							TextSize = 14f,
							IsAntialias = true,
							Typeface = SKTypeface.Default
						};
						try
						{
							float num7 = ((SKRect)(ref val7)).MidY + val9.TextSize / 3f;
							canvas.DrawText(contextMenuItem.Text, ((SKRect)(ref val7)).Left + 16f, num7, val9);
							num5 += 32f;
						}
						finally
						{
							((IDisposable)val9)?.Dispose();
						}
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

	private float CalculateMenuWidth()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected O, but got Unknown
		float num = 120f;
		SKPaint val = new SKPaint
		{
			TextSize = 14f,
			Typeface = SKTypeface.Default
		};
		try
		{
			foreach (ContextMenuItem item in _items)
			{
				if (!item.IsSeparator)
				{
					float val2 = val.MeasureText(item.Text) + 32f;
					num = Math.Max(num, val2);
				}
			}
			return num + 8f;
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private float CalculateMenuHeight()
	{
		float num = 8f;
		foreach (ContextMenuItem item in _items)
		{
			num += (item.IsSeparator ? 9f : 32f);
		}
		return num;
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		int hoveredIndex = _hoveredIndex;
		_hoveredIndex = -1;
		for (int i = 0; i < _itemBounds.Length; i++)
		{
			if (((SKRect)(ref _itemBounds[i])).Contains(e.X, e.Y) && !_items[i].IsSeparator)
			{
				_hoveredIndex = i;
				break;
			}
		}
		if (hoveredIndex != _hoveredIndex)
		{
			Invalidate();
		}
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		for (int i = 0; i < _itemBounds.Length; i++)
		{
			if (((SKRect)(ref _itemBounds[i])).Contains(e.X, e.Y))
			{
				ContextMenuItem contextMenuItem = _items[i];
				if (contextMenuItem.IsEnabled && !contextMenuItem.IsSeparator && contextMenuItem.Action != null)
				{
					LinuxDialogService.HideContextMenu();
					contextMenuItem.Action();
					return;
				}
			}
		}
		LinuxDialogService.HideContextMenu();
	}

	public override void OnKeyDown(KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			LinuxDialogService.HideContextMenu();
			e.Handled = true;
		}
	}
}
