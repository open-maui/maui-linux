// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// Minimal concrete SkiaView that uses base MeasureOverride (respects WidthRequest/HeightRequest).
/// SkiaLabel overrides MeasureOverride to use font metrics, so we need this for layout tests.
/// </summary>
internal class TestView : SkiaView
{
    protected override void OnDraw(SkiaSharp.SKCanvas canvas, SkiaSharp.SKRect bounds) { }
}

#region StackLayoutIntegrationTests

public class StackLayoutIntegrationTests
{
    [Fact]
    public void VerticalStack_MeasuresChildrenSequentially()
    {
        // Arrange
        var stack = new SkiaStackLayout
        {
            Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical
        };

        var child1 = new TestView { HeightRequest = 30, WidthRequest = 100 };
        var child2 = new TestView { HeightRequest = 30, WidthRequest = 100 };
        var child3 = new TestView { HeightRequest = 30, WidthRequest = 100 };

        stack.AddChild(child1);
        stack.AddChild(child2);
        stack.AddChild(child3);

        // Act
        var size = stack.Measure(new Size(400, 600));

        // Assert - 3 children each 30px tall => total height >= 90
        size.Height.Should().BeGreaterThanOrEqualTo(90);
    }

    [Fact]
    public void HorizontalStack_MeasuresChildrenSideToSide()
    {
        // Arrange
        var stack = new SkiaStackLayout
        {
            Orientation = Microsoft.Maui.Platform.StackOrientation.Horizontal
        };

        var child1 = new TestView { WidthRequest = 50, HeightRequest = 30 };
        var child2 = new TestView { WidthRequest = 50, HeightRequest = 30 };
        var child3 = new TestView { WidthRequest = 50, HeightRequest = 30 };

        stack.AddChild(child1);
        stack.AddChild(child2);
        stack.AddChild(child3);

        // Act
        var size = stack.Measure(new Size(600, 400));

        // Assert - 3 children each 50px wide => total width >= 150
        size.Width.Should().BeGreaterThanOrEqualTo(150);
    }

    [Fact]
    public void VerticalStack_ArrangePositionsChildrenVertically()
    {
        // Arrange
        var stack = new SkiaStackLayout
        {
            Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical
        };

        var child1 = new TestView { HeightRequest = 40, WidthRequest = 100 };
        var child2 = new TestView { HeightRequest = 40, WidthRequest = 100 };
        var child3 = new TestView { HeightRequest = 40, WidthRequest = 100 };

        stack.AddChild(child1);
        stack.AddChild(child2);
        stack.AddChild(child3);

        stack.Measure(new Size(400, 600));

        // Act
        stack.Arrange(new Rect(0, 0, 400, 600));

        // Assert - children should have increasing Y positions
        child2.Bounds.Top.Should().BeGreaterThan(child1.Bounds.Top);
        child3.Bounds.Top.Should().BeGreaterThan(child2.Bounds.Top);
    }

    [Fact]
    public void HorizontalStack_ArrangePositionsChildrenHorizontally()
    {
        // Arrange
        var stack = new SkiaStackLayout
        {
            Orientation = Microsoft.Maui.Platform.StackOrientation.Horizontal
        };

        var child1 = new TestView { WidthRequest = 60, HeightRequest = 30 };
        var child2 = new TestView { WidthRequest = 60, HeightRequest = 30 };
        var child3 = new TestView { WidthRequest = 60, HeightRequest = 30 };

        stack.AddChild(child1);
        stack.AddChild(child2);
        stack.AddChild(child3);

        stack.Measure(new Size(600, 400));

        // Act
        stack.Arrange(new Rect(0, 0, 600, 400));

        // Assert - children should have increasing X positions
        child2.Bounds.Left.Should().BeGreaterThan(child1.Bounds.Left);
        child3.Bounds.Left.Should().BeGreaterThan(child2.Bounds.Left);
    }

    [Fact]
    public void NestedStacks_MeasureCorrectly()
    {
        // Arrange
        var outerStack = new SkiaStackLayout
        {
            Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical
        };

        var innerStack = new SkiaStackLayout
        {
            Orientation = Microsoft.Maui.Platform.StackOrientation.Horizontal
        };

        var innerChild1 = new TestView { WidthRequest = 50, HeightRequest = 30 };
        var innerChild2 = new TestView { WidthRequest = 50, HeightRequest = 30 };

        innerStack.AddChild(innerChild1);
        innerStack.AddChild(innerChild2);

        var outerChild = new TestView { WidthRequest = 100, HeightRequest = 40 };

        outerStack.AddChild(innerStack);
        outerStack.AddChild(outerChild);

        // Act
        var size = outerStack.Measure(new Size(400, 600));

        // Assert - should measure without error and produce positive size
        size.Width.Should().BeGreaterThan(0);
        size.Height.Should().BeGreaterThan(0);
        // Height should include both the inner stack's height and the outer child
        size.Height.Should().BeGreaterThanOrEqualTo(30 + 40);
    }

