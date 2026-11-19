// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// The native window's title follows the MAUI window's (what the taskbar and the window switcher
/// show) as on MAUI's other platforms: IWindow.Title (the window's Title, else the title an app set
/// on its Shell), set when the window is created and when either changes; an untitled window shows
/// the app's title.
/// </summary>
[Collection("LinuxApplication.Current")]
public class WindowTitleTests
{
    [Fact]
    public void A_title_set_when_the_window_is_created_reaches_the_native_window()
    {
        using var host = new HeadlessMauiHost(new ContentPage(), windowTitle: "A11y Check");

        host.DisplayWindow.Title.Should().Be("A11y Check", "CreateWindow's Window.Title was left at the native default");
    }

    [Fact]
    public void An_untitled_window_shows_the_app_title_not_its_page_title()
    {
        // MAUI's IWindow.Title: the window's Title, else a Shell's; a ContentPage's or
        // NavigationPage's title never becomes the window title on MAUI's platforms.
        using var host = new HeadlessMauiHost(new NavigationPage(new ContentPage { Title = "Inbox" }) { Title = "Root" });

        host.DisplayWindow.Title.Should().Be(Microsoft.Maui.Platform.Linux.LinuxApplication.DefaultWindowTitle);
    }

    [Fact]
    public void A_Shell_title_set_by_the_app_is_the_window_title_and_follows_changes()
    {
        var shell = new Shell { Title = "My Shell", Items = { new ShellContent { Content = new ContentPage { Title = "Home" } } } };
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();

        host.DisplayWindow.Title.Should().Be("My Shell");

        shell.Title = "My Shell - Inbox";
        host.DisplayWindow.Title.Should().Be("My Shell - Inbox", "MAUI raises the window handler's Title for a Shell's title");

        host.Window.Title = "Window title";
        host.DisplayWindow.Title.Should().Be("Window title", "the window's own Title wins over the Shell's");
    }

    [Fact]
    public void A_later_title_change_reaches_the_native_window_and_a_screen_reader()
    {
        using var host = new HeadlessMauiHost(new ContentPage { Title = "Inbox" }, windowTitle: "Mail");
        host.DisplayWindow.Title.Should().Be("Mail");

        host.Window.Title = "Mail - 3 unread";

        host.DisplayWindow.Title.Should().Be("Mail - 3 unread");
        IAccessible frame = new Microsoft.Maui.Platform.Linux.Services.AtSpi.WindowAccessible(host.Context);
        frame.AccessibleName.Should().Be("Mail - 3 unread", "the screen reader's frame is named by the same title");
    }
}
