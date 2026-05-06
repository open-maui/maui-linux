// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Rendering;

public class TextRenderingHelperTests
{
    [Fact]
    public void ToSKColor_WithNull_ReturnsDefault()
    {
        // Arrange & Act
        var result = TextRenderingHelper.ToSKColor(null);

        // Assert
        result.Should().Be(default(SKColor));
    }

    [Fact]
    public void ToSKColor_WithValidColor_ReturnsCorrectSKColor()
    {
        // Arrange
        var color = Microsoft.Maui.Graphics.Colors.Red;

        // Act
        var result = TextRenderingHelper.ToSKColor(color);

        // Assert
        result.Red.Should().Be(255);
        result.Green.Should().Be(0);
        result.Blue.Should().Be(0);
        result.Alpha.Should().Be(255);
    }

    [Fact]
    public void GetFontStyle_WithNone_ReturnsNormal()
    {
        // Arrange & Act
        var result = TextRenderingHelper.GetFontStyle(FontAttributes.None);

        // Assert
        result.Should().Be(SKFontStyle.Normal);
    }

    [Fact]
    public void GetFontStyle_WithBold_ReturnsBold()
    {
        // Arrange & Act
        var result = TextRenderingHelper.GetFontStyle(FontAttributes.Bold);

        // Assert
        result.Should().Be(SKFontStyle.Bold);
    }

    [Fact]
    public void GetFontStyle_WithItalic_ReturnsItalic()
    {
        // Arrange & Act
        var result = TextRenderingHelper.GetFontStyle(FontAttributes.Italic);

        // Assert
        result.Should().Be(SKFontStyle.Italic);
    }

    [Fact]
    public void GetFontStyle_WithBoldItalic_ReturnsBoldItalic()
    {
        // Arrange & Act
        var result = TextRenderingHelper.GetFontStyle(FontAttributes.Bold | FontAttributes.Italic);

        // Assert
        result.Should().Be(SKFontStyle.BoldItalic);
    }

    [Fact]
    public void GetEffectiveFontFamily_WithNull_ReturnsSans()
    {
        // Arrange & Act
        var result = TextRenderingHelper.GetEffectiveFontFamily(null);

        // Assert
        result.Should().Be("Sans");
    }

    [Fact]
    public void GetEffectiveFontFamily_WithEmpty_ReturnsSans()
    {
        // Arrange & Act
        var result = TextRenderingHelper.GetEffectiveFontFamily(string.Empty);

        // Assert
        result.Should().Be("Sans");
    }

    [Fact]
    public void GetEffectiveFontFamily_WithValue_ReturnsValue()
    {
        // Arrange & Act
        var result = TextRenderingHelper.GetEffectiveFontFamily("Roboto");

        // Assert
        result.Should().Be("Roboto");
    }
}
