using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaItemsView : SkiaView
{
	private IEnumerable? _itemsSource;

	private List<object> _items = new List<object>();

	protected float _scrollOffset;

	private float _itemHeight = 44f;

	private float _itemSpacing;

	private int _firstVisibleIndex;

	private int _lastVisibleIndex;

	private bool _isDragging;

	private bool _isDraggingScrollbar;

	private float _dragStartY;

	private float _dragStartOffset;

	private float _scrollbarDragStartY;

	private float _scrollbarDragStartScrollOffset;

	private float _scrollbarDragAvailableTrack;

	private float _scrollbarDragMaxScroll;

	private float _velocity;

	private DateTime _lastDragTime;

	private bool _showVerticalScrollBar = true;

	private float _scrollBarWidth = 8f;

	private SKColor _scrollBarColor = new SKColor((byte)128, (byte)128, (byte)128, (byte)128);

	private SKColor _scrollBarTrackColor = new SKColor((byte)200, (byte)200, (byte)200, (byte)64);

	protected readonly Dictionary<int, SkiaView> _itemViewCache = new Dictionary<int, SkiaView>();

	protected readonly Dictionary<int, float> _itemHeights = new Dictionary<int, float>();

	private float _lastMeasuredWidth;

	public IEnumerable? ItemsSource
	{
		get
		{
			return _itemsSource;
		}
		set
		{
			if (_itemsSource is INotifyCollectionChanged notifyCollectionChanged)
			{
				notifyCollectionChanged.CollectionChanged -= OnCollectionChanged;
			}
			_itemsSource = value;
			RefreshItems();
			if (_itemsSource is INotifyCollectionChanged notifyCollectionChanged2)
			{
				notifyCollectionChanged2.CollectionChanged += OnCollectionChanged;
			}
			Invalidate();
		}
	}

	public float ItemHeight
	{
		get
		{
			return _itemHeight;
		}
		set
		{
			_itemHeight = value;
			Invalidate();
		}
	}

	public float ItemSpacing
	{
		get
		{
			return _itemSpacing;
		}
		set
		{
			_itemSpacing = value;
			Invalidate();
		}
	}

	public ScrollBarVisibility VerticalScrollBarVisibility { get; set; }

	public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; } = ScrollBarVisibility.Never;

	public object? EmptyView { get; set; }

	public string? EmptyViewText { get; set; } = "No items";

	public Func<object, int, SKRect, SKCanvas, SKPaint, bool>? ItemRenderer { get; set; }

	public Func<object, SkiaView?>? ItemViewCreator { get; set; }

	public virtual int SelectedIndex { get; set; } = -1;

	protected float TotalContentHeight
	{
		get
		{
			if (_items.Count == 0)
			{
				return 0f;
			}
			float num = 0f;
			for (int i = 0; i < _items.Count; i++)
			{
				num += GetItemHeight(i);
				if (i < _items.Count - 1)
				{
					num += _itemSpacing;
				}
			}
			return num;
		}
	}

	protected float MaxScrollOffset
	{
		get
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			float totalContentHeight = TotalContentHeight;
			SKRect screenBounds = base.ScreenBounds;
			return Math.Max(0f, totalContentHeight - ((SKRect)(ref screenBounds)).Height);
		}
	}

	protected int ItemCount => _items.Count;

	public event EventHandler<ItemsScrolledEventArgs>? Scrolled;

	public event EventHandler<ItemsViewItemTappedEventArgs>? ItemTapped;

	public SkiaItemsView()
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		base.IsFocusable = true;
	}

	protected virtual void RefreshItems()
	{
		Console.WriteLine($"[SkiaItemsView] RefreshItems called, clearing {_items.Count} items and {_itemViewCache.Count} cached views");
		_items.Clear();
		_itemViewCache.Clear();
		_itemHeights.Clear();
		if (_itemsSource != null)
		{
			foreach (object item in _itemsSource)
			{
				_items.Add(item);
			}
		}
		Console.WriteLine($"[SkiaItemsView] RefreshItems done, now have {_items.Count} items");
		_scrollOffset = 0f;
	}

	private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		RefreshItems();
		Invalidate();
	}

	protected float GetItemHeight(int index)
	{
		if (!_itemHeights.TryGetValue(index, out var value))
		{
			return _itemHeight;
		}
		return value;
	}

	protected float GetItemOffset(int index)
	{
		float num = 0f;
		for (int i = 0; i < index && i < _items.Count; i++)
		{
			num += GetItemHeight(i) + _itemSpacing;
		}
		return num;
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Expected O, but got Unknown
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Expected O, but got Unknown
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		Console.WriteLine($"[SkiaItemsView] OnDraw - bounds={bounds}, items={_items.Count}, ItemViewCreator={((ItemViewCreator != null) ? "set" : "null")}");
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
		if (_items.Count == 0)
		{
			DrawEmptyView(canvas, bounds);
			return;
		}
		_firstVisibleIndex = 0;
		float num = 0f;
		for (int i = 0; i < _items.Count; i++)
		{
			float itemHeight = GetItemHeight(i);
			if (num + itemHeight > _scrollOffset)
			{
				_firstVisibleIndex = i;
				break;
			}
			num += itemHeight + _itemSpacing;
		}
		canvas.Save();
		canvas.ClipRect(bounds, (SKClipOperation)1, false);
		SKPaint val2 = new SKPaint
		{
			IsAntialias = true
		};
		try
		{
			float num2 = ((SKRect)(ref bounds)).Top + GetItemOffset(_firstVisibleIndex) - _scrollOffset;
			SKRect bounds2 = default(SKRect);
			for (int j = _firstVisibleIndex; j < _items.Count; j++)
			{
				float itemHeight2 = GetItemHeight(j);
				((SKRect)(ref bounds2))._002Ector(((SKRect)(ref bounds)).Left, num2, ((SKRect)(ref bounds)).Right - (_showVerticalScrollBar ? _scrollBarWidth : 0f), num2 + itemHeight2);
				if (((SKRect)(ref bounds2)).Top > ((SKRect)(ref bounds)).Bottom)
				{
					_lastVisibleIndex = j - 1;
					break;
				}
				_lastVisibleIndex = j;
				if (((SKRect)(ref bounds2)).Bottom >= ((SKRect)(ref bounds)).Top)
				{
					DrawItem(canvas, _items[j], j, bounds2, val2);
				}
				num2 += itemHeight2 + _itemSpacing;
			}
			canvas.Restore();
			if (_showVerticalScrollBar && TotalContentHeight > ((SKRect)(ref bounds)).Height)
			{
				DrawScrollBar(canvas, bounds);
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	protected virtual void DrawItem(SKCanvas canvas, object item, int index, SKRect bounds, SKPaint paint)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Expected O, but got Unknown
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_024c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Expected O, but got Unknown
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		if (index == SelectedIndex)
		{
			paint.Color = new SKColor((byte)33, (byte)150, (byte)243, (byte)89);
			paint.Style = (SKPaintStyle)0;
			canvas.DrawRect(bounds, paint);
		}
		if (ItemViewCreator != null)
		{
			Console.WriteLine($"[SkiaItemsView] DrawItem {index} - ItemViewCreator exists, item: {item}");
			if (!_itemViewCache.TryGetValue(index, out SkiaView value) || value == null)
			{
				value = ItemViewCreator(item);
				if (value != null)
				{
					value.Parent = this;
					_itemViewCache[index] = value;
				}
			}
			if (value != null)
			{
				SKSize availableSize = default(SKSize);
				((SKSize)(ref availableSize))._002Ector(((SKRect)(ref bounds)).Width, float.MaxValue);
				SKSize val = value.Measure(availableSize);
				float num = Math.Max(((SKSize)(ref val)).Height, _itemHeight);
				if (!_itemHeights.TryGetValue(index, out var value2) || Math.Abs(value2 - num) > 1f)
				{
					_itemHeights[index] = num;
					if (Math.Abs(value2 - num) > 5f)
					{
						Invalidate();
					}
				}
				SKRect bounds2 = default(SKRect);
				((SKRect)(ref bounds2))._002Ector(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Top + num);
				value.Arrange(bounds2);
				value.Draw(canvas);
				return;
			}
		}
		else
		{
			Console.WriteLine($"[SkiaItemsView] DrawItem {index} - ItemViewCreator is NULL, falling back to ToString");
		}
		paint.Color = new SKColor((byte)224, (byte)224, (byte)224);
		paint.Style = (SKPaintStyle)1;
		paint.StrokeWidth = 1f;
		canvas.DrawLine(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Bottom, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom, paint);
		if (ItemRenderer != null && ItemRenderer(item, index, bounds, canvas, paint))
		{
			return;
		}
		paint.Color = SKColors.Black;
		paint.Style = (SKPaintStyle)0;
		SKFont val2 = new SKFont(SKTypeface.Default, 14f, 1f, 0f);
		try
		{
			SKPaint val3 = new SKPaint(val2)
			{
				Color = SKColors.Black,
				IsAntialias = true
			};
			try
			{
				string text = item?.ToString() ?? "";
				SKRect val4 = default(SKRect);
				val3.MeasureText(text, ref val4);
				float num2 = ((SKRect)(ref bounds)).Left + 16f;
				float num3 = ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val4)).MidY;
				canvas.DrawText(text, num2, num3, val3);
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

	protected virtual void DrawEmptyView(SKCanvas canvas, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Expected O, but got Unknown
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected O, but got Unknown
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = new SKColor((byte)128, (byte)128, (byte)128),
			IsAntialias = true
		};
		try
		{
			SKFont val2 = new SKFont(SKTypeface.Default, 16f, 1f, 0f);
			try
			{
				SKPaint val3 = new SKPaint(val2)
				{
					Color = new SKColor((byte)128, (byte)128, (byte)128),
					IsAntialias = true
				};
				try
				{
					string text = EmptyViewText ?? "No items";
					SKRect val4 = default(SKRect);
					val3.MeasureText(text, ref val4);
					float num = ((SKRect)(ref bounds)).MidX - ((SKRect)(ref val4)).MidX;
					float num2 = ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val4)).MidY;
					canvas.DrawText(text, num, num2, val3);
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

	private void DrawScrollBar(SKCanvas canvas, SKRect bounds)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Expected O, but got Unknown
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Expected O, but got Unknown
		SKRect val = default(SKRect);
		((SKRect)(ref val))._002Ector(((SKRect)(ref bounds)).Right - _scrollBarWidth, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom);
		SKPaint val2 = new SKPaint
		{
			Color = _scrollBarTrackColor,
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRect(val, val2);
			float num = ((SKRect)(ref bounds)).Height / TotalContentHeight;
			float num2 = Math.Max(20f, ((SKRect)(ref bounds)).Height * num);
			float num3 = _scrollOffset / MaxScrollOffset;
			float num4 = ((SKRect)(ref bounds)).Top + (((SKRect)(ref bounds)).Height - num2) * num3;
			SKRect val3 = new SKRect(((SKRect)(ref bounds)).Right - _scrollBarWidth + 1f, num4, ((SKRect)(ref bounds)).Right - 1f, num4 + num2);
			SKPaint val4 = new SKPaint
			{
				Color = _scrollBarColor,
				Style = (SKPaintStyle)0,
				IsAntialias = true
			};
			try
			{
				float num5 = (_scrollBarWidth - 2f) / 2f;
				canvas.DrawRoundRect(new SKRoundRect(val3, num5), val4);
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

	public override void OnPointerPressed(PointerEventArgs e)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		Console.WriteLine($"[SkiaItemsView] OnPointerPressed - x={e.X}, y={e.Y}, Bounds={base.Bounds}, ScreenBounds={base.ScreenBounds}, ItemCount={_items.Count}");
		if (!base.IsEnabled)
		{
			return;
		}
		if (_showVerticalScrollBar)
		{
			float totalContentHeight = TotalContentHeight;
			SKRect bounds = base.Bounds;
			if (totalContentHeight > ((SKRect)(ref bounds)).Height)
			{
				SKRect scrollbarThumbBounds = GetScrollbarThumbBounds();
				if (((SKRect)(ref scrollbarThumbBounds)).Contains(e.X, e.Y))
				{
					_isDraggingScrollbar = true;
					_scrollbarDragStartY = e.Y;
					_scrollbarDragStartScrollOffset = _scrollOffset;
					bounds = base.Bounds;
					float height = ((SKRect)(ref bounds)).Height;
					bounds = base.Bounds;
					float num = Math.Max(20f, height * (((SKRect)(ref bounds)).Height / TotalContentHeight));
					bounds = base.Bounds;
					_scrollbarDragAvailableTrack = ((SKRect)(ref bounds)).Height - num;
					_scrollbarDragMaxScroll = MaxScrollOffset;
					return;
				}
			}
		}
		_isDragging = true;
		_dragStartY = e.Y;
		_dragStartOffset = _scrollOffset;
		_lastDragTime = DateTime.Now;
		_velocity = 0f;
	}

	private SKRect GetScrollbarThumbBounds()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		SKRect screenBounds = base.ScreenBounds;
		float num = ((SKRect)(ref screenBounds)).Height / TotalContentHeight;
		float num2 = Math.Max(20f, ((SKRect)(ref screenBounds)).Height * num);
		float num3 = ((MaxScrollOffset > 0f) ? (_scrollOffset / MaxScrollOffset) : 0f);
		float num4 = ((SKRect)(ref screenBounds)).Top + (((SKRect)(ref screenBounds)).Height - num2) * num3;
		return new SKRect(((SKRect)(ref screenBounds)).Right - _scrollBarWidth, num4, ((SKRect)(ref screenBounds)).Right, num4 + num2);
	}

	public override void OnPointerMoved(PointerEventArgs e)
	{
		if (_isDraggingScrollbar)
		{
			if (_scrollbarDragAvailableTrack > 0f)
			{
				float num = (e.Y - _scrollbarDragStartY) / _scrollbarDragAvailableTrack * _scrollbarDragMaxScroll;
				SetScrollOffset(_scrollbarDragStartScrollOffset + num);
			}
		}
		else if (_isDragging)
		{
			float num2 = _dragStartY - e.Y;
			float num3 = _dragStartOffset + num2;
			DateTime now = DateTime.Now;
			double totalSeconds = (now - _lastDragTime).TotalSeconds;
			if (totalSeconds > 0.0)
			{
				_velocity = (float)((double)(_scrollOffset - num3) / totalSeconds);
			}
			_lastDragTime = now;
			SetScrollOffset(num3);
		}
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		if (_isDraggingScrollbar)
		{
			_isDraggingScrollbar = false;
		}
		else
		{
			if (!_isDragging)
			{
				return;
			}
			_isDragging = false;
			if (!(Math.Abs(e.Y - _dragStartY) < 5f))
			{
				return;
			}
			SKRect screenBounds = base.ScreenBounds;
			float num = e.Y - ((SKRect)(ref screenBounds)).Top + _scrollOffset;
			int num2 = -1;
			float num3 = 0f;
			for (int i = 0; i < _items.Count; i++)
			{
				float itemHeight = GetItemHeight(i);
				if (num >= num3 && num < num3 + itemHeight)
				{
					num2 = i;
					break;
				}
				num3 += itemHeight + _itemSpacing;
			}
			Console.WriteLine($"[SkiaItemsView] Tap at Y={e.Y}, screenBounds.Top={((SKRect)(ref screenBounds)).Top}, scrollOffset={_scrollOffset}, localY={num}, index={num2}");
			if (num2 >= 0 && num2 < _items.Count)
			{
				OnItemTapped(num2, _items[num2]);
			}
		}
	}

	private float GetTotalParentScrollY()
	{
		float num = 0f;
		for (SkiaView parent = base.Parent; parent != null; parent = parent.Parent)
		{
			if (parent is SkiaScrollView skiaScrollView)
			{
				num += skiaScrollView.ScrollY;
			}
		}
		return num;
	}

	protected virtual void OnItemTapped(int index, object item)
	{
		SelectedIndex = index;
		this.ItemTapped?.Invoke(this, new ItemsViewItemTappedEventArgs(index, item));
		Invalidate();
	}

	public override void OnScroll(ScrollEventArgs e)
	{
		float num = e.DeltaY * 20f;
		SetScrollOffset(_scrollOffset + num);
		e.Handled = true;
	}

	private void SetScrollOffset(float offset)
	{
		float scrollOffset = _scrollOffset;
		_scrollOffset = Math.Clamp(offset, 0f, MaxScrollOffset);
		if (Math.Abs(_scrollOffset - scrollOffset) > 0.1f)
		{
			this.Scrolled?.Invoke(this, new ItemsScrolledEventArgs(_scrollOffset, TotalContentHeight));
			Invalidate();
		}
	}

	public void ScrollToIndex(int index, bool animate = true)
	{
		if (index >= 0 && index < _items.Count)
		{
			float itemOffset = GetItemOffset(index);
			SetScrollOffset(itemOffset);
		}
	}

	public void ScrollToItem(object item, bool animate = true)
	{
		int num = _items.IndexOf(item);
		if (num >= 0)
		{
			ScrollToIndex(num, animate);
		}
	}

	public override void OnKeyDown(KeyEventArgs e)
	{
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		if (!base.IsEnabled)
		{
			return;
		}
		SKRect bounds;
		switch (e.Key)
		{
		case Key.Up:
			if (SelectedIndex > 0)
			{
				SelectedIndex--;
				EnsureIndexVisible(SelectedIndex);
				Invalidate();
			}
			e.Handled = true;
			break;
		case Key.Down:
			if (SelectedIndex < _items.Count - 1)
			{
				SelectedIndex++;
				EnsureIndexVisible(SelectedIndex);
				Invalidate();
			}
			e.Handled = true;
			break;
		case Key.PageUp:
		{
			float scrollOffset = _scrollOffset;
			bounds = base.Bounds;
			SetScrollOffset(scrollOffset - ((SKRect)(ref bounds)).Height);
			e.Handled = true;
			break;
		}
		case Key.PageDown:
		{
			float scrollOffset2 = _scrollOffset;
			bounds = base.Bounds;
			SetScrollOffset(scrollOffset2 + ((SKRect)(ref bounds)).Height);
			e.Handled = true;
			break;
		}
		case Key.Home:
			SelectedIndex = 0;
			SetScrollOffset(0f);
			Invalidate();
			e.Handled = true;
			break;
		case Key.End:
			SelectedIndex = _items.Count - 1;
			SetScrollOffset(MaxScrollOffset);
			Invalidate();
			e.Handled = true;
			break;
		case Key.Enter:
			if (SelectedIndex >= 0 && SelectedIndex < _items.Count)
			{
				OnItemTapped(SelectedIndex, _items[SelectedIndex]);
			}
			e.Handled = true;
			break;
		}
	}

	private void EnsureIndexVisible(int index)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		float itemOffset = GetItemOffset(index);
		float num = itemOffset + GetItemHeight(index);
		if (itemOffset < _scrollOffset)
		{
			SetScrollOffset(itemOffset);
			return;
		}
		float scrollOffset = _scrollOffset;
		SKRect bounds = base.Bounds;
		if (num > scrollOffset + ((SKRect)(ref bounds)).Height)
		{
			bounds = base.Bounds;
			SetScrollOffset(num - ((SKRect)(ref bounds)).Height);
		}
	}

	protected object? GetItemAt(int index)
	{
		if (index < 0 || index >= _items.Count)
		{
			return null;
		}
		return _items[index];
	}

	public SkiaView? GetItemView(int index)
	{
		if (!_itemViewCache.TryGetValue(index, out SkiaView value))
		{
			return null;
		}
		return value;
	}

	public override SkiaView? HitTest(float x, float y)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		if (base.IsVisible)
		{
			SKRect bounds = base.Bounds;
			if (((SKRect)(ref bounds)).Contains(new SKPoint(x, y)))
			{
				if (_showVerticalScrollBar)
				{
					float totalContentHeight = TotalContentHeight;
					bounds = base.Bounds;
					if (totalContentHeight > ((SKRect)(ref bounds)).Height)
					{
						bounds = base.Bounds;
						float num = ((SKRect)(ref bounds)).Right - _scrollBarWidth;
						bounds = base.Bounds;
						float top = ((SKRect)(ref bounds)).Top;
						bounds = base.Bounds;
						float right = ((SKRect)(ref bounds)).Right;
						bounds = base.Bounds;
						SKRect val = default(SKRect);
						((SKRect)(ref val))._002Ector(num, top, right, ((SKRect)(ref bounds)).Bottom);
						((SKRect)(ref val)).Contains(x, y);
						return this;
					}
				}
				return this;
			}
		}
		return null;
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		float num = ((((SKSize)(ref availableSize)).Width < float.MaxValue) ? ((SKSize)(ref availableSize)).Width : 200f);
		float num2 = ((((SKSize)(ref availableSize)).Height < float.MaxValue) ? ((SKSize)(ref availableSize)).Height : 300f);
		if (Math.Abs(num - _lastMeasuredWidth) > 5f)
		{
			_itemHeights.Clear();
			_itemViewCache.Clear();
			_lastMeasuredWidth = num;
		}
		return new SKSize(num, num2);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && _itemsSource is INotifyCollectionChanged notifyCollectionChanged)
		{
			notifyCollectionChanged.CollectionChanged -= OnCollectionChanged;
		}
		base.Dispose(disposing);
	}
}
