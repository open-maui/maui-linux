// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Xunit;
using Microsoft.Maui.Platform;
using MauiPlatform = Microsoft.Maui.Platform;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// Tests that SkiaGrid and SkiaStackLayout read alignment from the
/// MauiView back-reference (IView.HorizontalLayoutAlignment / VerticalLayoutAlignment)
/// rather than from SkiaView.HorizontalOptions / VerticalOptions.
/// This ensures children with Center/End alignment are positioned correctly.
/// </summary>
public class AlignmentTests
{
    /// <summary>
    /// Helper: creates a TestView with a MauiView back-reference that has the given alignment.
    /// </summary>
    private static TestView CreateAlignedChild(
        LayoutOptions horizontal, LayoutOptions vertical,
        double widthRequest = 100, double heightRequest = 40)
    {
        var child = new TestView
        {
            WidthRequest = widthRequest,
            HeightRequest = heightRequest
        };

        // Create a real MAUI ContentView as the back-reference.
        // ContentView inherits from View which implements IView,
        // so IView.HorizontalLayoutAlignment returns HorizontalOptions.Alignment.
        var mauiView = new ContentView
        {
            HorizontalOptions = horizontal,
            VerticalOptions = vertical,
            WidthRequest = widthRequest,
            HeightRequest = heightRequest
        };
        child.MauiView = mauiView;

        return child;
    }

    #region Grid Alignment Tests

    [Fact]
    public void Grid_Child_HorizontalCenter_IsCenteredInCell()
    {
        // Arrange: 1x1 grid, 400px wide, child is 100px wide centered
        var grid = new SkiaGrid();
        grid.RowDefinitions.Add(new MauiPlatform.GridLength(100));
        grid.ColumnDefinitions.Add(MauiPlatform.GridLength.Star);

        var child = CreateAlignedChild(LayoutOptions.Center, LayoutOptions.Fill, 100, 40);
        grid.AddChild(child, row: 0, column: 0);

        grid.Measure(new Size(400, 300));
        grid.Arrange(new Rect(0, 0, 400, 300));

        // Assert: child should be centered horizontally (approx 150 from left)
        child.Bounds.Width.Should().BeApproximately(100, 1);
        child.Bounds.Left.Should().BeApproximately(150, 1,
            "a 100px-wide child centered in a 400px cell should start at x=150");
    }

    [Fact]
    public void Grid_Child_VerticalCenter_IsCenteredInCell()
    {
        // Arrange: 1x1 grid, 300px tall, child is 40px tall centered
        var grid = new SkiaGrid();
        grid.RowDefinitions.Add(MauiPlatform.GridLength.Star);
        grid.ColumnDefinitions.Add(new MauiPlatform.GridLength(200));

        var child = CreateAlignedChild(LayoutOptions.Fill, LayoutOptions.Center, 200, 40);
        grid.AddChild(child, row: 0, column: 0);

        grid.Measure(new Size(400, 300));
        grid.Arrange(new Rect(0, 0, 400, 300));

        // Assert: child should be centered vertically (approx 130 from top)
        child.Bounds.Height.Should().BeApproximately(40, 1);
        child.Bounds.Top.Should().BeApproximately(130, 1,
            "a 40px-tall child centered in a 300px cell should start at y=130");
    }

    [Fact]
    public void Grid_Child_BothCenter_IsCenteredInCell()
    {
        // Arrange: child 100x40 centered both ways in 400x300 cell
        var grid = new SkiaGrid();
        grid.RowDefinitions.Add(MauiPlatform.GridLength.Star);
        grid.ColumnDefinitions.Add(MauiPlatform.GridLength.Star);

        var child = CreateAlignedChild(LayoutOptions.Center, LayoutOptions.Center, 100, 40);
        grid.AddChild(child, row: 0, column: 0);

        grid.Measure(new Size(400, 300));
        grid.Arrange(new Rect(0, 0, 400, 300));

        // Assert
        child.Bounds.Width.Should().BeApproximately(100, 1);
        child.Bounds.Height.Should().BeApproximately(40, 1);
        child.Bounds.Left.Should().BeApproximately(150, 1);
        child.Bounds.Top.Should().BeApproximately(130, 1);
    }

