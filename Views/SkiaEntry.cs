using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaEntry : SkiaView
{
	public static readonly BindableProperty TextProperty = BindableProperty.Create("Text", typeof(string), typeof(SkiaEntry), (object)"", (BindingMode)1, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).OnTextPropertyChanged((string)o, (string)n);
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create("Placeholder", typeof(string), typeof(SkiaEntry), (object)"", (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty PlaceholderColorProperty = BindableProperty.Create("PlaceholderColor", typeof(SKColor), typeof(SkiaEntry), (object)new SKColor((byte)158, (byte)158, (byte)158), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TextColorProperty = BindableProperty.Create("TextColor", typeof(SKColor), typeof(SkiaEntry), (object)SKColors.Black, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty EntryBackgroundColorProperty = BindableProperty.Create("EntryBackgroundColor", typeof(SKColor), typeof(SkiaEntry), (object)SKColors.White, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty BorderColorProperty = BindableProperty.Create("BorderColor", typeof(SKColor), typeof(SkiaEntry), (object)new SKColor((byte)189, (byte)189, (byte)189), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FocusedBorderColorProperty = BindableProperty.Create("FocusedBorderColor", typeof(SKColor), typeof(SkiaEntry), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty SelectionColorProperty = BindableProperty.Create("SelectionColor", typeof(SKColor), typeof(SkiaEntry), (object)new SKColor((byte)33, (byte)150, (byte)243, (byte)128), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CursorColorProperty = BindableProperty.Create("CursorColor", typeof(SKColor), typeof(SkiaEntry), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create("FontFamily", typeof(string), typeof(SkiaEntry), (object)"Sans", (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FontSizeProperty = BindableProperty.Create("FontSize", typeof(float), typeof(SkiaEntry), (object)14f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty IsBoldProperty = BindableProperty.Create("IsBold", typeof(bool), typeof(SkiaEntry), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty IsItalicProperty = BindableProperty.Create("IsItalic", typeof(bool), typeof(SkiaEntry), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create("CornerRadius", typeof(float), typeof(SkiaEntry), (object)4f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty BorderWidthProperty = BindableProperty.Create("BorderWidth", typeof(float), typeof(SkiaEntry), (object)1f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty PaddingProperty = BindableProperty.Create("Padding", typeof(SKRect), typeof(SkiaEntry), (object)new SKRect(12f, 8f, 12f, 8f), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty IsPasswordProperty = BindableProperty.Create("IsPassword", typeof(bool), typeof(SkiaEntry), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty PasswordCharProperty = BindableProperty.Create("PasswordChar", typeof(char), typeof(SkiaEntry), (object)'*', (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty MaxLengthProperty = BindableProperty.Create("MaxLength", typeof(int), typeof(SkiaEntry), (object)0, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)null, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty IsReadOnlyProperty = BindableProperty.Create("IsReadOnly", typeof(bool), typeof(SkiaEntry), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty HorizontalTextAlignmentProperty = BindableProperty.Create("HorizontalTextAlignment", typeof(TextAlignment), typeof(SkiaEntry), (object)TextAlignment.Start, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty VerticalTextAlignmentProperty = BindableProperty.Create("VerticalTextAlignment", typeof(TextAlignment), typeof(SkiaEntry), (object)TextAlignment.Center, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ShowClearButtonProperty = BindableProperty.Create("ShowClearButton", typeof(bool), typeof(SkiaEntry), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CharacterSpacingProperty = BindableProperty.Create("CharacterSpacing", typeof(float), typeof(SkiaEntry), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEntry)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private int _cursorPosition;

	private int _selectionStart;

	private int _selectionLength;

	private float _scrollOffset;

	private DateTime _cursorBlinkTime = DateTime.UtcNow;

	private bool _cursorVisible = true;

	private bool _isSelecting;

	private DateTime _lastClickTime = DateTime.MinValue;

	private float _lastClickX;

	private const double DoubleClickThresholdMs = 400.0;

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

	public string Placeholder
	{
		get
		{
			return (string)((BindableObject)this).GetValue(PlaceholderProperty);
		}
		set
		{
			((BindableObject)this).SetValue(PlaceholderProperty, (object)value);
		}
	}

	public SKColor PlaceholderColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(PlaceholderColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(PlaceholderColorProperty, (object)value);
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

	public SKColor EntryBackgroundColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(EntryBackgroundColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(EntryBackgroundColorProperty, (object)value);
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

	public SKColor FocusedBorderColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(FocusedBorderColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(FocusedBorderColorProperty, (object)value);
		}
	}

	public SKColor SelectionColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(SelectionColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(SelectionColorProperty, (object)value);
		}
	}

	public SKColor CursorColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(CursorColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(CursorColorProperty, (object)value);
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

	public bool IsPassword
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(IsPasswordProperty);
		}
		set
		{
			((BindableObject)this).SetValue(IsPasswordProperty, (object)value);
		}
	}

	public char PasswordChar
	{
		get
		{
			return (char)((BindableObject)this).GetValue(PasswordCharProperty);
		}
		set
		{
			((BindableObject)this).SetValue(PasswordCharProperty, (object)value);
		}
	}

	public int MaxLength
	{
		get
		{
			return (int)((BindableObject)this).GetValue(MaxLengthProperty);
		}
		set
		{
			((BindableObject)this).SetValue(MaxLengthProperty, (object)value);
		}
	}

	public bool IsReadOnly
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(IsReadOnlyProperty);
		}
		set
		{
			((BindableObject)this).SetValue(IsReadOnlyProperty, (object)value);
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

	public bool ShowClearButton
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(ShowClearButtonProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ShowClearButtonProperty, (object)value);
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

	public int CursorPosition
	{
		get
		{
			return _cursorPosition;
		}
		set
		{
			_cursorPosition = Math.Clamp(value, 0, Text.Length);
			ResetCursorBlink();
			Invalidate();
		}
	}

	public int SelectionLength
	{
		get
		{
			return _selectionLength;
		}
		set
		{
			_selectionLength = value;
			Invalidate();
		}
	}

	public event EventHandler<TextChangedEventArgs>? TextChanged;

	public event EventHandler? Completed;

	public SkiaEntry()
	{
		base.IsFocusable = true;
	}

	private void OnTextPropertyChanged(string oldText, string newText)
	{
		_cursorPosition = Math.Min(_cursorPosition, (newText ?? "").Length);
		_scrollOffset = 0f;
		_selectionLength = 0;
		this.TextChanged?.Invoke(this, new TextChangedEventArgs(oldText, newText ?? ""));
		Invalidate();
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Expected O, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Expected O, but got Unknown
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Expected O, but got Unknown
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Expected O, but got Unknown
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0310: Unknown result type (might be due to invalid IL or missing references)
		//IL_031c: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0381: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f4: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = EntryBackgroundColor,
			IsAntialias = true,
			Style = (SKPaintStyle)0
		};
		try
		{
			SKRoundRect val2 = new SKRoundRect(bounds, CornerRadius);
			canvas.DrawRoundRect(val2, val);
			SKColor color = (base.IsFocused ? FocusedBorderColor : BorderColor);
			float strokeWidth = (base.IsFocused ? (BorderWidth + 1f) : BorderWidth);
			SKPaint val3 = new SKPaint
			{
				Color = color,
				IsAntialias = true,
				Style = (SKPaintStyle)1,
				StrokeWidth = strokeWidth
			};
			try
			{
				canvas.DrawRoundRect(val2, val3);
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
				SKRect val4 = new SKRect(num, num2, num3, bottom - ((SKRect)(ref padding)).Bottom);
				float num4 = 20f;
				float num5 = 8f;
				if (ShowClearButton && !string.IsNullOrEmpty(Text) && base.IsFocused)
				{
					((SKRect)(ref val4)).Right = ((SKRect)(ref val4)).Right - (num4 + num5);
				}
				canvas.Save();
				canvas.ClipRect(val4, (SKClipOperation)1, false);
				SKFontStyle fontStyle = GetFontStyle();
				SKFont val5 = new SKFont(SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, fontStyle) ?? SKTypeface.Default, FontSize, 1f, 0f);
				try
				{
					SKPaint val6 = new SKPaint(val5)
					{
						IsAntialias = true
					};
					try
					{
						string displayText = GetDisplayText();
						if (!string.IsNullOrEmpty(displayText))
						{
							val6.Color = TextColor;
							string text = displayText.Substring(0, Math.Min(_cursorPosition, displayText.Length));
							float num6 = val6.MeasureText(text);
							if (num6 - _scrollOffset > ((SKRect)(ref val4)).Width - 10f)
							{
								_scrollOffset = num6 - ((SKRect)(ref val4)).Width + 10f;
							}
							else if (num6 - _scrollOffset < 0f)
							{
								_scrollOffset = num6;
							}
							if (base.IsFocused && _selectionLength != 0)
							{
								DrawSelection(canvas, val6, displayText, val4);
							}
							SKRect val7 = default(SKRect);
							val6.MeasureText(displayText, ref val7);
							float num7 = ((SKRect)(ref val4)).Left - _scrollOffset;
							canvas.DrawText(displayText, num7, VerticalTextAlignment switch
							{
								TextAlignment.Start => ((SKRect)(ref val4)).Top - ((SKRect)(ref val7)).Top, 
								TextAlignment.End => ((SKRect)(ref val4)).Bottom - ((SKRect)(ref val7)).Bottom, 
								_ => ((SKRect)(ref val4)).MidY - ((SKRect)(ref val7)).MidY, 
							}, val6);
							if (base.IsFocused && !IsReadOnly && _cursorVisible)
							{
								DrawCursor(canvas, val6, displayText, val4);
							}
						}
						else if (!string.IsNullOrEmpty(Placeholder))
						{
							val6.Color = PlaceholderColor;
							SKRect val8 = default(SKRect);
							val6.MeasureText(Placeholder, ref val8);
							float left2 = ((SKRect)(ref val4)).Left;
							float num8 = ((SKRect)(ref val4)).MidY - ((SKRect)(ref val8)).MidY;
							canvas.DrawText(Placeholder, left2, num8, val6);
						}
						else if (base.IsFocused && !IsReadOnly && _cursorVisible)
						{
							DrawCursor(canvas, val6, "", val4);
						}
						canvas.Restore();
						if (ShowClearButton && !string.IsNullOrEmpty(Text) && base.IsFocused)
						{
							DrawClearButton(canvas, bounds, num4, num5);
						}
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
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private SKFontStyle GetFontStyle()
	{
		if (IsBold && IsItalic)
		{
			return SKFontStyle.BoldItalic;
		}
		if (IsBold)
		{
			return SKFontStyle.Bold;
		}
		if (IsItalic)
		{
			return SKFontStyle.Italic;
		}
		return SKFontStyle.Normal;
	}

	private void DrawClearButton(SKCanvas canvas, SKRect bounds, float size, float margin)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Expected O, but got Unknown
		float num = ((SKRect)(ref bounds)).Right - margin - size / 2f;
		float midY = ((SKRect)(ref bounds)).MidY;
		SKPaint val = new SKPaint
		{
			Color = new SKColor((byte)189, (byte)189, (byte)189),
			IsAntialias = true,
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawCircle(num, midY, size / 2f - 2f, val);
			SKPaint val2 = new SKPaint
			{
				Color = SKColors.White,
				IsAntialias = true,
				Style = (SKPaintStyle)1,
				StrokeWidth = 2f,
				StrokeCap = (SKStrokeCap)1
			};
			try
			{
				float num2 = size / 4f - 1f;
				canvas.DrawLine(num - num2, midY - num2, num + num2, midY + num2, val2);
				canvas.DrawLine(num - num2, midY + num2, num + num2, midY - num2, val2);
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

	private string GetDisplayText()
	{
		if (IsPassword && !string.IsNullOrEmpty(Text))
		{
			return new string(PasswordChar, Text.Length);
		}
		return Text;
	}

	private void DrawSelection(SKCanvas canvas, SKPaint paint, string displayText, SKRect bounds)
	{
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Expected O, but got Unknown
		int length = Math.Min(_selectionStart, _selectionStart + _selectionLength);
		int length2 = Math.Max(_selectionStart, _selectionStart + _selectionLength);
		string text = displayText.Substring(0, length);
		string text2 = displayText.Substring(0, length2);
		float num = ((SKRect)(ref bounds)).Left - _scrollOffset + paint.MeasureText(text);
		float num2 = ((SKRect)(ref bounds)).Left - _scrollOffset + paint.MeasureText(text2);
		SKPaint val = new SKPaint
		{
			Color = SelectionColor,
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRect(num, ((SKRect)(ref bounds)).Top, num2 - num, ((SKRect)(ref bounds)).Height, val);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void DrawCursor(SKCanvas canvas, SKPaint paint, string displayText, SKRect bounds)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Expected O, but got Unknown
		string text = displayText.Substring(0, Math.Min(_cursorPosition, displayText.Length));
		float num = ((SKRect)(ref bounds)).Left - _scrollOffset + paint.MeasureText(text);
		SKPaint val = new SKPaint
		{
			Color = CursorColor,
			StrokeWidth = 2f,
			IsAntialias = true
		};
		try
		{
			canvas.DrawLine(num, ((SKRect)(ref bounds)).Top + 2f, num, ((SKRect)(ref bounds)).Bottom - 2f, val);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void ResetCursorBlink()
	{
		_cursorBlinkTime = DateTime.UtcNow;
		_cursorVisible = true;
	}

	public void UpdateCursorBlink()
	{
		if (base.IsFocused)
		{
			bool flag = (int)((DateTime.UtcNow - _cursorBlinkTime).TotalMilliseconds / 500.0) % 2 == 0;
			if (flag != _cursorVisible)
			{
				_cursorVisible = flag;
				Invalidate();
			}
		}
	}

	public override void OnTextInput(TextInputEventArgs e)
	{
		if (!base.IsEnabled || IsReadOnly || (!string.IsNullOrEmpty(e.Text) && e.Text.Length == 1 && e.Text[0] < ' '))
		{
			return;
		}
		if (_selectionLength != 0)
		{
			DeleteSelection();
		}
		if (MaxLength <= 0 || Text.Length < MaxLength)
		{
			string text = e.Text;
			if (MaxLength > 0)
			{
				int val = MaxLength - Text.Length;
				text = text.Substring(0, Math.Min(text.Length, val));
			}
			string text2 = Text.Insert(_cursorPosition, text);
			int cursorPosition = _cursorPosition;
			Text = text2;
			_cursorPosition = cursorPosition + text.Length;
			ResetCursorBlink();
			Invalidate();
		}
	}

	public override void OnKeyDown(KeyEventArgs e)
	{
		if (!base.IsEnabled)
		{
			return;
		}
		switch (e.Key)
		{
		case Key.Backspace:
			if (!IsReadOnly)
			{
				if (_selectionLength > 0)
				{
					DeleteSelection();
				}
				else if (_cursorPosition > 0)
				{
					string text = Text.Remove(_cursorPosition - 1, 1);
					int cursorPosition = _cursorPosition - 1;
					Text = text;
					_cursorPosition = cursorPosition;
				}
				ResetCursorBlink();
				Invalidate();
			}
			e.Handled = true;
			break;
		case Key.Delete:
			if (!IsReadOnly)
			{
				if (_selectionLength > 0)
				{
					DeleteSelection();
				}
				else if (_cursorPosition < Text.Length)
				{
					Text = Text.Remove(_cursorPosition, 1);
				}
				ResetCursorBlink();
				Invalidate();
			}
			e.Handled = true;
			break;
		case Key.Left:
			if (_cursorPosition > 0)
			{
				if (e.Modifiers.HasFlag(KeyModifiers.Shift))
				{
					ExtendSelection(-1);
				}
				else
				{
					ClearSelection();
					_cursorPosition--;
				}
				ResetCursorBlink();
				Invalidate();
			}
			e.Handled = true;
			break;
		case Key.Right:
			if (_cursorPosition < Text.Length)
			{
				if (e.Modifiers.HasFlag(KeyModifiers.Shift))
				{
					ExtendSelection(1);
				}
				else
				{
					ClearSelection();
					_cursorPosition++;
				}
				ResetCursorBlink();
				Invalidate();
			}
			e.Handled = true;
			break;
		case Key.Home:
			if (e.Modifiers.HasFlag(KeyModifiers.Shift))
			{
				ExtendSelectionTo(0);
			}
			else
			{
				ClearSelection();
				_cursorPosition = 0;
			}
			ResetCursorBlink();
			Invalidate();
			e.Handled = true;
			break;
		case Key.End:
			if (e.Modifiers.HasFlag(KeyModifiers.Shift))
			{
				ExtendSelectionTo(Text.Length);
			}
			else
			{
				ClearSelection();
				_cursorPosition = Text.Length;
			}
			ResetCursorBlink();
			Invalidate();
			e.Handled = true;
			break;
		case Key.A:
			if (e.Modifiers.HasFlag(KeyModifiers.Control))
			{
				SelectAll();
				e.Handled = true;
			}
			break;
		case Key.C:
			if (e.Modifiers.HasFlag(KeyModifiers.Control))
			{
				CopyToClipboard();
				e.Handled = true;
			}
			break;
		case Key.V:
			if (e.Modifiers.HasFlag(KeyModifiers.Control) && !IsReadOnly)
			{
				PasteFromClipboard();
				e.Handled = true;
			}
			break;
		case Key.X:
			if (e.Modifiers.HasFlag(KeyModifiers.Control) && !IsReadOnly)
			{
				CutToClipboard();
				e.Handled = true;
			}
			break;
		case Key.Enter:
			this.Completed?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
			break;
		}
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		Console.WriteLine($"[SkiaEntry] OnPointerPressed Button={e.Button} at ({e.X}, {e.Y})");
		if (!base.IsEnabled)
		{
			return;
		}
		if (e.Button == PointerButton.Right)
		{
			Console.WriteLine("[SkiaEntry] Right-click detected, showing context menu");
			ShowContextMenu(e.X, e.Y);
			return;
		}
		SKRect val;
		if (ShowClearButton && !string.IsNullOrEmpty(Text) && base.IsFocused)
		{
			float num = 20f;
			float num2 = 8f;
			val = base.Bounds;
			float num3 = ((SKRect)(ref val)).Right - num2 - num / 2f;
			val = base.Bounds;
			float midY = ((SKRect)(ref val)).MidY;
			float num4 = e.X - num3;
			float num5 = e.Y - midY;
			if (num4 * num4 + num5 * num5 < num / 2f * (num / 2f))
			{
				Text = "";
				_cursorPosition = 0;
				_selectionLength = 0;
				Invalidate();
				return;
			}
		}
		SKRect screenBounds = base.ScreenBounds;
		float num6 = e.X - ((SKRect)(ref screenBounds)).Left;
		val = Padding;
		float x = num6 - ((SKRect)(ref val)).Left + _scrollOffset;
		_cursorPosition = GetCharacterIndexAtX(x);
		DateTime utcNow = DateTime.UtcNow;
		double totalMilliseconds = (utcNow - _lastClickTime).TotalMilliseconds;
		float num7 = Math.Abs(e.X - _lastClickX);
		if (totalMilliseconds < 400.0 && num7 < 10f)
		{
			SelectWordAtCursor();
			_lastClickTime = DateTime.MinValue;
			_isSelecting = false;
		}
		else
		{
			_selectionStart = _cursorPosition;
			_selectionLength = 0;
			_isSelecting = true;
			_lastClickTime = utcNow;
			_lastClickX = e.X;
		}
		ResetCursorBlink();
		Invalidate();
	}

	private void SelectWordAtCursor()
	{
		if (!string.IsNullOrEmpty(Text))
		{
			int num = _cursorPosition;
			int i = _cursorPosition;
			while (num > 0 && IsWordChar(Text[num - 1]))
			{
				num--;
			}
			for (; i < Text.Length && IsWordChar(Text[i]); i++)
			{
			}
			_selectionStart = num;
			_cursorPosition = i;
			_selectionLength = i - num;
		}
	}

	private static bool IsWordChar(char c)
	{
		if (!char.IsLetterOrDigit(c))
		{
			return c == '_';
		}
		return true;
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (base.IsEnabled && _isSelecting)
		{
			SKRect screenBounds = base.ScreenBounds;
			float num = e.X - ((SKRect)(ref screenBounds)).Left;
			SKRect padding = Padding;
			float x = num - ((SKRect)(ref padding)).Left + _scrollOffset;
			int characterIndexAtX = GetCharacterIndexAtX(x);
			if (characterIndexAtX != _cursorPosition)
			{
				_cursorPosition = characterIndexAtX;
				_selectionLength = _cursorPosition - _selectionStart;
				ResetCursorBlink();
				Invalidate();
			}
		}
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		_isSelecting = false;
	}

	private int GetCharacterIndexAtX(float x)
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Expected O, but got Unknown
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected O, but got Unknown
		if (string.IsNullOrEmpty(Text))
		{
			return 0;
		}
		SKFontStyle fontStyle = GetFontStyle();
		SKFont val = new SKFont(SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, fontStyle) ?? SKTypeface.Default, FontSize, 1f, 0f);
		try
		{
			SKPaint val2 = new SKPaint(val);
			try
			{
				string displayText = GetDisplayText();
				for (int i = 0; i <= displayText.Length; i++)
				{
					string text = displayText.Substring(0, i);
					float num = val2.MeasureText(text);
					if (!(num >= x))
					{
						continue;
					}
					if (i > 0)
					{
						float num2 = val2.MeasureText(displayText.Substring(0, i - 1));
						if (x - num2 < num - x)
						{
							return i - 1;
						}
					}
					return i;
				}
				return displayText.Length;
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

	private void DeleteSelection()
	{
		int num = Math.Min(_selectionStart, _selectionStart + _selectionLength);
		int count = Math.Abs(_selectionLength);
		Text = Text.Remove(num, count);
		_cursorPosition = num;
		_selectionLength = 0;
	}

	private void ClearSelection()
	{
		_selectionLength = 0;
	}

	private void ExtendSelection(int delta)
	{
		if (_selectionLength == 0)
		{
			_selectionStart = _cursorPosition;
		}
		_cursorPosition += delta;
		_selectionLength = _cursorPosition - _selectionStart;
	}

	private void ExtendSelectionTo(int position)
	{
		if (_selectionLength == 0)
		{
			_selectionStart = _cursorPosition;
		}
		_cursorPosition = position;
		_selectionLength = _cursorPosition - _selectionStart;
	}

	public void SelectAll()
	{
		_selectionStart = 0;
		_cursorPosition = Text.Length;
		_selectionLength = Text.Length;
		Invalidate();
	}

	private void CopyToClipboard()
	{
		if (!IsPassword && _selectionLength != 0)
		{
			int startIndex = Math.Min(_selectionStart, _selectionStart + _selectionLength);
			int length = Math.Abs(_selectionLength);
			SystemClipboard.SetText(Text.Substring(startIndex, length));
		}
	}

	private void CutToClipboard()
	{
		if (!IsPassword)
		{
			CopyToClipboard();
			DeleteSelection();
			Invalidate();
		}
	}

	private void PasteFromClipboard()
	{
		string text = SystemClipboard.GetText();
		if (!string.IsNullOrEmpty(text))
		{
			if (_selectionLength != 0)
			{
				DeleteSelection();
			}
			if (MaxLength > 0)
			{
				int val = MaxLength - Text.Length;
				text = text.Substring(0, Math.Min(text.Length, val));
			}
			string text2 = Text.Insert(_cursorPosition, text);
			int cursorPosition = _cursorPosition + text.Length;
			Text = text2;
			_cursorPosition = cursorPosition;
			Invalidate();
		}
	}

	private void ShowContextMenu(float x, float y)
	{
		Console.WriteLine($"[SkiaEntry] ShowContextMenu at ({x}, {y})");
		bool isEnabled = _selectionLength != 0;
		bool isEnabled2 = !string.IsNullOrEmpty(Text);
		bool isEnabled3 = !string.IsNullOrEmpty(SystemClipboard.GetText());
		GtkContextMenuService.ShowContextMenu(new List<GtkMenuItem>
		{
			new GtkMenuItem("Cut", delegate
			{
				CutToClipboard();
				Invalidate();
			}, isEnabled),
			new GtkMenuItem("Copy", delegate
			{
				CopyToClipboard();
			}, isEnabled),
			new GtkMenuItem("Paste", delegate
			{
				PasteFromClipboard();
				Invalidate();
			}, isEnabled3),
			GtkMenuItem.Separator,
			new GtkMenuItem("Select All", delegate
			{
				SelectAll();
				Invalidate();
			}, isEnabled2)
		});
	}

	public override void OnFocusGained()
	{
		base.OnFocusGained();
		SkiaVisualStateManager.GoToState(this, "Focused");
	}

	public override void OnFocusLost()
	{
		base.OnFocusLost();
		SkiaVisualStateManager.GoToState(this, "Normal");
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		SKFontStyle fontStyle = GetFontStyle();
		SKFont val = new SKFont(SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, fontStyle) ?? SKTypeface.Default, FontSize, 1f, 0f);
		try
		{
			SKFontMetrics metrics = val.Metrics;
			float num = ((SKFontMetrics)(ref metrics)).Descent - ((SKFontMetrics)(ref metrics)).Ascent + ((SKFontMetrics)(ref metrics)).Leading;
			SKRect padding = Padding;
			float num2 = num + ((SKRect)(ref padding)).Top;
			padding = Padding;
			return new SKSize(200f, num2 + ((SKRect)(ref padding)).Bottom + BorderWidth * 2f);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}
}
