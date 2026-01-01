using System;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaProgressBar : SkiaView
{
	public static readonly BindableProperty ProgressProperty = BindableProperty.Create("Progress", typeof(double), typeof(SkiaProgressBar), (object)0.0, (BindingMode)1, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaProgressBar)(object)b).OnProgressChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)((BindableObject b, object v) => Math.Clamp((double)v, 0.0, 1.0)), (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TrackColorProperty = BindableProperty.Create("TrackColor", typeof(SKColor), typeof(SkiaProgressBar), (object)new SKColor((byte)224, (byte)224, (byte)224), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaProgressBar)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ProgressColorProperty = BindableProperty.Create("ProgressColor", typeof(SKColor), typeof(SkiaProgressBar), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaProgressBar)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty DisabledColorProperty = BindableProperty.Create("DisabledColor", typeof(SKColor), typeof(SkiaProgressBar), (object)new SKColor((byte)189, (byte)189, (byte)189), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaProgressBar)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty BarHeightProperty = BindableProperty.Create("BarHeight", typeof(float), typeof(SkiaProgressBar), (object)4f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaProgressBar)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create("CornerRadius", typeof(float), typeof(SkiaProgressBar), (object)2f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaProgressBar)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public double Progress
	{
		get
		{
			return (double)((BindableObject)this).GetValue(ProgressProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ProgressProperty, (object)value);
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

	public SKColor ProgressColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(ProgressColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(ProgressColorProperty, (object)value);
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

	public float BarHeight
	{
		get
		{
			return (float)((BindableObject)this).GetValue(BarHeightProperty);
		}
		set
		{
			((BindableObject)this).SetValue(BarHeightProperty, (object)value);
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

	public event EventHandler<ProgressChangedEventArgs>? ProgressChanged;

	private void OnProgressChanged()
	{
		this.ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(Progress));
		Invalidate();
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Expected O, but got Unknown
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Expected O, but got Unknown
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Expected O, but got Unknown
		float midY = ((SKRect)(ref bounds)).MidY;
		float num = midY - BarHeight / 2f;
		float num2 = midY + BarHeight / 2f;
		SKPaint val = new SKPaint
		{
			Color = (base.IsEnabled ? TrackColor : DisabledColor),
			IsAntialias = true,
			Style = (SKPaintStyle)0
		};
		try
		{
			SKRoundRect val2 = new SKRoundRect(new SKRect(((SKRect)(ref bounds)).Left, num, ((SKRect)(ref bounds)).Right, num2), CornerRadius);
			canvas.DrawRoundRect(val2, val);
			if (Progress > 0.0)
			{
				float num3 = ((SKRect)(ref bounds)).Width * (float)Progress;
				SKPaint val3 = new SKPaint
				{
					Color = (base.IsEnabled ? ProgressColor : DisabledColor),
					IsAntialias = true,
					Style = (SKPaintStyle)0
				};
				try
				{
					SKRoundRect val4 = new SKRoundRect(new SKRect(((SKRect)(ref bounds)).Left, num, ((SKRect)(ref bounds)).Left + num3, num2), CornerRadius);
					canvas.DrawRoundRect(val4, val3);
					return;
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		return new SKSize(200f, BarHeight + 8f);
	}
}
