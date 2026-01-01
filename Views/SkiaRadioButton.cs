using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaRadioButton : SkiaView
{
	public static readonly BindableProperty IsCheckedProperty = BindableProperty.Create("IsChecked", typeof(bool), typeof(SkiaRadioButton), (object)false, (BindingMode)1, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaRadioButton)(object)b).OnIsCheckedChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ContentProperty = BindableProperty.Create("Content", typeof(string), typeof(SkiaRadioButton), (object)"", (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaRadioButton)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ValueProperty = BindableProperty.Create("Value", typeof(object), typeof(SkiaRadioButton), (object)null, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)null, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty GroupNameProperty = BindableProperty.Create("GroupName", typeof(string), typeof(SkiaRadioButton), (object)null, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaRadioButton)(object)b).OnGroupNameChanged((string)o, (string)n);
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty RadioColorProperty = BindableProperty.Create("RadioColor", typeof(SKColor), typeof(SkiaRadioButton), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaRadioButton)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty UncheckedColorProperty = BindableProperty.Create("UncheckedColor", typeof(SKColor), typeof(SkiaRadioButton), (object)new SKColor((byte)117, (byte)117, (byte)117), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaRadioButton)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TextColorProperty = BindableProperty.Create("TextColor", typeof(SKColor), typeof(SkiaRadioButton), (object)SKColors.Black, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaRadioButton)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty DisabledColorProperty = BindableProperty.Create("DisabledColor", typeof(SKColor), typeof(SkiaRadioButton), (object)new SKColor((byte)189, (byte)189, (byte)189), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaRadioButton)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FontSizeProperty = BindableProperty.Create("FontSize", typeof(float), typeof(SkiaRadioButton), (object)14f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaRadioButton)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty RadioSizeProperty = BindableProperty.Create("RadioSize", typeof(float), typeof(SkiaRadioButton), (object)20f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaRadioButton)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty SpacingProperty = BindableProperty.Create("Spacing", typeof(float), typeof(SkiaRadioButton), (object)8f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaRadioButton)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private static readonly Dictionary<string, List<WeakReference<SkiaRadioButton>>> _groups = new Dictionary<string, List<WeakReference<SkiaRadioButton>>>();

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

	public string Content
	{
		get
		{
			return (string)((BindableObject)this).GetValue(ContentProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ContentProperty, (object)value);
		}
	}

	public object? Value
	{
		get
		{
			return ((BindableObject)this).GetValue(ValueProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ValueProperty, value);
		}
	}

	public string? GroupName
	{
		get
		{
			return (string)((BindableObject)this).GetValue(GroupNameProperty);
		}
		set
		{
			((BindableObject)this).SetValue(GroupNameProperty, (object)value);
		}
	}

	public SKColor RadioColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(RadioColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(RadioColorProperty, (object)value);
		}
	}

	public SKColor UncheckedColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(UncheckedColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(UncheckedColorProperty, (object)value);
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

	public float RadioSize
	{
		get
		{
			return (float)((BindableObject)this).GetValue(RadioSizeProperty);
		}
		set
		{
			((BindableObject)this).SetValue(RadioSizeProperty, (object)value);
		}
	}

	public float Spacing
	{
		get
		{
			return (float)((BindableObject)this).GetValue(SpacingProperty);
		}
		set
		{
			((BindableObject)this).SetValue(SpacingProperty, (object)value);
		}
	}

	public event EventHandler? CheckedChanged;

	public SkiaRadioButton()
	{
		base.IsFocusable = true;
	}

	private void OnIsCheckedChanged()
	{
		if (IsChecked && !string.IsNullOrEmpty(GroupName))
		{
			UncheckOthersInGroup();
		}
		this.CheckedChanged?.Invoke(this, EventArgs.Empty);
		SkiaVisualStateManager.GoToState(this, IsChecked ? "Checked" : "Unchecked");
		Invalidate();
	}

	private void OnGroupNameChanged(string? oldValue, string? newValue)
	{
		RemoveFromGroup(oldValue);
		AddToGroup(newValue);
	}

	private void AddToGroup(string? groupName)
	{
		if (!string.IsNullOrEmpty(groupName))
		{
			if (!_groups.TryGetValue(groupName, out List<WeakReference<SkiaRadioButton>> value))
			{
				value = new List<WeakReference<SkiaRadioButton>>();
				_groups[groupName] = value;
			}
			value.RemoveAll((WeakReference<SkiaRadioButton> wr) => !wr.TryGetTarget(out SkiaRadioButton _));
			value.Add(new WeakReference<SkiaRadioButton>(this));
		}
	}

	private void RemoveFromGroup(string? groupName)
	{
		if (!string.IsNullOrEmpty(groupName) && _groups.TryGetValue(groupName, out List<WeakReference<SkiaRadioButton>> value))
		{
			value.RemoveAll((WeakReference<SkiaRadioButton> wr) => !wr.TryGetTarget(out SkiaRadioButton target) || target == this);
			if (value.Count == 0)
			{
				_groups.Remove(groupName);
			}
		}
	}

	private void UncheckOthersInGroup()
	{
		if (string.IsNullOrEmpty(GroupName) || !_groups.TryGetValue(GroupName, out List<WeakReference<SkiaRadioButton>> value))
		{
			return;
		}
		foreach (WeakReference<SkiaRadioButton> item in value)
		{
			if (item.TryGetTarget(out var target) && target != this && target.IsChecked)
			{
				((BindableObject)target).SetValue(IsCheckedProperty, (object)false);
			}
		}
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Expected O, but got Unknown
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Expected O, but got Unknown
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Expected O, but got Unknown
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Expected O, but got Unknown
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Expected O, but got Unknown
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		float num = RadioSize / 2f;
		float num2 = ((SKRect)(ref bounds)).Left + num;
		float midY = ((SKRect)(ref bounds)).MidY;
		SKPaint val = new SKPaint
		{
			Color = ((!base.IsEnabled) ? DisabledColor : (IsChecked ? RadioColor : UncheckedColor)),
			Style = (SKPaintStyle)1,
			StrokeWidth = 2f,
			IsAntialias = true
		};
		try
		{
			canvas.DrawCircle(num2, midY, num - 1f, val);
			if (IsChecked)
			{
				SKPaint val2 = new SKPaint
				{
					Color = (base.IsEnabled ? RadioColor : DisabledColor),
					Style = (SKPaintStyle)0,
					IsAntialias = true
				};
				try
				{
					canvas.DrawCircle(num2, midY, num - 5f, val2);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			if (base.IsFocused)
			{
				SKPaint val3 = new SKPaint();
				SKColor radioColor = RadioColor;
				val3.Color = ((SKColor)(ref radioColor)).WithAlpha((byte)80);
				val3.Style = (SKPaintStyle)0;
				val3.IsAntialias = true;
				SKPaint val4 = val3;
				try
				{
					canvas.DrawCircle(num2, midY, num + 4f, val4);
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			if (string.IsNullOrEmpty(Content))
			{
				return;
			}
			SKFont val5 = new SKFont(SKTypeface.Default, FontSize, 1f, 0f);
			try
			{
				SKPaint val6 = new SKPaint(val5)
				{
					Color = (base.IsEnabled ? TextColor : DisabledColor),
					IsAntialias = true
				};
				try
				{
					float num3 = ((SKRect)(ref bounds)).Left + RadioSize + Spacing;
					SKRect val7 = default(SKRect);
					val6.MeasureText(Content, ref val7);
					canvas.DrawText(Content, num3, ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val7)).MidY, val6);
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
			((IDisposable)val)?.Dispose();
		}
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		if (base.IsEnabled && !IsChecked)
		{
			IsChecked = true;
		}
	}

	public override void OnKeyDown(KeyEventArgs e)
	{
		if (base.IsEnabled && (e.Key == Key.Space || e.Key == Key.Enter))
		{
			if (!IsChecked)
			{
				IsChecked = true;
			}
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
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		float num = 0f;
		if (!string.IsNullOrEmpty(Content))
		{
			SKFont val = new SKFont(SKTypeface.Default, FontSize, 1f, 0f);
			try
			{
				SKPaint val2 = new SKPaint(val);
				try
				{
					num = val2.MeasureText(Content) + Spacing;
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
		return new SKSize(RadioSize + num, Math.Max(RadioSize, FontSize * 1.5f));
	}
}
