// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Microsoft.Maui;

namespace Microsoft.Maui.Platform;

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

    protected override Size MeasureOverride(Size availableSize)
    {
        float maxRight = 0;
        float maxBottom = 0;

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            var layout = GetLayoutBounds(child);
            var bounds = layout.Bounds;

            child.Measure(new Size(bounds.Width, bounds.Height));

            maxRight = Math.Max(maxRight, bounds.Right);
            maxBottom = Math.Max(maxBottom, bounds.Bottom);
        }

        return new Size(
            maxRight + Padding.Left + Padding.Right,
            maxBottom + Padding.Top + Padding.Bottom);
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        var content = GetContentBounds(new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom));

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
                width = (float)child.DesiredSize.Width;
            else
                width = childBounds.Width;

            // Height
            if (flags.HasFlag(AbsoluteLayoutFlags.HeightProportional))
                height = childBounds.Height * content.Height;
            else if (childBounds.Height < 0)
                height = (float)child.DesiredSize.Height;
            else
                height = childBounds.Height;

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
