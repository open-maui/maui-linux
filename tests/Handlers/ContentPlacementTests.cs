// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls.Shapes;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

[Collection("LinuxApplication.Current")]
public class ContentPlacementTests
{
    [Fact]
    public void An_explicitly_sized_icon_is_centred_in_its_round_button()
    {
        // MarketAlly.Flywheel's scroll arrows: a 16 px chevron, alignment left at Fill, in a 34 px Border.
        var chevron = new Polyline { WidthRequest = 16, HeightRequest = 16, Points = { new(6, 3), new(11, 8), new(6, 13) } };
        var button = new Border { WidthRequest = 34, HeightRequest = 34, Padding = 0, StrokeThickness = 0, Content = chevron,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        using var host = new HeadlessMauiHost(new ContentPage { Content = button }, withEngine: true);
        host.Context.Render();

        var b = ((Microsoft.Maui.Platform.SkiaView)button.Handler!.PlatformView!).Bounds;
        var c = ((Microsoft.Maui.Platform.SkiaView)chevron.Handler!.PlatformView!).Bounds;
        (c.Left - b.Left).Should().BeApproximately(9, 0.5);
        (c.Top - b.Top).Should().BeApproximately(9, 0.5);
        c.Width.Should().BeApproximately(16, 0.5);
    }

    [Fact]
    public void A_two_line_truncating_label_measures_both_lines()
    {
        var label = new Label { FontSize = 13, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 2,
            Text = "Priya sent the revised retainer (12 pages). Scope is the same as Q3, rate up 8%, payment net-15." };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { label } }, withEngine: true);
        host.Context.Render();

        var one = ((IView)label).Measure(double.PositiveInfinity, double.PositiveInfinity).Height;
        var size = ((IView)label).Measure(300, double.PositiveInfinity);
        size.Width.Should().BeLessThanOrEqualTo(300);
        size.Height.Should().BeApproximately(one * 2, 1);
    }

    [Fact]
    public void A_border_measures_its_content_inside_the_contents_margin()
    {
        var label = new Label { Text = "A fairly long line of text that wraps", LineBreakMode = LineBreakMode.WordWrap, Margin = new Thickness(40, 0) };
        var border = new Border { Padding = 0, StrokeThickness = 0, Content = label };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { WidthRequest = 200, Children = { border } } }, withEngine: true);
        host.Context.Render();

        var l = ((Microsoft.Maui.Platform.SkiaView)label.Handler!.PlatformView!).Bounds;
        l.Width.Should().BeApproximately(120, 0.5);
        // Measured at the width it is drawn at, so its wrapped lines all fit its height.
        label.DesiredSize.Width.Should().BeLessThanOrEqualTo(120.5);
    }

    [Fact]
    public void Flex_items_keep_their_margins()
    {
        var a = new Label { Text = "86% confidence", Margin = new Thickness(0, 0, 8, 4) };
        var b = new Label { Text = "Due in 3 d", Margin = new Thickness(0, 0, 8, 4) };
        var flex = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Children = { a, b } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { flex } }, withEngine: true);
        host.Context.Render();

        var ab = ((Microsoft.Maui.Platform.SkiaView)a.Handler!.PlatformView!).Bounds;
        var bb = ((Microsoft.Maui.Platform.SkiaView)b.Handler!.PlatformView!).Bounds;
        bb.Left.Should().BeApproximately(ab.Right + 8, 0.5);
    }

    [Fact]
    public void A_horizontal_scroller_in_an_Auto_row_is_as_tall_as_its_content()
    {
        // InboxRevu's detail pane: tabs in a * row, a toolbar (a horizontal ScrollView
        // around a filling grid) in the Auto row under it.
        var bar = new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = new Grid { Children = { new Button { Text = "Delegate" } } } };
        var tabs = new BoxView();
        var body = new Grid { RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)) };
        body.Add(tabs, 0, 0);
        body.Add(bar, 0, 1);
        using var host = new HeadlessMauiHost(new ContentPage { Content = body }, withEngine: true);
        host.Context.Render();

        bar.Height.Should().BeLessThan(80);
        tabs.Height.Should().BeGreaterThan(500);
    }
}
