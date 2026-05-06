// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaTimePickerTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var picker = new SkiaTimePicker();

        // Assert
        picker.Format.Should().Be("t");
        picker.TextColor.Should().Be(Colors.Black);
        picker.IsOpen.Should().BeFalse();
        picker.IsFocusable.Should().BeTrue();
    }

    [Fact]
    public void Time_WhenSet_UpdatesProperty()
    {
        // Arrange
        var picker = new SkiaTimePicker();
        var newTime = new TimeSpan(14, 30, 0);

        // Act
        picker.Time = newTime;

        // Assert
        picker.Time.Should().Be(newTime);
    }

    [Fact]
    public void Format_WhenSet_UpdatesProperty()
    {
        // Arrange
        var picker = new SkiaTimePicker();

        // Act
        picker.Format = "HH:mm:ss";

        // Assert
        picker.Format.Should().Be("HH:mm:ss");
    }

    [Fact]
    public void TextColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var picker = new SkiaTimePicker();
        var color = Microsoft.Maui.Graphics.Colors.Blue;

        // Act
        picker.TextColor = color;

        // Assert
        picker.TextColor.Should().Be(color);
    }

    [Fact]
    public void TimeSelected_EventCanBeSubscribed()
    {
        // Arrange
        var picker = new SkiaTimePicker();
        var eventRaised = false;

        // Act
        picker.TimeSelected += (s, e) => eventRaised = true;
        picker.Time = new TimeSpan(10, 0, 0);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var picker = new SkiaTimePicker();
        picker.Bounds = new Rect(0, 0, 200, 40);

        using var surface = SKSurface.Create(new SKImageInfo(300, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => picker.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void Measure_ReturnsPositiveSize()
    {
        // Arrange
        var picker = new SkiaTimePicker();

        // Act
        var size = picker.Measure(new Size(1000, 1000));

        // Assert
        size.Width.Should().BeGreaterThan(0);
        size.Height.Should().BeGreaterThan(0);
    }
}
