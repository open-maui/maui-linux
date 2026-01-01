using System;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaCheckBox : SkiaView
{
	public static readonly BindableProperty IsCheckedProperty = BindableProperty.Create("IsChecked", typeof(bool), typeof(SkiaCheckBox), (object)false, (BindingMode)1, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCheckBox)(object)b).OnIsCheckedChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CheckColorProperty = BindableProperty.Create("CheckColor", typeof(SKColor), typeof(SkiaCheckBox), (object)SKColors.White, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCheckBox)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty BoxColorProperty = BindableProperty.Create("BoxColor", typeof(SKColor), typeof(SkiaCheckBox), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCheckBox)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty UncheckedBoxColorProperty = BindableProperty.Create("UncheckedBoxColor", typeof(SKColor), typeof(SkiaCheckBox), (object)SKColors.White, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCheckBox)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty BorderColorProperty = BindableProperty.Create("BorderColor", typeof(SKColor), typeof(SkiaCheckBox), (object)new SKColor((byte)117, (byte)117, (byte)117), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCheckBox)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty DisabledColorProperty = BindableProperty.Create("DisabledColor", typeof(SKColor), typeof(SkiaCheckBox), (object)new SKColor((byte)189, (byte)189, (byte)189), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCheckBox)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty HoveredBorderColorProperty = BindableProperty.Create("HoveredBorderColor", typeof(SKColor), typeof(SkiaCheckBox), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCheckBox)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty BoxSizeProperty = BindableProperty.Create("BoxSize", typeof(float), typeof(SkiaCheckBox), (object)20f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCheckBox)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create("CornerRadius", typeof(float), typeof(SkiaCheckBox), (object)3f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCheckBox)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty BorderWidthProperty = BindableProperty.Create("BorderWidth", typeof(float), typeof(SkiaCheckBox), (object)2f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCheckBox)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CheckStrokeWidthProperty = BindableProperty.Create("CheckStrokeWidth", typeof(float), typeof(SkiaCheckBox), (object)2.5f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCheckBox)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public bool IsChecked
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(IsCheckedProperty);
		}
		set
		{
			((BindableObject)this).SetValue(IsCheckedProperty, (object)value);
		}
	}

	public SKColor CheckColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(CheckColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(CheckColorProperty, (object)value);
		}
	}

	public SKColor BoxColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(BoxColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(BoxColorProperty, (object)value);
		}
	}

	public SKColor UncheckedBoxColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(UncheckedBoxColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(UncheckedBoxColorProperty, (object)value);
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

	public SKColor HoveredBorderColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(HoveredBorderColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(HoveredBorderColorProperty, (object)value);
		}
	}

	public float BoxSize
	{
		get
		{
			return (float)((BindableObject)this).GetValue(BoxSizeProperty);
		}
		set
		{
			((BindableObject)this).SetValue(BoxSizeProperty, (object)value);
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

	public float BorderWidth
	{
		get
		{
			return (float)((BindableObject)this).GetValue(BorderWidthProperty);
		}
		set
		{
			((BindableObject)this).SetValue(BorderWidthProperty, (object)value);
		}
	}

	public float CheckStrokeWidth
	{
		get
		{
			return (float)((BindableObject)this).GetValue(CheckStrokeWidthProperty);
		}
		set
		{
			((BindableObject)this).SetValue(CheckStrokeWidthProperty, (object)value);
		}
	}

	public bool IsHovered { get; private set; }

	public event EventHandler<CheckedChangedEventArgs>? CheckedChanged;

	public SkiaCheckBox()
	{
		base.IsFocusable = true;
	}

	private void OnIsCheckedChanged()
	{
		this.CheckedChanged?.Invoke(this, new CheckedChangedEventArgs(IsChecked));
		SkiaVisualStateManager.GoToState(this, IsChecked ? "Checked" : "Unchecked");
		Invalidate();
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Expected O, but got Unknown
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Expected O, but got Unknown
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_022f: Expected O, but got Unknown
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_025b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0262: Unknown result type (might be due to invalid IL or missing references)
		//IL_0269: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Expected O, but got Unknown
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0284: Expected O, but got Unknown
		SKRect val = default(SKRect);
		((SKRect)(ref val))._002Ector(((SKRect)(ref bounds)).Left + (((SKRect)(ref bounds)).Width - BoxSize) / 2f, ((SKRect)(ref bounds)).Top + (((SKRect)(ref bounds)).Height - BoxSize) / 2f, ((SKRect)(ref bounds)).Left + (((SKRect)(ref bounds)).Width - BoxSize) / 2f + BoxSize, ((SKRect)(ref bounds)).Top + (((SKRect)(ref bounds)).Height - BoxSize) / 2f + BoxSize);
		SKRoundRect val2 = new SKRoundRect(val, CornerRadius);
		SKColor val3;
		if (IsChecked)
		{
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(69, 6);
			defaultInterpolatedStringHandler.AppendLiteral("[SkiaCheckBox] OnDraw CHECKED - BoxColor=(");
			val3 = BoxColor;
			defaultInterpolatedStringHandler.AppendFormatted(((SKColor)(ref val3)).Red);
			defaultInterpolatedStringHandler.AppendLiteral(",");
			val3 = BoxColor;
			defaultInterpolatedStringHandler.AppendFormatted(((SKColor)(ref val3)).Green);
			defaultInterpolatedStringHandler.AppendLiteral(",");
			val3 = BoxColor;
			defaultInterpolatedStringHandler.AppendFormatted(((SKColor)(ref val3)).Blue);
			defaultInterpolatedStringHandler.AppendLiteral("), UncheckedBoxColor=(");
			val3 = UncheckedBoxColor;
			defaultInterpolatedStringHandler.AppendFormatted(((SKColor)(ref val3)).Red);
			defaultInterpolatedStringHandler.AppendLiteral(",");
			val3 = UncheckedBoxColor;
			defaultInterpolatedStringHandler.AppendFormatted(((SKColor)(ref val3)).Green);
			defaultInterpolatedStringHandler.AppendLiteral(",");
			val3 = UncheckedBoxColor;
			defaultInterpolatedStringHandler.AppendFormatted(((SKColor)(ref val3)).Blue);
			defaultInterpolatedStringHandler.AppendLiteral(")");
			Console.WriteLine(defaultInterpolatedStringHandler.ToStringAndClear());
		}
		SKPaint val4 = new SKPaint
		{
			Color = ((!base.IsEnabled) ? DisabledColor : (IsChecked ? BoxColor : UncheckedBoxColor)),
			IsAntialias = true,
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRoundRect(val2, val4);
			SKPaint val5 = new SKPaint
			{
				Color = ((!base.IsEnabled) ? DisabledColor : (IsChecked ? BoxColor : (IsHovered ? HoveredBorderColor : BorderColor))),
				IsAntialias = true,
				Style = (SKPaintStyle)1,
				StrokeWidth = BorderWidth
			};
			try
			{
				canvas.DrawRoundRect(val2, val5);
				if (base.IsFocused)
				{
					SKPaint val6 = new SKPaint();
					val3 = BoxColor;
					val6.Color = ((SKColor)(ref val3)).WithAlpha((byte)80);
					val6.IsAntialias = true;
					val6.Style = (SKPaintStyle)1;
					val6.StrokeWidth = 3f;
					SKPaint val7 = val6;
					try
					{
						SKRoundRect val8 = new SKRoundRect(val, CornerRadius);
						val8.Inflate(4f, 4f);
						canvas.DrawRoundRect(val8, val7);
					}
					finally
					{
						((IDisposable)val7)?.Dispose();
					}
				}
				if (IsChecked)
				{
					DrawCheckmark(canvas, val);
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

	private void DrawCheckmark(SKCanvas canvas, SKRect boxRect)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Expected O, but got Unknown
		SKPaint val = new SKPaint
		{
			Color = SKColors.White,
			IsAntialias = true,
			Style = (SKPaintStyle)1,
			StrokeWidth = CheckStrokeWidth,
			StrokeCap = (SKStrokeCap)1,
			StrokeJoin = (SKStrokeJoin)1
		};
		try
		{
			float num = BoxSize * 0.2f;
			float num2 = ((SKRect)(ref boxRect)).Left + num;
			float num3 = ((SKRect)(ref boxRect)).Right - num;
			float num4 = ((SKRect)(ref boxRect)).Top + num;
			float num5 = ((SKRect)(ref boxRect)).Bottom - num;
			SKPath val2 = new SKPath();
			try
			{
				val2.MoveTo(num2, ((SKRect)(ref boxRect)).MidY);
				val2.LineTo(((SKRect)(ref boxRect)).MidX - num * 0.3f, num5 - num * 0.5f);
				val2.LineTo(num3, num4 + num * 0.3f);
				canvas.DrawPath(val2, val);
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

	public override void OnPointerEntered(PointerEventArgs e)
	{
		if (base.IsEnabled)
		{
			IsHovered = true;
			SkiaVisualStateManager.GoToState(this, "PointerOver");
			Invalidate();
		}
	}

	public override void OnPointerExited(PointerEventArgs e)
	{
		IsHovered = false;
		SkiaVisualStateManager.GoToState(this, base.IsEnabled ? "Normal" : "Disabled");
		Invalidate();
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		if (base.IsEnabled)
		{
			IsChecked = !IsChecked;
			e.Handled = true;
		}
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
	}

	public override void OnKeyDown(KeyEventArgs e)
	{
		if (base.IsEnabled && e.Key == Key.Space)
		{
			IsChecked = !IsChecked;
			e.Handled = true;
		}
	}

	protected override void OnEnabledChanged()
	{
		base.OnEnabledChanged();
		SkiaVisualStateManager.GoToState(this, base.IsEnabled ? "Normal" : "Disabled");
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		return new SKSize(BoxSize + 8f, BoxSize + 8f);
	}
}
