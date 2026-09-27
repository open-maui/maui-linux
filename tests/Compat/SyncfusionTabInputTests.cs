// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Syncfusion.Maui.TabView;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>Controls on a tab other than the first take input once their tab is selected.</summary>
[Collection("LinuxApplication.Current")]
public sealed class SyncfusionTabInputTests
{
    [Fact]
    public void A_switch_on_the_second_tab_flips()
    {
        var toggle = new Switch();
        var tabs = new SfTabView
        {
            Items =
            {
                new SfTabItem { Header = "General", Content = new Label { Text = "general" } },
                new SfTabItem { Header = "Notes", Content = new VerticalStackLayout { Children = { toggle } } },
            },
        };
        using var host = new CompatHost(new ContentPage { Content = tabs }, b => b.UseLinuxSyncfusion(), 500, 400);
        host.Render();
        tabs.SelectedIndex = 1;
        for (int i = 0; i < 15; i++)
        {
            Thread.Sleep(40);
            Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll();
            host.Render();
        }

        // The second tab is laid out one tab-width to the right and slid into view:
        // the switch is drawn, and clicked, 500 px left of its layout bounds.
        var (x, y) = CompatHost.CenterOf(toggle);
        x -= 500;
        host.Context.RootView!.HitTestAt(x, y)?.MauiView.Should().BeSameAs(toggle);
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        toggle.IsToggled.Should().BeTrue();
    }
}
