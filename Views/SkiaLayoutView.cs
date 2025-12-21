// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Base class for layout containers that can arrange child views.
/// </summary>
public abstract class SkiaLayoutView : SkiaView
{
    private readonly List<SkiaView> _children = new();

    /// <summary>
    /// Gets the children of this layout.
    /// </summary>
    public new IReadOnlyList<SkiaView> Children => _children;

    /// <summary>
    /// Spacing between children.
    /// </summary>
    public float Spacing { get; set; } = 0;

    /// <summary>
    /// Padding around the content.
    /// </summary>
    public SKRect Padding { get; set; } = new SKRect(0, 0, 0, 0);

    /// <summary>
    /// Gets or sets whether child views are clipped to the bounds.
    /// </summary>
    public bool ClipToBounds { get; set; } = false;

    /// <summary>
    /// Called when binding context changes. Propagates to layout children.
    /// </summary>
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        // Propagate binding context to layout children
        foreach (var child in _children)
        {
            SetInheritedBindingContext(child, BindingContext);
        }
    }

    /// <summary>
    /// Adds a child view.
    /// </summary>
    public virtual void AddChild(SkiaView child)
    {
        if (child.Parent != null)
        {
            throw new InvalidOperationException("View already has a parent");
        }

        _children.Add(child);
        child.Parent = this;

        // Propagate binding context to new child
        if (BindingContext != null)
        {
            SetInheritedBindingContext(child, BindingContext);
        }

        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// Removes a child view.
    /// </summary>
    public virtual void RemoveChild(SkiaView child)
    {
        if (_children.Remove(child))
        {
            child.Parent = null;
            InvalidateMeasure();
            Invalidate();
        }
    }

    /// <summary>
    /// Removes a child at the specified index.
    /// </summary>
    public virtual void RemoveChildAt(int index)
    {
        if (index >= 0 && index < _children.Count)
        {
            var child = _children[index];
            _children.RemoveAt(index);
            child.Parent = null;
            InvalidateMeasure();
            Invalidate();
        }
    }

    /// <summary>
    /// Inserts a child at the specified index.
    /// </summary>
    public virtual void InsertChild(int index, SkiaView child)
    {
        if (child.Parent != null)
        {
            throw new InvalidOperationException("View already has a parent");
        }

        _children.Insert(index, child);
        child.Parent = this;

        // Propagate binding context to new child
        if (BindingContext != null)
        {
            SetInheritedBindingContext(child, BindingContext);
        }

        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// Clears all children.
    /// </summary>
    public virtual void ClearChildren()
    {
        foreach (var child in _children)
        {
            child.Parent = null;
        }
        _children.Clear();
        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// Gets the content bounds (bounds minus padding).
    /// </summary>
    protected virtual SKRect GetContentBounds()
    {
        return GetContentBounds(Bounds);
    }

    /// <summary>
    /// Gets the content bounds for a given bounds rectangle.
    /// </summary>
    protected SKRect GetContentBounds(SKRect bounds)
    {
        return new SKRect(
            bounds.Left + Padding.Left,
            bounds.Top + Padding.Top,
            bounds.Right - Padding.Right,
            bounds.Bottom - Padding.Bottom);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Draw background if set (for layouts inside CollectionView items)
        if (BackgroundColor != SKColors.Transparent)
        {
            using var bgPaint = new SKPaint { Color = BackgroundColor, Style = SKPaintStyle.Fill };
            canvas.DrawRect(bounds, bgPaint);
        }

        // Log for StackLayout
        if (this is SkiaStackLayout)
        {
            bool hasCV = false;
            foreach (var c in _children)
            {
                if (c is SkiaCollectionView) hasCV = true;
            }
            if (hasCV)
            {
                Console.WriteLine($"[SkiaStackLayout+CV] OnDraw - bounds={bounds}, children={_children.Count}");
                foreach (var c in _children)
                {
                    Console.WriteLine($"[SkiaStackLayout+CV] Child: {c.GetType().Name}, IsVisible={c.IsVisible}, Bounds={c.Bounds}");
                }
            }
        }

        // Draw children in order
        foreach (var child in _children)
        {
            if (child.IsVisible)
            {
                child.Draw(canvas);
            }
        }
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !IsEnabled || !Bounds.Contains(new SKPoint(x, y)))
        {
            if (this is SkiaBorder)
            {
                Console.WriteLine($"[SkiaBorder.HitTest] Miss - x={x}, y={y}, Bounds={Bounds}, IsVisible={IsVisible}, IsEnabled={IsEnabled}");
            }
            return null;
        }

        // Hit test children in reverse order (top-most first)
        for (int i = _children.Count - 1; i >= 0; i--)
        {
            var child = _children[i];
            var hit = child.HitTest(x, y);
            if (hit != null)
            {
                if (this is SkiaBorder)
                {
                    Console.WriteLine($"[SkiaBorder.HitTest] Hit child - x={x}, y={y}, Bounds={Bounds}, child={hit.GetType().Name}");
                }
                return hit;
            }
        }

        if (this is SkiaBorder)
        {
            Console.WriteLine($"[SkiaBorder.HitTest] Hit self - x={x}, y={y}, Bounds={Bounds}, children={_children.Count}");
        }
        return this;
    }

    /// <summary>
    /// Forward pointer pressed events to the appropriate child.
    /// </summary>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        // Find which child was hit and forward the event
        var hit = HitTest(e.X, e.Y);
        if (hit != null && hit != this)
        {
            hit.OnPointerPressed(e);
        }
    }

    /// <summary>
    /// Forward pointer released events to the appropriate child.
    /// </summary>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        // Find which child was hit and forward the event
        var hit = HitTest(e.X, e.Y);
        if (hit != null && hit != this)
        {
            hit.OnPointerReleased(e);
        }
    }

    /// <summary>
    /// Forward pointer moved events to the appropriate child.
    /// </summary>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        // Find which child was hit and forward the event
        var hit = HitTest(e.X, e.Y);
        if (hit != null && hit != this)
        {
            hit.OnPointerMoved(e);
        }
    }

    /// <summary>
    /// Forward scroll events to the appropriate child.
    /// </summary>
    public override void OnScroll(ScrollEventArgs e)
    {
        // Find which child was hit and forward the event
        var hit = HitTest(e.X, e.Y);
        if (hit != null && hit != this)
        {
            hit.OnScroll(e);
        }
    }
}

