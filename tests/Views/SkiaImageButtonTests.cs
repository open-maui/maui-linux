// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaImageButtonTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var button = new SkiaImageButton();

        // Assert
        button.Bitmap.Should().BeNull();
        button.Aspect.Should().Be(Aspect.AspectFit);
        button.IsOpaque.Should().BeFalse();
        button.IsLoading.Should().BeFalse();
        button.CornerRadius.Should().Be(0);
        button.Padding.Should().Be(new Thickness(0));
        button.StrokeColor.Should().Be(Colors.Transparent);
        button.StrokeThickness.Should().Be(0.0);
        button.IsPressed.Should().BeFalse();
        button.IsHovered.Should().BeFalse();
        button.IsFocusable.Should().BeTrue();
    }

    [Fact]
    public void StrokeColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var button = new SkiaImageButton();
        var color = Microsoft.Maui.Graphics.Colors.Red;

        // Act
        button.StrokeColor = color;

        // Assert
        button.StrokeColor.Should().Be(color);
    }

    [Fact]
    public void StrokeThickness_WhenSet_UpdatesProperty()
    {
        // Arrange
        var button = new SkiaImageButton();

        // Act
        button.StrokeThickness = 2.0;

        // Assert
        button.StrokeThickness.Should().Be(2.0);
    }

    [Fact]
    public void CornerRadius_WhenSet_UpdatesProperty()
    {
        // Arrange
        var button = new SkiaImageButton();

        // Act
        button.CornerRadius = 10;

        // Assert
        button.CornerRadius.Should().Be(10);
    }

    [Fact]
    public void Padding_WhenSet_UpdatesProperty()
    {
        // Arrange
        var button = new SkiaImageButton();
        var padding = new Thickness(5, 10, 15, 20);

        // Act
        button.Padding = padding;

        // Assert
        button.Padding.Should().Be(padding);
    }

    [Fact]
    public void Aspect_WhenSet_UpdatesProperty()
    {
        // Arrange
        var button = new SkiaImageButton();

        // Act
        button.Aspect = Aspect.Fill;

        // Assert
        button.Aspect.Should().Be(Aspect.Fill);
    }

    [Fact]
    public void Clicked_EventCanBeSubscribed()
    {
        // Arrange
        var button = new SkiaImageButton();
        var eventSubscribed = false;

        // Act
        button.Clicked += (s, e) => eventSubscribed = true;

        // Assert - Just verify we can subscribe without error
        eventSubscribed.Should().BeFalse();
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var button = new SkiaImageButton();
        button.Bounds = new Rect(0, 0, 100, 100);

        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => button.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void Measure_ReturnsPositiveSize()
    {
        // Arrange
        var button = new SkiaImageButton();

        // Act
        var size = button.Measure(new Size(1000, 1000));

        // Assert
        size.Width.Should().BeGreaterThan(0);
        size.Height.Should().BeGreaterThan(0);
    }
}
