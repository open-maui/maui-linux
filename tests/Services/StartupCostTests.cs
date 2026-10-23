// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// Work every app paid for at startup whether or not it used it
/// (https://github.com/open-maui/maui-linux/issues/30): the hidden GTK host window, whose
/// constructor loads the GTK theme CSS (7 s on the reporter's machine), and a recursive watcher
/// on ~/.config (one inotify watch per subdirectory, 3.6 s for 16,915 of them).
/// </summary>
public class StartupCostTests
{
    [Fact]
    public void Configuring_the_GTK_host_does_not_create_its_window()
    {
        var host = new GtkHostService();

        host.Configure("App", 1024, 768);

        host.IsInitialized.Should().BeFalse("the window is made on first use: a WebView shown, or GTK mode");
        host.HostWindow.Should().BeNull();
    }

    [Fact]
    public void The_theme_watcher_watches_only_the_theme_files_without_recursion()
    {
        var watchers = SystemThemeService.Instance.SettingsWatchers;

        watchers.Count.Should().BeLessThanOrEqualTo(2);
        foreach (var watcher in watchers)
        {
            watcher.IncludeSubdirectories.Should().BeFalse(watcher.Path);
            watcher.Filter.Should().BeOneOf("kdeglobals", "settings.ini");
        }
    }
}
