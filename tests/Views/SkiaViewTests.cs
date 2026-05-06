// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaViewTests
{
    [Fact]
    public void IsVisible_DefaultsToTrue()
    {
        // Arrange & Act
        var view = new SkiaLabel();

        // Assert
        view.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_DefaultsToTrue()
    {
        // Arrange & Act
        var view = new SkiaLabel();

        // Assert
        view.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Opacity_DefaultsToOne()
    {
        // Arrange & Act
        var view = new SkiaLabel();

        // Assert
        view.Opacity.Should().Be(1.0f);
    }

    [Fact]
    public void BackgroundColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var view = new SkiaLabel();
        var color = Microsoft.Maui.Graphics.Colors.Red;

        // Act
        view.BackgroundColor = color;

        // Assert
        view.BackgroundColor.Should().Be(color);
    }

    [Fact]
    public void Margin_WhenSet_UpdatesProperty()
    {
        // Arrange
        var view = new SkiaLabel();
        var margin = new Thickness(10, 20, 30, 40);

        // Act
        view.Margin = margin;

        // Assert
        view.Margin.Should().Be(margin);
    }

    [Fact]
    public void Padding_WhenSet_UpdatesProperty()
    {
        // Arrange
        var view = new SkiaLabel();
        var padding = new Thickness(5, 10, 15, 20);

        // Act
        view.Padding = padding;

        // Assert
        view.Padding.Should().Be(padding);
    }

    [Fact]
    public void Bounds_WhenSet_UpdatesProperty()
    {
        // Arrange
        var view = new SkiaLabel();
        var bounds = new Rect(10, 20, 200, 100);

        // Act
        view.Bounds = bounds;

        // Assert
        view.Bounds.Should().Be(bounds);
    }

    [Fact]
    public void InputTransparent_DefaultsToFalse()
    {
        // Arrange & Act
        var view = new SkiaLabel();

        // Assert
        view.InputTransparent.Should().BeFalse();
    }

    [Fact]
    public void AddChild_AddsChildToCollection()
    {
        // Arrange
        var parent = new SkiaButton();
        var child = new SkiaLabel();

        // Act
        parent.AddChild(child);

        // Assert
        parent.Children.Should().Contain(child);
        child.Parent.Should().Be(parent);
    }

    [Fact]
    public void RemoveChild_RemovesChildFromCollection()
    {
        // Arrange
        var parent = new SkiaButton();
        var child = new SkiaLabel();
        parent.AddChild(child);

        // Act
        parent.RemoveChild(child);

        // Assert
        parent.Children.Should().NotContain(child);
        child.Parent.Should().BeNull();
    }

    [Fact]
    public void HitTest_WithPointInsideBounds_ReturnsSelf()
    {
        // Arrange
        var view = new SkiaButton();
        view.Bounds = new Rect(0, 0, 100, 50);

        // Act
        var hit = view.HitTest(50, 25);

        // Assert
        hit.Should().NotBeNull();
    }

    [Fact]
    public void HitTest_WithPointOutsideBounds_ReturnsNull()
    {
        // Arrange
        var view = new SkiaButton();
        view.Bounds = new Rect(0, 0, 100, 50);

        // Act
        var hit = view.HitTest(200, 200);

        // Assert
        hit.Should().BeNull();
    }

    [Fact]
    public void Invalidate_DoesNotThrow()
    {
        // Arrange
        var view = new SkiaLabel();

        // Act & Assert
        var exception = Record.Exception(() => view.Invalidate());
        exception.Should().BeNull();
    }
}
