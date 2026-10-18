using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Flexbox layout for Skia children composed directly in Skia. A MAUI
/// <c>FlexLayout</c> is laid out by MAUI's own FlexLayoutManager on a
/// <see cref="SkiaCrossPlatformLayout"/> instead (see FlexLayoutHandler).
/// </summary>
public class SkiaFlexLayout : SkiaLayoutView
{
#pragma warning disable CS0618 // legacy type kept for binary compatibility until OpenMaui 11.0
    public static readonly BindableProperty DirectionProperty = BindableProperty.Create(
        nameof(Direction), typeof(FlexDirection), typeof(SkiaFlexLayout), FlexDirection.Row,
        BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaFlexLayout)b).InvalidateMeasure());
#pragma warning restore CS0618

#pragma warning disable CS0618 // legacy type kept for binary compatibility until OpenMaui 11.0
    public static readonly BindableProperty WrapProperty = BindableProperty.Create(
        nameof(Wrap), typeof(FlexWrap), typeof(SkiaFlexLayout), FlexWrap.NoWrap,
        BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaFlexLayout)b).InvalidateMeasure());
#pragma warning restore CS0618

#pragma warning disable CS0618 // legacy type kept for binary compatibility until OpenMaui 11.0
    public static readonly BindableProperty JustifyContentProperty = BindableProperty.Create(
        nameof(JustifyContent), typeof(FlexJustify), typeof(SkiaFlexLayout), FlexJustify.Start,
        BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaFlexLayout)b).InvalidateMeasure());
#pragma warning restore CS0618

#pragma warning disable CS0618 // legacy type kept for binary compatibility until OpenMaui 11.0
    public static readonly BindableProperty AlignItemsProperty = BindableProperty.Create(
        nameof(AlignItems), typeof(FlexAlignItems), typeof(SkiaFlexLayout), FlexAlignItems.Stretch,
        BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaFlexLayout)b).InvalidateMeasure());
#pragma warning restore CS0618

#pragma warning disable CS0618 // legacy type kept for binary compatibility until OpenMaui 11.0
    public static readonly BindableProperty AlignContentProperty = BindableProperty.Create(
        nameof(AlignContent), typeof(FlexAlignContent), typeof(SkiaFlexLayout), FlexAlignContent.Stretch,
        BindingMode.TwoWay, propertyChanged: (b, o, n) => ((SkiaFlexLayout)b).InvalidateMeasure());
#pragma warning restore CS0618

    public static readonly BindableProperty OrderProperty = BindableProperty.CreateAttached(
        "Order", typeof(int), typeof(SkiaFlexLayout), 0, BindingMode.TwoWay);

    public static readonly BindableProperty GrowProperty = BindableProperty.CreateAttached(
        "Grow", typeof(float), typeof(SkiaFlexLayout), 0f, BindingMode.TwoWay);

    public static readonly BindableProperty ShrinkProperty = BindableProperty.CreateAttached(
        "Shrink", typeof(float), typeof(SkiaFlexLayout), 1f, BindingMode.TwoWay);

#pragma warning disable CS0618 // legacy type kept for binary compatibility until OpenMaui 11.0
    public static readonly BindableProperty BasisProperty = BindableProperty.CreateAttached(
        "Basis", typeof(FlexBasis), typeof(SkiaFlexLayout), FlexBasis.Auto, BindingMode.TwoWay);
#pragma warning restore CS0618

#pragma warning disable CS0618 // legacy type kept for binary compatibility until OpenMaui 11.0
    public static readonly BindableProperty AlignSelfProperty = BindableProperty.CreateAttached(
        "AlignSelf", typeof(FlexAlignSelf), typeof(SkiaFlexLayout), FlexAlignSelf.Auto, BindingMode.TwoWay);
#pragma warning restore CS0618

