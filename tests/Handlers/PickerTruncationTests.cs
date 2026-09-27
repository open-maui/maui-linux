// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

public class PickerTruncationTests
{
    [Fact]
    public void Text_too_long_for_its_width_is_cut_with_an_ellipsis_and_fits()
    {
        using var font = SkiaFontFactory.Create(14);
        var text = "A very long selected dropdown choice that cannot fit";
        var cut = TextRenderingHelper.Ellipsize(font, text, 100);

        cut.Should().EndWith("…");
        TextRenderingHelper.MeasureWidth(font, cut).Should().BeLessThanOrEqualTo(100);
        TextRenderingHelper.Ellipsize(font, "Short", 100).Should().Be("Short");
    }

    [Fact]
    public void A_picker_draws_nothing_outside_its_bounds()
    {
        var picker = new SkiaPicker();
        picker.SetItems(new[] { "A very long selected dropdown choice that cannot fit" });
        picker.SelectedIndex = 0;
        picker.Arrange(new Microsoft.Maui.Graphics.Rect(0, 0, 120, 40));
        using var bitmap = new SKBitmap(300, 40);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Magenta);
        picker.Draw(canvas);

        for (int x = 121; x < 300; x++)
            for (int y = 0; y < 40; y++)
                bitmap.GetPixel(x, y).Should().Be(SKColors.Magenta);
    }
}
