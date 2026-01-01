using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaLabel : SkiaView
{
	public static readonly BindableProperty TextProperty = BindableProperty.Create("Text", typeof(string), typeof(SkiaLabel), (object)"", (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLabel)(object)b).OnTextChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FormattedSpansProperty = BindableProperty.Create("FormattedSpans", typeof(IList<SkiaTextSpan>), typeof(SkiaLabel), (object)null, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLabel)(object)b).OnTextChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TextColorProperty = BindableProperty.Create("TextColor", typeof(SKColor), typeof(SkiaLabel), (object)SKColors.Black, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLabel)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create("FontFamily", typeof(string), typeof(SkiaLabel), (object)"Sans", (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLabel)(object)b).OnFontChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FontSizeProperty = BindableProperty.Create("FontSize", typeof(float), typeof(SkiaLabel), (object)14f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLabel)(object)b).OnFontChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty IsBoldProperty = BindableProperty.Create("IsBold", typeof(bool), typeof(SkiaLabel), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLabel)(object)b).OnFontChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty IsItalicProperty = BindableProperty.Create("IsItalic", typeof(bool), typeof(SkiaLabel), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLabel)(object)b).OnFontChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty IsUnderlineProperty = BindableProperty.Create("IsUnderline", typeof(bool), typeof(SkiaLabel), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLabel)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty IsStrikethroughProperty = BindableProperty.Create("IsStrikethrough", typeof(bool), typeof(SkiaLabel), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLabel)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty HorizontalTextAlignmentProperty = BindableProperty.Create("HorizontalTextAlignment", typeof(TextAlignment), typeof(SkiaLabel), (object)TextAlignment.Start, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLabel)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty VerticalTextAlignmentProperty = BindableProperty.Create("VerticalTextAlignment", typeof(TextAlignment), typeof(SkiaLabel), (object)TextAlignment.Center, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLabel)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty LineBreakModeProperty = BindableProperty.Create("LineBreakMode", typeof(LineBreakMode), typeof(SkiaLabel), (object)LineBreakMode.TailTruncation, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLabel)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty MaxLinesProperty = BindableProperty.Create("MaxLines", typeof(int), typeof(SkiaLabel), (object)0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLabel)(object)b).OnTextChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty LineHeightProperty = BindableProperty.Create("LineHeight", typeof(float), typeof(SkiaLabel), (object)1.2f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLabel)(object)b).OnTextChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CharacterSpacingProperty = BindableProperty.Create("CharacterSpacing", typeof(float), typeof(SkiaLabel), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLabel)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty PaddingProperty = BindableProperty.Create("Padding", typeof(SKRect), typeof(SkiaLabel), (object)SKRect.Empty, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaLabel)(object)b).OnTextChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private static SKTypeface? _cachedTypeface;

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

	public IList<SkiaTextSpan>? FormattedSpans
	{
		get
		{
			return (IList<SkiaTextSpan>)((BindableObject)this).GetValue(FormattedSpansProperty);
		}
		set
		{
			((BindableObject)this).SetValue(FormattedSpansProperty, (object)value);
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

	public bool IsUnderline
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(IsUnderlineProperty);
		}
		set
		{
			((BindableObject)this).SetValue(IsUnderlineProperty, (object)value);
		}
	}

	public bool IsStrikethrough
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(IsStrikethroughProperty);
		}
		set
		{
			((BindableObject)this).SetValue(IsStrikethroughProperty, (object)value);
		}
	}

	public TextAlignment HorizontalTextAlignment
	{
		get
		{
			return (TextAlignment)((BindableObject)this).GetValue(HorizontalTextAlignmentProperty);
		}
		set
		{
			((BindableObject)this).SetValue(HorizontalTextAlignmentProperty, (object)value);
		}
	}

	public TextAlignment VerticalTextAlignment
	{
		get
		{
			return (TextAlignment)((BindableObject)this).GetValue(VerticalTextAlignmentProperty);
		}
		set
		{
			((BindableObject)this).SetValue(VerticalTextAlignmentProperty, (object)value);
		}
	}

	public LineBreakMode LineBreakMode
	{
		get
		{
			return (LineBreakMode)((BindableObject)this).GetValue(LineBreakModeProperty);
		}
		set
		{
			((BindableObject)this).SetValue(LineBreakModeProperty, (object)value);
		}
	}

	public int MaxLines
	{
		get
		{
			return (int)((BindableObject)this).GetValue(MaxLinesProperty);
		}
		set
		{
			((BindableObject)this).SetValue(MaxLinesProperty, (object)value);
		}
	}

	public float LineHeight
	{
		get
		{
			return (float)((BindableObject)this).GetValue(LineHeightProperty);
		}
		set
		{
			((BindableObject)this).SetValue(LineHeightProperty, (object)value);
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

	public SkiaTextAlignment HorizontalAlignment
	{
		get
		{
			return HorizontalTextAlignment switch
			{
				TextAlignment.Start => SkiaTextAlignment.Left, 
				TextAlignment.Center => SkiaTextAlignment.Center, 
				TextAlignment.End => SkiaTextAlignment.Right, 
				_ => SkiaTextAlignment.Left, 
			};
		}
		set
		{
			HorizontalTextAlignment = value switch
			{
				SkiaTextAlignment.Left => TextAlignment.Start, 
				SkiaTextAlignment.Center => TextAlignment.Center, 
				SkiaTextAlignment.Right => TextAlignment.End, 
				_ => TextAlignment.Start, 
			};
		}
	}

	public SkiaVerticalAlignment VerticalAlignment
	{
		get
		{
			return VerticalTextAlignment switch
			{
				TextAlignment.Start => SkiaVerticalAlignment.Top, 
				TextAlignment.Center => SkiaVerticalAlignment.Center, 
				TextAlignment.End => SkiaVerticalAlignment.Bottom, 
				_ => SkiaVerticalAlignment.Top, 
			};
		}
		set
		{
			VerticalTextAlignment = value switch
			{
				SkiaVerticalAlignment.Top => TextAlignment.Start, 
				SkiaVerticalAlignment.Center => TextAlignment.Center, 
				SkiaVerticalAlignment.Bottom => TextAlignment.End, 
				_ => TextAlignment.Start, 
			};
		}
	}

	public event EventHandler? Tapped;

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

	private static SKTypeface GetLinuxTypeface()
	{
		if (_cachedTypeface != null)
		{
			return _cachedTypeface;
		}
		string[] array = new string[4] { "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/ubuntu/Ubuntu-R.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf", "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf" };
		foreach (string text in array)
		{
			if (File.Exists(text))
			{
				_cachedTypeface = SKTypeface.FromFile(text, 0);
				if (_cachedTypeface != null)
				{
					return _cachedTypeface;
				}
			}
		}
		return SKTypeface.Default;
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Expected O, but got Unknown
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Expected O, but got Unknown
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Expected O, but got Unknown
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		float left = ((SKRect)(ref bounds)).Left;
		SKRect padding = Padding;
		float num = left + ((SKRect)(ref padding)).Left;
		float top = ((SKRect)(ref bounds)).Top;
		padding = Padding;
		float num2 = top + ((SKRect)(ref padding)).Top;
		float right = ((SKRect)(ref bounds)).Right;
		padding = Padding;
		float num3 = right - ((SKRect)(ref padding)).Right;
		float bottom = ((SKRect)(ref bounds)).Bottom;
		padding = Padding;
		SKRect bounds2 = default(SKRect);
		((SKRect)(ref bounds2))._002Ector(num, num2, num3, bottom - ((SKRect)(ref padding)).Bottom);
		if (FormattedSpans != null && FormattedSpans.Count > 0)
		{
			DrawFormattedText(canvas, bounds2);
		}
		else
		{
			if (string.IsNullOrEmpty(Text))
			{
				return;
			}
			SKFontStyle style = new SKFontStyle((SKFontStyleWeight)(IsBold ? 700 : 400), (SKFontStyleWidth)5, (SKFontStyleSlant)(IsItalic ? 1 : 0));
			SKTypeface val = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, style);
			if (val == null || val == SKTypeface.Default)
			{
				val = GetLinuxTypeface();
			}
			SKFont val2 = new SKFont(val, FontSize, 1f, 0f);
			try
			{
				SKPaint val3 = new SKPaint(val2);
				SKColor color;
				if (!base.IsEnabled)
				{
					SKColor textColor = TextColor;
					color = ((SKColor)(ref textColor)).WithAlpha((byte)128);
				}
				else
				{
					color = TextColor;
				}
				val3.Color = color;
				val3.IsAntialias = true;
				SKPaint val4 = val3;
				try
				{
					if (MaxLines > 1 || Text.Contains('\n') || LineBreakMode == LineBreakMode.WordWrap || LineBreakMode == LineBreakMode.CharacterWrap)
					{
						DrawMultiLineWithWrapping(canvas, val4, val2, bounds2);
					}
					else
					{
						DrawSingleLine(canvas, val4, val2, bounds2);
					}
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
	}

	private void DrawFormattedText(SKCanvas canvas, SKRect bounds)
	{
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		if (FormattedSpans == null || FormattedSpans.Count == 0)
		{
			return;
		}
		float num = ((SKRect)(ref bounds)).Left;
		float num2 = ((SKRect)(ref bounds)).Top;
		float num3 = 0f;
		float val = 0f;
		List<(SkiaTextSpan, float, float, float, SKPaint)> list = new List<(SkiaTextSpan, float, float, float, SKPaint)>();
		foreach (SkiaTextSpan formattedSpan in FormattedSpans)
		{
			if (!string.IsNullOrEmpty(formattedSpan.Text))
			{
				SKPaint val2 = CreateSpanPaint(formattedSpan);
				SKRect val3 = default(SKRect);
				val2.MeasureText(formattedSpan.Text, ref val3);
				num3 = Math.Max(num3, ((SKRect)(ref val3)).Height);
				if (num + ((SKRect)(ref val3)).Width > ((SKRect)(ref bounds)).Right && num > ((SKRect)(ref bounds)).Left)
				{
					num2 += num3 * LineHeight;
					num = ((SKRect)(ref bounds)).Left;
					val = Math.Max(val, num);
					num3 = ((SKRect)(ref val3)).Height;
				}
				list.Add((formattedSpan, num, ((SKRect)(ref val3)).Width, ((SKRect)(ref val3)).Height, val2));
				num += ((SKRect)(ref val3)).Width;
			}
		}
		float num4 = num2 + num3 - ((SKRect)(ref bounds)).Top;
		float num5 = VerticalTextAlignment switch
		{
			TextAlignment.Start => 0f, 
			TextAlignment.Center => (((SKRect)(ref bounds)).Height - num4) / 2f, 
			TextAlignment.End => ((SKRect)(ref bounds)).Height - num4, 
			_ => 0f, 
		};
		num = ((SKRect)(ref bounds)).Left;
		num2 = ((SKRect)(ref bounds)).Top + num5;
		num3 = 0f;
		float left = ((SKRect)(ref bounds)).Left;
		List<(SkiaTextSpan, float, float, float, SKPaint)> list2 = new List<(SkiaTextSpan, float, float, float, SKPaint)>();
		foreach (SkiaTextSpan formattedSpan2 in FormattedSpans)
		{
			if (!string.IsNullOrEmpty(formattedSpan2.Text))
			{
				SKPaint val4 = CreateSpanPaint(formattedSpan2);
				SKRect val5 = default(SKRect);
				val4.MeasureText(formattedSpan2.Text, ref val5);
				num3 = Math.Max(num3, ((SKRect)(ref val5)).Height);
				if (num + ((SKRect)(ref val5)).Width > ((SKRect)(ref bounds)).Right && num > ((SKRect)(ref bounds)).Left)
				{
					DrawFormattedLine(canvas, bounds, list2, num2 + num3);
					num2 += num3 * LineHeight;
					num = ((SKRect)(ref bounds)).Left;
					num3 = ((SKRect)(ref val5)).Height;
					list2.Clear();
				}
				list2.Add((formattedSpan2, num - left, ((SKRect)(ref val5)).Width, ((SKRect)(ref val5)).Height, val4));
				num += ((SKRect)(ref val5)).Width;
			}
		}
		if (list2.Count > 0)
		{
			DrawFormattedLine(canvas, bounds, list2, num2 + num3);
		}
	}

	private void DrawFormattedLine(SKCanvas canvas, SKRect bounds, List<(SkiaTextSpan span, float x, float width, float height, SKPaint paint)> lineSpans, float y)
	{
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Expected O, but got Unknown
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Expected O, but got Unknown
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Expected O, but got Unknown
		if (lineSpans.Count == 0)
		{
			return;
		}
		float num = 0f;
		foreach (var lineSpan in lineSpans)
		{
			float item = lineSpan.width;
			num += item;
		}
		float num2 = HorizontalTextAlignment switch
		{
			TextAlignment.Start => ((SKRect)(ref bounds)).Left, 
			TextAlignment.Center => ((SKRect)(ref bounds)).Left + (((SKRect)(ref bounds)).Width - num) / 2f, 
			TextAlignment.End => ((SKRect)(ref bounds)).Right - num, 
			_ => ((SKRect)(ref bounds)).Left, 
		};
		foreach (var (skiaTextSpan, _, num3, num4, val) in lineSpans)
		{
			if (skiaTextSpan.BackgroundColor.HasValue && skiaTextSpan.BackgroundColor.Value != SKColors.Transparent)
			{
				SKPaint val2 = new SKPaint
				{
					Color = skiaTextSpan.BackgroundColor.Value,
					Style = (SKPaintStyle)0
				};
				try
				{
					canvas.DrawRect(num2, y - num4, num3, num4 + 4f, val2);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			canvas.DrawText(skiaTextSpan.Text, num2, y, val);
			if (skiaTextSpan.IsUnderline)
			{
				SKPaint val3 = new SKPaint
				{
					Color = val.Color,
					StrokeWidth = 1f,
					IsAntialias = true
				};
				try
				{
					canvas.DrawLine(num2, y + 2f, num2 + num3, y + 2f, val3);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
			if (skiaTextSpan.IsStrikethrough)
			{
				SKPaint val4 = new SKPaint
				{
					Color = val.Color,
					StrokeWidth = 1f,
					IsAntialias = true
				};
				try
				{
					canvas.DrawLine(num2, y - num4 / 3f, num2 + num3, y - num4 / 3f, val4);
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			num2 += num3;
			((SKNativeObject)val).Dispose();
		}
	}

	private SKPaint CreateSpanPaint(SkiaTextSpan span)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Expected O, but got Unknown
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Expected O, but got Unknown
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		SKFontStyle style = new SKFontStyle((SKFontStyleWeight)(span.IsBold ? 700 : 400), (SKFontStyleWidth)5, (SKFontStyleSlant)(span.IsItalic ? 1 : 0));
		SKTypeface val = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(span.FontFamily ?? FontFamily, style);
		if (val == null || val == SKTypeface.Default)
		{
			val = GetLinuxTypeface();
		}
		float num = ((span.FontSize > 0f) ? span.FontSize : FontSize);
		SKFont val2 = new SKFont(val, num, 1f, 0f);
		try
		{
			SKColor color = (SKColor)(((_003F?)span.TextColor) ?? TextColor);
			if (!base.IsEnabled)
			{
				color = ((SKColor)(ref color)).WithAlpha((byte)128);
			}
			return new SKPaint(val2)
			{
				Color = color,
				IsAntialias = true
			};
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private void DrawSingleLine(SKCanvas canvas, SKPaint paint, SKFont font, SKRect bounds)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Expected O, but got Unknown
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Expected O, but got Unknown
		string text = Text;
		SKRect val = default(SKRect);
		paint.MeasureText(text, ref val);
		if (((SKRect)(ref val)).Width > ((SKRect)(ref bounds)).Width && LineBreakMode == LineBreakMode.TailTruncation)
		{
			text = TruncateText(paint, text, ((SKRect)(ref bounds)).Width);
			paint.MeasureText(text, ref val);
		}
		float num = HorizontalTextAlignment switch
		{
			TextAlignment.Start => ((SKRect)(ref bounds)).Left, 
			TextAlignment.Center => ((SKRect)(ref bounds)).MidX - ((SKRect)(ref val)).Width / 2f, 
			TextAlignment.End => ((SKRect)(ref bounds)).Right - ((SKRect)(ref val)).Width, 
			_ => ((SKRect)(ref bounds)).Left, 
		};
		float num2 = VerticalTextAlignment switch
		{
			TextAlignment.Start => ((SKRect)(ref bounds)).Top - ((SKRect)(ref val)).Top, 
			TextAlignment.Center => ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val)).MidY, 
			TextAlignment.End => ((SKRect)(ref bounds)).Bottom - ((SKRect)(ref val)).Bottom, 
			_ => ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val)).MidY, 
		};
		canvas.DrawText(text, num, num2, paint);
		if (IsUnderline)
		{
			SKPaint val2 = new SKPaint
			{
				Color = paint.Color,
				StrokeWidth = 1f,
				IsAntialias = true
			};
			try
			{
				float num3 = num2 + 2f;
				canvas.DrawLine(num, num3, num + ((SKRect)(ref val)).Width, num3, val2);
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		if (IsStrikethrough)
		{
			SKPaint val3 = new SKPaint
			{
				Color = paint.Color,
				StrokeWidth = 1f,
				IsAntialias = true
			};
			try
			{
				float num4 = num2 - ((SKRect)(ref val)).Height / 3f;
				canvas.DrawLine(num, num4, num + ((SKRect)(ref val)).Width, num4, val3);
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
	}

	private void DrawMultiLineWithWrapping(SKCanvas canvas, SKPaint paint, SKFont font, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		SKRect val = bounds;
		if (((SKRect)(ref bounds)).Height <= 0f)
		{
			float num = ((LineHeight <= 0f) ? 1.2f : LineHeight);
			float num2 = ((MaxLines > 0) ? ((float)MaxLines * FontSize * num) : (FontSize * num * 10f));
			((SKRect)(ref val))._002Ector(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Top + num2);
		}
		float num3 = ((SKRect)(ref val)).Width;
		if (num3 <= 0f)
		{
			num3 = 400f;
		}
		List<string> list = WrapText(paint, Text, num3);
		float num4 = ((LineHeight <= 0f) ? 1.2f : LineHeight);
		float num5 = FontSize * num4;
		int num6 = ((MaxLines > 0) ? Math.Min(MaxLines, list.Count) : list.Count);
		float num7 = (float)num6 * num5;
		float num8 = VerticalTextAlignment switch
		{
			TextAlignment.Start => ((SKRect)(ref val)).Top + FontSize, 
			TextAlignment.Center => ((SKRect)(ref val)).MidY - num7 / 2f + FontSize, 
			TextAlignment.End => ((SKRect)(ref val)).Bottom - num7 + FontSize, 
			_ => ((SKRect)(ref val)).Top + FontSize, 
		};
		for (int i = 0; i < num6; i++)
		{
			string text = list[i];
			bool num9 = i == num6 - 1;
			bool flag = num6 < list.Count;
			if (num9 && flag && LineBreakMode == LineBreakMode.TailTruncation)
			{
				text = TruncateTextWithEllipsis(paint, text, num3);
			}
			SKRect val2 = default(SKRect);
			paint.MeasureText(text, ref val2);
			float num10 = HorizontalTextAlignment switch
			{
				TextAlignment.Start => ((SKRect)(ref val)).Left, 
				TextAlignment.Center => ((SKRect)(ref val)).MidX - ((SKRect)(ref val2)).Width / 2f, 
				TextAlignment.End => ((SKRect)(ref val)).Right - ((SKRect)(ref val2)).Width, 
				_ => ((SKRect)(ref val)).Left, 
			};
			float num11 = num8 + (float)i * num5;
			if (!(((SKRect)(ref val)).Height > 0f) || !(num11 > ((SKRect)(ref val)).Bottom))
			{
				canvas.DrawText(text, num10, num11, paint);
				continue;
			}
			break;
		}
	}

	private List<string> WrapText(SKPaint paint, string text, float maxWidth)
	{
		List<string> list = new List<string>();
		string[] array = text.Split('\n');
		foreach (string text2 in array)
		{
			if (string.IsNullOrEmpty(text2))
			{
				list.Add("");
				continue;
			}
			if (paint.MeasureText(text2) <= maxWidth)
			{
				list.Add(text2);
				continue;
			}
			string[] array2 = text2.Split(' ');
			string text3 = "";
			string[] array3 = array2;
			foreach (string text4 in array3)
			{
				string text5 = (string.IsNullOrEmpty(text3) ? text4 : (text3 + " " + text4));
				if (paint.MeasureText(text5) > maxWidth && !string.IsNullOrEmpty(text3))
				{
					list.Add(text3);
					text3 = text4;
				}
				else
				{
					text3 = text5;
				}
			}
			if (!string.IsNullOrEmpty(text3))
			{
				list.Add(text3);
			}
		}
		return list;
	}

	private void DrawMultiLine(SKCanvas canvas, SKPaint paint, SKFont font, SKRect bounds)
	{
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		string[] array = Text.Split('\n');
		float num = ((LineHeight <= 0f) ? 1.2f : LineHeight);
		float num2 = FontSize * num;
		int num3 = ((MaxLines > 0) ? Math.Min(MaxLines, array.Length) : array.Length);
		float num4 = (float)num3 * num2;
		float num5 = VerticalTextAlignment switch
		{
			TextAlignment.Start => ((SKRect)(ref bounds)).Top + FontSize, 
			TextAlignment.Center => ((SKRect)(ref bounds)).MidY - num4 / 2f + FontSize, 
			TextAlignment.End => ((SKRect)(ref bounds)).Bottom - num4 + FontSize, 
			_ => ((SKRect)(ref bounds)).Top + FontSize, 
		};
		for (int i = 0; i < num3; i++)
		{
			string text = array[i];
			if (i == num3 - 1 && i < array.Length - 1 && LineBreakMode == LineBreakMode.TailTruncation)
			{
				text = TruncateText(paint, text, ((SKRect)(ref bounds)).Width);
			}
			SKRect val = default(SKRect);
			paint.MeasureText(text, ref val);
			float num6 = HorizontalTextAlignment switch
			{
				TextAlignment.Start => ((SKRect)(ref bounds)).Left, 
				TextAlignment.Center => ((SKRect)(ref bounds)).MidX - ((SKRect)(ref val)).Width / 2f, 
				TextAlignment.End => ((SKRect)(ref bounds)).Right - ((SKRect)(ref val)).Width, 
				_ => ((SKRect)(ref bounds)).Left, 
			};
			float num7 = num5 + (float)i * num2;
			if (!(num7 > ((SKRect)(ref bounds)).Bottom))
			{
				canvas.DrawText(text, num6, num7, paint);
				continue;
			}
			break;
		}
	}

	private string TruncateTextWithEllipsis(SKPaint paint, string text, float maxWidth)
	{
		float num = paint.MeasureText("...");
		if (paint.MeasureText(text) + num <= maxWidth)
		{
			return text + "...";
		}
		float num2 = maxWidth - num;
		if (num2 <= 0f)
		{
			return "...";
		}
		int num3 = 0;
		int num4 = text.Length;
		while (num3 < num4)
		{
			int num5 = (num3 + num4 + 1) / 2;
			string text2 = text.Substring(0, num5);
			if (paint.MeasureText(text2) <= num2)
			{
				num3 = num5;
			}
			else
			{
				num4 = num5 - 1;
			}
		}
		return text.Substring(0, num3) + "...";
	}

	private string TruncateText(SKPaint paint, string text, float maxWidth)
	{
		float num = paint.MeasureText("...");
		if (paint.MeasureText(text) <= maxWidth)
		{
			return text;
		}
		float num2 = maxWidth - num;
		if (num2 <= 0f)
		{
			return "...";
		}
		int num3 = 0;
		int num4 = text.Length;
		while (num3 < num4)
		{
			int num5 = (num3 + num4 + 1) / 2;
			string text2 = text.Substring(0, num5);
			if (paint.MeasureText(text2) <= num2)
			{
				num3 = num5;
			}
			else
			{
				num4 = num5 - 1;
			}
		}
		return text.Substring(0, num3) + "...";
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Expected O, but got Unknown
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Expected O, but got Unknown
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Expected O, but got Unknown
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_025a: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_026a: Unknown result type (might be due to invalid IL or missing references)
		//IL_026f: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0281: Unknown result type (might be due to invalid IL or missing references)
		//IL_028c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0291: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
		SKRect padding;
		if (string.IsNullOrEmpty(Text))
		{
			padding = Padding;
			float left = ((SKRect)(ref padding)).Left;
			padding = Padding;
			float num = left + ((SKRect)(ref padding)).Right;
			float fontSize = FontSize;
			padding = Padding;
			float num2 = fontSize + ((SKRect)(ref padding)).Top;
			padding = Padding;
			return new SKSize(num, num2 + ((SKRect)(ref padding)).Bottom);
		}
		SKFontStyle style = new SKFontStyle((SKFontStyleWeight)(IsBold ? 700 : 400), (SKFontStyleWidth)5, (SKFontStyleSlant)(IsItalic ? 1 : 0));
		SKTypeface val = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, style);
		if (val == null || val == SKTypeface.Default)
		{
			val = GetLinuxTypeface();
		}
		SKFont val2 = new SKFont(val, FontSize, 1f, 0f);
		try
		{
			SKPaint val3 = new SKPaint(val2);
			try
			{
				if (MaxLines <= 1 && !Text.Contains('\n') && LineBreakMode != LineBreakMode.WordWrap && LineBreakMode != LineBreakMode.CharacterWrap)
				{
					SKRect val4 = default(SKRect);
					val3.MeasureText(Text, ref val4);
					float width = ((SKRect)(ref val4)).Width;
					padding = Padding;
					float num3 = width + ((SKRect)(ref padding)).Left;
					padding = Padding;
					float num4 = num3 + ((SKRect)(ref padding)).Right + 4f;
					float height = ((SKRect)(ref val4)).Height;
					padding = Padding;
					float num5 = height + ((SKRect)(ref padding)).Top;
					padding = Padding;
					return new SKSize(num4, num5 + ((SKRect)(ref padding)).Bottom);
				}
				float width2 = ((SKSize)(ref availableSize)).Width;
				padding = Padding;
				float num6 = width2 - ((SKRect)(ref padding)).Left;
				padding = Padding;
				float num7 = num6 - ((SKRect)(ref padding)).Right;
				if (num7 <= 0f)
				{
					num7 = float.MaxValue;
				}
				List<string> list = WrapText(val3, Text, num7);
				int num8 = ((MaxLines > 0) ? Math.Min(MaxLines, list.Count) : list.Count);
				float num9 = 0f;
				foreach (string item in list.Take(num8))
				{
					num9 = Math.Max(num9, val3.MeasureText(item));
				}
				float num10 = ((LineHeight <= 0f) ? 1.2f : LineHeight);
				float num11 = (float)num8 * FontSize * num10;
				float num12 = num9;
				padding = Padding;
				float num13 = num12 + ((SKRect)(ref padding)).Left;
				padding = Padding;
				float num14 = num13 + ((SKRect)(ref padding)).Right;
				padding = Padding;
				float num15 = num11 + ((SKRect)(ref padding)).Top;
				padding = Padding;
				return new SKSize(num14, num15 + ((SKRect)(ref padding)).Bottom);
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

	public override void OnPointerPressed(PointerEventArgs e)
	{
		base.OnPointerPressed(e);
		this.Tapped?.Invoke(this, EventArgs.Empty);
	}
}
