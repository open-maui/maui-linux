// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// The Shell's tab bar as MAUI's Windows implementation shows it: the current ShellItem's sections
/// as tabs in a top navigation pane under the navigation bar (WinUI's NavigationView in Top pane
/// mode), shown when the item shows tabs (Windows' ShellItem.ShowTabs), hidden by
/// Shell.TabBarIsVisible (LeftMinimal, no tab row), the content laid out under it, a click on a tab
/// selecting its section through MAUI, and the pane staying while a page is pushed (Windows).
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
    public void A_ShellItem_with_two_sections_shows_them_as_top_tabs_and_a_click_selects_one()
    {
        var (shell, first, second) = TwoTabs();
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;

        platform.TabBarSections.Select(s => s.Title).Should().Equal("First", "Second");
        var bar = platform.TabBarBounds;
        bar.Height.Should().Be(platform.TabBarHeight);
        bar.Top.Should().Be(platform.NavBarHeight, "the pane is under the navigation bar, at the top of the content area");
        platform.CurrentContent!.Bounds.Top.Should().BeGreaterThanOrEqualTo(bar.Bottom, "the page is laid out under the pane");
        platform.CurrentContent!.Bounds.Bottom.Should().Be(600, "nothing is at the bottom");

        // A section MAUI made around a bare ShellContent is listed by that content (Windows' MapMenuItems).
        platform.TopNavigation.Items.Select(i => i.Data).Should().Equal(shell.Items[0].Items[0].Items[0], shell.Items[0].Items[1].Items[0]);
        platform.TopNavigation.Items[0].IsSelected.Should().BeTrue();

        // The tabs are laid out left to right from the pane's left edge.
        var firstTab = platform.TopNavigation.ItemBounds(shell.Items[0].Items[0].Items[0]);
        var secondTab = platform.TopNavigation.ItemBounds(shell.Items[0].Items[1].Items[0]);
        firstTab.Left.Should().Be((float)bar.Left);
        secondTab.Left.Should().Be(firstTab.Right);
        platform.TabBarItemBounds(shell.Items[0].Items[1]).Title.IsEmpty.Should().BeFalse();

        host.DisplayWindow.RaisePointerPressed(secondTab.MidX, secondTab.MidY);
        host.DisplayWindow.RaisePointerReleased(secondTab.MidX, secondTab.MidY);
        host.Context.Render();

        shell.CurrentItem.CurrentItem.Title.Should().Be("Second", "the click selects the section through MAUI");
        platform.CurrentMauiPage.Should().BeSameAs(second);
        platform.TopNavigation.Items[1].IsSelected.Should().BeTrue();
    }

    [Fact]
    public void The_pane_is_hidden_for_a_single_section_and_by_TabBarIsVisible()
    {
        var single = new ContentPage { Content = new Label { Text = "a" } };
        var lone = new Shell { Items = { new ShellContent { Content = single } } };
        using (var host = new HeadlessMauiHost(lone, withEngine: true))
        {
            host.Context.Render();
            var platform = (SkiaShell)lone.Handler!.PlatformView!;
            platform.TabBarSections.Should().BeEmpty("one section with one content shows no tabs");
            platform.TabBarBounds.IsEmpty.Should().BeTrue();
            platform.CurrentContent!.Bounds.Top.Should().Be(platform.NavBarHeight);
        }

        var (shell, first, _) = TwoTabs();
        Shell.SetTabBarIsVisible(first, false);
        using (var host = new HeadlessMauiHost(shell, withEngine: true))
        {
            host.Context.Render();
            var platform = (SkiaShell)shell.Handler!.PlatformView!;
            platform.TabBarBounds.IsEmpty.Should().BeTrue("the presented page hides the tab bar (LeftMinimal)");
            platform.CurrentContent!.Bounds.Top.Should().Be(platform.NavBarHeight, "the page takes the pane's place");

            Shell.SetTabBarIsVisible(first, true);
            host.Context.Render();
            platform.TabBarBounds.IsEmpty.Should().BeFalse("the page shows it again");
            platform.CurrentContent!.Bounds.Top.Should().Be(platform.NavBarHeight + platform.TabBarHeight);
        }
    }

    [Fact]
    public async Task The_pane_stays_for_a_pushed_page_unless_the_page_hides_it()
    {
        var (shell, _, _) = TwoTabs();
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;

        await shell.Navigation.PushAsync(new ContentPage { Title = "Pushed", Content = new Label { Text = "pushed" } });
        host.Context.Render();
        platform.TabBarBounds.IsEmpty.Should().BeFalse("on Windows the pane stays over the section's frame");
        platform.TabBarSections.Should().HaveCount(2);

        var hiding = new ContentPage { Title = "Hiding", Content = new Label { Text = "hiding" } };
        Shell.SetTabBarIsVisible(hiding, false);
        await shell.Navigation.PushAsync(hiding);
        host.Context.Render();
        platform.TabBarBounds.IsEmpty.Should().BeTrue("the pushed page hides it");

        await shell.Navigation.PopAsync();
        host.Context.Render();
        platform.TabBarBounds.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Sections_added_and_hidden_later_are_followed()
    {
        var (shell, _, _) = TwoTabs();
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;

        var third = new ShellContent { Route = "third", Title = "Third", Content = new ContentPage { Content = new Label { Text = "c" } } };
        shell.Items[0].Items.Add(third);
        host.Context.Render();
        platform.TopNavigation.Items.Select(i => i.Title).Should().Equal("First", "Second", "Third");

        third.IsVisible = false;
        host.Context.Render();
        platform.TopNavigation.Items.Select(i => i.Title).Should().Equal("First", "Second");
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
