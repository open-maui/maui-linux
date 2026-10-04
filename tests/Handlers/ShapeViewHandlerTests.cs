// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls.Linux.Tests.Views;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Xunit;
using ShapePath = Microsoft.Maui.Controls.Shapes.Path;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// Shapes realized through their handlers draw MAUI's ShapeDrawable on a
/// <see cref="SkiaShapeView"/>, as MAUI's ShapeViewHandler does: paints other
/// than a solid colour, the Aspect stretch (platform-only code in Controls), a
/// Path's RenderTransform and a change inside a Points collection all reach the
/// pixels.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShapeViewHandlerTests
{
    private static HeadlessMauiHost Host(View shape) =>
        new(new ContentPage
        {
            BackgroundColor = Colors.White,
            Content = new Grid
            {
                WidthRequest = 100, HeightRequest = 100,
                HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
                Children = { shape },
            },
        }, withEngine: true);

    private static (byte R, byte G, byte B, byte A) Render(HeadlessMauiHost host, int x, int y)
    {
        host.Context.Render();
        return host.DisplayWindow.PixelAt(x, y);
    }

    [Fact]
    public void Shape_handlers_draw_a_ShapeDrawable_over_the_shape()
    {
        var line = new Line { X1 = 0, Y1 = 0, X2 = 100, Y2 = 100, Stroke = Colors.Black, StrokeThickness = 4 };
        using var host = Host(line);

        line.Handler.Should().BeOfType<LineHandler>().And.BeAssignableTo<Microsoft.Maui.Handlers.IShapeViewHandler>();
        var platform = (SkiaShapeView)line.Handler!.PlatformView!;
        platform.Drawable.Should().BeOfType<ShapeDrawable>();
    }

    [Fact]
    public void A_gradient_fill_is_drawn()
    {
        var rectangle = new Rectangle
        {
            Fill = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(Colors.Red, 0),
                new GradientStop(Colors.Blue, 1),
            }, new Point(0, 0), new Point(1, 0)),
        };
        using var host = Host(rectangle);

        var left = Render(host, 5, 50);
        var right = Render(host, 95, 50);
        ((int)left.R).Should().BeGreaterThan(200, "the gradient starts red");
        ((int)left.B).Should().BeLessThan(60);
        ((int)right.B).Should().BeGreaterThan(200, "and ends blue");
        ((int)right.R).Should().BeLessThan(60);
    }

    [Fact]
    public void A_uniform_path_is_stretched_into_its_view()
    {
        // A 10x10 square, Uniform: it fills the 100x100 view.
        var path = new ShapePath
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString("M 0 0 L 10 0 L 10 10 L 0 10 Z")!,
            Fill = Colors.Red,
            Aspect = Stretch.Uniform,
        };
        using var host = Host(path);

        Render(host, 90, 90).Should().Be(((byte)255, (byte)0, (byte)0, (byte)255));
    }

    [Fact]
    public void A_path_render_transform_moves_the_shape()
    {
        var path = new ShapePath
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString("M 0 0 L 20 0 L 20 20 L 0 20 Z")!,
            Fill = Colors.Red,
        };
        using var host = Host(path);
        Render(host, 10, 10).Should().Be(((byte)255, (byte)0, (byte)0, (byte)255));

        path.RenderTransform = new TranslateTransform { X = 50 };
        var (_, g, _, _) = Render(host, 10, 10);
        ((int)g).Should().BeGreaterThan(240, "the square moved off");
        Render(host, 60, 10).Should().Be(((byte)255, (byte)0, (byte)0, (byte)255));
    }

    [Fact]
    public void A_point_added_to_a_polygon_redraws_it()
    {
        // A triangle over the top-left half, then a square once a corner is added.
        var polygon = new Polygon
        {
            Points = new PointCollection { new Point(0, 0), new Point(100, 0), new Point(0, 100) },
            Fill = Colors.Red,
        };
        using var host = Host(polygon);
        var (_, g, _, _) = Render(host, 90, 90);
        ((int)g).Should().BeGreaterThan(240, "the bottom-right corner is outside the triangle");

        polygon.Points.Insert(2, new Point(100, 100));
        Render(host, 90, 90).Should().Be(((byte)255, (byte)0, (byte)0, (byte)255));
    }

    [Fact]
    public void A_RoundRectangle_gets_its_handler()
    {
        var shape = new RoundRectangle { CornerRadius = 10, Fill = Colors.Red };
        using var host = Host(shape);

        shape.Handler.Should().BeOfType<RoundRectangleHandler>();
        Render(host, 50, 50).Should().Be(((byte)255, (byte)0, (byte)0, (byte)255));
        var (_, g, _, _) = Render(host, 1, 1);
        ((int)g).Should().BeGreaterThan(200, "the corner is rounded off");
    }

    [Fact]
    public void A_shape_view_with_only_a_background_fills_the_shape_with_it()
    {
        // MAUI's ShapeDrawable fills with Background when there is no Fill; the view
        // itself paints no background, so an ellipse leaves its corners clear.
        var ellipse = new Ellipse { Background = Colors.Red };
        using var host = Host(ellipse);

        Render(host, 50, 50).Should().Be(((byte)255, (byte)0, (byte)0, (byte)255));
        var (_, g, _, _) = Render(host, 2, 2);
        ((int)g).Should().BeGreaterThan(240, "outside the ellipse");
    }
}
