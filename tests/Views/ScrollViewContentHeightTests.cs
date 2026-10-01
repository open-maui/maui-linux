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
}