/// <summary>
/// Stack layout that arranges children in a horizontal or vertical line.
/// </summary>
public class SkiaStackLayout : SkiaLayoutView
{
    /// <summary>
    /// Gets or sets the orientation of the stack.
    /// </summary>
    public StackOrientation Orientation { get; set; } = StackOrientation.Vertical;

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        // Handle NaN/Infinity in padding
        var paddingLeft = float.IsNaN(Padding.Left) ? 0 : Padding.Left;
        var paddingRight = float.IsNaN(Padding.Right) ? 0 : Padding.Right;
        var paddingTop = float.IsNaN(Padding.Top) ? 0 : Padding.Top;
        var paddingBottom = float.IsNaN(Padding.Bottom) ? 0 : Padding.Bottom;

        var contentWidth = availableSize.Width - paddingLeft - paddingRight;
        var contentHeight = availableSize.Height - paddingTop - paddingBottom;

        // Clamp negative sizes to 0
        if (contentWidth < 0 || float.IsNaN(contentWidth)) contentWidth = 0;
        if (contentHeight < 0 || float.IsNaN(contentHeight)) contentHeight = 0;

        float totalWidth = 0;
        float totalHeight = 0;
        float maxWidth = 0;
        float maxHeight = 0;

        var childAvailable = new SKSize(contentWidth, contentHeight);

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            var childSize = child.Measure(childAvailable);

            // Skip NaN sizes from child measurements
            var childWidth = float.IsNaN(childSize.Width) ? 0 : childSize.Width;
            var childHeight = float.IsNaN(childSize.Height) ? 0 : childSize.Height;

