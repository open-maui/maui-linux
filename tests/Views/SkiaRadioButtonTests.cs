// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaRadioButtonTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var radioButton = new SkiaRadioButton();

        // Assert
        radioButton.IsChecked.Should().BeFalse();
        radioButton.Content.Should().BeEmpty();
        radioButton.GroupName.Should().BeNull();
        radioButton.Value.Should().BeNull();
        radioButton.IsFocusable.Should().BeTrue();
    }

    [Fact]
    public void IsChecked_WhenSet_UpdatesProperty()
    {
        // Arrange
        var radioButton = new SkiaRadioButton();

        // Act
        radioButton.IsChecked = true;

        // Assert
        radioButton.IsChecked.Should().BeTrue();
    }

    [Fact]
    public void GroupName_WhenSet_UpdatesProperty()
    {
        // Arrange
        var radioButton = new SkiaRadioButton();

        // Act
        radioButton.GroupName = "Group1";

        // Assert
        radioButton.GroupName.Should().Be("Group1");
    }

    [Fact]
    public void Value_WhenSet_UpdatesProperty()
    {
        // Arrange
        var radioButton = new SkiaRadioButton();

        // Act
        radioButton.Value = "Option1";

        // Assert
        radioButton.Value.Should().Be("Option1");
    }

    [Fact]
    public void Content_WhenSet_UpdatesProperty()
    {
        // Arrange
        var radioButton = new SkiaRadioButton();

        // Act
        radioButton.Content = "Option A";

        // Assert
        radioButton.Content.Should().Be("Option A");
    }

    [Fact]
    public void CheckedChanged_EventCanBeSubscribed()
    {
        // Arrange
        var radioButton = new SkiaRadioButton();
        var eventSubscribed = false;

        // Act
        radioButton.CheckedChanged += (s, e) => eventSubscribed = true;

        // Assert - Just verify we can subscribe without error
        eventSubscribed.Should().BeFalse(); // Not raised yet, just subscribed
    }

    [Fact]
    public void CheckedChanged_EventRaisedWhenIsCheckedChanges()
    {
        // Arrange
        var radioButton = new SkiaRadioButton();
        var eventRaised = false;
        var receivedValue = false;

        radioButton.CheckedChanged += (s, e) =>
        {
            eventRaised = true;
            receivedValue = e.IsChecked;
        };

        // Act
        radioButton.IsChecked = true;

        // Assert
        eventRaised.Should().BeTrue();
        receivedValue.Should().BeTrue();
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var radioButton = new SkiaRadioButton { Content = "Test" };
        radioButton.Bounds = new Rect(0, 0, 100, 30);

        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => radioButton.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void Draw_WhenChecked_DoesNotThrow()
    {
        // Arrange
        var radioButton = new SkiaRadioButton { Content = "Test", IsChecked = true };
        radioButton.Bounds = new Rect(0, 0, 100, 30);

        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => radioButton.Draw(canvas));
        exception.Should().BeNull();
    }
}
