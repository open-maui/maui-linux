// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Linux.Tests.Views;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// Character extents of the text controls (AT-SPI Text.GetCharacterExtents, what a screen
/// magnifier and review cursor follow): each character's rectangle holds that character's ink
/// in the frame drawn, and a later line is below an earlier one.
/// </summary>
[Collection("LinuxApplication.Current")]
public class AccessibleTextExtentsTests
{
    private static int Ink(HeadlessMauiHost host, AccessibleRect r)
    {
        int dark = 0;
        for (int y = Math.Max(0, r.Y); y < r.Y + r.Height && y < host.DisplayWindow.Height; y++)
        {
            for (int x = Math.Max(0, r.X); x < r.X + r.Width && x < host.DisplayWindow.Width; x++)
            {
                if (host.DisplayWindow.PixelAt(x, y).R < 110)
                    dark++;
            }
        }
        return dark;
    }

    private static HeadlessMauiHost Host(View content)
    {
        var host = new HeadlessMauiHost(new ContentPage { BackgroundColor = Colors.White, Content = content }, withEngine: true);
        host.Context.Render();
        return host;
    }

    private static void EachCharacterHoldsItsInk(HeadlessMauiHost host, IAccessibleText text, string expected)
    {
        text.Text.Should().Be(expected);
        for (int i = 0; i < expected.Length; i++)
        {
            var r = text.GetCharacterBounds(i);
            r.Width.Should().BeGreaterThan(0, $"character {i}");
            if (char.IsWhiteSpace(expected[i]))
                Ink(host, r).Should().BeLessThan(3, $"the space at {i} has no ink");
            else
                Ink(host, r).Should().BeGreaterThan(0, $"'{expected[i]}' at {i} is drawn in its rectangle");
        }
    }

    [Fact]
    public void A_label_gives_each_character_where_it_is_drawn()
    {
        var label = new Label { Text = "Wide HIM", FontSize = 28, TextColor = Colors.Black, Margin = new Thickness(30, 20) };
        using var host = Host(new VerticalStackLayout { label });
        var text = (IAccessibleText)label.Handler!.PlatformView!;

        EachCharacterHoldsItsInk(host, text, "Wide HIM");
        text.GetCharacterBounds(1).X.Should().BeGreaterThan(text.GetCharacterBounds(0).X);
    }

    [Fact]
    public void A_wrapped_label_puts_a_later_line_below_and_back_at_the_left()
    {
        var label = new Label { Text = "alpha beta gamma delta", FontSize = 20, TextColor = Colors.Black, WidthRequest = 120, LineBreakMode = LineBreakMode.WordWrap };
        using var host = Host(new VerticalStackLayout { label });
        var text = (IAccessibleText)label.Handler!.PlatformView!;

        var first = text.GetCharacterBounds(0);
        var last = text.GetCharacterBounds(text.Text.Length - 1);
        last.Y.Should().BeGreaterThan(first.Y + first.Height / 2, "the last word wrapped to a later line");
        int delta = text.Text.IndexOf("delta", StringComparison.Ordinal);
        text.GetCharacterBounds(delta).X.Should().BeLessThan(first.X + 5, "a wrapped line starts at the left");
        Ink(host, text.GetCharacterBounds(delta)).Should().BeGreaterThan(0, "the 'd' of the wrapped word is where it is drawn");
    }

    [Fact]
    public void Formatted_text_gives_each_character_where_its_span_is_drawn()
    {
        var label = new Label { FontSize = 24, TextColor = Colors.Black };
        label.FormattedText = new FormattedString { Spans = { new Span { Text = "Big " , FontSize = 32 }, new Span { Text = "small" } } };
        using var host = Host(new VerticalStackLayout { label });
        var text = (IAccessibleText)label.Handler!.PlatformView!;

        EachCharacterHoldsItsInk(host, text, "Big small");
    }

    [Fact]
    public void An_entry_gives_each_character_where_it_is_drawn()
    {
        var entry = new Entry { Text = "Hello World", FontSize = 22, TextColor = Colors.Black, BackgroundColor = Colors.White };
        using var host = Host(new VerticalStackLayout { Padding = 20, Children = { entry } });
        var text = (IAccessibleText)entry.Handler!.PlatformView!;

        EachCharacterHoldsItsInk(host, text, "Hello World");
    }

    [Fact]
    public void An_editor_puts_its_second_line_below_the_first()
    {
        var editor = new Editor { Text = "one\nTWO", FontSize = 22, TextColor = Colors.Black, BackgroundColor = Colors.White, HeightRequest = 120 };
        using var host = Host(new VerticalStackLayout { Padding = 20, Children = { editor } });
        var text = (IAccessibleText)editor.Handler!.PlatformView!;

        var o = text.GetCharacterBounds(0);
        var t = text.GetCharacterBounds(4);
        t.Y.Should().BeGreaterThan(o.Y + o.Height / 2, "'TWO' is on the second line");
        t.X.Should().BeLessThan(o.X + 3, "both lines start at the left");
        Ink(host, o).Should().BeGreaterThan(0);
        Ink(host, t).Should().BeGreaterThan(0);
        Ink(host, text.GetCharacterBounds(5)).Should().BeGreaterThan(0);
    }
}
