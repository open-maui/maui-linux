using System;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaButton : SkiaView
{
	public static readonly BindableProperty TextProperty = BindableProperty.Create("Text", typeof(string), typeof(SkiaButton), (object)"", (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).OnTextChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TextColorProperty = BindableProperty.Create("TextColor", typeof(SKColor), typeof(SkiaButton), (object)SKColors.White, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ButtonBackgroundColorProperty = BindableProperty.Create("ButtonBackgroundColor", typeof(SKColor), typeof(SkiaButton), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty PressedBackgroundColorProperty = BindableProperty.Create("PressedBackgroundColor", typeof(SKColor), typeof(SkiaButton), (object)new SKColor((byte)25, (byte)118, (byte)210), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty DisabledBackgroundColorProperty = BindableProperty.Create("DisabledBackgroundColor", typeof(SKColor), typeof(SkiaButton), (object)new SKColor((byte)189, (byte)189, (byte)189), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty HoveredBackgroundColorProperty = BindableProperty.Create("HoveredBackgroundColor", typeof(SKColor), typeof(SkiaButton), (object)new SKColor((byte)66, (byte)165, (byte)245), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty BorderColorProperty = BindableProperty.Create("BorderColor", typeof(SKColor), typeof(SkiaButton), (object)SKColors.Transparent, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create("FontFamily", typeof(string), typeof(SkiaButton), (object)"Sans", (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).OnFontChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FontSizeProperty = BindableProperty.Create("FontSize", typeof(float), typeof(SkiaButton), (object)14f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).OnFontChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty IsBoldProperty = BindableProperty.Create("IsBold", typeof(bool), typeof(SkiaButton), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).OnFontChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty IsItalicProperty = BindableProperty.Create("IsItalic", typeof(bool), typeof(SkiaButton), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).OnFontChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CharacterSpacingProperty = BindableProperty.Create("CharacterSpacing", typeof(float), typeof(SkiaButton), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create("CornerRadius", typeof(float), typeof(SkiaButton), (object)4f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty BorderWidthProperty = BindableProperty.Create("BorderWidth", typeof(float), typeof(SkiaButton), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty PaddingProperty = BindableProperty.Create("Padding", typeof(SKRect), typeof(SkiaButton), (object)new SKRect(16f, 8f, 16f, 8f), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CommandProperty = BindableProperty.Create("Command", typeof(ICommand), typeof(SkiaButton), (object)null, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).OnCommandChanged((ICommand)o, (ICommand)n);
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create("CommandParameter", typeof(object), typeof(SkiaButton), (object)null, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)null, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ImageSourceProperty = BindableProperty.Create("ImageSource", typeof(SKBitmap), typeof(SkiaButton), (object)null, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ImageSpacingProperty = BindableProperty.Create("ImageSpacing", typeof(float), typeof(SkiaButton), (object)8f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ContentLayoutPositionProperty = BindableProperty.Create("ContentLayoutPosition", typeof(int), typeof(SkiaButton), (object)0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaButton)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private bool _focusFromKeyboard;

	public string Text
	{
		get
		{
			return (string)((BindableObject)this).GetValue(TextProperty);
		}
		set
		{
			((BindableObject)this).SetValue(TextProperty, (object)value);
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

	public SKColor PressedBackgroundColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(PressedBackgroundColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(PressedBackgroundColorProperty, (object)value);
		}
	}

	public SKColor DisabledBackgroundColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(DisabledBackgroundColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(DisabledBackgroundColorProperty, (object)value);
		}
	}

	public SKColor HoveredBackgroundColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(HoveredBackgroundColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(HoveredBackgroundColorProperty, (object)value);
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

	public string FontFamily
	{
		get
		{
			return (string)((BindableObject)this).GetValue(FontFamilyProperty);
		}
		set
		{
			((BindableObject)this).SetValue(FontFamilyProperty, (object)value);
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

	public bool IsBold
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(IsBoldProperty);
		}
		set
		{
			((BindableObject)this).SetValue(IsBoldProperty, (object)value);
		}
	}

	public bool IsItalic
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(IsItalicProperty);
		}
		set
		{
			((BindableObject)this).SetValue(IsItalicProperty, (object)value);
		}
	}

	public float CharacterSpacing
	{
		get
		{
			return (float)((BindableObject)this).GetValue(CharacterSpacingProperty);
		}
		set
		{
			((BindableObject)this).SetValue(CharacterSpacingProperty, (object)value);
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

	public SKRect Padding
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKRect)((BindableObject)this).GetValue(PaddingProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(PaddingProperty, (object)value);
		}
	}

	public ICommand? Command
	{
		get
		{
			return (ICommand)((BindableObject)this).GetValue(CommandProperty);
		}
		set
		{
			((BindableObject)this).SetValue(CommandProperty, (object)value);
		}
	}

	public object? CommandParameter
	{
		get
		{
			return ((BindableObject)this).GetValue(CommandParameterProperty);
		}
		set
		{
			((BindableObject)this).SetValue(CommandParameterProperty, value);
		}
	}

	public SKBitmap? ImageSource
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Expected O, but got Unknown
			return (SKBitmap)((BindableObject)this).GetValue(ImageSourceProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ImageSourceProperty, (object)value);
		}
	}

	public float ImageSpacing
	{
		get
		{
			return (float)((BindableObject)this).GetValue(ImageSpacingProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ImageSpacingProperty, (object)value);
		}
	}

	public int ContentLayoutPosition
	{
		get
		{
			return (int)((BindableObject)this).GetValue(ContentLayoutPositionProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ContentLayoutPositionProperty, (object)value);
		}
	}

	public bool IsPressed { get; private set; }

	public bool IsHovered { get; private set; }

	public event EventHandler? Clicked;

	public event EventHandler? Pressed;

	public event EventHandler? Released;

	public SkiaButton()
	{
		base.IsFocusable = true;
	}

	private void OnTextChanged()
	{
		InvalidateMeasure();
		Invalidate();
	}

	private void OnFontChanged()
	{
		InvalidateMeasure();
		Invalidate();
	}

	private void OnCommandChanged(ICommand? oldCommand, ICommand? newCommand)
	{
		if (oldCommand != null)
		{
			oldCommand.CanExecuteChanged -= OnCanExecuteChanged;
		}
		if (newCommand != null)
		{
			newCommand.CanExecuteChanged += OnCanExecuteChanged;
			UpdateIsEnabled();
		}
	}

	private void OnCanExecuteChanged(object? sender, EventArgs e)
	{
		UpdateIsEnabled();
	}

	private void UpdateIsEnabled()
	{
		if (Command != null)
		{
			base.IsEnabled = Command.CanExecute(CommandParameter);
		}
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected O, but got Unknown
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Expected O, but got Unknown
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Expected O, but got Unknown
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Expected O, but got Unknown
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Expected O, but got Unknown
		SKColor buttonBackgroundColor = ButtonBackgroundColor;
		bool flag = ((SKColor)(ref buttonBackgroundColor)).Alpha == 0;
		SKColor color = (SKColor)((!base.IsEnabled) ? (flag ? SKColors.Transparent : DisabledBackgroundColor) : (IsPressed ? (flag ? new SKColor((byte)0, (byte)0, (byte)0, (byte)20) : PressedBackgroundColor) : ((!IsHovered) ? ButtonBackgroundColor : (flag ? new SKColor((byte)0, (byte)0, (byte)0, (byte)10) : HoveredBackgroundColor))));
		if (base.IsEnabled && !IsPressed && !flag)
		{
			DrawShadow(canvas, bounds);
		}
		SKRoundRect val = new SKRoundRect(bounds, CornerRadius);
		if (((SKColor)(ref color)).Alpha > 0)
		{
			SKPaint val2 = new SKPaint
			{
				Color = color,
				IsAntialias = true,
				Style = (SKPaintStyle)0
			};
			try
			{
				canvas.DrawRoundRect(val, val2);
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		if (BorderWidth > 0f && BorderColor != SKColors.Transparent)
		{
			SKPaint val3 = new SKPaint
			{
				Color = BorderColor,
				IsAntialias = true,
				Style = (SKPaintStyle)1,
				StrokeWidth = BorderWidth
			};
			try
			{
				canvas.DrawRoundRect(val, val3);
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		if (base.IsFocused && _focusFromKeyboard)
		{
			SKPaint val4 = new SKPaint
			{
				Color = new SKColor((byte)33, (byte)150, (byte)243, (byte)128),
				IsAntialias = true,
				Style = (SKPaintStyle)1,
				StrokeWidth = 2f
			};
			try
			{
				SKRoundRect val5 = new SKRoundRect(bounds, CornerRadius + 2f);
				val5.Inflate(2f, 2f);
				canvas.DrawRoundRect(val5, val4);
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
		}
		DrawContent(canvas, bounds, flag);
	}

	private void DrawContent(SKCanvas canvas, SKRect bounds, bool isTextOnly)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Expected O, but got Unknown
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Expected O, but got Unknown
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03be: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c7: Expected O, but got Unknown
		//IL_03fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e5: Unknown result type (might be due to invalid IL or missing references)
		SKFontStyle style = new SKFontStyle((SKFontStyleWeight)(IsBold ? 700 : 400), (SKFontStyleWidth)5, (SKFontStyleSlant)(IsItalic ? 1 : 0));
		SKFont val = new SKFont(SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, style) ?? SKTypeface.Default, FontSize, 1f, 0f);
		try
		{
			SKColor textColor;
			SKColor color;
			if (!base.IsEnabled)
			{
				textColor = TextColor;
				color = ((SKColor)(ref textColor)).WithAlpha((byte)128);
			}
			else if (isTextOnly && (IsHovered || IsPressed))
			{
				textColor = TextColor;
				byte num = (byte)Math.Max(0, ((SKColor)(ref textColor)).Red - 40);
				textColor = TextColor;
				byte num2 = (byte)Math.Max(0, ((SKColor)(ref textColor)).Green - 40);
				textColor = TextColor;
				byte num3 = (byte)Math.Max(0, ((SKColor)(ref textColor)).Blue - 40);
				textColor = TextColor;
				color = new SKColor(num, num2, num3, ((SKColor)(ref textColor)).Alpha);
			}
			else
			{
				color = TextColor;
			}
			SKPaint val2 = new SKPaint(val)
			{
				Color = color,
				IsAntialias = true
			};
			try
			{
				SKRect val3 = default(SKRect);
				bool flag = !string.IsNullOrEmpty(Text);
				if (flag)
				{
					val2.MeasureText(Text, ref val3);
				}
				bool flag2 = ImageSource != null;
				float num4 = 0f;
				float num5 = 0f;
				if (flag2)
				{
					float num6 = Math.Min(((SKRect)(ref bounds)).Height - 8f, 24f);
					float num7 = Math.Min(num6 / (float)ImageSource.Width, num6 / (float)ImageSource.Height);
					num4 = (float)ImageSource.Width * num7;
					num5 = (float)ImageSource.Height * num7;
				}
				bool flag3 = ContentLayoutPosition == 0 || ContentLayoutPosition == 2;
				float num8;
				float num9;
				if (flag2 && flag)
				{
					if (flag3)
					{
						num8 = num4 + ImageSpacing + ((SKRect)(ref val3)).Width;
						num9 = Math.Max(num5, ((SKRect)(ref val3)).Height);
					}
					else
					{
						num8 = Math.Max(num4, ((SKRect)(ref val3)).Width);
						num9 = num5 + ImageSpacing + ((SKRect)(ref val3)).Height;
					}
				}
				else if (flag2)
				{
					num8 = num4;
					num9 = num5;
				}
				else
				{
					num8 = ((SKRect)(ref val3)).Width;
					num9 = ((SKRect)(ref val3)).Height;
				}
				float num10 = ((SKRect)(ref bounds)).MidX - num8 / 2f;
				float num11 = ((SKRect)(ref bounds)).MidY - num9 / 2f;
				if (flag2)
				{
					float num12 = 0f;
					float num13 = 0f;
					float num14;
					float num15;
					switch (ContentLayoutPosition)
					{
					case 1:
						num14 = ((SKRect)(ref bounds)).MidX - num4 / 2f;
						num15 = num11;
						num12 = ((SKRect)(ref bounds)).MidX - ((SKRect)(ref val3)).Width / 2f;
						num13 = num11 + num5 + ImageSpacing - ((SKRect)(ref val3)).Top;
						break;
					case 2:
						num12 = num10;
						num13 = ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val3)).MidY;
						num14 = num10 + ((SKRect)(ref val3)).Width + ImageSpacing;
						num15 = ((SKRect)(ref bounds)).MidY - num5 / 2f;
						break;
					case 3:
						num12 = ((SKRect)(ref bounds)).MidX - ((SKRect)(ref val3)).Width / 2f;
						num13 = num11 - ((SKRect)(ref val3)).Top;
						num14 = ((SKRect)(ref bounds)).MidX - num4 / 2f;
						num15 = num11 + ((SKRect)(ref val3)).Height + ImageSpacing;
						break;
					default:
						num14 = num10;
						num15 = ((SKRect)(ref bounds)).MidY - num5 / 2f;
						num12 = num10 + num4 + ImageSpacing;
						num13 = ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val3)).MidY;
						break;
					}
					SKRect val4 = default(SKRect);
					((SKRect)(ref val4))._002Ector(num14, num15, num14 + num4, num15 + num5);
					SKPaint val5 = new SKPaint
					{
						IsAntialias = true
					};
					try
					{
						if (!base.IsEnabled)
						{
							val5.ColorFilter = SKColorFilter.CreateBlendMode(new SKColor((byte)128, (byte)128, (byte)128, (byte)128), (SKBlendMode)5);
						}
						canvas.DrawBitmap(ImageSource, val4, val5);
						if (flag)
						{
							canvas.DrawText(Text, num12, num13, val2);
						}
						return;
					}
					finally
					{
						((IDisposable)val5)?.Dispose();
					}
				}
				if (flag)
				{
					float num16 = ((SKRect)(ref bounds)).MidX - ((SKRect)(ref val3)).MidX;
					float num17 = ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val3)).MidY;
					canvas.DrawText(Text, num16, num17, val2);
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

	private void DrawShadow(SKCanvas canvas, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Expected O, but got Unknown
		SKPaint val = new SKPaint
		{
			Color = new SKColor((byte)0, (byte)0, (byte)0, (byte)50),
			IsAntialias = true,
			MaskFilter = SKMaskFilter.CreateBlur((SKBlurStyle)0, 4f)
		};
		try
		{
			SKRoundRect val2 = new SKRoundRect(new SKRect(((SKRect)(ref bounds)).Left + 2f, ((SKRect)(ref bounds)).Top + 4f, ((SKRect)(ref bounds)).Right + 2f, ((SKRect)(ref bounds)).Bottom + 4f), CornerRadius);
			canvas.DrawRoundRect(val2, val);
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
		if (IsPressed)
		{
			IsPressed = false;
		}
		SkiaVisualStateManager.GoToState(this, base.IsEnabled ? "Normal" : "Disabled");
		Invalidate();
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		Console.WriteLine($"[SkiaButton] OnPointerPressed - Text='{Text}', IsEnabled={base.IsEnabled}");
		if (base.IsEnabled)
		{
			IsPressed = true;
			_focusFromKeyboard = false;
			SkiaVisualStateManager.GoToState(this, "Pressed");
			Invalidate();
			this.Pressed?.Invoke(this, EventArgs.Empty);
		}
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		if (base.IsEnabled)
		{
			bool isPressed = IsPressed;
			IsPressed = false;
			SkiaVisualStateManager.GoToState(this, IsHovered ? "PointerOver" : "Normal");
			Invalidate();
			this.Released?.Invoke(this, EventArgs.Empty);
			if (isPressed)
			{
				this.Clicked?.Invoke(this, EventArgs.Empty);
				Command?.Execute(CommandParameter);
			}
		}
	}

	public override void OnKeyDown(KeyEventArgs e)
	{
		if (base.IsEnabled && (e.Key == Key.Enter || e.Key == Key.Space))
		{
			IsPressed = true;
			_focusFromKeyboard = true;
			SkiaVisualStateManager.GoToState(this, "Pressed");
			Invalidate();
			this.Pressed?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
		}
	}

	public override void OnKeyUp(KeyEventArgs e)
	{
		if (base.IsEnabled && (e.Key == Key.Enter || e.Key == Key.Space))
		{
			if (IsPressed)
			{
				IsPressed = false;
				SkiaVisualStateManager.GoToState(this, "Normal");
				Invalidate();
				this.Released?.Invoke(this, EventArgs.Empty);
				this.Clicked?.Invoke(this, EventArgs.Empty);
				Command?.Execute(CommandParameter);
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
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Expected O, but got Unknown
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Expected O, but got Unknown
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Expected O, but got Unknown
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		SKRect padding = Padding;
		float num;
		if (!float.IsNaN(((SKRect)(ref padding)).Left))
		{
			padding = Padding;
			num = ((SKRect)(ref padding)).Left;
		}
		else
		{
			num = 16f;
		}
		float num2 = num;
		padding = Padding;
		float num3;
		if (!float.IsNaN(((SKRect)(ref padding)).Right))
		{
			padding = Padding;
			num3 = ((SKRect)(ref padding)).Right;
		}
		else
		{
			num3 = 16f;
		}
		float num4 = num3;
		padding = Padding;
		float num5;
		if (!float.IsNaN(((SKRect)(ref padding)).Top))
		{
			padding = Padding;
			num5 = ((SKRect)(ref padding)).Top;
		}
		else
		{
			num5 = 8f;
		}
		float num6 = num5;
		padding = Padding;
		float num7;
		if (!float.IsNaN(((SKRect)(ref padding)).Bottom))
		{
			padding = Padding;
			num7 = ((SKRect)(ref padding)).Bottom;
		}
		else
		{
			num7 = 8f;
		}
		float num8 = num7;
		float num9 = ((float.IsNaN(FontSize) || FontSize <= 0f) ? 14f : FontSize);
		if (string.IsNullOrEmpty(Text))
		{
			return new SKSize(num2 + num4 + 40f, num6 + num8 + num9);
		}
		SKFontStyle style = new SKFontStyle((SKFontStyleWeight)(IsBold ? 700 : 400), (SKFontStyleWidth)5, (SKFontStyleSlant)(IsItalic ? 1 : 0));
		SKFont val = new SKFont(SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, style) ?? SKTypeface.Default, num9, 1f, 0f);
		try
		{
			SKPaint val2 = new SKPaint(val);
			try
			{
				SKRect val3 = default(SKRect);
				val2.MeasureText(Text, ref val3);
				float num10 = ((SKRect)(ref val3)).Width + num2 + num4;
				float num11 = ((SKRect)(ref val3)).Height + num6 + num8;
				if (float.IsNaN(num10) || num10 < 0f)
				{
					num10 = 72f;
				}
				if (float.IsNaN(num11) || num11 < 0f)
				{
					num11 = 30f;
				}
				if (base.WidthRequest >= 0.0)
				{
					num10 = (float)base.WidthRequest;
				}
				if (base.HeightRequest >= 0.0)
				{
					num11 = (float)base.HeightRequest;
				}
				return new SKSize(num10, num11);
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
}
