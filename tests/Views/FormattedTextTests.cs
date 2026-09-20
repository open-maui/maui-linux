// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Maui.Controls.Linux.Tests.Golden;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using PointerButton = Microsoft.Maui.Platform.PointerButton;
using Rect = Microsoft.Maui.Graphics.Rect;
using Size = Microsoft.Maui.Graphics.Size;

/// <summary>
/// Label.FormattedText: spans with their own font attributes, size, colour,
/// decorations and family lay out and draw as such, measure follows the
/// spans, and Span.GestureRecognizers receive taps on the span's drawn rect.
/// </summary>
public class FormattedTextTests
{
    private static FormattedString Formatted(params Span[] spans)
    {
        var f = new FormattedString();
        foreach (var s in spans) f.Spans.Add(s);
        return f;
    }

    private static readonly Size Unbounded = new(double.PositiveInfinity, double.PositiveInfinity);

    // Dark ink (black text / lines); coloured spans deliberately do not count.
    private static bool Ink(SKColor c) => c.Alpha > 128 && c.Red < 110 && c.Green < 110 && c.Blue < 110;

    private static bool IsColor(SKColor actual, SKColor expected, int tolerance = 40)
        => Math.Abs(actual.Red - expected.Red) <= tolerance
        && Math.Abs(actual.Green - expected.Green) <= tolerance
        && Math.Abs(actual.Blue - expected.Blue) <= tolerance;

    private static IEnumerable<(int x, int y)> Pixels(SKBitmap bmp, SKRectI area)
    {
        for (int y = Math.Max(0, area.Top); y < Math.Min(bmp.Height, area.Bottom); y++)
            for (int x = Math.Max(0, area.Left); x < Math.Min(bmp.Width, area.Right); x++)
                yield return (x, y);
    }

    // ---- Measure ------------------------------------------------------------

    [Fact]
    public void Measure_grows_with_a_larger_span()
    {
        var small = new SkiaLabel { FontSize = 14, FormattedText = Formatted(new Span { Text = "Hello" }, new Span { Text = " world" }) };
        var large = new SkiaLabel { FontSize = 14, FormattedText = Formatted(new Span { Text = "Hello" }, new Span { Text = " world", FontSize = 32 }) };

        var s = small.Measure(Unbounded);
        var l = large.Measure(Unbounded);

        l.Width.Should().BeGreaterThan(s.Width, "a 32pt span is wider than a 14pt one");
        l.Height.Should().BeGreaterThan(s.Height, "the line is as tall as its largest span");
        l.Height.Should().BeApproximately(32 * 1.2, 0.5, "line height = largest font size x default multiplier");
    }

    [Fact]
    public void Measure_matches_the_plain_label_for_a_single_span()
    {
        var plain = new SkiaLabel { Text = "Sample text", FontSize = 16 };
        var formatted = new SkiaLabel { FontSize = 16, FormattedText = Formatted(new Span { Text = "Sample text" }) };

        formatted.Measure(Unbounded).Width.Should().BeApproximately(plain.Measure(Unbounded).Width, 0.5);
        formatted.Measure(Unbounded).Height.Should().BeApproximately(plain.Measure(Unbounded).Height, 0.5);
    }

    [Fact]
    public void Bold_span_is_wider_than_a_regular_span()
    {
        var regular = new SkiaLabel { FontSize = 16, FormattedText = Formatted(new Span { Text = "Weight matters" }) };
        var bold = new SkiaLabel { FontSize = 16, FormattedText = Formatted(new Span { Text = "Weight matters", FontAttributes = FontAttributes.Bold }) };

        var ctx = new RenderContextPropagationTestsContext();
        regular.RenderContext = ctx; bold.RenderContext = ctx;

        bold.Measure(Unbounded).Width.Should().BeGreaterThan(regular.Measure(Unbounded).Width);
    }

    [Fact]
    public void Spaces_between_spans_are_preserved_in_the_advance()
    {
        var joined = new SkiaLabel { FontSize = 16, FormattedText = Formatted(new Span { Text = "Hello" }, new Span { Text = "World" }) };
        var spaced = new SkiaLabel { FontSize = 16, FormattedText = Formatted(new Span { Text = "Hello " }, new Span { Text = "World" }) };

        spaced.Measure(Unbounded).Width.Should().BeGreaterThan(joined.Measure(Unbounded).Width, "a trailing space is an advance, not ink");
    }

