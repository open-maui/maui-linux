// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaStepperTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var stepper = new SkiaStepper();

        // Assert
        stepper.Value.Should().Be(0);
        stepper.Minimum.Should().Be(0);
        stepper.Maximum.Should().Be(100);
        stepper.Increment.Should().Be(1);
        stepper.IsEnabled.Should().BeTrue();
        stepper.IsFocusable.Should().BeTrue();
        stepper.IsMinusPressed.Should().BeFalse();
        stepper.IsPlusPressed.Should().BeFalse();
    }

    [Fact]
    public void Value_WhenSet_UpdatesProperty()
    {
        // Arrange
        var stepper = new SkiaStepper();

        // Act
        stepper.Value = 50;

        // Assert
        stepper.Value.Should().Be(50);
    }

    [Fact]
    public void Value_ClampsToMinimum()
    {
        // Arrange
        var stepper = new SkiaStepper { Minimum = 0, Maximum = 100 };

        // Act
        stepper.Value = -10;

        // Assert
        stepper.Value.Should().Be(0);
    }

    [Fact]
    public void Value_ClampsToMaximum()
    {
        // Arrange
        var stepper = new SkiaStepper { Minimum = 0, Maximum = 100 };

        // Act
        stepper.Value = 150;

        // Assert
        stepper.Value.Should().Be(100);
    }

    [Fact]
    public void Minimum_WhenSet_UpdatesProperty()
    {
        // Arrange
        var stepper = new SkiaStepper();

        // Act
        stepper.Minimum = 10;

        // Assert
        stepper.Minimum.Should().Be(10);
    }

    [Fact]
    public void Maximum_WhenSet_UpdatesProperty()
    {
        // Arrange
        var stepper = new SkiaStepper();

        // Act
        stepper.Maximum = 200;

        // Assert
        stepper.Maximum.Should().Be(200);
    }

    [Fact]
    public void Increment_WhenSet_UpdatesProperty()
    {
        // Arrange
        var stepper = new SkiaStepper();

        // Act
        stepper.Increment = 5;

        // Assert
        stepper.Increment.Should().Be(5);
    }

    [Fact]
    public void ValueChanged_EventCanBeSubscribed()
    {
        // Arrange
        var stepper = new SkiaStepper();
        var eventRaised = false;

        // Act
        stepper.ValueChanged += (s, e) => eventRaised = true;
        stepper.Value = 50;

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void ValueChanged_EventReportsCorrectValues()
    {
        // Arrange
        var stepper = new SkiaStepper();
        var eventCount = 0;

        // Act
        stepper.ValueChanged += (s, e) => eventCount++;
        stepper.Value = 25;
        stepper.Value = 75;

        // Assert
        eventCount.Should().Be(2);
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var stepper = new SkiaStepper { Value = 50 };
        stepper.Bounds = new Rect(0, 0, 81, 32);

        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => stepper.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void Measure_ReturnsPositiveSize()
    {
        // Arrange
        var stepper = new SkiaStepper();

        // Act
        var size = stepper.Measure(new Size(1000, 1000));

        // Assert
        size.Width.Should().BeGreaterThan(0);
        size.Height.Should().BeGreaterThan(0);
    }
}
