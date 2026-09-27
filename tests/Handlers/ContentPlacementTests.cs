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

    [Fact]
    public void A_button_in_a_translated_panel_is_clicked_where_it_is_drawn()
    {
        // SfTabView slides a tab's content into view with TranslationX; input followed the
        // untranslated bounds and nothing on a later tab could be clicked.
        int clicks = 0;
        var button = new Button { Text = "Save", WidthRequest = 100, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start };
        button.Clicked += (_, _) => clicks++;
        var panel = new VerticalStackLayout { TranslationX = 200, Children = { button } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = panel }, withEngine: true);
        host.Context.Render();

        host.DisplayWindow.RaisePointerPressed(250, 20, Microsoft.Maui.Platform.PointerButton.Left);
        host.DisplayWindow.RaisePointerReleased(250, 20);
        clicks.Should().Be(1);

        host.DisplayWindow.RaisePointerPressed(50, 20, Microsoft.Maui.Platform.PointerButton.Left);
        host.DisplayWindow.RaisePointerReleased(50, 20);
        clicks.Should().Be(1, "nothing is drawn at the untranslated position");
    }

    [Fact]
    public void A_centred_pill_beside_a_taller_title_hugs_its_text()
    {
        // InboxRevu's VIP badge: Border Padding 6,1, VerticalOptions Center, in the title row.
        var vip = new Label { Text = "VIP", FontSize = 10, FontAttributes = FontAttributes.Bold };
        var pill = new Border { Padding = new Thickness(6, 1), StrokeThickness = 0, VerticalOptions = LayoutOptions.Center, Content = vip,
            StrokeShape = new RoundRectangle { CornerRadius = 4 } };
        var title = new Label { Text = "Harborline: sign the Q4 retainer", FontSize = 14, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)) };
        row.Add(pill, 0, 0);
        row.Add(title, 1, 0);
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { row } }, withEngine: true);
        host.Context.Render();

        var p = ((Microsoft.Maui.Platform.SkiaView)pill.Handler!.PlatformView!).Bounds;
        var v = ((Microsoft.Maui.Platform.SkiaView)vip.Handler!.PlatformView!).Bounds;
        var t = ((Microsoft.Maui.Platform.SkiaView)title.Handler!.PlatformView!).Bounds;
        p.Height.Should().BeApproximately(v.Height + 2, 0.5, "the pill hugs its text");
        (p.Center.Y).Should().BeApproximately(t.Center.Y, 0.5, "centred in the row");
        (v.Center.Y).Should().BeApproximately(p.Center.Y, 0.5);
    }

    [Fact]
    public void Capitals_sit_in_the_middle_of_a_tight_pill()
    {
        var vip = new Label { Text = "VIP", FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Colors.White };
        var pill = new Border { Padding = new Thickness(6, 1), StrokeThickness = 0, BackgroundColor = Colors.Black, Content = vip,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        using var host = new HeadlessMauiHost(new ContentPage { BackgroundColor = Colors.Red, Content = pill }, withEngine: true);
        host.Context.Render();
        host.Context.Render();
        var p = ((Microsoft.Maui.Platform.SkiaView)pill.Handler!.PlatformView!).Bounds;
        var v = ((Microsoft.Maui.Platform.SkiaView)vip.Handler!.PlatformView!).Bounds;
        int top = -1, bottom = -1;
        for (int y = (int)p.Top; y < (int)p.Bottom; y++)
            for (int x = (int)p.Left; x < (int)p.Right; x++)
            {
                var (r, g, b, _) = host.DisplayWindow.PixelAt(x, y);
                if (r > 128 && g > 128) { if (top < 0) top = y; bottom = y; break; }
            }
        // "VIP" (capitals, no descenders): as much room above as below, within a pixel.
        top.Should().BeGreaterThan(0);
        Math.Abs((top - p.Top) - (p.Bottom - 1 - bottom)).Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public void A_one_pixel_separator_in_a_star_row_stays_a_line()
    {
        // MarketAlly.Dialogs' action list: a HeightRequest=1 BoxView lands in the * row.
        var separator = new BoxView { HeightRequest = 1, Color = Colors.Gray };
        var grid = new Grid { HeightRequest = 200, RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)) };
        grid.Add(new Label { Text = "Title" }, 0, 0);
        grid.Add(separator, 0, 1);
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { grid } }, withEngine: true);
        host.Context.Render();

        var b = ((Microsoft.Maui.Platform.SkiaView)separator.Handler!.PlatformView!).Bounds;
        b.Height.Should().BeApproximately(1, 0.1);
    }
}
