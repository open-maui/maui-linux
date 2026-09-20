// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// FontImageSource renders a glyph from a (registered or system) font into a
/// square bitmap in the requested colour, which Image/ImageButton then show.
/// </summary>
public class FontImageSourceTests
{
    private static SKBitmap? Render(FontImageSource source, double width = 0, double height = 0)
        => ImageHandler.ImageSourceServiceResultManager.RenderFontImageSource(source, width, height);

    private static int CountPixels(SKBitmap bitmap, Func<SKColor, bool> predicate)
    {
        int n = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (predicate(bitmap.GetPixel(x, y))) n++;
        return n;
    }

    [Fact]
    public void Renders_the_glyph_in_the_requested_colour()
    {
        using var bitmap = Render(new FontImageSource { Glyph = "W", Color = Colors.Red, FontFamily = "Noto Sans" }, 48, 48);

        bitmap.Should().NotBeNull();
        bitmap!.Width.Should().Be(48);
        bitmap.Height.Should().Be(48);
        CountPixels(bitmap, c => c.Alpha > 200 && c.Red > 200 && c.Green < 60 && c.Blue < 60)
            .Should().BeGreaterThan(40, "the glyph is painted in the FontImageSource colour");
        CountPixels(bitmap, c => c.Alpha == 0).Should().BeGreaterThan(48 * 48 / 2, "the background stays transparent");
    }

    [Fact]
    public void Defaults_to_black_and_24px_when_nothing_is_requested()
    {
        using var bitmap = Render(new FontImageSource { Glyph = "A" });

        bitmap.Should().NotBeNull();
        bitmap!.Width.Should().Be(24);
        bitmap.Height.Should().Be(24);
        CountPixels(bitmap, c => c.Alpha > 200 && c.Red < 40 && c.Green < 40 && c.Blue < 40).Should().BeGreaterThan(10);
    }

    [Fact]
    public void Uses_the_larger_requested_dimension_with_a_16px_floor()
    {
        using var wide = Render(new FontImageSource { Glyph = "A" }, 64, 20);
        wide!.Width.Should().Be(64);

        using var tiny = Render(new FontImageSource { Glyph = "A" }, 4, 4);
        tiny!.Width.Should().Be(16);
    }

    [Fact]
    public void Empty_glyph_renders_nothing()
    {
        Render(new FontImageSource { Glyph = "" }).Should().BeNull();
    }

    [Fact]
    public void Unknown_font_family_still_renders_with_a_fallback_face()
    {
        using var bitmap = Render(new FontImageSource { Glyph = "X", FontFamily = "NoSuchFontFamily-OpenMaui", Color = Colors.Blue }, 32, 32);

        bitmap.Should().NotBeNull();
        CountPixels(bitmap!, c => c.Alpha > 200 && c.Blue > 200).Should().BeGreaterThan(20);
    }

    [Fact]
    public void Image_with_a_FontImageSource_gets_a_bitmap()
    {
        var image = new Image { Source = new FontImageSource { Glyph = "B", Color = Colors.Black }, WidthRequest = 32, HeightRequest = 32 };
        var platform = HeadlessMauiContext.Realize<SkiaImage>(image);

        // The loader is asynchronous; it completes inline for font sources but
        // give it a moment in case the dispatcher defers.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (platform.Bitmap == null && DateTime.UtcNow < deadline)
            Thread.Sleep(10);

        platform.Bitmap.Should().NotBeNull("the handler rendered the glyph into the image view");
    }
}