            if (Orientation == StackOrientation.Vertical)
            {
                totalHeight += childHeight;
                maxWidth = Math.Max(maxWidth, childWidth);
            }
            else
            {
                totalWidth += childWidth;
                maxHeight = Math.Max(maxHeight, childHeight);
            }
        }

        // Add spacing
        var visibleCount = Children.Count(c => c.IsVisible);
        var totalSpacing = Math.Max(0, visibleCount - 1) * Spacing;

        if (Orientation == StackOrientation.Vertical)
        {
            totalHeight += totalSpacing;
            return new SKSize(
                maxWidth + paddingLeft + paddingRight,
                totalHeight + paddingTop + paddingBottom);
        }
        else
        {
            totalWidth += totalSpacing;
            return new SKSize(
                totalWidth + paddingLeft + paddingRight,
                maxHeight + paddingTop + paddingBottom);
        }
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        var content = GetContentBounds(bounds);

        // Clamp content dimensions if infinite - use reasonable defaults
        var contentWidth = float.IsInfinity(content.Width) || float.IsNaN(content.Width) ? 800f : content.Width;
        var contentHeight = float.IsInfinity(content.Height) || float.IsNaN(content.Height) ? 600f : content.Height;

        float offset = 0;

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            var childDesired = child.DesiredSize;

            // Handle NaN and Infinity in desired size
            var childWidth = float.IsNaN(childDesired.Width) || float.IsInfinity(childDesired.Width)
                ? contentWidth
                : childDesired.Width;
            var childHeight = float.IsNaN(childDesired.Height) || float.IsInfinity(childDesired.Height)
                ? contentHeight
                : childDesired.Height;

            SKRect childBounds;
            if (Orientation == StackOrientation.Vertical)
            {
                // For ScrollView children, give them the remaining viewport height
                // Clamp to avoid giving them their content size
                var remainingHeight = Math.Max(0, contentHeight - offset);
                var useHeight = child is SkiaScrollView
                    ? remainingHeight
                    : Math.Min(childHeight, remainingHeight > 0 ? remainingHeight : childHeight);

                childBounds = new SKRect(
                    content.Left,
                    content.Top + offset,
                    content.Left + contentWidth,
                    content.Top + offset + useHeight);
                offset += useHeight + Spacing;
            }
            else
            {
                // For ScrollView children, give them the remaining viewport width
                var remainingWidth = Math.Max(0, contentWidth - offset);
                var useWidth = child is SkiaScrollView
                    ? remainingWidth
                    : Math.Min(childWidth, remainingWidth > 0 ? remainingWidth : childWidth);

                // Respect child's VerticalOptions for horizontal layouts
                var useHeight = Math.Min(childHeight, contentHeight);
                float childTop = content.Top;
                float childBottom = content.Top + useHeight;

                var verticalOptions = child.VerticalOptions;
                var alignmentValue = (int)verticalOptions.Alignment;

                // LayoutAlignment: Start=0, Center=1, End=2, Fill=3
                if (alignmentValue == 1) // Center
                {
                    childTop = content.Top + (contentHeight - useHeight) / 2;
                    childBottom = childTop + useHeight;
                }
                else if (alignmentValue == 2) // End
                {
                    childTop = content.Top + contentHeight - useHeight;
                    childBottom = content.Top + contentHeight;
                }
                else if (alignmentValue == 3) // Fill
                {
                    childTop = content.Top;
                    childBottom = content.Top + contentHeight;
                }

                childBounds = new SKRect(
                    content.Left + offset,
                    childTop,
                    content.Left + offset + useWidth,
                    childBottom);
                offset += useWidth + Spacing;
            }

            // Apply child's margin
            var margin = child.Margin;
            var marginedBounds = new SKRect(
                childBounds.Left + (float)margin.Left,
                childBounds.Top + (float)margin.Top,
                childBounds.Right - (float)margin.Right,
                childBounds.Bottom - (float)margin.Bottom);
            child.Arrange(marginedBounds);
        }
        return bounds;
    }
}

/// <summary>
/// Stack orientation options.
/// </summary>
public enum StackOrientation
{
    Vertical,
    Horizontal
}

/// <summary>
/// Grid layout that arranges children in rows and columns.
/// </summary>
public class SkiaGrid : SkiaLayoutView
{
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
    public float RowSpacing { get; set; } = 0;

    /// <summary>
    /// Spacing between columns.
    /// </summary>
    public float ColumnSpacing { get; set; } = 0;

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

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        var contentWidth = availableSize.Width - Padding.Left - Padding.Right;
        var contentHeight = availableSize.Height - Padding.Top - Padding.Bottom;

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
                var childSize = child.Measure(new SKSize(float.PositiveInfinity, float.PositiveInfinity));
                var childWidth = float.IsNaN(childSize.Width) ? 0 : childSize.Width;
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
            var childSize = child.Measure(new SKSize(cellWidth, float.PositiveInfinity));

            // Track max height for each row
            // Cap infinite/very large heights - child returning infinity means it doesn't have a natural height
            var childHeight = childSize.Height;
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

            child.Measure(new SKSize(cellWidth, cellHeight));
        }

        // Calculate total size
        var totalWidth = _columnWidths.Sum() + Math.Max(0, columnCount - 1) * ColumnSpacing;
        var totalHeight = _rowHeights.Sum() + Math.Max(0, rowCount - 1) * RowSpacing;

        return new SKSize(
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

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        try
        {
            var content = GetContentBounds(bounds);

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
            var marginedBounds = new SKRect(
                x + (float)margin.Left,
                y + (float)margin.Top,
                x + width - (float)margin.Right,
                y + height - (float)margin.Bottom);
            child.Arrange(marginedBounds);
        }
        return bounds;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SkiaGrid] EXCEPTION in ArrangeOverride: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[SkiaGrid] Bounds: {bounds}, RowHeights: {_rowHeights.Length}, RowDefs: {_rowDefinitions.Count}, Children: {Children.Count}");
            Console.WriteLine($"[SkiaGrid] Stack trace: {ex.StackTrace}");
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

