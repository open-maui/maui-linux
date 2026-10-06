// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using PointerButton = Microsoft.Maui.Platform.PointerButton;
using Rect = Microsoft.Maui.Graphics.Rect;

/// <summary>
/// Selecting FormattedText: a drag over the drawn spans selects across spans and wrapped
/// lines, and SelectedText is the spans' text, as a platform label's selection is.
/// </summary>
public class FormattedTextSelectionTests
{
    private static SkiaLabel Drawn(double width, params Span[] spans)
    {
        var formatted = new FormattedString();
        foreach (var s in spans) formatted.Spans.Add(s);
        var label = new Label { FontSize = 20, FormattedText = formatted, LineBreakMode = LineBreakMode.WordWrap };
        var handler = new LabelHandler();
        handler.SetVirtualView(label);
        var platform = handler.PlatformView;
        platform.Arrange(new Rect(0, 0, width, 200));
        using (var canvas = new SKCanvas(new SKBitmap((int)width, 200)))
            platform.Draw(canvas); // records where the runs were drawn
        return platform;
    }

    private static void Drag(SkiaLabel label, Rect from, Rect to, bool fromLeftEdge = true, bool toRightEdge = true)
    {
        float x0 = (float)(fromLeftEdge ? from.Left + 1 : from.Right - 1), y0 = (float)from.Center.Y;
        float x1 = (float)(toRightEdge ? to.Right - 1 : to.Left + 1), y1 = (float)to.Center.Y;
        label.OnPointerPressed(new PointerEventArgs(x0, y0, PointerButton.Left));
        label.OnPointerMoved(new PointerEventArgs(x1, y1, PointerButton.Left));
        label.OnPointerReleased(new PointerEventArgs(x1, y1, PointerButton.Left));
    }

    [Fact]
    public void Select_all_on_formatted_text_is_the_spans_text()
    {
        var label = Drawn(600, new Span { Text = "Hello " }, new Span { Text = "bold", FontAttributes = FontAttributes.Bold }, new Span { Text = " world" });

        label.SelectAll();

        label.SelectedText.Should().Be("Hello bold world");
    }

    [Fact]
    public void A_drag_across_spans_selects_from_one_span_into_the_next()
    {
        var first = new Span { Text = "alpha " };
        var second = new Span { Text = "beta" };
        var label = Drawn(600, first, second);

        Drag(label, label.GetSpanRects(first).Single(), label.GetSpanRects(second).Single());

        label.SelectedText.Should().Be("alpha beta");
    }

    [Fact]
    public void A_drag_down_a_wrapped_paragraph_selects_across_lines()
    {
        var span = new Span { Text = "one two three four five six seven eight nine ten eleven twelve" };
        var label = Drawn(160, span);
        var rects = label.GetSpanRects(span);
        rects.Count.Should().BeGreaterThan(1, "the paragraph wraps");

        Drag(label, rects[0], rects[1]);

        label.SelectedText.Should().StartWith("one").And.Contain(" ");
        label.SelectedText.Length.Should().BeGreaterThan(rects.Count == 2 ? 10 : 5, "the selection reaches into the second line");
    }

    [Fact]
    public void A_tap_without_a_drag_selects_nothing()
    {
        var span = new Span { Text = "tap here" };
        var label = Drawn(600, span);
        var r = label.GetSpanRects(span).Single();

        label.OnPointerPressed(new PointerEventArgs((float)r.Center.X, (float)r.Center.Y, PointerButton.Left));
        label.OnPointerReleased(new PointerEventArgs((float)r.Center.X, (float)r.Center.Y, PointerButton.Left));

        label.SelectedText.Should().BeEmpty();
    }

    [Fact]
    public void New_text_drops_the_old_selection()
    {
        var label = Drawn(600, new Span { Text = "old text" });
        label.SelectAll();

        label.FormattedText = new FormattedString { Spans = { new Span { Text = "new" } } };

        label.SelectedText.Should().BeEmpty();
    }
}
