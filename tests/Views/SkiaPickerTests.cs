// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaPickerTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var picker = new SkiaPicker();

        // Assert
        picker.SelectedIndex.Should().Be(-1);
        picker.Title.Should().BeEmpty();
        picker.TextColor.Should().Be(Colors.Black);
        picker.Items.Should().BeEmpty();
        picker.SelectedItem.Should().BeNull();
        picker.IsOpen.Should().BeFalse();
        picker.IsFocusable.Should().BeTrue();
    }

    [Fact]
    public void Title_WhenSet_UpdatesProperty()
    {
        // Arrange
        var picker = new SkiaPicker();

        // Act
        picker.Title = "Select an item";

        // Assert
        picker.Title.Should().Be("Select an item");
    }

    [Fact]
    public void SelectedIndex_WhenSet_UpdatesProperty()
    {
        // Arrange
        var picker = new SkiaPicker();
        picker.Items.Add("Item 1");
        picker.Items.Add("Item 2");

        // Act
        picker.SelectedIndex = 1;

        // Assert
        picker.SelectedIndex.Should().Be(1);
        picker.SelectedItem.Should().Be("Item 2");
    }

    [Fact]
    public void TextColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var picker = new SkiaPicker();
        var color = Microsoft.Maui.Graphics.Colors.Red;

        // Act
        picker.TextColor = color;

        // Assert
        picker.TextColor.Should().Be(color);
    }

    [Fact]
    public void Items_WhenAdded_UpdatesCollection()
    {
        // Arrange
        var picker = new SkiaPicker();

        // Act
        picker.Items.Add("Apple");
        picker.Items.Add("Banana");
        picker.Items.Add("Cherry");

        // Assert
        picker.Items.Should().HaveCount(3);
        picker.Items[0].Should().Be("Apple");
        picker.Items[1].Should().Be("Banana");
        picker.Items[2].Should().Be("Cherry");
    }

    [Fact]
    public void SetItems_ReplacesExistingItems()
    {
        // Arrange
        var picker = new SkiaPicker();
        picker.Items.Add("Old Item");

        // Act
        picker.SetItems(new[] { "New 1", "New 2" });

        // Assert
        picker.Items.Should().HaveCount(2);
        picker.Items[0].Should().Be("New 1");
    }

    [Fact]
    public void SelectedIndexChanged_EventCanBeSubscribed()
    {
        // Arrange
        var picker = new SkiaPicker();
        picker.Items.Add("Item 1");
        picker.Items.Add("Item 2");
        var eventRaised = false;

        // Act
        picker.SelectedIndexChanged += (s, e) => eventRaised = true;
        picker.SelectedIndex = 1;

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var picker = new SkiaPicker();
        picker.Items.Add("Test Item");
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
        var picker = new SkiaPicker();

        // Act
        var size = picker.Measure(new Size(1000, 1000));

        // Assert
        size.Width.Should().BeGreaterThan(0);
        size.Height.Should().BeGreaterThan(0);
    }
}
