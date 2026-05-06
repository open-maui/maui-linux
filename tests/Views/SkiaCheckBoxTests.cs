// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaCheckBoxTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var checkBox = new SkiaCheckBox();

        // Assert
        checkBox.IsChecked.Should().BeFalse();
        checkBox.IsFocusable.Should().BeTrue();
    }

    [Fact]
    public void IsChecked_WhenSet_UpdatesProperty()
    {
        // Arrange
        var checkBox = new SkiaCheckBox();

        // Act
        checkBox.IsChecked = true;

        // Assert
        checkBox.IsChecked.Should().BeTrue();
    }

    [Fact]
    public void Color_WhenSet_UpdatesProperty()
    {
        // Arrange
        var checkBox = new SkiaCheckBox();
        var color = Microsoft.Maui.Graphics.Colors.Green;

        // Act
        checkBox.Color = color;

        // Assert
        checkBox.Color.Should().Be(color);
    }

    [Fact]
    public void CheckedChanged_EventCanBeSubscribed()
    {
        // Arrange
        var checkBox = new SkiaCheckBox();
        var eventSubscribed = false;

        // Act
        checkBox.CheckedChanged += (s, e) => eventSubscribed = true;

        // Assert - Just verify we can subscribe without error
        eventSubscribed.Should().BeFalse(); // Not raised yet, just subscribed
    }

    [Fact]
    public void CheckedChanged_EventRaisedWhenIsCheckedChanges()
    {
        // Arrange
        var checkBox = new SkiaCheckBox();
        var eventRaised = false;
        var receivedValue = false;

        checkBox.CheckedChanged += (s, e) =>
        {
            eventRaised = true;
            receivedValue = e.IsChecked;
        };

        // Act
        checkBox.IsChecked = true;

        // Assert
        eventRaised.Should().BeTrue();
        receivedValue.Should().BeTrue();
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var checkBox = new SkiaCheckBox();
        checkBox.Bounds = new Rect(0, 0, 28, 28);

        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => checkBox.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void Draw_WhenChecked_DoesNotThrow()
    {
        // Arrange
        var checkBox = new SkiaCheckBox { IsChecked = true };
        checkBox.Bounds = new Rect(0, 0, 28, 28);

        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => checkBox.Draw(canvas));
        exception.Should().BeNull();
    }
}
