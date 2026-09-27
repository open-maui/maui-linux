// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// The nesting MarketAlly's MAToolbar builds: a ContentView holding a
/// horizontal ScrollView whose content Grid is pinned to the toolbar width
/// (WidthRequest), with a Star column that "AutoSize" items fill.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ToolbarShapeLayoutTests
{
    [Fact]
    public void A_star_column_inside_a_width_pinned_grid_in_a_horizontal_scroll_view_fills_the_rest()
    {
        var fill = new BoxView { Color = Colors.Red, HeightRequest = 20 };
        var fixedItem = new BoxView { Color = Colors.Blue, WidthRequest = 100, HeightRequest = 20 };
        var main = new Grid { HorizontalOptions = LayoutOptions.Fill };
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        main.Add(fill, 0, 0);
        main.Add(fixedItem, 1, 0);

        var pageView = new Grid();
        pageView.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pageView.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        pageView.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pageView.Add(new HorizontalStackLayout(), 0, 0);
        pageView.Add(main, 1, 0);
        pageView.Add(new HorizontalStackLayout(), 2, 0);

        var container = new Grid { HorizontalOptions = LayoutOptions.Fill, WidthRequest = 600 };
        container.Add(pageView);
        var toolbar = new ContentView
        {
            VerticalOptions = LayoutOptions.Start,
            Content = new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = container },
        };

        using var host = new HeadlessMauiHost(new ContentPage { Content = toolbar }, withEngine: true);
        host.Context.Render();

        var fillBounds = ((SkiaView)fill.Handler!.PlatformView!).Bounds;
        var fixedBounds = ((SkiaView)fixedItem.Handler!.PlatformView!).Bounds;
        fixedBounds.Width.Should().Be(100);
        fillBounds.Width.Should().BeApproximately(500, 1, "the Star column takes what the 600-wide grid has left");
        fixedBounds.X.Should().BeApproximately(fillBounds.Right, 1);
    }

    [Fact]
    public void A_star_grid_in_a_horizontal_stack_sizes_to_its_content()
    {
        // MAToolbar's end panel: items built as Grids with a Star text column,
        // measured by a HorizontalStackLayout (unconstrained width).
        var label = new Label { Text = "Update 2.4.4" };
        var item = new Grid();
        item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        item.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        item.Add(new BoxView(), 0, 0);
        item.Add(label, 1, 0);
        var stack = new HorizontalStackLayout { item, new BoxView { WidthRequest = 30 } };

        using var host = new HeadlessMauiHost(new ContentPage { Content = stack }, withEngine: true);
        host.Context.Render();

        var itemBounds = ((SkiaView)item.Handler!.PlatformView!).Bounds;
        var labelBounds = ((SkiaView)label.Handler!.PlatformView!).Bounds;
        itemBounds.Width.Should().BeLessThan(200, "an unconstrained Star column is as wide as its content");
        labelBounds.Width.Should().BeGreaterThan(20);
        itemBounds.Width.Should().BeApproximately(16 + labelBounds.Width, 1);
    }

    /// <summary>Records every height it is measured against.</summary>
    private sealed class HeightProbe : SkiaView
    {
        public List<double> Heights { get; } = new();

        protected override Microsoft.Maui.Graphics.Size MeasureOverride(Microsoft.Maui.Graphics.Size availableSize)
        {
            Heights.Add(availableSize.Height);
            return new Microsoft.Maui.Graphics.Size(10, 10);
        }
    }

    [Fact]
    public void A_star_row_child_is_never_measured_against_infinite_height()
    {
        // A virtualising list realises one item per visible row; offered
        // infinite height it realises them all.
        var grid = new SkiaGrid();
        grid.RowDefinitions.Add(Microsoft.Maui.Platform.GridLength.Auto);
        grid.RowDefinitions.Add(Microsoft.Maui.Platform.GridLength.Star);
        var header = new HeightProbe();
        var list = new HeightProbe();
        grid.AddChild(header, 0, 0);
        grid.AddChild(list, 1, 0);

        grid.Measure(new Microsoft.Maui.Graphics.Size(400, 300));

        list.Heights.Should().NotBeEmpty().And.OnlyContain(h => !double.IsInfinity(h));
        header.Heights.Should().Contain(double.PositiveInfinity, "an Auto row still sizes to its content");
    }

    /// <summary>Sizes itself in MeasureOverride, as SfTabView does.</summary>
    private sealed class SelfSizingView : ContentView
    {
        public List<double> Widths { get; } = new();

        protected override Microsoft.Maui.Graphics.Size MeasureOverride(double widthConstraint, double heightConstraint)
        {
            Widths.Add(widthConstraint);
            base.MeasureOverride(widthConstraint, heightConstraint);
            return new Microsoft.Maui.Graphics.Size(Math.Min(widthConstraint, 120), 33);
        }
    }

    [Fact]
    public void A_custom_MeasureOverride_runs_when_a_skia_parent_measures_the_view()
    {
        var custom = new SelfSizingView { Content = new BoxView(), HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        var grid = new Grid { custom };

        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true);
        host.Context.Render();

        custom.Widths.Should().NotBeEmpty();
        custom.Widths.Should().Contain(w => w > 0 && !double.IsInfinity(w));
        var bounds = ((SkiaView)custom.Handler!.PlatformView!).Bounds;
        bounds.Width.Should().Be(120);
        bounds.Height.Should().Be(33);
    }
}
