// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Rendering;

/// <summary>
/// Shapes, BoxViews, GraphicsViews and DrawingViews draw through one MAUI SkiaCanvas per thread:
/// a new SkiaCanvas per draw built its default paints and font every time and made pages with
/// many of them redraw and resize slowly.
/// </summary>
public class MauiCanvasCacheTests
{
    [Fact]
    public void Draws_on_a_thread_share_one_canvas_pointed_at_the_frames_canvas()
    {
        using var bitmap = new SKBitmap(10, 10);
        using var first = new SKCanvas(bitmap);
        using var second = new SKCanvas(bitmap);

        Graphics.Skia.SkiaCanvas a, b;
        using (var lease = MauiCanvasCache.Rent(first))
        {
            a = lease.Canvas;
            a.Canvas.Should().BeSameAs(first);
        }
        using (var lease = MauiCanvasCache.Rent(second))
        {
            b = lease.Canvas;
            b.Canvas.Should().BeSameAs(second);
        }

        b.Should().BeSameAs(a, "the canvas is reused, not rebuilt for every draw");
        a.Canvas.Should().BeNull("a released canvas does not keep the frame's canvas");
    }

    [Fact]
    public void A_draw_inside_another_gets_its_own_canvas()
    {
        using var bitmap = new SKBitmap(10, 10);
        using var canvas = new SKCanvas(bitmap);

        using var outer = MauiCanvasCache.Rent(canvas);
        using var inner = MauiCanvasCache.Rent(canvas);

        inner.Canvas.Should().NotBeSameAs(outer.Canvas, "the outer draw is still using the shared canvas");
    }
}
