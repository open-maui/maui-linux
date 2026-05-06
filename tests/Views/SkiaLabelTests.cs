// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaLabelTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var label = new SkiaLabel();

        // Assert
        label.Text.Should().BeEmpty();
        label.TextColor.Should().BeNull();
        label.FontSize.Should().Be(14.0);
        label.LineBreakMode.Should().Be(LineBreakMode.TailTruncation);
        label.FontFamily.Should().BeEmpty();
        label.FontAttributes.Should().Be(FontAttributes.None);
        label.HorizontalTextAlignment.Should().Be(TextAlignment.Start);
        label.VerticalTextAlignment.Should().Be(TextAlignment.Start);
        label.TextDecorations.Should().Be(TextDecorations.None);
        label.CharacterSpacing.Should().Be(0.0);
        label.MaxLines.Should().Be(0);
        label.LineHeight.Should().Be(-1.0);
    }

    [Fact]
    public void Text_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.Text = "Hello World";

        // Assert
        label.Text.Should().Be("Hello World");
    }

    [Fact]
    public void TextColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();
        var color = Microsoft.Maui.Graphics.Colors.Red;

        // Act
        label.TextColor = color;

        // Assert
        label.TextColor.Should().Be(color);
    }

    [Fact]
    public void FontFamily_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.FontFamily = "Roboto";

        // Assert
        label.FontFamily.Should().Be("Roboto");
    }

    [Fact]
    public void FontSize_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.FontSize = 24.0;

        // Assert
        label.FontSize.Should().Be(24.0);
    }

    [Fact]
    public void FontAttributes_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.FontAttributes = FontAttributes.Bold;

        // Assert
        label.FontAttributes.Should().Be(FontAttributes.Bold);
    }

    [Fact]
    public void HorizontalTextAlignment_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.HorizontalTextAlignment = TextAlignment.Center;

        // Assert
        label.HorizontalTextAlignment.Should().Be(TextAlignment.Center);
    }

    [Fact]
    public void VerticalTextAlignment_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.VerticalTextAlignment = TextAlignment.End;

        // Assert
        label.VerticalTextAlignment.Should().Be(TextAlignment.End);
    }

    [Fact]
    public void TextDecorations_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.TextDecorations = TextDecorations.Underline;

        // Assert
        label.TextDecorations.Should().Be(TextDecorations.Underline);
    }

    [Fact]
    public void LineHeight_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.LineHeight = 1.5;

        // Assert
        label.LineHeight.Should().Be(1.5);
    }

    [Fact]
    public void CharacterSpacing_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.CharacterSpacing = 2.0;

        // Assert
        label.CharacterSpacing.Should().Be(2.0);
    }

    [Fact]
    public void MaxLines_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.MaxLines = 3;

        // Assert
        label.MaxLines.Should().Be(3);
    }

    [Fact]
    public void LineBreakMode_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.LineBreakMode = LineBreakMode.WordWrap;

        // Assert
        label.LineBreakMode.Should().Be(LineBreakMode.WordWrap);
    }

    [Fact]
    public void Measure_ReturnsPositiveSize()
    {
        // Arrange
        var label = new SkiaLabel { Text = "Test" };

        // Act
        var size = label.Measure(new Size(1000, 1000));

        // Assert
        size.Width.Should().BeGreaterThan(0);
        size.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var label = new SkiaLabel();
        label.Bounds = new Rect(0, 0, 200, 40);

        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => label.Draw(canvas));
        exception.Should().BeNull();
    }
}