    [Fact]
    public void Grid_Child_End_AlignedToEnd()
    {
        var grid = new SkiaGrid();
        grid.RowDefinitions.Add(MauiPlatform.GridLength.Star);
        grid.ColumnDefinitions.Add(MauiPlatform.GridLength.Star);

        var child = CreateAlignedChild(LayoutOptions.End, LayoutOptions.End, 100, 40);
        grid.AddChild(child, row: 0, column: 0);

        grid.Measure(new Size(400, 300));
        grid.Arrange(new Rect(0, 0, 400, 300));

        // Assert: child should be at the right and bottom of the cell
        child.Bounds.Width.Should().BeApproximately(100, 1);
        child.Bounds.Height.Should().BeApproximately(40, 1);
        child.Bounds.Left.Should().BeApproximately(300, 1,
            "a 100px-wide child at End in a 400px cell should start at x=300");
        child.Bounds.Top.Should().BeApproximately(260, 1,
            "a 40px-tall child at End in a 300px cell should start at y=260");
    }

    [Fact]
    public void Grid_Child_Start_AlignedToStart()
    {
        var grid = new SkiaGrid();
        grid.RowDefinitions.Add(MauiPlatform.GridLength.Star);
        grid.ColumnDefinitions.Add(MauiPlatform.GridLength.Star);

        var child = CreateAlignedChild(LayoutOptions.Start, LayoutOptions.Start, 100, 40);
        grid.AddChild(child, row: 0, column: 0);

        grid.Measure(new Size(400, 300));
        grid.Arrange(new Rect(0, 0, 400, 300));

        // Assert: child should be at top-left
        child.Bounds.Width.Should().BeApproximately(100, 1);
        child.Bounds.Height.Should().BeApproximately(40, 1);
        child.Bounds.Left.Should().BeApproximately(0, 1);
        child.Bounds.Top.Should().BeApproximately(0, 1);
    }

    [Fact]
    public void Grid_Child_Fill_ExpandsToFillCell()
    {
        var grid = new SkiaGrid();
        grid.RowDefinitions.Add(MauiPlatform.GridLength.Star);
        grid.ColumnDefinitions.Add(MauiPlatform.GridLength.Star);

        var child = CreateAlignedChild(LayoutOptions.Fill, LayoutOptions.Fill, 100, 40);
        grid.AddChild(child, row: 0, column: 0);

        grid.Measure(new Size(400, 300));
        grid.Arrange(new Rect(0, 0, 400, 300));

        // Assert: child should fill the entire cell
        child.Bounds.Width.Should().BeApproximately(400, 1);
        child.Bounds.Height.Should().BeApproximately(300, 1);
        child.Bounds.Left.Should().BeApproximately(0, 1);
        child.Bounds.Top.Should().BeApproximately(0, 1);
    }

    [Fact]
    public void Grid_Child_WithWidthRequest_CenterAlignment_RespectsWidthRequest()
    {
        // This mimics the OnboardingHost IntroView scenario:
        // child with WidthRequest=400, HorizontalOptions=Center in a full-width grid cell
        var grid = new SkiaGrid();
        grid.RowDefinitions.Add(MauiPlatform.GridLength.Star);
        grid.ColumnDefinitions.Add(MauiPlatform.GridLength.Star);

        var child = CreateAlignedChild(LayoutOptions.Center, LayoutOptions.Center, 400, 200);
        grid.AddChild(child, row: 0, column: 0);

        grid.Measure(new Size(800, 600));
        grid.Arrange(new Rect(0, 0, 800, 600));

        // Assert: child should be 400px wide, centered in 800px cell
        child.Bounds.Width.Should().BeApproximately(400, 1);
        child.Bounds.Left.Should().BeApproximately(200, 1,
            "a 400px-wide child centered in an 800px cell should start at x=200");
        child.Bounds.Height.Should().BeApproximately(200, 1);
        child.Bounds.Top.Should().BeApproximately(200, 1,
            "a 200px-tall child centered in a 600px cell should start at y=200");
    }

