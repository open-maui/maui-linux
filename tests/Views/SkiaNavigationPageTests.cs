// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaNavigationPageTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var navPage = new SkiaNavigationPage();

        // Assert
        navPage.CurrentPage.Should().BeNull();
        navPage.IsVisible.Should().BeTrue();
        navPage.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void BarBackgroundColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var navPage = new SkiaNavigationPage();
        var color = Microsoft.Maui.Graphics.Colors.DarkBlue;

        // Act
        navPage.BarBackgroundColor = color;

        // Assert
        navPage.BarBackgroundColor.Should().Be(color);
    }

    [Fact]
    public void BarTextColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var navPage = new SkiaNavigationPage();
        var color = Microsoft.Maui.Graphics.Colors.Yellow;

        // Act
        navPage.BarTextColor = color;

        // Assert
        navPage.BarTextColor.Should().Be(color);
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var navPage = new SkiaNavigationPage();
        navPage.Bounds = new Rect(0, 0, 400, 600);

        using var surface = SKSurface.Create(new SKImageInfo(400, 600));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => navPage.Draw(canvas));
        exception.Should().BeNull();
    }
}
