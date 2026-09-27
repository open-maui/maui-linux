// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls.Shapes;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

[Collection("LinuxApplication.Current")]
public class LegacyStackLayoutTests
{
    [Fact]
    public void A_horizontal_StackLayout_lays_out_in_a_row()
    {
        // Strikeline's Outlook panels: a status dot beside the value.
        var dot = new Ellipse { WidthRequest = 16, HeightRequest = 16, Fill = Colors.Gold };
        var value = new Label { Text = "Mixed", FontSize = 20 };
        var row = new StackLayout { Orientation = StackOrientation.Horizontal, Spacing = 4, Children = { dot, value } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { WidthRequest = 300, Children = { row } } }, withEngine: true);
        host.Context.Render();

        var d = ((Microsoft.Maui.Platform.SkiaView)dot.Handler!.PlatformView!).Bounds;
        var v = ((Microsoft.Maui.Platform.SkiaView)value.Handler!.PlatformView!).Bounds;
        d.Width.Should().BeApproximately(16, 0.5);
        v.Left.Should().BeApproximately(d.Right + 4, 0.5, "beside the dot");
        v.Top.Should().BeLessThan(d.Bottom, "on the same row");
    }

    [Fact]
    public void Orientation_changed_after_start_takes_effect()
    {
        var a = new BoxView { WidthRequest = 20, HeightRequest = 20 };
        var b = new BoxView { WidthRequest = 20, HeightRequest = 20 };
        var stack = new StackLayout { Children = { a, b } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = stack }, withEngine: true);
        host.Context.Render();
        SkiaBounds(b).Top.Should().BeGreaterThan(SkiaBounds(a).Top);

        stack.Orientation = StackOrientation.Horizontal;
        host.Context.Render();
        SkiaBounds(b).Left.Should().BeGreaterThan(SkiaBounds(a).Left);
        SkiaBounds(b).Top.Should().Be(SkiaBounds(a).Top);
    }

    [Fact]
    public void A_sized_ellipse_in_a_vertical_stack_keeps_its_width()
    {
        var dot = new Ellipse { WidthRequest = 16, HeightRequest = 16, Fill = Colors.Gold };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { WidthRequest = 300, Children = { dot } } }, withEngine: true);
        host.Context.Render();

        var p = ((Microsoft.Maui.Platform.SkiaView)dot.Handler!.PlatformView!).Bounds;
        // MAUI centres a Fill view with an explicit size.
        p.Width.Should().BeApproximately(16, 0.5);
        var stack = ((Microsoft.Maui.Platform.SkiaView)((VisualElement)dot.Parent).Handler!.PlatformView!).Bounds;
        p.Center.X.Should().BeApproximately(stack.Center.X, 0.5);
    }

    private static Rect SkiaBounds(View v) => ((Microsoft.Maui.Platform.SkiaView)v.Handler!.PlatformView!).Bounds;
}
