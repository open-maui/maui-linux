// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// RadioButton's text style and border reach the Skia view: the handler copied only the font
/// size, so family, bold/italic, character spacing and the border were silently dropped.
/// </summary>
[Collection("LinuxApplication.Current")]
public class RadioButtonTextStyleMappingTests
{
    private static readonly Size Unbounded = new(double.PositiveInfinity, double.PositiveInfinity);

    [Fact]
    public void Font_spacing_content_and_border_map_to_the_view()
    {
        var radio = new RadioButton
        {
            Content = "Option",
            FontFamily = "Serif",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold | FontAttributes.Italic,
            CharacterSpacing = 2,
            TextColor = Colors.Purple,
            BorderColor = Colors.Red,
            BorderWidth = 3,
            CornerRadius = 6,
        };

        var view = HeadlessMauiContext.Realize<SkiaRadioButton>(radio);

        view.Content.Should().Be("Option");
        view.FontFamily.Should().Be("Serif");
        view.FontSize.Should().Be(18);
        view.FontAttributes.Should().Be(FontAttributes.Bold | FontAttributes.Italic);
        view.CharacterSpacing.Should().Be(2);
        view.TextColor.Should().Be(Colors.Purple);
        view.StrokeColor.Should().Be(Colors.Red);
        view.StrokeThickness.Should().Be(3);
        view.CornerRadius.Should().Be(6);

        radio.Content = "Changed";
        radio.FontAttributes = FontAttributes.None;
        view.Content.Should().Be("Changed");
        view.FontAttributes.Should().Be(FontAttributes.None);
    }

    [Fact]
    public void Unset_border_draws_no_outline()
    {
        // RadioButton's BorderWidth and CornerRadius default to -1 ("unset").
        var view = HeadlessMauiContext.Realize<SkiaRadioButton>(new RadioButton { Content = "x" });

        view.StrokeColor.Should().BeNull();
        view.StrokeThickness.Should().Be(0);
        view.CornerRadius.Should().Be(0);
    }

    [Fact]
    public void Character_spacing_and_bold_widen_the_content()
    {
        var plain = new SkiaRadioButton { Content = "Spacing" }.Measure(Unbounded).Width;
        new SkiaRadioButton { Content = "Spacing", CharacterSpacing = 4 }.Measure(Unbounded).Width
            .Should().BeApproximately(plain + 4 * 6, 0.5, "six gaps between seven characters");
        new SkiaRadioButton { Content = "Spacing", FontAttributes = FontAttributes.Bold }.Measure(Unbounded).Width
            .Should().BeGreaterThan(plain);
    }

    [Fact]
    public void The_stroke_outlines_the_control()
    {
        var view = new SkiaRadioButton { Content = "x", StrokeColor = Colors.Red, StrokeThickness = 4 };
        view.Arrange(new Rect(0, 0, 120, 40));
        using var bitmap = new SKBitmap(120, 40);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            view.Draw(canvas);
        }

        var edge = bitmap.GetPixel(60, 1);
        (edge.Red > 200 && edge.Green < 60 && edge.Blue < 60).Should().BeTrue($"the top edge is stroked, got {edge}");
        var inside = bitmap.GetPixel(100, 20);
        (inside.Red > 240 && inside.Green > 240).Should().BeTrue($"the interior is not filled, got {inside}");
    }
}