    [Fact]
    public void Formatted_text_wraps_at_the_available_width()
    {
        var label = new SkiaLabel
        {
            FontSize = 16,
            FormattedText = Formatted(new Span { Text = "one two three four five six seven eight nine ten " }, new Span { Text = "eleven twelve", FontAttributes = FontAttributes.Bold }),
        };
        var single = label.Measure(Unbounded);
        var wrapped = label.Measure(new Size(120, double.PositiveInfinity));

        wrapped.Width.Should().BeLessThanOrEqualTo(120.5);
        wrapped.Height.Should().BeGreaterThan(single.Height * 2, "the text needs several lines at 120px");
    }

    [Fact]
    public void Newlines_inside_a_span_force_line_breaks()
    {
        var one = new SkiaLabel { FontSize = 16, FormattedText = Formatted(new Span { Text = "a" }) };
        var three = new SkiaLabel { FontSize = 16, FormattedText = Formatted(new Span { Text = "a\nb\nc" }) };

        three.Measure(Unbounded).Height.Should().BeApproximately(one.Measure(Unbounded).Height * 3, 0.5);
    }

    [Fact]
    public void Padding_and_size_requests_apply_to_formatted_text()
    {
        var label = new SkiaLabel { FontSize = 16, Padding = new Thickness(10, 5), FormattedText = Formatted(new Span { Text = "x" }) };
        var bare = new SkiaLabel { FontSize = 16, FormattedText = Formatted(new Span { Text = "x" }) };
        label.Measure(Unbounded).Width.Should().BeApproximately(bare.Measure(Unbounded).Width + 20, 0.01);
        label.Measure(Unbounded).Height.Should().BeApproximately(bare.Measure(Unbounded).Height + 10, 0.01);

        label.WidthRequest = 300; label.HeightRequest = 50;
        label.Measure(Unbounded).Should().Be(new Size(300, 50));
    }

    // ---- Pixels -------------------------------------------------------------

    private static (SkiaLabel label, SKBitmap bmp) RenderLabel(SkiaLabel label, int w = 320, int h = 80, float scale = 1f)
    {
        var root = new SkiaAbsoluteLayout();
        root.AddChild(label);
        root.SetLayoutBounds(label, new SKRect(0, 0, w, h));
        return (label, GoldenHarness.Render(root, w, h, scale));
    }

    [Fact]
    public void Span_text_colour_shows_in_the_pixels()
    {
        var red = new Span { Text = "RED", TextColor = Colors.Red, FontSize = 24 };
        var label = new SkiaLabel { FontSize = 24, TextColor = Colors.Black, FormattedText = Formatted(new Span { Text = "black " }, red) };
        var (_, bmp) = RenderLabel(label);

        var rect = label.GetSpanRects(red).Single();
        var area = new SKRectI((int)rect.Left, (int)rect.Top, (int)rect.Right, (int)rect.Bottom);
        var reds = Pixels(bmp, area).Count(p => IsColor(bmp.GetPixel(p.x, p.y), SKColors.Red));
        var blacksInRed = Pixels(bmp, area).Count(p => Ink(bmp.GetPixel(p.x, p.y)));
        reds.Should().BeGreaterThan(20, "the RED span is drawn in red");
        blacksInRed.Should().Be(0, "no black ink inside the red span's rect");

        var firstRect = label.GetSpanRects(label.FormattedText!.Spans[0]).Single();
        var firstArea = new SKRectI((int)firstRect.Left, (int)firstRect.Top, (int)firstRect.Right, (int)firstRect.Bottom);
        Pixels(bmp, firstArea).Count(p => Ink(bmp.GetPixel(p.x, p.y))).Should().BeGreaterThan(20, "the black span is drawn in black");
    }

