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
        float totalMain = 0f;
        float maxCross = 0f;

        var sizeAvailable = new Size(availableSize.Width, availableSize.Height);

        foreach (var child in Children)
        {
            if (!child.IsVisible)
                continue;

            var childSize = child.Measure(sizeAvailable);
            if (isRow)
            {
                totalMain += (float)childSize.Width;
                maxCross = Math.Max(maxCross, (float)childSize.Height);
            }
            else
            {
                totalMain += (float)childSize.Height;
                maxCross = Math.Max(maxCross, (float)childSize.Width);
            }
        }

        return isRow ? new Size(totalMain, maxCross) : new Size(maxCross, totalMain);
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        if (Children.Count == 0)
            return bounds;

        bool isRow = Direction == FlexDirection.Row || Direction == FlexDirection.RowReverse;
        bool isReverse = Direction == FlexDirection.RowReverse || Direction == FlexDirection.ColumnReverse;

        var orderedChildren = Children.Where(c => c.IsVisible).OrderBy(c => GetOrder(c)).ToList();
        if (orderedChildren.Count == 0)
            return bounds;

        float mainSize = isRow ? (float)bounds.Width : (float)bounds.Height;
        float crossSize = isRow ? (float)bounds.Height : (float)bounds.Width;

        var childInfos = new List<(SkiaView child, Size size, float grow, float shrink)>();
        float totalBasis = 0f;
        float totalGrow = 0f;
        float totalShrink = 0f;

        foreach (var child in orderedChildren)
        {
            var basis = GetBasis(child);
            float grow = GetGrow(child);
            float shrink = GetShrink(child);

            Size size;
            if (basis.IsAuto)
            {
                size = child.Measure(new Size(bounds.Width, bounds.Height));
            }
            else
            {
                float length = basis.Length;
                size = isRow
                    ? child.Measure(new Size(length, bounds.Height))
                    : child.Measure(new Size(bounds.Width, length));
            }

            childInfos.Add((child, size, grow, shrink));
            totalBasis += isRow ? (float)size.Width : (float)size.Height;
            totalGrow += grow;
            totalShrink += shrink;
        }

        float freeSpace = mainSize - totalBasis;

        var resolvedSizes = new List<(SkiaView child, float mainSize, float crossSize)>();
        foreach (var (child, size, grow, shrink) in childInfos)
        {
            float childMainSize = isRow ? (float)size.Width : (float)size.Height;
            float childCrossSize = isRow ? (float)size.Height : (float)size.Width;

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

        float position = isRow ? (float)bounds.Left : (float)bounds.Top;
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

            float crossPos = isRow ? (float)bounds.Top : (float)bounds.Left;
            float finalCrossSize = childCrossSize;

            switch (effectiveAlign)
            {
                case FlexAlignItems.End:
                    crossPos = (isRow ? (float)bounds.Bottom : (float)bounds.Right) - finalCrossSize;
                    break;
                case FlexAlignItems.Center:
                    crossPos += (crossSize - finalCrossSize) / 2f;
                    break;
                case FlexAlignItems.Stretch:
                    finalCrossSize = crossSize;
                    break;
            }

            Rect childBounds;
            if (isRow)
            {
                childBounds = new Rect(position, crossPos, childMainSize, finalCrossSize);
            }
            else
            {
                childBounds = new Rect(crossPos, position, finalCrossSize, childMainSize);
            }

            child.Arrange(childBounds);
            position += childMainSize + spacing;
        }

        return bounds;
    }
}
