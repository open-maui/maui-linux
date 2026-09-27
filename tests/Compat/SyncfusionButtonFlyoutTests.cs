// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using SkiaShell = Microsoft.Maui.Platform.SkiaShell;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Syncfusion.Maui.Buttons;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// Strikeline's welcome page: an SfButton whose Clicked opens the Shell flyout from the main
/// thread (MainThread.BeginInvokeOnMainThread) and re-lays the page out.
/// </summary>
[Collection("LinuxApplication.Current")]
public sealed class SyncfusionButtonFlyoutTests
{
    [Fact]
    public void A_click_on_an_SfButton_that_opens_the_flyout_shows_it()
    {
        int clicks = 0;
        var button = new SfButton { Text = "Begin", WidthRequest = 200, HeightRequest = 50, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(0, 20) };
        var page = new ContentPage { Content = new Grid { RowDefinitions = new RowDefinitionCollection(new RowDefinition(Microsoft.Maui.GridLength.Star), new RowDefinition(Microsoft.Maui.GridLength.Auto)) } };
        ((Grid)page.Content).Add(new Label { Text = "welcome" }, 0, 0);
        ((Grid)page.Content).Add(button, 0, 1);
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Flyout, FlyoutWidth = 260, Items = { new Microsoft.Maui.Controls.ShellContent { Route = "Welcome", Content = page, Title = "" } } };
        button.Clicked += (_, _) =>
        {
            clicks++;
            var isMain = MainThread.IsMainThread;
            Action open = () =>
            {
                shell.FlyoutIsPresented = true;
                shell.CurrentPage.Layout(new Microsoft.Maui.Graphics.Rect(0, 0, shell.CurrentPage.Width + 1, shell.CurrentPage.Height + 1));
            };
            if (isMain) open(); else MainThread.BeginInvokeOnMainThread(open);
        };
        using var host = new CompatHost(shell, b => b.UseLinuxSyncfusion(), 800, 600);
        host.Render();
        host.Render();

        var (x, y) = CompatHost.CenterOf(button);
        host.DisplayWindow.RaisePointerMoved(x, y);
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        for (int i = 0; i < 5; i++)
        {
            Microsoft.Maui.Platform.Linux.Native.GLibNative.ProcessPendingEvents(50);
            Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll();
            host.Render();
        }

        clicks.Should().Be(1);
        shell.FlyoutIsPresented.Should().BeTrue();
        ((SkiaShell)shell.Handler!.PlatformView!).FlyoutIsPresented.Should().BeTrue();
    }
}
