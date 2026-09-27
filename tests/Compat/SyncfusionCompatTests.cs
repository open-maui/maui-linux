// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Syncfusion.Maui.TabView;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// Syncfusion .NET MAUI controls (34.2.9, platform-neutral build) through
/// OpenMaui.Controls.Linux.Syncfusion (<c>UseLinuxSyncfusion()</c>): they lay
/// out, and real pointer input reaches Syncfusion's gesture detectors.
/// </summary>
[Collection(CompatHost.Collection)]
public sealed class SyncfusionCompatTests
{
    private static (SfTabView Tabs, List<SfTabItem> Items) TabView()
    {
        var items = new List<SfTabItem>();
        foreach (var name in new[] { "Recent", "Favorites", "Operations" })
            items.Add(new SfTabItem { Header = name, Content = new Label { Text = name + " page" } });
        var tabs = new SfTabView();
        foreach (var item in items)
            tabs.Items.Add(item);
        return (tabs, items);
    }

    [Fact]
    public void TabView_pages_are_as_wide_as_the_tab_view()
    {
        var (tabs, items) = TabView();
        using var host = new CompatHost(new ContentPage { Content = tabs }, b => b.UseLinuxSyncfusion(), 600, 400);
        host.Render();

        var page = (Label)items[0].Content;
        CompatHost.PlatformOf(page).Bounds.Width.Should().BeLessThanOrEqualTo(600, "a page is the tab view's width, not the screen's");
    }

    [Fact]
    public void Clicking_a_tab_header_selects_it()
    {
        var (tabs, items) = TabView();
        using var host = new CompatHost(new ContentPage { Content = tabs }, b => b.UseLinuxSyncfusion(), 600, 400);
        host.Render();
        tabs.SelectedIndex.Should().Be(0);

        host.Tap(items[1]);
        host.Render();

        tabs.SelectedIndex.Should().Be(1);
    }

    /// <summary>A Syncfusion content view with SfListView's row clip (ListViewItem.ClipRect).</summary>
    private sealed class ClippedRow : Syncfusion.Maui.Core.SfContentView
    {
        internal Microsoft.Maui.Graphics.Rect ClipRect { get; set; }
    }

    [Fact]
    public void A_list_rows_ClipRect_clips_it()
    {
        // SfListView clips each row where a sticky group header covers it; the
        // platform-neutral build leaves applying it to the platform.
        var row = new ClippedRow
        {
            WidthRequest = 200, HeightRequest = 100,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
            Content = new BoxView { Color = Microsoft.Maui.Graphics.Colors.Red },
            ClipRect = new Microsoft.Maui.Graphics.Rect(0, 40, 200, 60),
        };
        using var host = new CompatHost(new ContentPage { BackgroundColor = Microsoft.Maui.Graphics.Colors.White, Content = row }, b => b.UseLinuxSyncfusion(), 400, 300);
        host.Render();

        var red = new SkiaSharp.SKColor(255, 0, 0);
        host.CountPixelsNear(red, new SkiaSharp.SKRectI(10, 5, 190, 35)).Should().Be(0, "the clipped-away top shows nothing");
        host.CountPixelsNear(red, new SkiaSharp.SKRectI(10, 50, 190, 90)).Should().BeGreaterThan(1000, "the rest of the row draws");
    }

    private sealed record Repo(string Name, string Group);

