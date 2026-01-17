// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaSliderTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var slider = new SkiaSlider();

        // Assert
        slider.Value.Should().Be(0);
        slider.Minimum.Should().Be(0);
        slider.Maximum.Should().Be(1.0); // MAUI Slider.Maximum default is 1.0
        slider.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Value_WhenSet_ClampsToRange()
    {
        // Arrange
        var slider = new SkiaSlider { Minimum = 0, Maximum = 100 };

        // Act & Assert - Below minimum
        slider.Value = -10;
        slider.Value.Should().Be(0);

        // Act & Assert - Above maximum
        slider.Value = 150;
        slider.Value.Should().Be(100);

        // Act & Assert - Within range
        slider.Value = 50;
        slider.Value.Should().Be(50);
    }

    [Fact]
    public void Value_WhenChanged_RaisesValueChangedEvent()
    {
        // Arrange
        var slider = new SkiaSlider { Minimum = 0, Maximum = 100 };
        var eventRaised = false;
        double newValue = 0;
        slider.ValueChanged += (s, e) =>
        {
            eventRaised = true;
            newValue = slider.Value;
        };

        // Act
        slider.Value = 50;

        // Assert
        eventRaised.Should().BeTrue();
        newValue.Should().Be(50);
    }

    [Fact]
    public void Minimum_WhenSet_UpdatesProperty()
    {
        // Arrange
        var slider = new SkiaSlider { Minimum = 0, Maximum = 100 };

        // Act
        slider.Minimum = 20;

        // Assert
        slider.Minimum.Should().Be(20);
    }

    [Fact]
    public void Maximum_WhenSet_UpdatesProperty()
    {
        // Arrange
        var slider = new SkiaSlider { Minimum = 0, Maximum = 100 };

        // Act
        slider.Maximum = 50;

        // Assert
        slider.Maximum.Should().Be(50);
    }

    [Fact]
    public void IsEnabled_DefaultsToTrue()
    {
        // Arrange & Act
        var slider = new SkiaSlider();

        // Assert
        slider.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void ValueChanged_EventCanBeSubscribed()
    {
        // Arrange
        var slider = new SkiaSlider { Minimum = 0, Maximum = 100 };
        var eventCount = 0;

        // Act
        slider.ValueChanged += (s, e) => eventCount++;
        slider.Value = 50;
        slider.Value = 75;

        // Assert
        eventCount.Should().Be(2);
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var slider = new SkiaSlider { Value = 50, Minimum = 0, Maximum = 100 };
        slider.Bounds = new Rect(0, 0, 200, 40);

        using var surface = SKSurface.Create(new SKImageInfo(300, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => slider.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void ThumbColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var slider = new SkiaSlider();
        var color = Microsoft.Maui.Graphics.Colors.Red;

        // Act
        slider.ThumbColor = color;

        // Assert
        slider.ThumbColor.Should().Be(color);
    }

    [Fact]
    public void MinimumTrackColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var slider = new SkiaSlider();
        var color = Microsoft.Maui.Graphics.Colors.Green;

        // Act
        slider.MinimumTrackColor = color;

        // Assert
        slider.MinimumTrackColor.Should().Be(color);
    }

    [Fact]
    public void MaximumTrackColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var slider = new SkiaSlider();
        var color = Microsoft.Maui.Graphics.Colors.Gray;

        // Act
        slider.MaximumTrackColor = color;

        // Assert
        slider.MaximumTrackColor.Should().Be(color);
    }
}
