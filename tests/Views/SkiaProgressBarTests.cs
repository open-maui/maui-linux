// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaProgressBarTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var progressBar = new SkiaProgressBar();

        // Assert
        progressBar.Progress.Should().Be(0.0);
    }

    [Fact]
    public void Progress_WhenSet_UpdatesProperty()
    {
        // Arrange
        var progressBar = new SkiaProgressBar();

        // Act
        progressBar.Progress = 0.5;

        // Assert
        progressBar.Progress.Should().Be(0.5);
    }

    [Fact]
    public void Progress_ClampsToMinimum()
    {
        // Arrange
        var progressBar = new SkiaProgressBar();

        // Act
        progressBar.Progress = -0.5;

        // Assert
        progressBar.Progress.Should().Be(0.0);
    }

    [Fact]
    public void Progress_ClampsToMaximum()
    {
        // Arrange
        var progressBar = new SkiaProgressBar();

        // Act
        progressBar.Progress = 1.5;

        // Assert
        progressBar.Progress.Should().Be(1.0);
    }

    [Fact]
    public void ProgressColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var progressBar = new SkiaProgressBar();
        var color = Microsoft.Maui.Graphics.Colors.Green;

        // Act
        progressBar.ProgressColor = color;

        // Assert
        progressBar.ProgressColor.Should().Be(color);
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var progressBar = new SkiaProgressBar { Progress = 0.5 };
        progressBar.Bounds = new Rect(0, 0, 200, 12);

        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => progressBar.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void Draw_AtZeroProgress_DoesNotThrow()
    {
        // Arrange
        var progressBar = new SkiaProgressBar { Progress = 0.0 };
        progressBar.Bounds = new Rect(0, 0, 200, 12);

        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => progressBar.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void Draw_AtFullProgress_DoesNotThrow()
    {
        // Arrange
        var progressBar = new SkiaProgressBar { Progress = 1.0 };
        progressBar.Bounds = new Rect(0, 0, 200, 12);

        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => progressBar.Draw(canvas));
        exception.Should().BeNull();
    }
}
