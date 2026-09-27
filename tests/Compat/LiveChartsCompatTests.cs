// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Maui;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls.Hosting;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// LiveCharts2 (LiveChartsCore.SkiaSharpView.Maui 2.0, generic net10.0 asset,
/// built against SkiaSharp 3 and running on OpenMaui's SkiaSharp 4), registered
/// the documented way (<c>UseSkiaSharp().UseLiveCharts()</c>). Pass = a
/// CartesianChart's series are drawn into the presented frame and data changes
/// redraw it. Pointer interaction (tooltips, pan, zoom) is out of scope: the
/// library's generic-TFM pointer controller is a no-op on every platform
/// without a native one.
/// </summary>
[Collection(CompatHost.Collection)]
public class LiveChartsCompatTests
{
    private static readonly SKColor SeriesBlue = new(0, 0, 255);

    private static CartesianChart Chart(double[] values) => new()
    {
        WidthRequest = 400,
        HeightRequest = 300,
        HorizontalOptions = LayoutOptions.Start,
        VerticalOptions = LayoutOptions.Start,
        EasingFunction = null,
        AnimationsSpeed = TimeSpan.Zero,
        Series = new ISeries[]
        {
            new ColumnSeries<double> { Values = values, Fill = new SolidColorPaint(SeriesBlue), Stroke = null },
        },
    };

    /// <summary>
    /// The chart is created after startup, as in an app (LiveCharts refuses to
    /// construct a chart before <c>UseLiveCharts()</c> has run).
    /// </summary>
    private static CompatHost Host(Func<View> chart)
        => new(_ => new ContentPage { Content = chart(), BackgroundColor = Colors.White },
               b => b.UseSkiaSharp().UseLiveCharts());

    /// <summary>
    /// LiveCharts measures on a background throttler and draws on the next
    /// frame; render until the condition holds (or give up after a timeout).
    /// </summary>
    private static int RenderUntil(CompatHost host, Func<int> probe, Func<int, bool> done, int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int value;
        do
        {
            host.Render();
            value = probe();
            if (done(value))
                return value;
            Thread.Sleep(25);
        }
        while (sw.ElapsedMilliseconds < timeoutMs);
        return value;
    }

    [Fact]
    public void CartesianChart_renders_its_column_series()
    {
        CartesianChart chart = null!;
        using var host = Host(() => chart = Chart(new double[] { 2, 5, 3 }));

        int blue = RenderUntil(host, () => host.CountPixelsNear(SeriesBlue, new SKRectI(0, 0, 400, 300), 60), n => n > 2000);

        chart.Handler.Should().NotBeNull();
        blue.Should().BeGreaterThan(2000, "three blue columns are drawn inside the chart");
        host.CountPixelsNear(SeriesBlue, new SKRectI(400, 0, 800, 600), 60).Should().Be(0, "nothing leaks outside the chart");
        host.CountPixelsNot(SKColors.White, new SKRectI(0, 0, 400, 300)).Should().BeGreaterThan(blue, "axes labels and separators are drawn too");
    }

    [Fact]
    public void CartesianChart_redraws_when_the_data_changes()
    {
        var values = new System.Collections.ObjectModel.ObservableCollection<double> { 1, 1, 1 };
        using var host = Host(() =>
        {
            var chart = Chart(Array.Empty<double>());
            chart.Series = new ISeries[] { new ColumnSeries<double> { Values = values, Fill = new SolidColorPaint(SeriesBlue), Stroke = null } };
            return chart;
        });
        var rect = new SKRectI(0, 0, 400, 300);

        int before = RenderUntil(host, () => host.CountPixelsNear(SeriesBlue, rect, 60), n => n > 500);
        before.Should().BeGreaterThan(500);

        // Same scale (max stays 1): shrinking two columns must remove blue area.
        values[0] = 0.1;
        values[1] = 0.1;
        int after = RenderUntil(host, () => host.CountPixelsNear(SeriesBlue, rect, 60), n => n < before * 0.8);

        after.Should().BeLessThan((int)(before * 0.8), "the observable collection change re-measures and repaints the chart");
    }

    [Fact]
    public void PieChart_renders_its_slices()
    {
        using var host = Host(() => new PieChart
        {
            WidthRequest = 300,
            HeightRequest = 300,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            EasingFunction = null,
            AnimationsSpeed = TimeSpan.Zero,
            Series = new ISeries[]
            {
                new PieSeries<double> { Values = new double[] { 3 }, Fill = new SolidColorPaint(SKColors.Red) },
                new PieSeries<double> { Values = new double[] { 1 }, Fill = new SolidColorPaint(SeriesBlue) },
            },
        });
        var rect = new SKRectI(0, 0, 300, 300);

        int red = RenderUntil(host, () => host.CountPixelsNear(SKColors.Red, rect, 60), n => n > 5000);
        int blue = host.CountPixelsNear(SeriesBlue, rect, 60);

        red.Should().BeGreaterThan(5000);
        blue.Should().BeGreaterThan(1000);
        ((double)red / blue).Should().BeInRange(2.0, 4.5, "slice areas follow the 3:1 values");
    }
}