    [Fact]
    public void Grid_FallsBackToSkiaViewOptions_WhenNoMauiView()
    {
        // When MauiView is null, should fall back to SkiaView.HorizontalOptions
        var grid = new SkiaGrid();
        grid.RowDefinitions.Add(MauiPlatform.GridLength.Star);
        grid.ColumnDefinitions.Add(MauiPlatform.GridLength.Star);

        var child = new TestView
        {
            WidthRequest = 100,
            HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        // Note: MauiView is NOT set — should use fallback path
        grid.AddChild(child, row: 0, column: 0);

        grid.Measure(new Size(400, 300));
        grid.Arrange(new Rect(0, 0, 400, 300));

        // Assert: should still center using the SkiaView fallback
        child.Bounds.Width.Should().BeApproximately(100, 1);
        child.Bounds.Left.Should().BeApproximately(150, 1);
    }

    #endregion

    #region StackLayout Alignment Tests (Vertical stack - horizontal alignment of children)

    [Fact]
    public void VerticalStack_Child_HorizontalCenter_IsCentered()
    {
        var stack = new SkiaStackLayout
        {
            Orientation = MauiPlatform.StackOrientation.Vertical
        };

        var child = CreateAlignedChild(LayoutOptions.Center, LayoutOptions.Fill, 100, 40);
        stack.AddChild(child);

        stack.Measure(new Size(400, 600));
        stack.Arrange(new Rect(0, 0, 400, 600));

        // Assert: 100px-wide child centered in 400px stack
        child.Bounds.Width.Should().BeApproximately(100, 1);
        child.Bounds.Left.Should().BeApproximately(150, 1);
    }

    [Fact]
    public void VerticalStack_Child_HorizontalEnd_IsAlignedRight()
    {
        var stack = new SkiaStackLayout
        {
            Orientation = MauiPlatform.StackOrientation.Vertical
        };

        var child = CreateAlignedChild(LayoutOptions.End, LayoutOptions.Fill, 100, 40);
        stack.AddChild(child);

        stack.Measure(new Size(400, 600));
        stack.Arrange(new Rect(0, 0, 400, 600));

        // Assert: 100px child at end of 400px stack
        child.Bounds.Width.Should().BeApproximately(100, 1);
        child.Bounds.Left.Should().BeApproximately(300, 1);
    }

    [Fact]
    public void VerticalStack_Child_HorizontalFill_FillsWidth()
    {
        var stack = new SkiaStackLayout
        {
            Orientation = MauiPlatform.StackOrientation.Vertical
        };

        var child = CreateAlignedChild(LayoutOptions.Fill, LayoutOptions.Fill, 100, 40);
        stack.AddChild(child);

        stack.Measure(new Size(400, 600));
        stack.Arrange(new Rect(0, 0, 400, 600));

        // Assert: child should fill the stack width
        child.Bounds.Width.Should().BeApproximately(400, 1);
        child.Bounds.Left.Should().BeApproximately(0, 1);
    }

    #endregion

    #region StackLayout Alignment Tests (Horizontal stack - vertical alignment of children)

    [Fact]
    public void HorizontalStack_Child_VerticalCenter_IsCentered()
    {
        var stack = new SkiaStackLayout
        {
            Orientation = MauiPlatform.StackOrientation.Horizontal
        };

        var child = CreateAlignedChild(LayoutOptions.Fill, LayoutOptions.Center, 100, 40);
        stack.AddChild(child);

        stack.Measure(new Size(600, 400));
        stack.Arrange(new Rect(0, 0, 600, 400));

        // Assert: 40px-tall child centered in 400px stack height
        child.Bounds.Height.Should().BeApproximately(40, 1);
        child.Bounds.Top.Should().BeApproximately(180, 1);
    }

    [Fact]
    public void HorizontalStack_Child_VerticalEnd_IsAlignedBottom()
    {
        var stack = new SkiaStackLayout
        {
            Orientation = MauiPlatform.StackOrientation.Horizontal
        };

        var child = CreateAlignedChild(LayoutOptions.Fill, LayoutOptions.End, 100, 40);
        stack.AddChild(child);

        stack.Measure(new Size(600, 400));
        stack.Arrange(new Rect(0, 0, 600, 400));

        // Assert: 40px child at bottom of 400px stack
        child.Bounds.Height.Should().BeApproximately(40, 1);
        child.Bounds.Top.Should().BeApproximately(360, 1);
    }

    [Fact]
    public void HorizontalStack_Child_VerticalFill_FillsHeight()
    {
        var stack = new SkiaStackLayout
        {
            Orientation = MauiPlatform.StackOrientation.Horizontal
        };

        var child = CreateAlignedChild(LayoutOptions.Fill, LayoutOptions.Fill, 100, 40);
        stack.AddChild(child);

        stack.Measure(new Size(600, 400));
        stack.Arrange(new Rect(0, 0, 600, 400));

        // Assert: child should fill the stack height
        child.Bounds.Height.Should().BeApproximately(400, 1);
        child.Bounds.Top.Should().BeApproximately(0, 1);
    }

    #endregion
}
