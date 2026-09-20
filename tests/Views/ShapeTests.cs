// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Maui.Controls.Linux.Tests.Golden;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

using Rect = Microsoft.Maui.Graphics.Rect;
using Size = Microsoft.Maui.Graphics.Size;
using StackOrientation = Microsoft.Maui.Platform.StackOrientation;
using ShapePath = Microsoft.Maui.Controls.Shapes.Path;

/// <summary>
/// Graphics and shapes: every Skia shape view measures, arranges and draws,
/// and the rendered pixels are where the shape says they are. Rendering goes
/// through the golden harness (real engine, bitmap target) so the coordinate
/// space is the production one: Bounds are absolute, shape points are local.
/// </summary>
public class ShapeTests
{
    private static SolidColorBrush Brush(Color c) => new(c);

    private static bool IsColor(SKColor actual, SKColor expected, int tolerance = 8)
        => Math.Abs(actual.Red - expected.Red) <= tolerance
        && Math.Abs(actual.Green - expected.Green) <= tolerance
        && Math.Abs(actual.Blue - expected.Blue) <= tolerance
        && Math.Abs(actual.Alpha - expected.Alpha) <= tolerance;

    /// <summary>Renders a single shape placed at (offsetX, offsetY) inside a transparent scene.</summary>
    private static SKBitmap RenderAt(SkiaView shape, int offsetX, int offsetY, int width = 200, int height = 200)
    {
        var root = new SkiaAbsoluteLayout();
        root.AddChild(shape);
        root.SetLayoutBounds(shape, new SKRect(offsetX, offsetY, offsetX + (float)shape.WidthRequest, offsetY + (float)shape.HeightRequest));
        return GoldenHarness.Render(root, width, height, 1f, SKColors.Transparent);
    }

    // ---- Measure / arrange ---------------------------------------------------

    public static TheoryData<SkiaView> AllShapes => new()
    {
        new SkiaRectangle { Fill = Brush(Colors.Red), WidthRequest = 50, HeightRequest = 30 },
        new SkiaEllipse { Fill = Brush(Colors.Blue), WidthRequest = 50, HeightRequest = 30 },
        new SkiaLine { X1 = 0, Y1 = 0, X2 = 50, Y2 = 30, Stroke = Brush(Colors.Black) },
        new SkiaShapePath { Data = Geometry("M 0 0 L 50 0 L 50 30 Z"), FillColor = Colors.Green },
        new SkiaPolygon { Points = Points((0, 0), (50, 0), (25, 30)), Fill = Brush(Colors.Purple) },
        new SkiaPolyline { Points = Points((0, 0), (25, 30), (50, 0)), Stroke = Brush(Colors.Orange) },
    };

