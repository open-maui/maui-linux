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
