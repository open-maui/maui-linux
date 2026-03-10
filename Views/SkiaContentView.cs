// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered container for a single content child (ContentView).
/// Measures and arranges its single child to fill the available space.
/// </summary>
public class SkiaContentView : SkiaLayoutView
{
    protected override Size MeasureOverride(Size availableSize)
    {
        // If we have explicit size, use it
        var w = WidthRequest >= 0 ? WidthRequest : availableSize.Width;
        var h = HeightRequest >= 0 ? HeightRequest : availableSize.Height;

        // Measure the single child (ContentView has one child)
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                var childSize = child.Measure(new Size(w, h));
                // If no explicit size, use child's desired size
                if (WidthRequest < 0)
                    w = Math.Max(w, childSize.Width);
                if (HeightRequest < 0)
                    h = Math.Max(h, childSize.Height);
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

        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                child.Arrange(contentBounds);
            }
        }

        return bounds;
    }
}
