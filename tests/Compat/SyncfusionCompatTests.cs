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
}
