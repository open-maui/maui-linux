// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaImageTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var image = new SkiaImage();

        // Assert
        image.Bitmap.Should().BeNull();
        image.Aspect.Should().Be(Aspect.AspectFit);
        image.IsOpaque.Should().BeFalse();
        image.IsLoading.Should().BeFalse();
        image.IsAnimationPlaying.Should().BeFalse();
    }

    [Fact]
    public void Aspect_WhenSet_UpdatesProperty()
    {
        // Arrange
        var image = new SkiaImage();

        // Act
        image.Aspect = Aspect.Fill;

        // Assert
        image.Aspect.Should().Be(Aspect.Fill);
    }

    [Fact]
    public void Aspect_WhenSetToAspectFill_UpdatesProperty()
    {
        // Arrange
        var image = new SkiaImage();

        // Act
        image.Aspect = Aspect.AspectFill;

        // Assert
        image.Aspect.Should().Be(Aspect.AspectFill);
    }

    [Fact]
    public void IsOpaque_WhenSet_UpdatesProperty()
    {
        // Arrange
        var image = new SkiaImage();

        // Act
        image.IsOpaque = true;

        // Assert
        image.IsOpaque.Should().BeTrue();
    }

    [Fact]
    public void ImageBackgroundColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var image = new SkiaImage();
        var color = Microsoft.Maui.Graphics.Colors.Red;

        // Act
        image.ImageBackgroundColor = color;

        // Assert
        image.ImageBackgroundColor.Should().Be(color);
    }

    [Fact]
    public void Draw_DoesNotThrow_WhenEmpty()
    {
        // Arrange
        var image = new SkiaImage();
        image.Bounds = new Rect(0, 0, 100, 100);

        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => image.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void Measure_ReturnsPositiveSize()
    {
        // Arrange
        var image = new SkiaImage();

        // Act
        var size = image.Measure(new Size(1000, 1000));

        // Assert
        size.Width.Should().BeGreaterThanOrEqualTo(0);
        size.Height.Should().BeGreaterThanOrEqualTo(0);
    }
}
