// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Pdf;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Pdf;

/// <summary>PDFium renders and reads a PDF: SkiaSharp writes one with known content to check against.</summary>
public class PdfiumDocumentTests
{
    // Page 1: 200 x 100 points, white, a red square at (10,10)-(60,60), the word "Citation".
    // Page 2: 300 x 400 points, blue.
    private static byte[] SamplePdf()
    {
        using var stream = new MemoryStream();
        using (var document = SKDocument.CreatePdf(stream))
        {
            var canvas = document.BeginPage(200, 100);
            canvas.Clear(SKColors.White);
            using (var red = new SKPaint { Color = SKColors.Red })
                canvas.DrawRect(new SKRect(10, 10, 60, 60), red);
            using (var font = new SKFont(SKTypeface.Default, 14))
            using (var black = new SKPaint { Color = SKColors.Black })
                canvas.DrawText("Citation", 80, 50, font, black);
            document.EndPage();

            canvas = document.BeginPage(300, 400);
            canvas.Clear(SKColors.Blue);
            document.EndPage();
        }
        return stream.ToArray();
    }

    private static (byte R, byte G, byte B) At(SKBitmap bitmap, int x, int y)
    {
        var c = bitmap.GetPixel(x, y);
        return (c.Red, c.Green, c.Blue);
    }

    [Fact]
    public void Pages_and_their_sizes_are_read()
    {
        using var pdf = PdfiumDocument.Open(SamplePdf());
        pdf.PageCount.Should().Be(2);
        pdf.GetPageSize(0).Should().Be(new SKSize(200, 100));
        pdf.GetPageSize(1).Should().Be(new SKSize(300, 400));
    }

    [Fact]
    public void A_page_renders_at_the_size_asked_with_its_content_in_place()
    {
        using var pdf = PdfiumDocument.Open(SamplePdf());
        using var bitmap = pdf.RenderPage(0, 400, 200); // 2 px per point
        bitmap.Width.Should().Be(400);
        bitmap.Height.Should().Be(200);
        At(bitmap, 70, 70).Should().Be(((byte)255, (byte)0, (byte)0), "the red square spans 20..120 px");
        At(bitmap, 140, 20).Should().Be(((byte)255, (byte)255, (byte)255), "the page is white beside it");

        using var second = pdf.RenderPage(1, 1f);
        second.Width.Should().Be(300);
        At(second, 150, 200).Should().Be(((byte)0, (byte)0, (byte)255));
    }

    [Fact]
    public void A_region_fills_the_bitmap()
    {
        using var pdf = PdfiumDocument.Open(SamplePdf());
        // The red square alone, magnified to 100 x 100.
        using var bitmap = pdf.RenderRegion(0, new SKRect(10, 10, 60, 60), 100, 100);
        At(bitmap, 5, 5).Should().Be(((byte)255, (byte)0, (byte)0));
        At(bitmap, 95, 95).Should().Be(((byte)255, (byte)0, (byte)0));
    }

    [Fact]
    public void A_page_text_is_extracted()
    {
        using var pdf = PdfiumDocument.Open(SamplePdf());
        pdf.GetPageText(0).Should().Contain("Citation");
        pdf.GetPageText(1).Should().BeEmpty();
    }

    [Fact]
    public void A_damaged_file_is_reported_and_a_bad_page_rejected()
    {
        var open = () => PdfiumDocument.Open(new byte[] { 1, 2, 3, 4 });
        open.Should().Throw<PdfiumException>().Which.Error.Should().Be(PdfiumError.Format);

        using var pdf = PdfiumDocument.Open(SamplePdf());
        var render = () => pdf.RenderPage(5, 10, 10);
        render.Should().Throw<ArgumentOutOfRangeException>();
    }
}
