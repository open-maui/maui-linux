// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Microsoft.Maui;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Grid layout that arranges children in rows and columns.
/// </summary>
public class SkiaGrid : SkiaLayoutView
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for RowSpacing.
    /// </summary>
    public static readonly BindableProperty RowSpacingProperty =
        BindableProperty.Create(
            nameof(RowSpacing),
            typeof(float),
            typeof(SkiaGrid),
            0f,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaGrid)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for ColumnSpacing.
    /// </summary>
    public static readonly BindableProperty ColumnSpacingProperty =
        BindableProperty.Create(
            nameof(ColumnSpacing),
            typeof(float),
            typeof(SkiaGrid),
            0f,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaGrid)b).InvalidateMeasure());

    #endregion

    private readonly List<GridLength> _rowDefinitions = new();
    private readonly List<GridLength> _columnDefinitions = new();
    private readonly Dictionary<SkiaView, GridPosition> _childPositions = new();

    private float[] _rowHeights = Array.Empty<float>();
    private float[] _columnWidths = Array.Empty<float>();

    /// <summary>
    /// Gets the row definitions.
    /// </summary>
    public IList<GridLength> RowDefinitions => _rowDefinitions;

    /// <summary>
    /// Gets the column definitions.
    /// </summary>
    public IList<GridLength> ColumnDefinitions => _columnDefinitions;

    /// <summary>
    /// Spacing between rows.
    /// </summary>
    public float RowSpacing
    {
        get => (float)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    /// <summary>
    /// Spacing between columns.
    /// </summary>
    public float ColumnSpacing
    {
        get => (float)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>
    /// Adds a child at the specified grid position.
    /// </summary>
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

    /// <summary>
    /// Gets the grid position of a child.
    /// </summary>
    public GridPosition GetPosition(SkiaView child)
    {
        return _childPositions.TryGetValue(child, out var pos) ? pos : new GridPosition(0, 0, 1, 1);
    }

    /// <summary>
    /// Sets the grid position of a child.
    /// </summary>
    public void SetPosition(SkiaView child, int row, int column, int rowSpan = 1, int columnSpan = 1)
    {
        _childPositions[child] = new GridPosition(row, column, rowSpan, columnSpan);
        InvalidateMeasure();
        Invalidate();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var contentWidth = (float)(availableSize.Width - Padding.Left - Padding.Right);
        var contentHeight = (float)(availableSize.Height - Padding.Top - Padding.Bottom);

        // Handle NaN/Infinity
        if (float.IsNaN(contentWidth) || float.IsInfinity(contentWidth)) contentWidth = 800;
        if (float.IsNaN(contentHeight) || float.IsInfinity(contentHeight)) contentHeight = float.PositiveInfinity;

        var rowCount = Math.Max(1, _rowDefinitions.Count > 0 ? _rowDefinitions.Count : GetMaxRow() + 1);
        var columnCount = Math.Max(1, _columnDefinitions.Count > 0 ? _columnDefinitions.Count : GetMaxColumn() + 1);

        // First pass: measure children in Auto columns to get natural widths
        var columnNaturalWidths = new float[columnCount];
        var rowNaturalHeights = new float[rowCount];

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            var pos = GetPosition(child);

            // For Auto columns, measure with infinite width to get natural size
            var def = pos.Column < _columnDefinitions.Count ? _columnDefinitions[pos.Column] : GridLength.Star;
            if (def.IsAuto && pos.ColumnSpan == 1)
            {
                var childSize = child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var childWidth = double.IsNaN(childSize.Width) ? 0f : (float)childSize.Width;
                columnNaturalWidths[pos.Column] = Math.Max(columnNaturalWidths[pos.Column], childWidth);
            }
        }

        // Calculate column widths - handle Auto, Absolute, and Star
        _columnWidths = CalculateSizesWithAuto(_columnDefinitions, contentWidth, ColumnSpacing, columnCount, columnNaturalWidths);

        // Second pass: measure all children with calculated column widths
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            var pos = GetPosition(child);
            var cellWidth = GetCellWidth(pos.Column, pos.ColumnSpan);

            // Give infinite height for initial measure
            var childSize = child.Measure(new Size(cellWidth, double.PositiveInfinity));

            // Track max height for each row
            // Cap infinite/very large heights - child returning infinity means it doesn't have a natural height
            var childHeight = (float)childSize.Height;
            if (float.IsNaN(childHeight) || float.IsInfinity(childHeight) || childHeight > 100000)
            {
                // Use a default minimum - will be expanded by Star sizing if finite height is available
                childHeight = 44; // Standard row height
            }
            if (pos.RowSpan == 1)
            {
                rowNaturalHeights[pos.Row] = Math.Max(rowNaturalHeights[pos.Row], childHeight);
            }
        }

        // Calculate row heights - use natural heights when available height is infinite or very large
        // (Some layouts pass float.MaxValue instead of PositiveInfinity)
        if (float.IsInfinity(contentHeight) || contentHeight > 100000)
        {
            _rowHeights = rowNaturalHeights;
        }
        else
        {
            _rowHeights = CalculateSizesWithAuto(_rowDefinitions, contentHeight, RowSpacing, rowCount, rowNaturalHeights);
        }

        // Third pass: re-measure children with actual cell sizes
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            var pos = GetPosition(child);
            var cellWidth = GetCellWidth(pos.Column, pos.ColumnSpan);
            var cellHeight = GetCellHeight(pos.Row, pos.RowSpan);

            child.Measure(new Size(cellWidth, cellHeight));
        }

        // Calculate total size
        var totalWidth = _columnWidths.Sum() + Math.Max(0, columnCount - 1) * ColumnSpacing;
        var totalHeight = _rowHeights.Sum() + Math.Max(0, rowCount - 1) * RowSpacing;

        return new Size(
            totalWidth + Padding.Left + Padding.Right,
            totalHeight + Padding.Top + Padding.Bottom);
    }

    private int GetMaxRow()
    {
        int maxRow = 0;
        foreach (var pos in _childPositions.Values)
        {
            maxRow = Math.Max(maxRow, pos.Row + pos.RowSpan - 1);
        }
        return maxRow;
    }

    private int GetMaxColumn()
    {
        int maxCol = 0;
        foreach (var pos in _childPositions.Values)
        {
            maxCol = Math.Max(maxCol, pos.Column + pos.ColumnSpan - 1);
        }
        return maxCol;
    }

    private float[] CalculateSizesWithAuto(List<GridLength> definitions, float available, float spacing, int count, float[] naturalSizes)
    {
        if (count == 0) return new float[] { available };

        var sizes = new float[count];
        var totalSpacing = Math.Max(0, count - 1) * spacing;
        var remainingSpace = available - totalSpacing;

        // First pass: absolute and auto sizes
        float starTotal = 0;
        for (int i = 0; i < count; i++)
        {
            var def = i < definitions.Count ? definitions[i] : GridLength.Star;

            if (def.IsAbsolute)
            {
                sizes[i] = def.Value;
                remainingSpace -= def.Value;
            }
            else if (def.IsAuto)
            {
                // Use natural size from measured children
                sizes[i] = naturalSizes[i];
                remainingSpace -= sizes[i];
            }
            else if (def.IsStar)
            {
                starTotal += def.Value;
            }
        }

        // Second pass: star sizes (distribute remaining space)
        if (starTotal > 0 && remainingSpace > 0)
        {
            for (int i = 0; i < count; i++)
            {
                var def = i < definitions.Count ? definitions[i] : GridLength.Star;
                if (def.IsStar)
                {
                    sizes[i] = (def.Value / starTotal) * remainingSpace;
                }
            }
        }

        return sizes;
    }

    private float GetCellWidth(int column, int span)
    {
        float width = 0;
        for (int i = column; i < Math.Min(column + span, _columnWidths.Length); i++)
        {
            width += _columnWidths[i];
            if (i > column) width += ColumnSpacing;
        }
        return width;
    }

    private float GetCellHeight(int row, int span)
    {
        float height = 0;
        for (int i = row; i < Math.Min(row + span, _rowHeights.Length); i++)
        {
            height += _rowHeights[i];
            if (i > row) height += RowSpacing;
        }
        return height;
    }

    private float GetColumnOffset(int column)
    {
        float offset = 0;
        for (int i = 0; i < Math.Min(column, _columnWidths.Length); i++)
        {
            offset += _columnWidths[i] + ColumnSpacing;
        }
        return offset;
    }

    private float GetRowOffset(int row)
    {
        float offset = 0;
        for (int i = 0; i < Math.Min(row, _rowHeights.Length); i++)
        {
            offset += _rowHeights[i] + RowSpacing;
        }
        return offset;
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        try
        {
            var content = GetContentBounds(new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom));

            // Recalculate row heights for arrange bounds if they differ from measurement
            // This ensures Star rows expand to fill available space
            var rowCount = _rowHeights.Length > 0 ? _rowHeights.Length : 1;
            var columnCount = _columnWidths.Length > 0 ? _columnWidths.Length : 1;
            var arrangeRowHeights = _rowHeights;

        // If we have arrange height and rows need recalculating
        if (content.Height > 0 && !float.IsInfinity(content.Height))
        {
            var measuredRowsTotal = _rowHeights.Sum() + Math.Max(0, rowCount - 1) * RowSpacing;

            // If arrange height is larger than measured, redistribute to Star rows
            if (content.Height > measuredRowsTotal + 1)
            {
                arrangeRowHeights = new float[rowCount];
                var extraHeight = content.Height - measuredRowsTotal;

                // Count Star rows (implicit rows without definitions are Star)
                float totalStarWeight = 0;
                for (int i = 0; i < rowCount; i++)
                {
                    var def = i < _rowDefinitions.Count ? _rowDefinitions[i] : GridLength.Star;
                    if (def.IsStar) totalStarWeight += def.Value;
                }

                // Distribute extra height to Star rows
                for (int i = 0; i < rowCount; i++)
                {
                    var def = i < _rowDefinitions.Count ? _rowDefinitions[i] : GridLength.Star;
                    arrangeRowHeights[i] = i < _rowHeights.Length ? _rowHeights[i] : 0;

                    if (def.IsStar && totalStarWeight > 0)
                    {
                        arrangeRowHeights[i] += extraHeight * (def.Value / totalStarWeight);
                    }
                }
            }
            else
            {
                arrangeRowHeights = _rowHeights;
            }
        }

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            var pos = GetPosition(child);

            var x = content.Left + GetColumnOffset(pos.Column);

            // Calculate y using arrange row heights
            float y = content.Top;
            for (int i = 0; i < Math.Min(pos.Row, arrangeRowHeights.Length); i++)
            {
                y += arrangeRowHeights[i] + RowSpacing;
            }

            var width = GetCellWidth(pos.Column, pos.ColumnSpan);

            // Calculate height using arrange row heights
            float height = 0;
            for (int i = pos.Row; i < Math.Min(pos.Row + pos.RowSpan, arrangeRowHeights.Length); i++)
            {
                height += arrangeRowHeights[i];
                if (i > pos.Row) height += RowSpacing;
            }

            // Clamp infinite dimensions
            if (float.IsInfinity(width) || float.IsNaN(width))
                width = content.Width;
            if (float.IsInfinity(height) || float.IsNaN(height) || height <= 0)
                height = content.Height;

            // Apply child's margin
            var margin = child.Margin;
            var cellX = x + (float)margin.Left;
            var cellY = y + (float)margin.Top;
            var cellWidth = width - (float)margin.Left - (float)margin.Right;
            var cellHeight = height - (float)margin.Top - (float)margin.Bottom;

            // Get child's desired size
            var childDesiredSize = child.Measure(new Size(cellWidth, cellHeight));
            var childWidth = (float)childDesiredSize.Width;
            var childHeight = (float)childDesiredSize.Height;

            // Read alignment from the MAUI virtual view (authoritative source).
            // Falls back to SkiaView.HorizontalOptions/VerticalOptions if no MauiView is set.
            // Note: IView.HorizontalLayoutAlignment uses OpenMaui LayoutAlignment (Fill=0, Start=1, Center=2, End=3).
            // SkiaView.HorizontalOptions.Alignment uses MAUI Controls LayoutAlignment (Start=0, Center=1, End=2, Fill=3).
            // We normalize both to OpenMaui LayoutAlignment via MapAlignment.
            var hAlign = child.MauiView is IView hv
                ? (LayoutAlignment)(int)hv.HorizontalLayoutAlignment
                : LayoutAlignmentHelper.MapFromMaui(child.HorizontalOptions);
            var vAlign = child.MauiView is IView vv
                ? (LayoutAlignment)(int)vv.VerticalLayoutAlignment
                : LayoutAlignmentHelper.MapFromMaui(child.VerticalOptions);

            // Apply HorizontalOptions
            float finalX = cellX;
            float finalWidth = cellWidth;
            if (hAlign != LayoutAlignment.Fill && childWidth < cellWidth && childWidth > 0)
            {
                finalWidth = childWidth;
                if (hAlign == LayoutAlignment.Center)
                    finalX = cellX + (cellWidth - childWidth) / 2;
                else if (hAlign == LayoutAlignment.End)
                    finalX = cellX + cellWidth - childWidth;
            }

            // Apply VerticalOptions
            float finalY = cellY;
            float finalHeight = cellHeight;
            if (vAlign != LayoutAlignment.Fill && childHeight < cellHeight && childHeight > 0)
            {
                finalHeight = childHeight;
                if (vAlign == LayoutAlignment.Center)
                    finalY = cellY + (cellHeight - childHeight) / 2;
                else if (vAlign == LayoutAlignment.End)
                    finalY = cellY + cellHeight - childHeight;
            }

            child.Arrange(new Rect(finalX, finalY, finalWidth, finalHeight));
        }
        return bounds;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaGrid", $"EXCEPTION in ArrangeOverride: {ex.GetType().Name}: {ex.Message}", ex);
            DiagnosticLog.Error("SkiaGrid", $"Bounds: {bounds}, RowHeights: {_rowHeights.Length}, RowDefs: {_rowDefinitions.Count}, Children: {Children.Count}");
            DiagnosticLog.Error("SkiaGrid", $"Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}

/// <summary>
/// Grid position information.
/// </summary>
public readonly struct GridPosition
{
    public int Row { get; }
    public int Column { get; }
    public int RowSpan { get; }
    public int ColumnSpan { get; }

    public GridPosition(int row, int column, int rowSpan = 1, int columnSpan = 1)
    {
        Row = row;
        Column = column;
        RowSpan = Math.Max(1, rowSpan);
        ColumnSpan = Math.Max(1, columnSpan);
    }
}

/// <summary>
/// Grid length specification.
/// </summary>
public readonly struct GridLength
{
    public float Value { get; }
    public GridUnitType GridUnitType { get; }

    public bool IsAbsolute => GridUnitType == GridUnitType.Absolute;
    public bool IsAuto => GridUnitType == GridUnitType.Auto;
    public bool IsStar => GridUnitType == GridUnitType.Star;

    public static GridLength Auto => new(1, GridUnitType.Auto);
    public static GridLength Star => new(1, GridUnitType.Star);

    public GridLength(float value, GridUnitType unitType = GridUnitType.Absolute)
    {
        Value = value;
        GridUnitType = unitType;
    }

    public static GridLength FromAbsolute(float value) => new(value, GridUnitType.Absolute);
    public static GridLength FromStar(float value = 1) => new(value, GridUnitType.Star);
}

/// <summary>
/// Grid unit type options.
/// </summary>
public enum GridUnitType
{
    Absolute,
    Star,
    Auto
}
