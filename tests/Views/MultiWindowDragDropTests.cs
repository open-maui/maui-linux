// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// Drag-and-drop onto a secondary window: the backend names the window a
/// drag is over, and the drop lands in that window's tree (not the primary's),
/// using that window's own scale.
/// </summary>
[Collection("LinuxApplication.Current")]
public class MultiWindowDragDropTests
{
    private static Label DropLabel(Action<DropEventArgs> onDrop, Action? onOver = null)
    {
        var label = new Label { Text = "drop here", WidthRequest = 200, HeightRequest = 100, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        var recognizer = new DropGestureRecognizer { AllowDrop = true };
        recognizer.Drop += (_, e) => onDrop(e);
        if (onOver != null) recognizer.DragOver += (_, _) => onOver();
        label.GestureRecognizers.Add(recognizer);
        return label;
    }

    [Fact]
    public void A_drop_over_a_secondary_window_reaches_that_window()
    {
        int primaryDrops = 0, secondaryDrops = 0, secondaryOvers = 0;
        using var host = new HeadlessMauiHost(new ContentPage { Content = DropLabel(_ => primaryDrops++) }, withEngine: true);
        host.Context.Render();

        var secondaryWindow = new HeadlessMauiHost.FakeDisplayWindow { Width = 800, Height = 600 };
        var secondaryEngine = new SkiaRenderingEngine(secondaryWindow);
        var secondary = host.LinuxApp.AttachWindowContext(secondaryWindow, secondaryEngine, raisesMauiLifecycle: false);
        var secondaryPage = new ContentPage { Content = DropLabel(_ => secondaryDrops++, () => secondaryOvers++) };
        secondary.RootView = new Microsoft.Maui.Platform.Linux.Hosting.LinuxViewRenderer(host.MauiContext).RenderPage(secondaryPage);
        secondary.Render();

        host.LinuxApp.WireDragDropRouting();
        try
        {
            var data = new DragData { SupportedMimeTypes = new[] { "text/plain" } };
            DragDropService.Default.RaiseDragEnter(data, 50, 50, secondaryWindow);
            DragDropService.Default.RaiseDragOver(data, 50, 50, secondaryWindow);
            DragDropService.Default.RaiseDrop(data, "payload", 50, 50, secondaryWindow);
        }
        finally
        {
            host.LinuxApp.UnwireDragDropRouting();
        }

        secondaryOvers.Should().BeGreaterThan(0);
        secondaryDrops.Should().Be(1);
        primaryDrops.Should().Be(0, "the primary window's label sits at the same coordinates but was not under the drag");
    }

    [Fact]
    public void A_drop_without_a_window_identity_goes_to_the_primary()
    {
        int primaryDrops = 0;
        using var host = new HeadlessMauiHost(new ContentPage { Content = DropLabel(_ => primaryDrops++) }, withEngine: true);
        host.Context.Render();

        host.LinuxApp.WireDragDropRouting();
        try
        {
            var data = new DragData { SupportedMimeTypes = new[] { "text/plain" } };
            DragDropService.Default.RaiseDragEnter(data, 50, 50);
            DragDropService.Default.RaiseDrop(data, "payload", 50, 50);
        }
        finally
        {
            host.LinuxApp.UnwireDragDropRouting();
        }

        primaryDrops.Should().Be(1);
    }

    [Fact]
    public void Drop_coordinates_use_the_target_windows_scale()
    {
        int drops = 0;
        using var host = new HeadlessMauiHost(new ContentPage { Content = new Label { Text = "primary" } }, withEngine: true);
        host.Context.Render();

        var secondaryWindow = new HeadlessMauiHost.FakeDisplayWindow { Width = 1600, Height = 1200 };
        var secondaryEngine = new SkiaRenderingEngine(secondaryWindow) { DpiScale = 2f };
        var secondary = host.LinuxApp.AttachWindowContext(secondaryWindow, secondaryEngine, raisesMauiLifecycle: false);
        secondary.RootView = new Microsoft.Maui.Platform.Linux.Hosting.LinuxViewRenderer(host.MauiContext)
            .RenderPage(new ContentPage { Content = DropLabel(_ => drops++) });
        secondary.Render();

        host.LinuxApp.WireDragDropRouting();
        try
        {
            var data = new DragData { SupportedMimeTypes = new[] { "text/plain" } };
            // Physical (300, 150) at 2x is logical (150, 75): inside the 200x100 label.
            DragDropService.Default.RaiseDragEnter(data, 300, 150, secondaryWindow);
            DragDropService.Default.RaiseDrop(data, "payload", 300, 150, secondaryWindow);
            // Physical (500, 150) is logical (250, 75): outside it.
            DragDropService.Default.RaiseDragEnter(data, 500, 150, secondaryWindow);
            DragDropService.Default.RaiseDrop(data, "payload", 500, 150, secondaryWindow);
        }
        finally
        {
            host.LinuxApp.UnwireDragDropRouting();
        }

        drops.Should().Be(1);
    }
}

/// <summary>
/// GTK mode: gtk_main owns the thread, so native secondary windows are pumped
/// from a GLib timeout. The pump renders them, reaps closed ones and stops
/// itself when none are left.
/// </summary>
[Collection("LinuxApplication.Current")]
public class GtkModeSecondaryPumpTests
{
    [Fact]
    public void Pump_renders_secondaries_and_stops_when_the_last_one_closes()
    {
        using var host = new HeadlessMauiHost(new ContentPage(), withEngine: true);
        var window = new HeadlessMauiHost.FakeDisplayWindow { Width = 400, Height = 300 };
        var engine = new SkiaRenderingEngine(window);
        var ctx = host.LinuxApp.AttachWindowContext(window, engine, raisesMauiLifecycle: false);
        ctx.RootView = new Microsoft.Maui.Platform.Linux.Hosting.LinuxViewRenderer(host.MauiContext)
            .RenderPage(new ContentPage { Content = new Label { Text = "secondary" } });
        engine.InvalidateAll();

        host.LinuxApp.PumpNativeWindows().Should().BeTrue("a native window is live");
        window.PresentCount.Should().BeGreaterThan(0, "the secondary was rendered");

        window.Stop();
        host.LinuxApp.PumpNativeWindows();

        host.LinuxApp.WindowContexts.Should().NotContain(ctx, "the closed secondary was reaped");
    }
}
