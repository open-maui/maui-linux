// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// Live changes on a MAUI Window reach its native window: page replacement
/// (the post-login MainPage swap), title, size, position and size limits.
/// </summary>
[Collection("LinuxApplication.Current")]
public class WindowSyncTests
{
    [Fact]
    public void Replacing_the_page_renders_the_new_page()
    {
        var first = new ContentPage { Content = new Label { Text = "login" } };
        using var host = new HeadlessMauiHost(first, withEngine: true);
        host.Context.Render();
        var firstRoot = host.Context.RootView;

        var secondLabel = new Label { Text = "home" };
        var second = new ContentPage { Content = secondLabel };
        host.Window.Page = second;
        host.Context.Render();

        host.Context.RootView.Should().NotBeSameAs(firstRoot);
        host.Context.RootView.Should().BeSameAs(second.Handler!.PlatformView);
        secondLabel.Handler.Should().NotBeNull("the new page's content was realised");
    }

    [Fact]
    public void Replacing_the_page_routes_input_to_the_new_page()
    {
        using var host = new HeadlessMauiHost(new ContentPage { Content = new Label { Text = "old" } }, withEngine: true);
        int clicks = 0;
        var button = new Button { Text = "go", WidthRequest = 120, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        button.Clicked += (_, _) => clicks++;

        host.Window.Page = new ContentPage { Content = button };
        host.Context.Render();
        host.DisplayWindow.RaisePointerPressed(20, 20);
        host.DisplayWindow.RaisePointerReleased(20, 20);

        clicks.Should().Be(1);
    }

    [Fact]
    public void Setting_the_same_page_again_does_not_re_render()
    {
        var page = new ContentPage { Content = new Label { Text = "same" } };
        using var host = new HeadlessMauiHost(page, withEngine: true);
        var root = host.Context.RootView;

        host.Window.Page = page;

        host.Context.RootView.Should().BeSameAs(root);
    }

    [Fact]
    public void Title_changes_reach_the_native_window()
    {
        using var host = new HeadlessMauiHost(new ContentPage(), withEngine: true);

        host.Window.Title = "Inventory";

        host.DisplayWindow.Title.Should().Be("Inventory");
    }

    [Fact]
    public void A_replaced_page_title_is_used_when_the_window_has_none()
    {
        using var host = new HeadlessMauiHost(new ContentPage(), withEngine: true);

        host.Window.Page = new ContentPage { Title = "Dashboard" };

        host.DisplayWindow.Title.Should().Be("Dashboard");
    }

    [Fact]
    public void Width_and_height_request_a_logical_size()
    {
        using var host = new HeadlessMauiHost(new ContentPage(), withEngine: true);

        host.Window.Width = 1024;
        host.Window.Height = 700;

        host.DisplayWindow.RequestedLogicalSize.Should().Be((1024, 700));
        host.DisplayWindow.Width.Should().Be(1024);
        host.DisplayWindow.Height.Should().Be(700);
    }

    [Fact]
    public void A_size_equal_to_the_native_size_is_not_re_requested()
    {
        using var host = new HeadlessMauiHost(new ContentPage(), withEngine: true);

        host.Window.Width = 800;
        host.Window.Height = 600;

        host.DisplayWindow.RequestedLogicalSize.Should().BeNull("the native window is already 800x600");
    }

    [Fact]
    public void Position_and_limits_are_forwarded_in_logical_pixels()
    {
        using var host = new HeadlessMauiHost(new ContentPage(), withEngine: true);

        host.Window.X = 40;
        host.Window.Y = 60;
        host.Window.MinimumWidth = 320;
        host.Window.MinimumHeight = 240;
        host.Window.MaximumWidth = 1600;

        host.DisplayWindow.RequestedLogicalPosition.Should().Be((40, 60));
        host.DisplayWindow.SizeLimits.Should().Be((320, 240, 1600, 0));
    }

    [Fact]
    public void Minimizing_raises_Stopped_and_restoring_raises_Resumed()
    {
        using var host = new HeadlessMauiHost(new ContentPage(), withEngine: true);
        var events = new List<string>();
        host.Window.Stopped += (_, _) => events.Add("stopped");
        host.Window.Resumed += (_, _) => events.Add("resumed");

        host.DisplayWindow.RaiseSuspended(true);
        host.DisplayWindow.RaiseSuspended(true);
        host.DisplayWindow.RaiseSuspended(false);

        events.Should().Equal("stopped", "resumed");
    }

    [Fact]
    public void Resumed_is_not_raised_without_a_prior_Stopped()
    {
        using var host = new HeadlessMauiHost(new ContentPage(), withEngine: true);
        int resumed = 0;
        host.Window.Resumed += (_, _) => resumed++;

        host.DisplayWindow.RaiseSuspended(false);

        resumed.Should().Be(0);
    }
}
