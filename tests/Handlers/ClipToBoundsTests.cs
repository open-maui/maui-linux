// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// IsClippedToBounds: SfTabView slides a strip of pages inside a clipped
/// layout; without the clip the next page drew beside the current one.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ClipToBoundsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_clipped_layout_keeps_a_translated_child_inside(bool clipped)
    {
        var page2 = new BoxView { Color = Colors.Red, WidthRequest = 100, HeightRequest = 100, TranslationX = 100 };
        var strip = new HorizontalStackLayout
        {
            WidthRequest = 100, HeightRequest = 100,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
            IsClippedToBounds = clipped,
            BackgroundColor = Colors.Blue,
            Children = { page2 },
        };
        using var host = new HeadlessMauiHost(new ContentPage { BackgroundColor = Colors.White, Content = strip }, withEngine: true);
        host.Context.Render();

        var (_, g, _, _) = host.DisplayWindow.PixelAt(150, 50);
        if (clipped)
            ((int)g).Should().BeGreaterThan(200, "outside the clipped strip is the white page");
        else
            ((int)g).Should().BeLessThan(50, "unclipped, the translated red child draws outside");
    }

    [Fact]
    public void A_slid_page_strip_shows_its_second_page()
    {
        // SfTabView: a clipped grid one page wide holds a clipped strip as
        // wide as all its pages (WidthRequest), slid by TranslationX.
        var first = new BoxView { Color = Colors.Red, WidthRequest = 100, HeightRequest = 100 };
        var second = new BoxView { Color = Colors.Lime, WidthRequest = 100, HeightRequest = 100 };
        var strip = new HorizontalStackLayout
        {
            WidthRequest = 200, HorizontalOptions = LayoutOptions.Start,
            IsClippedToBounds = true, Spacing = 0,
            Children = { first, second },
        };
        var grid = new Grid
        {
            WidthRequest = 100, HeightRequest = 100,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
            IsClippedToBounds = true, Children = { strip },
        };
        using var host = new HeadlessMauiHost(new ContentPage { BackgroundColor = Colors.White, Content = grid }, withEngine: true);
        host.Context.Render();
        strip.TranslationX = -100;
        host.Context.Render();

        var (r, g, _, _) = host.DisplayWindow.PixelAt(50, 50);
        ((int)g).Should().BeGreaterThan(200, "the second (green) page is showing");
        ((int)r).Should().BeLessThan(50);
        var (_, g2, _, _) = host.DisplayWindow.PixelAt(150, 50);
        ((int)g2).Should().BeGreaterThan(200, "nothing draws outside the grid");
    }
}
