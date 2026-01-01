using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaGrid : SkiaLayoutView
{
	public static readonly BindableProperty RowSpacingProperty = BindableProperty.Create("RowSpacing", typeof(float), typeof(SkiaGrid), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaGrid)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static readonly BindableProperty ColumnSpacingProperty = BindableProperty.Create("ColumnSpacing", typeof(float), typeof(SkiaGrid), (object)0f, (BindingMode)2, (ValidateValueDelegate)null, (BindingPropertyChangedDelegate)delegate(BindableObject b, object o, object n)
	{
		((SkiaGrid)(object)b).InvalidateMeasure();
	}, (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	private readonly List<GridLength> _rowDefinitions = new List<GridLength>();

	private readonly List<GridLength> _columnDefinitions = new List<GridLength>();

	private readonly Dictionary<SkiaView, GridPosition> _childPositions = new Dictionary<SkiaView, GridPosition>();

	private float[] _rowHeights = Array.Empty<float>();

	private float[] _columnWidths = Array.Empty<float>();

	public IList<GridLength> RowDefinitions => _rowDefinitions;

	public IList<GridLength> ColumnDefinitions => _columnDefinitions;

	public float RowSpacing
	{
		get
		{
			return (float)((BindableObject)this).GetValue(RowSpacingProperty);
		}
		set
		{
			((BindableObject)this).SetValue(RowSpacingProperty, (object)value);
		}
	}

	public float ColumnSpacing
	{
		get
		{
			return (float)((BindableObject)this).GetValue(ColumnSpacingProperty);
		}
		set
		{
			((BindableObject)this).SetValue(ColumnSpacingProperty, (object)value);
		}
	}

	public void AddChild(SkiaView child, int row, int column, int rowSpan = 1, int columnSpan = 1)
	{
		base.AddChild(child);
		_childPositions[child] = new GridPosition(row, column, rowSpan, columnSpan);
	}

	public override void RemoveChild(SkiaView child)
	{
		base.RemoveChild(child);
		_childPositions.Remove(child);
	}

	public GridPosition GetPosition(SkiaView child)
	{
		if (!_childPositions.TryGetValue(child, out var value))
		{
			return new GridPosition(0, 0);
		}
		return value;
	}

	public void SetPosition(SkiaView child, int row, int column, int rowSpan = 1, int columnSpan = 1)
	{
		_childPositions[child] = new GridPosition(row, column, rowSpan, columnSpan);
		InvalidateMeasure();
		Invalidate();
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_022f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_0333: Unknown result type (might be due to invalid IL or missing references)
		//IL_0338: Unknown result type (might be due to invalid IL or missing references)
		//IL_0392: Unknown result type (might be due to invalid IL or missing references)
		//IL_0397: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d3: Unknown result type (might be due to invalid IL or missing references)
		float width = ((SKSize)(ref availableSize)).Width;
		SKRect padding = base.Padding;
		float num = width - ((SKRect)(ref padding)).Left;
		padding = base.Padding;
		float num2 = num - ((SKRect)(ref padding)).Right;
		float height = ((SKSize)(ref availableSize)).Height;
		padding = base.Padding;
		float num3 = height - ((SKRect)(ref padding)).Top;
		padding = base.Padding;
		float num4 = num3 - ((SKRect)(ref padding)).Bottom;
		if (float.IsNaN(num2) || float.IsInfinity(num2))
		{
			num2 = 800f;
		}
		if (float.IsNaN(num4) || float.IsInfinity(num4))
		{
			num4 = float.PositiveInfinity;
		}
		int num5 = Math.Max(1, (_rowDefinitions.Count > 0) ? _rowDefinitions.Count : (GetMaxRow() + 1));
		int num6 = Math.Max(1, (_columnDefinitions.Count > 0) ? _columnDefinitions.Count : (GetMaxColumn() + 1));
		float[] array = new float[num6];
		float[] array2 = new float[num5];
		foreach (SkiaView child in base.Children)
		{
			if (child.IsVisible)
			{
				GridPosition position = GetPosition(child);
				if (((position.Column < _columnDefinitions.Count) ? _columnDefinitions[position.Column] : GridLength.Star).IsAuto && position.ColumnSpan == 1)
				{
					SKSize val = child.Measure(new SKSize(float.PositiveInfinity, float.PositiveInfinity));
					float val2 = (float.IsNaN(((SKSize)(ref val)).Width) ? 0f : ((SKSize)(ref val)).Width);
					array[position.Column] = Math.Max(array[position.Column], val2);
				}
			}
		}
		_columnWidths = CalculateSizesWithAuto(_columnDefinitions, num2, ColumnSpacing, num6, array);
		foreach (SkiaView child2 in base.Children)
		{
			if (child2.IsVisible)
			{
				GridPosition position2 = GetPosition(child2);
				float cellWidth = GetCellWidth(position2.Column, position2.ColumnSpan);
				SKSize val3 = child2.Measure(new SKSize(cellWidth, float.PositiveInfinity));
				float num7 = ((SKSize)(ref val3)).Height;
				if (float.IsNaN(num7) || float.IsInfinity(num7) || num7 > 100000f)
				{
					num7 = 44f;
				}
				if (position2.RowSpan == 1)
				{
					array2[position2.Row] = Math.Max(array2[position2.Row], num7);
				}
			}
		}
		if (float.IsInfinity(num4) || num4 > 100000f)
		{
			_rowHeights = array2;
		}
		else
		{
			_rowHeights = CalculateSizesWithAuto(_rowDefinitions, num4, RowSpacing, num5, array2);
		}
		foreach (SkiaView child3 in base.Children)
		{
			if (child3.IsVisible)
			{
				GridPosition position3 = GetPosition(child3);
				float cellWidth2 = GetCellWidth(position3.Column, position3.ColumnSpan);
				float cellHeight = GetCellHeight(position3.Row, position3.RowSpan);
				child3.Measure(new SKSize(cellWidth2, cellHeight));
			}
		}
		float num8 = _columnWidths.Sum() + (float)Math.Max(0, num6 - 1) * ColumnSpacing;
		float num9 = _rowHeights.Sum() + (float)Math.Max(0, num5 - 1) * RowSpacing;
		padding = base.Padding;
		float num10 = num8 + ((SKRect)(ref padding)).Left;
		padding = base.Padding;
		float num11 = num10 + ((SKRect)(ref padding)).Right;
		padding = base.Padding;
		float num12 = num9 + ((SKRect)(ref padding)).Top;
		padding = base.Padding;
		return new SKSize(num11, num12 + ((SKRect)(ref padding)).Bottom);
	}

	private int GetMaxRow()
	{
		int num = 0;
		foreach (GridPosition value in _childPositions.Values)
		{
			num = Math.Max(num, value.Row + value.RowSpan - 1);
		}
		return num;
	}

	private int GetMaxColumn()
	{
		int num = 0;
		foreach (GridPosition value in _childPositions.Values)
		{
			num = Math.Max(num, value.Column + value.ColumnSpan - 1);
		}
		return num;
	}

	private float[] CalculateSizesWithAuto(List<GridLength> definitions, float available, float spacing, int count, float[] naturalSizes)
	{
		if (count == 0)
		{
			return new float[1] { available };
		}
		float[] array = new float[count];
		float num = (float)Math.Max(0, count - 1) * spacing;
		float num2 = available - num;
		float num3 = 0f;
		for (int i = 0; i < count; i++)
		{
			GridLength gridLength = ((i < definitions.Count) ? definitions[i] : GridLength.Star);
			if (gridLength.IsAbsolute)
			{
				array[i] = gridLength.Value;
				num2 -= gridLength.Value;
			}
			else if (gridLength.IsAuto)
			{
				array[i] = naturalSizes[i];
				num2 -= array[i];
			}
			else if (gridLength.IsStar)
			{
				num3 += gridLength.Value;
			}
		}
		if (num3 > 0f && num2 > 0f)
		{
			for (int j = 0; j < count; j++)
			{
				GridLength gridLength2 = ((j < definitions.Count) ? definitions[j] : GridLength.Star);
				if (gridLength2.IsStar)
				{
					array[j] = gridLength2.Value / num3 * num2;
				}
			}
		}
		return array;
	}

	private float GetCellWidth(int column, int span)
	{
		float num = 0f;
		for (int i = column; i < Math.Min(column + span, _columnWidths.Length); i++)
		{
			num += _columnWidths[i];
			if (i > column)
			{
				num += ColumnSpacing;
			}
		}
		return num;
	}

	private float GetCellHeight(int row, int span)
	{
		float num = 0f;
		for (int i = row; i < Math.Min(row + span, _rowHeights.Length); i++)
		{
			num += _rowHeights[i];
			if (i > row)
			{
				num += RowSpacing;
			}
		}
		return num;
	}

	private float GetColumnOffset(int column)
	{
		float num = 0f;
		for (int i = 0; i < Math.Min(column, _columnWidths.Length); i++)
		{
			num += _columnWidths[i] + ColumnSpacing;
		}
		return num;
	}

	private float GetRowOffset(int row)
	{
		float num = 0f;
		for (int i = 0; i < Math.Min(row, _rowHeights.Length); i++)
		{
			num += _rowHeights[i] + RowSpacing;
		}
		return num;
	}

	protected override SKRect ArrangeOverride(SKRect bounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0349: Unknown result type (might be due to invalid IL or missing references)
		//IL_0300: Unknown result type (might be due to invalid IL or missing references)
		//IL_0301: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_029d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02df: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			SKRect contentBounds = GetContentBounds(bounds);
			int num = ((_rowHeights.Length == 0) ? 1 : _rowHeights.Length);
			if (_columnWidths.Length != 0)
			{
				_ = _columnWidths.Length;
			}
			float[] array = _rowHeights;
			if (((SKRect)(ref contentBounds)).Height > 0f && !float.IsInfinity(((SKRect)(ref contentBounds)).Height))
			{
				float num2 = _rowHeights.Sum() + (float)Math.Max(0, num - 1) * RowSpacing;
				if (((SKRect)(ref contentBounds)).Height > num2 + 1f)
				{
					array = new float[num];
					float num3 = ((SKRect)(ref contentBounds)).Height - num2;
					float num4 = 0f;
					for (int i = 0; i < num; i++)
					{
						GridLength gridLength = ((i < _rowDefinitions.Count) ? _rowDefinitions[i] : GridLength.Star);
						if (gridLength.IsStar)
						{
							num4 += gridLength.Value;
						}
					}
					for (int j = 0; j < num; j++)
					{
						GridLength gridLength2 = ((j < _rowDefinitions.Count) ? _rowDefinitions[j] : GridLength.Star);
						array[j] = ((j < _rowHeights.Length) ? _rowHeights[j] : 0f);
						if (gridLength2.IsStar && num4 > 0f)
						{
							array[j] += num3 * (gridLength2.Value / num4);
						}
					}
				}
				else
				{
					array = _rowHeights;
				}
			}
			SKRect bounds2 = default(SKRect);
			foreach (SkiaView child in base.Children)
			{
				if (!child.IsVisible)
				{
					continue;
				}
				GridPosition position = GetPosition(child);
				float num5 = ((SKRect)(ref contentBounds)).Left + GetColumnOffset(position.Column);
				float num6 = ((SKRect)(ref contentBounds)).Top;
				for (int k = 0; k < Math.Min(position.Row, array.Length); k++)
				{
					num6 += array[k] + RowSpacing;
				}
				float num7 = GetCellWidth(position.Column, position.ColumnSpan);
				float num8 = 0f;
				for (int l = position.Row; l < Math.Min(position.Row + position.RowSpan, array.Length); l++)
				{
					num8 += array[l];
					if (l > position.Row)
					{
						num8 += RowSpacing;
					}
				}
				if (float.IsInfinity(num7) || float.IsNaN(num7))
				{
					num7 = ((SKRect)(ref contentBounds)).Width;
				}
				if (float.IsInfinity(num8) || float.IsNaN(num8) || num8 <= 0f)
				{
					num8 = ((SKRect)(ref contentBounds)).Height;
				}
				Thickness margin = child.Margin;
				((SKRect)(ref bounds2))._002Ector(num5 + (float)((Thickness)(ref margin)).Left, num6 + (float)((Thickness)(ref margin)).Top, num5 + num7 - (float)((Thickness)(ref margin)).Right, num6 + num8 - (float)((Thickness)(ref margin)).Bottom);
				child.Arrange(bounds2);
			}
			return bounds;
		}
		catch (Exception ex)
		{
			Console.WriteLine("[SkiaGrid] EXCEPTION in ArrangeOverride: " + ex.GetType().Name + ": " + ex.Message);
			Console.WriteLine($"[SkiaGrid] Bounds: {bounds}, RowHeights: {_rowHeights.Length}, RowDefs: {_rowDefinitions.Count}, Children: {base.Children.Count}");
			Console.WriteLine("[SkiaGrid] Stack trace: " + ex.StackTrace);
			throw;
		}
	}
}
