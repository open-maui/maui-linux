// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using SkiaSharp;
using Syncfusion.Maui.Charts;
using Xunit;
using Colors = Microsoft.Maui.Graphics.Colors;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// Syncfusion Charts (34.2.9, platform-neutral build) through
/// <c>UseLinuxSyncfusion()</c>: every chart type draws its series, data
/// labels and legend, and the interactive behaviours (tooltip, trackball,
/// zoom/pan, selection) answer real pointer and wheel input. Set
/// CHART_FRAMES to a directory to keep each test's frames.
/// </summary>
[Collection(CompatHost.Collection)]
public sealed class SyncfusionChartsCompatTests
{
    private static readonly SKColor Red = new(255, 0, 0);
    private static readonly SKColor Green = new(0, 200, 0);
    private static readonly SKColor Blue = new(0, 0, 255);
    private static readonly SKColor Magenta = new(255, 0, 255);
    private static readonly SKColor Orange = new(255, 140, 0);

    private static SolidColorBrush Brush(SKColor c) => new(Microsoft.Maui.Graphics.Color.FromRgb(c.Red, c.Green, c.Blue));

    private static List<Microsoft.Maui.Controls.Brush> Palette() => new() { Brush(Red), Brush(Green), Brush(Blue), Brush(Magenta), Brush(Orange) };

    public sealed record Sample(string Name, double Value, double High, double Low, double Size);

    public sealed record Ohlc(string Name, double Open, double High, double Low, double Close);

    public sealed record Box(string Name, List<double> Values);

    private static List<Sample> Samples() => new()
    {
        new("A", 3, 5, 1, 2),
        new("B", 5, 7, 2, 4),
        new("C", 2, 4, 1, 3),
        new("D", 4, 6, 2, 1),
    };

    private static CompatHost Host(View chart, int width = 400, int height = 300)
    {
        var host = new CompatHost(new ContentPage { BackgroundColor = Colors.White, Content = chart }, b => b.UseLinuxSyncfusion(), width, height);
        Settle(host);
        return host;
    }

    private static void Settle(CompatHost host)
    {
        for (int i = 0; i < 4; i++)
            host.Render();
    }

    private static void Save(CompatHost host, string name)
    {
        if (Environment.GetEnvironmentVariable("CHART_FRAMES") is { Length: > 0 } dir)
            host.SaveFrame(Path.Combine(dir, name + ".png"));
    }

    private static SfCartesianChart Cartesian(ChartSeries series, bool categoryX = true)
    {
        var chart = new SfCartesianChart();
        chart.XAxes.Add(categoryX ? new CategoryAxis() : new NumericalAxis());
        chart.YAxes.Add(new NumericalAxis());
        chart.Series.Add(series);
        return chart;
    }

    // ---- Cartesian series ----------------------------------------------------

