// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// A view set directly as ContentPage.Content honours its layout options
/// (it used to be stretched over the whole page), and a press beside it does
/// not reach it.
/// </summary>
[Collection("LinuxApplication.Current")]
public class PageContentAlignmentTests
{
    private static (HeadlessMauiHost Host, SkiaView Platform) Host(View content)
    {
        var host = new HeadlessMauiHost(new ContentPage { Content = content }, withEngine: true);
        host.Context.Render();
        return (host, (SkiaView)content.Handler!.PlatformView!);
    }

    [Fact]
    public void Default_options_fill_the_page()
    {
        var (host, platform) = Host(new BoxView { Color = Colors.Red, WidthRequest = 100, HeightRequest = 40 });
        using (host)
        {
            platform.Bounds.Width.Should().Be(800);
            platform.Bounds.Height.Should().Be(600);
        }
    }

    [Theory]
    [InlineData("Start", 0)]
    [InlineData("Center", 350)]
    [InlineData("End", 700)]
    public void Horizontal_options_place_the_content(string option, double expectedX)
    {
        var options = option switch { "Start" => LayoutOptions.Start, "Center" => LayoutOptions.Center, _ => LayoutOptions.End };
        var (host, platform) = Host(new BoxView { Color = Colors.Red, WidthRequest = 100, HeightRequest = 40, HorizontalOptions = options });
        using (host)
        {
            platform.Bounds.X.Should().Be(expectedX);
            platform.Bounds.Width.Should().Be(100);
            platform.Bounds.Height.Should().Be(600, "VerticalOptions is still Fill");
        }
    }

    [Fact]
    public void Centered_content_is_centered_both_ways()
    {
        var (host, platform) = Host(new BoxView
        {
            Color = Colors.Red, WidthRequest = 100, HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
        });
        using (host)
        {
            platform.Bounds.Should().Be(new Microsoft.Maui.Graphics.Rect(350, 280, 100, 40));
        }
    }

    [Fact]
    public void A_press_beside_start_aligned_content_does_not_reach_it()
    {
        int clicks = 0;
        var button = new Button { Text = "b", WidthRequest = 100, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        button.Clicked += (_, _) => clicks++;
        var (host, _) = Host(button);
        using (host)
        {
            host.DisplayWindow.RaisePointerPressed(400, 300);
            host.DisplayWindow.RaisePointerReleased(400, 300);
            clicks.Should().Be(0);

            host.DisplayWindow.RaisePointerPressed(50, 20);
            host.DisplayWindow.RaisePointerReleased(50, 20);
            clicks.Should().Be(1);
        }
    }
}
