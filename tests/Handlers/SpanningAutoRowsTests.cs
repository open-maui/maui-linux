// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls.Shapes;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

[Collection("LinuxApplication.Current")]
public class SpanningAutoRowsTests
{
    [Fact]
    public void A_pill_spanning_two_Auto_rows_gets_the_height_it_needs()
    {
        // Strikeline's watchlist row: symbol and name labels (negative margins) in two Auto
        // rows, and a price pill spanning both.
        var price = new Label { Text = "341.46", FontSize = 16, Padding = new Thickness(0, 2), HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
        var pill = new Border { Padding = 6, Margin = 5, StrokeShape = new RoundRectangle { CornerRadius = 10 }, Stroke = Colors.Transparent, BackgroundColor = Colors.Green,
            Content = new Grid { Children = { price } } };
        var row = new Grid
        {
            Margin = 4, Padding = 2, ColumnSpacing = 4,
            RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)),
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(80)), new ColumnDefinition(new GridLength(110))),
        };
        row.Add(new Label { Text = "AAPL", FontSize = 18, Margin = new Thickness(5, -4, 0, -2) }, 0, 0);
        row.Add(new Label { Text = "Apple Inc.", FontSize = 15, Margin = new Thickness(5, -4, 0, 0) }, 0, 1);
        row.Add(new Label { Text = "5.15", FontSize = 16, Margin = new Thickness(0, -4, 0, -2) }, 2, 0);
        row.Add(new Label { Text = "1.53%", FontSize = 15, Margin = new Thickness(0, -4, 0, 0) }, 2, 1);
        row.Add(pill, 3, 0);
        Grid.SetRowSpan(pill, 2);
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { row } }, withEngine: true);
        host.Context.Render();

        Rect B(View v) => ((Microsoft.Maui.Platform.SkiaView)v.Handler!.PlatformView!).Bounds;
        var p = B(pill); var l = B(price); var r = B(row);
        l.Height.Should().BeGreaterThan(20, $"the price keeps its line: pill {p}, label {l}, row {r}");
        l.Top.Should().BeGreaterThanOrEqualTo(p.Top + 6 - 0.5, $"pill {p}, label {l}, row {r}");
        l.Bottom.Should().BeLessThanOrEqualTo(p.Bottom - 6 + 0.5, $"pill {p}, label {l}, row {r}");
        r.Bottom.Should().BeGreaterThanOrEqualTo(p.Bottom + 5 - 0.5, "the rows grow to hold the pill and its margin");
    }

    [Fact]
    public void A_child_spanning_two_Auto_columns_widens_them_equally()
    {
        var wide = new BoxView { WidthRequest = 200, HeightRequest = 10 };
        var a = new BoxView { WidthRequest = 20, HeightRequest = 10 };
        var b = new BoxView { WidthRequest = 20, HeightRequest = 10 };
        var grid = new Grid
        {
            ColumnSpacing = 10, HorizontalOptions = LayoutOptions.Start,
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)),
            RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)),
        };
        grid.Add(a, 0, 0);
        grid.Add(b, 1, 0);
        grid.Add(wide, 0, 1);
        Grid.SetColumnSpan(wide, 2);
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { grid } }, withEngine: true);
        host.Context.Render();

        // 200 = 20 + 20 + 10 spacing + 150 shared: each column gets 75 more (95 each);
        // the second column starts at 105 and b (20 wide, Fill) is centred in it.
        var g = ((Microsoft.Maui.Platform.SkiaView)grid.Handler!.PlatformView!).Bounds;
        g.Width.Should().BeApproximately(200, 0.5);
        var bb = ((Microsoft.Maui.Platform.SkiaView)b.Handler!.PlatformView!).Bounds;
        (bb.Left - g.Left).Should().BeApproximately(105 + (95 - 20) / 2.0, 0.5);
    }
}
