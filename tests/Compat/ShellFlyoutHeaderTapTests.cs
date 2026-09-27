// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Xunit;
using Xunit.Abstractions;
using SkiaShell = Microsoft.Maui.Platform.SkiaShell;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// Strikeline's setup flyout: the menu is the FlyoutHeader, a view whose rows carry
/// TapGestureRecognizers. A click on a row in the opened flyout fires its tap.
/// </summary>
[Collection("LinuxApplication.Current")]
public sealed class ShellFlyoutHeaderTapTests
{
    private readonly ITestOutputHelper _out;
    public ShellFlyoutHeaderTapTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void A_tap_on_a_flyout_header_row_fires()
    {
        int taps = 0;
        var row = new Grid { HeightRequest = 60, ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new Microsoft.Maui.GridLength(0.2, Microsoft.Maui.GridUnitType.Star)), new ColumnDefinition(new Microsoft.Maui.GridLength(0.8, Microsoft.Maui.GridUnitType.Star))) };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => taps++;
        row.GestureRecognizers.Add(tap);
        row.Add(new Label { Text = "Demo Account" }, 1, 0);
        // Taller than the old fixed 140 px header area: logo, a spacer, then the row.
        var header = new ContentView { Content = new StackLayout { Children = { new BoxView { HeightRequest = 100 }, new BoxView { HeightRequest = 120 }, row } } };
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Flyout, FlyoutWidth = 260, FlyoutHeader = header,
            Items = { new Microsoft.Maui.Controls.ShellContent { Route = "Welcome", Content = new ContentPage { Content = new Label { Text = "welcome" } } } } };
        using var host = new CompatHost(shell, null, 800, 600);
        host.Render();
        shell.FlyoutIsPresented = true;
        for (int i = 0; i < 3; i++) { Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll(); host.Render(); }

        var pv = (Microsoft.Maui.Platform.SkiaView)row.Handler!.PlatformView!;
        var (x, y) = CompatHost.CenterOf(row);
        var hit = host.Context.RootView!.HitTestAt(x, y);
        _out.WriteLine($"row {pv.Bounds} screen {pv.ScreenBounds} hit {hit?.GetType().Name}/{hit?.MauiView?.GetType().Name} presented {((SkiaShell)shell.Handler!.PlatformView!).FlyoutIsPresented}");
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        for (int i = 0; i < 3; i++) { Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll(); host.Render(); }

        taps.Should().Be(1);
    }

    [Fact]
    public async Task A_flyout_header_row_that_closes_the_flyout_and_pushes_a_page_shows_it()
    {
        Shell? shell = null;
        var pushed = new ContentPage { Title = "Signup", Content = new Label { Text = "signup" } };
        var row = new Grid { HeightRequest = 60 };
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            shell!.FlyoutIsPresented = false;
            await shell.Navigation.PushAsync(pushed);
        };
        row.GestureRecognizers.Add(tap);
        row.Add(new Label { Text = "New Account" });
        shell = new Shell { FlyoutBehavior = FlyoutBehavior.Flyout, FlyoutWidth = 260,
            FlyoutHeader = new ContentView { Content = new StackLayout { Children = { new BoxView { HeightRequest = 100 }, new BoxView { HeightRequest = 120 }, row } } },
            Items = { new Microsoft.Maui.Controls.ShellContent { Route = "Welcome", Content = new ContentPage { Content = new Label { Text = "welcome" } } } } };
        using var host = new CompatHost(shell, null, 800, 600);
        host.Render();
        shell.FlyoutIsPresented = true;
        for (int i = 0; i < 3; i++) { Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll(); host.Render(); }

        var (x, y) = CompatHost.CenterOf(row);
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        for (int i = 0; i < 5; i++) { await Task.Delay(20); Microsoft.Maui.Platform.Linux.Native.GLibNative.ProcessPendingEvents(50); Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll(); host.Render(); }

        var platform = (SkiaShell)shell.Handler!.PlatformView!;
        _out.WriteLine($"maui page {shell.CurrentPage?.Title} stack {shell.Navigation.NavigationStack.Count} platform depth {platform.NavigationStackDepth} presented {platform.FlyoutIsPresented}");
        shell.CurrentPage.Should().BeSameAs(pushed);
        platform.FlyoutIsPresented.Should().BeFalse();
        platform.NavigationStackDepth.Should().Be(1);
        platform.CurrentMauiPage.Should().BeSameAs(pushed);
    }

    [Fact]
    public void A_tap_on_a_row_of_a_tall_flyout_footer_fires()
    {
        // Strikeline's footer: three 60 px rows, taller than the default 120 px footer area.
        int taps = 0;
        var rows = new StackLayout();
        Grid? last = null;
        for (int i = 0; i < 3; i++)
        {
            last = new Grid { HeightRequest = 60, Children = { new Label { Text = $"Footer {i}" } } };
            rows.Children.Add(last);
        }
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => taps++;
        var first = (Grid)rows.Children[0];
        first.GestureRecognizers.Add(tap);
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Flyout, FlyoutWidth = 260, FlyoutFooter = new ContentView { Content = rows },
            Items = { new Microsoft.Maui.Controls.ShellContent { Route = "Welcome", Content = new ContentPage { Content = new Label { Text = "welcome" } } } } };
        using var host = new CompatHost(shell, null, 800, 600);
        host.Render();
        shell.FlyoutIsPresented = true;
        for (int i = 0; i < 3; i++) { Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll(); host.Render(); }

        var (x, y) = CompatHost.CenterOf(first);
        y.Should().BeLessThan(600 - 120, "the first row sits above the old fixed footer area");
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        for (int i = 0; i < 3; i++) { Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll(); host.Render(); }

        taps.Should().Be(1);
    }
}
