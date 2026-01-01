using System;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaSwitch : SkiaView
{
	public static readonly BindableProperty IsOnProperty = BindableProperty.Create("IsOn", typeof(bool), typeof(SkiaSwitch), (object)false, (BindingMode)1, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSwitch)(object)b).OnIsOnChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty OnTrackColorProperty = BindableProperty.Create("OnTrackColor", typeof(SKColor), typeof(SkiaSwitch), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSwitch)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty OffTrackColorProperty = BindableProperty.Create("OffTrackColor", typeof(SKColor), typeof(SkiaSwitch), (object)new SKColor((byte)158, (byte)158, (byte)158), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSwitch)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ThumbColorProperty = BindableProperty.Create("ThumbColor", typeof(SKColor), typeof(SkiaSwitch), (object)SKColors.White, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSwitch)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty DisabledColorProperty = BindableProperty.Create("DisabledColor", typeof(SKColor), typeof(SkiaSwitch), (object)new SKColor((byte)189, (byte)189, (byte)189), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSwitch)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TrackWidthProperty = BindableProperty.Create("TrackWidth", typeof(float), typeof(SkiaSwitch), (object)52f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSwitch)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TrackHeightProperty = BindableProperty.Create("TrackHeight", typeof(float), typeof(SkiaSwitch), (object)32f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSwitch)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ThumbRadiusProperty = BindableProperty.Create("ThumbRadius", typeof(float), typeof(SkiaSwitch), (object)12f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSwitch)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ThumbPaddingProperty = BindableProperty.Create("ThumbPadding", typeof(float), typeof(SkiaSwitch), (object)4f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaSwitch)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private float _animationProgress;

	public bool IsOn
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(IsOnProperty);
		}
		set
		{
			((BindableObject)this).SetValue(IsOnProperty, (object)value);
		}
	}

	public SKColor OnTrackColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(OnTrackColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(OnTrackColorProperty, (object)value);
		}
	}

	public SKColor OffTrackColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(OffTrackColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(OffTrackColorProperty, (object)value);
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

	public float TrackWidth
	{
		get
		{
			return (float)((BindableObject)this).GetValue(TrackWidthProperty);
		}
		set
		{
			((BindableObject)this).SetValue(TrackWidthProperty, (object)value);
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

	public float ThumbPadding
	{
		get
		{
			return (float)((BindableObject)this).GetValue(ThumbPaddingProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ThumbPaddingProperty, (object)value);
		}
	}

	public event EventHandler<ToggledEventArgs>? Toggled;

	public SkiaSwitch()
	{
		base.IsFocusable = true;
	}

	private void OnIsOnChanged()
	{
		_animationProgress = (IsOn ? 1f : 0f);
		this.Toggled?.Invoke(this, new ToggledEventArgs(IsOn));
		SkiaVisualStateManager.GoToState(this, IsOn ? "On" : "Off");
		Invalidate();
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected O, but got Unknown
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Expected O, but got Unknown
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Expected O, but got Unknown
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Expected O, but got Unknown
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Expected O, but got Unknown
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Expected O, but got Unknown
		float midY = ((SKRect)(ref bounds)).MidY;
		float num = ((SKRect)(ref bounds)).MidX - TrackWidth / 2f;
		float num2 = num + TrackWidth;
		float num3 = num + ThumbPadding + ThumbRadius;
		float num4 = num2 - ThumbPadding - ThumbRadius;
		float num5 = num3 + _animationProgress * (num4 - num3);
		SKColor color = (base.IsEnabled ? InterpolateColor(OffTrackColor, OnTrackColor, _animationProgress) : DisabledColor);
		SKPaint val = new SKPaint
		{
			Color = color,
			IsAntialias = true,
			Style = (SKPaintStyle)0
		};
		try
		{
			SKRoundRect val2 = new SKRoundRect(new SKRect(num, midY - TrackHeight / 2f, num2, midY + TrackHeight / 2f), TrackHeight / 2f);
			canvas.DrawRoundRect(val2, val);
			if (base.IsEnabled)
			{
				SKPaint val3 = new SKPaint
				{
					Color = new SKColor((byte)0, (byte)0, (byte)0, (byte)40),
					IsAntialias = true,
					MaskFilter = SKMaskFilter.CreateBlur((SKBlurStyle)0, 2f)
				};
				try
				{
					canvas.DrawCircle(num5 + 1f, midY + 1f, ThumbRadius, val3);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
			SKPaint val4 = new SKPaint
			{
				Color = (SKColor)(base.IsEnabled ? ThumbColor : new SKColor((byte)245, (byte)245, (byte)245)),
				IsAntialias = true,
				Style = (SKPaintStyle)0
			};
			try
			{
				canvas.DrawCircle(num5, midY, ThumbRadius, val4);
				if (base.IsFocused)
				{
					SKPaint val5 = new SKPaint();
					SKColor onTrackColor = OnTrackColor;
					val5.Color = ((SKColor)(ref onTrackColor)).WithAlpha((byte)60);
					val5.IsAntialias = true;
					val5.Style = (SKPaintStyle)1;
					val5.StrokeWidth = 3f;
					SKPaint val6 = val5;
					try
					{
						SKRoundRect val7 = new SKRoundRect(val2.Rect, TrackHeight / 2f);
						val7.Inflate(3f, 3f);
						canvas.DrawRoundRect(val7, val6);
						return;
					}
					finally
					{
						((IDisposable)val6)?.Dispose();
					}
				}
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private static SKColor InterpolateColor(SKColor from, SKColor to, float t)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		return new SKColor((byte)((float)(int)((SKColor)(ref from)).Red + (float)(((SKColor)(ref to)).Red - ((SKColor)(ref from)).Red) * t), (byte)((float)(int)((SKColor)(ref from)).Green + (float)(((SKColor)(ref to)).Green - ((SKColor)(ref from)).Green) * t), (byte)((float)(int)((SKColor)(ref from)).Blue + (float)(((SKColor)(ref to)).Blue - ((SKColor)(ref from)).Blue) * t), (byte)((float)(int)((SKColor)(ref from)).Alpha + (float)(((SKColor)(ref to)).Alpha - ((SKColor)(ref from)).Alpha) * t));
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		if (base.IsEnabled)
		{
			IsOn = !IsOn;
			e.Handled = true;
		}
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
	}

	public override void OnKeyDown(KeyEventArgs e)
	{
		if (base.IsEnabled && (e.Key == Key.Space || e.Key == Key.Enter))
		{
			IsOn = !IsOn;
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
		return new SKSize(TrackWidth + 8f, TrackHeight + 8f);
	}
}
