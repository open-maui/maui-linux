// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Controls.Linux.Tests.Golden;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>BoxView and Frame rendering, plus ContentView hosting a child.</summary>
public class SkiaBoxViewTests
{
    [Fact]
    public void Fills_its_bounds_with_its_colour()
    {
        var box = new SkiaBoxView { Color = Colors.Red };
        using var bmp = GoldenHarness.Render(box, 40, 30, 1f);
        var c = bmp.GetPixel(20, 15);
        c.Red.Should().BeGreaterThan(200); c.Green.Should().BeLessThan(40); c.Blue.Should().BeLessThan(40);
    }

    [Fact]
    public void Corner_radius_leaves_the_corner_unpainted()
    {
        var box = new SkiaBoxView { Color = Colors.Red, CornerRadius = new CornerRadius(15) };
        using var bmp = GoldenHarness.Render(box, 60, 60, 1f);
        bmp.GetPixel(30, 30).Red.Should().BeGreaterThan(200);
        var corner = bmp.GetPixel(1, 1);
        corner.Red.Should().BeGreaterThan(200); corner.Green.Should().BeGreaterThan(200); // white background shows through
    }

    [Fact]
    public void Measures_to_its_requests()
    {
        var box = new SkiaBoxView { Color = Colors.Red, WidthRequest = 33, HeightRequest = 21 };
        var size = box.Measure(new Size(500, 500));
        size.Width.Should().Be(33);
        size.Height.Should().Be(21);
    }
}

public class SkiaFrameTests
{
    [Fact]
    public void Frame_has_frame_defaults()
    {
        var frame = new SkiaFrame();
        frame.HasShadow.Should().BeTrue();
        frame.CornerRadius.Should().Be(4.0);
        frame.BackgroundColor.Should().Be(Colors.White);
    }

    [Fact]
    public void Frame_pads_and_hosts_its_content()
    {
        var frame = new SkiaFrame();
        var label = new SkiaLabel { Text = "inside", FontSize = 14 };
        frame.AddChild(label);
        frame.Measure(new Size(300, 100));
        frame.Arrange(new Rect(0, 0, 300, 100));

        label.Bounds.Left.Should().BeGreaterThanOrEqualTo(10, "frame padding is 10");
        label.Bounds.Top.Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void Frame_draws_background_and_shadow()
    {
        var frame = new SkiaFrame { BackgroundColor = Colors.Blue, HasShadow = true };
        frame.AddChild(new SkiaBoxView { Color = Colors.Blue, WidthRequest = 20, HeightRequest = 20 });
        using var bmp = GoldenHarness.Render(frame, 80, 60, 1f);
        var centre = bmp.GetPixel(40, 30);
        centre.Blue.Should().BeGreaterThan(150);
    }
}

public class ContentViewTests
{
    [Fact]
    public void ContentView_sizes_to_its_child_and_hosts_it()
    {
        var content = new SkiaContentView();
        var label = new SkiaLabel { Text = "content", FontSize = 16 };
        content.AddChild(label);
        content.Measure(new Size(300, 300));
        content.Arrange(new Rect(0, 0, 300, 300));

        label.Bounds.Width.Should().BeGreaterThan(0);
        content.Children.Should().ContainSingle().Which.Should().BeSameAs(label);
    }

    [Fact]
    public void ContentView_padding_offsets_the_child()
    {
        var content = new SkiaContentView { Padding = new Thickness(12, 8) };
        var label = new SkiaLabel { Text = "content", FontSize = 16 };
        content.AddChild(label);
        content.Measure(new Size(300, 300));
        content.Arrange(new Rect(0, 0, 300, 300));

        label.Bounds.Left.Should().Be(12);
        label.Bounds.Top.Should().Be(8);
    }

    [Fact]
    public void ContentView_draws_background_and_child()
    {
        var content = new SkiaContentView { BackgroundColor = Colors.Yellow };
        content.AddChild(new SkiaBoxView { Color = Colors.Black, WidthRequest = 10, HeightRequest = 10 });
        using var bmp = GoldenHarness.Render(content, 60, 60, 1f);
        bmp.GetPixel(55, 55).Blue.Should().BeLessThan(60, "yellow background");
        bmp.GetPixel(5, 5).Red.Should().BeLessThan(60, "black child");
    }
}
