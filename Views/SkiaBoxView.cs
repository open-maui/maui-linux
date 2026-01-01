using System;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaBoxView : SkiaView
{
	public static readonly BindableProperty ColorProperty = BindableProperty.Create("Color", typeof(SKColor), typeof(SkiaBoxView), (object)SKColors.Transparent, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaBoxView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create("CornerRadius", typeof(float), typeof(SkiaBoxView), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaBoxView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public SKColor Color
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(ColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(ColorProperty, (object)value);
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

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = Color,
			Style = (SKPaintStyle)0,
			IsAntialias = true
		};
		try
		{
			if (CornerRadius > 0f)
			{
				canvas.DrawRoundRect(bounds, CornerRadius, CornerRadius, val);
			}
			else
			{
				canvas.DrawRect(bounds, val);
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		float num = ((base.WidthRequest >= 0.0) ? ((float)base.WidthRequest) : (float.IsInfinity(((SKSize)(ref availableSize)).Width) ? 40f : ((SKSize)(ref availableSize)).Width));
		float num2 = ((base.HeightRequest >= 0.0) ? ((float)base.HeightRequest) : (float.IsInfinity(((SKSize)(ref availableSize)).Height) ? 40f : ((SKSize)(ref availableSize)).Height));
		if (float.IsNaN(num))
		{
			num = 40f;
		}
		if (float.IsNaN(num2))
		{
			num2 = 40f;
		}
		return new SKSize(num, num2);
	}
}
