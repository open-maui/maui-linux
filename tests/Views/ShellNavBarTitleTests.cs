// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// The Shell navigation bar shows the presented page's Title when the page sets one, as MAUI's
/// ShellToolbar does, not the flyout item's (CiteLynq's "Daily" item shows "Daily Outlook").
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellNavBarTitleTests
{
    [Fact]
    public async Task The_bar_shows_the_page_title_and_follows_it()
    {
        var titled = new ContentPage { Title = "Daily Outlook", Content = new Label { Text = "a" } };
        var untitled = new ContentPage { Content = new Label { Text = "b" } };
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Locked };
        shell.Items.Add(new FlyoutItem { Title = "Daily", Items = { new ShellContent { Route = "home", Content = titled } } });
        shell.Items.Add(new FlyoutItem { Title = "Library", Items = { new ShellContent { Route = "library", Content = untitled } } });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (Microsoft.Maui.Platform.SkiaShell)shell.Handler!.PlatformView!;

        platform.Title.Should().Be("Daily Outlook", "the page sets its title");

        titled.Title = "Tuesday";
        platform.Title.Should().Be("Tuesday", "the bar follows the page's title");

        await Shell.Current.GoToAsync("//library");
        host.Context.Render();
        platform.Title.Should().Be("Library", "a page with no title shows its item's at the root");

        await Shell.Current.GoToAsync("//home");
        host.Context.Render();
        platform.Title.Should().Be("Tuesday");
    }
}

/// <summary>
/// The presented page's ToolbarItems are in the Shell navigation bar, as on the other platforms
/// (CiteLynq's Notifications and Search); a click raises Clicked and runs the Command.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellToolbarItemTests
{
    [Fact]
    public void The_pages_toolbar_items_are_in_the_bar_and_a_click_activates_one()
    {
        var clicked = new List<string>();
        var search = new ToolbarItem { Text = "Search" };
        search.Clicked += (s, e) => clicked.Add("Search");
        var off = new ToolbarItem { Text = "Off", IsEnabled = false };
        off.Clicked += (s, e) => clicked.Add("Off");
        var page = new ContentPage { Title = "Daily Outlook", Content = new Label { Text = "a" } };
        page.ToolbarItems.Add(search);
        page.ToolbarItems.Add(off);
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Disabled };
        shell.Items.Add(new ShellContent { Route = "home", Content = page });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (Microsoft.Maui.Platform.SkiaShell)shell.Handler!.PlatformView!;

        platform.PresentedToolbarItems.Should().Equal(search, off);
        var areas = platform.ToolbarHitAreas;
        areas.Select(a => a.Item).Should().Equal(new[] { off, search }, "drawn from the right end, the first item leftmost");
        var searchArea = areas.Single(a => a.Item == search).Bounds;
        searchArea.Left.Should().BeLessThan(areas.Single(a => a.Item == off).Bounds.Left);

        void Click(SkiaSharp.SKRect r)
        {
            host.DisplayWindow.RaisePointerPressed(r.MidX, r.MidY);
            host.DisplayWindow.RaisePointerReleased(r.MidX, r.MidY);
            host.Context.Render();
        }
        Click(searchArea);
        Click(areas.Single(a => a.Item == off).Bounds);
        clicked.Should().Equal("Search");

        var bell = new ToolbarItem { Text = "Notifications" };
        page.ToolbarItems.Insert(0, bell);
        host.Context.Render();
        platform.PresentedToolbarItems.Should().Equal(new[] { bell, search, off }, "the bar follows the page's items");
    }
}
