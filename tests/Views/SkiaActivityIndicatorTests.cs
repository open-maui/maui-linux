// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaActivityIndicatorTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var indicator = new SkiaActivityIndicator();

        // Assert
        indicator.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void IsRunning_WhenSet_UpdatesProperty()
    {
        // Arrange
        var indicator = new SkiaActivityIndicator();

        // Act
        indicator.IsRunning = true;

        // Assert
        indicator.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void Color_WhenSet_UpdatesProperty()
    {
        // Arrange
        var indicator = new SkiaActivityIndicator();
        var color = Microsoft.Maui.Graphics.Colors.Orange;

        // Act
        indicator.Color = color;

        // Assert
        indicator.Color.Should().Be(color);
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var indicator = new SkiaActivityIndicator { IsRunning = true };
        indicator.Bounds = new Rect(0, 0, 40, 40);

        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => indicator.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void Draw_WhenNotRunning_DoesNotThrow()
    {
        // Arrange
        var indicator = new SkiaActivityIndicator { IsRunning = false };
        indicator.Bounds = new Rect(0, 0, 40, 40);

        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => indicator.Draw(canvas));
        exception.Should().BeNull();
    }
}
