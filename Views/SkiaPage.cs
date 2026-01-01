using System;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaPage : SkiaView
{
	private SkiaView? _content;

	private string _title = "";

	private SKColor _titleBarColor = new SKColor((byte)33, (byte)150, (byte)243);

	private SKColor _titleTextColor = SKColors.White;

	private bool _showNavigationBar;

	private float _navigationBarHeight = 56f;

	private float _paddingLeft;

	private float _paddingTop;

	private float _paddingRight;

	private float _paddingBottom;

	public SkiaView? Content
	{
		get
		{
			return _content;
		}
		set
		{
			if (_content != null)
			{
				_content.Parent = null;
			}
			_content = value;
			if (_content != null)
			{
				_content.Parent = this;
			}
			Invalidate();
		}
	}

	public string Title
	{
		get
		{
			return _title;
		}
		set
		{
			_title = value;
			Invalidate();
		}
	}

	public SKColor TitleBarColor
	{
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return _titleBarColor;
		}
		set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			_titleBarColor = value;
			Invalidate();
		}
	}

	public SKColor TitleTextColor
	{
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return _titleTextColor;
		}
		set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			_titleTextColor = value;
			Invalidate();
		}
	}

	public bool ShowNavigationBar
	{
		get
		{
			return _showNavigationBar;
		}
		set
		{
			_showNavigationBar = value;
			Invalidate();
		}
	}

	public float NavigationBarHeight
	{
		get
		{
			return _navigationBarHeight;
		}
		set
		{
			_navigationBarHeight = value;
			Invalidate();
		}
	}

	public float PaddingLeft
	{
		get
		{
			return _paddingLeft;
		}
		set
		{
			_paddingLeft = value;
			Invalidate();
		}
	}

	public float PaddingTop
	{
		get
		{
			return _paddingTop;
		}
		set
		{
			_paddingTop = value;
			Invalidate();
		}
	}

	public float PaddingRight
	{
		get
		{
			return _paddingRight;
		}
		set
		{
			_paddingRight = value;
			Invalidate();
		}
	}

	public float PaddingBottom
	{
		get
		{
			return _paddingBottom;
		}
		set
		{
			_paddingBottom = value;
			Invalidate();
		}
	}

	public bool IsBusy { get; set; }

	public event EventHandler? Appearing;

	public event EventHandler? Disappearing;

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		if (base.BackgroundColor != SKColors.Transparent)
		{
			SKPaint val = new SKPaint
			{
				Color = base.BackgroundColor,
				Style = (SKPaintStyle)0
			};
			try
			{
				canvas.DrawRect(bounds, val);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		float num = ((SKRect)(ref bounds)).Top;
		if (_showNavigationBar)
		{
			DrawNavigationBar(canvas, new SKRect(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Top + _navigationBarHeight));
			num = ((SKRect)(ref bounds)).Top + _navigationBarHeight;
		}
		SKRect val2 = default(SKRect);
		((SKRect)(ref val2))._002Ector(((SKRect)(ref bounds)).Left + _paddingLeft, num + _paddingTop, ((SKRect)(ref bounds)).Right - _paddingRight, ((SKRect)(ref bounds)).Bottom - _paddingBottom);
		if (_content != null)
		{
			Thickness margin = _content.Margin;
			SKRect bounds2 = default(SKRect);
			((SKRect)(ref bounds2))._002Ector(((SKRect)(ref val2)).Left + (float)((Thickness)(ref margin)).Left, ((SKRect)(ref val2)).Top + (float)((Thickness)(ref margin)).Top, ((SKRect)(ref val2)).Right - (float)((Thickness)(ref margin)).Right, ((SKRect)(ref val2)).Bottom - (float)((Thickness)(ref margin)).Bottom);
			SKSize availableSize = default(SKSize);
			((SKSize)(ref availableSize))._002Ector(((SKRect)(ref bounds2)).Width, ((SKRect)(ref bounds2)).Height);
			_content.Measure(availableSize);
			_content.Arrange(bounds2);
			Console.WriteLine($"[SkiaPage] Drawing content: {((object)_content).GetType().Name}, Bounds={_content.Bounds}, IsVisible={_content.IsVisible}");
			_content.Draw(canvas);
		}
		if (IsBusy)
		{
			DrawBusyIndicator(canvas, bounds);
		}
	}

	protected virtual void DrawNavigationBar(SKCanvas canvas, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Expected O, but got Unknown
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Expected O, but got Unknown
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = _titleBarColor,
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRect(bounds, val);
			if (!string.IsNullOrEmpty(_title))
			{
				SKFont val2 = new SKFont(SKTypeface.Default, 20f, 1f, 0f);
				try
				{
					SKPaint val3 = new SKPaint(val2)
					{
						Color = _titleTextColor,
						IsAntialias = true
					};
					try
					{
						SKRect val4 = default(SKRect);
						val3.MeasureText(_title, ref val4);
						float num = ((SKRect)(ref bounds)).Left + 16f;
						float num2 = ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val4)).MidY;
						canvas.DrawText(_title, num, num2, val3);
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
			SKPaint val5 = new SKPaint
			{
				Color = new SKColor((byte)0, (byte)0, (byte)0, (byte)30),
				Style = (SKPaintStyle)0,
				MaskFilter = SKMaskFilter.CreateBlur((SKBlurStyle)0, 2f)
			};
			try
			{
				canvas.DrawRect(new SKRect(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Bottom, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom + 4f), val5);
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

	private void DrawBusyIndicator(SKCanvas canvas, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Expected O, but got Unknown
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Expected O, but got Unknown
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = new SKColor(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)180),
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRect(bounds, val);
			SKPaint val2 = new SKPaint
			{
				Color = _titleBarColor,
				Style = (SKPaintStyle)1,
				StrokeWidth = 4f,
				IsAntialias = true,
				StrokeCap = (SKStrokeCap)1
			};
			try
			{
				float midX = ((SKRect)(ref bounds)).MidX;
				float midY = ((SKRect)(ref bounds)).MidY;
				float num = 20f;
				SKPath val3 = new SKPath();
				try
				{
					val3.AddArc(new SKRect(midX - num, midY - num, midX + num, midY + num), 0f, 270f);
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
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public void OnAppearing()
	{
		Console.WriteLine($"[SkiaPage] OnAppearing called for: {Title}, HasListeners={this.Appearing != null}");
		this.Appearing?.Invoke(this, EventArgs.Empty);
	}

	public void OnDisappearing()
	{
		this.Disappearing?.Invoke(this, EventArgs.Empty);
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		return availableSize;
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		float num = (_showNavigationBar ? _navigationBarHeight : 0f);
		if (e.Y > num && _content != null)
		{
			PointerEventArgs e2 = new PointerEventArgs(e.X - _paddingLeft, e.Y - num - _paddingTop, e.Button);
			_content.OnPointerPressed(e2);
		}
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		float num = (_showNavigationBar ? _navigationBarHeight : 0f);
		if (e.Y > num && _content != null)
		{
			PointerEventArgs e2 = new PointerEventArgs(e.X - _paddingLeft, e.Y - num - _paddingTop, e.Button);
			_content.OnPointerMoved(e2);
		}
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		float num = (_showNavigationBar ? _navigationBarHeight : 0f);
		if (e.Y > num && _content != null)
		{
			PointerEventArgs e2 = new PointerEventArgs(e.X - _paddingLeft, e.Y - num - _paddingTop, e.Button);
			_content.OnPointerReleased(e2);
		}
	}

	public override void OnKeyDown(KeyEventArgs e)
	{
		_content?.OnKeyDown(e);
	}

	public override void OnKeyUp(KeyEventArgs e)
	{
		_content?.OnKeyUp(e);
	}

	public override void OnScroll(ScrollEventArgs e)
	{
		_content?.OnScroll(e);
	}

	public override SkiaView? HitTest(float x, float y)
	{
		if (!base.IsVisible)
		{
			return null;
		}
		if (_content != null)
		{
			SkiaView skiaView = _content.HitTest(x, y);
			if (skiaView != null)
			{
				return skiaView;
			}
		}
		return this;
	}
}