    [Fact]
    public void InvisibleChildren_ExcludedFromLayout()
    {
        // Arrange
        var stack = new SkiaStackLayout
        {
            Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical
        };

        var child1 = new TestView { HeightRequest = 30, WidthRequest = 100 };
        var child2 = new TestView { HeightRequest = 30, WidthRequest = 100, IsVisible = false };
        var child3 = new TestView { HeightRequest = 30, WidthRequest = 100 };

        stack.AddChild(child1);
        stack.AddChild(child2);
        stack.AddChild(child3);

        // Act
        var size = stack.Measure(new Size(400, 600));

        // Assert - only 2 visible children, so height should reflect only 2 x 30 = 60
        // The invisible child should not contribute to measured height
        size.Height.Should().BeLessThan(90);
        size.Height.Should().BeGreaterThanOrEqualTo(60);
    }

    [Fact]
    public void Spacing_AppliedBetweenVisibleChildrenOnly()
    {
        // Arrange - 3 children with spacing=10, middle one invisible
        var stack = new SkiaStackLayout
        {
            Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical,
            Spacing = 10
        };

        var child1 = new TestView { HeightRequest = 30, WidthRequest = 100 };
        var child2 = new TestView { HeightRequest = 30, WidthRequest = 100, IsVisible = false };
        var child3 = new TestView { HeightRequest = 30, WidthRequest = 100 };

        stack.AddChild(child1);
        stack.AddChild(child2);
        stack.AddChild(child3);

        // Act
        var size = stack.Measure(new Size(400, 600));

        // Assert - 2 visible children with 1 gap of spacing=10
        // Expected: 30 + 10 + 30 = 70
        size.Height.Should().Be(70);
    }
}

#endregion

#region GridIntegrationTests

public class GridIntegrationTests
{
    [Fact]
    public void SingleCell_MeasuresChild()
    {
        // Arrange
        var grid = new SkiaGrid();
        grid.RowDefinitions.Add(Microsoft.Maui.Platform.GridLength.Auto);
        grid.ColumnDefinitions.Add(Microsoft.Maui.Platform.GridLength.Auto);

        var button = new TestView { WidthRequest = 80, HeightRequest = 40 };
        grid.AddChild(button, row: 0, column: 0);

        // Act
        var size = grid.Measure(new Size(400, 300));

        // Assert - grid should measure to at least the child's requested size
        size.Width.Should().BeGreaterThanOrEqualTo(80);
        size.Height.Should().BeGreaterThanOrEqualTo(40);
    }

