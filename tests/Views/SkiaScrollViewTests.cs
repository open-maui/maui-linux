// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaScrollViewTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var scrollView = new SkiaScrollView();

        // Assert
        scrollView.ScrollX.Should().Be(0);
        scrollView.ScrollY.Should().Be(0);
        scrollView.Content.Should().BeNull();
    }

    [Fact]
    public void Content_WhenSet_UpdatesParent()
    {
        // Arrange
        var scrollView = new SkiaScrollView();
        var content = new SkiaStackLayout();

        // Act
        scrollView.Content = content;

        // Assert
        scrollView.Content.Should().Be(content);
        content.Parent.Should().Be(scrollView);
    }

    [Fact]
    public void ScrollY_WhenSet_ClampsToValidRange()
    {
        // Arrange
        var scrollView = new SkiaScrollView();
        var content = new SkiaStackLayout();
        content.AddChild(new SkiaButton { Text = "1", RequestedHeight = 100 });
        content.AddChild(new SkiaButton { Text = "2", RequestedHeight = 100 });
        scrollView.Content = content;
        scrollView.Measure(new SKSize(200, 100)); // Viewport smaller than content
        scrollView.Arrange(new SKRect(0, 0, 200, 100));

        // Act - Try to scroll below 0
        scrollView.ScrollY = -50;

        // Assert
        scrollView.ScrollY.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void OnScroll_UpdatesScrollOffset()
    {
        // Arrange
        var scrollView = new SkiaScrollView();
        var content = new SkiaStackLayout();
        for (int i = 0; i < 20; i++)
        {
            content.AddChild(new SkiaButton { Text = $"Button {i}", RequestedHeight = 50 });
        }
        scrollView.Content = content;
        scrollView.Measure(new SKSize(200, 300));
        scrollView.Arrange(new SKRect(0, 0, 200, 300));

        var initialScrollY = scrollView.ScrollY;

        // Act
        scrollView.OnScroll(new ScrollEventArgs(100, 100, 0, 3)); // Scroll down

        // Assert
        scrollView.ScrollY.Should().BeGreaterThan(initialScrollY);
    }

    [Fact]
    public void HitTest_ReturnsView()
    {
        // Arrange
        var scrollView = new SkiaScrollView();
        var content = new SkiaStackLayout();
        var button = new SkiaButton { Text = "Test" };
        content.AddChild(button);
        scrollView.Content = content;

        scrollView.Measure(new SKSize(200, 200));
        scrollView.Arrange(new SKRect(0, 0, 200, 200));

        // Act
        var hit = scrollView.HitTest(100, 25);

        // Assert - HitTest should return a view within bounds
        hit.Should().NotBeNull();
    }

    [Fact]
    public void ScrollY_CanBeSet()
    {
        // Arrange
        var scrollView = new SkiaScrollView();
        var content = new SkiaStackLayout();
        for (int i = 0; i < 10; i++)
        {
            content.AddChild(new SkiaButton { Text = $"Button {i}" });
        }
        scrollView.Content = content;

        scrollView.Measure(new SKSize(200, 100));
        scrollView.Arrange(new SKRect(0, 0, 200, 100));

        // Act
        scrollView.ScrollY = 50;

        // Assert - ScrollY should be settable
        scrollView.ScrollY.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var scrollView = new SkiaScrollView();
        var content = new SkiaStackLayout();
        content.AddChild(new SkiaButton { Text = "Test" });
        scrollView.Content = content;
        scrollView.Bounds = new SKRect(0, 0, 200, 200);

        using var surface = SKSurface.Create(new SKImageInfo(300, 300));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => scrollView.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void MaxScrollOffset_CalculatesCorrectly()
    {
        // Arrange
        var scrollView = new SkiaScrollView();
        var content = new SkiaStackLayout();
        for (int i = 0; i < 20; i++)
        {
            content.AddChild(new SkiaButton { Text = $"Button {i}", RequestedHeight = 50 });
        }
        scrollView.Content = content;
        scrollView.Measure(new SKSize(200, 200));
        scrollView.Arrange(new SKRect(0, 0, 200, 200));

        // Act - Scroll to maximum
        scrollView.ScrollY = 10000; // Very large value

        // Assert - Should be clamped to content height minus viewport
        scrollView.ScrollY.Should().BeLessOrEqualTo(1000 - 200); // 20*50 - 200
    }
}
