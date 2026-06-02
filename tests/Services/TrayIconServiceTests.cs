// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Platform.Tests;

public class TrayIconServiceTests
{
    // Show()/Hide() actually pop a real StatusNotifierItem on the user's
    // desktop when libappindicator is present — we cannot exercise that from
    // an automated test without polluting the host. These tests only cover the
    // object-shape / mutation surface that doesn't touch the backend.

    [Fact]
    public void Constructor_AssignsIdAndInitializesEmpty()
    {
        using var icon = new TrayIcon("openmaui.tests.tray");
        icon.Id.Should().Be("openmaui.tests.tray");
        icon.MenuItems.Should().BeEmpty();
        icon.Title.Should().BeEmpty();
        icon.Tooltip.Should().BeEmpty();
        icon.IconPath.Should().BeEmpty();
    }

    [Fact]
    public void MenuItems_AreMutable()
    {
        using var icon = new TrayIcon("openmaui.tests.tray.menu");
        icon.MenuItems.Add(new TrayMenuItem { Text = "Open", Action = () => { } });
        icon.MenuItems.Add(new TrayMenuItem { IsSeparator = true });
        icon.MenuItems.Add(new TrayMenuItem { Text = "Quit", Action = () => { } });

        icon.MenuItems.Should().HaveCount(3);
        icon.MenuItems[0].Text.Should().Be("Open");
        icon.MenuItems[1].IsSeparator.Should().BeTrue();
        icon.MenuItems[2].Text.Should().Be("Quit");
    }

    [Fact]
    public void UpdateMenu_IsSafe_WhileIconHasNotBeenShown()
    {
        // No backend call until Show(); UpdateMenu/Update must be no-ops.
        using var icon = new TrayIcon("openmaui.tests.tray.update");
        icon.UpdateMenu();
        icon.Update();
    }

    [Fact]
    public void Dispose_IsIdempotent_AndDoesNotShowAnIcon()
    {
        var icon = new TrayIcon("openmaui.tests.tray.dispose");
        icon.Dispose();
        icon.Dispose();
        // Subsequent Show() after Dispose() must be a no-op.
        icon.Show();
    }
}