    [Theory]
    [MemberData(nameof(AllShapes))]
    public void Every_shape_measures_arranges_and_draws_without_throwing(SkiaView shape)
    {
        var size = shape.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        size.Width.Should().BeGreaterThan(0);
        size.Height.Should().BeGreaterThan(0);

        shape.Arrange(new Rect(10, 10, size.Width, size.Height));
        shape.Bounds.Width.Should().Be(size.Width);

        using var bmp = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bmp);
        var act = () => shape.Draw(canvas);
        act.Should().NotThrow();
    }

    [Fact]
    public void Rectangle_measures_from_requests_and_defaults_to_40_when_unbounded()
    {
        new SkiaRectangle { WidthRequest = 80, HeightRequest = 20 }
            .Measure(new Size(300, 300)).Should().Be(new Size(80, 20));
        new SkiaRectangle()
            .Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)).Should().Be(new Size(40, 40));
    }

    [Fact]
    public void Line_measures_its_extent_plus_stroke()
    {
        var line = new SkiaLine { X1 = 0, Y1 = 0, X2 = 100, Y2 = 40, StrokeThickness = 4 };
        line.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)).Should().Be(new Size(104, 44));
    }

    [Fact]
    public void Polygon_and_polyline_measure_from_their_points()
    {
        var polygon = new SkiaPolygon { Points = Points((0, 0), (60, 0), (30, 40)), StrokeThickness = 2 };
        polygon.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)).Should().Be(new Size(62, 42));
        var polyline = new SkiaPolyline { Points = Points((0, 10), (80, 10)), StrokeThickness = 1 };
        polyline.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)).Should().Be(new Size(81, 11));
    }

    [Fact]
    public void Path_measures_from_geometry_bounds()
    {
        var path = new SkiaShapePath { Data = Geometry("M 10 10 L 100 10 L 100 100 Z") };
        var size = path.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        size.Width.Should().BeApproximately(100, 0.01);
        size.Height.Should().BeApproximately(100, 0.01);
    }

    // ---- Rectangle ------------------------------------------------------------

    [Fact]
    public void Filled_rectangle_is_red_inside_and_transparent_outside()
    {
        var rect = new SkiaRectangle { Fill = Brush(Colors.Red), WidthRequest = 60, HeightRequest = 40 };
        using var bmp = RenderAt(rect, 20, 30);

        IsColor(bmp.GetPixel(50, 50), SKColors.Red).Should().BeTrue("centre of the fill");
        IsColor(bmp.GetPixel(21, 31), SKColors.Red).Should().BeTrue("just inside the top-left corner");
        bmp.GetPixel(10, 10).Alpha.Should().Be(0, "outside the shape stays transparent");
        bmp.GetPixel(90, 50).Alpha.Should().Be(0, "right of the shape stays transparent");
    }

    [Fact]
    public void Stroked_rectangle_has_stroke_colour_on_the_edge_and_fill_in_the_middle()
    {
        var rect = new SkiaRectangle
        {
            Fill = Brush(Colors.Yellow),
            Stroke = Brush(Colors.Blue),
            StrokeThickness = 6,
            WidthRequest = 60,
            HeightRequest = 40,
        };
        using var bmp = RenderAt(rect, 20, 30);

        IsColor(bmp.GetPixel(50, 50), SKColors.Yellow).Should().BeTrue("fill in the middle");
        IsColor(bmp.GetPixel(22, 50), SKColors.Blue).Should().BeTrue("stroke on the left edge");
        IsColor(bmp.GetPixel(50, 32), SKColors.Blue).Should().BeTrue("stroke on the top edge");
        IsColor(bmp.GetPixel(77, 50), SKColors.Blue).Should().BeTrue("stroke on the right edge");
    }

    [Fact]
    public void Rounded_rectangle_leaves_corner_pixels_empty()
    {
        var rect = new SkiaRectangle { Fill = Brush(Colors.Red), RadiusX = 20, RadiusY = 20, WidthRequest = 60, HeightRequest = 60 };
        using var bmp = RenderAt(rect, 0, 0);

        bmp.GetPixel(1, 1).Alpha.Should().Be(0, "a 20px corner radius clears the corner pixel");
        IsColor(bmp.GetPixel(30, 30), SKColors.Red).Should().BeTrue();
        IsColor(bmp.GetPixel(30, 1), SKColors.Red).Should().BeTrue("the top edge midpoint is still filled");
    }

    [Fact]
    public void Rectangle_stroke_dash_array_breaks_the_edge()
    {
        var rect = new SkiaRectangle
        {
            Stroke = Brush(Colors.Black),
            StrokeThickness = 4,
            StrokeDashArray = new DoubleCollection { 4, 4 },
            WidthRequest = 120,
            HeightRequest = 60,
        };
        using var bmp = RenderAt(rect, 0, 0);

        // Along the top edge (y = 2, the stroke centre) some pixels are inked and some are gaps.
        var row = Enumerable.Range(4, 100).Select(x => bmp.GetPixel(x, 2).Alpha > 128).ToList();
        row.Should().Contain(true).And.Contain(false, "a dashed stroke alternates ink and gaps");
    }

    // ---- Ellipse --------------------------------------------------------------

    [Fact]
    public void Ellipse_fills_its_centre_but_not_its_corners()
    {
        var ellipse = new SkiaEllipse { Fill = Brush(Colors.Blue), WidthRequest = 80, HeightRequest = 40 };
        using var bmp = RenderAt(ellipse, 10, 10);

        IsColor(bmp.GetPixel(50, 30), SKColors.Blue).Should().BeTrue("centre");
        bmp.GetPixel(11, 11).Alpha.Should().Be(0, "corner of the bounding box is outside the ellipse");
        bmp.GetPixel(89, 49).Alpha.Should().Be(0);
    }

    [Fact]
    public void Ellipse_stroke_sits_on_the_rim()
    {
        var ellipse = new SkiaEllipse { Fill = Brush(Colors.White), Stroke = Brush(Colors.Green), StrokeThickness = 6, WidthRequest = 80, HeightRequest = 80 };
        using var bmp = RenderAt(ellipse, 0, 0);

        IsColor(bmp.GetPixel(40, 3), SKColors.Green).Should().BeTrue("top of the rim");
        IsColor(bmp.GetPixel(3, 40), SKColors.Green).Should().BeTrue("left of the rim");
        IsColor(bmp.GetPixel(40, 40), SKColors.White).Should().BeTrue("fill in the middle");
    }

    // ---- Line -----------------------------------------------------------------

    [Fact]
    public void Line_draws_ink_along_its_slope_relative_to_its_bounds()
    {
        var line = new SkiaLine { X1 = 0, Y1 = 0, X2 = 100, Y2 = 100, Stroke = Brush(Colors.Black), StrokeThickness = 3, WidthRequest = 104, HeightRequest = 104 };
        using var bmp = RenderAt(line, 40, 40);

        // Points on the diagonal (offset by the placement) are inked.
        foreach (var t in new[] { 10, 30, 50, 70, 90 })
            bmp.GetPixel(40 + t, 40 + t).Alpha.Should().BeGreaterThan(128, $"the diagonal at t={t} is inked");

        // Off-diagonal points are not.
        bmp.GetPixel(40 + 80, 40 + 20).Alpha.Should().Be(0);
        bmp.GetPixel(40 + 20, 40 + 80).Alpha.Should().Be(0);
        bmp.GetPixel(10, 10).Alpha.Should().Be(0, "nothing is drawn at the window origin when the line is placed at (40,40)");
    }

    [Fact]
    public void Line_without_stroke_draws_nothing()
    {
        var line = new SkiaLine { X1 = 0, Y1 = 0, X2 = 100, Y2 = 0, StrokeThickness = 3, WidthRequest = 100, HeightRequest = 10 };
        using var bmp = RenderAt(line, 0, 0);
        Enumerable.Range(0, 100).All(x => bmp.GetPixel(x, 1).Alpha == 0).Should().BeTrue();
    }

    // ---- Path -----------------------------------------------------------------

    [Fact]
    public void Path_from_geometry_string_fills_the_triangle()
    {
        var path = new SkiaShapePath { Data = Geometry("M 10 10 L 100 10 L 100 100 Z"), FillColor = Colors.Red, WidthRequest = 110, HeightRequest = 110 };
        using var bmp = RenderAt(path, 0, 0);

        // The triangle occupies the upper-right half of the (10,10)-(100,100) square.
        IsColor(bmp.GetPixel(80, 30), SKColors.Red).Should().BeTrue("inside the triangle");
        bmp.GetPixel(30, 80).Alpha.Should().Be(0, "the lower-left half is outside the triangle");
        bmp.GetPixel(5, 5).Alpha.Should().Be(0);
    }

    [Fact]
    public void Path_stroke_draws_along_the_hypotenuse()
    {
        var path = new SkiaShapePath
        {
            Data = Geometry("M 0 0 L 100 0 L 100 100 Z"),
            StrokeColor = Colors.Blue,
            StrokeThickness = 4,
            WidthRequest = 110,
            HeightRequest = 110,
        };
        using var bmp = RenderAt(path, 0, 0);

        IsColor(bmp.GetPixel(50, 50), SKColors.Blue).Should().BeTrue("hypotenuse passes through (50,50)");
        bmp.GetPixel(60, 40).Alpha.Should().Be(0, "no fill: the interior stays transparent");
    }

    [Fact]
    public void Path_handles_ellipse_rectangle_and_line_geometries()
    {
        foreach (var geometry in new Geometry[]
        {
            new EllipseGeometry(new Point(50, 50), 40, 40),
            new RectangleGeometry(new Rect(10, 10, 80, 80)),
            new LineGeometry(new Point(0, 0), new Point(100, 100)),
        })
        {
            var path = new SkiaShapePath { Data = geometry, FillColor = Colors.Red, StrokeColor = Colors.Red, StrokeThickness = 3, WidthRequest = 100, HeightRequest = 100 };
            using var bmp = RenderAt(path, 0, 0);
            IsColor(bmp.GetPixel(50, 50), SKColors.Red).Should().BeTrue($"{geometry.GetType().Name} covers its centre");
        }
    }

    [Fact]
    public void Path_stroke_dash_array_breaks_the_line()
    {
        var path = new SkiaShapePath
        {
            Data = Geometry("M 0 5 L 150 5"),
            StrokeColor = Colors.Black,
            StrokeThickness = 4,
            StrokeDashArray = new DoubleCollection { 3, 3 },
            WidthRequest = 150,
            HeightRequest = 10,
        };
        using var bmp = RenderAt(path, 0, 0);
        var row = Enumerable.Range(0, 150).Select(x => bmp.GetPixel(x, 5).Alpha > 128).ToList();
        row.Should().Contain(true).And.Contain(false);
    }

    [Fact]
    public void Path_aspect_fill_stretches_the_geometry_to_its_bounds()
    {
        var path = new SkiaShapePath
        {
            Data = Geometry("M 0 0 L 10 0 L 10 10 L 0 10 Z"),
            FillColor = Colors.Red,
            Aspect = Stretch.Fill,
            WidthRequest = 100,
            HeightRequest = 50,
        };
        using var bmp = RenderAt(path, 0, 0);
        IsColor(bmp.GetPixel(95, 45), SKColors.Red).Should().BeTrue("a 10x10 square stretched to 100x50 reaches the far corner");
        IsColor(bmp.GetPixel(5, 5), SKColors.Red).Should().BeTrue();
    }

    [Fact]
    public void Path_aspect_uniform_keeps_the_ratio_and_centres()
    {
        var path = new SkiaShapePath
        {
            Data = Geometry("M 0 0 L 10 0 L 10 10 L 0 10 Z"),
            FillColor = Colors.Red,
            Aspect = Stretch.Uniform,
            WidthRequest = 100,
            HeightRequest = 50,
        };
        using var bmp = RenderAt(path, 0, 0);
        // Uniform scale is 5 -> 50x50 square centred horizontally: x in [25,75].
        IsColor(bmp.GetPixel(50, 25), SKColors.Red).Should().BeTrue();
        bmp.GetPixel(10, 25).Alpha.Should().Be(0, "left of the centred square");
        bmp.GetPixel(90, 25).Alpha.Should().Be(0, "right of the centred square");
    }

    [Fact]
    public void ShapePathHandler_maps_every_path_property_to_the_platform_view()
    {
        var handler = new ShapePathHandler();
        var path = new ShapePath
        {
            Data = Geometry("M 0 0 L 10 0 L 10 10 Z"),
            Fill = Brush(Colors.Red),
            Stroke = Brush(Colors.Blue),
            StrokeThickness = 3,
            StrokeDashArray = new DoubleCollection { 2, 1 },
            StrokeDashOffset = 1,
            StrokeLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Bevel,
            Aspect = Stretch.Uniform,
        };
        handler.SetVirtualView(path);

        var pv = handler.PlatformView;
        pv.Data.Should().BeSameAs(path.Data);
        pv.FillColor.Should().Be(Colors.Red);
        pv.StrokeColor.Should().Be(Colors.Blue);
        pv.StrokeThickness.Should().Be(3);
        pv.StrokeDashArray.Should().BeEquivalentTo(new[] { 2.0, 1.0 });
        pv.StrokeDashOffset.Should().Be(1);
        pv.StrokeLineCap.Should().Be(PenLineCap.Round);
        pv.StrokeLineJoin.Should().Be(PenLineJoin.Bevel);
        pv.Aspect.Should().Be(Stretch.Uniform);

        // Property changes after connection flow through the mapper.
        path.Fill = Brush(Colors.Green);
        pv.FillColor.Should().Be(Colors.Green);
        path.Data = Geometry("M 0 0 L 5 5");
        pv.Data.Should().BeSameAs(path.Data);
    }

    [Fact]
    public void Shape_handlers_map_dash_arrays()
    {
        var dash = new DoubleCollection { 1, 2 };

        var rh = new RectangleHandler(); rh.SetVirtualView(new Microsoft.Maui.Controls.Shapes.Rectangle { StrokeDashArray = dash, StrokeDashOffset = 2 });
        rh.PlatformView.StrokeDashArray.Should().BeSameAs(dash); rh.PlatformView.StrokeDashOffset.Should().Be(2);

        var eh = new EllipseHandler(); eh.SetVirtualView(new Ellipse { StrokeDashArray = dash });
        eh.PlatformView.StrokeDashArray.Should().BeSameAs(dash);

        var lh = new LineHandler(); lh.SetVirtualView(new Line { StrokeDashArray = dash });
        lh.PlatformView.StrokeDashArray.Should().BeSameAs(dash);

        var pgh = new PolygonHandler(); pgh.SetVirtualView(new Polygon { StrokeDashArray = dash });
        pgh.PlatformView.StrokeDashArray.Should().BeSameAs(dash);

        var plh = new PolylineHandler(); plh.SetVirtualView(new Polyline { StrokeDashArray = dash });
        plh.PlatformView.StrokeDashArray.Should().BeSameAs(dash);
    }

    // ---- Polygon / Polyline --------------------------------------------------

    [Fact]
    public void Polygon_fills_inside_its_points_relative_to_its_bounds()
    {
        var polygon = new SkiaPolygon { Points = Points((0, 0), (80, 0), (40, 60)), Fill = Brush(Colors.Purple), StrokeThickness = 0, WidthRequest = 80, HeightRequest = 60 };
        using var bmp = RenderAt(polygon, 50, 50);

        IsColor(bmp.GetPixel(50 + 40, 50 + 20), SKColors.Purple).Should().BeTrue("inside the triangle");
        bmp.GetPixel(50 + 2, 50 + 55).Alpha.Should().Be(0, "bottom-left corner of the bounds is outside the triangle");
        bmp.GetPixel(40, 20).Alpha.Should().Be(0, "nothing at the window origin region");
    }

    [Fact]
    public void Polygon_is_closed_so_the_last_edge_is_stroked()
    {
        var polygon = new SkiaPolygon { Points = Points((0, 0), (100, 0), (100, 100)), Stroke = Brush(Colors.Black), StrokeThickness = 4, WidthRequest = 104, HeightRequest = 104 };
        using var bmp = RenderAt(polygon, 0, 0);
        bmp.GetPixel(50, 50).Alpha.Should().BeGreaterThan(128, "the closing edge (100,100)->(0,0) passes through (50,50)");
    }

    [Fact]
    public void Polyline_is_open_so_the_closing_edge_is_not_drawn()
    {
        var polyline = new SkiaPolyline { Points = Points((0, 0), (100, 0), (100, 100)), Stroke = Brush(Colors.Black), StrokeThickness = 4, WidthRequest = 104, HeightRequest = 104 };
        using var bmp = RenderAt(polyline, 0, 0);
        bmp.GetPixel(50, 50).Alpha.Should().Be(0, "a polyline does not close back to its first point");
        bmp.GetPixel(50, 1).Alpha.Should().BeGreaterThan(128, "the first segment is drawn");
        bmp.GetPixel(99, 50).Alpha.Should().BeGreaterThan(128, "the second segment is drawn");
    }

    [Fact]
    public void Polyline_draws_relative_to_its_bounds()
    {
        var polyline = new SkiaPolyline { Points = Points((0, 5), (60, 5)), Stroke = Brush(Colors.Black), StrokeThickness = 4, WidthRequest = 60, HeightRequest = 10 };
        using var bmp = RenderAt(polyline, 100, 100);
        bmp.GetPixel(130, 105).Alpha.Should().BeGreaterThan(128);
        bmp.GetPixel(30, 5).Alpha.Should().Be(0, "the line must not be drawn at the window origin");
    }

    // ---- GraphicsView --------------------------------------------------------

    private sealed class FillDrawable : IDrawable
    {
        public int DrawCount;
        public Color Color = Colors.Red;
        public RectF LastRect;
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            DrawCount++;
            LastRect = dirtyRect;
            canvas.FillColor = Color;
            canvas.FillRectangle(dirtyRect);
        }
    }

    [Fact]
    public void GraphicsView_invokes_the_drawable_and_shows_its_pixels()
    {
        var drawable = new FillDrawable();
        var view = new SkiaGraphicsView { Drawable = drawable, WidthRequest = 60, HeightRequest = 40 };
        using var bmp = RenderAt(view, 20, 20);

        drawable.DrawCount.Should().BeGreaterThan(0);
        drawable.LastRect.Width.Should().Be(60);
        drawable.LastRect.Height.Should().Be(40);
        IsColor(bmp.GetPixel(50, 40), SKColors.Red).Should().BeTrue("the drawable filled its rect");
        bmp.GetPixel(5, 5).Alpha.Should().Be(0);
    }

    [Fact]
    public void GraphicsView_invalidate_redraws_with_the_new_state()
    {
        var drawable = new FillDrawable();
        var view = new SkiaGraphicsView { Drawable = drawable, WidthRequest = 60, HeightRequest = 40 };
        var root = new SkiaAbsoluteLayout();
        root.AddChild(view);
        root.SetLayoutBounds(view, new SKRect(0, 0, 60, 40));

        using (var first = GoldenHarness.Render(root, 100, 100, 1f, SKColors.Transparent))
            IsColor(first.GetPixel(30, 20), SKColors.Red).Should().BeTrue();

        int before = drawable.DrawCount;
        drawable.Color = Colors.Blue;
        view.Invalidate();

        using var second = GoldenHarness.Render(root, 100, 100, 1f, SKColors.Transparent);
        drawable.DrawCount.Should().BeGreaterThan(before, "Invalidate triggers another Draw");
        IsColor(second.GetPixel(30, 20), SKColors.Blue).Should().BeTrue();
    }

    [Fact]
    public void GraphicsViewHandler_maps_drawable_and_invalidate_command()
    {
        var drawable = new FillDrawable();
        var handler = new GraphicsViewHandler();
        var view = new GraphicsView { Drawable = drawable };
        handler.SetVirtualView(view);

        handler.PlatformView.Drawable.Should().BeSameAs(drawable);
        var act = () => view.Invalidate();
        act.Should().NotThrow();
    }

    // ---- Helpers ------------------------------------------------------------

    private static Geometry Geometry(string data)
        => (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!;

    private static PointCollection Points(params (double X, double Y)[] pts)
    {
        var c = new PointCollection();
        foreach (var (x, y) in pts) c.Add(new Point(x, y));
        return c;
    }
}
