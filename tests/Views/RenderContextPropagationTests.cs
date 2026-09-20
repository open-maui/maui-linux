// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Controls.Linux.Tests.Golden;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

using StackOrientation = Microsoft.Maui.Platform.StackOrientation;

/// <summary>
/// Views inside layouts must see the engine's render context (typeface
/// resolution, resource cache). Containers keep their own child lists, so the
/// context is resolved through the parent chain; before that fix every view
/// inside a layout fell back to SKTypeface.Default and italic / FontFamily
/// were ignored. Found by the golden tests.
/// </summary>
public class RenderContextPropagationTests
{
    private sealed class FakeContext : IRenderContext
    {
        public ResourceCache Resources { get; } = new();
        public float DpiScale => 1f;
        public void Invalidate() { }
        public void InvalidateRegion(SKRect rect) { }
    }

    [Fact]
    public void Children_of_a_layout_resolve_the_root_context()
    {
        var root = new SkiaStackLayout { Orientation = StackOrientation.Vertical };
        var inner = new SkiaStackLayout { Orientation = StackOrientation.Horizontal };
        var label = new SkiaLabel { Text = "x" };
        inner.AddChild(label);
        root.AddChild(inner);

        var ctx = new FakeContext();
        root.RenderContext = ctx;

        inner.RenderContext.Should().BeSameAs(ctx);
        label.RenderContext.Should().BeSameAs(ctx);
    }

    [Fact]
    public void Children_added_after_the_context_is_set_resolve_it_too()
    {
        var root = new SkiaStackLayout();
        var ctx = new FakeContext();
        root.RenderContext = ctx;

        var label = new SkiaLabel { Text = "late" };
        root.AddChild(label);

        label.RenderContext.Should().BeSameAs(ctx);
    }

    // Slant of the first glyph: leftmost ink x in the top third minus bottom third (>0 = italic).
    private static int Slant(SKBitmap bmp)
    {
        int w = System.Math.Min(bmp.Width, 60), h = bmp.Height;
        bool Ink(int x, int y) { var c = bmp.GetPixel(x, y); return c.Alpha > 128 && (c.Red + c.Green + c.Blue) / 3 < 128; }
        var rows = Enumerable.Range(0, h).Where(y => Enumerable.Range(0, w).Any(x => Ink(x, y))).ToList();
        if (rows.Count == 0) return int.MinValue;
        int top = rows.First(), bot = rows.Last(), third = System.Math.Max(1, (bot - top) / 3);
        int Left(int y0, int y1) => Enumerable.Range(y0, y1 - y0 + 1).SelectMany(y => Enumerable.Range(0, w).Where(x => Ink(x, y))).DefaultIfEmpty(-1).Min();
        return Left(top, top + third) - Left(bot - third, bot);
    }

    [Fact]
    public void Italic_label_inside_a_layout_renders_slanted()
    {
        var root = new SkiaStackLayout { Orientation = StackOrientation.Vertical, Spacing = 8, Padding = new Thickness(12) };
        root.AddChild(new SkiaLabel { Text = "Regular text", FontSize = 16 });
        var italic = new SkiaLabel { Text = "Italic text", FontSize = 16, FontAttributes = FontAttributes.Italic };
        root.AddChild(italic);

        using var bmp = GoldenHarness.Render(root, 200, 100, 1.75f);
        var rect = new SKRectI(0, (int)(italic.Bounds.Top * 1.75f), 100, (int)(italic.Bounds.Bottom * 1.75f));
        using var crop = new SKBitmap(rect.Width, rect.Height);
        bmp.ExtractSubset(crop, rect);

        Slant(crop).Should().BeGreaterThan(0, "an italic label inside a layout must resolve the italic typeface");
    }
}