/// <summary>
/// Absolute layout that positions children at exact coordinates.
/// </summary>
public class SkiaAbsoluteLayout : SkiaLayoutView
{
    private readonly Dictionary<SkiaView, AbsoluteLayoutBounds> _childBounds = new();

    /// <summary>
    /// Adds a child at the specified position and size.
    /// </summary>
    public void AddChild(SkiaView child, SKRect bounds, AbsoluteLayoutFlags flags = AbsoluteLayoutFlags.None)
    {
        base.AddChild(child);
        _childBounds[child] = new AbsoluteLayoutBounds(bounds, flags);
    }

    public override void RemoveChild(SkiaView child)
    {
        base.RemoveChild(child);
        _childBounds.Remove(child);
    }

    /// <summary>
    /// Gets the layout bounds for a child.
    /// </summary>
    public AbsoluteLayoutBounds GetLayoutBounds(SkiaView child)
    {
        return _childBounds.TryGetValue(child, out var bounds)
            ? bounds
            : new AbsoluteLayoutBounds(SKRect.Empty, AbsoluteLayoutFlags.None);
    }

    /// <summary>
    /// Sets the layout bounds for a child.
    /// </summary>
    public void SetLayoutBounds(SkiaView child, SKRect bounds, AbsoluteLayoutFlags flags = AbsoluteLayoutFlags.None)
    {
        _childBounds[child] = new AbsoluteLayoutBounds(bounds, flags);
        InvalidateMeasure();
        Invalidate();
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        float maxRight = 0;
        float maxBottom = 0;

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            var layout = GetLayoutBounds(child);
            var bounds = layout.Bounds;

            child.Measure(new SKSize(bounds.Width, bounds.Height));

            maxRight = Math.Max(maxRight, bounds.Right);
            maxBottom = Math.Max(maxBottom, bounds.Bottom);
        }

        return new SKSize(
            maxRight + Padding.Left + Padding.Right,
            maxBottom + Padding.Top + Padding.Bottom);
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        var content = GetContentBounds(bounds);

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            var layout = GetLayoutBounds(child);
            var childBounds = layout.Bounds;
            var flags = layout.Flags;

            float x, y, width, height;

            // X position
            if (flags.HasFlag(AbsoluteLayoutFlags.XProportional))
                x = content.Left + childBounds.Left * content.Width;
            else
                x = content.Left + childBounds.Left;

            // Y position
            if (flags.HasFlag(AbsoluteLayoutFlags.YProportional))
                y = content.Top + childBounds.Top * content.Height;
            else
                y = content.Top + childBounds.Top;

            // Width
            if (flags.HasFlag(AbsoluteLayoutFlags.WidthProportional))
                width = childBounds.Width * content.Width;
            else if (childBounds.Width < 0)
                width = child.DesiredSize.Width;
            else
                width = childBounds.Width;

            // Height
            if (flags.HasFlag(AbsoluteLayoutFlags.HeightProportional))
                height = childBounds.Height * content.Height;
            else if (childBounds.Height < 0)
                height = child.DesiredSize.Height;
            else
                height = childBounds.Height;

            // Apply child's margin
            var margin = child.Margin;
            var marginedBounds = new SKRect(
                x + (float)margin.Left,
                y + (float)margin.Top,
                x + width - (float)margin.Right,
                y + height - (float)margin.Bottom);
            child.Arrange(marginedBounds);
        }
        return bounds;
    }
}

/// <summary>
/// Absolute layout bounds for a child.
/// </summary>
public readonly struct AbsoluteLayoutBounds
{
    public SKRect Bounds { get; }
    public AbsoluteLayoutFlags Flags { get; }

    public AbsoluteLayoutBounds(SKRect bounds, AbsoluteLayoutFlags flags)
    {
        Bounds = bounds;
        Flags = flags;
    }
}

/// <summary>
/// Flags for absolute layout positioning.
/// </summary>
[Flags]
public enum AbsoluteLayoutFlags
{
    None = 0,
    XProportional = 1,
    YProportional = 2,
    WidthProportional = 4,
    HeightProportional = 8,
    PositionProportional = XProportional | YProportional,
    SizeProportional = WidthProportional | HeightProportional,
    All = XProportional | YProportional | WidthProportional | HeightProportional
}