    [Fact]
    public void A_row_under_a_sticky_group_header_is_clipped()
    {
        var repos = Enumerable.Range(1, 40).Select(i => new Repo($"repo{i}", i <= 20 ? "Clean" : "Dirty")).ToList();
        var list = new Syncfusion.Maui.ListView.SfListView
        {
            ItemsSource = repos,
            IsStickyGroupHeader = true,
            ItemSize = 40,
            HeightRequest = 300,
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label();
                label.SetBinding(Label.TextProperty, nameof(Repo.Name));
                return label;
            }),
        };
        list.DataSource!.GroupDescriptors.Add(new Syncfusion.Maui.DataSource.GroupDescriptor { PropertyName = nameof(Repo.Group) });
        using var host = new CompatHost(new ContentPage { Content = list }, b => b.UseLinuxSyncfusion(), 400, 400);
        host.Render();

        list.ScrollTo(60, true);
        host.Render();
        host.Render();

        var clipRect = typeof(Syncfusion.Maui.ListView.ListViewItem).GetProperty("ClipRect",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!;
        var rows = list.GetVisualTreeDescendants().OfType<Syncfusion.Maui.ListView.ListViewItem>().ToList();
        rows.Should().NotBeEmpty();
        rows.Any(r => (Microsoft.Maui.Graphics.Rect)clipRect.GetValue(r)! != Microsoft.Maui.Graphics.Rect.Zero)
            .Should().BeTrue("the row scrolled under the sticky header is clipped there");
    }

    private sealed record Point2(double X, double Y);

    [Fact]
    public void A_cartesian_chart_draws_its_axes_and_series()
    {
        var chart = new Syncfusion.Maui.Charts.SfCartesianChart
        {
            WidthRequest = 300, HeightRequest = 200,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
        };
        chart.XAxes.Add(new Syncfusion.Maui.Charts.NumericalAxis());
        chart.YAxes.Add(new Syncfusion.Maui.Charts.NumericalAxis());
        chart.Series.Add(new Syncfusion.Maui.Charts.LineSeries
        {
            ItemsSource = new[] { new Point2(0, 1), new Point2(1, 3), new Point2(2, 2) },
            XBindingPath = nameof(Point2.X), YBindingPath = nameof(Point2.Y),
            Fill = new SolidColorBrush(Microsoft.Maui.Graphics.Colors.Red), StrokeWidth = 3,
        });
        using var host = new CompatHost(new ContentPage { BackgroundColor = Microsoft.Maui.Graphics.Colors.White, Content = chart }, b => b.UseLinuxSyncfusion(), 400, 300);
        for (int i = 0; i < 4; i++) host.Render();
        if (Environment.GetEnvironmentVariable("CHART_FRAME") is { Length: > 0 } frame) host.SaveFrame(frame);

        var area = new SkiaSharp.SKRectI(0, 0, 300, 200);
        host.CountPixelsNot(SkiaSharp.SKColors.White, area).Should().BeGreaterThan(200, "axes and gridlines draw");
        host.CountPixelsNear(new SkiaSharp.SKColor(255, 0, 0), area).Should().BeGreaterThan(100, "the red line series draws");
    }

    [Fact]
    public void Syncfusion_default_text_sizes_do_not_throw()
    {
        using var host = new CompatHost(new ContentPage(), b => b.UseLinuxSyncfusion(), 200, 200);
        var busy = new Syncfusion.Maui.Core.SfBusyIndicator { Title = "Loading" };
        busy.FontSize.Should().Be(14);
        var helper = typeof(Syncfusion.Maui.Core.SfBusyIndicator).Assembly.GetType("Syncfusion.Maui.Core.TooltipHelper")!;
        var method = helper.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .First(m => m.Name.EndsWith("FontSizeDefaultValueCreator"));
        var instance = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(helper);
        ((double)method.Invoke(instance, null)!).Should().Be(14);
    }

    private sealed record Trend(string Label, double Rate);

    [Fact]
    public void A_chart_whose_data_arrives_later_draws_it()
    {
        // Claude Toolkit's trends: a category axis, data bound after first layout.
        var series = new Syncfusion.Maui.Charts.LineSeries
        {
            XBindingPath = nameof(Trend.Label), YBindingPath = nameof(Trend.Rate),
            Fill = new SolidColorBrush(Microsoft.Maui.Graphics.Colors.Red), StrokeWidth = 3,
        };
        var chart = new Syncfusion.Maui.Charts.SfCartesianChart
        {
            WidthRequest = 300, HeightRequest = 170,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
        };
        chart.XAxes.Add(new Syncfusion.Maui.Charts.CategoryAxis { ShowMajorGridLines = false });
        chart.YAxes.Add(new Syncfusion.Maui.Charts.NumericalAxis { Minimum = 0 });
        chart.Series.Add(series);
        using var host = new CompatHost(new ContentPage { BackgroundColor = Microsoft.Maui.Graphics.Colors.White, Content = new VerticalStackLayout { chart } }, b => b.UseLinuxSyncfusion(), 400, 300);
        for (int i = 0; i < 3; i++) host.Render();

        series.ItemsSource = new[] { new Trend("Mon", 1), new Trend("Tue", 4), new Trend("Wed", 2) };
        for (int i = 0; i < 4; i++) host.Render();
        if (Environment.GetEnvironmentVariable("CHART_FRAME") is { Length: > 0 } frame) host.SaveFrame(frame);

        host.CountPixelsNear(new SkiaSharp.SKColor(255, 0, 0), new SkiaSharp.SKRectI(0, 0, 300, 170)).Should().BeGreaterThan(100, "the late data draws");
    }

    [Fact]
    public void A_chart_away_from_the_window_corner_draws_where_it_is()
    {
        var chart = new Syncfusion.Maui.Charts.SfCartesianChart
        {
            WidthRequest = 300, HeightRequest = 170, Margin = new Thickness(120, 90, 0, 0),
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
        };
        chart.XAxes.Add(new Syncfusion.Maui.Charts.CategoryAxis());
        chart.YAxes.Add(new Syncfusion.Maui.Charts.NumericalAxis { Minimum = 0 });
        chart.Series.Add(new Syncfusion.Maui.Charts.LineSeries
        {
            ItemsSource = new[] { new Trend("Mon", 1), new Trend("Tue", 4), new Trend("Wed", 2) },
            XBindingPath = nameof(Trend.Label), YBindingPath = nameof(Trend.Rate),
            Fill = new SolidColorBrush(Microsoft.Maui.Graphics.Colors.Red), StrokeWidth = 3,
        });
        using var host = new CompatHost(new ContentPage { BackgroundColor = Microsoft.Maui.Graphics.Colors.White, Content = new VerticalStackLayout { chart } }, b => b.UseLinuxSyncfusion(), 500, 400);
        for (int i = 0; i < 4; i++) host.Render();
        if (Environment.GetEnvironmentVariable("CHART_FRAME") is { Length: > 0 } frame) host.SaveFrame(frame);

        var red = new SkiaSharp.SKColor(255, 0, 0);
        host.CountPixelsNear(red, new SkiaSharp.SKRectI(120, 90, 420, 260)).Should().BeGreaterThan(100, "the series draws inside the chart");
        host.CountPixelsNear(red, new SkiaSharp.SKRectI(0, 0, 500, 400)).Should().BeLessThan(
            host.CountPixelsNear(red, new SkiaSharp.SKRectI(120, 90, 420, 260)) + 20, "and nowhere else");
    }

    [Fact]
    public void A_chart_in_a_tab_shown_later_draws()
    {
        // Claude Toolkit's Stats tab: a ScrollView hidden until its tab is picked.
        var chart = new Syncfusion.Maui.Charts.SfCartesianChart { HeightRequest = 170 };
        chart.XAxes.Add(new Syncfusion.Maui.Charts.CategoryAxis());
        chart.YAxes.Add(new Syncfusion.Maui.Charts.NumericalAxis { Minimum = 0 });
        chart.Series.Add(new Syncfusion.Maui.Charts.LineSeries
        {
            ItemsSource = new[] { new Trend("Mon", 1), new Trend("Tue", 4), new Trend("Wed", 2) },
            XBindingPath = nameof(Trend.Label), YBindingPath = nameof(Trend.Rate),
            Fill = new SolidColorBrush(Microsoft.Maui.Graphics.Colors.Red), StrokeWidth = 3,
        });
        var tab = new ScrollView { IsVisible = false, Content = new VerticalStackLayout { new Label { Text = "Trends" }, chart } };
        using var host = new CompatHost(new ContentPage { BackgroundColor = Microsoft.Maui.Graphics.Colors.White, Content = new Grid { Children = { tab } } }, b => b.UseLinuxSyncfusion(), 500, 400);
        for (int i = 0; i < 3; i++) host.Render();

        tab.IsVisible = true;
        for (int i = 0; i < 4; i++) host.Render();
        if (Environment.GetEnvironmentVariable("CHART_FRAME") is { Length: > 0 } frame) host.SaveFrame(frame);

        host.CountPixelsNear(new SkiaSharp.SKColor(255, 0, 0), host.WindowRect).Should().BeGreaterThan(100);
    }

    [Fact]
    public void A_chart_over_an_ObservableCollection_filled_later_draws_it()
    {
        // Claude Toolkit's trends: TrendPoints is cleared and refilled in place.
        var points = new System.Collections.ObjectModel.ObservableCollection<Trend>();
        var chart = new Syncfusion.Maui.Charts.SfCartesianChart { HeightRequest = 170 };
        chart.XAxes.Add(new Syncfusion.Maui.Charts.CategoryAxis());
        chart.YAxes.Add(new Syncfusion.Maui.Charts.NumericalAxis { Minimum = 0 });
        chart.Series.Add(new Syncfusion.Maui.Charts.LineSeries
        {
            ItemsSource = points,
            XBindingPath = nameof(Trend.Label), YBindingPath = nameof(Trend.Rate),
            Fill = new SolidColorBrush(Microsoft.Maui.Graphics.Colors.Red), StrokeWidth = 3,
        });
        using var host = new CompatHost(new ContentPage { BackgroundColor = Microsoft.Maui.Graphics.Colors.White, Content = new VerticalStackLayout { chart } }, b => b.UseLinuxSyncfusion(), 500, 400);
        for (int i = 0; i < 3; i++) host.Render();

        points.Clear();
        points.Add(new Trend("Mon", 1));
        points.Add(new Trend("Tue", 4));
        points.Add(new Trend("Wed", 2));
        for (int i = 0; i < 4; i++) host.Render();
        if (Environment.GetEnvironmentVariable("CHART_FRAME") is { Length: > 0 } frame) host.SaveFrame(frame);

        host.CountPixelsNear(new SkiaSharp.SKColor(255, 0, 0), host.WindowRect).Should().BeGreaterThan(100);
    }

    private sealed record ModelPoint(string Label, double Haiku, double Sonnet, double MistakeRate);

    [Fact]
    public void Claude_Toolkits_trend_charts_draw()
    {
        var points = new System.Collections.ObjectModel.ObservableCollection<ModelPoint>();
        Syncfusion.Maui.Charts.SfCartesianChart Chart(bool legend)
        {
            var chart = new Syncfusion.Maui.Charts.SfCartesianChart { HeightRequest = 170 };
            if (legend) chart.Legend = new Syncfusion.Maui.Charts.ChartLegend();
            chart.XAxes.Add(new Syncfusion.Maui.Charts.CategoryAxis { ShowMajorGridLines = false, LabelStyle = new Syncfusion.Maui.Charts.ChartAxisLabelStyle { FontSize = 10 } });
            chart.YAxes.Add(new Syncfusion.Maui.Charts.NumericalAxis { Minimum = 0, LabelStyle = new Syncfusion.Maui.Charts.ChartAxisLabelStyle { FontSize = 10 } });
            return chart;
        }
        var line = Chart(false);
        line.Series.Add(new Syncfusion.Maui.Charts.LineSeries
        {
            ItemsSource = points, XBindingPath = "Label", YBindingPath = "MistakeRate",
            StrokeWidth = 2, EnableTooltip = true, Fill = new SolidColorBrush(Microsoft.Maui.Graphics.Colors.Red),
        });
        var stacked = Chart(true);
        foreach (var (name, path, color) in new[] { ("haiku", "Haiku", Microsoft.Maui.Graphics.Colors.Lime), ("sonnet", "Sonnet", Microsoft.Maui.Graphics.Colors.Blue) })
            stacked.Series.Add(new Syncfusion.Maui.Charts.StackingColumnSeries
            {
                ItemsSource = points, Label = name, XBindingPath = "Label", YBindingPath = path,
                Fill = new SolidColorBrush(color), EnableTooltip = true, StrokeWidth = 2,
                Stroke = new SolidColorBrush(Microsoft.Maui.Graphics.Color.FromArgb("#212121")),
            });
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 14 };
        grid.Add(new VerticalStackLayout { Spacing = 2, Children = { new Label { Text = "Mistakes" }, line } }, 0, 0);
        grid.Add(new VerticalStackLayout { Spacing = 2, Children = { new Label { Text = "Tokens" }, stacked } }, 1, 0);
        var page = new ContentPage { BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#1E1E1E"), Content = new ScrollView { Content = new VerticalStackLayout { grid } } };
        using var host = new CompatHost(page, b => b.UseLinuxSyncfusion(), 800, 400);
        for (int i = 0; i < 3; i++) host.Render();

        points.Clear();
        foreach (var (l, h, s2, m) in new[] { ("Mon", 5.0, 3.0, 1.0), ("Tue", 2.0, 6.0, 4.0), ("Wed", 4.0, 1.0, 2.0) })
            points.Add(new ModelPoint(l, h, s2, m));
        for (int i = 0; i < 4; i++) host.Render();
        if (Environment.GetEnvironmentVariable("CHART_FRAME") is { Length: > 0 } frame) host.SaveFrame(frame);

        host.CountPixelsNear(new SkiaSharp.SKColor(255, 0, 0), host.WindowRect).Should().BeGreaterThan(100, "the line series draws");
        host.CountPixelsNear(new SkiaSharp.SKColor(0, 255, 0), host.WindowRect).Should().BeGreaterThan(100, "the stacked columns draw");
    }

    [Fact]
    public void An_empty_chart_still_draws_its_axes()
    {
        var chart = new Syncfusion.Maui.Charts.SfCartesianChart { HeightRequest = 170, WidthRequest = 300, HorizontalOptions = LayoutOptions.Start };
        chart.XAxes.Add(new Syncfusion.Maui.Charts.CategoryAxis());
        chart.YAxes.Add(new Syncfusion.Maui.Charts.NumericalAxis { Minimum = 0 });
        chart.Series.Add(new Syncfusion.Maui.Charts.LineSeries { ItemsSource = new System.Collections.ObjectModel.ObservableCollection<Trend>(), XBindingPath = "Label", YBindingPath = "Rate" });
        using var host = new CompatHost(new ContentPage { BackgroundColor = Microsoft.Maui.Graphics.Colors.White, Content = new VerticalStackLayout { chart } }, b => b.UseLinuxSyncfusion(), 400, 300);
        for (int i = 0; i < 4; i++) host.Render();
        if (Environment.GetEnvironmentVariable("CHART_FRAME") is { Length: > 0 } frame) host.SaveFrame(frame);

        host.CountPixelsNot(SkiaSharp.SKColors.White, new SkiaSharp.SKRectI(0, 0, 300, 170)).Should().BeGreaterThan(50, "the axes draw with no data");
    }
}
