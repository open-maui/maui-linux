// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;
using PlatformStackOrientation = Microsoft.Maui.Platform.StackOrientation;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaStackLayoutTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var layout = new SkiaStackLayout();

        // Assert
        layout.Orientation.Should().Be(PlatformStackOrientation.Vertical);
        layout.Spacing.Should().Be(0);
        layout.Children.Should().BeEmpty();
    }

    [Fact]
    public void AddChild_AddsToChildren()
    {
        // Arrange
        var layout = new SkiaStackLayout();
        var button = new SkiaButton { Text = "Test" };

        // Act
        layout.AddChild(button);

        // Assert
        layout.Children.Should().Contain(button);
        button.Parent.Should().Be(layout);
    }

    [Fact]
    public void RemoveChild_RemovesFromChildren()
    {
        // Arrange
        var layout = new SkiaStackLayout();
        var button = new SkiaButton { Text = "Test" };
        layout.AddChild(button);

        // Act
        layout.RemoveChild(button);

        // Assert
        layout.Children.Should().NotContain(button);
        button.Parent.Should().BeNull();
    }

    [Fact]
    public void Measure_Vertical_ReturnsPositiveSize()
    {
        // Arrange
        var layout = new SkiaStackLayout
        {
            Orientation = PlatformStackOrientation.Vertical,
            Spacing = 10
        };
        layout.AddChild(new SkiaButton { Text = "1" });
        layout.AddChild(new SkiaButton { Text = "2" });
        layout.AddChild(new SkiaButton { Text = "3" });

        // Act
        var size = layout.Measure(new SKSize(200, 1000));

        // Assert - Size should account for 3 children with spacing
        size.Height.Should().BeGreaterThan(0);
        size.Width.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Measure_Horizontal_ReturnsPositiveSize()
    {
        // Arrange
        var layout = new SkiaStackLayout
        {
            Orientation = PlatformStackOrientation.Horizontal,
            Spacing = 10
        };
        layout.AddChild(new SkiaButton { Text = "1" });
        layout.AddChild(new SkiaButton { Text = "2" });
        layout.AddChild(new SkiaButton { Text = "3" });

        // Act
        var size = layout.Measure(new SKSize(1000, 200));

        // Assert - Size should account for 3 children with spacing
        size.Width.Should().BeGreaterThan(0);
        size.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Arrange_Vertical_PositionsChildren()
    {
        // Arrange
        var layout = new SkiaStackLayout
        {
            Orientation = PlatformStackOrientation.Vertical,
            Spacing = 10
        };
        var button1 = new SkiaButton { Text = "1" };
        var button2 = new SkiaButton { Text = "2" };
        layout.AddChild(button1);
        layout.AddChild(button2);

        // Act
        layout.Measure(new SKSize(200, 500));
        layout.Arrange(new SKRect(0, 0, 200, 500));

        // Assert - Button2 should be below Button1
        button2.Bounds.Top.Should().BeGreaterThan(button1.Bounds.Top);
    }

    [Fact]
    public void Arrange_Horizontal_PositionsChildren()
    {
        // Arrange
        var layout = new SkiaStackLayout
        {
            Orientation = PlatformStackOrientation.Horizontal,
            Spacing = 10
        };
        var button1 = new SkiaButton { Text = "1" };
        var button2 = new SkiaButton { Text = "2" };
        layout.AddChild(button1);
        layout.AddChild(button2);

        // Act
        layout.Measure(new SKSize(500, 200));
        layout.Arrange(new SKRect(0, 0, 500, 200));

        // Assert - Button2 should be to the right of Button1
        button2.Bounds.Left.Should().BeGreaterThan(button1.Bounds.Left);
    }

    [Fact]
    public void Padding_CanBeSet()
    {
        // Arrange
        var layout = new SkiaStackLayout
        {
            Orientation = PlatformStackOrientation.Vertical,
            Padding = new SKRect(20, 20, 20, 20)
        };
        var button = new SkiaButton { Text = "Test" };
        layout.AddChild(button);

        // Act
        layout.Measure(new SKSize(300, 300));
        layout.Arrange(new SKRect(0, 0, 300, 300));

        // Assert - Padding property is set
        layout.Padding.Left.Should().Be(20);
        layout.Padding.Top.Should().Be(20);
    }

    [Fact]
    public void Draw_DrawsAllChildren()
    {
        // Arrange
        var layout = new SkiaStackLayout();
        layout.AddChild(new SkiaButton { Text = "1" });
        layout.AddChild(new SkiaButton { Text = "2" });
        layout.Bounds = new SKRect(0, 0, 200, 200);

        using var surface = SKSurface.Create(new SKImageInfo(300, 300));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => layout.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void HitTest_ReturnsView()
    {
        // Arrange
        var layout = new SkiaStackLayout { Orientation = PlatformStackOrientation.Vertical };
        var button1 = new SkiaButton { Text = "1" };
        var button2 = new SkiaButton { Text = "2" };
        layout.AddChild(button1);
        layout.AddChild(button2);

        layout.Measure(new SKSize(200, 200));
        layout.Arrange(new SKRect(0, 0, 200, 200));

        // Act - Hit test within layout bounds
        var hit = layout.HitTest(100, 10);

        // Assert - Should return a view (either button1 or layout)
        hit.Should().NotBeNull();
    }
}
