// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Hosting;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using SkiaSharp.Views.Maui.Controls.Hosting;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// SkiaSharp.Views.Maui.Controls (4.152, generic net10.0 asset): SKCanvasView
/// and SKGLView paint through OpenMaui's Skia pipeline. Pass = PaintSurface
/// output reaches the presented frame, InvalidateSurface repaints, touch events
/// arrive in view coordinates, and the app's own <c>UseSkiaSharp()</c> call
/// (which registers SkiaSharp's generic, platform-less handlers) does not
/// displace OpenMaui's handlers whichever order it is chained in.
/// </summary>
[Collection(CompatHost.Collection)]
public class SkiaSharpViewsCompatTests
{
    public enum Order { NoUseSkiaSharp, UseSkiaSharpAfterUseLinux }

    private static Action<MauiAppBuilder> Configure(Order order) => order switch
    {
        Order.UseSkiaSharpAfterUseLinux => b => b.UseSkiaSharp(),
        _ => _ => { },
    };

    private static SKCanvasView RedCanvas(Action<SKPaintSurfaceEventArgs>? onPaint = null) =>
        new SKCanvasView
        {
            WidthRequest = 200,
            HeightRequest = 100,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
        }.Also(v => v.PaintSurface += (_, e) =>
        {
            e.Surface.Canvas.Clear(SKColors.Red);
            onPaint?.Invoke(e);
        });

    [Theory]
    [InlineData(Order.NoUseSkiaSharp)]
    [InlineData(Order.UseSkiaSharpAfterUseLinux)]
    public void SKCanvasView_paint_surface_output_is_presented(Order order)
    {
        SKImageInfo? info = null;
        var canvas = RedCanvas(e => info = e.Info);
        // Nested in a layout: children resolve their handlers through the
        // platform's handler lookup, where UseSkiaSharp's registration lives.
        var layout = new VerticalStackLayout { Children = { canvas } };
        using var host = new CompatHost(new ContentPage { Content = layout, BackgroundColor = Colors.White }, Configure(order));
        host.Render();

        canvas.Handler.Should().NotBeNull();
        canvas.Handler!.GetType().Namespace.Should().StartWith("Microsoft.Maui.Platform.Linux", "OpenMaui's handler wins over SkiaSharp's generic one");
        info.Should().NotBeNull("PaintSurface was raised");
        info!.Value.Width.Should().Be(200);
        info.Value.Height.Should().Be(100);
        host.CountPixelsNear(SKColors.Red, new SKRectI(0, 0, 200, 100)).Should().Be(200 * 100, "the whole canvas is red");
        host.CountPixelsNear(SKColors.Red, new SKRectI(200, 0, 400, 100)).Should().Be(0, "nothing is painted outside it");
    }

    [Fact]
    public void SKCanvasView_InvalidateSurface_repaints_with_new_state()
    {
        var color = SKColors.Red;
        int paints = 0;
        var canvas = new SKCanvasView { WidthRequest = 100, HeightRequest = 100, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        canvas.PaintSurface += (_, e) => { paints++; e.Surface.Canvas.Clear(color); };
        using var host = new CompatHost(new ContentPage { Content = canvas, BackgroundColor = Colors.White }, b => b.UseSkiaSharp());
        host.Render();
        host.CountPixelsNear(SKColors.Red, new SKRectI(0, 0, 100, 100)).Should().Be(100 * 100);
        int before = paints;

        color = SKColors.Blue;
        canvas.InvalidateSurface();
        host.Render();

        paints.Should().BeGreaterThan(before);
        host.CountPixelsNear(SKColors.Blue, new SKRectI(0, 0, 100, 100)).Should().Be(100 * 100);
    }

    [Fact]
    public void SKCanvasView_touch_events_arrive_in_view_coordinates()
    {
        var touches = new List<(SKTouchAction Action, SKPoint Location)>();
        var canvas = RedCanvas();
        canvas.EnableTouchEvents = true;
        canvas.Touch += (_, e) => { touches.Add((e.ActionType, e.Location)); e.Handled = true; };
        var page = new ContentPage
        {
            BackgroundColor = Colors.White,
            Content = new VerticalStackLayout { Padding = new Microsoft.Maui.Thickness(50, 20, 0, 0), Children = { canvas } },
        };
        using var host = new CompatHost(page, b => b.UseSkiaSharp());
        host.Render();

        host.DisplayWindow.RaisePointerPressed(60, 30);
        host.DisplayWindow.RaisePointerMoved(70, 40);
        host.DisplayWindow.RaisePointerReleased(70, 40);

        touches.Select(t => t.Action).Should().ContainInOrder(SKTouchAction.Pressed, SKTouchAction.Moved, SKTouchAction.Released);
        var pressed = touches.First(t => t.Action == SKTouchAction.Pressed).Location;
        pressed.X.Should().BeApproximately(10, 1, "locations are relative to the canvas");
        pressed.Y.Should().BeApproximately(10, 1);
    }

    [Fact]
    public void SKGLView_paints_through_the_platform_pipeline()
    {
        int paints = 0;
        var gl = new SKGLView { WidthRequest = 120, HeightRequest = 80, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        gl.PaintSurface += (_, e) => { paints++; e.Surface.Canvas.Clear(SKColors.Lime); };
        using var host = new CompatHost(new ContentPage { Content = gl, BackgroundColor = Colors.White }, b => b.UseSkiaSharp());
        host.Render();

        paints.Should().BeGreaterThan(0);
        host.CountPixelsNear(SKColors.Lime, new SKRectI(0, 0, 120, 80)).Should().Be(120 * 80);
    }
}

internal static class FluentObjectExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
