using System;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaSlider : SkiaView
{
	public static readonly BindableProperty MinimumProperty = BindableProperty.Create("Minimum", typeof(double), typeof(SkiaSlider), (object)0.0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSlider)(object)b).OnRangeChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty MaximumProperty = BindableProperty.Create("Maximum", typeof(double), typeof(SkiaSlider), (object)100.0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSlider)(object)b).OnRangeChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ValueProperty = BindableProperty.Create("Value", typeof(double), typeof(SkiaSlider), (object)0.0, (BindingMode)1, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSlider)(object)b).OnValuePropertyChanged((double)o, (double)n);
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TrackColorProperty = BindableProperty.Create("TrackColor", typeof(SKColor), typeof(SkiaSlider), (object)new SKColor((byte)224, (byte)224, (byte)224), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSlider)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ActiveTrackColorProperty = BindableProperty.Create("ActiveTrackColor", typeof(SKColor), typeof(SkiaSlider), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSlider)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ThumbColorProperty = BindableProperty.Create("ThumbColor", typeof(SKColor), typeof(SkiaSlider), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSlider)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty DisabledColorProperty = BindableProperty.Create("DisabledColor", typeof(SKColor), typeof(SkiaSlider), (object)new SKColor((byte)189, (byte)189, (byte)189), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSlider)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TrackHeightProperty = BindableProperty.Create("TrackHeight", typeof(float), typeof(SkiaSlider), (object)4f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSlider)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ThumbRadiusProperty = BindableProperty.Create("ThumbRadius", typeof(float), typeof(SkiaSlider), (object)10f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSlider)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private bool _isDragging;

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

	public SKColor TrackColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(TrackColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(TrackColorProperty, (object)value);
		}
	}

	public SKColor ActiveTrackColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(ActiveTrackColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(ActiveTrackColorProperty, (object)value);
		}
	}

	public SKColor ThumbColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(ThumbColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(ThumbColorProperty, (object)value);
		}
	}

	public SKColor DisabledColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(DisabledColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(DisabledColorProperty, (object)value);
		}
	}

	public float TrackHeight
	{
		get
		{
			return (float)((BindableObject)this).GetValue(TrackHeightProperty);
		}
		set
		{
			((BindableObject)this).SetValue(TrackHeightProperty, (object)value);
		}
	}

	public float ThumbRadius
	{
		get
		{
			return (float)((BindableObject)this).GetValue(ThumbRadiusProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ThumbRadiusProperty, (object)value);
		}
	}

	public event EventHandler<SliderValueChangedEventArgs>? ValueChanged;

	public event EventHandler? DragStarted;

	public event EventHandler? DragCompleted;

	public SkiaSlider()
	{
		base.IsFocusable = true;
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

	private void OnValuePropertyChanged(double oldValue, double newValue)
	{
		this.ValueChanged?.Invoke(this, new SliderValueChangedEventArgs(newValue));
		Invalidate();
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Expected O, but got Unknown
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Expected O, but got Unknown
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Expected O, but got Unknown
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Expected O, but got Unknown
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Expected O, but got Unknown
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Expected O, but got Unknown
		//IL_0217: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0243: Expected O, but got Unknown
		float midY = ((SKRect)(ref bounds)).MidY;
		float num = ((SKRect)(ref bounds)).Left + ThumbRadius;
		float num2 = ((SKRect)(ref bounds)).Right - ThumbRadius;
		float num3 = num2 - num;
		double num4 = ((Maximum > Minimum) ? ((Value - Minimum) / (Maximum - Minimum)) : 0.0);
		float num5 = num + (float)(num4 * (double)num3);
		SKPaint val = new SKPaint
		{
			Color = (base.IsEnabled ? TrackColor : DisabledColor),
			IsAntialias = true,
			Style = (SKPaintStyle)0
		};
		try
		{
			SKRoundRect val2 = new SKRoundRect(new SKRect(num, midY - TrackHeight / 2f, num2, midY + TrackHeight / 2f), TrackHeight / 2f);
			canvas.DrawRoundRect(val2, val);
			if (num4 > 0.0)
			{
				SKPaint val3 = new SKPaint
				{
					Color = (base.IsEnabled ? ActiveTrackColor : DisabledColor),
					IsAntialias = true,
					Style = (SKPaintStyle)0
				};
				try
				{
					SKRoundRect val4 = new SKRoundRect(new SKRect(num, midY - TrackHeight / 2f, num5, midY + TrackHeight / 2f), TrackHeight / 2f);
					canvas.DrawRoundRect(val4, val3);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
			if (base.IsEnabled)
			{
				SKPaint val5 = new SKPaint
				{
					Color = new SKColor((byte)0, (byte)0, (byte)0, (byte)30),
					IsAntialias = true,
					MaskFilter = SKMaskFilter.CreateBlur((SKBlurStyle)0, 3f)
				};
				try
				{
					canvas.DrawCircle(num5 + 1f, midY + 2f, ThumbRadius, val5);
				}
				finally
				{
					((IDisposable)val5)?.Dispose();
				}
			}
			SKPaint val6 = new SKPaint
			{
				Color = (base.IsEnabled ? ThumbColor : DisabledColor),
				IsAntialias = true,
				Style = (SKPaintStyle)0
			};
			try
			{
				canvas.DrawCircle(num5, midY, ThumbRadius, val6);
				if (base.IsFocused)
				{
					SKPaint val7 = new SKPaint();
					SKColor thumbColor = ThumbColor;
					val7.Color = ((SKColor)(ref thumbColor)).WithAlpha((byte)60);
					val7.IsAntialias = true;
					val7.Style = (SKPaintStyle)0;
					SKPaint val8 = val7;
					try
					{
						canvas.DrawCircle(num5, midY, ThumbRadius + 8f, val8);
						return;
					}
					finally
					{
						((IDisposable)val8)?.Dispose();
					}
				}
			}
			finally
			{
				((IDisposable)val6)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		if (base.IsEnabled)
		{
			_isDragging = true;
			UpdateValueFromPosition(e.X);
			this.DragStarted?.Invoke(this, EventArgs.Empty);
			SkiaVisualStateManager.GoToState(this, "Pressed");
		}
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		if (base.IsEnabled && _isDragging)
		{
			UpdateValueFromPosition(e.X);
		}
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		if (_isDragging)
		{
			_isDragging = false;
			this.DragCompleted?.Invoke(this, EventArgs.Empty);
			SkiaVisualStateManager.GoToState(this, base.IsEnabled ? "Normal" : "Disabled");
		}
	}

	private void UpdateValueFromPosition(float x)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		SKRect bounds = base.Bounds;
		float num = ((SKRect)(ref bounds)).Left + ThumbRadius;
		bounds = base.Bounds;
		float num2 = ((SKRect)(ref bounds)).Right - ThumbRadius - num;
		float num3 = Math.Clamp((x - num) / num2, 0f, 1f);
		Value = Minimum + (double)num3 * (Maximum - Minimum);
	}

	public override void OnKeyDown(KeyEventArgs e)
	{
		if (base.IsEnabled)
		{
			double num = (Maximum - Minimum) / 100.0;
			switch (e.Key)
			{
			case Key.Left:
			case Key.Down:
				Value -= num * 10.0;
				e.Handled = true;
				break;
			case Key.Up:
			case Key.Right:
				Value += num * 10.0;
				e.Handled = true;
				break;
			case Key.Home:
				Value = Minimum;
				e.Handled = true;
				break;
			case Key.End:
				Value = Maximum;
				e.Handled = true;
				break;
			}
		}
	}

	protected override void OnEnabledChanged()
	{
		base.OnEnabledChanged();
		SkiaVisualStateManager.GoToState(this, base.IsEnabled ? "Normal" : "Disabled");
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		return new SKSize(200f, ThumbRadius * 2f + 16f);
	}
}