    [Fact]
    public void TwoRows_ArrangePositionsCorrectly()
    {
        // Arrange
        var grid = new SkiaGrid();
        grid.RowDefinitions.Add(new Microsoft.Maui.Platform.GridLength(50));
        grid.RowDefinitions.Add(new Microsoft.Maui.Platform.GridLength(50));
        grid.ColumnDefinitions.Add(Microsoft.Maui.Platform.GridLength.Star);

        var child1 = new TestView { WidthRequest = 100, HeightRequest = 30 };
        var child2 = new TestView { WidthRequest = 100, HeightRequest = 30 };

        grid.AddChild(child1, row: 0, column: 0);
        grid.AddChild(child2, row: 1, column: 0);

        grid.Measure(new Size(400, 300));

        // Act
        grid.Arrange(new Rect(0, 0, 400, 300));

        // Assert - second row child should be at Y >= 50
        child2.Bounds.Top.Should().BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public void TwoColumns_ArrangePositionsCorrectly()
    {
        // Arrange
        var grid = new SkiaGrid();
        grid.RowDefinitions.Add(Microsoft.Maui.Platform.GridLength.Star);
        grid.ColumnDefinitions.Add(new Microsoft.Maui.Platform.GridLength(100));
        grid.ColumnDefinitions.Add(new Microsoft.Maui.Platform.GridLength(100));

        var child1 = new TestView { WidthRequest = 80, HeightRequest = 30 };
        var child2 = new TestView { WidthRequest = 80, HeightRequest = 30 };

        grid.AddChild(child1, row: 0, column: 0);
        grid.AddChild(child2, row: 0, column: 1);

        grid.Measure(new Size(400, 300));

        // Act
        grid.Arrange(new Rect(0, 0, 400, 300));

        // Assert - second column child should be at X >= 100
        child2.Bounds.Left.Should().BeGreaterThanOrEqualTo(100);
    }

    [Fact]
    public void EmptyGrid_MeasuresWithDefinitions()
    {
        // Arrange
        var grid = new SkiaGrid();
        grid.RowDefinitions.Add(new Microsoft.Maui.Platform.GridLength(60));
        grid.RowDefinitions.Add(new Microsoft.Maui.Platform.GridLength(40));
        grid.ColumnDefinitions.Add(new Microsoft.Maui.Platform.GridLength(120));
        grid.ColumnDefinitions.Add(new Microsoft.Maui.Platform.GridLength(80));

        // Act - no children added
        var size = grid.Measure(new Size(400, 300));

        // Assert - should still measure based on definitions
        // Width = 120 + 80 = 200, Height = 60 + 40 = 100
        size.Width.Should().BeGreaterThanOrEqualTo(200);
        size.Height.Should().BeGreaterThanOrEqualTo(100);
    }

    [Fact]
    public void RowSpacing_IncludedInMeasure()
    {
        // Arrange
        var grid = new SkiaGrid { RowSpacing = 20 };
        grid.RowDefinitions.Add(new Microsoft.Maui.Platform.GridLength(50));
        grid.RowDefinitions.Add(new Microsoft.Maui.Platform.GridLength(50));
        grid.ColumnDefinitions.Add(Microsoft.Maui.Platform.GridLength.Star);

        var child1 = new TestView { WidthRequest = 100, HeightRequest = 30 };
        var child2 = new TestView { WidthRequest = 100, HeightRequest = 30 };

        grid.AddChild(child1, row: 0, column: 0);
        grid.AddChild(child2, row: 1, column: 0);

        // Act
        var size = grid.Measure(new Size(400, 300));

        // Assert - height should include row spacing: 50 + 20 + 50 = 120
        size.Height.Should().BeGreaterThanOrEqualTo(120);
    }
}

#endregion

#region MeasureArrangePipelineTests

public class MeasureArrangePipelineTests
{
    [Fact]
    public void Measure_PropagatesConstraints()
    {
        // Arrange - parent with padding subtracts from available space
        var stack = new SkiaStackLayout
        {
            Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical,
            Padding = new Thickness(20)
        };

        var child = new TestView { WidthRequest = 500, HeightRequest = 50 };
        stack.AddChild(child);

        // Act
        stack.Measure(new Size(400, 600));

        // Assert - child's DesiredSize should reflect WidthRequest/HeightRequest
        child.DesiredSize.Width.Should().Be(500);
        child.DesiredSize.Height.Should().Be(50);
    }

    [Fact]
    public void Arrange_SetsBoundsOnChildren()
    {
        // Arrange
        var stack = new SkiaStackLayout
        {
            Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical
        };

        var child1 = new TestView { WidthRequest = 100, HeightRequest = 40 };
        var child2 = new TestView { WidthRequest = 100, HeightRequest = 40 };

        stack.AddChild(child1);
        stack.AddChild(child2);

        stack.Measure(new Size(400, 600));

        // Act
        stack.Arrange(new Rect(0, 0, 400, 600));

        // Assert - children should have non-zero bounds after arrange
        child1.Bounds.Width.Should().BeGreaterThan(0);
        child1.Bounds.Height.Should().BeGreaterThan(0);
        child2.Bounds.Width.Should().BeGreaterThan(0);
        child2.Bounds.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void InvalidateMeasure_CausesMeasureRecalculation()
    {
        // Arrange
        var stack = new SkiaStackLayout
        {
            Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical
        };

        var child = new TestView { WidthRequest = 100, HeightRequest = 30 };
        stack.AddChild(child);

        // First measure
        var size1 = stack.Measure(new Size(400, 600));

        // Act - change child property and invalidate
        child.HeightRequest = 60;
        child.InvalidateMeasure();

        // Re-measure
        var size2 = stack.Measure(new Size(400, 600));

        // Assert - new measured size should reflect the change
        size2.Height.Should().BeGreaterThan(size1.Height);
        size2.Height.Should().BeGreaterThanOrEqualTo(60);
    }

    [Fact]
    public void DesiredSize_SetAfterMeasure()
    {
        // Arrange
        var view = new TestView { WidthRequest = 120, HeightRequest = 45 };

        // Before measure, DesiredSize should be zero
        view.DesiredSize.Should().Be(Size.Zero);

        // Act
        var measured = view.Measure(new Size(400, 600));

        // Assert - DesiredSize should reflect the measured size
        view.DesiredSize.Width.Should().Be(measured.Width);
        view.DesiredSize.Height.Should().Be(measured.Height);
        view.DesiredSize.Width.Should().BeGreaterThan(0);
        view.DesiredSize.Height.Should().BeGreaterThan(0);
    }
}

#endregion
