using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaTimePicker : SkiaView
{
	public static readonly BindableProperty TimeProperty = BindableProperty.Create("Time", typeof(TimeSpan), typeof(SkiaTimePicker), (object)DateTime.Now.TimeOfDay, (BindingMode)1, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaTimePicker)(object)b).OnTimePropertyChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FormatProperty = BindableProperty.Create("Format", typeof(string), typeof(SkiaTimePicker), (object)"t", (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaTimePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TextColorProperty = BindableProperty.Create("TextColor", typeof(SKColor), typeof(SkiaTimePicker), (object)SKColors.Black, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaTimePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty BorderColorProperty = BindableProperty.Create("BorderColor", typeof(SKColor), typeof(SkiaTimePicker), (object)new SKColor((byte)189, (byte)189, (byte)189), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaTimePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ClockBackgroundColorProperty = BindableProperty.Create("ClockBackgroundColor", typeof(SKColor), typeof(SkiaTimePicker), (object)SKColors.White, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaTimePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ClockFaceColorProperty = BindableProperty.Create("ClockFaceColor", typeof(SKColor), typeof(SkiaTimePicker), (object)new SKColor((byte)245, (byte)245, (byte)245), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaTimePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty SelectedColorProperty = BindableProperty.Create("SelectedColor", typeof(SKColor), typeof(SkiaTimePicker), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaTimePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty HeaderColorProperty = BindableProperty.Create("HeaderColor", typeof(SKColor), typeof(SkiaTimePicker), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaTimePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FontSizeProperty = BindableProperty.Create("FontSize", typeof(float), typeof(SkiaTimePicker), (object)14f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaTimePicker)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create("CornerRadius", typeof(float), typeof(SkiaTimePicker), (object)4f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaTimePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private bool _isOpen;

	private int _selectedHour;

	private int _selectedMinute;

	private bool _isSelectingHours = true;

	private const float ClockSize = 280f;

	private const float ClockRadius = 100f;

	private const float HeaderHeight = 80f;

	private const float PopupHeight = 360f;

	public TimeSpan Time
	{
		get
		{
			return (TimeSpan)((BindableObject)this).GetValue(TimeProperty);
		}
		set
		{
			((BindableObject)this).SetValue(TimeProperty, (object)value);
		}
	}

	public string Format
	{
		get
		{
			return (string)((BindableObject)this).GetValue(FormatProperty);
		}
		set
		{
			((BindableObject)this).SetValue(FormatProperty, (object)value);
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

	public SKColor ClockBackgroundColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(ClockBackgroundColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(ClockBackgroundColorProperty, (object)value);
		}
	}

	public SKColor ClockFaceColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(ClockFaceColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(ClockFaceColorProperty, (object)value);
		}
	}

	public SKColor SelectedColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(SelectedColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(SelectedColorProperty, (object)value);
		}
	}

	public SKColor HeaderColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(HeaderColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(HeaderColorProperty, (object)value);
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
					SkiaView.RegisterPopupOverlay(this, DrawClockOverlay);
				}
				else
				{
					SkiaView.UnregisterPopupOverlay(this);
				}
				Invalidate();
			}
		}
	}

	public event EventHandler? TimeSelected;

	private SKRect GetPopupRect(SKRect pickerBounds)
	{
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		int num = LinuxApplication.Current?.MainWindow?.Width ?? 800;
		int num2 = LinuxApplication.Current?.MainWindow?.Height ?? 600;
		float num3 = ((SKRect)(ref pickerBounds)).Left;
		float num4 = ((SKRect)(ref pickerBounds)).Bottom + 4f;
		if (num3 + 280f > (float)num)
		{
			num3 = (float)num - 280f - 4f;
		}
		if (num3 < 0f)
		{
			num3 = 4f;
		}
		if (num4 + 360f > (float)num2)
		{
			num4 = ((SKRect)(ref pickerBounds)).Top - 360f - 4f;
		}
		if (num4 < 0f)
		{
			num4 = 4f;
		}
		return new SKRect(num3, num4, num3 + 280f, num4 + 360f);
	}

	public SkiaTimePicker()
	{
		base.IsFocusable = true;
		_selectedHour = DateTime.Now.Hour;
		_selectedMinute = DateTime.Now.Minute;
	}

	private void OnTimePropertyChanged()
	{
		_selectedHour = Time.Hours;
		_selectedMinute = Time.Minutes;
		this.TimeSelected?.Invoke(this, EventArgs.Empty);
		Invalidate();
	}

	private void DrawClockOverlay(SKCanvas canvas)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		if (_isOpen)
		{
			DrawClockPopup(canvas, base.ScreenBounds);
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
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Expected O, but got Unknown
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Expected O, but got Unknown
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Expected O, but got Unknown
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Expected O, but got Unknown
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Expected O, but got Unknown
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = (SKColor)(base.IsEnabled ? base.BackgroundColor : new SKColor((byte)245, (byte)245, (byte)245)),
			Style = (SKPaintStyle)0,
			IsAntialias = true
		};
		try
		{
			canvas.DrawRoundRect(new SKRoundRect(bounds, CornerRadius), val);
			SKPaint val2 = new SKPaint
			{
				Color = (base.IsFocused ? SelectedColor : BorderColor),
				Style = (SKPaintStyle)1,
				StrokeWidth = ((!base.IsFocused) ? 1 : 2),
				IsAntialias = true
			};
			try
			{
				canvas.DrawRoundRect(new SKRoundRect(bounds, CornerRadius), val2);
				SKFont val3 = new SKFont(SKTypeface.Default, FontSize, 1f, 0f);
				try
				{
					SKPaint val4 = new SKPaint(val3);
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
					val4.Color = color;
					val4.IsAntialias = true;
					SKPaint val5 = val4;
					try
					{
						string text = DateTime.Today.Add(Time).ToString(Format);
						SKRect val6 = default(SKRect);
						val5.MeasureText(text, ref val6);
						canvas.DrawText(text, ((SKRect)(ref bounds)).Left + 12f, ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val6)).MidY, val5);
						DrawClockIcon(canvas, new SKRect(((SKRect)(ref bounds)).Right - 36f, ((SKRect)(ref bounds)).MidY - 10f, ((SKRect)(ref bounds)).Right - 12f, ((SKRect)(ref bounds)).MidY + 10f));
					}
					finally
					{
						((IDisposable)val5)?.Dispose();
					}
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

	private void DrawClockIcon(SKCanvas canvas, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Expected O, but got Unknown
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
		val.StrokeWidth = 1.5f;
		val.IsAntialias = true;
		SKPaint val2 = val;
		try
		{
			float num = Math.Min(((SKRect)(ref bounds)).Width, ((SKRect)(ref bounds)).Height) / 2f - 2f;
			canvas.DrawCircle(((SKRect)(ref bounds)).MidX, ((SKRect)(ref bounds)).MidY, num, val2);
			canvas.DrawLine(((SKRect)(ref bounds)).MidX, ((SKRect)(ref bounds)).MidY, ((SKRect)(ref bounds)).MidX, ((SKRect)(ref bounds)).MidY - num * 0.5f, val2);
			canvas.DrawLine(((SKRect)(ref bounds)).MidX, ((SKRect)(ref bounds)).MidY, ((SKRect)(ref bounds)).MidX + num * 0.4f, ((SKRect)(ref bounds)).MidY, val2);
			val2.Style = (SKPaintStyle)0;
			canvas.DrawCircle(((SKRect)(ref bounds)).MidX, ((SKRect)(ref bounds)).MidY, 1.5f, val2);
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private void DrawClockPopup(SKCanvas canvas, SKRect bounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected O, but got Unknown
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Expected O, but got Unknown
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Expected O, but got Unknown
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Expected O, but got Unknown
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Expected O, but got Unknown
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Expected O, but got Unknown
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		SKRect popupRect = GetPopupRect(bounds);
		SKPaint val = new SKPaint
		{
			Color = new SKColor((byte)0, (byte)0, (byte)0, (byte)40),
			MaskFilter = SKMaskFilter.CreateBlur((SKBlurStyle)0, 4f),
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRoundRect(new SKRoundRect(new SKRect(((SKRect)(ref popupRect)).Left + 2f, ((SKRect)(ref popupRect)).Top + 2f, ((SKRect)(ref popupRect)).Right + 2f, ((SKRect)(ref popupRect)).Bottom + 2f), CornerRadius), val);
			SKPaint val2 = new SKPaint
			{
				Color = ClockBackgroundColor,
				Style = (SKPaintStyle)0,
				IsAntialias = true
			};
			try
			{
				canvas.DrawRoundRect(new SKRoundRect(popupRect, CornerRadius), val2);
				SKPaint val3 = new SKPaint
				{
					Color = BorderColor,
					Style = (SKPaintStyle)1,
					StrokeWidth = 1f,
					IsAntialias = true
				};
				try
				{
					canvas.DrawRoundRect(new SKRoundRect(popupRect, CornerRadius), val3);
					DrawTimeHeader(canvas, new SKRect(((SKRect)(ref popupRect)).Left, ((SKRect)(ref popupRect)).Top, ((SKRect)(ref popupRect)).Right, ((SKRect)(ref popupRect)).Top + 80f));
					DrawClockFace(canvas, new SKRect(((SKRect)(ref popupRect)).Left, ((SKRect)(ref popupRect)).Top + 80f, ((SKRect)(ref popupRect)).Right, ((SKRect)(ref popupRect)).Bottom));
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

	private void DrawTimeHeader(SKCanvas canvas, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Expected O, but got Unknown
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Expected O, but got Unknown
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Expected O, but got Unknown
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Expected O, but got Unknown
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = HeaderColor,
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.Save();
			canvas.ClipRoundRect(new SKRoundRect(new SKRect(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Top + CornerRadius * 2f), CornerRadius), (SKClipOperation)1, false);
			canvas.DrawRect(bounds, val);
			canvas.Restore();
			canvas.DrawRect(new SKRect(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top + CornerRadius, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom), val);
			SKFont val2 = new SKFont(SKTypeface.Default, 32f, 1f, 0f);
			try
			{
				SKPaint val3 = new SKPaint(val2)
				{
					Color = SKColors.White,
					IsAntialias = true
				};
				try
				{
					SKPaint val4 = new SKPaint(val2)
					{
						Color = new SKColor(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)150),
						IsAntialias = true
					};
					try
					{
						string text = _selectedHour.ToString("D2");
						string text2 = _selectedMinute.ToString("D2");
						SKPaint val5 = (_isSelectingHours ? val3 : val4);
						SKPaint val6 = (_isSelectingHours ? val4 : val3);
						SKRect val7 = default(SKRect);
						SKRect val8 = default(SKRect);
						SKRect val9 = default(SKRect);
						val5.MeasureText(text, ref val7);
						val3.MeasureText(":", ref val8);
						val6.MeasureText(text2, ref val9);
						float num = ((SKRect)(ref val7)).Width + ((SKRect)(ref val8)).Width + ((SKRect)(ref val9)).Width + 8f;
						float num2 = ((SKRect)(ref bounds)).MidX - num / 2f;
						float num3 = ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val7)).MidY;
						canvas.DrawText(text, num2, num3, val5);
						canvas.DrawText(":", num2 + ((SKRect)(ref val7)).Width + 4f, num3, val3);
						canvas.DrawText(text2, num2 + ((SKRect)(ref val7)).Width + ((SKRect)(ref val8)).Width + 8f, num3, val6);
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
				((IDisposable)val2)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void DrawClockFace(SKCanvas canvas, SKRect bounds)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Expected O, but got Unknown
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Expected O, but got Unknown
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_0217: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		//IL_0228: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Expected O, but got Unknown
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Expected O, but got Unknown
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		float midX = ((SKRect)(ref bounds)).MidX;
		float midY = ((SKRect)(ref bounds)).MidY;
		SKPaint val = new SKPaint
		{
			Color = ClockFaceColor,
			Style = (SKPaintStyle)0,
			IsAntialias = true
		};
		try
		{
			canvas.DrawCircle(midX, midY, 120f, val);
			SKFont val2 = new SKFont(SKTypeface.Default, 14f, 1f, 0f);
			try
			{
				SKPaint val3 = new SKPaint(val2)
				{
					Color = TextColor,
					IsAntialias = true
				};
				try
				{
					if (_isSelectingHours)
					{
						for (int i = 1; i <= 12; i++)
						{
							double num = (double)(i * 30 - 90) * Math.PI / 180.0;
							float num2 = midX + (float)(100.0 * Math.Cos(num));
							float num3 = midY + (float)(100.0 * Math.Sin(num));
							if (_selectedHour % 12 == i % 12)
							{
								SKPaint val4 = new SKPaint
								{
									Color = SelectedColor,
									Style = (SKPaintStyle)0,
									IsAntialias = true
								};
								try
								{
									canvas.DrawCircle(num2, num3, 18f, val4);
									val3.Color = SKColors.White;
								}
								finally
								{
									((IDisposable)val4)?.Dispose();
								}
							}
							else
							{
								val3.Color = TextColor;
							}
							SKRect val5 = default(SKRect);
							val3.MeasureText(i.ToString(), ref val5);
							canvas.DrawText(i.ToString(), num2 - ((SKRect)(ref val5)).MidX, num3 - ((SKRect)(ref val5)).MidY, val3);
						}
						DrawClockHand(canvas, midX, midY, _selectedHour % 12 * 30 - 90, 82f);
						return;
					}
					for (int j = 0; j < 12; j++)
					{
						int num4 = j * 5;
						double num5 = (double)(num4 * 6 - 90) * Math.PI / 180.0;
						float num6 = midX + (float)(100.0 * Math.Cos(num5));
						float num7 = midY + (float)(100.0 * Math.Sin(num5));
						if (_selectedMinute / 5 == j)
						{
							SKPaint val6 = new SKPaint
							{
								Color = SelectedColor,
								Style = (SKPaintStyle)0,
								IsAntialias = true
							};
							try
							{
								canvas.DrawCircle(num6, num7, 18f, val6);
								val3.Color = SKColors.White;
							}
							finally
							{
								((IDisposable)val6)?.Dispose();
							}
						}
						else
						{
							val3.Color = TextColor;
						}
						SKRect val7 = default(SKRect);
						val3.MeasureText(num4.ToString("D2"), ref val7);
						canvas.DrawText(num4.ToString("D2"), num6 - ((SKRect)(ref val7)).MidX, num7 - ((SKRect)(ref val7)).MidY, val3);
					}
					DrawClockHand(canvas, midX, midY, _selectedMinute * 6 - 90, 82f);
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

	private void DrawClockHand(SKCanvas canvas, float centerX, float centerY, float angleDegrees, float length)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		double num = (double)angleDegrees * Math.PI / 180.0;
		SKPaint val = new SKPaint
		{
			Color = SelectedColor,
			Style = (SKPaintStyle)1,
			StrokeWidth = 2f,
			IsAntialias = true
		};
		try
		{
			canvas.DrawLine(centerX, centerY, centerX + (float)((double)length * Math.Cos(num)), centerY + (float)((double)length * Math.Sin(num)), val);
			val.Style = (SKPaintStyle)0;
			canvas.DrawCircle(centerX, centerY, 6f, val);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		if (!base.IsEnabled)
		{
			return;
		}
		if (IsOpen)
		{
			SKRect screenBounds = base.ScreenBounds;
			SKRect popupRect = GetPopupRect(screenBounds);
			SKRect val = default(SKRect);
			((SKRect)(ref val))._002Ector(((SKRect)(ref popupRect)).Left, ((SKRect)(ref popupRect)).Top, ((SKRect)(ref popupRect)).Right, ((SKRect)(ref popupRect)).Top + 80f);
			if (((SKRect)(ref val)).Contains(e.X, e.Y))
			{
				_isSelectingHours = e.X < ((SKRect)(ref popupRect)).Left + 140f;
				Invalidate();
				return;
			}
			float num = ((SKRect)(ref popupRect)).Left + 140f;
			float num2 = ((SKRect)(ref popupRect)).Top + 80f + 140f;
			float num3 = e.X - num;
			float num4 = e.Y - num2;
			if (Math.Sqrt(num3 * num3 + num4 * num4) <= 120.0)
			{
				double num5 = Math.Atan2(num4, num3) * 180.0 / Math.PI + 90.0;
				if (num5 < 0.0)
				{
					num5 += 360.0;
				}
				if (_isSelectingHours)
				{
					_selectedHour = (int)Math.Round(num5 / 30.0) % 12;
					if (_selectedHour == 0)
					{
						_selectedHour = 12;
					}
					if (Time.Hours >= 12 && _selectedHour != 12)
					{
						_selectedHour += 12;
					}
					else if (Time.Hours < 12 && _selectedHour == 12)
					{
						_selectedHour = 0;
					}
					_isSelectingHours = false;
				}
				else
				{
					_selectedMinute = (int)Math.Round(num5 / 6.0) % 60;
					Time = new TimeSpan(_selectedHour, _selectedMinute, 0);
					IsOpen = false;
				}
				Invalidate();
				return;
			}
			if (((SKRect)(ref screenBounds)).Contains(e.X, e.Y))
			{
				IsOpen = false;
			}
		}
		else
		{
			IsOpen = true;
			_isSelectingHours = true;
		}
		Invalidate();
	}

	public override void OnFocusLost()
	{
		base.OnFocusLost();
		if (IsOpen)
		{
			IsOpen = false;
		}
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
			if (IsOpen)
			{
				if (_isSelectingHours)
				{
					_isSelectingHours = false;
				}
				else
				{
					Time = new TimeSpan(_selectedHour, _selectedMinute, 0);
					IsOpen = false;
				}
			}
			else
			{
				IsOpen = true;
				_isSelectingHours = true;
			}
			e.Handled = true;
			break;
		case Key.Escape:
			if (IsOpen)
			{
				IsOpen = false;
				e.Handled = true;
			}
			break;
		case Key.Up:
			if (_isSelectingHours)
			{
				_selectedHour = (_selectedHour + 1) % 24;
			}
			else
			{
				_selectedMinute = (_selectedMinute + 1) % 60;
			}
			e.Handled = true;
			break;
		case Key.Down:
			if (_isSelectingHours)
			{
				_selectedHour = (_selectedHour - 1 + 24) % 24;
			}
			else
			{
				_selectedMinute = (_selectedMinute - 1 + 60) % 60;
			}
			e.Handled = true;
			break;
		case Key.Left:
		case Key.Right:
			_isSelectingHours = !_isSelectingHours;
			e.Handled = true;
			break;
		}
		Invalidate();
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
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		SKRect screenBounds = base.ScreenBounds;
		if (((SKRect)(ref screenBounds)).Contains(x, y))
		{
			return true;
		}
		if (_isOpen)
		{
			SKRect popupRect = GetPopupRect(screenBounds);
			return ((SKRect)(ref popupRect)).Contains(x, y);
		}
		return false;
	}
}
