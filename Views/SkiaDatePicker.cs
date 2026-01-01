using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaDatePicker : SkiaView
{
	public static readonly BindableProperty DateProperty = BindableProperty.Create("Date", typeof(DateTime), typeof(SkiaDatePicker), (object)DateTime.Today, (BindingMode)1, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaDatePicker)(object)b).OnDatePropertyChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty MinimumDateProperty = BindableProperty.Create("MinimumDate", typeof(DateTime), typeof(SkiaDatePicker), (object)new DateTime(1900, 1, 1), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaDatePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty MaximumDateProperty = BindableProperty.Create("MaximumDate", typeof(DateTime), typeof(SkiaDatePicker), (object)new DateTime(2100, 12, 31), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaDatePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FormatProperty = BindableProperty.Create("Format", typeof(string), typeof(SkiaDatePicker), (object)"d", (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaDatePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TextColorProperty = BindableProperty.Create("TextColor", typeof(SKColor), typeof(SkiaDatePicker), (object)SKColors.Black, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaDatePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty BorderColorProperty = BindableProperty.Create("BorderColor", typeof(SKColor), typeof(SkiaDatePicker), (object)new SKColor((byte)189, (byte)189, (byte)189), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaDatePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CalendarBackgroundColorProperty = BindableProperty.Create("CalendarBackgroundColor", typeof(SKColor), typeof(SkiaDatePicker), (object)SKColors.White, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaDatePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty SelectedDayColorProperty = BindableProperty.Create("SelectedDayColor", typeof(SKColor), typeof(SkiaDatePicker), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaDatePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TodayColorProperty = BindableProperty.Create("TodayColor", typeof(SKColor), typeof(SkiaDatePicker), (object)new SKColor((byte)33, (byte)150, (byte)243, (byte)64), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaDatePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty HeaderColorProperty = BindableProperty.Create("HeaderColor", typeof(SKColor), typeof(SkiaDatePicker), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaDatePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty DisabledDayColorProperty = BindableProperty.Create("DisabledDayColor", typeof(SKColor), typeof(SkiaDatePicker), (object)new SKColor((byte)189, (byte)189, (byte)189), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaDatePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FontSizeProperty = BindableProperty.Create("FontSize", typeof(float), typeof(SkiaDatePicker), (object)14f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaDatePicker)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create("CornerRadius", typeof(float), typeof(SkiaDatePicker), (object)4f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaDatePicker)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private DateTime _displayMonth;

	private bool _isOpen;

	private const float CalendarWidth = 280f;

	private const float CalendarHeight = 320f;

	private const float HeaderHeight = 48f;

	public DateTime Date
	{
		get
		{
			return (DateTime)((BindableObject)this).GetValue(DateProperty);
		}
		set
		{
			((BindableObject)this).SetValue(DateProperty, (object)ClampDate(value));
		}
	}

	public DateTime MinimumDate
	{
		get
		{
			return (DateTime)((BindableObject)this).GetValue(MinimumDateProperty);
		}
		set
		{
			((BindableObject)this).SetValue(MinimumDateProperty, (object)value);
		}
	}

	public DateTime MaximumDate
	{
		get
		{
			return (DateTime)((BindableObject)this).GetValue(MaximumDateProperty);
		}
		set
		{
			((BindableObject)this).SetValue(MaximumDateProperty, (object)value);
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

	public SKColor CalendarBackgroundColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(CalendarBackgroundColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(CalendarBackgroundColorProperty, (object)value);
		}
	}

	public SKColor SelectedDayColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(SelectedDayColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(SelectedDayColorProperty, (object)value);
		}
	}

	public SKColor TodayColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(TodayColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(TodayColorProperty, (object)value);
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

	public SKColor DisabledDayColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(DisabledDayColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(DisabledDayColorProperty, (object)value);
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
					SkiaView.RegisterPopupOverlay(this, DrawCalendarOverlay);
				}
				else
				{
					SkiaView.UnregisterPopupOverlay(this);
				}
				Invalidate();
			}
		}
	}

	public event EventHandler? DateSelected;

	private SKRect GetCalendarRect(SKRect pickerBounds)
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
		if (num4 + 320f > (float)num2)
		{
			num4 = ((SKRect)(ref pickerBounds)).Top - 320f - 4f;
		}
		if (num4 < 0f)
		{
			num4 = 4f;
		}
		return new SKRect(num3, num4, num3 + 280f, num4 + 320f);
	}

	public SkiaDatePicker()
	{
		base.IsFocusable = true;
		_displayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
	}

	private void OnDatePropertyChanged()
	{
		_displayMonth = new DateTime(Date.Year, Date.Month, 1);
		this.DateSelected?.Invoke(this, EventArgs.Empty);
		Invalidate();
	}

	private DateTime ClampDate(DateTime date)
	{
		if (date < MinimumDate)
		{
			return MinimumDate;
		}
		if (date > MaximumDate)
		{
			return MaximumDate;
		}
		return date;
	}

	private void DrawCalendarOverlay(SKCanvas canvas)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		if (_isOpen)
		{
			DrawCalendar(canvas, base.ScreenBounds);
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
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
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
				Color = (base.IsFocused ? SelectedDayColor : BorderColor),
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
						string text = Date.ToString(Format);
						SKRect val6 = default(SKRect);
						val5.MeasureText(text, ref val6);
						canvas.DrawText(text, ((SKRect)(ref bounds)).Left + 12f, ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val6)).MidY, val5);
						DrawCalendarIcon(canvas, new SKRect(((SKRect)(ref bounds)).Right - 36f, ((SKRect)(ref bounds)).MidY - 10f, ((SKRect)(ref bounds)).Right - 12f, ((SKRect)(ref bounds)).MidY + 10f));
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

	private void DrawCalendarIcon(SKCanvas canvas, SKRect bounds)
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
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Expected O, but got Unknown
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
			SKRect val3 = new SKRect(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top + 3f, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom);
			canvas.DrawRoundRect(new SKRoundRect(val3, 2f), val2);
			canvas.DrawLine(((SKRect)(ref bounds)).Left + 5f, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Left + 5f, ((SKRect)(ref bounds)).Top + 5f, val2);
			canvas.DrawLine(((SKRect)(ref bounds)).Right - 5f, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Right - 5f, ((SKRect)(ref bounds)).Top + 5f, val2);
			canvas.DrawLine(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top + 8f, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Top + 8f, val2);
			val2.Style = (SKPaintStyle)0;
			for (int i = 0; i < 2; i++)
			{
				for (int j = 0; j < 3; j++)
				{
					canvas.DrawCircle(((SKRect)(ref bounds)).Left + 4f + (float)(j * 6), ((SKRect)(ref bounds)).Top + 12f + (float)(i * 4), 1f, val2);
				}
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private void DrawCalendar(SKCanvas canvas, SKRect bounds)
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
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		SKRect calendarRect = GetCalendarRect(bounds);
		SKPaint val = new SKPaint
		{
			Color = new SKColor((byte)0, (byte)0, (byte)0, (byte)40),
			MaskFilter = SKMaskFilter.CreateBlur((SKBlurStyle)0, 4f),
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRoundRect(new SKRoundRect(new SKRect(((SKRect)(ref calendarRect)).Left + 2f, ((SKRect)(ref calendarRect)).Top + 2f, ((SKRect)(ref calendarRect)).Right + 2f, ((SKRect)(ref calendarRect)).Bottom + 2f), CornerRadius), val);
			SKPaint val2 = new SKPaint
			{
				Color = CalendarBackgroundColor,
				Style = (SKPaintStyle)0,
				IsAntialias = true
			};
			try
			{
				canvas.DrawRoundRect(new SKRoundRect(calendarRect, CornerRadius), val2);
				SKPaint val3 = new SKPaint
				{
					Color = BorderColor,
					Style = (SKPaintStyle)1,
					StrokeWidth = 1f,
					IsAntialias = true
				};
				try
				{
					canvas.DrawRoundRect(new SKRoundRect(calendarRect, CornerRadius), val3);
					DrawCalendarHeader(canvas, new SKRect(((SKRect)(ref calendarRect)).Left, ((SKRect)(ref calendarRect)).Top, ((SKRect)(ref calendarRect)).Right, ((SKRect)(ref calendarRect)).Top + 48f));
					DrawWeekdayHeaders(canvas, new SKRect(((SKRect)(ref calendarRect)).Left, ((SKRect)(ref calendarRect)).Top + 48f, ((SKRect)(ref calendarRect)).Right, ((SKRect)(ref calendarRect)).Top + 48f + 30f));
					DrawDays(canvas, new SKRect(((SKRect)(ref calendarRect)).Left, ((SKRect)(ref calendarRect)).Top + 48f + 30f, ((SKRect)(ref calendarRect)).Right, ((SKRect)(ref calendarRect)).Bottom));
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

	private void DrawCalendarHeader(SKCanvas canvas, SKRect bounds)
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
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Expected O, but got Unknown
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Expected O, but got Unknown
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Expected O, but got Unknown
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
			SKFont val2 = new SKFont(SKTypeface.Default, 16f, 1f, 0f);
			try
			{
				SKPaint val3 = new SKPaint(val2)
				{
					Color = SKColors.White,
					IsAntialias = true
				};
				try
				{
					string text = _displayMonth.ToString("MMMM yyyy");
					SKRect val4 = default(SKRect);
					val3.MeasureText(text, ref val4);
					canvas.DrawText(text, ((SKRect)(ref bounds)).MidX - ((SKRect)(ref val4)).MidX, ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val4)).MidY, val3);
					SKPaint val5 = new SKPaint
					{
						Color = SKColors.White,
						Style = (SKPaintStyle)1,
						StrokeWidth = 2f,
						IsAntialias = true,
						StrokeCap = (SKStrokeCap)1
					};
					try
					{
						SKPath val6 = new SKPath();
						try
						{
							val6.MoveTo(((SKRect)(ref bounds)).Left + 26f, ((SKRect)(ref bounds)).MidY - 6f);
							val6.LineTo(((SKRect)(ref bounds)).Left + 20f, ((SKRect)(ref bounds)).MidY);
							val6.LineTo(((SKRect)(ref bounds)).Left + 26f, ((SKRect)(ref bounds)).MidY + 6f);
							canvas.DrawPath(val6, val5);
							SKPath val7 = new SKPath();
							try
							{
								val7.MoveTo(((SKRect)(ref bounds)).Right - 26f, ((SKRect)(ref bounds)).MidY - 6f);
								val7.LineTo(((SKRect)(ref bounds)).Right - 20f, ((SKRect)(ref bounds)).MidY);
								val7.LineTo(((SKRect)(ref bounds)).Right - 26f, ((SKRect)(ref bounds)).MidY + 6f);
								canvas.DrawPath(val7, val5);
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

	private void DrawWeekdayHeaders(SKCanvas canvas, SKRect bounds)
	{
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Expected O, but got Unknown
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Expected O, but got Unknown
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		string[] array = new string[7] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
		float num = ((SKRect)(ref bounds)).Width / 7f;
		SKFont val = new SKFont(SKTypeface.Default, 12f, 1f, 0f);
		try
		{
			SKPaint val2 = new SKPaint(val)
			{
				Color = new SKColor((byte)128, (byte)128, (byte)128),
				IsAntialias = true
			};
			try
			{
				for (int i = 0; i < 7; i++)
				{
					SKRect val3 = default(SKRect);
					val2.MeasureText(array[i], ref val3);
					canvas.DrawText(array[i], ((SKRect)(ref bounds)).Left + (float)i * num + num / 2f - ((SKRect)(ref val3)).MidX, ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val3)).MidY, val2);
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

	private void DrawDays(SKCanvas canvas, SKRect bounds)
	{
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Expected O, but got Unknown
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Expected O, but got Unknown
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Expected O, but got Unknown
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		DateTime dateTime = new DateTime(_displayMonth.Year, _displayMonth.Month, 1);
		int num = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);
		int dayOfWeek = (int)dateTime.DayOfWeek;
		float num2 = ((SKRect)(ref bounds)).Width / 7f;
		float num3 = (((SKRect)(ref bounds)).Height - 10f) / 6f;
		SKFont val = new SKFont(SKTypeface.Default, 14f, 1f, 0f);
		try
		{
			SKPaint val2 = new SKPaint(val)
			{
				IsAntialias = true
			};
			try
			{
				SKPaint val3 = new SKPaint
				{
					Style = (SKPaintStyle)0,
					IsAntialias = true
				};
				try
				{
					DateTime today = DateTime.Today;
					SKRect val4 = default(SKRect);
					for (int i = 1; i <= num; i++)
					{
						DateTime dateTime2 = new DateTime(_displayMonth.Year, _displayMonth.Month, i);
						int num4 = dayOfWeek + i - 1;
						int num5 = num4 / 7;
						int num6 = num4 % 7;
						((SKRect)(ref val4))._002Ector(((SKRect)(ref bounds)).Left + (float)num6 * num2 + 2f, ((SKRect)(ref bounds)).Top + (float)num5 * num3 + 2f, ((SKRect)(ref bounds)).Left + (float)(num6 + 1) * num2 - 2f, ((SKRect)(ref bounds)).Top + (float)(num5 + 1) * num3 - 2f);
						bool flag = dateTime2.Date == Date.Date;
						bool flag2 = dateTime2.Date == today;
						bool flag3 = dateTime2 < MinimumDate || dateTime2 > MaximumDate;
						if (flag)
						{
							val3.Color = SelectedDayColor;
							canvas.DrawCircle(((SKRect)(ref val4)).MidX, ((SKRect)(ref val4)).MidY, Math.Min(((SKRect)(ref val4)).Width, ((SKRect)(ref val4)).Height) / 2f - 2f, val3);
						}
						else if (flag2)
						{
							val3.Color = TodayColor;
							canvas.DrawCircle(((SKRect)(ref val4)).MidX, ((SKRect)(ref val4)).MidY, Math.Min(((SKRect)(ref val4)).Width, ((SKRect)(ref val4)).Height) / 2f - 2f, val3);
						}
						val2.Color = (flag ? SKColors.White : (flag3 ? DisabledDayColor : TextColor));
						string text = i.ToString();
						SKRect val5 = default(SKRect);
						val2.MeasureText(text, ref val5);
						canvas.DrawText(text, ((SKRect)(ref val4)).MidX - ((SKRect)(ref val5)).MidX, ((SKRect)(ref val4)).MidY - ((SKRect)(ref val5)).MidY, val2);
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
			SKRect calendarRect = GetCalendarRect(screenBounds);
			SKRect val = default(SKRect);
			((SKRect)(ref val))._002Ector(((SKRect)(ref calendarRect)).Left, ((SKRect)(ref calendarRect)).Top, ((SKRect)(ref calendarRect)).Right, ((SKRect)(ref calendarRect)).Top + 48f);
			if (((SKRect)(ref val)).Contains(e.X, e.Y))
			{
				if (e.X < ((SKRect)(ref calendarRect)).Left + 40f)
				{
					_displayMonth = _displayMonth.AddMonths(-1);
					Invalidate();
				}
				else if (e.X > ((SKRect)(ref calendarRect)).Right - 40f)
				{
					_displayMonth = _displayMonth.AddMonths(1);
					Invalidate();
				}
				return;
			}
			float num = ((SKRect)(ref calendarRect)).Top + 48f + 30f;
			SKRect val2 = default(SKRect);
			((SKRect)(ref val2))._002Ector(((SKRect)(ref calendarRect)).Left, num, ((SKRect)(ref calendarRect)).Right, ((SKRect)(ref calendarRect)).Bottom);
			if (((SKRect)(ref val2)).Contains(e.X, e.Y))
			{
				float num2 = 40f;
				float num3 = 38.666668f;
				int num4 = (int)((e.X - ((SKRect)(ref calendarRect)).Left) / num2);
				int num5 = (int)((int)((e.Y - num) / num3) * 7 + num4 - new DateTime(_displayMonth.Year, _displayMonth.Month, 1).DayOfWeek + 1);
				int num6 = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);
				if (num5 >= 1 && num5 <= num6)
				{
					DateTime dateTime = new DateTime(_displayMonth.Year, _displayMonth.Month, num5);
					if (dateTime >= MinimumDate && dateTime <= MaximumDate)
					{
						Date = dateTime;
						IsOpen = false;
					}
				}
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
		}
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
			break;
		case Key.Escape:
			if (IsOpen)
			{
				IsOpen = false;
				e.Handled = true;
			}
			break;
		case Key.Left:
			Date = Date.AddDays(-1.0);
			e.Handled = true;
			break;
		case Key.Right:
			Date = Date.AddDays(1.0);
			e.Handled = true;
			break;
		case Key.Up:
			Date = Date.AddDays(-7.0);
			e.Handled = true;
			break;
		case Key.Down:
			Date = Date.AddDays(7.0);
			e.Handled = true;
			break;
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
			SKRect calendarRect = GetCalendarRect(screenBounds);
			return ((SKRect)(ref calendarRect)).Contains(x, y);
		}
		return false;
	}
}
