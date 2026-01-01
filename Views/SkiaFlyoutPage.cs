using System;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaFlyoutPage : SkiaLayoutView
{
	private SkiaView? _flyout;

	private SkiaView? _detail;

	private bool _isPresented;

	private float _flyoutWidth = 300f;

	private float _flyoutAnimationProgress;

	private bool _gestureEnabled = true;

	private bool _isDragging;

	private float _dragStartX;

	private float _dragCurrentX;

	public SkiaView? Flyout
	{
		get
		{
			return _flyout;
		}
		set
		{
			if (_flyout != value)
			{
				if (_flyout != null)
				{
					RemoveChild(_flyout);
				}
				_flyout = value;
				if (_flyout != null)
				{
					AddChild(_flyout);
				}
				Invalidate();
			}
		}
	}

	public SkiaView? Detail
	{
		get
		{
			return _detail;
		}
		set
		{
			if (_detail != value)
			{
				if (_detail != null)
				{
					RemoveChild(_detail);
				}
				_detail = value;
				if (_detail != null)
				{
					AddChild(_detail);
				}
				Invalidate();
			}
		}
	}

	public bool IsPresented
	{
		get
		{
			return _isPresented;
		}
		set
		{
			if (_isPresented != value)
			{
				_isPresented = value;
				_flyoutAnimationProgress = (value ? 1f : 0f);
				this.IsPresentedChanged?.Invoke(this, EventArgs.Empty);
				Invalidate();
			}
		}
	}

	public float FlyoutWidth
	{
		get
		{
			return _flyoutWidth;
		}
		set
		{
			if (_flyoutWidth != value)
			{
				_flyoutWidth = Math.Max(100f, value);
				InvalidateMeasure();
				Invalidate();
			}
		}
	}

	public bool GestureEnabled
	{
		get
		{
			return _gestureEnabled;
		}
		set
		{
			_gestureEnabled = value;
		}
	}

	public FlyoutLayoutBehavior FlyoutLayoutBehavior { get; set; }

	public SKColor ScrimColor { get; set; } = new SKColor((byte)0, (byte)0, (byte)0, (byte)100);

	public float ShadowWidth { get; set; } = 8f;

	public event EventHandler? IsPresentedChanged;

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		if (_flyout != null)
		{
			_flyout.Measure(new SKSize(FlyoutWidth, ((SKSize)(ref availableSize)).Height));
		}
		if (_detail != null)
		{
			_detail.Measure(availableSize);
		}
		return availableSize;
	}

	protected override SKRect ArrangeOverride(SKRect bounds)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		if (_detail != null)
		{
			_detail.Arrange(bounds);
		}
		if (_flyout != null)
		{
			float num = ((SKRect)(ref bounds)).Left - FlyoutWidth + FlyoutWidth * _flyoutAnimationProgress;
			SKRect bounds2 = default(SKRect);
			((SKRect)(ref bounds2))._002Ector(num, ((SKRect)(ref bounds)).Top, num + FlyoutWidth, ((SKRect)(ref bounds)).Bottom);
			_flyout.Arrange(bounds2);
		}
		return bounds;
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Expected O, but got Unknown
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		canvas.Save();
		canvas.ClipRect(bounds, (SKClipOperation)1, false);
		_detail?.Draw(canvas);
		if (_flyoutAnimationProgress > 0f)
		{
			SKPaint val = new SKPaint();
			SKColor scrimColor = ScrimColor;
			SKColor scrimColor2 = ScrimColor;
			val.Color = ((SKColor)(ref scrimColor)).WithAlpha((byte)((float)(int)((SKColor)(ref scrimColor2)).Alpha * _flyoutAnimationProgress));
			val.Style = (SKPaintStyle)0;
			SKPaint val2 = val;
			try
			{
				canvas.DrawRect(base.Bounds, val2);
				if (_flyout != null && ShadowWidth > 0f)
				{
					DrawFlyoutShadow(canvas);
				}
				_flyout?.Draw(canvas);
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		canvas.Restore();
	}

	private void DrawFlyoutShadow(SKCanvas canvas)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		if (_flyout == null)
		{
			return;
		}
		SKRect bounds = _flyout.Bounds;
		float right = ((SKRect)(ref bounds)).Right;
		bounds = base.Bounds;
		float top = ((SKRect)(ref bounds)).Top;
		float num = right + ShadowWidth;
		bounds = base.Bounds;
		SKRect val = default(SKRect);
		((SKRect)(ref val))._002Ector(right, top, num, ((SKRect)(ref bounds)).Bottom);
		SKPaint val2 = new SKPaint();
		val2.Shader = SKShader.CreateLinearGradient(new SKPoint(((SKRect)(ref val)).Left, ((SKRect)(ref val)).MidY), new SKPoint(((SKRect)(ref val)).Right, ((SKRect)(ref val)).MidY), (SKColor[])(object)new SKColor[2]
		{
			new SKColor((byte)0, (byte)0, (byte)0, (byte)60),
			SKColors.Transparent
		}, (float[])null, (SKShaderTileMode)0);
		SKPaint val3 = val2;
		try
		{
			canvas.DrawRect(val, val3);
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
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
				if (_flyoutAnimationProgress > 0f && _flyout != null)
				{
					SkiaView skiaView = _flyout.HitTest(x, y);
					if (skiaView != null)
					{
						return skiaView;
					}
					if (_isPresented)
					{
						return this;
					}
				}
				if (_detail != null)
				{
					SkiaView skiaView2 = _detail.HitTest(x, y);
					if (skiaView2 != null)
					{
						return skiaView2;
					}
				}
				return this;
			}
		}
		return null;
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		if (!base.IsEnabled)
		{
			return;
		}
		if (_isPresented && _flyout != null)
		{
			SKRect bounds = _flyout.Bounds;
			if (!((SKRect)(ref bounds)).Contains(e.X, e.Y))
			{
				IsPresented = false;
				e.Handled = true;
				return;
			}
		}
		if (_gestureEnabled)
		{
			_isDragging = true;
			_dragStartX = e.X;
			_dragCurrentX = e.X;
		}
		base.OnPointerPressed(e);
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		if (_isDragging && _gestureEnabled)
		{
			_dragCurrentX = e.X;
			float num = _dragCurrentX - _dragStartX;
			if (_isPresented)
			{
				_flyoutAnimationProgress = Math.Clamp(1f + num / FlyoutWidth, 0f, 1f);
			}
			else if (_dragStartX < 30f)
			{
				_flyoutAnimationProgress = Math.Clamp(num / FlyoutWidth, 0f, 1f);
			}
			Invalidate();
			e.Handled = true;
		}
		base.OnPointerMoved(e);
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		if (_isDragging)
		{
			_isDragging = false;
			if (_flyoutAnimationProgress > 0.5f)
			{
				_isPresented = true;
				_flyoutAnimationProgress = 1f;
			}
			else
			{
				_isPresented = false;
				_flyoutAnimationProgress = 0f;
			}
			this.IsPresentedChanged?.Invoke(this, EventArgs.Empty);
			Invalidate();
		}
		base.OnPointerReleased(e);
	}

	public void ToggleFlyout()
	{
		IsPresented = !IsPresented;
	}
}
