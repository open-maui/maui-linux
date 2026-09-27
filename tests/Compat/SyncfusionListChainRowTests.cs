// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Syncfusion.Maui.Buttons;
using Xunit;
using Xunit.Abstractions;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// An SfListView (DynamicHeight) row holding two side-by-side SfCardViews of label stacks and a
/// price button is as tall as its content. SfCardView measures its content at 200 when its own
/// height is unbounded; the Grid inside reported the whole 200 (its star row's share) instead of
/// what its content needs, as MAUI's grid does, so Strikeline's option chain rows were 200 tall.
/// </summary>
[Collection("LinuxApplication.Current")]
public sealed class SyncfusionListChainRowTests(ITestOutputHelper output)
{
    private static readonly List<Grid> Rows = new();

    private static View Side(string name)
    {
        var grid = new Grid
        {
            Padding = new Thickness(6, 4), ColumnSpacing = 6,
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(50), new ColumnDefinition(50), new ColumnDefinition(90)),
        };
        grid.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children =
        {
            new Label { Text = name, FontSize = 16, FontAttributes = FontAttributes.Bold },
            new Label { Text = "No time decay impact, no volatility sensitivity", FontSize = 14, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 2 },
            new Label { Text = "No time premium impact, no volatility", FontSize = 14, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 2, IsVisible = false },
        } }, 0, 0);
        grid.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { new Label { Text = "0.0%", FontSize = 16, HorizontalOptions = LayoutOptions.Center }, new Label { Text = "IV", FontSize = 12, HorizontalOptions = LayoutOptions.Center } } }, 1, 0);
        grid.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { new Label { Text = "3", FontSize = 16, HorizontalOptions = LayoutOptions.Center }, new Label { Text = "OI", FontSize = 12, HorizontalOptions = LayoutOptions.Center } } }, 2, 0);
        grid.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End, Children =
        {
            new SfButton { Text = "0.01", HeightRequest = 36, WidthRequest = 84, Padding = 4, Margin = 2, CornerRadius = 5, FontSize = 16, StrokeThickness = 1 },
            new Label { Text = "Last: 0.16", FontSize = 13, HorizontalOptions = LayoutOptions.Center },
        } }, 3, 0);
        var card = new Syncfusion.Maui.Cards.SfCardView { CornerRadius = 0, BorderWidth = 0, Padding = 0, Margin = 0,
            IndicatorPosition = Syncfusion.Maui.Cards.CardIndicatorPosition.Left, IndicatorThickness = 4, Content = grid };
        card.SetBinding(VisualElement.IsVisibleProperty, nameof(Strike.HasCall));
        return card;
    }

    public sealed class Strike
    {
        public string Price { get; init; } = "";
        public bool HasCall { get; init; } = true;
    }

    [Fact]
    public void A_two_card_chain_row_hugs_its_content()
    {
        Rows.Clear();
        var list = new Syncfusion.Maui.ListView.SfListView
        {
            ItemsSource = new[] { new Strike { Price = "405" }, new Strike { Price = "400" }, new Strike { Price = "395" } },
            AutoFitMode = Syncfusion.Maui.ListView.AutoFitMode.DynamicHeight,
            SelectionMode = Syncfusion.Maui.ListView.SelectionMode.None,
            ItemTemplate = new DataTemplate(() =>
            {
                var row = new Grid
                {
                    Padding = 2, ColumnSpacing = 4,
                    ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(80), new ColumnDefinition(GridLength.Star)),
                };
                row.Add(Side("Sep 28 Call 405.00"), 0, 0);
                row.Add(new Border { Content = new Label { Text = "405.00", FontSize = 20 } }, 1, 0);
                row.Add(Side("Sep 28 Put 405.00"), 2, 0);
                lock (Rows) Rows.Add(row);
                return row;
            }),
        };
        using var host = new CompatHost(new ContentPage { Content = list }, b => b.UseLinuxSyncfusion(), 1165, 800);
        host.Render();
        host.Render();

        var shown = Rows.Where(r => r.Handler != null && r.Height > 0).ToList();
        shown.Should().NotBeEmpty();
        foreach (var r in shown)
            output.WriteLine($"row {r.Bounds}");
        shown.Should().OnlyContain(r => r.Height < 80, "the tallest content is a 36 px button (+4 margin) over an 18 px label, plus padding");
    }

    [Fact]
    public void A_chain_list_shown_after_its_items_arrive_hugs_its_content()
    {
        // The page fills the list while it is hidden and shows it once a date is picked.
        Rows.Clear();
        var items = new System.Collections.ObjectModel.ObservableCollection<Strike>();
        var list = new Syncfusion.Maui.ListView.SfListView
        {
            IsVisible = false,
            ItemsSource = items,
            AutoFitMode = Syncfusion.Maui.ListView.AutoFitMode.DynamicHeight,
            SelectionMode = Syncfusion.Maui.ListView.SelectionMode.None,
            ItemTemplate = new DataTemplate(() =>
            {
                var row = new Grid
                {
                    Padding = 2, ColumnSpacing = 4,
                    ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(80), new ColumnDefinition(GridLength.Star)),
                };
                row.Add(Side("Sep 28 Call 405.00"), 0, 0);
                row.Add(new Border { Content = new Label { Text = "405.00", FontSize = 20 } }, 1, 0);
                row.Add(Side("Sep 28 Put 405.00"), 2, 0);
                lock (Rows) Rows.Add(row);
                return row;
            }),
        };
        var holder = new Grid { IsVisible = false, Children = { list } };
        using var host = new CompatHost(new ContentPage { Content = new Grid { Children = { holder } } }, b => b.UseLinuxSyncfusion(), 1165, 800);
        host.Render();
        foreach (var i in new[] { "405", "400", "395" }) items.Add(new Strike { Price = i });
        host.Render();
        holder.IsVisible = true;
        list.IsVisible = true;
        for (int i = 0; i < 3; i++) { Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll(); host.Render(); }

        var shown = Rows.Where(r => r.Handler != null && r.Height > 0).ToList();
        shown.Should().NotBeEmpty();
        foreach (var r in shown)
            output.WriteLine($"row {r.Bounds}");
        shown.Should().OnlyContain(r => r.Height < 80);
    }
}
