// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaButtonTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var button = new SkiaButton();

        // Assert
        button.Text.Should().BeEmpty();
        button.IsEnabled.Should().BeTrue();
        button.IsFocusable.Should().BeTrue();
    }

    [Fact]
    public void Text_WhenSet_UpdatesProperty()
    {
        // Arrange
        var button = new SkiaButton();

        // Act
        button.Text = "Click Me";

        // Assert
        button.Text.Should().Be("Click Me");
    }

    [Fact]
    public void Measure_ReturnsMinimumSize()
    {
        // Arrange
        var button = new SkiaButton { Text = "Test" };

        // Act
        var size = button.Measure(new SKSize(1000, 1000));

        // Assert
        size.Width.Should().BeGreaterThan(0);
        size.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Measure_ReturnsPositiveSize()
    {
        // Arrange
        var button = new SkiaButton
        {
            Text = "Test",
            RequestedWidth = 200,
            RequestedHeight = 50
        };

        // Act
        var size = button.Measure(new SKSize(1000, 1000));

        // Assert - Measure returns content-based size
        size.Width.Should().BeGreaterThan(0);
        size.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void IsEnabled_WhenFalse_UpdatesProperty()
    {
        // Arrange
        var button = new SkiaButton { Text = "Test" };

        // Act
        button.IsEnabled = false;

        // Assert
        button.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Clicked_EventCanBeSubscribed()
    {
        // Arrange
        var button = new SkiaButton { Text = "Test" };
        var eventSubscribed = false;

        // Act
        button.Clicked += (s, e) => eventSubscribed = true;

        // Assert - Just verify we can subscribe without error
        eventSubscribed.Should().BeFalse(); // Not raised yet, just subscribed
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var button = new SkiaButton { Text = "Test" };
        button.Bounds = new SKRect(0, 0, 100, 40);

        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => button.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void TextColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var button = new SkiaButton();
        var color = new SKColor(255, 0, 0);

        // Act
        button.TextColor = color;

        // Assert
        button.TextColor.Should().Be(color);
    }

    [Fact]
    public void BackgroundColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var button = new SkiaButton();
        var color = new SKColor(0, 255, 0);

        // Act
        button.BackgroundColor = color;

        // Assert
        button.BackgroundColor.Should().Be(color);
    }
}
