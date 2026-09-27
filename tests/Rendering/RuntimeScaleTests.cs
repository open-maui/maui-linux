// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Rendering;

/// <summary>
/// Runtime scale change: a window moved to a monitor with a different scale
/// (or a desktop scale change) re-renders at the new density while keeping its
/// logical size, and input keeps mapping to the right logical coordinates.
/// </summary>
[Collection("LinuxApplication.Current")]
public class RuntimeScaleTests
{
    [Theory]
    [InlineData(1400, 1050, 1.75f, 1.0f, 800, 600, 800, 600)]
    [InlineData(800, 600, 1.0f, 2.0f, 800, 600, 1600, 1200)]
    [InlineData(1000, 750, 1.25f, 1.5f, 800, 600, 1200, 900)]
    [InlineData(1, 1, 2.0f, 1.0f, 1, 1, 1, 1)]
    public void Rescale_keeps_the_logical_size(int bw, int bh, float oldScale, float newScale, int lw, int lh, int nbw, int nbh)
    {
        var r = WaylandWindow.RescaleSize(bw, bh, oldScale, newScale);

        r.LogicalWidth.Should().Be(lw);
        r.LogicalHeight.Should().Be(lh);
        r.BufferWidth.Should().Be(nbw);
        r.BufferHeight.Should().Be(nbh);
    }

    [Fact]
    public void Scale_change_updates_the_engine_and_lays_out_at_the_same_logical_size()
    {
        var label = new Label { Text = "scale" };
        using var host = new HeadlessMauiHost(new ContentPage { Content = label }, withEngine: true);
        host.Context.Render();
        var engine = host.Context.RenderingEngine!;
        engine.LogicalWidth.Should().Be(800);

        host.DisplayWindow.RaiseScaleChanged(2.0f);
        host.Context.Render();

        engine.DpiScale.Should().Be(2.0f);
        host.DisplayWindow.Width.Should().Be(1600, "the buffer doubled");
        engine.LogicalWidth.Should().Be(800, "the logical size is kept");
        engine.LogicalHeight.Should().Be(600);
        host.RootView.Bounds.Width.Should().Be(800);
    }

    [Fact]
    public void Pointer_input_maps_through_the_new_scale()
    {
        var clicked = 0;
        var button = new Button { Text = "hit", WidthRequest = 100, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        button.Clicked += (_, _) => clicked++;
        using var host = new HeadlessMauiHost(new ContentPage { Content = button }, withEngine: true);
        host.Context.Render();

        host.DisplayWindow.RaiseScaleChanged(2.0f);
        host.Context.Render();

        // Physical (160, 60) is logical (80, 30): inside the 100x40 button.
        host.DisplayWindow.RaisePointerPressed(160, 60);
        host.DisplayWindow.RaisePointerReleased(160, 60);
        clicked.Should().Be(1);

        // Physical (300, 60) is logical (150, 30): outside it.
        var pv = (Microsoft.Maui.Platform.SkiaView)button.Handler!.PlatformView!;
        host.DisplayWindow.RaisePointerPressed(300, 60);
        host.DisplayWindow.RaisePointerReleased(300, 60);
        clicked.Should().Be(1, $"bounds={pv.Bounds}");
    }

    [Fact]
    public void X11_style_change_keeps_pixels_and_shrinks_the_logical_size()
    {
        using var host = new HeadlessMauiHost(new ContentPage { Content = new Label { Text = "x11" } }, withEngine: true);
        host.Context.Render();

        host.DisplayWindow.RaiseScaleChanged(2.0f, keepPixels: true);
        host.Context.Render();

        host.DisplayWindow.Width.Should().Be(800);
        host.Context.RenderingEngine!.LogicalWidth.Should().Be(400);
        host.RootView.Bounds.Width.Should().Be(400);
    }

    [Fact]
    public void Primary_window_scale_change_updates_the_application_scale()
    {
        using var host = new HeadlessMauiHost(new ContentPage(), withEngine: true);
        float? raised = null;
        host.LinuxApp.DpiScaleChanged += (_, s) => raised = s;

        host.DisplayWindow.RaiseScaleChanged(1.5f);

        if (ReferenceEquals(host.LinuxApp.MainWindow, host.DisplayWindow))
        {
            host.LinuxApp.DpiScale.Should().Be(1.5f);
            raised.Should().Be(1.5f);
        }
        host.Context.RenderingEngine!.DpiScale.Should().Be(1.5f);
    }

    [Fact]
    public void Repeated_identical_scale_is_ignored()
    {
        using var host = new HeadlessMauiHost(new ContentPage(), withEngine: true);
        host.DisplayWindow.RaiseScaleChanged(2.0f);
        int widthAfterFirst = host.DisplayWindow.Width;

        host.DisplayWindow.RaiseScaleChanged(2.0f, keepPixels: true);

        host.Context.RenderingEngine!.DpiScale.Should().Be(2.0f);
        host.DisplayWindow.Width.Should().Be(widthAfterFirst);
    }

    [Fact]
    public void Scale_detection_parses_with_the_invariant_culture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousGdk = Environment.GetEnvironmentVariable("GDK_SCALE");
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // "1.5" would parse as 15 there
            Environment.SetEnvironmentVariable("GDK_SCALE", "1.5");

            var hiDpi = new HiDpiService();
            hiDpi.DetectScaleFactor();

            hiDpi.ScaleFactor.Should().Be(1.5f);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            Environment.SetEnvironmentVariable("GDK_SCALE", previousGdk);
        }
    }
}
