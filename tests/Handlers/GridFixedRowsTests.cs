// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls.Shapes;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// Strikeline's flyout header: a Grid with Auto, 1 px and 15 px rows holding a section
/// label, a separator Line and a two-row item (30 px title over 30 px description).
/// </summary>
[Collection("LinuxApplication.Current")]
public class GridFixedRowsTests
{
    private readonly ITestOutputHelper _out;
    public GridFixedRowsTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Fixed_rows_keep_their_height_and_stacked_labels_do_not_overlap()
    {
        var section = new Label { Text = "Alpaca Markets", FontSize = 12, FontAttributes = FontAttributes.Bold, Margin = new Thickness(10, 4, 0, 2) };
        var line = new Line { BackgroundColor = Colors.Gray, HorizontalOptions = LayoutOptions.Fill, Margin = new Thickness(20, 0) };
        var title = new Label { Text = "Demo Account", FontSize = 15, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 4, 0, -4), VerticalTextAlignment = TextAlignment.Center };
        var detail = new Label { Text = "Preview the application", FontSize = 12, FontAttributes = FontAttributes.Italic, Margin = new Thickness(0, -4, 0, 2), VerticalTextAlignment = TextAlignment.Center };
        var item = new Grid { RowSpacing = 0, ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new GridLength(0.2, GridUnitType.Star)), new ColumnDefinition(new GridLength(0.8, GridUnitType.Star))),
            RowDefinitions = new RowDefinitionCollection(new RowDefinition(30), new RowDefinition(30)) };
        item.Add(new BoxView { Margin = 5, HeightRequest = 30 }, 0, 0);
        Grid.SetRowSpan(item.Children[0] as BindableObject, 2);
        item.Add(title, 1, 0);
        item.Add(detail, 1, 1);
        var grid = new Grid { Margin = new Thickness(0, 10), RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(1), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(15)),
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
        grid.Add(section, 0, 0); Grid.SetColumnSpan(section, 2);
        grid.Add(line, 0, 1); Grid.SetColumnSpan(line, 2);
        grid.Add(item, 0, 3); Grid.SetColumnSpan(item, 2);
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { WidthRequest = 260, HorizontalOptions = LayoutOptions.Start, Children = { grid } } }, withEngine: true);
        host.Context.Render();

        Rect B(VisualElement v) => ((Microsoft.Maui.Platform.SkiaView)v.Handler!.PlatformView!).Bounds;
        _out.WriteLine($"section {B(section)} line {B(line)} item {B(item)} title {B(title)} detail {B(detail)}");
        B(line).Height.Should().BeApproximately(1, 0.5, "the 1 px row");
        B(item).Height.Should().BeApproximately(60, 0.5, "two 30 px rows");
        B(title).Height.Should().BeApproximately(30, 0.5);
        B(detail).Top.Should().BeApproximately(B(item).Top + 30 - 4, 0.5, "second row, less its top margin");
        B(item).Top.Should().BeApproximately(B(line).Bottom, 0.5, "the empty Auto row between them is 0 tall");
    }

    [Fact]
    public void A_span_past_the_rows_is_clamped_and_a_grid_without_rows_has_one()
    {
        // Strikeline's sign-up page: a background image with RowSpan 2 and a ScrollView,
        // in a Grid with no RowDefinitions. As in MAUI, both fill the one row.
        var image = new BoxView { Color = Colors.Gray };
        Grid.SetRowSpan(image, 2);
        var scroll = new ScrollView { Content = new BoxView { HeightRequest = 200 } };
        var grid = new Grid { Children = { image, scroll } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true);
        host.Context.Render();

        Rect B(VisualElement v) => ((Microsoft.Maui.Platform.SkiaView)v.Handler!.PlatformView!).Bounds;
        B(scroll).Height.Should().BeApproximately(600, 0.5);
        B(image).Height.Should().BeApproximately(600, 0.5);
    }
}
