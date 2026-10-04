// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// The Shell's bottom tab bar shows the current ShellItem's sections, as on MAUI's platforms: shown
/// when the item has more than one, hidden by Shell.TabBarIsVisible, the content laid out above
/// it, and a click on a tab selects its section through MAUI.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellTabBarTests
{
    private static (Shell Shell, ContentPage First, ContentPage Second) TwoTabs()
    {
        var first = new ContentPage { Title = "First", Content = new Label { Text = "a" } };
        var second = new ContentPage { Title = "Second", Content = new Label { Text = "b" } };
        var shell = new Shell();
        shell.Items.Add(new TabBar
        {
            Items =
            {
                new ShellContent { Route = "first", Title = "First", Content = first },
                new ShellContent { Route = "second", Title = "Second", Content = second },
            },
        });
        return (shell, first, second);
    }

    [Fact]
    public void A_ShellItem_with_two_sections_shows_them_as_tabs_and_a_click_selects_one()
    {
        var (shell, first, second) = TwoTabs();
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;

        platform.TabBarSections.Select(s => s.Title).Should().Equal("First", "Second");
        var bar = platform.TabBarBounds;
        bar.Height.Should().Be(platform.TabBarHeight);
        bar.Bottom.Should().Be(600);
        platform.CurrentContent!.Bounds.Bottom.Should().BeLessThanOrEqualTo(bar.Top, "the page is laid out above the bar");

        // The second tab is the right half of the bar.
        float x = (float)(bar.Left + bar.Width * 0.75), y = (float)bar.Center.Y;
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        host.Context.Render();

        shell.CurrentItem.CurrentItem.Title.Should().Be("Second", "the click selects the section through MAUI");
        platform.CurrentMauiPage.Should().BeSameAs(second);
    }

    [Fact]
    public void The_bar_is_hidden_for_a_single_section_and_by_TabBarIsVisible()
    {
        var single = new ContentPage { Content = new Label { Text = "a" } };
        var lone = new Shell { Items = { new ShellContent { Content = single } } };
        using (var host = new HeadlessMauiHost(lone, withEngine: true))
        {
            host.Context.Render();
            var platform = (SkiaShell)lone.Handler!.PlatformView!;
            platform.TabBarSections.Should().BeEmpty("one section shows no tabs");
            platform.TabBarBounds.IsEmpty.Should().BeTrue();
        }

        var (shell, first, _) = TwoTabs();
        Shell.SetTabBarIsVisible(first, false);
        using (var host = new HeadlessMauiHost(shell, withEngine: true))
        {
            host.Context.Render();
            var platform = (SkiaShell)shell.Handler!.PlatformView!;
            platform.TabBarBounds.IsEmpty.Should().BeTrue("the presented page hides the tab bar");
            platform.CurrentContent!.Bounds.Bottom.Should().Be(600, "the page takes the bar's place");

            Shell.SetTabBarIsVisible(first, true);
            host.Context.Render();
            platform.TabBarBounds.IsEmpty.Should().BeFalse("the page shows it again");
        }
    }
}

/// <summary>
/// Shell.TitleView takes the title's place in the navigation bar, as MAUI's ShellToolbar resolves
/// it: the presented page's, else the Shell's, followed as pages and the property change.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellTitleViewTests
{
    [Fact]
    public async Task The_bar_shows_the_presented_pages_TitleView_else_the_shells()
    {
        var pageTitle = new Label { Text = "page" };
        var shellTitle = new Label { Text = "shell" };
        var first = new ContentPage { Content = new Label { Text = "a" } };
        var second = new ContentPage { Content = new Label { Text = "b" } };
        Shell.SetTitleView(first, pageTitle);
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Disabled };
        Shell.SetTitleView(shell, shellTitle);
        shell.Items.Add(new FlyoutItem { Items = { new ShellContent { Route = "first", Content = first } } });
        shell.Items.Add(new FlyoutItem { Items = { new ShellContent { Route = "second", Content = second } } });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;

        platform.TitleView.Should().NotBeNull().And.BeSameAs(pageTitle.Handler!.PlatformView);
        var slot = platform.TitleViewBounds;
        slot.Height.Should().Be(platform.NavBarHeight, "the TitleView has the bar's height");
        platform.TitleView!.Bounds.Should().Be(slot);

        await shell.GoToAsync("//second");
        host.Context.Render();
        platform.TitleView.Should().BeSameAs(shellTitle.Handler!.PlatformView, "a page without one shows the Shell's");

        var replacement = new Label { Text = "new" };
        Shell.SetTitleView(second, replacement);
        host.Context.Render();
        platform.TitleView.Should().BeSameAs(replacement.Handler!.PlatformView, "the bar follows the property");
    }
}

/// <summary>
/// The flyout panel's bounds are known (SkiaShell.FlyoutBounds), and a CollapseOnScroll header is
/// at least an app bar tall, as MAUI keeps it on every platform.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellFlyoutHeaderBehaviorTests
{
    [Theory]
    [InlineData(FlyoutHeaderBehavior.Default, false)]
    [InlineData(FlyoutHeaderBehavior.CollapseOnScroll, true)]
    public void A_CollapseOnScroll_header_is_at_least_56_tall(FlyoutHeaderBehavior behavior, bool collapses)
    {
        var header = new VerticalStackLayout { new Label { Text = "Header" } };
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Locked, FlyoutHeader = header, FlyoutHeaderBehavior = behavior };
        shell.Items.Add(new FlyoutItem { Title = "Home", Items = { new ShellContent { Content = new ContentPage() } } });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;

        platform.FlyoutBounds.Should().Be(new Microsoft.Maui.Graphics.Rect(0, 0, platform.FlyoutWidth, 600), "a locked flyout is at the shell's left edge");
        var headerBounds = ((SkiaView)header.Handler!.PlatformView!).Bounds;
        if (collapses)
            headerBounds.Height.Should().Be(56);
        else
            headerBounds.Height.Should().BeLessThan(56, "the header is as tall as its content");
    }
}
