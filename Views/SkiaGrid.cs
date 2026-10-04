// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Microsoft.Maui;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Grid layout that arranges Skia children in rows and columns, for views
/// composed directly in Skia (no MAUI <c>Grid</c> behind them). A MAUI
/// <c>Grid</c> is laid out by MAUI's own GridLayoutManager on a
/// <see cref="SkiaCrossPlatformLayout"/> instead (see GridHandler).
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
    private float[] _columnNaturalWidths = Array.Empty<float>();

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

        // Unconstrained width (a HorizontalStackLayout or horizontal ScrollView
        // measuring us): Star columns size to their content, as MAUI's grid does.
        bool infiniteWidth = float.IsNaN(contentWidth) || float.IsInfinity(contentWidth);
        if (float.IsNaN(contentHeight) || float.IsInfinity(contentHeight)) contentHeight = float.PositiveInfinity;

        // Without definitions a code-built grid grows rows and columns for its children.
        var rowCount = Math.Max(1, _rowDefinitions.Count > 0 ? _rowDefinitions.Count : GetMaxRow() + 1);
        var columnCount = Math.Max(1, _columnDefinitions.Count > 0 ? _columnDefinitions.Count : GetMaxColumn() + 1);

        // First pass: measure children in Auto columns to get natural widths
        var columnNaturalWidths = new float[columnCount];
        var rowNaturalHeights = new float[rowCount];
        var columnSpans = new Dictionary<(int Start, int Length), float>();
        var rowSpans = new Dictionary<(int Start, int Length), float>();

        foreach (var child in Children.ToArray())
        {
            if (!child.IsVisible) continue;

            var pos = GetPosition(child);

            // For Auto columns, measure with infinite width to get natural size
            var def = pos.Column < _columnDefinitions.Count ? _columnDefinitions[pos.Column] : GridLength.Star;
            if (pos.ColumnSpan > 1)
            {
                // A child spanning Auto columns widens them (resolved below).
                if (SpanHasAuto(_columnDefinitions, pos.Column, pos.ColumnSpan))
                {
                    var spanMargin = child.Margin;
                    var spanSize = child.Measure(new Size(double.PositiveInfinity,
                        NeedsNaturalHeight(pos, contentHeight) ? double.PositiveInfinity : Inset(contentHeight, spanMargin.VerticalThickness)));
                    if (!double.IsNaN(spanSize.Width))
                        TrackSpan(columnSpans, pos.Column, pos.ColumnSpan, (float)(spanSize.Width + spanMargin.HorizontalThickness));
                }
            }
            else if (def.IsAuto || (infiniteWidth && def.IsStar))
            {
                // Height: unbounded only where a natural height is wanted (see
                // NeedsNaturalHeight); otherwise the grid's height bounds it.
                // A child's size excludes its margin; its column holds both.
                var margin = child.Margin;
                var childSize = child.Measure(new Size(double.PositiveInfinity,
                    NeedsNaturalHeight(pos, contentHeight) ? double.PositiveInfinity : Inset(contentHeight, margin.VerticalThickness)));
                var childWidth = double.IsNaN(childSize.Width) ? 0f : (float)(childSize.Width + margin.HorizontalThickness);
                columnNaturalWidths[pos.Column] = Math.Max(columnNaturalWidths[pos.Column], childWidth);
            }
        }

        ResolveSpans(_columnDefinitions, columnNaturalWidths, columnSpans, ColumnSpacing);

        // Calculate column widths - handle Auto, Absolute, and Star
        _columnNaturalWidths = columnNaturalWidths;
        _columnWidths = infiniteWidth
            ? NaturalSizes(_columnDefinitions, columnCount, columnNaturalWidths)
            : CalculateSizesWithAuto(_columnDefinitions, contentWidth, ColumnSpacing, columnCount, columnNaturalWidths);

        // Second pass: measure all children with calculated column widths
        foreach (var child in Children.ToArray())
        {
            if (!child.IsVisible) continue;

            var pos = GetPosition(child);

            // Only Auto rows (or an unconstrained grid) size to their content.
            // A child in a Star or fixed row is measured once, at its real
            // cell size, below: measuring it against infinite height first made
            // a virtualising list realise every item it has (an SfListView of
            // thousands of commits, on every resize).
            if (!NeedsNaturalHeight(pos, contentHeight))
                continue;

            var cellWidth = GetCellWidth(pos.Column, pos.ColumnSpan);
            var childMargin = child.Margin;
            var childSize = child.Measure(new Size(Inset(cellWidth, childMargin.HorizontalThickness), double.PositiveInfinity));

            // Track max height for each row
            // Cap infinite/very large heights - child returning infinity means it doesn't have a natural height
            var childHeight = (float)childSize.Height;
            if (float.IsNaN(childHeight) || float.IsInfinity(childHeight) || childHeight > 100000)
            {
                // Use a default minimum - will be expanded by Star sizing if finite height is available
                childHeight = 44; // Standard row height
            }
            else
            {
                childHeight += (float)childMargin.VerticalThickness;
            }
            if (pos.RowSpan == 1)
            {
                rowNaturalHeights[pos.Row] = Math.Max(rowNaturalHeights[pos.Row], childHeight);
            }
            else
            {
                // A child spanning Auto rows makes them tall enough for it (resolved
                // below): a price pill spanning two label rows was squeezed to the
                // labels' height and its text cut off (Strikeline's watchlist).
                TrackSpan(rowSpans, pos.Row, pos.RowSpan, childHeight);
            }
        }

        ResolveSpans(_rowDefinitions, rowNaturalHeights, rowSpans, RowSpacing);

        // Calculate row heights - use natural heights when available height is infinite or very large
        // (Some layouts pass float.MaxValue instead of PositiveInfinity)
        if (float.IsInfinity(contentHeight) || contentHeight > 100000)
        {
            // Unconstrained: Auto and Star rows take their content, Absolute rows their
            // value (a 30 px row inside an Auto row of another grid was as tall as its
            // label, and the rows of Strikeline's flyout items ran into each other).
            _rowHeights = NaturalSizes(_rowDefinitions, rowCount, rowNaturalHeights);
        }
        else
        {
            _rowHeights = CalculateSizesWithAuto(_rowDefinitions, contentHeight, RowSpacing, rowCount, rowNaturalHeights);
        }

        // Third pass: re-measure children with actual cell sizes
        foreach (var child in Children.ToArray())
        {
            if (!child.IsVisible) continue;

            var pos = GetPosition(child);
            var cellWidth = GetCellWidth(pos.Column, pos.ColumnSpan);
            var cellHeight = GetCellHeight(pos.Row, pos.RowSpan);
            var cellMargin = child.Margin;

            child.Measure(new Size(Inset(cellWidth, cellMargin.HorizontalThickness), Inset(cellHeight, cellMargin.VerticalThickness)));
        }

        // The measured size, as MAUI's GridLayoutManager reports it: a Star row (column)
        // counts only what its content needs, up to its share (MinimizeStarsForMeasurement);
        // it fills the space when the grid is arranged. Reporting the whole share made a
        // parent that measures with a height it does not mean to fill (SfCardView passes 200
        // for "unbounded") get that height back: Strikeline's option chain rows were 200 tall.
        var measuredWidths = infiniteWidth ? _columnWidths
            : StarMinimums(_columnDefinitions, _columnWidths, ColumnSpacing, horizontal: true);
        var measuredHeights = float.IsInfinity(contentHeight) || contentHeight > 100000 ? _rowHeights
            : StarMinimums(_rowDefinitions, _rowHeights, RowSpacing, horizontal: false);
        var totalWidth = measuredWidths.Sum() + Math.Max(0, columnCount - 1) * ColumnSpacing;
        var totalHeight = measuredHeights.Sum() + Math.Max(0, rowCount - 1) * RowSpacing;

        return new Size(
            totalWidth + Padding.Left + Padding.Right,
            totalHeight + Padding.Top + Padding.Bottom);
    }

    /// <summary>
    /// The row heights (column widths) with each Star one reduced to what its children need,
    /// capped at its share: MAUI's MinimumSize for a star definition. A child spanning several
    /// rows shares what it still needs, after the non-star rows and spacing it spans, equally
    /// over its star rows (GridStructure.DetermineMinimumStarSizesInSpan).
    /// </summary>
    private float[] StarMinimums(List<GridLength> definitions, float[] sizes, double spacing, bool horizontal)
    {
        bool IsStar(int i) => (i < definitions.Count ? definitions[i] : GridLength.Star).IsStar;
        var result = (float[])sizes.Clone();
        var minimums = new float[sizes.Length];
        bool anyStar = false;
        for (int i = 0; i < sizes.Length; i++)
            anyStar |= IsStar(i);
        if (!anyStar)
            return result;

        var spans = new List<(int Start, int End, float Needed)>();
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            var pos = GetPosition(child);
            int start = horizontal ? pos.Column : pos.Row;
            int end = Math.Min(start + (horizontal ? pos.ColumnSpan : pos.RowSpan), sizes.Length);
            if (start >= sizes.Length) continue;
            var margin = child.Margin;
            float needed = horizontal
                ? (float)(child.DesiredSize.Width + margin.HorizontalThickness)
                : (float)(child.DesiredSize.Height + margin.VerticalThickness);
            if (float.IsNaN(needed) || float.IsInfinity(needed)) continue;
            if (end - start == 1)
            {
                if (IsStar(start))
                    minimums[start] = Math.Max(minimums[start], needed);
            }
            else
                spans.Add((start, end, needed));
        }
        foreach (var (start, end, needed) in spans)
        {
            float remaining = needed - (float)(spacing * (end - start - 1));
            int stars = 0;
            float starTotal = 0;
            for (int i = start; i < end; i++)
            {
                if (IsStar(i)) { stars++; starTotal += minimums[i]; }
                else remaining -= sizes[i];
            }
            if (stars == 0 || starTotal >= remaining) continue;
            float share = (remaining - starTotal) / stars;
            for (int i = start; i < end; i++)
                if (IsStar(i))
                    minimums[i] += share;
        }
        for (int i = 0; i < result.Length; i++)
            if (IsStar(i))
                result[i] = Math.Min(minimums[i], sizes[i]);
        return result;
    }

    private static bool SpanHasAuto(List<GridLength> definitions, int start, int length)
    {
        for (int i = start; i < start + length; i++)
            if (i < definitions.Count && definitions[i].IsAuto)
                return true;
        return false;
    }

    private static void TrackSpan(Dictionary<(int Start, int Length), float> spans, int start, int length, float requested)
    {
        if (!spans.TryGetValue((start, length), out var current) || requested > current)
            spans[(start, length)] = requested;
    }

    /// <summary>
    /// MAUI's span resolution (GridLayoutManager.ResolveSpan): when a child spanning
    /// several rows (or columns) needs more than they add up to, with the spacing
    /// between them, the difference is shared equally by the Auto ones it spans. A
    /// span that includes a Star row or column is left to the star sizing.
    /// </summary>
    private static void ResolveSpans(List<GridLength> definitions, float[] naturalSizes,
        Dictionary<(int Start, int Length), float> spans, double spacing)
    {
        foreach (var ((start, length), requested) in spans)
        {
            int end = Math.Min(start + length, naturalSizes.Length);
            float current = (float)(spacing * (end - start - 1));
            int autoCount = 0;
            bool hasStar = false;
            for (int i = start; i < end; i++)
            {
                var def = i < definitions.Count ? definitions[i] : GridLength.Star;
                if (def.IsAbsolute) current += def.Value;
                else if (def.IsAuto) { current += naturalSizes[i]; autoCount++; }
                else hasStar = true;
            }
            if (hasStar || autoCount == 0 || requested <= current)
                continue;
            float share = (requested - current) / autoCount;
            for (int i = start; i < end; i++)
                if (i < definitions.Count && definitions[i].IsAuto)
                    naturalSizes[i] += share;
        }
    }

    /// <summary>
    /// True when the child's rows take their height from content: the grid's
    /// height is unconstrained, or a row the child spans is Auto.
    /// </summary>
    private bool NeedsNaturalHeight(GridPosition pos, float contentHeight)
    {
        if (float.IsInfinity(contentHeight) || contentHeight > 100000)
            return true;
        for (int r = pos.Row; r < pos.Row + Math.Max(1, pos.RowSpan); r++)
        {
            var def = r < _rowDefinitions.Count ? _rowDefinitions[r] : GridLength.Star;
            if (def.IsAuto)
                return true;
        }
        return false;
    }

    private bool HasStarColumn(int columnCount)
    {
        for (int i = 0; i < columnCount; i++)
        {
            var def = i < _columnDefinitions.Count ? _columnDefinitions[i] : GridLength.Star;
            if (def.IsStar)
                return true;
        }
        return false;
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

    /// <summary>Track sizes with no constraint: absolute as declared, everything else its content.</summary>
    private static float[] NaturalSizes(List<GridLength> definitions, int count, float[] naturalSizes)
    {
        var sizes = new float[count];
        for (int i = 0; i < count; i++)
        {
            var def = i < definitions.Count ? definitions[i] : GridLength.Star;
            sizes[i] = def.IsAbsolute ? (float)def.Value : naturalSizes[i];
        }
        return sizes;
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

    /// <summary>A cell's extent less the child's margin, never negative (infinity stays infinite).</summary>
    private static double Inset(double extent, double margin) => Math.Max(0, extent - margin);

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

            // Star columns take the arranged width, which can differ from the
            // measured one (measured unconstrained, then placed in a fixed slot).
            if (content.Width > 0 && !float.IsInfinity(content.Width) && HasStarColumn(columnCount)
                && Math.Abs(content.Width - (_columnWidths.Sum() + Math.Max(0, columnCount - 1) * ColumnSpacing)) > 1)
            {
                _columnWidths = CalculateSizesWithAuto(_columnDefinitions, content.Width, ColumnSpacing, columnCount,
                    _columnNaturalWidths.Length == columnCount ? _columnNaturalWidths : new float[columnCount]);
            }

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

        foreach (var child in Children.ToArray())
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
            if (float.IsInfinity(height) || float.IsNaN(height))
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

            // An explicit size under Fill is centred in a larger cell, as MAUI's ComputeFrame
            // treats it (a 1 px separator in a * row is a line in the middle, not a bar).
            if (hAlign == LayoutAlignment.Fill && child.WidthRequest >= 0 && childWidth < cellWidth)
                hAlign = LayoutAlignment.Center;
            if (vAlign == LayoutAlignment.Fill && child.HeightRequest >= 0 && childHeight < cellHeight)
                vAlign = LayoutAlignment.Center;

            // Apply HorizontalOptions. An explicit size is kept even when it is
            // larger than the cell, placed by the alignment (Fill as Start), as
            // MAUI's ComputeFrame does: SfTabView's page strip is as wide as all
            // its pages, slid inside a clipped grid one page wide.
            float finalX = cellX;
            float finalWidth = cellWidth;
            if (child.WidthRequest >= 0 && childWidth > cellWidth)
            {
                finalWidth = childWidth;
                if (hAlign == LayoutAlignment.Center)
                    finalX = cellX + (cellWidth - childWidth) / 2;
                else if (hAlign == LayoutAlignment.End)
                    finalX = cellX + cellWidth - childWidth;
            }
            else if (hAlign != LayoutAlignment.Fill && childWidth < cellWidth && childWidth > 0)
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
            if (child.HeightRequest >= 0 && childHeight > cellHeight)
            {
                finalHeight = childHeight;
                if (vAlign == LayoutAlignment.Center)
                    finalY = cellY + (cellHeight - childHeight) / 2;
                else if (vAlign == LayoutAlignment.End)
                    finalY = cellY + cellHeight - childHeight;
            }
            else if (vAlign != LayoutAlignment.Fill && childHeight < cellHeight && childHeight > 0)
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
