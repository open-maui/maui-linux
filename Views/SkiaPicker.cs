using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaPicker : SkiaView
{
	public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create("SelectedIndex", typeof(int), typeof(SkiaPicker), (object)(-1), (BindingMode)1, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaPicker)(object)b).OnSelectedIndexChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TitleProperty = BindableProperty.Create("Title", typeof(string), typeof(SkiaPicker), (object)"", (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaPicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TextColorProperty = BindableProperty.Create("TextColor", typeof(SKColor), typeof(SkiaPicker), (object)SKColors.Black, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaPicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TitleColorProperty = BindableProperty.Create("TitleColor", typeof(SKColor), typeof(SkiaPicker), (object)new SKColor((byte)128, (byte)128, (byte)128), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaPicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty BorderColorProperty = BindableProperty.Create("BorderColor", typeof(SKColor), typeof(SkiaPicker), (object)new SKColor((byte)189, (byte)189, (byte)189), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaPicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty DropdownBackgroundColorProperty = BindableProperty.Create("DropdownBackgroundColor", typeof(SKColor), typeof(SkiaPicker), (object)SKColors.White, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaPicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty SelectedItemBackgroundColorProperty = BindableProperty.Create("SelectedItemBackgroundColor", typeof(SKColor), typeof(SkiaPicker), (object)new SKColor((byte)33, (byte)150, (byte)243, (byte)48), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaPicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty HoverItemBackgroundColorProperty = BindableProperty.Create("HoverItemBackgroundColor", typeof(SKColor), typeof(SkiaPicker), (object)new SKColor((byte)224, (byte)224, (byte)224), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaPicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create("FontFamily", typeof(string), typeof(SkiaPicker), (object)"Sans", (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaPicker)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FontSizeProperty = BindableProperty.Create("FontSize", typeof(float), typeof(SkiaPicker), (object)14f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaPicker)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ItemHeightProperty = BindableProperty.Create("ItemHeight", typeof(float), typeof(SkiaPicker), (object)40f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaPicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create("CornerRadius", typeof(float), typeof(SkiaPicker), (object)4f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaPicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private readonly List<string> _items = new List<string>();

	private bool _isOpen;

	private float _dropdownMaxHeight = 200f;

	private int _hoveredItemIndex = -1;

	public int SelectedIndex
	{
		get
		{
			return (int)((BindableObject)this).GetValue(SelectedIndexProperty);
		}
		set
		{
			((BindableObject)this).SetValue(SelectedIndexProperty, (object)value);
		}
	}

	public string Title
	{
		get
		{
			return (string)((BindableObject)this).GetValue(TitleProperty);
		}
		set
		{
			((BindableObject)this).SetValue(TitleProperty, (object)value);
		}
	}

	public SKColor TextColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(TextColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(TextColorProperty, (object)value);
		}
	}

	public SKColor TitleColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(TitleColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(TitleColorProperty, (object)value);
		}
	}

	public SKColor BorderColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(BorderColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(BorderColorProperty, (object)value);
		}
	}

	public SKColor DropdownBackgroundColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(DropdownBackgroundColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(DropdownBackgroundColorProperty, (object)value);
		}
	}

	public SKColor SelectedItemBackgroundColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(SelectedItemBackgroundColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(SelectedItemBackgroundColorProperty, (object)value);
		}
	}

	public SKColor HoverItemBackgroundColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(HoverItemBackgroundColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(HoverItemBackgroundColorProperty, (object)value);
		}
	}

	public string FontFamily
	{
		get
		{
			return (string)((BindableObject)this).GetValue(FontFamilyProperty);
		}
		set
		{
			((BindableObject)this).SetValue(FontFamilyProperty, (object)value);
		}
	}

	public float FontSize
	{
		get
		{
			return (float)((BindableObject)this).GetValue(FontSizeProperty);
		}
		set
		{
			((BindableObject)this).SetValue(FontSizeProperty, (object)value);
		}
	}

	public float ItemHeight
	{
		get
		{
			return (float)((BindableObject)this).GetValue(ItemHeightProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ItemHeightProperty, (object)value);
		}
	}

	public float CornerRadius
	{
		get
		{
			return (float)((BindableObject)this).GetValue(CornerRadiusProperty);
		}
		set
		{
			((BindableObject)this).SetValue(CornerRadiusProperty, (object)value);
		}
	}

	public IList<string> Items => _items;

	public string? SelectedItem
	{
		get
		{
			if (SelectedIndex < 0 || SelectedIndex >= _items.Count)
			{
				return null;
			}
			return _items[SelectedIndex];
		}
	}

	public bool IsOpen
	{
		get
		{
			return _isOpen;
		}
		set
		{
			if (_isOpen != value)
			{
				_isOpen = value;
				if (_isOpen)
				{
					SkiaView.RegisterPopupOverlay(this, DrawDropdownOverlay);
				}
				else
				{
					SkiaView.UnregisterPopupOverlay(this);
				}
				Invalidate();
			}
		}
	}

	public event EventHandler? SelectedIndexChanged;

	public SkiaPicker()
	{
		base.IsFocusable = true;
	}

	private void OnSelectedIndexChanged()
	{
		this.SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
		Invalidate();
	}

	public void SetItems(IEnumerable<string> items)
	{
		_items.Clear();
		_items.AddRange(items);
		if (SelectedIndex >= _items.Count)
		{
			SelectedIndex = ((_items.Count <= 0) ? (-1) : 0);
		}
		Invalidate();
	}

	private void DrawDropdownOverlay(SKCanvas canvas)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		if (_items.Count != 0 && _isOpen)
		{
			DrawDropdown(canvas, base.ScreenBounds);
		}
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		DrawPickerButton(canvas, bounds);
	}

	private void DrawPickerButton(SKCanvas canvas, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Expected O, but got Unknown
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Expected O, but got Unknown
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Expected O, but got Unknown
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Expected O, but got Unknown
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = (SKColor)(base.IsEnabled ? base.BackgroundColor : new SKColor((byte)245, (byte)245, (byte)245)),
			Style = (SKPaintStyle)0,
			IsAntialias = true
		};
		try
		{
			SKRoundRect val2 = new SKRoundRect(bounds, CornerRadius);
			canvas.DrawRoundRect(val2, val);
			SKPaint val3 = new SKPaint
			{
				Color = (SKColor)(base.IsFocused ? new SKColor((byte)33, (byte)150, (byte)243) : BorderColor),
				Style = (SKPaintStyle)1,
				StrokeWidth = ((!base.IsFocused) ? 1 : 2),
				IsAntialias = true
			};
			try
			{
				canvas.DrawRoundRect(val2, val3);
				SKFont val4 = new SKFont(SKTypeface.Default, FontSize, 1f, 0f);
				try
				{
					SKPaint val5 = new SKPaint(val4)
					{
						IsAntialias = true
					};
					try
					{
						string text;
						if (SelectedIndex >= 0 && SelectedIndex < _items.Count)
						{
							text = _items[SelectedIndex];
							SKColor color;
							if (!base.IsEnabled)
							{
								SKColor textColor = TextColor;
								color = ((SKColor)(ref textColor)).WithAlpha((byte)128);
							}
							else
							{
								color = TextColor;
							}
							val5.Color = color;
						}
						else
						{
							text = Title;
							val5.Color = TitleColor;
						}
						SKRect val6 = default(SKRect);
						val5.MeasureText(text, ref val6);
						float num = ((SKRect)(ref bounds)).Left + 12f;
						float num2 = ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val6)).MidY;
						canvas.DrawText(text, num, num2, val5);
						DrawDropdownArrow(canvas, bounds);
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

	private void DrawDropdownArrow(SKCanvas canvas, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Expected O, but got Unknown
		SKPaint val = new SKPaint();
		SKColor color;
		if (!base.IsEnabled)
		{
			SKColor textColor = TextColor;
			color = ((SKColor)(ref textColor)).WithAlpha((byte)128);
		}
		else
		{
			color = TextColor;
		}
		val.Color = color;
		val.Style = (SKPaintStyle)1;
		val.StrokeWidth = 2f;
		val.IsAntialias = true;
		val.StrokeCap = (SKStrokeCap)1;
		SKPaint val2 = val;
		try
		{
			float num = 6f;
			float num2 = ((SKRect)(ref bounds)).Right - 20f;
			float midY = ((SKRect)(ref bounds)).MidY;
			SKPath val3 = new SKPath();
			try
			{
				if (_isOpen)
				{
					val3.MoveTo(num2 - num, midY + num / 2f);
					val3.LineTo(num2, midY - num / 2f);
					val3.LineTo(num2 + num, midY + num / 2f);
				}
				else
				{
					val3.MoveTo(num2 - num, midY - num / 2f);
					val3.LineTo(num2, midY + num / 2f);
					val3.LineTo(num2 + num, midY - num / 2f);
				}
				canvas.DrawPath(val3, val2);
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

	private void DrawDropdown(SKCanvas canvas, SKRect bounds)
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Expected O, but got Unknown
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Expected O, but got Unknown
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Expected O, but got Unknown
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Expected O, but got Unknown
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Expected O, but got Unknown
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Expected O, but got Unknown
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Expected O, but got Unknown
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Expected O, but got Unknown
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Expected O, but got Unknown
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_020f: Expected O, but got Unknown
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		//IL_0242: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Expected O, but got Unknown
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_024c: Unknown result type (might be due to invalid IL or missing references)
		if (_items.Count == 0)
		{
			return;
		}
		float num = Math.Min((float)_items.Count * ItemHeight, _dropdownMaxHeight);
		SKRect val = default(SKRect);
		((SKRect)(ref val))._002Ector(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Bottom + 4f, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom + 4f + num);
		SKPaint val2 = new SKPaint
		{
			Color = new SKColor((byte)0, (byte)0, (byte)0, (byte)40),
			MaskFilter = SKMaskFilter.CreateBlur((SKBlurStyle)0, 4f),
			Style = (SKPaintStyle)0
		};
		try
		{
			SKRect val3 = new SKRect(((SKRect)(ref val)).Left + 2f, ((SKRect)(ref val)).Top + 2f, ((SKRect)(ref val)).Right + 2f, ((SKRect)(ref val)).Bottom + 2f);
			canvas.DrawRoundRect(new SKRoundRect(val3, CornerRadius), val2);
			SKPaint val4 = new SKPaint
			{
				Color = DropdownBackgroundColor,
				Style = (SKPaintStyle)0,
				IsAntialias = true
			};
			try
			{
				canvas.DrawRoundRect(new SKRoundRect(val, CornerRadius), val4);
				SKPaint val5 = new SKPaint
				{
					Color = BorderColor,
					Style = (SKPaintStyle)1,
					StrokeWidth = 1f,
					IsAntialias = true
				};
				try
				{
					canvas.DrawRoundRect(new SKRoundRect(val, CornerRadius), val5);
					canvas.Save();
					canvas.ClipRoundRect(new SKRoundRect(val, CornerRadius), (SKClipOperation)1, false);
					SKFont val6 = new SKFont(SKTypeface.Default, FontSize, 1f, 0f);
					try
					{
						SKPaint val7 = new SKPaint(val6)
						{
							Color = TextColor,
							IsAntialias = true
						};
						try
						{
							SKRect val8 = default(SKRect);
							for (int i = 0; i < _items.Count; i++)
							{
								float num2 = ((SKRect)(ref val)).Top + (float)i * ItemHeight;
								if (num2 > ((SKRect)(ref val)).Bottom)
								{
									break;
								}
								((SKRect)(ref val8))._002Ector(((SKRect)(ref val)).Left, num2, ((SKRect)(ref val)).Right, num2 + ItemHeight);
								if (i == SelectedIndex)
								{
									SKPaint val9 = new SKPaint
									{
										Color = SelectedItemBackgroundColor,
										Style = (SKPaintStyle)0
									};
									try
									{
										canvas.DrawRect(val8, val9);
									}
									finally
									{
										((IDisposable)val9)?.Dispose();
									}
								}
								else if (i == _hoveredItemIndex)
								{
									SKPaint val10 = new SKPaint
									{
										Color = HoverItemBackgroundColor,
										Style = (SKPaintStyle)0
									};
									try
									{
										canvas.DrawRect(val8, val10);
									}
									finally
									{
										((IDisposable)val10)?.Dispose();
									}
								}
								SKRect val11 = default(SKRect);
								val7.MeasureText(_items[i], ref val11);
								float num3 = ((SKRect)(ref val8)).Left + 12f;
								float num4 = ((SKRect)(ref val8)).MidY - ((SKRect)(ref val11)).MidY;
								canvas.DrawText(_items[i], num3, num4, val7);
							}
							canvas.Restore();
						}
						finally
						{
							((IDisposable)val7)?.Dispose();
						}
					}
					finally
					{
						((IDisposable)val6)?.Dispose();
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
			((IDisposable)val2)?.Dispose();
		}
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		if (!base.IsEnabled)
		{
			return;
		}
		if (IsOpen)
		{
			SKRect screenBounds = base.ScreenBounds;
			float num = ((SKRect)(ref screenBounds)).Bottom + 4f;
			if (e.Y >= num)
			{
				int num2 = (int)((e.Y - num) / ItemHeight);
				if (num2 >= 0 && num2 < _items.Count)
				{
					SelectedIndex = num2;
				}
			}
			IsOpen = false;
		}
		else
		{
			IsOpen = true;
		}
		Invalidate();
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (!_isOpen)
		{
			return;
		}
		SKRect screenBounds = base.ScreenBounds;
		float num = ((SKRect)(ref screenBounds)).Bottom + 4f;
		if (e.Y >= num)
		{
			int num2 = (int)((e.Y - num) / ItemHeight);
			if (num2 != _hoveredItemIndex && num2 >= 0 && num2 < _items.Count)
			{
				_hoveredItemIndex = num2;
				Invalidate();
			}
		}
		else if (_hoveredItemIndex != -1)
		{
			_hoveredItemIndex = -1;
			Invalidate();
		}
	}

	public override void OnPointerExited(PointerEventArgs e)
	{
		_hoveredItemIndex = -1;
		Invalidate();
	}

	public override void OnKeyDown(KeyEventArgs e)
	{
		if (!base.IsEnabled)
		{
			return;
		}
		switch (e.Key)
		{
		case Key.Enter:
		case Key.Space:
			IsOpen = !IsOpen;
			e.Handled = true;
			Invalidate();
			break;
		case Key.Escape:
			if (IsOpen)
			{
				IsOpen = false;
				e.Handled = true;
				Invalidate();
			}
			break;
		case Key.Up:
			if (SelectedIndex > 0)
			{
				SelectedIndex--;
				e.Handled = true;
			}
			break;
		case Key.Down:
			if (SelectedIndex < _items.Count - 1)
			{
				SelectedIndex++;
				e.Handled = true;
			}
			break;
		}
	}

	public override void OnFocusLost()
	{
		base.OnFocusLost();
		if (IsOpen)
		{
			IsOpen = false;
		}
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		return new SKSize((((SKSize)(ref availableSize)).Width < float.MaxValue) ? Math.Min(((SKSize)(ref availableSize)).Width, 200f) : 200f, 40f);
	}

	protected override bool HitTestPopupArea(float x, float y)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		SKRect screenBounds = base.ScreenBounds;
		if (((SKRect)(ref screenBounds)).Contains(x, y))
		{
			return true;
		}
		if (_isOpen && _items.Count > 0)
		{
			float num = Math.Min((float)_items.Count * ItemHeight, _dropdownMaxHeight);
			SKRect val = default(SKRect);
			((SKRect)(ref val))._002Ector(((SKRect)(ref screenBounds)).Left, ((SKRect)(ref screenBounds)).Bottom + 4f, ((SKRect)(ref screenBounds)).Right, ((SKRect)(ref screenBounds)).Bottom + 4f + num);
			return ((SKRect)(ref val)).Contains(x, y);
		}
		return false;
	}
}
