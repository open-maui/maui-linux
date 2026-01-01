using System;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaStepper : SkiaView
{
	public static readonly BindableProperty ValueProperty = BindableProperty.Create("Value", typeof(double), typeof(SkiaStepper), (object)0.0, (BindingMode)1, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaStepper)(object)b).OnValuePropertyChanged((double)o, (double)n);
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty MinimumProperty = BindableProperty.Create("Minimum", typeof(double), typeof(SkiaStepper), (object)0.0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaStepper)(object)b).OnRangeChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty MaximumProperty = BindableProperty.Create("Maximum", typeof(double), typeof(SkiaStepper), (object)100.0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaStepper)(object)b).OnRangeChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty IncrementProperty = BindableProperty.Create("Increment", typeof(double), typeof(SkiaStepper), (object)1.0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)null, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ButtonBackgroundColorProperty = BindableProperty.Create("ButtonBackgroundColor", typeof(SKColor), typeof(SkiaStepper), (object)new SKColor((byte)224, (byte)224, (byte)224), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaStepper)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ButtonPressedColorProperty = BindableProperty.Create("ButtonPressedColor", typeof(SKColor), typeof(SkiaStepper), (object)new SKColor((byte)189, (byte)189, (byte)189), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaStepper)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ButtonDisabledColorProperty = BindableProperty.Create("ButtonDisabledColor", typeof(SKColor), typeof(SkiaStepper), (object)new SKColor((byte)245, (byte)245, (byte)245), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaStepper)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty BorderColorProperty = BindableProperty.Create("BorderColor", typeof(SKColor), typeof(SkiaStepper), (object)new SKColor((byte)189, (byte)189, (byte)189), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaStepper)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty SymbolColorProperty = BindableProperty.Create("SymbolColor", typeof(SKColor), typeof(SkiaStepper), (object)SKColors.Black, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaStepper)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty SymbolDisabledColorProperty = BindableProperty.Create("SymbolDisabledColor", typeof(SKColor), typeof(SkiaStepper), (object)new SKColor((byte)189, (byte)189, (byte)189), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaStepper)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create("CornerRadius", typeof(float), typeof(SkiaStepper), (object)4f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaStepper)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ButtonWidthProperty = BindableProperty.Create("ButtonWidth", typeof(float), typeof(SkiaStepper), (object)40f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaStepper)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private bool _isMinusPressed;

	private bool _isPlusPressed;

	public double Value
	{
		get
		{
			return (double)((BindableObject)this).GetValue(ValueProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ValueProperty, (object)Math.Clamp(value, Minimum, Maximum));
		}
	}

	public double Minimum
	{
		get
		{
			return (double)((BindableObject)this).GetValue(MinimumProperty);
		}
		set
		{
			((BindableObject)this).SetValue(MinimumProperty, (object)value);
		}
	}

	public double Maximum
	{
		get
		{
			return (double)((BindableObject)this).GetValue(MaximumProperty);
		}
		set
		{
			((BindableObject)this).SetValue(MaximumProperty, (object)value);
		}
	}

	public double Increment
	{
		get
		{
			return (double)((BindableObject)this).GetValue(IncrementProperty);
		}
		set
		{
			((BindableObject)this).SetValue(IncrementProperty, (object)Math.Max(0.001, value));
		}
	}

	public SKColor ButtonBackgroundColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(ButtonBackgroundColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(ButtonBackgroundColorProperty, (object)value);
		}
	}

	public SKColor ButtonPressedColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(ButtonPressedColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(ButtonPressedColorProperty, (object)value);
		}
	}

	public SKColor ButtonDisabledColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(ButtonDisabledColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(ButtonDisabledColorProperty, (object)value);
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

	public SKColor SymbolColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(SymbolColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(SymbolColorProperty, (object)value);
		}
	}

	public SKColor SymbolDisabledColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(SymbolDisabledColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(SymbolDisabledColorProperty, (object)value);
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

	public float ButtonWidth
	{
		get
		{
			return (float)((BindableObject)this).GetValue(ButtonWidthProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ButtonWidthProperty, (object)value);
		}
	}

	public event EventHandler? ValueChanged;

	public SkiaStepper()
	{
		base.IsFocusable = true;
	}

	private void OnValuePropertyChanged(double oldValue, double newValue)
	{
		this.ValueChanged?.Invoke(this, EventArgs.Empty);
		Invalidate();
	}

	private void OnRangeChanged()
	{
		double num = Math.Clamp(Value, Minimum, Maximum);
		if (Value != num)
		{
			Value = num;
		}
		Invalidate();
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Expected O, but got Unknown
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Expected O, but got Unknown
		SKRect rect = default(SKRect);
		((SKRect)(ref rect))._002Ector(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Left + ButtonWidth, ((SKRect)(ref bounds)).Bottom);
		SKRect rect2 = default(SKRect);
		((SKRect)(ref rect2))._002Ector(((SKRect)(ref bounds)).Right - ButtonWidth, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom);
		DrawButton(canvas, rect, "-", _isMinusPressed, !CanDecrement());
		DrawButton(canvas, rect2, "+", _isPlusPressed, !CanIncrement());
		SKPaint val = new SKPaint
		{
			Color = BorderColor,
			Style = (SKPaintStyle)1,
			StrokeWidth = 1f,
			IsAntialias = true
		};
		try
		{
			SKRect val2 = new SKRect(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom);
			canvas.DrawRoundRect(new SKRoundRect(val2, CornerRadius), val);
			float midX = ((SKRect)(ref bounds)).MidX;
			canvas.DrawLine(midX, ((SKRect)(ref bounds)).Top, midX, ((SKRect)(ref bounds)).Bottom, val);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void DrawButton(SKCanvas canvas, SKRect rect, string symbol, bool isPressed, bool isDisabled)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Expected O, but got Unknown
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = (isDisabled ? ButtonDisabledColor : (isPressed ? ButtonPressedColor : ButtonBackgroundColor)),
			Style = (SKPaintStyle)0,
			IsAntialias = true
		};
		try
		{
			canvas.DrawRect(rect, val);
			SKFont val2 = new SKFont(SKTypeface.Default, 20f, 1f, 0f);
			try
			{
				SKPaint val3 = new SKPaint(val2)
				{
					Color = (isDisabled ? SymbolDisabledColor : SymbolColor),
					IsAntialias = true
				};
				try
				{
					SKRect val4 = default(SKRect);
					val3.MeasureText(symbol, ref val4);
					canvas.DrawText(symbol, ((SKRect)(ref rect)).MidX - ((SKRect)(ref val4)).MidX, ((SKRect)(ref rect)).MidY - ((SKRect)(ref val4)).MidY, val3);
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

	private bool CanIncrement()
	{
		if (base.IsEnabled)
		{
			return Value < Maximum;
		}
		return false;
	}

	private bool CanDecrement()
	{
		if (base.IsEnabled)
		{
			return Value > Minimum;
		}
		return false;
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		if (!base.IsEnabled)
		{
			return;
		}
		if (e.X < ButtonWidth)
		{
			_isMinusPressed = true;
			if (CanDecrement())
			{
				Value -= Increment;
			}
		}
		else
		{
			float x = e.X;
			SKRect bounds = base.Bounds;
			if (x > ((SKRect)(ref bounds)).Width - ButtonWidth)
			{
				_isPlusPressed = true;
				if (CanIncrement())
				{
					Value += Increment;
				}
			}
		}
		Invalidate();
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		_isMinusPressed = false;
		_isPlusPressed = false;
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
		case Key.Up:
		case Key.Right:
			if (CanIncrement())
			{
				Value += Increment;
			}
			e.Handled = true;
			break;
		case Key.Left:
		case Key.Down:
			if (CanDecrement())
			{
				Value -= Increment;
			}
			e.Handled = true;
			break;
		}
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		return new SKSize(ButtonWidth * 2f + 1f, 32f);
	}
}
