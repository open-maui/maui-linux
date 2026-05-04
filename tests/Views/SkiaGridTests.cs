// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaGridTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var grid = new SkiaGrid();

        // Assert
        grid.RowSpacing.Should().Be(0f);
        grid.ColumnSpacing.Should().Be(0f);
        grid.Children.Should().BeEmpty();
        grid.IsVisible.Should().BeTrue();
        grid.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void AddChild_PositionsInGrid()
    {
        // Arrange
        var grid = new SkiaGrid();
        var child = new SkiaLabel();

        // Act
        grid.AddChild(child, row: 1, column: 2);

        // Assert
        grid.Children.Should().Contain(child);
        var position = grid.GetPosition(child);
        position.Row.Should().Be(1);
        position.Column.Should().Be(2);
    }

    [Fact]
    public void Measure_ReturnsPositiveSize()
    {
        // Arrange
        var grid = new SkiaGrid();
        grid.RowDefinitions.Add(new Microsoft.Maui.Platform.GridLength(50));
        grid.ColumnDefinitions.Add(new Microsoft.Maui.Platform.GridLength(100));

        var child = new SkiaButton { Text = "Test" };
        grid.AddChild(child, row: 0, column: 0);

        // Act
        var size = grid.Measure(new Size(400, 300));

        // Assert
        size.Width.Should().BeGreaterThan(0);
        size.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Arrange_PositionsChildren()
    {
        // Arrange
        var grid = new SkiaGrid();
        grid.RowDefinitions.Add(new Microsoft.Maui.Platform.GridLength(50));
        grid.ColumnDefinitions.Add(new Microsoft.Maui.Platform.GridLength(100));

        var child = new SkiaButton { Text = "Test" };
        grid.AddChild(child, row: 0, column: 0);

        grid.Measure(new Size(400, 300));

        // Act
        grid.Arrange(new Rect(0, 0, 400, 300));

        // Assert
        child.Bounds.Width.Should().BeGreaterThan(0);
        child.Bounds.Height.Should().BeGreaterThan(0);
    }
}
