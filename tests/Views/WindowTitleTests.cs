// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// The native window's title follows the MAUI window's (what the taskbar and the window switcher
/// show): the Title an app gives the window when it creates it, its page's title when the window
/// has none, and later changes.
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
    public void An_untitled_window_takes_its_page_title()
    {
        using var host = new HeadlessMauiHost(new ContentPage { Title = "Inbox" });

        host.DisplayWindow.Title.Should().Be("Inbox");
    }

    [Fact]
    public void A_later_title_change_reaches_the_native_window_and_a_screen_reader()
    {
        using var host = new HeadlessMauiHost(new ContentPage { Title = "Inbox" }, windowTitle: "Mail");
        host.DisplayWindow.Title.Should().Be("Mail", "the window's own title wins over its page's");

        host.Window.Title = "Mail - 3 unread";

        host.DisplayWindow.Title.Should().Be("Mail - 3 unread");
        IAccessible frame = new Microsoft.Maui.Platform.Linux.Services.AtSpi.WindowAccessible(host.Context);
        frame.AccessibleName.Should().Be("Mail - 3 unread", "the screen reader's frame is named by the same title");
    }
}