    [Fact]
    public void Underlined_span_draws_a_line_below_its_text()
    {
        var underlined = new Span { Text = "under", TextDecorations = TextDecorations.Underline, FontSize = 24 };
        var label = new SkiaLabel { FontSize = 24, TextColor = Colors.Black, FormattedText = Formatted(new Span { Text = "plain " }, underlined) };
        var (_, bmp) = RenderLabel(label);

        var rect = label.GetSpanRects(underlined).Single();
        int left = (int)rect.Left, right = (int)rect.Right;

        // Find a row within the span rect where ink spans (almost) the full width: the underline.
        bool foundLine = false;
        // The underline sits just under the baseline, which for a 1.2x line box
        // is within a couple of pixels of the box's bottom edge.
        for (int y = (int)rect.Top; y < (int)rect.Bottom + 4 && !foundLine; y++)
        {
            int inked = Enumerable.Range(left, Math.Max(1, right - left)).Count(x => Ink(bmp.GetPixel(x, y)));
            foundLine = inked >= (right - left) * 0.9;
        }
        foundLine.Should().BeTrue("an underline is a continuous row of ink across the span");

        // The plain span has no such row.
        var plainRect = label.GetSpanRects(label.FormattedText!.Spans[0]).Single();
        int pl = (int)plainRect.Left, pr = (int)plainRect.Right;
        bool plainHasLine = false;
        for (int y = (int)plainRect.Top; y < (int)plainRect.Bottom + 4; y++)
        {
            int inked = Enumerable.Range(pl, Math.Max(1, pr - pl - 4)).Count(x => Ink(bmp.GetPixel(x, y)));
            if (inked >= (pr - pl - 4) * 0.9) plainHasLine = true;
        }
        plainHasLine.Should().BeFalse();
    }

    [Fact]
    public void Strikethrough_span_draws_a_line_through_its_text()
    {
        var struck = new Span { Text = "struck", TextDecorations = TextDecorations.Strikethrough, FontSize = 24 };
        var label = new SkiaLabel { FontSize = 24, TextColor = Colors.Black, FormattedText = Formatted(struck) };
        var (_, bmp) = RenderLabel(label);

        var rect = label.GetSpanRects(struck).Single();
        int left = (int)rect.Left, right = (int)rect.Right;
        int midTop = (int)(rect.Top + rect.Height * 0.3), midBottom = (int)(rect.Top + rect.Height * 0.7);
        bool found = Enumerable.Range(midTop, midBottom - midTop).Any(y =>
            Enumerable.Range(left, right - left).Count(x => Ink(bmp.GetPixel(x, y))) >= (right - left) * 0.9);
        found.Should().BeTrue("a strikethrough is a continuous row through the middle of the span");
    }

    // Slant of the glyphs in a crop: leftmost ink x in the top third minus bottom third (>0 = italic).
    private static int Slant(SKBitmap bmp, SKRectI area)
    {
        bool InkAt(int x, int y) => Ink(bmp.GetPixel(x, y));
        var rows = Enumerable.Range(area.Top, area.Height).Where(y => Enumerable.Range(area.Left, area.Width).Any(x => InkAt(x, y))).ToList();
        if (rows.Count == 0) return int.MinValue;
        int top = rows.First(), bot = rows.Last(), third = Math.Max(1, (bot - top) / 3);
        int Left(int y0, int y1) => Enumerable.Range(y0, y1 - y0 + 1).SelectMany(y => Enumerable.Range(area.Left, area.Width).Where(x => InkAt(x, y))).DefaultIfEmpty(-1).Min();
        return Left(top, top + third) - Left(bot - third, bot);
    }

    [Fact]
    public void Italic_span_slants_while_the_regular_span_does_not()
    {
        var italic = new Span { Text = "lllll", FontAttributes = FontAttributes.Italic, FontSize = 28 };
        var regular = new Span { Text = "lllll", FontSize = 28 };
        var label = new SkiaLabel { FontSize = 28, TextColor = Colors.Black, FormattedText = Formatted(regular, new Span { Text = "   " }, italic) };
        var (_, bmp) = RenderLabel(label, 400, 80, 2f);

        SKRectI Area(Span s)
        {
            var r = label.GetSpanRects(s).Single();
            return new SKRectI((int)(r.Left * 2), (int)(r.Top * 2), (int)(r.Right * 2), (int)(r.Bottom * 2));
        }

        Slant(bmp, Area(italic)).Should().BeGreaterThan(0, "the italic span resolves the italic typeface");
        Slant(bmp, Area(regular)).Should().BeLessThanOrEqualTo(1, "the regular span stays upright");
    }

