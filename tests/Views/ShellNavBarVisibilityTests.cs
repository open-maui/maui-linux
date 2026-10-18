// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// Shell.NavBarIsVisible as MAUI resolves it on every platform (Shell.GetEffectiveValue): the
/// presented page's value, else its ShellContent's, section's or item's, else the Shell's. A page
/// that hides the bar (a splash or sign-in page set in its constructor) gets the whole height; a
/// change while the page is shown takes effect at once.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellNavBarVisibilityTests
{
    private static (Shell Shell, ContentPage Splash, ContentPage Home) BareContents(Action<ContentPage>? configureSplash = null)
    {
        var splash = new ContentPage { Title = "Splash", Content = new Label { Text = "splash" } };
        configureSplash?.Invoke(splash);
        var home = new ContentPage { Title = "Home", Content = new Label { Text = "home" } };
        var shell = new Shell();
        shell.Items.Add(new ShellContent { Route = "splash", Content = splash });
        shell.Items.Add(new ShellContent { Route = "home", Content = home });
        return (shell, splash, home);
    }

    [Fact]
    public void A_page_that_hides_the_nav_bar_gets_the_whole_height()
    {
        var (shell, _, _) = BareContents(p => Shell.SetNavBarIsVisible(p, false));
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;

        platform.IsNavBarShown.Should().BeFalse("the presented page sets Shell.NavBarIsVisible=False");
        platform.CurrentContent!.Bounds.Top.Should().Be(0, "no bar above the page");
    }

    [Fact]
    public void Another_page_shows_the_bar_again_after_navigation()
    {
        var (shell, _, home) = BareContents(p => Shell.SetNavBarIsVisible(p, false));
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;

        shell.GoToAsync("//home").Wait(TimeSpan.FromSeconds(5));
        host.Context.Render();

        platform.CurrentMauiPage.Should().BeSameAs(home);
        platform.IsNavBarShown.Should().BeTrue("the home page does not hide it");
        platform.CurrentContent!.Bounds.Top.Should().Be(platform.NavBarHeight);
    }

    [Fact]
    public void A_change_while_the_page_is_shown_takes_effect()
    {
        var (shell, splash, _) = BareContents();
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;
        platform.IsNavBarShown.Should().BeTrue();

        Shell.SetNavBarIsVisible(splash, false);
        host.Context.Render();

        platform.IsNavBarShown.Should().BeFalse();
        platform.CurrentContent!.Bounds.Top.Should().Be(0);
    }

    [Fact]
    public void The_value_is_inherited_from_the_ShellContent_or_the_Shell()
    {
        var (shell, _, _) = BareContents();
        Shell.SetNavBarIsVisible(shell, false);
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;
        platform.IsNavBarShown.Should().BeFalse("the Shell hides it and the page does not say otherwise");

        var (shell2, _, _) = BareContents();
        Shell.SetNavBarIsVisible(shell2.Items[0].Items[0].Items[0], false);
        using var host2 = new HeadlessMauiHost(shell2, withEngine: true);
        host2.Context.Render();
        ((SkiaShell)shell2.Handler!.PlatformView!).IsNavBarShown.Should().BeFalse("the page's ShellContent hides it");
    }
}
