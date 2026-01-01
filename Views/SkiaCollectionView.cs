using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaCollectionView : SkiaItemsView
{
	public static readonly BindableProperty SelectionModeProperty = BindableProperty.Create("SelectionMode", typeof(SkiaSelectionMode), typeof(SkiaCollectionView), (object)SkiaSelectionMode.Single, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCollectionView)(object)b).OnSelectionModeChanged();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create("SelectedItem", typeof(object), typeof(SkiaCollectionView), (object)null, (BindingMode)1, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCollectionView)(object)b).OnSelectedItemChanged(n);
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty OrientationProperty = BindableProperty.Create("Orientation", typeof(ItemsLayoutOrientation), typeof(SkiaCollectionView), (object)ItemsLayoutOrientation.Vertical, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCollectionView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty SpanCountProperty = BindableProperty.Create("SpanCount", typeof(int), typeof(SkiaCollectionView), (object)1, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCollectionView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)((BindableObject b, object v) => Math.Max(1, (int)v)), (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty GridItemWidthProperty = BindableProperty.Create("GridItemWidth", typeof(float), typeof(SkiaCollectionView), (object)100f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCollectionView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty HeaderProperty = BindableProperty.Create("Header", typeof(object), typeof(SkiaCollectionView), (object)null, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCollectionView)(object)b).OnHeaderChanged(n);
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FooterProperty = BindableProperty.Create("Footer", typeof(object), typeof(SkiaCollectionView), (object)null, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCollectionView)(object)b).OnFooterChanged(n);
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty HeaderHeightProperty = BindableProperty.Create("HeaderHeight", typeof(float), typeof(SkiaCollectionView), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCollectionView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FooterHeightProperty = BindableProperty.Create("FooterHeight", typeof(float), typeof(SkiaCollectionView), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCollectionView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty SelectionColorProperty = BindableProperty.Create("SelectionColor", typeof(SKColor), typeof(SkiaCollectionView), (object)new SKColor((byte)33, (byte)150, (byte)243, (byte)89), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCollectionView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty HeaderBackgroundColorProperty = BindableProperty.Create("HeaderBackgroundColor", typeof(SKColor), typeof(SkiaCollectionView), (object)new SKColor((byte)245, (byte)245, (byte)245), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCollectionView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty FooterBackgroundColorProperty = BindableProperty.Create("FooterBackgroundColor", typeof(SKColor), typeof(SkiaCollectionView), (object)new SKColor((byte)245, (byte)245, (byte)245), (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaCollectionView)(object)b).Invalidate();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private List<object> _selectedItems = new List<object>();

	private int _selectedIndex = -1;

	private bool _isSelectingItem;

	private bool _heightsChangedDuringDraw;

	public SkiaSelectionMode SelectionMode
	{
		get
		{
			return (SkiaSelectionMode)((BindableObject)this).GetValue(SelectionModeProperty);
		}
		set
		{
			((BindableObject)this).SetValue(SelectionModeProperty, (object)value);
		}
	}

	public object? SelectedItem
	{
		get
		{
			return ((BindableObject)this).GetValue(SelectedItemProperty);
		}
		set
		{
			((BindableObject)this).SetValue(SelectedItemProperty, value);
		}
	}

	public IList<object> SelectedItems => _selectedItems.AsReadOnly();

	public override int SelectedIndex
	{
		get
		{
			return _selectedIndex;
		}
		set
		{
			if (SelectionMode != SkiaSelectionMode.None)
			{
				object itemAt = GetItemAt(value);
				if (itemAt != null)
				{
					SelectedItem = itemAt;
				}
			}
		}
	}

	public ItemsLayoutOrientation Orientation
	{
		get
		{
			return (ItemsLayoutOrientation)((BindableObject)this).GetValue(OrientationProperty);
		}
		set
		{
			((BindableObject)this).SetValue(OrientationProperty, (object)value);
		}
	}

	public int SpanCount
	{
		get
		{
			return (int)((BindableObject)this).GetValue(SpanCountProperty);
		}
		set
		{
			((BindableObject)this).SetValue(SpanCountProperty, (object)value);
		}
	}

	public float GridItemWidth
	{
		get
		{
			return (float)((BindableObject)this).GetValue(GridItemWidthProperty);
		}
		set
		{
			((BindableObject)this).SetValue(GridItemWidthProperty, (object)value);
		}
	}

	public object? Header
	{
		get
		{
			return ((BindableObject)this).GetValue(HeaderProperty);
		}
		set
		{
			((BindableObject)this).SetValue(HeaderProperty, value);
		}
	}

	public object? Footer
	{
		get
		{
			return ((BindableObject)this).GetValue(FooterProperty);
		}
		set
		{
			((BindableObject)this).SetValue(FooterProperty, value);
		}
	}

	public float HeaderHeight
	{
		get
		{
			return (float)((BindableObject)this).GetValue(HeaderHeightProperty);
		}
		set
		{
			((BindableObject)this).SetValue(HeaderHeightProperty, (object)value);
		}
	}

	public float FooterHeight
	{
		get
		{
			return (float)((BindableObject)this).GetValue(FooterHeightProperty);
		}
		set
		{
			((BindableObject)this).SetValue(FooterHeightProperty, (object)value);
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

	public SKColor HeaderBackgroundColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(HeaderBackgroundColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(HeaderBackgroundColorProperty, (object)value);
		}
	}

	public SKColor FooterBackgroundColor
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (SKColor)((BindableObject)this).GetValue(FooterBackgroundColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((BindableObject)this).SetValue(FooterBackgroundColorProperty, (object)value);
		}
	}

	public event EventHandler<CollectionSelectionChangedEventArgs>? SelectionChanged;

	protected override void RefreshItems()
	{
		_selectedItems.Clear();
		((BindableObject)this).SetValue(SelectedItemProperty, (object)null);
		_selectedIndex = -1;
		base.RefreshItems();
	}

	private void OnSelectionModeChanged()
	{
		switch (SelectionMode)
		{
		case SkiaSelectionMode.None:
			ClearSelection();
			break;
		case SkiaSelectionMode.Single:
			if (_selectedItems.Count > 1)
			{
				object obj = _selectedItems.FirstOrDefault();
				ClearSelection();
				if (obj != null)
				{
					SelectItem(obj);
				}
			}
			break;
		}
		Invalidate();
	}

	private void OnSelectedItemChanged(object? newValue)
	{
		if (SelectionMode != SkiaSelectionMode.None && !_isSelectingItem)
		{
			ClearSelection();
			if (newValue != null)
			{
				SelectItem(newValue);
			}
		}
	}

	private void OnHeaderChanged(object? newValue)
	{
		HeaderHeight = ((newValue != null) ? 44 : 0);
		Invalidate();
	}

	private void OnFooterChanged(object? newValue)
	{
		FooterHeight = ((newValue != null) ? 44 : 0);
		Invalidate();
	}

	private void SelectItem(object item)
	{
		if (SelectionMode == SkiaSelectionMode.None)
		{
			return;
		}
		List<object> previousSelection = _selectedItems.ToList();
		if (SelectionMode == SkiaSelectionMode.Single)
		{
			_selectedItems.Clear();
			_selectedItems.Add(item);
			((BindableObject)this).SetValue(SelectedItemProperty, item);
			for (int i = 0; i < base.ItemCount; i++)
			{
				if (GetItemAt(i) == item)
				{
					_selectedIndex = i;
					break;
				}
			}
		}
		else
		{
			if (_selectedItems.Contains(item))
			{
				_selectedItems.Remove(item);
				if (SelectedItem == item)
				{
					((BindableObject)this).SetValue(SelectedItemProperty, _selectedItems.FirstOrDefault());
				}
			}
			else
			{
				_selectedItems.Add(item);
				((BindableObject)this).SetValue(SelectedItemProperty, item);
			}
			_selectedIndex = ((SelectedItem != null) ? GetIndexOf(SelectedItem) : (-1));
		}
		this.SelectionChanged?.Invoke(this, new CollectionSelectionChangedEventArgs(previousSelection, _selectedItems.ToList()));
		Invalidate();
	}

	private int GetIndexOf(object item)
	{
		for (int i = 0; i < base.ItemCount; i++)
		{
			if (GetItemAt(i) == item)
			{
				return i;
			}
		}
		return -1;
	}

	private void ClearSelection()
	{
		List<object> list = _selectedItems.ToList();
		_selectedItems.Clear();
		((BindableObject)this).SetValue(SelectedItemProperty, (object)null);
		_selectedIndex = -1;
		if (list.Count > 0)
		{
			this.SelectionChanged?.Invoke(this, new CollectionSelectionChangedEventArgs(list, new List<object>()));
		}
	}

	protected override void OnItemTapped(int index, object item)
	{
		if (_isSelectingItem)
		{
			return;
		}
		_isSelectingItem = true;
		try
		{
			if (SelectionMode != SkiaSelectionMode.None)
			{
				SelectItem(item);
			}
			base.OnItemTapped(index, item);
		}
		finally
		{
			_isSelectingItem = false;
		}
	}

	protected override void DrawItem(SKCanvas canvas, object item, int index, SKRect bounds, SKPaint paint)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0271: Expected O, but got Unknown
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_0278: Unknown result type (might be due to invalid IL or missing references)
		//IL_0282: Unknown result type (might be due to invalid IL or missing references)
		//IL_028a: Expected O, but got Unknown
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_0320: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
		bool flag = _selectedItems.Contains(item);
		if (Orientation == ItemsLayoutOrientation.Vertical && SpanCount == 1)
		{
			paint.Color = new SKColor((byte)224, (byte)224, (byte)224);
			paint.Style = (SKPaintStyle)1;
			paint.StrokeWidth = 1f;
			canvas.DrawLine(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Bottom, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom, paint);
		}
		if (base.ItemViewCreator != null)
		{
			if (!_itemViewCache.TryGetValue(index, out SkiaView value) || value == null)
			{
				value = base.ItemViewCreator(item);
				if (value != null)
				{
					value.Parent = this;
					_itemViewCache[index] = value;
				}
			}
			if (value != null)
			{
				try
				{
					SKSize availableSize = default(SKSize);
					((SKSize)(ref availableSize))._002Ector(((SKRect)(ref bounds)).Width, float.MaxValue);
					SKSize val = value.Measure(availableSize);
					float num = ((SKSize)(ref val)).Height;
					if (float.IsNaN(num) || float.IsInfinity(num) || num > 10000f)
					{
						num = base.ItemHeight;
					}
					float num2 = Math.Max(num, base.ItemHeight);
					if (!_itemHeights.TryGetValue(index, out var value2) || Math.Abs(value2 - num2) > 1f)
					{
						_itemHeights[index] = num2;
						_heightsChangedDuringDraw = true;
					}
					SKRect val2 = default(SKRect);
					((SKRect)(ref val2))._002Ector(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Top + num2);
					value.Arrange(val2);
					value.Draw(canvas);
					if (flag)
					{
						paint.Color = SelectionColor;
						paint.Style = (SKPaintStyle)0;
						canvas.DrawRoundRect(val2, 12f, 12f, paint);
					}
					if (flag && SelectionMode == SkiaSelectionMode.Multiple)
					{
						DrawCheckmark(canvas, new SKRect(((SKRect)(ref val2)).Right - 32f, ((SKRect)(ref val2)).MidY - 8f, ((SKRect)(ref val2)).Right - 16f, ((SKRect)(ref val2)).MidY + 8f));
					}
					return;
				}
				catch (Exception ex)
				{
					Console.WriteLine("[SkiaCollectionView.DrawItem] EXCEPTION: " + ex.Message + "\n" + ex.StackTrace);
					return;
				}
			}
		}
		if (base.ItemRenderer != null && base.ItemRenderer(item, index, bounds, canvas, paint))
		{
			return;
		}
		paint.Color = SKColors.Black;
		paint.Style = (SKPaintStyle)0;
		SKFont val3 = new SKFont(SKTypeface.Default, 14f, 1f, 0f);
		try
		{
			SKPaint val4 = new SKPaint(val3)
			{
				Color = SKColors.Black,
				IsAntialias = true
			};
			try
			{
				string text = item?.ToString() ?? "";
				SKRect val5 = default(SKRect);
				val4.MeasureText(text, ref val5);
				float num3 = ((SKRect)(ref bounds)).Left + 16f;
				float num4 = ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val5)).MidY;
				canvas.DrawText(text, num3, num4, val4);
				if (flag && SelectionMode == SkiaSelectionMode.Multiple)
				{
					DrawCheckmark(canvas, new SKRect(((SKRect)(ref bounds)).Right - 32f, ((SKRect)(ref bounds)).MidY - 8f, ((SKRect)(ref bounds)).Right - 16f, ((SKRect)(ref bounds)).MidY + 8f));
				}
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
		}
	}

	private void DrawCheckmark(SKCanvas canvas, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Expected O, but got Unknown
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		SKPaint val = new SKPaint
		{
			Color = new SKColor((byte)33, (byte)150, (byte)243),
			Style = (SKPaintStyle)1,
			StrokeWidth = 2f,
			IsAntialias = true,
			StrokeCap = (SKStrokeCap)1
		};
		try
		{
			SKPath val2 = new SKPath();
			try
			{
				val2.MoveTo(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).MidY);
				val2.LineTo(((SKRect)(ref bounds)).MidX - 2f, ((SKRect)(ref bounds)).Bottom - 2f);
				val2.LineTo(((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Top + 2f);
				canvas.DrawPath(val2, val);
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

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected O, but got Unknown
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		_heightsChangedDuringDraw = false;
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
		if (Header != null && HeaderHeight > 0f)
		{
			SKRect bounds2 = default(SKRect);
			((SKRect)(ref bounds2))._002Ector(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Top + HeaderHeight);
			DrawHeader(canvas, bounds2);
		}
		if (Footer != null && FooterHeight > 0f)
		{
			SKRect bounds3 = default(SKRect);
			((SKRect)(ref bounds3))._002Ector(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Bottom - FooterHeight, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom);
			DrawFooter(canvas, bounds3);
		}
		SKRect bounds4 = default(SKRect);
		((SKRect)(ref bounds4))._002Ector(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top + HeaderHeight, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom - FooterHeight);
		if (base.ItemCount == 0)
		{
			DrawEmptyView(canvas, bounds4);
			return;
		}
		if (SpanCount > 1)
		{
			DrawGridItems(canvas, bounds4);
		}
		else
		{
			DrawListItems(canvas, bounds4);
		}
		if (_heightsChangedDuringDraw)
		{
			_heightsChangedDuringDraw = false;
			Invalidate();
		}
	}

	private void DrawListItems(SKCanvas canvas, SKRect bounds)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Expected O, but got Unknown
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		canvas.Save();
		canvas.ClipRect(bounds, (SKClipOperation)1, false);
		SKPaint val = new SKPaint
		{
			IsAntialias = true
		};
		try
		{
			float scrollOffset = GetScrollOffset();
			int num = 0;
			float num2 = 0f;
			for (int i = 0; i < base.ItemCount; i++)
			{
				float itemHeight = GetItemHeight(i);
				if (num2 + itemHeight > scrollOffset)
				{
					num = i;
					break;
				}
				num2 += itemHeight + base.ItemSpacing;
			}
			float num3 = ((SKRect)(ref bounds)).Top + GetItemOffset(num) - scrollOffset;
			SKRect bounds2 = default(SKRect);
			for (int j = num; j < base.ItemCount; j++)
			{
				float itemHeight2 = GetItemHeight(j);
				((SKRect)(ref bounds2))._002Ector(((SKRect)(ref bounds)).Left, num3, ((SKRect)(ref bounds)).Right - 8f, num3 + itemHeight2);
				if (((SKRect)(ref bounds2)).Top > ((SKRect)(ref bounds)).Bottom)
				{
					break;
				}
				if (((SKRect)(ref bounds2)).Bottom >= ((SKRect)(ref bounds)).Top)
				{
					object itemAt = GetItemAt(j);
					if (itemAt != null)
					{
						DrawItem(canvas, itemAt, j, bounds2, val);
					}
				}
				num3 += itemHeight2 + base.ItemSpacing;
			}
			canvas.Restore();
			float totalContentHeight = base.TotalContentHeight;
			if (totalContentHeight > ((SKRect)(ref bounds)).Height)
			{
				DrawScrollBarInternal(canvas, bounds, scrollOffset, totalContentHeight);
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void DrawGridItems(SKCanvas canvas, SKRect bounds)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Expected O, but got Unknown
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Expected O, but got Unknown
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Expected O, but got Unknown
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		canvas.Save();
		canvas.ClipRect(bounds, (SKClipOperation)1, false);
		SKPaint val = new SKPaint
		{
			IsAntialias = true
		};
		try
		{
			float num = (((SKRect)(ref bounds)).Width - 8f) / (float)SpanCount;
			float itemHeight = base.ItemHeight;
			int num2 = (int)Math.Ceiling((double)base.ItemCount / (double)SpanCount);
			float num3 = (float)num2 * (itemHeight + base.ItemSpacing) - base.ItemSpacing;
			float scrollOffset = GetScrollOffset();
			int num4 = Math.Max(0, (int)(scrollOffset / (itemHeight + base.ItemSpacing)));
			int num5 = Math.Min(num2 - 1, (int)((scrollOffset + ((SKRect)(ref bounds)).Height) / (itemHeight + base.ItemSpacing)) + 1);
			SKRect val2 = default(SKRect);
			for (int i = num4; i <= num5; i++)
			{
				float num6 = ((SKRect)(ref bounds)).Top + (float)i * (itemHeight + base.ItemSpacing) - scrollOffset;
				for (int j = 0; j < SpanCount; j++)
				{
					int num7 = i * SpanCount + j;
					if (num7 >= base.ItemCount)
					{
						break;
					}
					float num8 = ((SKRect)(ref bounds)).Left + (float)j * num;
					((SKRect)(ref val2))._002Ector(num8 + 2f, num6, num8 + num - 2f, num6 + itemHeight);
					if (((SKRect)(ref val2)).Bottom < ((SKRect)(ref bounds)).Top || ((SKRect)(ref val2)).Top > ((SKRect)(ref bounds)).Bottom)
					{
						continue;
					}
					object itemAt = GetItemAt(num7);
					if (itemAt != null)
					{
						SKPaint val3 = new SKPaint
						{
							Color = (SKColor)(_selectedItems.Contains(itemAt) ? SelectionColor : new SKColor((byte)250, (byte)250, (byte)250)),
							Style = (SKPaintStyle)0
						};
						try
						{
							canvas.DrawRoundRect(new SKRoundRect(val2, 4f), val3);
							DrawItem(canvas, itemAt, num7, val2, val);
						}
						finally
						{
							((IDisposable)val3)?.Dispose();
						}
					}
				}
			}
			canvas.Restore();
			if (num3 > ((SKRect)(ref bounds)).Height)
			{
				DrawScrollBarInternal(canvas, bounds, scrollOffset, num3);
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void DrawScrollBarInternal(SKCanvas canvas, SKRect bounds, float scrollOffset, float totalHeight)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Expected O, but got Unknown
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Expected O, but got Unknown
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Expected O, but got Unknown
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Expected O, but got Unknown
		float num = 6f;
		float num2 = 2f;
		SKRect val = default(SKRect);
		((SKRect)(ref val))._002Ector(((SKRect)(ref bounds)).Right - num - num2, ((SKRect)(ref bounds)).Top + num2, ((SKRect)(ref bounds)).Right - num2, ((SKRect)(ref bounds)).Bottom - num2);
		SKPaint val2 = new SKPaint
		{
			Color = new SKColor((byte)0, (byte)0, (byte)0, (byte)20),
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRoundRect(new SKRoundRect(val, 3f), val2);
			float num3 = Math.Max(0f, totalHeight - ((SKRect)(ref bounds)).Height);
			float num4 = ((SKRect)(ref bounds)).Height / totalHeight;
			float height = ((SKRect)(ref val)).Height;
			float num5 = Math.Max(30f, height * num4);
			float num6 = ((num3 > 0f) ? (scrollOffset / num3) : 0f);
			float num7 = ((SKRect)(ref val)).Top + (height - num5) * num6;
			SKRect val3 = new SKRect(((SKRect)(ref val)).Left, num7, ((SKRect)(ref val)).Right, num7 + num5);
			SKPaint val4 = new SKPaint
			{
				Color = new SKColor((byte)100, (byte)100, (byte)100, (byte)180),
				Style = (SKPaintStyle)0,
				IsAntialias = true
			};
			try
			{
				canvas.DrawRoundRect(new SKRoundRect(val3, 3f), val4);
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

	private float GetScrollOffset()
	{
		return _scrollOffset;
	}

	private void DrawHeader(SKCanvas canvas, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Expected O, but got Unknown
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = HeaderBackgroundColor,
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRect(bounds, val);
			string text = Header.ToString() ?? "";
			if (!string.IsNullOrEmpty(text))
			{
				SKFont val2 = new SKFont(SKTypeface.Default, 16f, 1f, 0f);
				try
				{
					SKPaint val3 = new SKPaint(val2)
					{
						Color = SKColors.Black,
						IsAntialias = true
					};
					try
					{
						SKRect val4 = default(SKRect);
						val3.MeasureText(text, ref val4);
						float num = ((SKRect)(ref bounds)).Left + 16f;
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
			SKPaint val5 = new SKPaint
			{
				Color = new SKColor((byte)224, (byte)224, (byte)224),
				Style = (SKPaintStyle)1,
				StrokeWidth = 1f
			};
			try
			{
				canvas.DrawLine(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Bottom, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Bottom, val5);
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

	private void DrawFooter(SKCanvas canvas, SKRect bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Expected O, but got Unknown
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Expected O, but got Unknown
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = FooterBackgroundColor,
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRect(bounds, val);
			SKPaint val2 = new SKPaint
			{
				Color = new SKColor((byte)224, (byte)224, (byte)224),
				Style = (SKPaintStyle)1,
				StrokeWidth = 1f
			};
			try
			{
				canvas.DrawLine(((SKRect)(ref bounds)).Left, ((SKRect)(ref bounds)).Top, ((SKRect)(ref bounds)).Right, ((SKRect)(ref bounds)).Top, val2);
				string text = Footer.ToString() ?? "";
				if (string.IsNullOrEmpty(text))
				{
					return;
				}
				SKFont val3 = new SKFont(SKTypeface.Default, 14f, 1f, 0f);
				try
				{
					SKPaint val4 = new SKPaint(val3)
					{
						Color = new SKColor((byte)128, (byte)128, (byte)128),
						IsAntialias = true
					};
					try
					{
						SKRect val5 = default(SKRect);
						val4.MeasureText(text, ref val5);
						float num = ((SKRect)(ref bounds)).MidX - ((SKRect)(ref val5)).MidX;
						float num2 = ((SKRect)(ref bounds)).MidY - ((SKRect)(ref val5)).MidY;
						canvas.DrawText(text, num, num2, val4);
					}
					finally
					{
						((IDisposable)val4)?.Dispose();
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
}