    [Fact]
    public void Span_font_family_is_resolved_per_span()
    {
        // Two spans with the same text: a monospace family advances differently
        // from the default sans family, so the run widths differ.
        var sans = new Span { Text = "iiiiiiiiii", FontSize = 20 };
        var mono = new Span { Text = "iiiiiiiiii", FontSize = 20, FontFamily = "monospace" };
        var label = new SkiaLabel { FontSize = 20, FormattedText = Formatted(sans, new Span { Text = " " }, mono) };
        RenderLabel(label, 400, 60);

        var sansWidth = label.GetSpanRects(sans).Single().Width;
        var monoWidth = label.GetSpanRects(mono).Single().Width;
        monoWidth.Should().BeGreaterThan(sansWidth * 1.3, "ten i's are much wider in a monospace face");
    }

    [Fact]
    public void Span_rects_are_recorded_in_label_local_coordinates_after_draw()
    {
        var a = new Span { Text = "first " };
        var b = new Span { Text = "second" };
        var label = new SkiaLabel { FontSize = 16, Padding = new Thickness(4), FormattedText = Formatted(a, b) };

        label.GetSpanRects(a).Should().BeEmpty("nothing has been drawn yet");

        var root = new SkiaAbsoluteLayout();
        root.AddChild(label);
        root.SetLayoutBounds(label, new SKRect(50, 40, 350, 80));
        using var _ = GoldenHarness.Render(root, 400, 100, 1f);

        var ra = label.GetSpanRects(a).Single();
        var rb = label.GetSpanRects(b).Single();
        ra.Left.Should().BeApproximately(4, 0.01, "local to the label, after padding");
        ra.Top.Should().BeApproximately(4, 0.01);
        rb.Left.Should().BeApproximately(ra.Right, 0.01, "the second span starts where the first ends");
        rb.Width.Should().BeGreaterThan(0);
        ra.Height.Should().BeApproximately(16 * 1.2, 0.5);
    }

    [Fact]
    public void Wrapped_span_records_one_rect_per_line()
    {
        var span = new Span { Text = "alpha beta gamma delta epsilon zeta eta theta iota kappa" };
        var label = new SkiaLabel { FontSize = 16, FormattedText = Formatted(span) };
        RenderLabel(label, 120, 200);

        var rects = label.GetSpanRects(span);
        rects.Count.Should().BeGreaterThan(1);
        rects.Select(r => r.Top).Should().BeInAscendingOrder();
        rects.All(r => r.Right <= 120.5).Should().BeTrue();
    }

    // ---- Span gestures ------------------------------------------------------

    private static (Label label, SkiaLabel platform) HostedLabel(FormattedString formatted, double x, double y, double w, double h)
    {
        var label = new Label { FontSize = 20, FormattedText = formatted };
        var handler = new LabelHandler();
        handler.SetVirtualView(label);
        var platform = handler.PlatformView;
        platform.Arrange(new Rect(x, y, w, h));
        return (label, platform);
    }

    private static void Tap(SkiaView v, float x, float y)
    {
        v.OnPointerPressed(new PointerEventArgs(x, y, PointerButton.Left));
        v.OnPointerReleased(new PointerEventArgs(x, y, PointerButton.Left));
    }

    [Fact]
    public void Tap_on_a_span_fires_the_spans_recognizer_and_not_its_neighbour()
    {
        var first = new Span { Text = "first " };
        var second = new Span { Text = "second" };
        int firstTaps = 0, secondTaps = 0;
        object? sender = null;
        first.GestureRecognizers.Add(new TapGestureRecognizer().With(t => t.Tapped += (s, _) => { firstTaps++; sender = s; }));
        second.GestureRecognizers.Add(new TapGestureRecognizer().With(t => t.Tapped += (_, _) => secondTaps++));

        var (label, platform) = HostedLabel(Formatted(first, second), 100, 50, 300, 40);
        using (var canvas = new SKCanvas(new SKBitmap(400, 100)))
            platform.Draw(canvas); // records span rects

        var r1 = platform.GetSpanRects(first).Single();
        var r2 = platform.GetSpanRects(second).Single();

        Tap(platform, (float)(100 + r1.Center.X), (float)(50 + r1.Center.Y));
        firstTaps.Should().Be(1);
        secondTaps.Should().Be(0);
        sender.Should().BeSameAs(label, "MAUI raises span taps with the host label as sender");

        Tap(platform, (float)(100 + r2.Center.X), (float)(50 + r2.Center.Y));
        firstTaps.Should().Be(1);
        secondTaps.Should().Be(1);
    }

