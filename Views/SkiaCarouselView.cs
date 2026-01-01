using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaCarouselView : SkiaLayoutView
{
	private readonly List<SkiaView> _items = new List<SkiaView>();

	private int _currentPosition;

	private float _scrollOffset;

	private float _targetScrollOffset;

	private bool _isDragging;

	private float _dragStartX;

	private float _dragStartOffset;

	private float _velocity;

	private DateTime _lastDragTime;

	private float _lastDragX;

	private bool _isAnimating;

	private float _animationStartOffset;

	private float _animationTargetOffset;

	private DateTime _animationStartTime;

	private const float AnimationDurationMs = 300f;

	public int Position
	{
		get
		{
			return _currentPosition;
		}
		set
		{
			if (value >= 0 && value < _items.Count && value != _currentPosition)
			{
				int currentPosition = _currentPosition;
				_currentPosition = value;
				AnimateToPosition(value);
				this.PositionChanged?.Invoke(this, new PositionChangedEventArgs(currentPosition, value));
			}
		}
	}

	public int ItemCount => _items.Count;

	public bool Loop { get; set; }

	public float PeekAreaInsets { get; set; }

	public float ItemSpacing { get; set; }

	public bool IsSwipeEnabled { get; set; } = true;

	public bool ShowIndicators { get; set; } = true;

	public SKColor IndicatorColor { get; set; } = new SKColor((byte)180, (byte)180, (byte)180);

	public SKColor SelectedIndicatorColor { get; set; } = new SKColor((byte)33, (byte)150, (byte)243);

	public event EventHandler<PositionChangedEventArgs>? PositionChanged;

	public event EventHandler? Scrolled;

	public void AddItem(SkiaView item)
	{
		_items.Add(item);
		AddChild(item);
		InvalidateMeasure();
		Invalidate();
	}

	public void RemoveItem(SkiaView item)
	{
		if (_items.Remove(item))
		{
			RemoveChild(item);
			if (_currentPosition >= _items.Count)
			{
				_currentPosition = Math.Max(0, _items.Count - 1);
			}
			InvalidateMeasure();
			Invalidate();
		}
	}

	public void ClearItems()
	{
		foreach (SkiaView item in _items)
		{
			RemoveChild(item);
		}
		_items.Clear();
		_currentPosition = 0;
		_scrollOffset = 0f;
		_targetScrollOffset = 0f;
		InvalidateMeasure();
		Invalidate();
	}

	public void ScrollTo(int position, bool animate = true)
	{
		if (position >= 0 && position < _items.Count)
		{
			int currentPosition = _currentPosition;
			_currentPosition = position;
			if (animate)
			{
				AnimateToPosition(position);
			}
			else
			{
				_scrollOffset = GetOffsetForPosition(position);
				_targetScrollOffset = _scrollOffset;
				Invalidate();
			}
			if (currentPosition != position)
			{
				this.PositionChanged?.Invoke(this, new PositionChangedEventArgs(currentPosition, position));
			}
		}
	}

	private void AnimateToPosition(int position)
	{
		_animationStartOffset = _scrollOffset;
		_animationTargetOffset = GetOffsetForPosition(position);
		_animationStartTime = DateTime.UtcNow;
		_isAnimating = true;
		Invalidate();
	}

	private float GetOffsetForPosition(int position)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		SKRect bounds = base.Bounds;
		float num = ((SKRect)(ref bounds)).Width - PeekAreaInsets * 2f;
		return (float)position * (num + ItemSpacing);
	}

	private int GetPositionForOffset(float offset)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		SKRect bounds = base.Bounds;
		float num = ((SKRect)(ref bounds)).Width - PeekAreaInsets * 2f;
		if (num <= 0f)
		{
			return 0;
		}
		return Math.Clamp((int)Math.Round(offset / (num + ItemSpacing)), 0, Math.Max(0, _items.Count - 1));
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		float num = ((SKSize)(ref availableSize)).Width - PeekAreaInsets * 2f;
		float num2 = ((SKSize)(ref availableSize)).Height - (float)(ShowIndicators ? 30 : 0);
		foreach (SkiaView item in _items)
		{
			item.Measure(new SKSize(num, num2));
		}
		return availableSize;
	}

	protected override SKRect ArrangeOverride(SKRect bounds)
	{
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		float num = ((SKRect)(ref bounds)).Width - PeekAreaInsets * 2f;
		float num2 = ((SKRect)(ref bounds)).Height - (float)(ShowIndicators ? 30 : 0);
		SKRect bounds2 = default(SKRect);
		for (int i = 0; i < _items.Count; i++)
		{
			float num3 = ((SKRect)(ref bounds)).Left + PeekAreaInsets + (float)i * (num + ItemSpacing) - _scrollOffset;
			((SKRect)(ref bounds2))._002Ector(num3, ((SKRect)(ref bounds)).Top, num3 + num, ((SKRect)(ref bounds)).Top + num2);
			_items[i].Arrange(bounds2);
		}
		return bounds;
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		if (_isAnimating)
		{
			float num = Math.Clamp((float)(DateTime.UtcNow - _animationStartTime).TotalMilliseconds / 300f, 0f, 1f);
			float num2 = 1f - (1f - num) * (1f - num) * (1f - num);
			_scrollOffset = _animationStartOffset + (_animationTargetOffset - _animationStartOffset) * num2;
			if (num >= 1f)
			{
				_isAnimating = false;
				_scrollOffset = _animationTargetOffset;
			}
			else
			{
				Invalidate();
			}
		}
		canvas.Save();
		canvas.ClipRect(bounds, (SKClipOperation)1, false);
		float num3 = ((SKRect)(ref bounds)).Width - PeekAreaInsets * 2f;
		_ = ((SKRect)(ref bounds)).Height;
		_ = ShowIndicators;
		for (int i = 0; i < _items.Count; i++)
		{
			float num4 = ((SKRect)(ref bounds)).Left + PeekAreaInsets + (float)i * (num3 + ItemSpacing) - _scrollOffset;
			if (num4 + num3 > ((SKRect)(ref bounds)).Left && num4 < ((SKRect)(ref bounds)).Right)
			{
				_items[i].Draw(canvas);
			}
		}
		if (ShowIndicators && _items.Count > 1)
		{
			DrawIndicators(canvas, bounds);
		}
		canvas.Restore();
	}

	private void DrawIndicators(SKCanvas canvas, SKRect bounds)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Expected O, but got Unknown
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Expected O, but got Unknown
		float num = 8f;
		float num2 = 12f;
		float num3 = (float)_items.Count * num + (float)(_items.Count - 1) * (num2 - num);
		float num4 = ((SKRect)(ref bounds)).MidX - num3 / 2f;
		float num5 = ((SKRect)(ref bounds)).Bottom - 15f;
		SKPaint val = new SKPaint
		{
			Color = IndicatorColor,
			Style = (SKPaintStyle)0,
			IsAntialias = true
		};
		try
		{
			SKPaint val2 = new SKPaint
			{
				Color = SelectedIndicatorColor,
				Style = (SKPaintStyle)0,
				IsAntialias = true
			};
			try
			{
				for (int i = 0; i < _items.Count; i++)
				{
					float num6 = num4 + (float)i * num2;
					SKPaint val3 = ((i == _currentPosition) ? val2 : val);
					canvas.DrawCircle(num6, num5, num / 2f, val3);
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
				{
					foreach (SkiaView item in _items)
					{
						SkiaView skiaView = item.HitTest(x, y);
						if (skiaView != null)
						{
							return skiaView;
						}
					}
					return this;
				}
			}
		}
		return null;
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		if (base.IsEnabled && IsSwipeEnabled)
		{
			_isDragging = true;
			_dragStartX = e.X;
			_dragStartOffset = _scrollOffset;
			_lastDragX = e.X;
			_lastDragTime = DateTime.UtcNow;
			_velocity = 0f;
			_isAnimating = false;
			e.Handled = true;
			base.OnPointerPressed(e);
		}
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		if (_isDragging)
		{
			float num = _dragStartX - e.X;
			_scrollOffset = _dragStartOffset + num;
			float offsetForPosition = GetOffsetForPosition(_items.Count - 1);
			_scrollOffset = Math.Clamp(_scrollOffset, 0f, offsetForPosition);
			DateTime utcNow = DateTime.UtcNow;
			float num2 = (float)(utcNow - _lastDragTime).TotalSeconds;
			if (num2 > 0f)
			{
				_velocity = (_lastDragX - e.X) / num2;
			}
			_lastDragX = e.X;
			_lastDragTime = utcNow;
			this.Scrolled?.Invoke(this, EventArgs.Empty);
			Invalidate();
			e.Handled = true;
			base.OnPointerMoved(e);
		}
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		if (!_isDragging)
		{
			return;
		}
		_isDragging = false;
		SKRect bounds = base.Bounds;
		_ = ((SKRect)(ref bounds)).Width;
		_ = PeekAreaInsets;
		int num = GetPositionForOffset(_scrollOffset);
		if (Math.Abs(_velocity) > 500f)
		{
			if (_velocity > 0f && num < _items.Count - 1)
			{
				num++;
			}
			else if (_velocity < 0f && num > 0)
			{
				num--;
			}
		}
		ScrollTo(num);
		e.Handled = true;
		base.OnPointerReleased(e);
	}
}
