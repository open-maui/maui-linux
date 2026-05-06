// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaPageTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var page = new SkiaPage();

        // Assert
        page.Title.Should().BeEmpty();
        page.IsVisible.Should().BeTrue();
        page.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Title_WhenSet_UpdatesProperty()
    {
        // Arrange
        var page = new SkiaPage();

        // Act
        page.Title = "My Page";

        // Assert
        page.Title.Should().Be("My Page");
    }

    [Fact]
    public void BackgroundColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var page = new SkiaPage();
        var color = Microsoft.Maui.Graphics.Colors.LightGray;

        // Act
        page.BackgroundColor = color;

        // Assert
        page.BackgroundColor.Should().Be(color);
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var page = new SkiaPage();
        page.Title = "Test Page";
        page.Bounds = new Rect(0, 0, 400, 600);

        using var surface = SKSurface.Create(new SKImageInfo(400, 600));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => page.Draw(canvas));
        exception.Should().BeNull();
    }
}