    [Fact]
    public void Tap_outside_every_span_does_not_fire_span_recognizers()
    {
        var span = new Span { Text = "short" };
        int taps = 0;
        span.GestureRecognizers.Add(new TapGestureRecognizer().With(t => t.Tapped += (_, _) => taps++));

        var (_, platform) = HostedLabel(Formatted(span), 0, 0, 300, 40);
        using (var canvas = new SKCanvas(new SKBitmap(400, 100)))
            platform.Draw(canvas);

        Tap(platform, 290, 20); // far right of the label, past the text
        taps.Should().Be(0);
    }

    [Fact]
    public void Span_tap_runs_the_command_and_the_label_recognizer_still_fires()
    {
        var span = new Span { Text = "link" };
        object? param = null;
        span.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command<object>(p => param = p), CommandParameter = "go" });

        var (label, platform) = HostedLabel(Formatted(span), 0, 0, 300, 40);
        int labelTaps = 0;
        label.GestureRecognizers.Add(new TapGestureRecognizer().With(t => t.Tapped += (_, _) => labelTaps++));
        using (var canvas = new SKCanvas(new SKBitmap(400, 100)))
            platform.Draw(canvas);

        var r = platform.GetSpanRects(span).Single();
        Tap(platform, (float)r.Center.X, (float)r.Center.Y);

        param.Should().Be("go");
        labelTaps.Should().Be(1);
    }

    [Fact]
    public void Span_tap_before_any_draw_is_ignored_safely()
    {
        var span = new Span { Text = "x" };
        int taps = 0;
        span.GestureRecognizers.Add(new TapGestureRecognizer().With(t => t.Tapped += (_, _) => taps++));
        var (_, platform) = HostedLabel(Formatted(span), 0, 0, 100, 40);

        var act = () => Tap(platform, 5, 5);
        act.Should().NotThrow();
        taps.Should().Be(0, "no region has been published yet");
    }

    // ---- Golden -------------------------------------------------------------

    public static IEnumerable<object[]> Scales => GoldenHarness.Scales.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Scales))]
    public void Formatted_text_golden(float scale)
    {
        var root = new SkiaStackLayout { Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical, Spacing = 8, Padding = new Thickness(12) };
        root.AddChild(new SkiaLabel
        {
            FontSize = 16,
            FormattedText = Formatted(
                new Span { Text = "Regular, " },
                new Span { Text = "bold, ", FontAttributes = FontAttributes.Bold },
                new Span { Text = "italic, ", FontAttributes = FontAttributes.Italic },
                new Span { Text = "red", TextColor = Colors.Red }),
        });
        root.AddChild(new SkiaLabel
        {
            FontSize = 14,
            FormattedText = Formatted(
                new Span { Text = "Small " },
                new Span { Text = "LARGE ", FontSize = 26 },
                new Span { Text = "underlined ", TextDecorations = TextDecorations.Underline },
                new Span { Text = "struck", TextDecorations = TextDecorations.Strikethrough }),
        });
        root.AddChild(new SkiaLabel
        {
            FontSize = 15,
            FormattedText = Formatted(
                new Span { Text = "A paragraph long enough to wrap onto more than one line, with a " },
                new Span { Text = "highlighted", BackgroundColor = Colors.Yellow, FontAttributes = FontAttributes.Bold },
                new Span { Text = " span in the middle and a " },
                new Span { Text = "monospace", FontFamily = "monospace" },
                new Span { Text = " one at the end." }),
        });
        GoldenHarness.Verify("formatted-text", root, 340, 200, scale);
    }
}

internal sealed class RenderContextPropagationTestsContext : Microsoft.Maui.Platform.Linux.Rendering.IRenderContext
{
    public Microsoft.Maui.Platform.Linux.Rendering.ResourceCache Resources { get; } = new();
    public float DpiScale => 1f;
    public void Invalidate() { }
    public void InvalidateRegion(SKRect rect) { }
}

internal static class FormattedTextTestExtensions
{
    public static T With<T>(this T recognizer, Action<T> configure)
    {
        configure(recognizer);
        return recognizer;
    }
}
