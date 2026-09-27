// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered container for a single content child (ContentView).
/// Measures and arranges its single child within the available space by its layout options.
/// </summary>
public class SkiaContentView : SkiaLayoutView
{
    protected override Size MeasureOverride(Size availableSize)
    {
        // If we have explicit size, use it; otherwise accumulate from children
        var w = WidthRequest >= 0 ? WidthRequest : 0.0;
        var h = HeightRequest >= 0 ? HeightRequest : 0.0;

        // Available size for child measurement (may be infinite from stack layouts)
        var childAvailableW = WidthRequest >= 0 ? WidthRequest : availableSize.Width;
        var childAvailableH = HeightRequest >= 0 ? HeightRequest : availableSize.Height;

        // The content sits inside the padding and its own margin, as in MAUI;
        // both count towards the size (a header stack's top margin clearing
        // a toggle above it was dropped).
        var inset = new Thickness(Padding.Left, Padding.Top, Padding.Right, Padding.Bottom);

        // Measure the single child (ContentView has one child)
        foreach (var child in Children.ToArray())
        {
            if (child.IsVisible)
            {
                var margin = child.Margin;
                var childSize = child.Measure(new Size(
                    Math.Max(0, childAvailableW - inset.HorizontalThickness - margin.HorizontalThickness),
                    Math.Max(0, childAvailableH - inset.VerticalThickness - margin.VerticalThickness)));
                // If no explicit size, use child's desired size
                if (WidthRequest < 0)
                    w = Math.Max(w, childSize.Width + margin.HorizontalThickness + inset.HorizontalThickness);
                if (HeightRequest < 0)
                    h = Math.Max(h, childSize.Height + margin.VerticalThickness + inset.VerticalThickness);
            }
        }

        // Clamp infinities
        if (double.IsInfinity(w) || double.IsNaN(w)) w = 0;
        if (double.IsInfinity(h) || double.IsNaN(h)) h = 0;

        return new Size(w, h);
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        // Arrange the single child to fill the content area
        var contentBounds = new Rect(
            bounds.X + Padding.Left,
            bounds.Y + Padding.Top,
            Math.Max(0, bounds.Width - Padding.Left - Padding.Right),
            Math.Max(0, bounds.Height - Padding.Top - Padding.Bottom));

        foreach (var child in Children.ToArray())
        {
            if (child.IsVisible)
            {
                // Inside the content's margin, then its layout options, as in MAUI.
                var margin = child.Margin;
                var area = new SKRect(
                    (float)(contentBounds.Left + margin.Left),
                    (float)(contentBounds.Top + margin.Top),
                    (float)Math.Max(contentBounds.Left + margin.Left, contentBounds.Right - margin.Right),
                    (float)Math.Max(contentBounds.Top + margin.Top, contentBounds.Bottom - margin.Bottom));
                var desired = child.Measure(new Size(area.Width, area.Height));
                child.Arrange(SkiaPage.AlignContent(child, area, desired));
            }
        }

        return bounds;
    }
}
