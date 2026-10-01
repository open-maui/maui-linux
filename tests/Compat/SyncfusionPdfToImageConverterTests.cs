// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Pdf.Syncfusion;
using SkiaSharp;
using Syncfusion.Maui.PdfToImageConverter;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// Syncfusion's PdfToImageConverter (behind SfPdfViewer) renders on Linux: its platform-neutral
/// build throws NotImplementedException for page count and rendering; UseLinuxPdf renders with
/// PDFium at the Windows build's sizes (96 DPI, crop in points, times the scale factor), as PNG.
/// </summary>
public sealed class SyncfusionPdfToImageConverterTests
{
    // Page 1: 150 x 75 points, a red square at (15,15)-(45,45). Page 2: 75 x 75 points, blue.
    private static MemoryStream SamplePdf()
    {
        var stream = new MemoryStream();
        using (var document = SKDocument.CreatePdf(stream))
        {
            var canvas = document.BeginPage(150, 75);
            canvas.Clear(SKColors.White);
            using (var red = new SKPaint { Color = SKColors.Red })
                canvas.DrawRect(new SKRect(15, 15, 45, 45), red);
            document.EndPage();
            canvas = document.BeginPage(75, 75);
            canvas.Clear(SKColors.Blue);
            document.EndPage();
        }
        stream.Position = 0;
        return stream;
    }

    private static SKBitmap Decode(Stream? png)
    {
        png.Should().NotBeNull();
        var bitmap = SKBitmap.Decode(png!);
        bitmap.Should().NotBeNull("the page is a PNG");
        return bitmap;
    }

    [Fact]
    public void Pages_are_counted_and_rendered_at_96_dpi()
    {
        PdfToImageConverterPatches.Install();
        using var converter = new PdfToImageConverter(SamplePdf());
        converter.PageCount.Should().Be(2);

        using var page = Decode(converter.Convert(0));
        page.Width.Should().Be(200, "150 points at 96 DPI");
        page.Height.Should().Be(100);
        page.GetPixel(40, 40).Should().Be(SKColors.Red, "the square spans 20..60 px");
        page.GetPixel(150, 50).Should().Be(SKColors.White);
    }

    [Fact]
    public async Task Sizes_crops_scales_and_ranges_follow_the_windows_build()
    {
        PdfToImageConverterPatches.Install();
        using var converter = new PdfToImageConverter(SamplePdf());

        using (var sized = Decode(converter.Convert(0, new Size(300, 150))))
            sized.Width.Should().Be(300);
        using (var scaled = Decode(converter.Convert(0, 2f)))
            scaled.Width.Should().Be(400);
        using (var cropped = Decode(converter.Convert(0, new Rect(15, 15, 30, 30))))
        {
            cropped.Width.Should().Be(40, "30 points at 96 DPI");
            cropped.GetPixel(20, 20).Should().Be(SKColors.Red, "the crop is the square");
        }

        var all = await converter.ConvertAsync();
        all.Should().NotBeNull().And.HaveCount(2);
        using (var second = Decode(all![1]))
            second.GetPixel(10, 10).Should().Be(SKColors.Blue);
    }
}
