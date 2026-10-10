// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// A vertical ScrollView is as tall as its content, up to the room it is given, as in MAUI:
/// a centred card around a scrolling form wrapped the form on
/// the other platforms and took the whole window here.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ScrollViewContentHeightTests
{
    private static ScrollView Form(double height) => new()
    {
        Content = new VerticalStackLayout { Padding = 20, Children = { new BoxView { HeightRequest = height } } },
    };

    [Fact]
    public void A_centred_card_around_a_short_scrolling_form_wraps_it()
    {
        var card = new Border { MaximumHeightRequest = 680, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center, Content = new ContentView { Content = Form(200) } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = card }, withEngine: true);
        host.Context.Render();

        card.Frame.Height.Should().BeApproximately(242, 2, "the form's 240 and the border's stroke");
        card.Frame.Y.Should().BeApproximately((600 - card.Frame.Height) / 2, 1, "centred");
    }

    [Fact]
    public void A_tall_form_scrolls_within_the_room_and_a_filling_scroller_still_fills()
    {
        var tall = Form(2000);
        tall.VerticalOptions = LayoutOptions.Center;
        using (var host = new HeadlessMauiHost(new ContentPage { Content = tall }, withEngine: true))
        {
            host.Context.Render();
            tall.Frame.Height.Should().Be(600, "content taller than the page scrolls in the page's height");
        }

        var filling = Form(100);
        using (var host = new HeadlessMauiHost(new ContentPage { Content = filling }, withEngine: true))
        {
            host.Context.Render();
            filling.Frame.Height.Should().Be(600, "Fill stretches it to the page, as before");
        }
    }

    [Fact]
    public void In_a_vertical_stack_a_scroller_is_as_tall_as_its_content_as_in_MAUI()
    {
        // The stack measures it with unbounded height: MAUI's ScrollView then takes its content's
        // height (it was given a made-up 400 viewport here).
        var scroll = Form(1000);
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { Children = { scroll } } }, withEngine: true);
        host.Context.Render();

        scroll.Frame.Height.Should().BeApproximately(1040, 1);
    }

    [Fact]
    public void A_centred_scroller_is_as_wide_as_its_content()
    {
        var scroll = new ScrollView
        {
            HorizontalOptions = LayoutOptions.Center,
            Content = new BoxView { WidthRequest = 140, HeightRequest = 60 },
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = scroll }, withEngine: true);
        host.Context.Render();

        scroll.Frame.Width.Should().BeLessThan(160, "it wraps its 140 wide content (and the scrollbar it keeps room for)");
        scroll.Frame.X.Should().BeApproximately((800 - scroll.Frame.Width) / 2, 1, "centred");
    }
}
