// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Xunit;
using Xunit.Abstractions;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// A list row whose price pill spans two Auto rows is as tall as the pill needs
/// (Strikeline's Outlook watchlist: the pill's text was cut off at the bottom).
/// </summary>
[Collection("LinuxApplication.Current")]
public sealed class SyncfusionListSpanningPillTests(ITestOutputHelper output)
{
    private sealed record Quote(string Symbol, string Name, string Change, string Percent, string Close);

    private static readonly List<(Border Pill, Label Price)> Pills = new();

    [Fact]
    public void A_pill_spanning_two_Auto_rows_is_not_clipped()
    {
        Pills.Clear();
        var quotes = new[] { new Quote("AAPL", "Apple Inc. Common Stock", "5.15", "1.53%", "341.46"), new Quote("COST", "Costco Wholesale", "26.29", "2.93%", "922.00") };
        var list = new Syncfusion.Maui.ListView.SfListView
        {
            ItemsSource = quotes,
            AutoFitMode = Syncfusion.Maui.ListView.AutoFitMode.DynamicHeight,
            ItemTemplate = new DataTemplate(() =>
            {
                Label L(string path, double size, Thickness margin)
                {
                    var l = new Label { FontSize = size, Margin = margin, VerticalTextAlignment = TextAlignment.Center };
                    l.SetBinding(Label.TextProperty, path);
                    return l;
                }
                var price = L(nameof(Quote.Close), 16, default);
                price.Padding = new Thickness(0, 2);
                price.HorizontalTextAlignment = TextAlignment.Center;
                var pill = new Border { Padding = 6, Margin = 5, StrokeShape = new RoundRectangle { CornerRadius = 10 }, Stroke = Colors.Transparent,
                    BackgroundColor = Colors.Green, Content = new Grid { Children = { price } } };
                var grid = new Grid
                {
                    Margin = 4, Padding = 2, ColumnSpacing = 4,
                    RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)),
                    ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(80), new ColumnDefinition(110)),
                };
                grid.Add(L(nameof(Quote.Symbol), 18, new Thickness(5, -4, 0, -2)), 0, 0);
                grid.Add(L(nameof(Quote.Name), 15, new Thickness(5, -4, 0, 0)), 0, 1);
                grid.Add(L(nameof(Quote.Change), 16, new Thickness(0, -4, 0, -2)), 1, 0);
                grid.Add(L(nameof(Quote.Percent), 15, new Thickness(0, -4, 0, 0)), 1, 1);
                grid.Add(pill, 2, 0);
                Grid.SetRowSpan(pill, 2);
                lock (Pills) Pills.Add((pill, price));
                return new ContentView { Margin = 4, Padding = 2, Content = grid };
            }),
        };
        using var host = new CompatHost(new ContentPage { Content = list }, b => b.UseLinuxSyncfusion(), 600, 400);
        host.Render();
        host.Render();

        var shown = Pills.Where(p => p.Price.Handler != null && p.Price.Width > 0).ToList();
        shown.Should().NotBeEmpty();
        foreach (var (pill, price) in shown)
        {
            output.WriteLine($"pill {pill.Bounds} price {price.Bounds} desired {price.DesiredSize} grid {((View)pill.Parent).Bounds}");
            price.Height.Should().BeGreaterThanOrEqualTo(price.DesiredSize.Height - 0.5, "the price label gets the height it asked for");
            price.Height.Should().BeGreaterThan(20);
        }
    }
}
