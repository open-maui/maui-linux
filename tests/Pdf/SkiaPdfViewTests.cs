// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Pdf;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Pdf;

/// <summary>SkiaPdfView: pages at 96 DPI times the zoom, centred, rendered in the background; scrolling, page tracking and zoom.</summary>
public class SkiaPdfViewTests
{
    // Page 1: 150 x 75 points (200 x 100 px at 96 DPI), red square at (15,15)-(45,45) pt = (20,20)-(60,60) px.
    // Page 2: 150 x 75 points, blue.
    private static PdfiumDocument SamplePdf(int pages = 2)
    {
        using var stream = new MemoryStream();
        using (var document = SKDocument.CreatePdf(stream))
        {
            for (int i = 0; i < pages; i++)
            {
                var canvas = document.BeginPage(150, 75);
                canvas.Clear(i == 0 ? SKColors.White : SKColors.Blue);
                if (i == 0)
                    using (var red = new SKPaint { Color = SKColors.Red })
                        canvas.DrawRect(new SKRect(15, 15, 45, 45), red);
                document.EndPage();
            }
        }
        return PdfiumDocument.Open(stream.ToArray());
    }

    private static SkiaPdfView Laid(PdfiumDocument document, int width, int height, bool horizontal = false)
    {
        var view = new SkiaPdfView { IsHorizontal = horizontal, ShadowEnabled = false };
        view.Load(document);
        view.Measure(new Size(width, height));
        view.Arrange(new Rect(0, 0, width, height));
        return view;
    }

    /// <summary>Draws until the pages in view are rendered (they render in the background).</summary>
    private static SKBitmap Draw(SkiaPdfView view, int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        for (int attempt = 0; attempt < 50; attempt++)
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Gray);
            view.Draw(canvas);
            Thread.Sleep(40);
            using var again = new SKCanvas(bitmap);
            again.Clear(SKColors.Gray);
            view.Draw(again);
            if (bitmap.GetPixel(width / 2, 30) != SKColors.White || attempt > 5)
                break;
        }
        return bitmap;
    }

    [Fact]
    public void Pages_sit_at_96_dpi_centred_with_their_content()
    {
        using var view = Laid(SamplePdf(), 400, 300);
        using var bitmap = Draw(view, 400, 300);
        // Page 1 is 200 wide, centred in 400: x 100..300, below the 8 px top margin.
        bitmap.GetPixel(100 + 40, 8 + 40).Should().Be(SKColors.Red, "the square is drawn where PDFium put it");
        bitmap.GetPixel(100 + 150, 8 + 50).Should().Be(SKColors.White);
        bitmap.GetPixel(50, 50).Should().Be(SKColors.Gray, "outside the page");
        // Page 2 follows after 100 + 8 + 8 px.
        bitmap.GetPixel(200, 8 + 100 + 16 + 50).Should().Be(SKColors.Blue);
    }

    [Fact]
    public void Scrolling_tracks_the_page_in_view_and_PageIndex_scrolls_to_a_page()
    {
        using var view = Laid(SamplePdf(5), 400, 120);
        var changes = new List<int>();
        view.PageChanged += (_, e) => changes.Add(e.PageIndex);
        view.PageIndex.Should().Be(0);

        view.OnScroll(new ScrollEventArgs(200, 60, 0, 3)); // 120 px down: page 2 fills the view
        view.PageIndex.Should().Be(1);

        view.PageIndex = 4;
        view.PageIndex.Should().Be(4);
        changes.Should().Equal(1, 4);
    }

    [Fact]
    public void Ctrl_wheel_zooms_within_MaxZoom_keeping_the_point_under_the_pointer()
    {
        // 300 wide: at 2x the strip is 432 wide, room to scroll the point back under the pointer.
        using var view = Laid(SamplePdf(), 300, 300);
        view.MaxZoom = 2;
        // The pointer on the red square's centre: page 1 at x 50, y 8; the square's centre 40 px in.
        view.OnScroll(new ScrollEventArgs(90, 48, 0, -20, KeyModifiers.Control));
        view.Zoom.Should().Be(2, "clamped to MaxZoom");
        using var bitmap = Draw(view, 300, 300);
        bitmap.GetPixel(90, 48).Should().Be(SKColors.Red, "the point under the pointer stays under it");
        // The square is now 80 px wide: 35 px either side of its centre is still inside it.
        bitmap.GetPixel(90 - 35, 48).Should().Be(SKColors.Red);
        bitmap.GetPixel(90 + 35, 48).Should().Be(SKColors.Red);
    }

    [Fact]
    public void A_horizontal_strip_lays_pages_side_by_side()
    {
        using var view = Laid(SamplePdf(), 600, 200, horizontal: true);
        using var bitmap = Draw(view, 600, 200);
        // Page 1 at x 16..216, centred vertically in 200: y 50..150; page 2 from 216 + 32.
        bitmap.GetPixel(16 + 40, 50 + 40).Should().Be(SKColors.Red);
        bitmap.GetPixel(216 + 32 + 100, 100).Should().Be(SKColors.Blue);
    }
}
