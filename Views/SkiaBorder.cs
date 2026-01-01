using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaBorder : SkiaLayoutView
{
	public static readonly BindableProperty StrokeThicknessProperty = BindableProperty.Create("StrokeThickness", typeof(float), typeof(SkiaBorder), (object)1f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaBorder)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create("CornerRadius", typeof(float), typeof(SkiaBorder), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaBorder)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty StrokeProperty = BindableProperty.Create("Stroke", typeof(SKColor), typeof(SkiaBorder), (object)SKColors.Black, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaBorder)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty PaddingLeftProperty = BindableProperty.Create("PaddingLeft", typeof(float), typeof(SkiaBorder), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaBorder)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty PaddingTopProperty = BindableProperty.Create("PaddingTop", typeof(float), typeof(SkiaBorder), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaBorder)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty PaddingRightProperty = BindableProperty.Create("PaddingRight", typeof(float), typeof(SkiaBorder), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaBorder)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty PaddingBottomProperty = BindableProperty.Create("PaddingBottom", typeof(float), typeof(SkiaBorder), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaBorder)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty HasShadowProperty = BindableProperty.Create("HasShadow", typeof(bool), typeof(SkiaBorder), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaBorder)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ShadowColorProperty = BindableProperty.Create("ShadowColor", typeof(SKColor), typeof(SkiaBorder), (object)new SKColor((byte)0, (byte)0, (byte)0, (byte)40), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaBorder)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ShadowBlurRadiusProperty = BindableProperty.Create("ShadowBlurRadius", typeof(float), typeof(SkiaBorder), (object)4f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaBorder)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ShadowOffsetXProperty = BindableProperty.Create("ShadowOffsetX", typeof(float), typeof(SkiaBorder), (object)2f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaBorder)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ShadowOffsetYProperty = BindableProperty.Create("ShadowOffsetY", typeof(float), typeof(SkiaBorder), (object)2f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaBorder)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private bool _isPressed;

	public float StrokeThickness
	{
		get
		{
			return (float)((BindableObject)this).GetValue(StrokeThicknessProperty);
		}
		set
		{
			((BindableObject)this).SetValue(StrokeThicknessProperty, (object)value);
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

	public SKColor Stroke
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(StrokeProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(StrokeProperty, (object)value);
		}
	}

	public float PaddingLeft
	{
		get
		{
			return (float)((BindableObject)this).GetValue(PaddingLeftProperty);
		}
		set
		{
			((BindableObject)this).SetValue(PaddingLeftProperty, (object)value);
		}
	}

	public float PaddingTop
	{
		get
		{
			return (float)((BindableObject)this).GetValue(PaddingTopProperty);
		}
		set
		{
			((BindableObject)this).SetValue(PaddingTopProperty, (object)value);
		}
	}

	public float PaddingRight
	{
		get
		{
			return (float)((BindableObject)this).GetValue(PaddingRightProperty);
		}
		set
		{
			((BindableObject)this).SetValue(PaddingRightProperty, (object)value);
		}
	}

	public float PaddingBottom
	{
		get
		{
			return (float)((BindableObject)this).GetValue(PaddingBottomProperty);
		}
		set
		{
			((BindableObject)this).SetValue(PaddingBottomProperty, (object)value);
		}
	}

	public bool HasShadow
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(HasShadowProperty);
		}
		set
		{
			((BindableObject)this).SetValue(HasShadowProperty, (object)value);
		}
	}

	public SKColor ShadowColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(ShadowColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(ShadowColorProperty, (object)value);
		}
	}

	public float ShadowBlurRadius
	{
		get
		{
			return (float)((BindableObject)this).GetValue(ShadowBlurRadiusProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ShadowBlurRadiusProperty, (object)value);
		}
	}

	public float ShadowOffsetX
	{
		get
		{
			return (float)((BindableObject)this).GetValue(ShadowOffsetXProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ShadowOffsetXProperty, (object)value);
		}
	}

	public float ShadowOffsetY
	{
		get
		{
			return (float)((BindableObject)this).GetValue(ShadowOffsetYProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ShadowOffsetYProperty, (object)value);
		}
	}

	public event EventHandler? Tapped;

	public void SetPadding(float all)
	{
		float num = (PaddingBottom = all);
		float num3 = (PaddingRight = num);
		float paddingLeft = (PaddingTop = num3);
		PaddingLeft = paddingLeft;
	}

	public void SetPadding(float horizontal, float vertical)
	{
		float paddingLeft = (PaddingRight = horizontal);
		PaddingLeft = paddingLeft;
		paddingLeft = (PaddingBottom = vertical);
		PaddingTop = paddingLeft;
	}

	public void SetPadding(float left, float top, float right, float bottom)
	{
		PaddingLeft = left;
		PaddingTop = top;
		PaddingRight = right;
		PaddingBottom = bottom;
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Expected O, but got Unknown
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Expected O, but got Unknown
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Expected O, but got Unknown
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Expected O, but got Unknown
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Expected O, but got Unknown
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Expected O, but got Unknown
		float strokeThickness = StrokeThickness;
		float cornerRadius = CornerRadius;
		SKRect val = default(SKRect);
		((SKRect)(ref val))._002Ector(((SKRect)(ref bounds)).Left + strokeThickness / 2f, ((SKRect)(ref bounds)).Top + strokeThickness / 2f, ((SKRect)(ref bounds)).Right - strokeThickness / 2f, ((SKRect)(ref bounds)).Bottom - strokeThickness / 2f);
		if (HasShadow)
		{
			SKPaint val2 = new SKPaint
			{
				Color = ShadowColor,
				MaskFilter = SKMaskFilter.CreateBlur((SKBlurStyle)0, ShadowBlurRadius),
				Style = (SKPaintStyle)0
			};
			try
			{
				SKRect val3 = new SKRect(((SKRect)(ref val)).Left + ShadowOffsetX, ((SKRect)(ref val)).Top + ShadowOffsetY, ((SKRect)(ref val)).Right + ShadowOffsetX, ((SKRect)(ref val)).Bottom + ShadowOffsetY);
				canvas.DrawRoundRect(new SKRoundRect(val3, cornerRadius), val2);
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		SKPaint val4 = new SKPaint
		{
			Color = base.BackgroundColor,
			Style = (SKPaintStyle)0,
			IsAntialias = true
		};
		try
		{
			canvas.DrawRoundRect(new SKRoundRect(val, cornerRadius), val4);
			if (strokeThickness > 0f)
			{
				SKPaint val5 = new SKPaint
				{
					Color = Stroke,
					Style = (SKPaintStyle)1,
					StrokeWidth = strokeThickness,
					IsAntialias = true
				};
				try
				{
					canvas.DrawRoundRect(new SKRoundRect(val, cornerRadius), val5);
				}
				finally
				{
					((IDisposable)val5)?.Dispose();
				}
			}
			foreach (SkiaView child in base.Children)
			{
				if (child.IsVisible)
				{
					child.Draw(canvas);
				}
			}
		}
		finally
		{
			((IDisposable)val4)?.Dispose();
		}
	}

	protected override SKRect GetContentBounds()
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return GetContentBounds(base.Bounds);
	}

	protected new SKRect GetContentBounds(SKRect bounds)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		float strokeThickness = StrokeThickness;
		return new SKRect(((SKRect)(ref bounds)).Left + PaddingLeft + strokeThickness, ((SKRect)(ref bounds)).Top + PaddingTop + strokeThickness, ((SKRect)(ref bounds)).Right - PaddingRight - strokeThickness, ((SKRect)(ref bounds)).Bottom - PaddingBottom - strokeThickness);
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		float strokeThickness = StrokeThickness;
		float num = PaddingLeft + PaddingRight + strokeThickness * 2f;
		float num2 = PaddingTop + PaddingBottom + strokeThickness * 2f;
		float num3 = ((base.WidthRequest >= 0.0) ? ((float)base.WidthRequest) : ((SKSize)(ref availableSize)).Width);
		float num4 = ((base.HeightRequest >= 0.0) ? ((float)base.HeightRequest) : ((SKSize)(ref availableSize)).Height);
		SKSize availableSize2 = default(SKSize);
		((SKSize)(ref availableSize2))._002Ector(Math.Max(0f, num3 - num), Math.Max(0f, num4 - num2));
		SKSize val = SKSize.Empty;
		foreach (SkiaView child in base.Children)
		{
			SKSize val2 = child.Measure(availableSize2);
			val = new SKSize(Math.Max(((SKSize)(ref val)).Width, ((SKSize)(ref val2)).Width), Math.Max(((SKSize)(ref val)).Height, ((SKSize)(ref val2)).Height));
		}
		float num5 = ((base.WidthRequest >= 0.0) ? ((float)base.WidthRequest) : (((SKSize)(ref val)).Width + num));
		float num6 = ((base.HeightRequest >= 0.0) ? ((float)base.HeightRequest) : (((SKSize)(ref val)).Height + num2));
		return new SKSize(num5, num6);
	}

	protected override SKRect ArrangeOverride(SKRect bounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		SKRect contentBounds = GetContentBounds(bounds);
		SKRect bounds2 = default(SKRect);
		foreach (SkiaView child in base.Children)
		{
			Thickness margin = child.Margin;
			((SKRect)(ref bounds2))._002Ector(((SKRect)(ref contentBounds)).Left + (float)((Thickness)(ref margin)).Left, ((SKRect)(ref contentBounds)).Top + (float)((Thickness)(ref margin)).Top, ((SKRect)(ref contentBounds)).Right - (float)((Thickness)(ref margin)).Right, ((SKRect)(ref contentBounds)).Bottom - (float)((Thickness)(ref margin)).Bottom);
			child.Arrange(bounds2);
		}
		return bounds;
	}

	private bool HasTapGestureRecognizers()
	{
		View? mauiView = base.MauiView;
		if (((mauiView != null) ? mauiView.GestureRecognizers : null) == null)
		{
			return false;
		}
		foreach (IGestureRecognizer gestureRecognizer in base.MauiView.GestureRecognizers)
		{
			if (gestureRecognizer is TapGestureRecognizer)
			{
				return true;
			}
		}
		return false;
	}

	public override SkiaView? HitTest(float x, float y)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (base.IsVisible && base.IsEnabled)
		{
			SKRect bounds = base.Bounds;
			if (((SKRect)(ref bounds)).Contains(new SKPoint(x, y)))
			{
				if (HasTapGestureRecognizers())
				{
					Console.WriteLine("[SkiaBorder.HitTest] Intercepting for gesture - returning self");
					return this;
				}
				return base.HitTest(x, y);
			}
		}
		return null;
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		if (HasTapGestureRecognizers())
		{
			_isPressed = true;
			e.Handled = true;
			Console.WriteLine("[SkiaBorder] OnPointerPressed INTERCEPTED for gesture, MauiView=" + ((object)base.MauiView)?.GetType().Name);
			if (base.MauiView != null)
			{
				GestureManager.ProcessPointerDown(base.MauiView, e.X, e.Y);
			}
		}
		else
		{
			base.OnPointerPressed(e);
		}
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		if (_isPressed)
		{
			_isPressed = false;
			e.Handled = true;
			Console.WriteLine("[SkiaBorder] OnPointerReleased - processing gesture recognizers, MauiView=" + ((object)base.MauiView)?.GetType().Name);
			if (base.MauiView != null)
			{
				GestureManager.ProcessPointerUp(base.MauiView, e.X, e.Y);
			}
			this.Tapped?.Invoke(this, EventArgs.Empty);
		}
		else
		{
			base.OnPointerReleased(e);
		}
	}

	public override void OnPointerExited(PointerEventArgs e)
	{
		base.OnPointerExited(e);
		_isPressed = false;
	}
}