#pragma warning disable CS0618 // legacy type kept for binary compatibility until OpenMaui 11.0
    [Obsolete("Use MauiDirection, which takes Microsoft.Maui.Layouts.FlexDirection; this property is removed in OpenMaui 11.0.")]
    public FlexDirection Direction
    {
        get => (FlexDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public Layouts.FlexDirection MauiDirection
    {
        get => ((FlexDirection)GetValue(DirectionProperty)).ToMaui();
        set => SetValue(DirectionProperty, value.ToLegacy());
    }
#pragma warning restore CS0618

#pragma warning disable CS0618 // legacy type kept for binary compatibility until OpenMaui 11.0
    [Obsolete("Use MauiWrap, which takes Microsoft.Maui.Layouts.FlexWrap; this property is removed in OpenMaui 11.0.")]
    public FlexWrap Wrap
    {
        get => (FlexWrap)GetValue(WrapProperty);
        set => SetValue(WrapProperty, value);
    }

    public Layouts.FlexWrap MauiWrap
    {
        get => ((FlexWrap)GetValue(WrapProperty)).ToMaui();
        set => SetValue(WrapProperty, value.ToLegacy());
    }
#pragma warning restore CS0618

#pragma warning disable CS0618 // legacy type kept for binary compatibility until OpenMaui 11.0
    [Obsolete("Use MauiJustifyContent, which takes Microsoft.Maui.Layouts.FlexJustify; this property is removed in OpenMaui 11.0.")]
    public FlexJustify JustifyContent
    {
        get => (FlexJustify)GetValue(JustifyContentProperty);
        set => SetValue(JustifyContentProperty, value);
    }

    public Layouts.FlexJustify MauiJustifyContent
    {
        get => ((FlexJustify)GetValue(JustifyContentProperty)).ToMaui();
        set => SetValue(JustifyContentProperty, value.ToLegacy());
    }
#pragma warning restore CS0618

#pragma warning disable CS0618 // legacy type kept for binary compatibility until OpenMaui 11.0
    [Obsolete("Use MauiAlignItems, which takes Microsoft.Maui.Layouts.FlexAlignItems; this property is removed in OpenMaui 11.0.")]
    public FlexAlignItems AlignItems
    {
        get => (FlexAlignItems)GetValue(AlignItemsProperty);
        set => SetValue(AlignItemsProperty, value);
    }

    public Layouts.FlexAlignItems MauiAlignItems
    {
        get => ((FlexAlignItems)GetValue(AlignItemsProperty)).ToMaui();
        set => SetValue(AlignItemsProperty, value.ToLegacy());
    }
#pragma warning restore CS0618

#pragma warning disable CS0618 // legacy type kept for binary compatibility until OpenMaui 11.0
    [Obsolete("Use MauiAlignContent, which takes Microsoft.Maui.Layouts.FlexAlignContent; this property is removed in OpenMaui 11.0.")]
    public FlexAlignContent AlignContent
    {
        get => (FlexAlignContent)GetValue(AlignContentProperty);
        set => SetValue(AlignContentProperty, value);
    }

    public Layouts.FlexAlignContent MauiAlignContent
    {
        get => ((FlexAlignContent)GetValue(AlignContentProperty)).ToMaui();
        set => SetValue(AlignContentProperty, value.ToLegacy());
    }
#pragma warning restore CS0618

    public static int GetOrder(SkiaView view) => (int)view.GetValue(OrderProperty);
    public static void SetOrder(SkiaView view, int value) => view.SetValue(OrderProperty, value);

    public static float GetGrow(SkiaView view) => (float)view.GetValue(GrowProperty);
    public static void SetGrow(SkiaView view, float value) => view.SetValue(GrowProperty, value);

    public static float GetShrink(SkiaView view) => (float)view.GetValue(ShrinkProperty);
    public static void SetShrink(SkiaView view, float value) => view.SetValue(ShrinkProperty, value);

#pragma warning disable CS0618 // legacy type kept for binary compatibility until OpenMaui 11.0
    [Obsolete("Use GetMauiBasis, which returns Microsoft.Maui.Layouts.FlexBasis; this method is removed in OpenMaui 11.0.")]
    public static FlexBasis GetBasis(SkiaView view) => (FlexBasis)view.GetValue(BasisProperty);
    [Obsolete("Use the overload that takes Microsoft.Maui.Layouts.FlexBasis; this overload is removed in OpenMaui 11.0.")]
    public static void SetBasis(SkiaView view, FlexBasis value) => view.SetValue(BasisProperty, value);

    [Obsolete("Use GetMauiAlignSelf, which returns Microsoft.Maui.Layouts.FlexAlignSelf; this method is removed in OpenMaui 11.0.")]
    public static FlexAlignSelf GetAlignSelf(SkiaView view) => (FlexAlignSelf)view.GetValue(AlignSelfProperty);
    [Obsolete("Use the overload that takes Microsoft.Maui.Layouts.FlexAlignSelf; this overload is removed in OpenMaui 11.0.")]
    public static void SetAlignSelf(SkiaView view, FlexAlignSelf value) => view.SetValue(AlignSelfProperty, value);

    /// <summary>The flex basis of a child: Auto (its measured size) or an absolute length.</summary>
    public static Layouts.FlexBasis GetMauiBasis(SkiaView view) => ((FlexBasis)view.GetValue(BasisProperty)).ToMaui();

    /// <summary>
    /// Sets the flex basis of a child: Auto (its measured size) or an absolute length.
    /// A relative basis is not supported by this Skia-composed layout and is treated as Auto
    /// (MAUI's FlexLayout, laid out by MAUI's own manager, supports it).
    /// </summary>
    public static void SetBasis(SkiaView view, Layouts.FlexBasis value) => view.SetValue(BasisProperty, value.ToLegacy());

    /// <summary>The cross-axis alignment of a child; Auto follows <see cref="MauiAlignItems"/>.</summary>
    public static Layouts.FlexAlignSelf GetMauiAlignSelf(SkiaView view) => ((FlexAlignSelf)view.GetValue(AlignSelfProperty)).ToMaui();

    /// <summary>Sets the cross-axis alignment of a child; Auto follows <see cref="MauiAlignItems"/>.</summary>
    public static void SetAlignSelf(SkiaView view, Layouts.FlexAlignSelf value) => view.SetValue(AlignSelfProperty, value.ToLegacy());
#pragma warning restore CS0618

    protected override Size MeasureOverride(Size availableSize)
    {
        bool isRow = MauiDirection == Layouts.FlexDirection.Row || MauiDirection == Layouts.FlexDirection.RowReverse;
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
        bool wrap = MauiWrap != Layouts.FlexWrap.NoWrap && !float.IsInfinity(mainLimit) && !float.IsNaN(mainLimit);

        var current = new List<FlexItem>();
        float used = 0f;
        foreach (var child in ordered)
        {
            var basis = GetMauiBasis(child);
            // An item's slot is its size plus its margin (chips spaced by a trailing margin
            // ran together); the child is measured inside the margin.
            var margin = child.Margin;
            var inner = new Size(Math.Max(0, available.Width - margin.HorizontalThickness), Math.Max(0, available.Height - margin.VerticalThickness));
            Size size;
            if (basis.Equals(Layouts.FlexBasis.Auto))
                size = child.Measure(inner);
            else
                size = isRow ? child.Measure(new Size(basis.Length, inner.Height)) : child.Measure(new Size(inner.Width, basis.Length));

            var item = new FlexItem(child,
                (float)(isRow ? size.Width + margin.HorizontalThickness : size.Height + margin.VerticalThickness),
                (float)(isRow ? size.Height + margin.VerticalThickness : size.Width + margin.HorizontalThickness),
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

        bool isRow = MauiDirection == Layouts.FlexDirection.Row || MauiDirection == Layouts.FlexDirection.RowReverse;
        bool isReverse = MauiDirection == Layouts.FlexDirection.RowReverse || MauiDirection == Layouts.FlexDirection.ColumnReverse;
        float mainSize = isRow ? (float)bounds.Width : (float)bounds.Height;
        float crossSize = isRow ? (float)bounds.Height : (float)bounds.Width;

        var lines = BuildLines(new Size(bounds.Width, bounds.Height), isRow);
        if (lines.Count == 0)
            return bounds;
        if (MauiWrap == Layouts.FlexWrap.Reverse)
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
        if (resolved.Count > 1 || MauiAlignContent == Layouts.FlexAlignContent.Stretch)
        {
            switch (MauiAlignContent)
            {
                case Layouts.FlexAlignContent.Center: crossPos += crossFree / 2f; break;
                case Layouts.FlexAlignContent.End: crossPos += crossFree; break;
                case Layouts.FlexAlignContent.SpaceBetween: if (resolved.Count > 1) lineGap = crossFree / (resolved.Count - 1); break;
                case Layouts.FlexAlignContent.SpaceAround: lineGap = crossFree / resolved.Count; crossPos += lineGap / 2f; break;
                case Layouts.FlexAlignContent.SpaceEvenly: lineGap = crossFree / (resolved.Count + 1); crossPos += lineGap; break;
                case Layouts.FlexAlignContent.Stretch: stretchLines = true; break;
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
            switch (MauiJustifyContent)
            {
                case Layouts.FlexJustify.Center: position += remaining / 2f; break;
                case Layouts.FlexJustify.End: position += remaining; break;
                case Layouts.FlexJustify.SpaceBetween: if (items.Count > 1) spacing = remaining / (items.Count - 1); break;
                case Layouts.FlexJustify.SpaceAround: spacing = remaining / items.Count; position += spacing / 2f; break;
                case Layouts.FlexJustify.SpaceEvenly: spacing = remaining / (items.Count + 1); position += spacing; break;
            }

            var ordered = isReverse ? items.AsEnumerable().Reverse() : items;
            foreach (var (child, main, cross) in ordered)
            {
                var alignSelf = GetMauiAlignSelf(child);
                var align = alignSelf == Layouts.FlexAlignSelf.Auto ? MauiAlignItems : (Layouts.FlexAlignItems)alignSelf;
                float itemCross = cross;
                float itemCrossPos = crossPos;
                switch (align)
                {
                    case Layouts.FlexAlignItems.End: itemCrossPos = crossPos + lineCrossForItems - cross; break;
                    case Layouts.FlexAlignItems.Center: itemCrossPos = crossPos + (lineCrossForItems - cross) / 2f; break;
                    case Layouts.FlexAlignItems.Stretch: itemCross = lineCrossForItems; break;
                }

                var childBounds = isRow
                    ? new Rect(position, itemCrossPos, main, itemCross)
                    : new Rect(itemCrossPos, position, itemCross, main);
                var m = child.Margin;
                child.Arrange(new Rect(childBounds.X + m.Left, childBounds.Y + m.Top,
                    Math.Max(0, childBounds.Width - m.HorizontalThickness), Math.Max(0, childBounds.Height - m.VerticalThickness)));
                position += main + spacing;
            }

            crossPos += lineCross + lineGap;
        }

        return bounds;
    }
}
