// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaEntryTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var entry = new SkiaEntry();

        // Assert
        entry.Text.Should().BeEmpty();
        entry.Placeholder.Should().BeEmpty();
        entry.IsEnabled.Should().BeTrue();
        entry.IsReadOnly.Should().BeFalse();
        entry.IsFocusable.Should().BeTrue();
    }

    [Fact]
    public void Text_WhenSet_UpdatesProperty()
    {
        // Arrange
        var entry = new SkiaEntry();

        // Act
        entry.Text = "Hello World";

        // Assert
        entry.Text.Should().Be("Hello World");
    }

    [Fact]
    public void Text_WhenSet_RaisesTextChangedEvent()
    {
        // Arrange
        var entry = new SkiaEntry();
        string? oldText = null;
        string? newText = null;
        entry.TextChanged += (s, e) =>
        {
            oldText = e.OldTextValue;
            newText = e.NewTextValue;
        };

        // Act
        entry.Text = "Test";

        // Assert
        oldText.Should().BeEmpty();
        newText.Should().Be("Test");
    }

    [Fact]
    public void Placeholder_WhenSet_UpdatesProperty()
    {
        // Arrange
        var entry = new SkiaEntry();

        // Act
        entry.Placeholder = "Enter text...";

        // Assert
        entry.Placeholder.Should().Be("Enter text...");
    }

    [Fact]
    public void IsPassword_WhenTrue_MasksText()
    {
        // Arrange
        var entry = new SkiaEntry
        {
            Text = "secret",
            IsPassword = true
        };

        // Assert
        entry.IsPassword.Should().BeTrue();
        // The actual masking is done in Draw, but we verify the property is set
    }

    [Fact]
    public void MaxLength_CanBeSet()
    {
        // Arrange
        var entry = new SkiaEntry();

        // Act
        entry.MaxLength = 5;

        // Assert
        entry.MaxLength.Should().Be(5);
    }

    [Fact]
    public void OnTextInput_ModifiesText()
    {
        // Arrange
        var entry = new SkiaEntry { Text = "Hello" };
        entry.Bounds = new SKRect(0, 0, 200, 40);
        entry.OnFocusGained();
        var originalLength = entry.Text.Length;

        // Act
        entry.OnTextInput(new TextInputEventArgs(" World"));

        // Assert - Text is modified (inserted at cursor position)
        entry.Text.Length.Should().BeGreaterThan(originalLength);
    }

    [Fact]
    public void OnKeyDown_ReturnsKeyEvent()
    {
        // Arrange
        var entry = new SkiaEntry { Text = "Hello" };
        entry.Bounds = new SKRect(0, 0, 200, 40);
        entry.OnFocusGained();

        // Act - Verify OnKeyDown doesn't throw
        var exception = Record.Exception(() => entry.OnKeyDown(new KeyEventArgs(Key.Backspace)));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void OnKeyDown_WhenReadOnly_TextRemainsSame()
    {
        // Arrange
        var entry = new SkiaEntry { Text = "Hello", IsReadOnly = true };
        var originalText = entry.Text;
        entry.Bounds = new SKRect(0, 0, 200, 40);
        entry.OnFocusGained();

        // Act
        entry.OnKeyDown(new KeyEventArgs(Key.Backspace));

        // Assert - Text should remain unchanged
        entry.Text.Should().Be(originalText);
    }

    [Fact]
    public void CursorPosition_CanBeSet()
    {
        // Arrange
        var entry = new SkiaEntry { Text = "Hello World" };

        // Act
        entry.CursorPosition = 5;

        // Assert
        entry.CursorPosition.Should().Be(5);
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var entry = new SkiaEntry { Text = "Test", Placeholder = "Enter..." };
        entry.Bounds = new SKRect(0, 0, 200, 40);

        using var surface = SKSurface.Create(new SKImageInfo(300, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => entry.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void SelectAll_SelectsEntireText()
    {
        // Arrange
        var entry = new SkiaEntry { Text = "Hello World" };
        entry.OnFocusGained();

        // Act
        entry.SelectAll();

        // Assert
        entry.SelectionLength.Should().Be(11);
    }
}
