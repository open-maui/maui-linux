using System;
using System.Windows.Input;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaRefreshView : SkiaLayoutView
{
	private SkiaView? _content;

	private bool _isRefreshing;

	private float _pullDistance;

	private float _refreshThreshold = 80f;

	private bool _isPulling;

	private float _pullStartY;

	private float _spinnerRotation;

	private DateTime _lastSpinnerUpdate;

	public SkiaView? Content
	{
		get
		{
			return _content;
		}
		set
		{
			if (_content != value)
			{
				if (_content != null)
				{
					RemoveChild(_content);
				}
				_content = value;
				if (_content != null)
				{
					AddChild(_content);
				}
				InvalidateMeasure();
				Invalidate();
			}
		}
	}

	public bool IsRefreshing
	{
		get
		{
			return _isRefreshing;
		}
		set
		{
			if (_isRefreshing != value)
			{
				_isRefreshing = value;
				if (!value)
				{
					_pullDistance = 0f;
				}
				Invalidate();
			}
		}
	}

	public float RefreshThreshold
	{
		get
		{
			return _refreshThreshold;
		}
		set
		{
			_refreshThreshold = Math.Max(40f, value);
		}
	}

	public SKColor RefreshColor { get; set; } = new SKColor((byte)33, (byte)150, (byte)243);

	public SKColor RefreshBackgroundColor { get; set; } = SKColors.White;

	public ICommand? Command { get; set; }

	public object? CommandParameter { get; set; }

	public event EventHandler? Refreshing;

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (_content != null)
		{
			_content.Measure(availableSize);
		}
		return availableSize;
	}

	protected override SKRect ArrangeOverride(SKRect bounds)
	{
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		if (_content != null)
		{
			float num = (_isRefreshing ? _refreshThreshold : _pullDistance);
			SKRect bounds2 = default(SKRect);
			((SKRect)(ref bounds2))._002Ector(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top + num, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom + num);
			_content.Arrange(bounds2);
		}
		return bounds;
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		canvas.Save();
		canvas.ClipRect(bounds, (SKClipOperation)1, false);
		float y = ((SKRect)(ref bounds)).Top + (_isRefreshing ? _refreshThreshold : _pullDistance) / 2f;
		if (_pullDistance > 0f || _isRefreshing)
		{
			DrawRefreshIndicator(canvas, ((SKRect)(ref bounds)).MidX, y);
		}
		_content?.Draw(canvas);
		canvas.Restore();
	}

	private void DrawRefreshIndicator(SKCanvas canvas, float x, float y)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Expected O, but got Unknown
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Expected O, but got Unknown
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Expected O, but got Unknown
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		float num = 36f;
		float num2 = Math.Clamp(_pullDistance / _refreshThreshold, 0f, 1f);
		SKPaint val = new SKPaint
		{
			Color = RefreshBackgroundColor,
			Style = (SKPaintStyle)0,
			IsAntialias = true
		};
		try
		{
			val.ImageFilter = SKImageFilter.CreateDropShadow(0f, 2f, 4f, 4f, new SKColor((byte)0, (byte)0, (byte)0, (byte)40));
			canvas.DrawCircle(x, y, num / 2f, val);
			SKPaint val2 = new SKPaint
			{
				Color = RefreshColor,
				Style = (SKPaintStyle)1,
				StrokeWidth = 3f,
				IsAntialias = true,
				StrokeCap = (SKStrokeCap)1
			};
			try
			{
				if (_isRefreshing)
				{
					DateTime utcNow = DateTime.UtcNow;
					float num3 = (float)(utcNow - _lastSpinnerUpdate).TotalMilliseconds;
					_spinnerRotation += num3 * 0.36f;
					_lastSpinnerUpdate = utcNow;
					canvas.Save();
					canvas.Translate(x, y);
					canvas.RotateDegrees(_spinnerRotation);
					SKPath val3 = new SKPath();
					try
					{
						SKRect val4 = new SKRect((0f - num) / 3f, (0f - num) / 3f, num / 3f, num / 3f);
						val3.AddArc(val4, 0f, 270f);
						canvas.DrawPath(val3, val2);
						canvas.Restore();
						Invalidate();
						return;
					}
					finally
					{
						((IDisposable)val3)?.Dispose();
					}
				}
				canvas.Save();
				canvas.Translate(x, y);
				SKPath val5 = new SKPath();
				try
				{
					SKRect val6 = new SKRect((0f - num) / 3f, (0f - num) / 3f, num / 3f, num / 3f);
					float num4 = 270f * num2;
					val5.AddArc(val6, -90f, num4);
					canvas.DrawPath(val5, val2);
					canvas.Restore();
				}
				finally
				{
					((IDisposable)val5)?.Dispose();
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

	public override SkiaView? HitTest(float x, float y)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		if (base.IsVisible)
		{
			SKRect bounds = base.Bounds;
			if (((SKRect)(ref bounds)).Contains(x, y))
			{
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
		return null;
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		if (base.IsEnabled && !_isRefreshing)
		{
			bool flag = true;
			if (_content is SkiaScrollView skiaScrollView)
			{
				flag = skiaScrollView.ScrollY <= 0f;
			}
			if (flag)
			{
				_isPulling = true;
				_pullStartY = e.Y;
				_pullDistance = 0f;
			}
			base.OnPointerPressed(e);
		}
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		if (_isPulling)
		{
			float num = e.Y - _pullStartY;
			if (num > 0f)
			{
				_pullDistance = num * 0.5f;
				_pullDistance = Math.Min(_pullDistance, _refreshThreshold * 1.5f);
				Invalidate();
				e.Handled = true;
			}
			else
			{
				_pullDistance = 0f;
			}
			base.OnPointerMoved(e);
		}
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		if (_isPulling)
		{
			_isPulling = false;
			if (_pullDistance >= _refreshThreshold)
			{
				_isRefreshing = true;
				_pullDistance = _refreshThreshold;
				_lastSpinnerUpdate = DateTime.UtcNow;
				this.Refreshing?.Invoke(this, EventArgs.Empty);
				Command?.Execute(CommandParameter);
			}
			else
			{
				_pullDistance = 0f;
			}
			Invalidate();
			base.OnPointerReleased(e);
		}
	}
}
