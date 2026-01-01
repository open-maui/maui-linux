using System;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaScrollView : SkiaView
{
	public static readonly BindableProperty OrientationProperty = BindableProperty.Create("Orientation", typeof(ScrollOrientation), typeof(SkiaScrollView), (object)ScrollOrientation.Both, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaScrollView)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty HorizontalScrollBarVisibilityProperty = BindableProperty.Create("HorizontalScrollBarVisibility", typeof(ScrollBarVisibility), typeof(SkiaScrollView), (object)ScrollBarVisibility.Auto, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaScrollView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty VerticalScrollBarVisibilityProperty = BindableProperty.Create("VerticalScrollBarVisibility", typeof(ScrollBarVisibility), typeof(SkiaScrollView), (object)ScrollBarVisibility.Auto, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaScrollView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ScrollBarColorProperty = BindableProperty.Create("ScrollBarColor", typeof(SKColor), typeof(SkiaScrollView), (object)new SKColor((byte)128, (byte)128, (byte)128, (byte)128), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaScrollView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ScrollBarWidthProperty = BindableProperty.Create("ScrollBarWidth", typeof(float), typeof(SkiaScrollView), (object)8f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaScrollView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private SkiaView? _content;

	private float _scrollX;

	private float _scrollY;

	private float _velocityX;

	private float _velocityY;

	private bool _isDragging;

	private bool _isDraggingVerticalScrollbar;

	private bool _isDraggingHorizontalScrollbar;

	private float _scrollbarDragStartY;

	private float _scrollbarDragStartScrollY;

	private float _scrollbarDragStartX;

	private float _scrollbarDragStartScrollX;

	private float _scrollbarDragAvailableTrack;

	private float _scrollbarDragScrollableExtent;

	private float _lastPointerX;

	private float _lastPointerY;

	public ScrollOrientation Orientation
	{
		get
		{
			return (ScrollOrientation)((BindableObject)this).GetValue(OrientationProperty);
		}
		set
		{
			((BindableObject)this).SetValue(OrientationProperty, (object)value);
		}
	}

	public ScrollBarVisibility HorizontalScrollBarVisibility
	{
		get
		{
			return (ScrollBarVisibility)((BindableObject)this).GetValue(HorizontalScrollBarVisibilityProperty);
		}
		set
		{
			((BindableObject)this).SetValue(HorizontalScrollBarVisibilityProperty, (object)value);
		}
	}

	public ScrollBarVisibility VerticalScrollBarVisibility
	{
		get
		{
			return (ScrollBarVisibility)((BindableObject)this).GetValue(VerticalScrollBarVisibilityProperty);
		}
		set
		{
			((BindableObject)this).SetValue(VerticalScrollBarVisibilityProperty, (object)value);
		}
	}

	public SKColor ScrollBarColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(ScrollBarColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(ScrollBarColorProperty, (object)value);
		}
	}

	public float ScrollBarWidth
	{
		get
		{
			return (float)((BindableObject)this).GetValue(ScrollBarWidthProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ScrollBarWidthProperty, (object)value);
		}
	}

	public SkiaView? Content
	{
		get
		{
			return _content;
		}
		set
		{
			if (_content == value)
			{
				return;
			}
			if (_content != null)
			{
				_content.Parent = null;
			}
			_content = value;
			if (_content != null)
			{
				_content.Parent = this;
				if (((BindableObject)this).BindingContext != null)
				{
					BindableObject.SetInheritedBindingContext((BindableObject)(object)_content, ((BindableObject)this).BindingContext);
				}
			}
			InvalidateMeasure();
			Invalidate();
		}
	}

	public float ScrollX
	{
		get
		{
			return _scrollX;
		}
		set
		{
			float num = ClampScrollX(value);
			if (_scrollX != num)
			{
				_scrollX = num;
				this.Scrolled?.Invoke(this, new ScrolledEventArgs(_scrollX, _scrollY));
				Invalidate();
			}
		}
	}

	public float ScrollY
	{
		get
		{
			return _scrollY;
		}
		set
		{
			float num = ClampScrollY(value);
			if (_scrollY != num)
			{
				_scrollY = num;
				this.Scrolled?.Invoke(this, new ScrolledEventArgs(_scrollX, _scrollY));
				Invalidate();
			}
		}
	}

	public float ScrollableWidth
	{
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			SKRect bounds = base.Bounds;
			float num;
			if (!float.IsInfinity(((SKRect)(ref bounds)).Width))
			{
				bounds = base.Bounds;
				if (!float.IsNaN(((SKRect)(ref bounds)).Width))
				{
					bounds = base.Bounds;
					if (!(((SKRect)(ref bounds)).Width <= 0f))
					{
						bounds = base.Bounds;
						num = ((SKRect)(ref bounds)).Width;
						goto IL_0054;
					}
				}
			}
			num = 800f;
			goto IL_0054;
			IL_0054:
			float num2 = num;
			SKSize contentSize = ContentSize;
			return Math.Max(0f, ((SKSize)(ref contentSize)).Width - num2);
		}
	}

	public float ScrollableHeight
	{
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			SKRect bounds = base.Bounds;
			float height = ((SKRect)(ref bounds)).Height;
			float num = ((float.IsInfinity(height) || float.IsNaN(height) || height <= 0f || height > 10000f) ? 544f : height);
			SKSize contentSize = ContentSize;
			return Math.Max(0f, ((SKSize)(ref contentSize)).Height - num);
		}
	}

	public SKSize ContentSize { get; private set; }

	public event EventHandler<ScrolledEventArgs>? Scrolled;

	protected override void OnBindingContextChanged()
	{
		base.OnBindingContextChanged();
		if (_content != null)
		{
			BindableObject.SetInheritedBindingContext((BindableObject)(object)_content, ((BindableObject)this).BindingContext);
		}
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		canvas.Save();
		canvas.ClipRect(bounds, (SKClipOperation)1, false);
		if (_content != null)
		{
			float num = ((SKRect)(ref bounds)).Width;
			if (Orientation != ScrollOrientation.Horizontal && VerticalScrollBarVisibility != ScrollBarVisibility.Never)
			{
				num -= ScrollBarWidth;
			}
			SKSize availableSize = default(SKSize);
			((SKSize)(ref availableSize))._002Ector(num, float.PositiveInfinity);
			ContentSize = _content.Measure(availableSize);
			Thickness margin = _content.Margin;
			float num2 = ((SKRect)(ref bounds)).Left + (float)((Thickness)(ref margin)).Left;
			float num3 = ((SKRect)(ref bounds)).Top + (float)((Thickness)(ref margin)).Top;
			float left = ((SKRect)(ref bounds)).Left;
			float width = ((SKRect)(ref bounds)).Width;
			SKSize desiredSize = _content.DesiredSize;
			float num4 = left + Math.Max(width, ((SKSize)(ref desiredSize)).Width) - (float)((Thickness)(ref margin)).Right;
			float top = ((SKRect)(ref bounds)).Top;
			float height = ((SKRect)(ref bounds)).Height;
			desiredSize = _content.DesiredSize;
			SKRect bounds2 = default(SKRect);
			((SKRect)(ref bounds2))._002Ector(num2, num3, num4, top + Math.Max(height, ((SKSize)(ref desiredSize)).Height) - (float)((Thickness)(ref margin)).Bottom);
			_content.Arrange(bounds2);
			canvas.Save();
			canvas.Translate(0f - _scrollX, 0f - _scrollY);
			_content.Draw(canvas);
			canvas.Restore();
		}
		DrawScrollbars(canvas, bounds);
		canvas.Restore();
	}

	private void DrawScrollbars(SKCanvas canvas, SKRect bounds)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		bool flag = ShouldShowVerticalScrollbar();
		bool flag2 = ShouldShowHorizontalScrollbar();
		if (flag && ScrollableHeight > 0f)
		{
			DrawVerticalScrollbar(canvas, bounds, flag2);
		}
		if (flag2 && ScrollableWidth > 0f)
		{
			DrawHorizontalScrollbar(canvas, bounds, flag);
		}
	}

	private bool ShouldShowVerticalScrollbar()
	{
		if (Orientation == ScrollOrientation.Horizontal)
		{
			return false;
		}
		return VerticalScrollBarVisibility switch
		{
			ScrollBarVisibility.Always => true, 
			ScrollBarVisibility.Never => false, 
			_ => ScrollableHeight > 0f, 
		};
	}

	private bool ShouldShowHorizontalScrollbar()
	{
		if (Orientation == ScrollOrientation.Vertical)
		{
			return false;
		}
		return HorizontalScrollBarVisibility switch
		{
			ScrollBarVisibility.Always => true, 
			ScrollBarVisibility.Never => false, 
			_ => ScrollableWidth > 0f, 
		};
	}

	private void DrawVerticalScrollbar(SKCanvas canvas, SKRect bounds, bool hasHorizontal)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Expected O, but got Unknown
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Expected O, but got Unknown
		float num = ((SKRect)(ref bounds)).Height - (hasHorizontal ? ScrollBarWidth : 0f);
		float height = ((SKRect)(ref bounds)).Height;
		SKSize contentSize = ContentSize;
		float num2 = Math.Max(20f, height / ((SKSize)(ref contentSize)).Height * num);
		float num3 = ScrollY / ScrollableHeight * (num - num2);
		SKPaint val = new SKPaint
		{
			Color = ScrollBarColor,
			IsAntialias = true
		};
		try
		{
			SKRoundRect val2 = new SKRoundRect(new SKRect(((SKRect)(ref bounds)).Right - ScrollBarWidth, ((SKRect)(ref bounds)).Top + num3, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Top + num3 + num2), ScrollBarWidth / 2f);
			canvas.DrawRoundRect(val2, val);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void DrawHorizontalScrollbar(SKCanvas canvas, SKRect bounds, bool hasVertical)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Expected O, but got Unknown
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Expected O, but got Unknown
		float num = ((SKRect)(ref bounds)).Width - (hasVertical ? ScrollBarWidth : 0f);
		float width = ((SKRect)(ref bounds)).Width;
		SKSize contentSize = ContentSize;
		float num2 = Math.Max(20f, width / ((SKSize)(ref contentSize)).Width * num);
		float num3 = ScrollX / ScrollableWidth * (num - num2);
		SKPaint val = new SKPaint
		{
			Color = ScrollBarColor,
			IsAntialias = true
		};
		try
		{
			SKRoundRect val2 = new SKRoundRect(new SKRect(((SKRect)(ref bounds)).Left + num3, ((SKRect)(ref bounds)).Bottom - ScrollBarWidth, ((SKRect)(ref bounds)).Left + num3 + num2, ((SKRect)(ref bounds)).Bottom), ScrollBarWidth / 2f);
			canvas.DrawRoundRect(val2, val);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public override void OnScroll(ScrollEventArgs e)
	{
		float num = 40f;
		bool flag = false;
		if (Orientation != ScrollOrientation.Horizontal && ScrollableHeight > 0f)
		{
			float scrollY = _scrollY;
			ScrollY += e.DeltaY * num;
			if (_scrollY != scrollY)
			{
				flag = true;
			}
		}
		if (Orientation != ScrollOrientation.Vertical && ScrollableWidth > 0f)
		{
			float scrollX = _scrollX;
			ScrollX += e.DeltaX * num;
			if (_scrollX != scrollX)
			{
				flag = true;
			}
		}
		if (flag)
		{
			e.Handled = true;
		}
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		SKRect bounds;
		SKSize contentSize;
		if (ShouldShowVerticalScrollbar() && ScrollableHeight > 0f)
		{
			SKRect verticalScrollbarThumbBounds = GetVerticalScrollbarThumbBounds();
			if (((SKRect)(ref verticalScrollbarThumbBounds)).Contains(e.X, e.Y))
			{
				_isDraggingVerticalScrollbar = true;
				_scrollbarDragStartY = e.Y;
				_scrollbarDragStartScrollY = _scrollY;
				bool flag = ShouldShowHorizontalScrollbar();
				bounds = base.Bounds;
				float num = ((SKRect)(ref bounds)).Height - (flag ? ScrollBarWidth : 0f);
				bounds = base.Bounds;
				float height = ((SKRect)(ref bounds)).Height;
				contentSize = ContentSize;
				float num2 = Math.Max(20f, height / ((SKSize)(ref contentSize)).Height * num);
				_scrollbarDragAvailableTrack = num - num2;
				_scrollbarDragScrollableExtent = ScrollableHeight;
				return;
			}
		}
		if (ShouldShowHorizontalScrollbar() && ScrollableWidth > 0f)
		{
			SKRect horizontalScrollbarThumbBounds = GetHorizontalScrollbarThumbBounds();
			if (((SKRect)(ref horizontalScrollbarThumbBounds)).Contains(e.X, e.Y))
			{
				_isDraggingHorizontalScrollbar = true;
				_scrollbarDragStartX = e.X;
				_scrollbarDragStartScrollX = _scrollX;
				bool flag2 = ShouldShowVerticalScrollbar();
				bounds = base.Bounds;
				float num3 = ((SKRect)(ref bounds)).Width - (flag2 ? ScrollBarWidth : 0f);
				bounds = base.Bounds;
				float width = ((SKRect)(ref bounds)).Width;
				contentSize = ContentSize;
				float num4 = Math.Max(20f, width / ((SKSize)(ref contentSize)).Width * num3);
				_scrollbarDragAvailableTrack = num3 - num4;
				_scrollbarDragScrollableExtent = ScrollableWidth;
				return;
			}
		}
		if (_content != null)
		{
			PointerEventArgs e2 = new PointerEventArgs(e.X + _scrollX, e.Y + _scrollY, e.Button);
			SkiaView skiaView = _content.HitTest(e2.X, e2.Y);
			if (skiaView != null && skiaView != _content)
			{
				skiaView.OnPointerPressed(e2);
				return;
			}
		}
		_isDragging = true;
		_lastPointerX = e.X;
		_lastPointerY = e.Y;
		_velocityX = 0f;
		_velocityY = 0f;
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		if (_isDraggingVerticalScrollbar)
		{
			if (_scrollbarDragAvailableTrack > 0f)
			{
				float num = (e.Y - _scrollbarDragStartY) / _scrollbarDragAvailableTrack * _scrollbarDragScrollableExtent;
				ScrollY = _scrollbarDragStartScrollY + num;
			}
		}
		else if (_isDraggingHorizontalScrollbar)
		{
			if (_scrollbarDragAvailableTrack > 0f)
			{
				float num2 = (e.X - _scrollbarDragStartX) / _scrollbarDragAvailableTrack * _scrollbarDragScrollableExtent;
				ScrollX = _scrollbarDragStartScrollX + num2;
			}
		}
		else if (_isDragging)
		{
			float num3 = _lastPointerX - e.X;
			float num4 = _lastPointerY - e.Y;
			_velocityX = num3;
			_velocityY = num4;
			if (Orientation != ScrollOrientation.Horizontal)
			{
				ScrollY += num4;
			}
			if (Orientation != ScrollOrientation.Vertical)
			{
				ScrollX += num3;
			}
			_lastPointerX = e.X;
			_lastPointerY = e.Y;
		}
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		_isDragging = false;
		_isDraggingVerticalScrollbar = false;
		_isDraggingHorizontalScrollbar = false;
	}

	private SKRect GetVerticalScrollbarThumbBounds()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		bool flag = ShouldShowHorizontalScrollbar();
		SKRect bounds = base.Bounds;
		float num = ((SKRect)(ref bounds)).Height - (flag ? ScrollBarWidth : 0f);
		bounds = base.Bounds;
		float height = ((SKRect)(ref bounds)).Height;
		SKSize contentSize = ContentSize;
		float num2 = Math.Max(20f, height / ((SKSize)(ref contentSize)).Height * num);
		float num3 = ((ScrollableHeight > 0f) ? (ScrollY / ScrollableHeight * (num - num2)) : 0f);
		bounds = base.Bounds;
		float num4 = ((SKRect)(ref bounds)).Right - ScrollBarWidth;
		bounds = base.Bounds;
		float num5 = ((SKRect)(ref bounds)).Top + num3;
		bounds = base.Bounds;
		float right = ((SKRect)(ref bounds)).Right;
		bounds = base.Bounds;
		return new SKRect(num4, num5, right, ((SKRect)(ref bounds)).Top + num3 + num2);
	}

	private SKRect GetHorizontalScrollbarThumbBounds()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		bool flag = ShouldShowVerticalScrollbar();
		SKRect bounds = base.Bounds;
		float num = ((SKRect)(ref bounds)).Width - (flag ? ScrollBarWidth : 0f);
		bounds = base.Bounds;
		float width = ((SKRect)(ref bounds)).Width;
		SKSize contentSize = ContentSize;
		float num2 = Math.Max(20f, width / ((SKSize)(ref contentSize)).Width * num);
		float num3 = ((ScrollableWidth > 0f) ? (ScrollX / ScrollableWidth * (num - num2)) : 0f);
		bounds = base.Bounds;
		float num4 = ((SKRect)(ref bounds)).Left + num3;
		bounds = base.Bounds;
		float num5 = ((SKRect)(ref bounds)).Bottom - ScrollBarWidth;
		bounds = base.Bounds;
		float num6 = ((SKRect)(ref bounds)).Left + num3 + num2;
		bounds = base.Bounds;
		return new SKRect(num4, num5, num6, ((SKRect)(ref bounds)).Bottom);
	}

	public override SkiaView? HitTest(float x, float y)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		if (base.IsVisible && base.IsEnabled)
		{
			SKRect bounds = base.Bounds;
			if (((SKRect)(ref bounds)).Contains(new SKPoint(x, y)))
			{
				if (ShouldShowVerticalScrollbar() && ScrollableHeight > 0f)
				{
					GetVerticalScrollbarThumbBounds();
					bounds = base.Bounds;
					float num = ((SKRect)(ref bounds)).Right - ScrollBarWidth;
					bounds = base.Bounds;
					float top = ((SKRect)(ref bounds)).Top;
					bounds = base.Bounds;
					float right = ((SKRect)(ref bounds)).Right;
					bounds = base.Bounds;
					SKRect val = default(SKRect);
					((SKRect)(ref val))._002Ector(num, top, right, ((SKRect)(ref bounds)).Bottom);
					if (((SKRect)(ref val)).Contains(x, y))
					{
						return this;
					}
				}
				if (ShouldShowHorizontalScrollbar() && ScrollableWidth > 0f)
				{
					bounds = base.Bounds;
					float left = ((SKRect)(ref bounds)).Left;
					bounds = base.Bounds;
					float num2 = ((SKRect)(ref bounds)).Bottom - ScrollBarWidth;
					bounds = base.Bounds;
					float right2 = ((SKRect)(ref bounds)).Right;
					bounds = base.Bounds;
					SKRect val2 = default(SKRect);
					((SKRect)(ref val2))._002Ector(left, num2, right2, ((SKRect)(ref bounds)).Bottom);
					if (((SKRect)(ref val2)).Contains(x, y))
					{
						return this;
					}
				}
				if (_content != null)
				{
					SkiaView skiaView = _content.HitTest(x + _scrollX, y + _scrollY);
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

	public void ScrollTo(float x, float y, bool animated = false)
	{
		ScrollX = x;
		ScrollY = y;
	}

	public void ScrollToView(SkiaView view, bool animated = false)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		if (_content == null)
		{
			return;
		}
		SKRect bounds = view.Bounds;
		float scrollX = ScrollX;
		float scrollY = ScrollY;
		float scrollX2 = ScrollX;
		SKRect bounds2 = base.Bounds;
		float num = scrollX2 + ((SKRect)(ref bounds2)).Width;
		float scrollY2 = ScrollY;
		bounds2 = base.Bounds;
		SKRect val = default(SKRect);
		((SKRect)(ref val))._002Ector(scrollX, scrollY, num, scrollY2 + ((SKRect)(ref bounds2)).Height);
		if (!((SKRect)(ref val)).Contains(bounds))
		{
			float x = ScrollX;
			float y = ScrollY;
			if (((SKRect)(ref bounds)).Left < ((SKRect)(ref val)).Left)
			{
				x = ((SKRect)(ref bounds)).Left;
			}
			else if (((SKRect)(ref bounds)).Right > ((SKRect)(ref val)).Right)
			{
				float right = ((SKRect)(ref bounds)).Right;
				bounds2 = base.Bounds;
				x = right - ((SKRect)(ref bounds2)).Width;
			}
			if (((SKRect)(ref bounds)).Top < ((SKRect)(ref val)).Top)
			{
				y = ((SKRect)(ref bounds)).Top;
			}
			else if (((SKRect)(ref bounds)).Bottom > ((SKRect)(ref val)).Bottom)
			{
				float bottom = ((SKRect)(ref bounds)).Bottom;
				bounds2 = base.Bounds;
				y = bottom - ((SKRect)(ref bounds2)).Height;
			}
			ScrollTo(x, y, animated);
		}
	}

	private float ClampScrollX(float value)
	{
		if (Orientation == ScrollOrientation.Vertical)
		{
			return 0f;
		}
		return Math.Clamp(value, 0f, ScrollableWidth);
	}

	private float ClampScrollY(float value)
	{
		if (Orientation == ScrollOrientation.Horizontal)
		{
			return 0f;
		}
		return Math.Clamp(value, 0f, ScrollableHeight);
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		if (_content != null)
		{
			float num;
			float num2;
			switch (Orientation)
			{
			case ScrollOrientation.Horizontal:
				num = float.PositiveInfinity;
				num2 = (float.IsInfinity(((SKSize)(ref availableSize)).Height) ? 400f : ((SKSize)(ref availableSize)).Height);
				break;
			case ScrollOrientation.Neither:
				num = (float.IsInfinity(((SKSize)(ref availableSize)).Width) ? 400f : ((SKSize)(ref availableSize)).Width);
				num2 = (float.IsInfinity(((SKSize)(ref availableSize)).Height) ? 400f : ((SKSize)(ref availableSize)).Height);
				break;
			case ScrollOrientation.Both:
				num = (float.IsInfinity(((SKSize)(ref availableSize)).Width) ? 800f : ((SKSize)(ref availableSize)).Width);
				if (VerticalScrollBarVisibility != ScrollBarVisibility.Never)
				{
					num -= ScrollBarWidth;
				}
				num2 = float.PositiveInfinity;
				break;
			default:
				num = (float.IsInfinity(((SKSize)(ref availableSize)).Width) ? 800f : ((SKSize)(ref availableSize)).Width);
				if (VerticalScrollBarVisibility != ScrollBarVisibility.Never)
				{
					num -= ScrollBarWidth;
				}
				num2 = float.PositiveInfinity;
				break;
			}
			ContentSize = _content.Measure(new SKSize(num, num2));
		}
		else
		{
			ContentSize = SKSize.Empty;
		}
		float num3;
		SKSize contentSize;
		if (!float.IsInfinity(((SKSize)(ref availableSize)).Width) && !float.IsNaN(((SKSize)(ref availableSize)).Width))
		{
			num3 = ((SKSize)(ref availableSize)).Width;
		}
		else
		{
			contentSize = ContentSize;
			num3 = Math.Min(((SKSize)(ref contentSize)).Width, 400f);
		}
		float num4;
		if (!float.IsInfinity(((SKSize)(ref availableSize)).Height) && !float.IsNaN(((SKSize)(ref availableSize)).Height))
		{
			num4 = ((SKSize)(ref availableSize)).Height;
		}
		else
		{
			contentSize = ContentSize;
			num4 = Math.Min(((SKSize)(ref contentSize)).Height, 400f);
		}
		float num5 = num4;
		return new SKSize(num3, num5);
	}

	protected override SKRect ArrangeOverride(SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		SKRect result = bounds;
		if (float.IsInfinity(((SKRect)(ref bounds)).Height) || float.IsNaN(((SKRect)(ref bounds)).Height))
		{
			Console.WriteLine($"[SkiaScrollView] WARNING: Infinite/NaN height, using default viewport={544f}");
			((SKRect)(ref result))._002Ector(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Top + 544f);
		}
		if (_content != null)
		{
			Thickness margin = _content.Margin;
			float num = ((SKRect)(ref result)).Left + (float)((Thickness)(ref margin)).Left;
			float num2 = ((SKRect)(ref result)).Top + (float)((Thickness)(ref margin)).Top;
			float left = ((SKRect)(ref result)).Left;
			float width = ((SKRect)(ref result)).Width;
			SKSize contentSize = ContentSize;
			float num3 = left + Math.Max(width, ((SKSize)(ref contentSize)).Width) - (float)((Thickness)(ref margin)).Right;
			float top = ((SKRect)(ref result)).Top;
			float height = ((SKRect)(ref result)).Height;
			contentSize = ContentSize;
			SKRect bounds2 = default(SKRect);
			((SKRect)(ref bounds2))._002Ector(num, num2, num3, top + Math.Max(height, ((SKSize)(ref contentSize)).Height) - (float)((Thickness)(ref margin)).Bottom);
			_content.Arrange(bounds2);
		}
		return result;
	}
}
