using System;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaSearchBar : SkiaView
{
	private readonly SkiaEntry _entry;

	private bool _showClearButton;

	public string Text
	{
		get
		{
			return _entry.Text;
		}
		set
		{
			_entry.Text = value;
		}
	}

	public string Placeholder
	{
		get
		{
			return _entry.Placeholder;
		}
		set
		{
			_entry.Placeholder = value;
		}
	}

	public SKColor TextColor
	{
		get
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			return _entry.TextColor;
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			_entry.TextColor = value;
		}
	}

	public SKColor PlaceholderColor
	{
		get
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			return _entry.PlaceholderColor;
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			_entry.PlaceholderColor = value;
		}
	}

	public new SKColor BackgroundColor { get; set; } = new SKColor((byte)245, (byte)245, (byte)245);

	public SKColor IconColor { get; set; } = new SKColor((byte)117, (byte)117, (byte)117);

	public SKColor ClearButtonColor { get; set; } = new SKColor((byte)158, (byte)158, (byte)158);

	public SKColor FocusedBorderColor { get; set; } = new SKColor((byte)33, (byte)150, (byte)243);

	public string FontFamily { get; set; } = "Sans";

	public float FontSize { get; set; } = 14f;

	public float CornerRadius { get; set; } = 8f;

	public float IconSize { get; set; } = 20f;

	public event EventHandler<TextChangedEventArgs>? TextChanged;

	public event EventHandler? SearchButtonPressed;

	public SkiaSearchBar()
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		_entry = new SkiaEntry
		{
			Placeholder = "Search...",
			EntryBackgroundColor = SKColors.Transparent,
			BackgroundColor = SKColors.Transparent,
			BorderColor = SKColors.Transparent,
			FocusedBorderColor = SKColors.Transparent,
			BorderWidth = 0f
		};
		_entry.TextChanged += delegate(object? s, TextChangedEventArgs e)
		{
			_showClearButton = !string.IsNullOrEmpty(e.NewTextValue);
			this.TextChanged?.Invoke(this, e);
			Invalidate();
		};
		_entry.Completed += delegate
		{
			this.SearchButtonPressed?.Invoke(this, EventArgs.Empty);
		};
		base.IsFocusable = true;
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Expected O, but got Unknown
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		float num = 12f;
		float num2 = 20f;
		SKPaint val = new SKPaint
		{
			Color = BackgroundColor,
			IsAntialias = true,
			Style = (SKPaintStyle)0
		};
		try
		{
			SKRoundRect val2 = new SKRoundRect(bounds, CornerRadius);
			canvas.DrawRoundRect(val2, val);
			if (base.IsFocused || _entry.IsFocused)
			{
				SKPaint val3 = new SKPaint
				{
					Color = FocusedBorderColor,
					IsAntialias = true,
					Style = (SKPaintStyle)1,
					StrokeWidth = 2f
				};
				try
				{
					canvas.DrawRoundRect(val2, val3);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
			float num3 = ((SKRect)(ref bounds)).Left + num;
			float midY = ((SKRect)(ref bounds)).MidY;
			DrawSearchIcon(canvas, num3, midY, IconSize);
			float num4 = num3 + IconSize + num;
			float num5 = (_showClearButton ? (((SKRect)(ref bounds)).Right - num2 - num * 2f) : (((SKRect)(ref bounds)).Right - num));
			SKRect bounds2 = new SKRect(num4, ((SKRect)(ref bounds)).Top, num5, ((SKRect)(ref bounds)).Bottom);
			_entry.Arrange(bounds2);
			_entry.Draw(canvas);
			if (_showClearButton)
			{
				float x = ((SKRect)(ref bounds)).Right - num - num2 / 2f;
				float midY2 = ((SKRect)(ref bounds)).MidY;
				DrawClearButton(canvas, x, midY2, num2 / 2f);
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void DrawSearchIcon(SKCanvas canvas, float x, float y, float size)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected O, but got Unknown
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = IconColor,
			IsAntialias = true,
			Style = (SKPaintStyle)1,
			StrokeWidth = 2f,
			StrokeCap = (SKStrokeCap)1
		};
		try
		{
			float num = size * 0.35f;
			SKPoint val2 = new SKPoint(x + num, y - num * 0.3f);
			canvas.DrawCircle(val2, num, val);
			SKPoint val3 = new SKPoint(((SKPoint)(ref val2)).X + num * 0.7f, ((SKPoint)(ref val2)).Y + num * 0.7f);
			SKPoint val4 = new SKPoint(x + size * 0.8f, y + size * 0.3f);
			canvas.DrawLine(val3, val4, val);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void DrawClearButton(SKCanvas canvas, float x, float y, float radius)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected O, but got Unknown
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Expected O, but got Unknown
		SKPaint val = new SKPaint();
		SKColor clearButtonColor = ClearButtonColor;
		val.Color = ((SKColor)(ref clearButtonColor)).WithAlpha((byte)80);
		val.IsAntialias = true;
		val.Style = (SKPaintStyle)0;
		SKPaint val2 = val;
		try
		{
			canvas.DrawCircle(x, y, radius + 2f, val2);
			SKPaint val3 = new SKPaint
			{
				Color = ClearButtonColor,
				IsAntialias = true,
				Style = (SKPaintStyle)1,
				StrokeWidth = 2f,
				StrokeCap = (SKStrokeCap)1
			};
			try
			{
				float num = radius * 0.5f;
				canvas.DrawLine(x - num, y - num, x + num, y + num, val3);
				canvas.DrawLine(x + num, y - num, x - num, y + num, val3);
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
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		if (!base.IsEnabled)
		{
			return;
		}
		float x = e.X;
		SKRect bounds = base.Bounds;
		float num = x - ((SKRect)(ref bounds)).Left;
		if (_showClearButton)
		{
			bounds = base.Bounds;
			if (num >= ((SKRect)(ref bounds)).Width - 40f)
			{
				Text = "";
				Invalidate();
				return;
			}
		}
		_entry.IsFocused = true;
		base.IsFocused = true;
		_entry.OnPointerPressed(e);
		Invalidate();
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		if (base.IsEnabled)
		{
			_entry.OnPointerMoved(e);
		}
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		_entry.OnPointerReleased(e);
	}

	public override void OnTextInput(TextInputEventArgs e)
	{
		_entry.OnTextInput(e);
	}

	public override void OnKeyDown(KeyEventArgs e)
	{
		if (e.Key == Key.Escape && _showClearButton)
		{
			Text = "";
			e.Handled = true;
		}
		else
		{
			_entry.OnKeyDown(e);
		}
	}

	public override void OnKeyUp(KeyEventArgs e)
	{
		_entry.OnKeyUp(e);
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		return new SKSize(250f, 40f);
	}
}
