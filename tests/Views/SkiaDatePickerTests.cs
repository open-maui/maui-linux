// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaDatePickerTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var picker = new SkiaDatePicker();

        // Assert
        picker.Date.Date.Should().Be(DateTime.Today);
        picker.MinimumDate.Should().Be(new DateTime(1900, 1, 1));
        picker.MaximumDate.Should().Be(new DateTime(2100, 12, 31));
        picker.Format.Should().Be("d");
        picker.TextColor.Should().Be(Colors.Black);
        picker.IsOpen.Should().BeFalse();
        picker.IsFocusable.Should().BeTrue();
    }

    [Fact]
    public void Date_WhenSet_UpdatesProperty()
    {
        // Arrange
        var picker = new SkiaDatePicker();
        var newDate = new DateTime(2025, 6, 15);

        // Act
        picker.Date = newDate;

        // Assert
        picker.Date.Should().Be(newDate);
    }

    [Fact]
    public void MinimumDate_WhenSet_UpdatesProperty()
    {
        // Arrange
        var picker = new SkiaDatePicker();
        var minDate = new DateTime(2000, 1, 1);

        // Act
        picker.MinimumDate = minDate;

        // Assert
        picker.MinimumDate.Should().Be(minDate);
    }

    [Fact]
    public void MaximumDate_WhenSet_UpdatesProperty()
    {
        // Arrange
        var picker = new SkiaDatePicker();
        var maxDate = new DateTime(2030, 12, 31);

        // Act
        picker.MaximumDate = maxDate;

        // Assert
        picker.MaximumDate.Should().Be(maxDate);
    }

    [Fact]
    public void Format_WhenSet_UpdatesProperty()
    {
        // Arrange
        var picker = new SkiaDatePicker();

        // Act
        picker.Format = "yyyy-MM-dd";

        // Assert
        picker.Format.Should().Be("yyyy-MM-dd");
    }

    [Fact]
    public void DateSelected_EventCanBeSubscribed()
    {
        // Arrange
        var picker = new SkiaDatePicker();
        var eventRaised = false;

        // Act
        picker.DateSelected += (s, e) => eventRaised = true;
        picker.Date = new DateTime(2025, 3, 1);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void Date_ClampsToMinimumDate()
    {
        // Arrange
        var picker = new SkiaDatePicker();
        picker.MinimumDate = new DateTime(2020, 1, 1);

        // Act
        picker.Date = new DateTime(2019, 1, 1);

        // Assert
        picker.Date.Should().Be(new DateTime(2020, 1, 1));
    }

    [Fact]
    public void Date_ClampsToMaximumDate()
    {
        // Arrange
        var picker = new SkiaDatePicker();
        picker.MaximumDate = new DateTime(2025, 12, 31);

        // Act
        picker.Date = new DateTime(2026, 6, 1);

        // Assert
        picker.Date.Should().Be(new DateTime(2025, 12, 31));
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var picker = new SkiaDatePicker();
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
        var picker = new SkiaDatePicker();

        // Act
        var size = picker.Measure(new Size(1000, 1000));

        // Assert
        size.Width.Should().BeGreaterThan(0);
        size.Height.Should().BeGreaterThan(0);
    }
}
