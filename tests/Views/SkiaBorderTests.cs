// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaBorderTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var border = new SkiaBorder();

        // Assert
        border.StrokeThickness.Should().Be(1.0);
        border.CornerRadius.Should().Be(0.0);
        border.Stroke.Should().Be(Colors.Black);
        border.HasShadow.Should().BeFalse();
        border.IsVisible.Should().BeTrue();
        border.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Content_WhenSet_UpdatesProperty()
    {
        // Arrange
        var border = new SkiaBorder();
        var content = new SkiaLabel();

        // Act
        border.AddChild(content);

        // Assert
        border.Children.Should().Contain(content);
        content.Parent.Should().Be(border);
    }

    [Fact]
    public void StrokeColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var border = new SkiaBorder();
        var color = Microsoft.Maui.Graphics.Colors.Blue;

        // Act
        border.Stroke = color;

        // Assert
        border.Stroke.Should().Be(color);
    }

    [Fact]
    public void StrokeThickness_WhenSet_UpdatesProperty()
    {
        // Arrange
        var border = new SkiaBorder();

        // Act
        border.StrokeThickness = 3.0;

        // Assert
        border.StrokeThickness.Should().Be(3.0);
    }

    [Fact]
    public void CornerRadius_WhenSet_UpdatesProperty()
    {
        // Arrange
        var border = new SkiaBorder();

        // Act
        border.CornerRadius = 12.0;

        // Assert
        border.CornerRadius.Should().Be(12.0);
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var border = new SkiaBorder();
        border.Bounds = new Rect(0, 0, 200, 100);

        using var surface = SKSurface.Create(new SKImageInfo(300, 200));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => border.Draw(canvas));
        exception.Should().BeNull();
    }
}
