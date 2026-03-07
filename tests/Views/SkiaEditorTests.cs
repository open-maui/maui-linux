// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaEditorTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var editor = new SkiaEditor();

        // Assert
        editor.Text.Should().BeEmpty();
        editor.Placeholder.Should().BeEmpty();
        editor.IsReadOnly.Should().BeFalse();
        editor.TextColor.Should().BeNull();
        editor.PlaceholderColor.Should().BeNull();
        editor.FontSize.Should().Be(14.0);
        editor.FontFamily.Should().BeEmpty();
        editor.MaxLength.Should().Be(-1);
        editor.IsFocusable.Should().BeTrue();
    }

    [Fact]
    public void Text_WhenSet_UpdatesProperty()
    {
        // Arrange
        var editor = new SkiaEditor();

        // Act
        editor.Text = "Hello World";

        // Assert
        editor.Text.Should().Be("Hello World");
    }

    [Fact]
    public void Placeholder_WhenSet_UpdatesProperty()
    {
        // Arrange
        var editor = new SkiaEditor();

        // Act
        editor.Placeholder = "Enter text...";

        // Assert
        editor.Placeholder.Should().Be("Enter text...");
    }

    [Fact]
    public void TextColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var editor = new SkiaEditor();
        var color = Microsoft.Maui.Graphics.Colors.Blue;

        // Act
        editor.TextColor = color;

        // Assert
        editor.TextColor.Should().Be(color);
    }

    [Fact]
    public void PlaceholderColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var editor = new SkiaEditor();
        var color = Microsoft.Maui.Graphics.Colors.Gray;

        // Act
        editor.PlaceholderColor = color;

        // Assert
        editor.PlaceholderColor.Should().Be(color);
    }

    [Fact]
    public void FontSize_WhenSet_UpdatesProperty()
    {
        // Arrange
        var editor = new SkiaEditor();

        // Act
        editor.FontSize = 20.0;

        // Assert
        editor.FontSize.Should().Be(20.0);
    }

    [Fact]
    public void FontFamily_WhenSet_UpdatesProperty()
    {
        // Arrange
        var editor = new SkiaEditor();

        // Act
        editor.FontFamily = "Monospace";

        // Assert
        editor.FontFamily.Should().Be("Monospace");
    }

    [Fact]
    public void IsReadOnly_WhenSet_UpdatesProperty()
    {
        // Arrange
        var editor = new SkiaEditor();

        // Act
        editor.IsReadOnly = true;

        // Assert
        editor.IsReadOnly.Should().BeTrue();
    }

    [Fact]
    public void MaxLength_WhenSet_UpdatesProperty()
    {
        // Arrange
        var editor = new SkiaEditor();

        // Act
        editor.MaxLength = 100;

        // Assert
        editor.MaxLength.Should().Be(100);
    }

    [Fact]
    public void TextChanged_EventCanBeSubscribed()
    {
        // Arrange
        var editor = new SkiaEditor();
        var eventSubscribed = false;

        // Act
        editor.TextChanged += (s, e) => eventSubscribed = true;

        // Assert - Just verify we can subscribe without error
        eventSubscribed.Should().BeFalse(); // Not raised yet, just subscribed
    }

    [Fact]
    public void Measure_ReturnsPositiveSize()
    {
        // Arrange
        var editor = new SkiaEditor { Text = "Test" };

        // Act
        var size = editor.Measure(new Size(1000, 1000));

        // Assert
        size.Width.Should().BeGreaterThan(0);
        size.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var editor = new SkiaEditor { Text = "Test" };
        editor.Bounds = new Rect(0, 0, 200, 100);

        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => editor.Draw(canvas));
        exception.Should().BeNull();
    }
}
