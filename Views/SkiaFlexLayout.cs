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

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        bool isRow = Direction == FlexDirection.Row || Direction == FlexDirection.RowReverse;
        float totalMain = 0f;
        float maxCross = 0f;

        foreach (var child in Children)
        {
            if (!child.IsVisible)
                continue;

            var childSize = child.Measure(availableSize);
            if (isRow)
            {
                totalMain += childSize.Width;
                maxCross = Math.Max(maxCross, childSize.Height);
            }
            else
            {
                totalMain += childSize.Height;
                maxCross = Math.Max(maxCross, childSize.Width);
            }
        }

        return isRow ? new SKSize(totalMain, maxCross) : new SKSize(maxCross, totalMain);
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        if (Children.Count == 0)
            return bounds;

        bool isRow = Direction == FlexDirection.Row || Direction == FlexDirection.RowReverse;
        bool isReverse = Direction == FlexDirection.RowReverse || Direction == FlexDirection.ColumnReverse;

        var orderedChildren = Children.Where(c => c.IsVisible).OrderBy(c => GetOrder(c)).ToList();
        if (orderedChildren.Count == 0)
            return bounds;

        float mainSize = isRow ? bounds.Width : bounds.Height;
        float crossSize = isRow ? bounds.Height : bounds.Width;

        var childInfos = new List<(SkiaView child, SKSize size, float grow, float shrink)>();
        float totalBasis = 0f;
        float totalGrow = 0f;
        float totalShrink = 0f;

        foreach (var child in orderedChildren)
        {
            var basis = GetBasis(child);
            float grow = GetGrow(child);
            float shrink = GetShrink(child);

            SKSize size;
            if (basis.IsAuto)
            {
                size = child.Measure(new SKSize(bounds.Width, bounds.Height));
            }
            else
            {
                float length = basis.Length;
                size = isRow
                    ? child.Measure(new SKSize(length, bounds.Height))
                    : child.Measure(new SKSize(bounds.Width, length));
            }

            childInfos.Add((child, size, grow, shrink));
            totalBasis += isRow ? size.Width : size.Height;
            totalGrow += grow;
            totalShrink += shrink;
        }

        float freeSpace = mainSize - totalBasis;

        var resolvedSizes = new List<(SkiaView child, float mainSize, float crossSize)>();
        foreach (var (child, size, grow, shrink) in childInfos)
        {
            float childMainSize = isRow ? size.Width : size.Height;
            float childCrossSize = isRow ? size.Height : size.Width;

            if (freeSpace > 0f && totalGrow > 0f)
            {
                childMainSize += freeSpace * (grow / totalGrow);
            }
            else if (freeSpace < 0f && totalShrink > 0f)
            {
                childMainSize += freeSpace * (shrink / totalShrink);
            }

            resolvedSizes.Add((child, Math.Max(0f, childMainSize), childCrossSize));
        }

        float usedSpace = resolvedSizes.Sum(s => s.mainSize);
        float remainingSpace = Math.Max(0f, mainSize - usedSpace);

        float position = isRow ? bounds.Left : bounds.Top;
        float spacing = 0f;

        switch (JustifyContent)
        {
            case FlexJustify.Center:
                position += remainingSpace / 2f;
                break;
            case FlexJustify.End:
                position += remainingSpace;
                break;
            case FlexJustify.SpaceBetween:
                if (resolvedSizes.Count > 1)
                    spacing = remainingSpace / (resolvedSizes.Count - 1);
                break;
            case FlexJustify.SpaceAround:
                if (resolvedSizes.Count > 0)
                {
                    spacing = remainingSpace / resolvedSizes.Count;
                    position += spacing / 2f;
                }
                break;
            case FlexJustify.SpaceEvenly:
                if (resolvedSizes.Count > 0)
                {
                    spacing = remainingSpace / (resolvedSizes.Count + 1);
                    position += spacing;
                }
                break;
        }

        var items = isReverse ? resolvedSizes.AsEnumerable().Reverse() : resolvedSizes;

        foreach (var (child, childMainSize, childCrossSize) in items)
        {
            var alignSelf = GetAlignSelf(child);
            var effectiveAlign = alignSelf == FlexAlignSelf.Auto ? AlignItems : (FlexAlignItems)alignSelf;

            float crossPos = isRow ? bounds.Top : bounds.Left;
            float finalCrossSize = childCrossSize;

            switch (effectiveAlign)
            {
                case FlexAlignItems.End:
                    crossPos = (isRow ? bounds.Bottom : bounds.Right) - finalCrossSize;
                    break;
                case FlexAlignItems.Center:
                    crossPos += (crossSize - finalCrossSize) / 2f;
                    break;
                case FlexAlignItems.Stretch:
                    finalCrossSize = crossSize;
                    break;
            }

            SKRect childBounds;
            if (isRow)
            {
                childBounds = new SKRect(position, crossPos, position + childMainSize, crossPos + finalCrossSize);
            }
            else
            {
                childBounds = new SKRect(crossPos, position, crossPos + finalCrossSize, position + childMainSize);
            }

            child.Arrange(childBounds);
            position += childMainSize + spacing;
        }

        return bounds;
    }
}
