using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaEditor : SkiaView
{
	public static readonly BindableProperty TextProperty = BindableProperty.Create("Text", typeof(string), typeof(SkiaEditor), (object)"", (BindingMode)1, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEditor)(object)b).OnTextPropertyChanged((string)o, (string)n);
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create("Placeholder", typeof(string), typeof(SkiaEditor), (object)"", (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEditor)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty TextColorProperty = BindableProperty.Create("TextColor", typeof(SKColor), typeof(SkiaEditor), (object)SKColors.Black, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEditor)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty PlaceholderColorProperty = BindableProperty.Create("PlaceholderColor", typeof(SKColor), typeof(SkiaEditor), (object)new SKColor((byte)128, (byte)128, (byte)128), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEditor)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty BorderColorProperty = BindableProperty.Create("BorderColor", typeof(SKColor), typeof(SkiaEditor), (object)new SKColor((byte)189, (byte)189, (byte)189), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEditor)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty SelectionColorProperty = BindableProperty.Create("SelectionColor", typeof(SKColor), typeof(SkiaEditor), (object)new SKColor((byte)33, (byte)150, (byte)243, (byte)96), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEditor)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CursorColorProperty = BindableProperty.Create("CursorColor", typeof(SKColor), typeof(SkiaEditor), (object)new SKColor((byte)33, (byte)150, (byte)243), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEditor)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create("FontFamily", typeof(string), typeof(SkiaEditor), (object)"Sans", (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEditor)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FontSizeProperty = BindableProperty.Create("FontSize", typeof(float), typeof(SkiaEditor), (object)14f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEditor)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty LineHeightProperty = BindableProperty.Create("LineHeight", typeof(float), typeof(SkiaEditor), (object)1.4f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEditor)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create("CornerRadius", typeof(float), typeof(SkiaEditor), (object)4f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEditor)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty PaddingProperty = BindableProperty.Create("Padding", typeof(float), typeof(SkiaEditor), (object)12f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEditor)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty IsReadOnlyProperty = BindableProperty.Create("IsReadOnly", typeof(bool), typeof(SkiaEditor), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEditor)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty MaxLengthProperty = BindableProperty.Create("MaxLength", typeof(int), typeof(SkiaEditor), (object)(-1), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)null, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty AutoSizeProperty = BindableProperty.Create("AutoSize", typeof(bool), typeof(SkiaEditor), (object)false, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaEditor)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private int _cursorPosition;

	private int _selectionStart = -1;

	private int _selectionLength;

	private float _scrollOffsetY;

	private bool _cursorVisible = true;

	private DateTime _lastCursorBlink = DateTime.Now;

	private List<string> _lines = new List<string> { "" };

	private float _wrapWidth;

	private bool _isSelecting;

	private DateTime _lastClickTime = DateTime.MinValue;

	private float _lastClickX;

	private float _lastClickY;

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

	public float Padding
	{
		get
		{
			return (float)((BindableObject)this).GetValue(PaddingProperty);
		}
		set
		{
			((BindableObject)this).SetValue(PaddingProperty, (object)value);
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

	public bool AutoSize
	{
		get
		{
			return (bool)((BindableObject)this).GetValue(AutoSizeProperty);
		}
		set
		{
			((BindableObject)this).SetValue(AutoSizeProperty, (object)value);
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
			EnsureCursorVisible();
			Invalidate();
		}
	}

	public event EventHandler? TextChanged;

	public event EventHandler? Completed;

	public SkiaEditor()
	{
		base.IsFocusable = true;
	}

	private void OnTextPropertyChanged(string oldText, string newText)
	{
		string text = newText ?? "";
		if (MaxLength > 0 && text.Length > MaxLength)
		{
			text = text.Substring(0, MaxLength);
			((BindableObject)this).SetValue(TextProperty, (object)text);
			return;
		}
		UpdateLines();
		_cursorPosition = Math.Min(_cursorPosition, text.Length);
		_scrollOffsetY = 0f;
		_selectionLength = 0;
		this.TextChanged?.Invoke(this, EventArgs.Empty);
		Invalidate();
	}

	private void UpdateLines()
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected O, but got Unknown
		_lines.Clear();
		string text = Text ?? "";
		if (string.IsNullOrEmpty(text))
		{
			_lines.Add("");
			return;
		}
		SKFont val = new SKFont(SKTypeface.Default, FontSize, 1f, 0f);
		try
		{
			string[] array = text.Split('\n');
			foreach (string text2 in array)
			{
				if (string.IsNullOrEmpty(text2))
				{
					_lines.Add("");
				}
				else if (_wrapWidth > 0f)
				{
					WrapParagraph(text2, val, _wrapWidth);
				}
				else
				{
					_lines.Add(text2);
				}
			}
			if (_lines.Count == 0)
			{
				_lines.Add("");
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void WrapParagraph(string paragraph, SKFont font, float maxWidth)
	{
		string[] array = paragraph.Split(' ');
		string text = "";
		string[] array2 = array;
		foreach (string text2 in array2)
		{
			string text3 = (string.IsNullOrEmpty(text) ? text2 : (text + " " + text2));
			if (MeasureText(text3, font) > maxWidth && !string.IsNullOrEmpty(text))
			{
				_lines.Add(text);
				text = text2;
			}
			else
			{
				text = text3;
			}
		}
		if (!string.IsNullOrEmpty(text))
		{
			_lines.Add(text);
		}
	}

	private (int line, int column) GetLineColumn(int position)
	{
		int num = 0;
		for (int i = 0; i < _lines.Count; i++)
		{
			int length = _lines[i].Length;
			if (num + length >= position || i == _lines.Count - 1)
			{
				return (line: i, column: position - num);
			}
			num += length + 1;
		}
		int item = _lines.Count - 1;
		List<string> lines = _lines;
		return (line: item, column: lines[lines.Count - 1].Length);
	}

	private int GetPosition(int line, int column)
	{
		int num = 0;
		for (int i = 0; i < line && i < _lines.Count; i++)
		{
			num += _lines[i].Length + 1;
		}
		if (line < _lines.Count)
		{
			num += Math.Min(column, _lines[line].Length);
		}
		return Math.Min(num, Text.Length);
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Expected O, but got Unknown
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Expected O, but got Unknown
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Expected O, but got Unknown
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Expected O, but got Unknown
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Expected O, but got Unknown
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_020c: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Expected O, but got Unknown
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Expected O, but got Unknown
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Expected O, but got Unknown
		//IL_04ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03df: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0404: Expected O, but got Unknown
		//IL_0365: Unknown result type (might be due to invalid IL or missing references)
		float num = ((SKRect)(ref bounds)).Width - Padding * 2f;
		if (Math.Abs(num - _wrapWidth) > 1f)
		{
			_wrapWidth = num;
			UpdateLines();
		}
		if (base.IsFocused && (DateTime.Now - _lastCursorBlink).TotalMilliseconds > 500.0)
		{
			_cursorVisible = !_cursorVisible;
			_lastCursorBlink = DateTime.Now;
		}
		SKPaint val = new SKPaint
		{
			Color = (SKColor)(base.IsEnabled ? base.BackgroundColor : new SKColor((byte)245, (byte)245, (byte)245)),
			Style = (SKPaintStyle)0,
			IsAntialias = true
		};
		try
		{
			canvas.DrawRoundRect(new SKRoundRect(bounds, CornerRadius), val);
			SKPaint val2 = new SKPaint
			{
				Color = (base.IsFocused ? CursorColor : BorderColor),
				Style = (SKPaintStyle)1,
				StrokeWidth = ((!base.IsFocused) ? 1 : 2),
				IsAntialias = true
			};
			try
			{
				canvas.DrawRoundRect(new SKRoundRect(bounds, CornerRadius), val2);
				SKFont val3 = new SKFont(SKTypeface.Default, FontSize, 1f, 0f);
				try
				{
					float num2 = FontSize * LineHeight;
					SKRect val4 = new SKRect(((SKRect)(ref bounds)).Left + Padding, ((SKRect)(ref bounds)).Top + Padding, ((SKRect)(ref bounds)).Right - Padding, ((SKRect)(ref bounds)).Bottom - Padding);
					canvas.Save();
					canvas.ClipRect(val4, (SKClipOperation)1, false);
					if (string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(Placeholder))
					{
						SKPaint val5 = new SKPaint(val3)
						{
							Color = PlaceholderColor,
							IsAntialias = true
						};
						try
						{
							canvas.DrawText(Placeholder, ((SKRect)(ref val4)).Left, ((SKRect)(ref val4)).Top + FontSize, val5);
						}
						finally
						{
							((IDisposable)val5)?.Dispose();
						}
					}
					else
					{
						SKPaint val6 = new SKPaint(val3);
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
						val6.Color = color;
						val6.IsAntialias = true;
						SKPaint val7 = val6;
						try
						{
							SKPaint val8 = new SKPaint
							{
								Color = SelectionColor,
								Style = (SKPaintStyle)0
							};
							try
							{
								float num3 = ((SKRect)(ref val4)).Top + FontSize;
								int num4 = 0;
								for (int i = 0; i < _lines.Count; i++)
								{
									string text = _lines[i];
									float left = ((SKRect)(ref val4)).Left;
									if (_selectionStart >= 0 && _selectionLength != 0)
									{
										int num5 = ((_selectionLength > 0) ? _selectionStart : (_selectionStart + _selectionLength));
										int num6 = ((_selectionLength > 0) ? (_selectionStart + _selectionLength) : _selectionStart);
										int num7 = num4;
										int num8 = num4 + text.Length;
										if (num6 > num7 && num5 < num8)
										{
											int length = Math.Max(0, num5 - num7);
											int length2 = Math.Min(text.Length, num6 - num7);
											float num9 = left + MeasureText(text.Substring(0, length), val3);
											float num10 = left + MeasureText(text.Substring(0, length2), val3);
											canvas.DrawRect(new SKRect(num9, num3 - FontSize, num10, num3 + num2 - FontSize), val8);
										}
									}
									canvas.DrawText(text, left, num3, val7);
									if (base.IsFocused && _cursorVisible)
									{
										var (num11, val9) = GetLineColumn(_cursorPosition);
										if (num11 == i)
										{
											float num12 = left + MeasureText(text.Substring(0, Math.Min(val9, text.Length)), val3);
											SKPaint val10 = new SKPaint
											{
												Color = CursorColor,
												Style = (SKPaintStyle)1,
												StrokeWidth = 2f,
												IsAntialias = true
											};
											try
											{
												canvas.DrawLine(num12, num3 - FontSize + 2f, num12, num3 + 2f, val10);
											}
											finally
											{
												((IDisposable)val10)?.Dispose();
											}
										}
									}
									num3 += num2;
									num4 += text.Length + 1;
								}
							}
							finally
							{
								((IDisposable)val8)?.Dispose();
							}
						}
						finally
						{
							((IDisposable)val7)?.Dispose();
						}
					}
					canvas.Restore();
					float num13 = (float)_lines.Count * FontSize * LineHeight;
					if (num13 > ((SKRect)(ref val4)).Height)
					{
						DrawScrollbar(canvas, bounds, ((SKRect)(ref val4)).Height, num13);
					}
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
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private float MeasureText(string text, SKFont font)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		if (string.IsNullOrEmpty(text))
		{
			return 0f;
		}
		SKPaint val = new SKPaint(font);
		try
		{
			return val.MeasureText(text);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void DrawScrollbar(SKCanvas canvas, SKRect bounds, float viewHeight, float contentHeight)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Expected O, but got Unknown
		float num = 6f;
		float num2 = 2f;
		float num3 = Math.Max(20f, viewHeight * (viewHeight / contentHeight));
		float num4 = ((SKRect)(ref bounds)).Top + Padding + _scrollOffsetY / contentHeight * (viewHeight - num3);
		SKPaint val = new SKPaint
		{
			Color = new SKColor((byte)0, (byte)0, (byte)0, (byte)60),
			Style = (SKPaintStyle)0,
			IsAntialias = true
		};
		try
		{
			canvas.DrawRoundRect(new SKRoundRect(new SKRect(((SKRect)(ref bounds)).Right - num - num2, num4, ((SKRect)(ref bounds)).Right - num2, num4 + num3), num / 2f), val);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void EnsureCursorVisible()
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		int item = GetLineColumn(_cursorPosition).line;
		float num = FontSize * LineHeight;
		float num2 = (float)item * num;
		SKRect bounds = base.Bounds;
		float num3 = ((SKRect)(ref bounds)).Height - Padding * 2f;
		if (num2 < _scrollOffsetY)
		{
			_scrollOffsetY = num2;
		}
		else if (num2 + num > _scrollOffsetY + num3)
		{
			_scrollOffsetY = num2 + num - num3;
		}
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Expected O, but got Unknown
		if (!base.IsEnabled)
		{
			return;
		}
		base.IsFocused = true;
		SKRect screenBounds = base.ScreenBounds;
		float num = e.X - ((SKRect)(ref screenBounds)).Left - Padding;
		float num2 = e.Y - ((SKRect)(ref screenBounds)).Top - Padding + _scrollOffsetY;
		float num3 = FontSize * LineHeight;
		int num4 = Math.Clamp((int)(num2 / num3), 0, _lines.Count - 1);
		SKFont val = new SKFont(SKTypeface.Default, FontSize, 1f, 0f);
		try
		{
			string text = _lines[num4];
			int column = 0;
			for (int i = 0; i <= text.Length; i++)
			{
				if (MeasureText(text.Substring(0, i), val) > num)
				{
					column = ((i > 0) ? (i - 1) : 0);
					break;
				}
				column = i;
			}
			_cursorPosition = GetPosition(num4, column);
			DateTime utcNow = DateTime.UtcNow;
			double totalMilliseconds = (utcNow - _lastClickTime).TotalMilliseconds;
			double num5 = Math.Sqrt(Math.Pow(e.X - _lastClickX, 2.0) + Math.Pow(e.Y - _lastClickY, 2.0));
			if (totalMilliseconds < 400.0 && num5 < 10.0)
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
				_lastClickY = e.Y;
			}
			_cursorVisible = true;
			_lastCursorBlink = DateTime.Now;
			Invalidate();
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Expected O, but got Unknown
		if (!base.IsEnabled || !_isSelecting)
		{
			return;
		}
		SKRect screenBounds = base.ScreenBounds;
		float num = e.X - ((SKRect)(ref screenBounds)).Left - Padding;
		float num2 = e.Y - ((SKRect)(ref screenBounds)).Top - Padding + _scrollOffsetY;
		float num3 = FontSize * LineHeight;
		int num4 = Math.Clamp((int)(num2 / num3), 0, _lines.Count - 1);
		SKFont val = new SKFont(SKTypeface.Default, FontSize, 1f, 0f);
		try
		{
			string text = _lines[num4];
			int column = 0;
			for (int i = 0; i <= text.Length; i++)
			{
				if (MeasureText(text.Substring(0, i), val) > num)
				{
					column = ((i > 0) ? (i - 1) : 0);
					break;
				}
				column = i;
			}
			int position = GetPosition(num4, column);
			if (position != _cursorPosition)
			{
				_cursorPosition = position;
				_selectionLength = _cursorPosition - _selectionStart;
				_cursorVisible = true;
				_lastCursorBlink = DateTime.Now;
				Invalidate();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		_isSelecting = false;
	}

	public override void OnKeyDown(KeyEventArgs e)
	{
		if (!base.IsEnabled)
		{
			return;
		}
		(int line, int column) lineColumn = GetLineColumn(_cursorPosition);
		int item = lineColumn.line;
		int item2 = lineColumn.column;
		_cursorVisible = true;
		_lastCursorBlink = DateTime.Now;
		switch (e.Key)
		{
		case Key.Left:
			if (_cursorPosition > 0)
			{
				_cursorPosition--;
				EnsureCursorVisible();
			}
			e.Handled = true;
			break;
		case Key.Right:
			if (_cursorPosition < Text.Length)
			{
				_cursorPosition++;
				EnsureCursorVisible();
			}
			e.Handled = true;
			break;
		case Key.Up:
			if (item > 0)
			{
				_cursorPosition = GetPosition(item - 1, item2);
				EnsureCursorVisible();
			}
			e.Handled = true;
			break;
		case Key.Down:
			if (item < _lines.Count - 1)
			{
				_cursorPosition = GetPosition(item + 1, item2);
				EnsureCursorVisible();
			}
			e.Handled = true;
			break;
		case Key.Home:
			_cursorPosition = GetPosition(item, 0);
			EnsureCursorVisible();
			e.Handled = true;
			break;
		case Key.End:
			_cursorPosition = GetPosition(item, _lines[item].Length);
			EnsureCursorVisible();
			e.Handled = true;
			break;
		case Key.Enter:
			if (!IsReadOnly)
			{
				InsertText("\n");
			}
			e.Handled = true;
			break;
		case Key.Backspace:
			if (!IsReadOnly)
			{
				if (_selectionLength != 0)
				{
					DeleteSelection();
				}
				else if (_cursorPosition > 0)
				{
					Text = Text.Remove(_cursorPosition - 1, 1);
					_cursorPosition--;
				}
				EnsureCursorVisible();
			}
			e.Handled = true;
			break;
		case Key.Delete:
			if (!IsReadOnly)
			{
				if (_selectionLength != 0)
				{
					DeleteSelection();
				}
				else if (_cursorPosition < Text.Length)
				{
					Text = Text.Remove(_cursorPosition, 1);
				}
			}
			e.Handled = true;
			break;
		case Key.Tab:
			if (!IsReadOnly)
			{
				InsertText("    ");
			}
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
		}
		Invalidate();
	}

	public override void OnTextInput(TextInputEventArgs e)
	{
		if (base.IsEnabled && !IsReadOnly && (string.IsNullOrEmpty(e.Text) || e.Text.Length != 1 || e.Text[0] >= ' ') && !string.IsNullOrEmpty(e.Text))
		{
			InsertText(e.Text);
			e.Handled = true;
		}
	}

	private void InsertText(string text)
	{
		if (_selectionLength > 0)
		{
			string text2 = Text;
			Text = text2.Remove(_selectionStart, _selectionLength);
			_cursorPosition = _selectionStart;
			_selectionStart = -1;
			_selectionLength = 0;
		}
		if (MaxLength > 0 && Text.Length + text.Length > MaxLength)
		{
			text = text.Substring(0, Math.Max(0, MaxLength - Text.Length));
		}
		if (!string.IsNullOrEmpty(text))
		{
			Text = Text.Insert(_cursorPosition, text);
			_cursorPosition += text.Length;
			EnsureCursorVisible();
		}
	}

	public override void OnScroll(ScrollEventArgs e)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		float num = FontSize * LineHeight;
		float num2 = (float)_lines.Count * num;
		SKRect bounds = base.Bounds;
		float num3 = ((SKRect)(ref bounds)).Height - Padding * 2f;
		float max = Math.Max(0f, num2 - num3);
		_scrollOffsetY = Math.Clamp(_scrollOffsetY - e.DeltaY * 3f, 0f, max);
		Invalidate();
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

	public void SelectAll()
	{
		_selectionStart = 0;
		_cursorPosition = Text.Length;
		_selectionLength = Text.Length;
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

	private void CopyToClipboard()
	{
		if (_selectionLength != 0)
		{
			int startIndex = Math.Min(_selectionStart, _selectionStart + _selectionLength);
			int length = Math.Abs(_selectionLength);
			SystemClipboard.SetText(Text.Substring(startIndex, length));
		}
	}

	private void CutToClipboard()
	{
		CopyToClipboard();
		DeleteSelection();
		Invalidate();
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
			InsertText(text);
		}
	}

	private void DeleteSelection()
	{
		if (_selectionLength != 0)
		{
			int num = Math.Min(_selectionStart, _selectionStart + _selectionLength);
			int count = Math.Abs(_selectionLength);
			Text = Text.Remove(num, count);
			_cursorPosition = num;
			_selectionStart = -1;
			_selectionLength = 0;
		}
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		if (AutoSize)
		{
			float num = FontSize * LineHeight;
			float val = Math.Max(num + Padding * 2f, (float)_lines.Count * num + Padding * 2f);
			return new SKSize((((SKSize)(ref availableSize)).Width < float.MaxValue) ? ((SKSize)(ref availableSize)).Width : 200f, Math.Min(val, (((SKSize)(ref availableSize)).Height < float.MaxValue) ? ((SKSize)(ref availableSize)).Height : 200f));
		}
		return new SKSize((((SKSize)(ref availableSize)).Width < float.MaxValue) ? Math.Min(((SKSize)(ref availableSize)).Width, 200f) : 200f, (((SKSize)(ref availableSize)).Height < float.MaxValue) ? Math.Min(((SKSize)(ref availableSize)).Height, 150f) : 150f);
	}
}