    public static IEnumerable<object[]> CartesianSeries() => new[]
    {
        new object[] { "Area", (Func<ChartSeries>)(() => new AreaSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red) }) },
        new object[] { "Spline", (Func<ChartSeries>)(() => new SplineSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red), StrokeWidth = 4 }) },
        new object[] { "SplineArea", (Func<ChartSeries>)(() => new SplineAreaSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red) }) },
        new object[] { "StepLine", (Func<ChartSeries>)(() => new StepLineSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red), StrokeWidth = 4 }) },
        new object[] { "Column", (Func<ChartSeries>)(() => new ColumnSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red) }) },
        new object[] { "FastLine", (Func<ChartSeries>)(() => new FastLineSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red), StrokeWidth = 4 }) },
        new object[] { "Scatter", (Func<ChartSeries>)(() => new ScatterSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red), PointHeight = 16, PointWidth = 16 }) },
        new object[] { "FastScatter", (Func<ChartSeries>)(() => new FastScatterSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red), PointHeight = 16, PointWidth = 16 }) },
        new object[] { "Bubble", (Func<ChartSeries>)(() => new BubbleSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", SizeValuePath = "Size", Fill = Brush(Red) }) },
        new object[] { "RangeColumn", (Func<ChartSeries>)(() => new RangeColumnSeries { ItemsSource = Samples(), XBindingPath = "Name", High = "High", Low = "Low", Fill = Brush(Red) }) },
        new object[] { "RangeArea", (Func<ChartSeries>)(() => new RangeAreaSeries { ItemsSource = Samples(), XBindingPath = "Name", High = "High", Low = "Low", Fill = Brush(Red) }) },
        new object[] { "StackingArea", (Func<ChartSeries>)(() => new StackingAreaSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red) }) },
        new object[] { "Waterfall", (Func<ChartSeries>)(() => new WaterfallSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red), NegativePointsBrush = Brush(Red), SummaryPointsBrush = Brush(Red) }) },
        new object[] { "Candle", (Func<ChartSeries>)(() => new CandleSeries { ItemsSource = Ohlcs(), XBindingPath = "Name", Open = "Open", High = "High", Low = "Low", Close = "Close", BullishFill = Brush(Red), BearishFill = Brush(Red) }) },
        new object[] { "HiLoOpenClose", (Func<ChartSeries>)(() => new HiLoOpenCloseSeries { ItemsSource = Ohlcs(), XBindingPath = "Name", Open = "Open", High = "High", Low = "Low", Close = "Close", BullishFill = Brush(Red), BearishFill = Brush(Red) }) },
        new object[] { "BoxAndWhisker", (Func<ChartSeries>)(() => new BoxAndWhiskerSeries { ItemsSource = Boxes(), XBindingPath = "Name", YBindingPath = "Values", Fill = Brush(Red) }) },
    };

    private static List<Ohlc> Ohlcs() => new()
    {
        new("A", 2, 5, 1, 4),
        new("B", 4, 6, 2, 3),
        new("C", 3, 7, 2, 6),
    };

    private static List<Box> Boxes() => new()
    {
        new("A", new() { 1, 2, 3, 4, 5, 6, 7 }),
        new("B", new() { 2, 3, 3, 5, 6, 8, 9 }),
    };

    [Theory]
    [MemberData(nameof(CartesianSeries))]
    public void A_cartesian_series_draws(string name, Func<ChartSeries> create)
    {
        using var host = Host(Cartesian(create()));
        Save(host, "cartesian-" + name);

        // Reddish, not only pure red: HiLo bars are 1px antialiased strokes.
        host.CountPixelsNear(Red, host.WindowRect, tolerance: 200).Should().BeGreaterThan(150, $"the {name} series draws");
    }

    [Fact]
    public void A_bar_chart_draws_its_columns_sideways()
    {
        var chart = Cartesian(new ColumnSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red) });
        chart.IsTransposed = true;
        using var host = Host(chart);
        Save(host, "cartesian-Bar");

        host.CountPixelsNear(Red, host.WindowRect).Should().BeGreaterThan(1000);
    }

    [Fact]
    public void A_histogram_draws_its_bins_and_curve()
    {
        var values = new[] { 1.0, 2, 2, 3, 3, 3, 4, 4, 5, 6, 6, 7 }.Select((v, i) => new Sample(i.ToString(), v, 0, 0, 0)).ToList();
        var chart = Cartesian(new HistogramSeries
        {
            ItemsSource = values, XBindingPath = "Value", YBindingPath = "Value", HistogramInterval = 2,
            Fill = Brush(Red), ShowNormalDistributionCurve = true,
            CurveStyle = new ChartLineStyle { Stroke = Brush(Blue), StrokeWidth = 3 },
        }, categoryX: false);
        using var host = Host(chart);
        Save(host, "cartesian-Histogram");

        host.CountPixelsNear(Red, host.WindowRect).Should().BeGreaterThan(1000, "the bins draw");
        host.CountPixelsNear(Blue, host.WindowRect).Should().BeGreaterThan(80, "the normal distribution curve draws");
    }

    [Fact]
    public void An_error_bar_series_draws()
    {
        var chart = Cartesian(new ColumnSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red) });
        chart.Series.Add(new ErrorBarSeries
        {
            ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value",
            Type = ErrorBarType.Fixed, Mode = ErrorBarMode.Vertical, VerticalErrorValue = 1,
            VerticalLineStyle = new ErrorBarLineStyle { Stroke = Brush(Blue), StrokeWidth = 3 },
            VerticalCapLineStyle = new ErrorBarCapLineStyle { Stroke = Brush(Blue), StrokeWidth = 3 },
        });
        using var host = Host(chart);
        Save(host, "cartesian-ErrorBar");

        host.CountPixelsNear(Blue, host.WindowRect).Should().BeGreaterThan(60, "the error bars draw");
    }

    [Fact]
    public void A_fast_line_draws_from_its_first_point()
    {
        // Two points: the whole line is the segment between them.
        var chart = Cartesian(new FastLineSeries
        {
            ItemsSource = new List<Sample> { new("A", 1, 0, 0, 0), new("B", 5, 0, 0, 0) },
            XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red), StrokeWidth = 4,
        });
        using var host = Host(chart);
        Save(host, "cartesian-FastLine-two");

        host.CountPixelsNear(Red, host.WindowRect).Should().BeGreaterThan(300);
    }

    // ---- Data labels and legends --------------------------------------------

    [Fact]
    public void Column_data_labels_draw()
    {
        var series = new ColumnSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red), ShowDataLabels = true };
        series.DataLabelSettings = new CartesianDataLabelSettings
        {
            LabelPlacement = DataLabelPlacement.Outer,
            LabelStyle = new ChartDataLabelStyle { TextColor = Colors.Blue, FontSize = 16 },
        };
        using var host = Host(Cartesian(series));
        Save(host, "labels-Column");

        host.CountPixelsNear(Blue, host.WindowRect).Should().BeGreaterThan(30, "each column carries its value");
    }

    [Fact]
    public void A_legend_lists_the_series()
    {
        var chart = Cartesian(new ColumnSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red), Label = "Revenue" });
        chart.Series.Add(new LineSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "High", Fill = Brush(Blue), Label = "Target" });
        chart.Legend = new ChartLegend { Placement = Syncfusion.Maui.Core.LegendPlacement.Bottom };
        using var host = Host(chart);
        Save(host, "legend-Cartesian");

        var bottom = new SKRectI(0, 250, 400, 300);
        host.CountPixelsNear(Red, bottom).Should().BeGreaterThan(20, "the legend shows the column series' icon");
        host.CountPixelsNot(SKColors.White, bottom).Should().BeGreaterThan(120, "and the series names");
    }

    // ---- Circular, polar, funnel, pyramid -----------------------------------

    public static IEnumerable<object[]> CircularSeries() => new[]
    {
        new object[] { "Pie", (Func<ChartSeries>)(() => new PieSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", PaletteBrushes = Palette(), ShowDataLabels = true }) },
        new object[] { "Doughnut", (Func<ChartSeries>)(() => new DoughnutSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", PaletteBrushes = Palette(), ShowDataLabels = true }) },
        new object[] { "RadialBar", (Func<ChartSeries>)(() => new RadialBarSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", PaletteBrushes = Palette(), ShowDataLabels = true }) },
    };

    [Theory]
    [MemberData(nameof(CircularSeries))]
    public void A_circular_chart_draws_each_slice_and_its_legend(string name, Func<ChartSeries> create)
    {
        var chart = new SfCircularChart { Legend = new ChartLegend() };
        chart.Series.Add(create());
        using var host = Host(chart);
        Save(host, "circular-" + name);

        host.CountPixelsNear(Red, host.WindowRect).Should().BeGreaterThan(200, $"the {name} draws its first slice");
        host.CountPixelsNear(Green, host.WindowRect).Should().BeGreaterThan(200, "and its second");
        host.CountPixelsNear(Blue, host.WindowRect).Should().BeGreaterThan(200, "and its third");
    }

    [Fact]
    public void A_doughnut_leaves_its_hole_empty()
    {
        var chart = new SfCircularChart();
        chart.Series.Add(new DoughnutSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", PaletteBrushes = Palette(), InnerRadius = 0.5 });
        using var host = Host(chart);
        Save(host, "circular-Doughnut-hole");

        host.CountPixelsNot(SKColors.White, new SKRectI(190, 140, 210, 160)).Should().Be(0, "the centre of a doughnut is empty");
    }

    [Fact]
    public void A_polar_chart_draws_its_series()
    {
        var chart = new SfPolarChart { PrimaryAxis = new CategoryAxis(), SecondaryAxis = new NumericalAxis() };
        chart.Series.Add(new PolarAreaSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red) });
        chart.Series.Add(new PolarLineSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "High", Fill = Brush(Blue), StrokeWidth = 3 });
        using var host = Host(chart);
        Save(host, "polar");

        host.CountPixelsNear(Red, host.WindowRect).Should().BeGreaterThan(500, "the area series fills");
        host.CountPixelsNear(Blue, host.WindowRect).Should().BeGreaterThan(100, "the line series draws");
    }

    [Fact]
    public void A_funnel_chart_draws_its_sections_labels_and_legend()
    {
        var chart = new SfFunnelChart
        {
            ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value",
            PaletteBrushes = Palette(), ShowDataLabels = true, Legend = new ChartLegend(),
        };
        using var host = Host(chart);
        Save(host, "funnel");

        host.CountPixelsNear(Red, host.WindowRect).Should().BeGreaterThan(300);
        host.CountPixelsNear(Green, host.WindowRect).Should().BeGreaterThan(300);
        host.CountPixelsNear(Blue, host.WindowRect).Should().BeGreaterThan(300);
    }

    [Fact]
    public void A_pyramid_chart_draws_its_sections_labels_and_legend()
    {
        var chart = new SfPyramidChart
        {
            ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value",
            PaletteBrushes = Palette(), ShowDataLabels = true, Legend = new ChartLegend(),
        };
        using var host = Host(chart);
        Save(host, "pyramid");

        host.CountPixelsNear(Red, host.WindowRect).Should().BeGreaterThan(300);
        host.CountPixelsNear(Green, host.WindowRect).Should().BeGreaterThan(300);
        host.CountPixelsNear(Blue, host.WindowRect).Should().BeGreaterThan(300);
    }

    // ---- Axis labels ------------------------------------------------------

    private static List<bool> LabelVisibility(ChartAxis axis)
    {
        var isVisible = typeof(ChartAxisLabel).GetProperty("IsVisible",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!;
        return axis.VisibleLabels.Select(l => (bool)isVisible.GetValue(l)!).ToList();
    }

    [Fact]
    public void Every_numeric_axis_label_shows_when_there_is_room_for_it()
    {
        // 0..4 in steps of 1: Syncfusion hides a label only where its box
        // (text height plus the 4px label margins) would overlap the next one.
        foreach (var (height, fontSize) in new[] { (250, 12.0), (170, 10.0) })
        {
            var chart = new SfCartesianChart { HeightRequest = height };
            chart.XAxes.Add(new CategoryAxis { LabelStyle = new ChartAxisLabelStyle { FontSize = fontSize } });
            chart.YAxes.Add(new NumericalAxis { Minimum = 0, LabelStyle = new ChartAxisLabelStyle { FontSize = fontSize } });
            chart.Series.Add(new LineSeries
            {
                ItemsSource = new List<Sample> { new("Mon", 1, 0, 0, 0), new("Tue", 4, 0, 0, 0), new("Wed", 2, 0, 0, 0) },
                XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red),
            });
            using var host = new CompatHost(new ContentPage { BackgroundColor = Colors.White, Content = new VerticalStackLayout { chart } }, b => b.UseLinuxSyncfusion(), 400, 300);
            Settle(host);
            Save(host, $"axis-labels-{height}-{fontSize}");

            var yAxis = chart.YAxes[0];
            yAxis.VisibleLabels.Select(l => l.Content?.ToString()).Should().Equal("0", "1", "2", "3", "4");
            LabelVisibility(yAxis).Should().AllBeEquivalentTo(true, $"a {height}px chart has room for {fontSize}pt labels");
        }
    }

    // ---- Interactions ---------------------------------------------------------

    /// <summary>Horizontal extents of the runs of <paramref name="color"/> on the lowest row that has <paramref name="count"/> of them.</summary>
    private static List<(int Start, int End)> Runs(CompatHost host, SKColor color, int count)
    {
        for (int y = host.DisplayWindow.Height - 1; y >= 0; y--)
        {
            var runs = new List<(int, int)>();
            int start = -1;
            for (int x = 0; x <= host.DisplayWindow.Width; x++)
            {
                bool near = x < host.DisplayWindow.Width && host.CountPixelsNear(color, new SKRectI(x, y, x + 1, y + 1), 60) == 1;
                if (near && start < 0) start = x;
                if (!near && start >= 0) { runs.Add((start, x - 1)); start = -1; }
            }
            if (runs.Count == count)
                return runs;
        }
        throw new InvalidOperationException($"no row with {count} runs of {color}");
    }

    private static SfCartesianChart Columns(ChartSeries? extra = null)
    {
        var chart = Cartesian(new ColumnSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red), EnableTooltip = true });
        if (extra != null)
            chart.Series.Add(extra);
        return chart;
    }

    private static void Hover(CompatHost host, float x, float y)
    {
        host.DisplayWindow.RaisePointerMoved(x - 3, y - 3);
        host.DisplayWindow.RaisePointerMoved(x, y);
        Settle(host);
    }

    private static void Click(CompatHost host, float x, float y)
    {
        host.DisplayWindow.RaisePointerMoved(x, y);
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        Settle(host);
    }

    private static void Wheel(CompatHost host, float x, float y, float deltaY)
    {
        var field = host.DisplayWindow.GetType().GetField("Scroll", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var handler = (EventHandler<Microsoft.Maui.Platform.ScrollEventArgs>?)field.GetValue(host.DisplayWindow);
        handler?.Invoke(host.DisplayWindow, new Microsoft.Maui.Platform.ScrollEventArgs(x, y, 0, deltaY));
        Settle(host);
    }

    private static void Drag(CompatHost host, float fromX, float fromY, float toX, float toY)
    {
        host.DisplayWindow.RaisePointerMoved(fromX, fromY);
        host.DisplayWindow.RaisePointerPressed(fromX, fromY);
        for (int i = 1; i <= 8; i++)
            host.DisplayWindow.RaisePointerMoved(fromX + (toX - fromX) * i / 8, fromY + (toY - fromY) * i / 8);
        host.DisplayWindow.RaisePointerReleased(toX, toY);
        Settle(host);
    }

    [Fact]
    public void Hovering_a_column_shows_its_tooltip()
    {
        var chart = Columns();
        chart.TooltipBehavior = new ChartTooltipBehavior { Background = Brush(Blue) };
        using var host = Host(chart);
        host.CountPixelsNear(Blue, host.WindowRect).Should().Be(0);

        var b = Runs(host, Red, 4)[1];
        Hover(host, (b.Start + b.End) / 2f, 200);
        Save(host, "tooltip-hover");

        host.CountPixelsNear(Blue, host.WindowRect).Should().BeGreaterThan(300, "the tooltip shows over the hovered column");
    }

    [Fact]
    public void Clicking_a_column_shows_its_tooltip()
    {
        var chart = Columns();
        chart.TooltipBehavior = new ChartTooltipBehavior { Background = Brush(Blue) };
        using var host = Host(chart);

        var b = Runs(host, Red, 4)[1];
        Click(host, (b.Start + b.End) / 2f, 200);
        Save(host, "tooltip-click");

        host.CountPixelsNear(Blue, host.WindowRect).Should().BeGreaterThan(300);
    }

    [Fact]
    public void Clicking_a_pie_slice_shows_its_tooltip()
    {
        var chart = new SfCircularChart { TooltipBehavior = new ChartTooltipBehavior { Background = Brush(Orange) } };
        chart.Series.Add(new PieSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", PaletteBrushes = Palette(), EnableTooltip = true });
        using var host = Host(chart);
        host.CountPixelsNear(Orange, host.WindowRect).Should().Be(0);

        // The first slice (A, red) starts at 3 o'clock and runs clockwise.
        Click(host, 260, 175);
        Save(host, "tooltip-pie");

        host.CountPixelsNear(Orange, host.WindowRect).Should().BeGreaterThan(150);
    }

    [Fact]
    public void The_trackball_follows_the_mouse()
    {
        var chart = Cartesian(new LineSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red), StrokeWidth = 2 });
        chart.TrackballBehavior = new ChartTrackballBehavior { LineStyle = new ChartLineStyle { Stroke = Brush(Blue), StrokeWidth = 3 } };
        using var host = Host(chart);
        host.CountPixelsNear(Blue, host.WindowRect).Should().Be(0);

        Hover(host, 200, 120);
        Save(host, "trackball");

        host.CountPixelsNear(Blue, host.WindowRect).Should().BeGreaterThan(300, "the trackball line spans the plot");
    }

    [Fact]
    public void The_wheel_zooms_and_a_drag_pans()
    {
        var chart = Columns();
        chart.ZoomPanBehavior = new ChartZoomPanBehavior { EnablePanning = true, ZoomMode = ZoomMode.X };
        using var host = Host(chart);
        var xAxis = chart.XAxes[0];
        xAxis.ZoomFactor.Should().Be(1);

        Wheel(host, 200, 150, -1);
        Wheel(host, 200, 150, -1);
        Save(host, "zoom-wheel");
        xAxis.ZoomFactor.Should().BeLessThan(1, "wheeling up zooms in");
        var zoomed = xAxis.ZoomFactor;
        var position = xAxis.ZoomPosition;

        Drag(host, 250, 150, 150, 150);
        Save(host, "zoom-pan");
        xAxis.ZoomFactor.Should().BeApproximately(zoomed, 1e-9, "a drag pans without zooming");
        xAxis.ZoomPosition.Should().BeGreaterThan(position, "dragging left moves to later values");

        // Each notch steps the zoom level by a quarter; the panned position
        // trims the last step, so one more notch reaches the full range.
        for (int i = 0; i < 3; i++)
            Wheel(host, 200, 150, 1);
        xAxis.ZoomFactor.Should().Be(1, "wheeling down zooms back out");
    }

    [Fact]
    public void Clicking_a_column_selects_it()
    {
        var series = new ColumnSeries
        {
            ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red),
            SelectionBehavior = new DataPointSelectionBehavior { SelectionBrush = Brush(Blue) },
        };
        using var host = Host(Cartesian(series));

        var b = Runs(host, Red, 4)[1];
        Click(host, (b.Start + b.End) / 2f, 200);
        Save(host, "selection-column");

        series.SelectionBehavior.SelectedIndex.Should().Be(1);
        host.CountPixelsNear(Blue, host.WindowRect).Should().BeGreaterThan(3000, "the selected column takes the selection brush");
        Runs(host, Red, 3).Should().HaveCount(3, "the others keep theirs");
    }

    [Fact]
    public void Clicking_a_series_selects_the_series()
    {
        var chart = Columns(new ColumnSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "High", Fill = Brush(Green) });
        chart.SelectionBehavior = new SeriesSelectionBehavior { SelectionBrush = Brush(Blue) };
        using var host = Host(chart);

        var greens = Runs(host, Green, 4);
        Click(host, (greens[0].Start + greens[0].End) / 2f, 220);
        Save(host, "selection-series");

        chart.SelectionBehavior.SelectedIndex.Should().Be(1);
        host.CountPixelsNear(Green, host.WindowRect).Should().BeLessThan(50, "every column of the selected series is selected");
        host.CountPixelsNear(Blue, host.WindowRect).Should().BeGreaterThan(3000);
    }

    [Fact]
    public void Clicking_a_pie_slice_selects_it()
    {
        var series = new PieSeries
        {
            ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", PaletteBrushes = Palette(),
            SelectionBehavior = new DataPointSelectionBehavior { SelectionBrush = Brush(Orange) },
        };
        var chart = new SfCircularChart();
        chart.Series.Add(series);
        using var host = Host(chart);

        Click(host, 260, 175);
        Save(host, "selection-pie");

        series.SelectionBehavior.SelectedIndex.Should().Be(0);
        host.CountPixelsNear(Orange, host.WindowRect).Should().BeGreaterThan(2000);
        host.CountPixelsNear(Red, host.WindowRect).Should().BeLessThan(50);
    }

    [Fact]
    public void Double_clicking_a_zoomed_chart_resets_the_zoom()
    {
        var chart = Columns();
        chart.ZoomPanBehavior = new ChartZoomPanBehavior { EnableDoubleTap = true, ZoomMode = ZoomMode.X };
        using var host = Host(chart);
        Wheel(host, 200, 150, -1);
        chart.XAxes[0].ZoomFactor.Should().BeLessThan(1);

        for (int i = 0; i < 2; i++)
        {
            host.DisplayWindow.RaisePointerPressed(200, 150);
            host.DisplayWindow.RaisePointerReleased(200, 150);
        }
        Settle(host);

        chart.XAxes[0].ZoomFactor.Should().Be(1);
    }

    [Fact]
    public void The_crosshair_follows_the_mouse()
    {
        var chart = Cartesian(new LineSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red) });
        chart.CrosshairBehavior = new ChartCrosshairBehavior
        {
            VerticalLineStyle = new ChartLineStyle { Stroke = Brush(Blue), StrokeWidth = 3 },
            HorizontalLineStyle = new ChartLineStyle { Stroke = Brush(Green), StrokeWidth = 3 },
        };
        using var host = Host(chart);

        Hover(host, 200, 120);
        Save(host, "crosshair");

        host.CountPixelsNear(Blue, host.WindowRect).Should().BeGreaterThan(300, "the vertical line spans the plot");
        host.CountPixelsNear(Green, host.WindowRect).Should().BeGreaterThan(300, "and so does the horizontal one");
    }

    [Fact]
    public void Clicking_a_legend_item_hides_its_series()
    {
        var revenue = new ColumnSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red), Label = "Revenue" };
        var chart = Cartesian(revenue);
        chart.Series.Add(new LineSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "High", Fill = Brush(Blue), Label = "Target" });
        chart.Legend = new ChartLegend { Placement = Syncfusion.Maui.Core.LegendPlacement.Bottom, ToggleSeriesVisibility = true };
        using var host = Host(chart);

        // The legend's first item: the red icon on the bottom row.
        var icon = Runs(host, Red, 1)[0];
        Click(host, (icon.Start + icon.End) / 2f, BottomRowOf(host, Red));
        Save(host, "legend-toggle");

        revenue.IsVisible.Should().BeFalse();
        host.CountPixelsNear(Red, new SKRectI(0, 0, 400, 240)).Should().BeLessThan(20, "the hidden columns are gone");
    }

    private static int BottomRowOf(CompatHost host, SKColor color)
    {
        for (int y = host.DisplayWindow.Height - 1; y >= 0; y--)
            if (host.CountPixelsNear(color, new SKRectI(0, y, host.DisplayWindow.Width, y + 1), 60) > 0)
                return y - 3;
        throw new InvalidOperationException("colour not found");
    }

    [Fact]
    public void An_animated_series_finishes_drawing()
    {
        var chart = Cartesian(new ColumnSeries { ItemsSource = Samples(), XBindingPath = "Name", YBindingPath = "Value", Fill = Brush(Red), EnableAnimation = true });
        using var host = Host(chart);
        // The app's run loop pumps the animation ticker between frames.
        for (int i = 0; i < 40; i++)
        {
            Thread.Sleep(40);
            Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll();
            host.Render();
        }
        Save(host, "animation");

        Runs(host, Red, 4).Should().HaveCount(4);
        host.CountPixelsNear(Red, new SKRectI(0, 0, 400, 40)).Should().BeGreaterThan(0, "the tallest column grows to the top of the plot");
    }

    [Fact]
    public void A_panning_chart_holds_its_touch_until_release()
    {
        var chart = Columns();
        chart.ZoomPanBehavior = new ChartZoomPanBehavior { EnablePanning = true };
        using var host = Host(chart);
        var isHandled = typeof(SfCartesianChart).GetProperty("IsHandled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        host.DisplayWindow.RaisePointerPressed(200, 150);
        host.DisplayWindow.RaisePointerMoved(180, 150);
        ((bool)isHandled.GetValue(chart)!).Should().BeTrue("a pan in progress is the chart's");

        host.DisplayWindow.RaisePointerReleased(180, 150);
        ((bool)isHandled.GetValue(chart)!).Should().BeFalse();
    }
}
