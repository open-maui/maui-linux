// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

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
        if (!IsVisible || !Bounds.Contains(new SKPoint(x, y)))
            return null;

        // Hit test children in reverse order (top-most first)
        for (int i = _children.Count - 1; i >= 0; i--)
        {
            var child = _children[i];
            var hit = child.HitTest(x, y);
            if (hit != null)
                return hit;
        }

        return this;
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
        var contentWidth = availableSize.Width - Padding.Left - Padding.Right;
        var contentHeight = availableSize.Height - Padding.Top - Padding.Bottom;

        float totalWidth = 0;
        float totalHeight = 0;
        float maxWidth = 0;
        float maxHeight = 0;

        var childAvailable = new SKSize(contentWidth, contentHeight);

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            var childSize = child.Measure(childAvailable);

            if (Orientation == StackOrientation.Vertical)
            {
                totalHeight += childSize.Height;
                maxWidth = Math.Max(maxWidth, childSize.Width);
            }
            else
            {
                totalWidth += childSize.Width;
                maxHeight = Math.Max(maxHeight, childSize.Height);
            }
        }

        // Add spacing
        var visibleCount = Children.Count(c => c.IsVisible);
        var totalSpacing = Math.Max(0, visibleCount - 1) * Spacing;

        if (Orientation == StackOrientation.Vertical)
        {
            totalHeight += totalSpacing;
            return new SKSize(
                maxWidth + Padding.Left + Padding.Right,
                totalHeight + Padding.Top + Padding.Bottom);
        }
        else
        {
            totalWidth += totalSpacing;
            return new SKSize(
                totalWidth + Padding.Left + Padding.Right,
                maxHeight + Padding.Top + Padding.Bottom);
        }
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        var content = GetContentBounds(bounds);
        float offset = 0;

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            var childDesired = child.DesiredSize;

            SKRect childBounds;
            if (Orientation == StackOrientation.Vertical)
            {
                childBounds = new SKRect(
                    content.Left,
                    content.Top + offset,
                    content.Right,
                    content.Top + offset + childDesired.Height);
                offset += childDesired.Height + Spacing;
            }
            else
            {
                childBounds = new SKRect(
                    content.Left + offset,
                    content.Top,
                    content.Left + offset + childDesired.Width,
                    content.Bottom);
                offset += childDesired.Width + Spacing;
            }

            child.Arrange(childBounds);
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

        var rowCount = Math.Max(1, _rowDefinitions.Count);
        var columnCount = Math.Max(1, _columnDefinitions.Count);

        // Calculate column widths
        _columnWidths = CalculateSizes(_columnDefinitions, contentWidth, ColumnSpacing, columnCount);
        _rowHeights = CalculateSizes(_rowDefinitions, contentHeight, RowSpacing, rowCount);

        // Measure children to adjust auto sizes
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

    private float[] CalculateSizes(List<GridLength> definitions, float available, float spacing, int count)
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
                sizes[i] = 0; // Will be calculated from children
            }
            else if (def.IsStar)
            {
                starTotal += def.Value;
            }
        }

        // Second pass: star sizes
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
        var content = GetContentBounds(bounds);

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            var pos = GetPosition(child);

            var x = content.Left + GetColumnOffset(pos.Column);
            var y = content.Top + GetRowOffset(pos.Row);
            var width = GetCellWidth(pos.Column, pos.ColumnSpan);
            var height = GetCellHeight(pos.Row, pos.RowSpan);

            child.Arrange(new SKRect(x, y, x + width, y + height));
        }
        return bounds;
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

            child.Arrange(new SKRect(x, y, x + width, y + height));
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
