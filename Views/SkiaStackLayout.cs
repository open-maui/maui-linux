// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Microsoft.Maui;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Stack layout that arranges children in a horizontal or vertical line.
/// </summary>
public class SkiaStackLayout : SkiaLayoutView
{
    /// <summary>
    /// Bindable property for Orientation.
    /// </summary>
    public static readonly BindableProperty OrientationProperty =
        BindableProperty.Create(
            nameof(Orientation),
            typeof(StackOrientation),
            typeof(SkiaStackLayout),
            StackOrientation.Vertical,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaStackLayout)b).InvalidateMeasure());

    /// <summary>
    /// Gets or sets the orientation of the stack.
    /// </summary>
    public StackOrientation Orientation
    {
        get => (StackOrientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Handle NaN/Infinity in padding
        var paddingLeft = (float)(double.IsNaN(Padding.Left) ? 0 : Padding.Left);
        var paddingRight = (float)(double.IsNaN(Padding.Right) ? 0 : Padding.Right);
        var paddingTop = (float)(double.IsNaN(Padding.Top) ? 0 : Padding.Top);
        var paddingBottom = (float)(double.IsNaN(Padding.Bottom) ? 0 : Padding.Bottom);

        var contentWidth = (float)availableSize.Width - paddingLeft - paddingRight;
        var contentHeight = (float)availableSize.Height - paddingTop - paddingBottom;

        // Clamp negative sizes to 0
        if (contentWidth < 0 || float.IsNaN(contentWidth)) contentWidth = 0;
        if (contentHeight < 0 || float.IsNaN(contentHeight)) contentHeight = 0;

        float totalWidth = 0;
        float totalHeight = 0;
        float maxWidth = 0;
        float maxHeight = 0;

        // For stack layouts, give children infinite size in the stacking direction
        // so they can measure to their natural size
        var childAvailable = Orientation == StackOrientation.Horizontal
            ? new Size(double.PositiveInfinity, contentHeight)  // Horizontal: infinite width, constrained height
            : new Size(contentWidth, double.PositiveInfinity);  // Vertical: constrained width, infinite height

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            var childSize = child.Measure(childAvailable);

            // Skip NaN sizes from child measurements
            var childWidth = double.IsNaN(childSize.Width) ? 0f : (float)childSize.Width;
            var childHeight = double.IsNaN(childSize.Height) ? 0f : (float)childSize.Height;

            if (Orientation == StackOrientation.Vertical)
            {
                totalHeight += childHeight;
                maxWidth = Math.Max(maxWidth, childWidth);
            }
            else
            {
                totalWidth += childWidth;
                maxHeight = Math.Max(maxHeight, childHeight);
            }
        }

        // Add spacing
        var visibleCount = Children.Count(c => c.IsVisible);
        var totalSpacing = (float)(Math.Max(0, visibleCount - 1) * Spacing);

        if (Orientation == StackOrientation.Vertical)
        {
            totalHeight += totalSpacing;
            return new Size(
                maxWidth + paddingLeft + paddingRight,
                totalHeight + paddingTop + paddingBottom);
        }
        else
        {
            totalWidth += totalSpacing;
            return new Size(
                totalWidth + paddingLeft + paddingRight,
                maxHeight + paddingTop + paddingBottom);
        }
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        var content = GetContentBounds(new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom));

        // Clamp content dimensions if infinite - use reasonable defaults
        var contentWidth = float.IsInfinity(content.Width) || float.IsNaN(content.Width) ? 800f : content.Width;
        var contentHeight = float.IsInfinity(content.Height) || float.IsNaN(content.Height) ? 600f : content.Height;

        float offset = 0;

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            var childDesired = child.DesiredSize;

            // Handle NaN and Infinity in desired size
            var childWidth = double.IsNaN(childDesired.Width) || double.IsInfinity(childDesired.Width)
                ? contentWidth
                : (float)childDesired.Width;
            var childHeight = double.IsNaN(childDesired.Height) || double.IsInfinity(childDesired.Height)
                ? contentHeight
                : (float)childDesired.Height;

            float childBoundsLeft, childBoundsTop, childBoundsWidth, childBoundsHeight;
            if (Orientation == StackOrientation.Vertical)
            {
                // For ScrollView children, give them the remaining viewport height
                // Clamp to avoid giving them their content size
                var remainingHeight = Math.Max(0, contentHeight - offset);
                var useHeight = child is SkiaScrollView
                    ? remainingHeight
                    : Math.Min(childHeight, remainingHeight > 0 ? remainingHeight : childHeight);

                // Respect child's HorizontalOptions for vertical layouts
                var useWidth = Math.Min(childWidth, contentWidth);
                float childLeft = content.Left;

                var horizontalOptions = child.HorizontalOptions;
                var alignmentValue = (int)horizontalOptions.Alignment;

                // LayoutAlignment: Start=0, Center=1, End=2, Fill=3
                if (alignmentValue == 1) // Center
                {
                    childLeft = content.Left + (contentWidth - useWidth) / 2;
                }
                else if (alignmentValue == 2) // End
                {
                    childLeft = content.Left + contentWidth - useWidth;
                }
                else if (alignmentValue == 3) // Fill
                {
                    useWidth = contentWidth;
                }

                childBoundsLeft = childLeft;
                childBoundsTop = content.Top + offset;
                childBoundsWidth = useWidth;
                childBoundsHeight = useHeight;
                offset += useHeight + (float)Spacing;
            }
            else
            {
                // Horizontal stack: give each child its measured width
                // Don't constrain - let content overflow if needed (parent clips)
                var useWidth = childWidth;

                // Respect child's VerticalOptions for horizontal layouts
                var useHeight = Math.Min(childHeight, contentHeight);
                float childTop = content.Top;
                float childBottomCalc = content.Top + useHeight;

                var verticalOptions = child.VerticalOptions;
                var alignmentValue = (int)verticalOptions.Alignment;

                // LayoutAlignment: Start=0, Center=1, End=2, Fill=3
                if (alignmentValue == 1) // Center
                {
                    childTop = content.Top + (contentHeight - useHeight) / 2;
                    childBottomCalc = childTop + useHeight;
                }
                else if (alignmentValue == 2) // End
                {
                    childTop = content.Top + contentHeight - useHeight;
                    childBottomCalc = content.Top + contentHeight;
                }
                else if (alignmentValue == 3) // Fill
                {
                    childTop = content.Top;
                    childBottomCalc = content.Top + contentHeight;
                }

                childBoundsLeft = content.Left + offset;
                childBoundsTop = childTop;
                childBoundsWidth = useWidth;
                childBoundsHeight = childBottomCalc - childTop;
                offset += useWidth + (float)Spacing;
            }

            // Apply child's margin
            var margin = child.Margin;
            var marginedBounds = new Rect(
                childBoundsLeft + (float)margin.Left,
                childBoundsTop + (float)margin.Top,
                childBoundsWidth - (float)margin.Left - (float)margin.Right,
                childBoundsHeight - (float)margin.Top - (float)margin.Bottom);
            child.Arrange(marginedBounds);
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
