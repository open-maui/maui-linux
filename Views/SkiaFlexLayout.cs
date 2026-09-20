using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaFlexLayout : SkiaLayoutView
{
    public static readonly BindableProperty DirectionProperty = BindableProperty.Create(
        nameof(Direction), typeof(FlexDirection), typeof(SkiaFlexLayout), FlexDirection.Row,
        BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaFlexLayout)b).InvalidateMeasure());

    public static readonly BindableProperty WrapProperty = BindableProperty.Create(
        nameof(Wrap), typeof(FlexWrap), typeof(SkiaFlexLayout), FlexWrap.NoWrap,
        BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaFlexLayout)b).InvalidateMeasure());

    public static readonly BindableProperty JustifyContentProperty = BindableProperty.Create(
        nameof(JustifyContent), typeof(FlexJustify), typeof(SkiaFlexLayout), FlexJustify.Start,
        BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaFlexLayout)b).InvalidateMeasure());

    public static readonly BindableProperty AlignItemsProperty = BindableProperty.Create(
        nameof(AlignItems), typeof(FlexAlignItems), typeof(SkiaFlexLayout), FlexAlignItems.Stretch,
        BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaFlexLayout)b).InvalidateMeasure());

    public static readonly BindableProperty AlignContentProperty = BindableProperty.Create(
        nameof(AlignContent), typeof(FlexAlignContent), typeof(SkiaFlexLayout), FlexAlignContent.Stretch,
        BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaFlexLayout)b).InvalidateMeasure());

    public static readonly BindableProperty OrderProperty = BindableProperty.CreateAttached(
        "Order", typeof(int), typeof(SkiaFlexLayout), 0, BindingMode.TwoWay);

    public static readonly BindableProperty GrowProperty = BindableProperty.CreateAttached(
        "Grow", typeof(float), typeof(SkiaFlexLayout), 0f, BindingMode.TwoWay);

    public static readonly BindableProperty ShrinkProperty = BindableProperty.CreateAttached(
        "Shrink", typeof(float), typeof(SkiaFlexLayout), 1f, BindingMode.TwoWay);

    public static readonly BindableProperty BasisProperty = BindableProperty.CreateAttached(
        "Basis", typeof(FlexBasis), typeof(SkiaFlexLayout), FlexBasis.Auto, BindingMode.TwoWay);

    public static readonly BindableProperty AlignSelfProperty = BindableProperty.CreateAttached(
        "AlignSelf", typeof(FlexAlignSelf), typeof(SkiaFlexLayout), FlexAlignSelf.Auto, BindingMode.TwoWay);

    public FlexDirection Direction
    {
        get => (FlexDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public FlexWrap Wrap
    {
        get => (FlexWrap)GetValue(WrapProperty);
        set => SetValue(WrapProperty, value);
    }

    public FlexJustify JustifyContent
    {
        get => (FlexJustify)GetValue(JustifyContentProperty);
        set => SetValue(JustifyContentProperty, value);
    }

    public FlexAlignItems AlignItems
    {
        get => (FlexAlignItems)GetValue(AlignItemsProperty);
        set => SetValue(AlignItemsProperty, value);
    }

    public FlexAlignContent AlignContent
    {
        get => (FlexAlignContent)GetValue(AlignContentProperty);
        set => SetValue(AlignContentProperty, value);
    }

    public static int GetOrder(SkiaView view) => (int)view.GetValue(OrderProperty);
    public static void SetOrder(SkiaView view, int value) => view.SetValue(OrderProperty, value);

    public static float GetGrow(SkiaView view) => (float)view.GetValue(GrowProperty);
    public static void SetGrow(SkiaView view, float value) => view.SetValue(GrowProperty, value);

    public static float GetShrink(SkiaView view) => (float)view.GetValue(ShrinkProperty);
    public static void SetShrink(SkiaView view, float value) => view.SetValue(ShrinkProperty, value);

    public static FlexBasis GetBasis(SkiaView view) => (FlexBasis)view.GetValue(BasisProperty);
    public static void SetBasis(SkiaView view, FlexBasis value) => view.SetValue(BasisProperty, value);

    public static FlexAlignSelf GetAlignSelf(SkiaView view) => (FlexAlignSelf)view.GetValue(AlignSelfProperty);
    public static void SetAlignSelf(SkiaView view, FlexAlignSelf value) => view.SetValue(AlignSelfProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        bool isRow = Direction == FlexDirection.Row || Direction == FlexDirection.RowReverse;
        var lines = BuildLines(availableSize, isRow);
        float mainExtent = 0f, crossExtent = 0f;
        foreach (var line in lines)
        {
            mainExtent = Math.Max(mainExtent, line.Sum(i => i.MainSize));
            crossExtent += line.Max(i => i.CrossSize);
        }
        return isRow ? new Size(mainExtent, crossExtent) : new Size(crossExtent, mainExtent);
    }

    private readonly record struct FlexItem(SkiaView Child, float MainSize, float CrossSize, float Grow, float Shrink);

    /// <summary>
    /// Measures visible children in order and splits them into lines: one
    /// line when Wrap is NoWrap, otherwise a new line whenever the next item's
    /// basis would overflow the main axis. Grow/shrink are applied later per line.
    /// </summary>
    private List<List<FlexItem>> BuildLines(Size available, bool isRow)
    {
        var lines = new List<List<FlexItem>>();
        var ordered = Children.Where(c => c.IsVisible).OrderBy(GetOrder).ToList();
        if (ordered.Count == 0)
            return lines;

        float mainLimit = isRow ? (float)available.Width : (float)available.Height;
        bool wrap = Wrap != FlexWrap.NoWrap && !float.IsInfinity(mainLimit) && !float.IsNaN(mainLimit);

        var current = new List<FlexItem>();
        float used = 0f;
        foreach (var child in ordered)
        {
            var basis = GetBasis(child);
            Size size;
            if (basis.IsAuto)
                size = child.Measure(available);
            else
                size = isRow ? child.Measure(new Size(basis.Length, available.Height)) : child.Measure(new Size(available.Width, basis.Length));

            var item = new FlexItem(child,
                isRow ? (float)size.Width : (float)size.Height,
                isRow ? (float)size.Height : (float)size.Width,
                GetGrow(child), GetShrink(child));

            if (wrap && current.Count > 0 && used + item.MainSize > mainLimit + 0.01f)
            {
                lines.Add(current);
                current = new List<FlexItem>();
                used = 0f;
            }
            current.Add(item);
            used += item.MainSize;
        }
        if (current.Count > 0)
            lines.Add(current);
        return lines;
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        if (Children.Count == 0)
            return bounds;

        bool isRow = Direction == FlexDirection.Row || Direction == FlexDirection.RowReverse;
        bool isReverse = Direction == FlexDirection.RowReverse || Direction == FlexDirection.ColumnReverse;
        float mainSize = isRow ? (float)bounds.Width : (float)bounds.Height;
        float crossSize = isRow ? (float)bounds.Height : (float)bounds.Width;

        var lines = BuildLines(new Size(bounds.Width, bounds.Height), isRow);
        if (lines.Count == 0)
            return bounds;
        if (Wrap == FlexWrap.WrapReverse)
            lines.Reverse();

        // Resolve grow/shrink per line and the line's cross extent.
        var resolved = new List<(List<(SkiaView child, float main, float cross)> items, float lineCross)>();
        foreach (var line in lines)
        {
            float totalBasis = line.Sum(i => i.MainSize);
            float totalGrow = line.Sum(i => i.Grow);
            float totalShrink = line.Sum(i => i.Shrink);
            float free = mainSize - totalBasis;
            var items = new List<(SkiaView, float, float)>();
            foreach (var i in line)
            {
                float main = i.MainSize;
                if (free > 0f && totalGrow > 0f) main += free * (i.Grow / totalGrow);
                else if (free < 0f && totalShrink > 0f) main += free * (i.Shrink / totalShrink);
                items.Add((i.Child, Math.Max(0f, main), i.CrossSize));
            }
            resolved.Add((items, line.Max(i => i.CrossSize)));
        }

        // AlignContent distributes lines on the cross axis (only meaningful with several lines).
        float totalLinesCross = resolved.Sum(l => l.lineCross);
        float crossFree = Math.Max(0f, crossSize - totalLinesCross);
        float crossPos = isRow ? (float)bounds.Top : (float)bounds.Left;
        float lineGap = 0f;
        bool stretchLines = false;
        if (resolved.Count > 1 || AlignContent == FlexAlignContent.Stretch)
        {
            switch (AlignContent)
            {
                case FlexAlignContent.Center: crossPos += crossFree / 2f; break;
                case FlexAlignContent.End: crossPos += crossFree; break;
                case FlexAlignContent.SpaceBetween: if (resolved.Count > 1) lineGap = crossFree / (resolved.Count - 1); break;
                case FlexAlignContent.SpaceAround: lineGap = crossFree / resolved.Count; crossPos += lineGap / 2f; break;
                case FlexAlignContent.SpaceEvenly: lineGap = crossFree / (resolved.Count + 1); crossPos += lineGap; break;
                case FlexAlignContent.Stretch: stretchLines = true; break;
            }
        }
        float stretchExtra = stretchLines && resolved.Count > 0 ? crossFree / resolved.Count : 0f;

        foreach (var (items, lineCrossRaw) in resolved)
        {
            float lineCross = lineCrossRaw + stretchExtra;
            // Single-line layouts align items against the whole cross axis (CSS behaviour
            // when align-content does not apply); multi-line ones against their line.
            float lineCrossForItems = resolved.Count == 1 && !stretchLines ? crossSize : lineCross;

            float used = items.Sum(i => i.main);
            float remaining = Math.Max(0f, mainSize - used);
            float position = isRow ? (float)bounds.Left : (float)bounds.Top;
            float spacing = 0f;
            switch (JustifyContent)
            {
                case FlexJustify.Center: position += remaining / 2f; break;
                case FlexJustify.End: position += remaining; break;
                case FlexJustify.SpaceBetween: if (items.Count > 1) spacing = remaining / (items.Count - 1); break;
                case FlexJustify.SpaceAround: spacing = remaining / items.Count; position += spacing / 2f; break;
                case FlexJustify.SpaceEvenly: spacing = remaining / (items.Count + 1); position += spacing; break;
            }

            var ordered = isReverse ? items.AsEnumerable().Reverse() : items;
            foreach (var (child, main, cross) in ordered)
            {
                var alignSelf = GetAlignSelf(child);
                var align = alignSelf == FlexAlignSelf.Auto ? AlignItems : (FlexAlignItems)alignSelf;
                float itemCross = cross;
                float itemCrossPos = crossPos;
                switch (align)
                {
                    case FlexAlignItems.End: itemCrossPos = crossPos + lineCrossForItems - cross; break;
                    case FlexAlignItems.Center: itemCrossPos = crossPos + (lineCrossForItems - cross) / 2f; break;
                    case FlexAlignItems.Stretch: itemCross = lineCrossForItems; break;
                }

                var childBounds = isRow
                    ? new Rect(position, itemCrossPos, main, itemCross)
                    : new Rect(itemCrossPos, position, itemCross, main);
                child.Arrange(childBounds);
                position += main + spacing;
            }

            crossPos += lineCross + lineGap;
        }

        return bounds;
    }
}
