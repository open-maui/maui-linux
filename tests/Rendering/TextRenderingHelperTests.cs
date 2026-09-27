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

    [Fact]
    public void A_string_made_only_of_missing_glyphs_draws_with_the_fallback_face()
    {
        const string text = "\u22EE"; // the vertical-ellipsis overflow glyph
        var preferred = SKTypeface.FromFamilyName("sans-serif");
        var runs = Microsoft.Maui.Platform.Linux.Services.FontFallbackManager.Instance.ShapeTextWithFallback(text, preferred);
        if (runs.Count != 1 || ReferenceEquals(runs[0].Typeface, preferred))
            return; // this machine's UI font has the glyph, or no font does

        using var viaHelper = Draw(c => TextRenderingHelper.DrawTextWithFallback(c, text, 10, 40, Paint(), preferred, 32));
        using var direct = Draw(c =>
        {
            using var font = Microsoft.Maui.Platform.Linux.Rendering.SkiaFontFactory.Create(runs[0].Typeface, 32);
            c.DrawText(text, 10, 40, SKTextAlign.Left, font, Paint());
        });

        viaHelper.Bytes.Should().Equal(direct.Bytes);
    }

    private static SKPaint Paint() => new() { Color = SKColors.Black, IsAntialias = true };

    private static SKBitmap Draw(Action<SKCanvas> draw)
    {
        var bitmap = new SKBitmap(64, 64);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        draw(canvas);
        return bitmap;
    }

    [Fact]
    public void MeasureWidth_matches_the_primary_font_when_no_fallback_is_needed()
    {
        using var font = Microsoft.Maui.Platform.Linux.Rendering.SkiaFontFactory.Create(SKTypeface.FromFamilyName("sans-serif"), 14);
        TextRenderingHelper.MeasureWidth(font, "Last scan").Should().Be(font.MeasureText("Last scan"));
    }

    [Fact]
    public void MeasureWidth_measures_fallback_runs_in_their_own_face()
    {
        var preferred = SKTypeface.FromFamilyName("sans-serif");
        const string text = "\uD83D\uDCC1 20 repositories"; // folder emoji
        var runs = Microsoft.Maui.Platform.Linux.Services.FontFallbackManager.Instance.ShapeTextWithFallback(text, preferred);
        if (runs.Count < 2)
            return; // no emoji font on this machine

        using var font = Microsoft.Maui.Platform.Linux.Rendering.SkiaFontFactory.Create(preferred, 14);
        float expected = 0;
        foreach (var run in runs)
        {
            using var runFont = Microsoft.Maui.Platform.Linux.Rendering.SkiaFontFactory.Create(run.Typeface, 14);
            expected += runFont.MeasureText(run.Text);
        }

        TextRenderingHelper.MeasureWidth(font, text).Should().BeApproximately(expected, 0.01f);
    }
}
