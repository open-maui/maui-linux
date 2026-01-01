using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaSwipeView : SkiaLayoutView
{
	private SkiaView? _content;

	private readonly List<SwipeItem> _leftItems = new List<SwipeItem>();

	private readonly List<SwipeItem> _rightItems = new List<SwipeItem>();

	private readonly List<SwipeItem> _topItems = new List<SwipeItem>();

	private readonly List<SwipeItem> _bottomItems = new List<SwipeItem>();

	private float _swipeOffset;

	private SwipeDirection _activeDirection;

	private bool _isSwiping;

	private float _swipeStartX;

	private float _swipeStartY;

	private float _swipeStartOffset;

	private bool _isOpen;

	private const float SwipeThreshold = 60f;

	private const float VelocityThreshold = 500f;

	private float _velocity;

	private DateTime _lastMoveTime;

	private float _lastMovePosition;

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

	public IList<SwipeItem> LeftItems => _leftItems;

	public IList<SwipeItem> RightItems => _rightItems;

	public IList<SwipeItem> TopItems => _topItems;

	public IList<SwipeItem> BottomItems => _bottomItems;

	public SwipeMode Mode { get; set; }

	public float LeftSwipeThreshold { get; set; } = 100f;

	public float RightSwipeThreshold { get; set; } = 100f;

	public event EventHandler<SwipeStartedEventArgs>? SwipeStarted;

	public event EventHandler<SwipeEndedEventArgs>? SwipeEnded;

	public void Open(SwipeDirection direction)
	{
		_activeDirection = direction;
		_isOpen = true;
		AnimateTo(direction switch
		{
			SwipeDirection.Left => 0f - RightSwipeThreshold, 
			SwipeDirection.Right => LeftSwipeThreshold, 
			_ => 0f, 
		});
	}

	public void Close()
	{
		_isOpen = false;
		AnimateTo(0f);
	}

	private void AnimateTo(float target)
	{
		_swipeOffset = target;
		Invalidate();
	}

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
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		if (_content != null)
		{
			SKRect bounds2 = default(SKRect);
			((SKRect)(ref bounds2))._002Ector(((SKRect)(ref bounds)).Left + _swipeOffset, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Right + _swipeOffset, ((SKRect)(ref bounds)).Bottom);
			_content.Arrange(bounds2);
		}
		return bounds;
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		canvas.Save();
		canvas.ClipRect(bounds, (SKClipOperation)1, false);
		if (_swipeOffset > 0f)
		{
			DrawSwipeItems(canvas, bounds, _leftItems, isLeft: true);
		}
		else if (_swipeOffset < 0f)
		{
			DrawSwipeItems(canvas, bounds, _rightItems, isLeft: false);
		}
		_content?.Draw(canvas);
		canvas.Restore();
	}

	private void DrawSwipeItems(SKCanvas canvas, SKRect bounds, List<SwipeItem> items, bool isLeft)
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Expected O, but got Unknown
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Expected O, but got Unknown
		if (items.Count == 0)
		{
			return;
		}
		float num = Math.Abs(_swipeOffset) / (float)items.Count;
		SKRect val = default(SKRect);
		for (int i = 0; i < items.Count; i++)
		{
			SwipeItem swipeItem = items[i];
			float num2 = (isLeft ? (((SKRect)(ref bounds)).Left + (float)i * num) : (((SKRect)(ref bounds)).Right - (float)(items.Count - i) * num));
			((SKRect)(ref val))._002Ector(num2, ((SKRect)(ref bounds)).Top, num2 + num, ((SKRect)(ref bounds)).Bottom);
			SKPaint val2 = new SKPaint
			{
				Color = swipeItem.BackgroundColor,
				Style = (SKPaintStyle)0
			};
			try
			{
				canvas.DrawRect(val, val2);
				if (!string.IsNullOrEmpty(swipeItem.Text))
				{
					SKPaint val3 = new SKPaint
					{
						Color = swipeItem.TextColor,
						TextSize = 14f,
						IsAntialias = true,
						TextAlign = (SKTextAlign)1
					};
					try
					{
						float num3 = ((SKRect)(ref val)).MidY + 5f;
						canvas.DrawText(swipeItem.Text, ((SKRect)(ref val)).MidX, num3, val3);
					}
					finally
					{
						((IDisposable)val3)?.Dispose();
					}
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
	}

	public override SkiaView? HitTest(float x, float y)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		if (base.IsVisible)
		{
			SKRect bounds = base.Bounds;
			if (((SKRect)(ref bounds)).Contains(x, y))
			{
				if (_isOpen)
				{
					if (_swipeOffset > 0f)
					{
						bounds = base.Bounds;
						if (x < ((SKRect)(ref bounds)).Left + _swipeOffset)
						{
							return this;
						}
					}
					if (_swipeOffset < 0f)
					{
						bounds = base.Bounds;
						if (x > ((SKRect)(ref bounds)).Right + _swipeOffset)
						{
							return this;
						}
					}
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
		return null;
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		if (!base.IsEnabled)
		{
			return;
		}
		if (_isOpen)
		{
			SwipeItem swipeItem = null;
			SKRect bounds;
			if (_swipeOffset > 0f)
			{
				float x = e.X;
				bounds = base.Bounds;
				int num = (int)((x - ((SKRect)(ref bounds)).Left) / (_swipeOffset / (float)_leftItems.Count));
				if (num >= 0 && num < _leftItems.Count)
				{
					swipeItem = _leftItems[num];
				}
			}
			else if (_swipeOffset < 0f)
			{
				float num2 = Math.Abs(_swipeOffset) / (float)_rightItems.Count;
				float x2 = e.X;
				bounds = base.Bounds;
				int num3 = (int)((x2 - (((SKRect)(ref bounds)).Right + _swipeOffset)) / num2);
				if (num3 >= 0 && num3 < _rightItems.Count)
				{
					swipeItem = _rightItems[num3];
				}
			}
			if (swipeItem != null)
			{
				swipeItem.OnInvoked();
				Close();
				e.Handled = true;
				return;
			}
		}
		_isSwiping = true;
		_swipeStartX = e.X;
		_swipeStartY = e.Y;
		_swipeStartOffset = _swipeOffset;
		_lastMovePosition = e.X;
		_lastMoveTime = DateTime.UtcNow;
		_velocity = 0f;
		base.OnPointerPressed(e);
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		if (!_isSwiping)
		{
			return;
		}
		float num = e.X - _swipeStartX;
		_ = e.Y;
		_ = _swipeStartY;
		if (_activeDirection == SwipeDirection.None && Math.Abs(num) > 10f)
		{
			_activeDirection = ((!(num > 0f)) ? SwipeDirection.Left : SwipeDirection.Right);
			this.SwipeStarted?.Invoke(this, new SwipeStartedEventArgs(_activeDirection));
		}
		if (_activeDirection == SwipeDirection.Right || _activeDirection == SwipeDirection.Left)
		{
			_swipeOffset = _swipeStartOffset + num;
			float max = ((_leftItems.Count > 0) ? LeftSwipeThreshold : 0f);
			float min = ((_rightItems.Count > 0) ? (0f - RightSwipeThreshold) : 0f);
			_swipeOffset = Math.Clamp(_swipeOffset, min, max);
			DateTime utcNow = DateTime.UtcNow;
			float num2 = (float)(utcNow - _lastMoveTime).TotalSeconds;
			if (num2 > 0f)
			{
				_velocity = (e.X - _lastMovePosition) / num2;
			}
			_lastMovePosition = e.X;
			_lastMoveTime = utcNow;
			Invalidate();
			e.Handled = true;
		}
		base.OnPointerMoved(e);
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		if (!_isSwiping)
		{
			return;
		}
		_isSwiping = false;
		bool flag = false;
		if ((!(Math.Abs(_velocity) > 500f)) ? (Math.Abs(_swipeOffset) > 60f) : ((_velocity > 0f && _leftItems.Count > 0) || (_velocity < 0f && _rightItems.Count > 0)))
		{
			if (_swipeOffset > 0f)
			{
				Open(SwipeDirection.Right);
			}
			else
			{
				Open(SwipeDirection.Left);
			}
		}
		else
		{
			Close();
		}
		this.SwipeEnded?.Invoke(this, new SwipeEndedEventArgs(_activeDirection, _isOpen));
		_activeDirection = SwipeDirection.None;
		base.OnPointerReleased(e);
	}
}
