// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Rendering;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// A Button whose text is wider than the button: a truncating LineBreakMode cuts the text to
/// fit and marks the cut with an ellipsis (a row toggle keeps the start of its sentence);
/// the other modes leave the text alone. A truncating button asks for no more width than it
/// is offered.
/// </summary>
public class SkiaButtonTruncationTests
{
    private const string Text = "START thinking about a long line of reasoning that cannot fit END";
    private const float Room = 200f;

    private static string Shown(LineBreakMode mode, string text = Text, float room = Room)
    {
        var button = new SkiaButton { Text = text, FontSize = 13, LineBreakMode = mode };
        using var font = SkiaFontFactory.Create(13f);
        var shown = button.TruncateForLineBreakMode(text, font, room);
        if (shown != text)
            button.MeasureTextWidth(shown, font).Should().BeLessThanOrEqualTo(room, "a cut text fits the room it was cut for");
        return shown;
    }

    [Fact]
    public void Tail_truncation_keeps_the_start_and_ends_with_an_ellipsis()
    {
        var shown = Shown(LineBreakMode.TailTruncation);
        shown.Should().StartWith("START").And.EndWith("…");
        shown.Should().NotContain("END");
    }

    [Fact]
    public void Head_truncation_keeps_the_end_and_starts_with_an_ellipsis()
    {
        var shown = Shown(LineBreakMode.HeadTruncation);
        shown.Should().StartWith("…").And.EndWith("END");
        shown.Should().NotContain("START");
    }

    [Fact]
    public void Middle_truncation_keeps_both_ends_around_an_ellipsis()
    {
        var shown = Shown(LineBreakMode.MiddleTruncation);
        shown.Should().StartWith("START").And.EndWith("END").And.Contain("…");
    }

    [Fact]
    public void A_text_that_fits_is_left_whole()
    {
        Shown(LineBreakMode.TailTruncation, "Short", 200f).Should().Be("Short");
    }

    [Fact]
    public void A_mode_that_does_not_truncate_leaves_the_text_alone()
    {
        Shown(LineBreakMode.NoWrap).Should().Be(Text);
        Shown(LineBreakMode.WordWrap).Should().Be(Text);
    }

    [Fact]
    public void A_cut_never_splits_a_surrogate_pair()
    {
        var emoji = string.Concat(Enumerable.Repeat("😀", 40));
        var shown = Shown(LineBreakMode.TailTruncation, emoji, 120f);
        shown.Should().EndWith("…");
        char.IsHighSurrogate(shown[^2]).Should().BeFalse("the character before the ellipsis is a whole one");
    }

    [Fact]
    public void A_truncating_button_asks_for_no_more_width_than_it_is_offered()
    {
        var truncating = new SkiaButton { Text = Text, FontSize = 13, LineBreakMode = LineBreakMode.TailTruncation };
        truncating.Measure(new Size(Room, double.PositiveInfinity)).Width.Should().BeLessThanOrEqualTo(Room + 0.5);

        var plain = new SkiaButton { Text = Text, FontSize = 13, LineBreakMode = LineBreakMode.NoWrap };
        plain.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)).Width.Should().BeGreaterThan(Room);
    }
}
