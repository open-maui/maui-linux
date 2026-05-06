// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaSwitchTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var switchControl = new SkiaSwitch();

        // Assert
        switchControl.IsOn.Should().BeFalse();
        switchControl.IsFocusable.Should().BeTrue();
    }

    [Fact]
    public void IsOn_WhenSet_UpdatesProperty()
    {
        // Arrange
        var switchControl = new SkiaSwitch();

        // Act
        switchControl.IsOn = true;

        // Assert
        switchControl.IsOn.Should().BeTrue();
    }

    [Fact]
    public void OnTrackColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var switchControl = new SkiaSwitch();
        var color = Microsoft.Maui.Graphics.Colors.Green;

        // Act
        switchControl.OnTrackColor = color;

        // Assert
        switchControl.OnTrackColor.Should().Be(color);
    }

    [Fact]
    public void ThumbColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var switchControl = new SkiaSwitch();
        var color = Microsoft.Maui.Graphics.Colors.Red;

        // Act
        switchControl.ThumbColor = color;

        // Assert
        switchControl.ThumbColor.Should().Be(color);
    }

    [Fact]
    public void Toggled_EventCanBeSubscribed()
    {
        // Arrange
        var switchControl = new SkiaSwitch();
        var eventSubscribed = false;

        // Act
        switchControl.Toggled += (s, e) => eventSubscribed = true;

        // Assert - Just verify we can subscribe without error
        eventSubscribed.Should().BeFalse(); // Not raised yet, just subscribed
    }

    [Fact]
    public void Toggled_EventRaisedWhenIsOnChanges()
    {
        // Arrange
        var switchControl = new SkiaSwitch();
        var eventRaised = false;
        var receivedValue = false;

        switchControl.Toggled += (s, e) =>
        {
            eventRaised = true;
            receivedValue = e.Value;
        };

        // Act
        switchControl.IsOn = true;

        // Assert
        eventRaised.Should().BeTrue();
        receivedValue.Should().BeTrue();
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var switchControl = new SkiaSwitch();
        switchControl.Bounds = new Rect(0, 0, 60, 40);

        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => switchControl.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void Draw_WhenOn_DoesNotThrow()
    {
        // Arrange
        var switchControl = new SkiaSwitch { IsOn = true };
        switchControl.Bounds = new Rect(0, 0, 60, 40);

        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => switchControl.Draw(canvas));
        exception.Should().BeNull();
    }
}
