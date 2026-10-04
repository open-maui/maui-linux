// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Microsoft.Maui;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Absolute layout that positions Skia children at exact coordinates, for
/// views composed directly in Skia. A MAUI <c>AbsoluteLayout</c> is laid out
/// by MAUI's own AbsoluteLayoutManager on a <see cref="SkiaCrossPlatformLayout"/>
/// instead (see AbsoluteLayoutHandler).
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

    protected override Size MeasureOverride(Size availableSize)
    {
        double availableWidth = Math.Max(0, availableSize.Width - Padding.Left - Padding.Right);
        double availableHeight = Math.Max(0, availableSize.Height - Padding.Top - Padding.Bottom);
        float maxRight = 0;
        float maxBottom = 0;

        foreach (var child in Children.ToArray())
        {
            if (!child.IsVisible) continue;

            var layout = GetLayoutBounds(child);
            var bounds = layout.Bounds;
            var flags = layout.Flags;

            // Each dimension as MAUI's AbsoluteLayout measures it: a fraction of
            // the layout when proportional, the child's own size when AutoSize
            // (the handler passes AutoSize as 0), otherwise the given value.
            double width = flags.HasFlag(AbsoluteLayoutFlags.WidthProportional) ? bounds.Width * availableWidth
                : bounds.Width <= 0 ? double.PositiveInfinity : bounds.Width;
            double height = flags.HasFlag(AbsoluteLayoutFlags.HeightProportional) ? bounds.Height * availableHeight
                : bounds.Height <= 0 ? double.PositiveInfinity : bounds.Height;
            var desired = child.Measure(new Size(width, height));

            // Only absolutely placed extents grow the layout; proportional ones fill it.
            if (!flags.HasFlag(AbsoluteLayoutFlags.XProportional) && !flags.HasFlag(AbsoluteLayoutFlags.WidthProportional))
                maxRight = Math.Max(maxRight, bounds.Left + (float)(bounds.Width <= 0 ? desired.Width + child.Margin.HorizontalThickness : bounds.Width));
            if (!flags.HasFlag(AbsoluteLayoutFlags.YProportional) && !flags.HasFlag(AbsoluteLayoutFlags.HeightProportional))
                maxBottom = Math.Max(maxBottom, bounds.Top + (float)(bounds.Height <= 0 ? desired.Height + child.Margin.VerticalThickness : bounds.Height));
        }

        return new Size(
            maxRight + Padding.Left + Padding.Right,
            maxBottom + Padding.Top + Padding.Bottom);
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        var content = GetContentBounds(new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom));

        foreach (var child in Children.ToArray())
        {
            if (!child.IsVisible) continue;

            var layout = GetLayoutBounds(child);
            var childBounds = layout.Bounds;
            var flags = layout.Flags;

            float x, y, width, height;

            // Size first: MAUI defines a proportional position as a fraction of the
            // space left after the child's size ((layout - child) * fraction),
            // so 0.5 centres a child rather than placing its left edge mid-way.
            if (flags.HasFlag(AbsoluteLayoutFlags.WidthProportional))
                width = childBounds.Width * content.Width;
            else if (childBounds.Width <= 0)
                width = (float)(child.DesiredSize.Width + child.Margin.HorizontalThickness); // AutoSize: the child and its margin
            else
                width = childBounds.Width;

            if (flags.HasFlag(AbsoluteLayoutFlags.HeightProportional))
                height = childBounds.Height * content.Height;
            else if (childBounds.Height <= 0)
                height = (float)(child.DesiredSize.Height + child.Margin.VerticalThickness);
            else
                height = childBounds.Height;

            if (flags.HasFlag(AbsoluteLayoutFlags.XProportional))
                x = content.Left + childBounds.Left * Math.Max(0f, content.Width - width);
            else
                x = content.Left + childBounds.Left;

            if (flags.HasFlag(AbsoluteLayoutFlags.YProportional))
                y = content.Top + childBounds.Top * Math.Max(0f, content.Height - height);
            else
                y = content.Top + childBounds.Top;

            // Apply child's margin
            var margin = child.Margin;
            var marginedBounds = new Rect(
                x + (float)margin.Left,
                y + (float)margin.Top,
                width - (float)margin.Left - (float)margin.Right,
                height - (float)margin.Top - (float)margin.Bottom);
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
