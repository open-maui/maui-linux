using System;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaActivityIndicator : SkiaView
{
	public static readonly BindableProperty IsRunningProperty = BindableProperty.Create("IsRunning", typeof(bool), typeof(SkiaActivityIndicator), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaActivityIndicator)(object)b).OnIsRunningChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ColorProperty = BindableProperty.Create("Color", typeof(SKColor), typeof(SkiaActivityIndicator), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaActivityIndicator)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty DisabledColorProperty = BindableProperty.Create("DisabledColor", typeof(SKColor), typeof(SkiaActivityIndicator), (object)new SKColor((byte)189, (byte)189, (byte)189), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaActivityIndicator)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty SizeProperty = BindableProperty.Create("Size", typeof(float), typeof(SkiaActivityIndicator), (object)32f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaActivityIndicator)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty StrokeWidthProperty = BindableProperty.Create("StrokeWidth", typeof(float), typeof(SkiaActivityIndicator), (object)3f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaActivityIndicator)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty RotationSpeedProperty = BindableProperty.Create("RotationSpeed", typeof(float), typeof(SkiaActivityIndicator), (object)360f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)null, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ArcCountProperty = BindableProperty.Create("ArcCount", typeof(int), typeof(SkiaActivityIndicator), (object)12, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaActivityIndicator)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private float _rotationAngle;

	private DateTime _lastUpdateTime = DateTime.UtcNow;

	public bool IsRunning
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(IsRunningProperty);
		}
		set
		{
			((BindableObject)this).SetValue(IsRunningProperty, (object)value);
		}
	}

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

	public float Size
	{
		get
		{
			return (float)((BindableObject)this).GetValue(SizeProperty);
		}
		set
		{
			((BindableObject)this).SetValue(SizeProperty, (object)value);
		}
	}

	public float StrokeWidth
	{
		get
		{
			return (float)((BindableObject)this).GetValue(StrokeWidthProperty);
		}
		set
		{
			((BindableObject)this).SetValue(StrokeWidthProperty, (object)value);
		}
	}

	public float RotationSpeed
	{
		get
		{
			return (float)((BindableObject)this).GetValue(RotationSpeedProperty);
		}
		set
		{
			((BindableObject)this).SetValue(RotationSpeedProperty, (object)value);
		}
	}

	public int ArcCount
	{
		get
		{
			return (int)((BindableObject)this).GetValue(ArcCountProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ArcCountProperty, (object)value);
		}
	}

	private void OnIsRunningChanged()
	{
		if (IsRunning)
		{
			_lastUpdateTime = DateTime.UtcNow;
		}
		Invalidate();
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Expected O, but got Unknown
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Expected O, but got Unknown
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		if (!IsRunning && !base.IsEnabled)
		{
			return;
		}
		float midX = ((SKRect)(ref bounds)).MidX;
		float midY = ((SKRect)(ref bounds)).MidY;
		float num = Math.Min(Size / 2f, Math.Min(((SKRect)(ref bounds)).Width, ((SKRect)(ref bounds)).Height) / 2f) - StrokeWidth;
		if (IsRunning)
		{
			DateTime utcNow = DateTime.UtcNow;
			double totalSeconds = (utcNow - _lastUpdateTime).TotalSeconds;
			_lastUpdateTime = utcNow;
			_rotationAngle = (_rotationAngle + (float)((double)RotationSpeed * totalSeconds)) % 360f;
		}
		canvas.Save();
		canvas.Translate(midX, midY);
		canvas.RotateDegrees(_rotationAngle);
		SKColor val = (base.IsEnabled ? Color : DisabledColor);
		for (int i = 0; i < ArcCount; i++)
		{
			byte b = (byte)(255f * (1f - (float)i / (float)ArcCount));
			SKColor color = ((SKColor)(ref val)).WithAlpha(b);
			SKPaint val2 = new SKPaint
			{
				Color = color,
				IsAntialias = true,
				Style = (SKPaintStyle)1,
				StrokeWidth = StrokeWidth,
				StrokeCap = (SKStrokeCap)1
			};
			try
			{
				float num2 = 360f / (float)ArcCount * (float)i;
				float num3 = 360f / (float)ArcCount / 2f;
				SKPath val3 = new SKPath();
				try
				{
					val3.AddArc(new SKRect(0f - num, 0f - num, num, num), num2, num3);
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
		canvas.Restore();
		if (IsRunning)
		{
			Invalidate();
		}
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		return new SKSize(Size + StrokeWidth * 2f, Size + StrokeWidth * 2f);
	}
}
