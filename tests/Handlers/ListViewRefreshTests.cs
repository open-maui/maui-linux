// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// MarketAlly.Dialogs' ActionListDialog: a RetainElement ListView of ViewCells
/// whose template is replaced and items refilled (Clear, then Add) at run time.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ListViewRefreshTests
{
    private static DataTemplate Template() => new(() =>
    {
        var label = new Label();
        label.SetBinding(Label.TextProperty, ".");
        return new ViewCell { View = new Grid { Padding = new Thickness(15, 10), Children = { label } } };
    });

    /// <summary>The texts of the rows the list shows (rows are drawn from its item views).</summary>
    private static List<string> ShownTexts(SkiaView view)
    {
        var texts = new List<string>();
        void Walk(SkiaView? v)
        {
            if (v == null || !v.IsVisible) return;
            if (v is SkiaLabel { Text: { Length: > 0 } t }) texts.Add(t);
            var children = v is SkiaLayoutView l ? l.Children : v.Children;
            foreach (var c in children) Walk(c);
        }
        var list = (SkiaCollectionView)view;
        for (int i = 0; i < 3; i++)
            Walk(list.GetItemView(i));
        return texts;
    }

    [Fact]
    public void Items_show_after_the_template_is_replaced_and_the_list_refilled()
    {
        var items = new ObservableCollection<string> { "Analyze", "Copy path", "Open terminal" };
        var list = new ListView(ListViewCachingStrategy.RetainElement)
        {
            ItemsSource = items,
            HasUnevenRows = true,
            ItemTemplate = Template(),
            HeightRequest = 200,
        };

        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true);
        host.Context.Render();
        var skia = (SkiaView)list.Handler!.PlatformView!;
        ShownTexts(skia).Should().Contain(new[] { "Analyze", "Copy path", "Open terminal" });

        // RefreshListView
        list.ItemTemplate = Template();
        list.HasUnevenRows = false;
        list.HasUnevenRows = true;
        var current = items.ToList();
        items.Clear();
        foreach (var item in current) items.Add(item);
        host.Context.Render();

        ShownTexts(skia).Should().Contain(new[] { "Analyze", "Copy path", "Open terminal" });
    }

    [Fact]
    public void Separators_follow_the_ListView_and_a_CollectionView_has_none()
    {
        var list = new ListView { ItemsSource = new[] { "a" }, SeparatorColor = Colors.Red };
        var collection = new CollectionView { ItemsSource = new[] { "a" } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { list, collection } }, withEngine: true);
        host.Context.Render();

        var listView = (SkiaCollectionView)list.Handler!.PlatformView!;
        listView.ShowSeparators.Should().BeTrue();
        listView.SeparatorColorSK.Should().Be(SkiaSharp.SKColors.Red);

        list.SeparatorVisibility = SeparatorVisibility.None;
        listView.ShowSeparators.Should().BeFalse();

        ((SkiaCollectionView)collection.Handler!.PlatformView!).ShowSeparators.Should().BeFalse();
    }

    [Fact]
    public void A_CollectionView_row_is_as_tall_as_its_template()
    {
        // No minimum row height, as on the other platforms: a diff of short
        // lines stays dense instead of spacing each line 44 px apart.
        var collection = new CollectionView
        {
            ItemsSource = new[] { "one", "two", "three" },
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label { FontSize = 12, Padding = new Thickness(0) };
                label.SetBinding(Label.TextProperty, ".");
                return new Grid { Padding = new Thickness(4, 1), Children = { label } };
            }),
            HeightRequest = 200,
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = collection }, withEngine: true);
        host.Context.Render();
        host.Context.Render();

        var first = ((SkiaCollectionView)collection.Handler!.PlatformView!).GetItemView(0)!;
        var second = ((SkiaCollectionView)collection.Handler!.PlatformView!).GetItemView(1)!;
        (second.Bounds.Top - first.Bounds.Top).Should().BeLessThan(30);
    }

    [Fact]
    public void An_empty_CollectionView_in_an_Auto_row_takes_no_room_and_shows_nothing()
    {
        // Commit Quality's issue list: empty, no EmptyView, MaximumHeightRequest.
        var issues = new CollectionView { ItemsSource = new string[0], MaximumHeightRequest = 160 };
        var below = new Label { Text = "Suggested commits" };
        var grid = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
        grid.Add(issues, 0, 0);
        grid.Add(below, 0, 1);
        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true);
        host.Context.Render();

        ((SkiaView)issues.Handler!.PlatformView!).Bounds.Height.Should().BeApproximately(0, 0.5);
        ((SkiaView)below.Handler!.PlatformView!).Bounds.Top.Should().BeApproximately(0, 0.5);
    }

    [Fact]
    public void A_CollectionView_in_an_Auto_row_is_as_tall_as_its_items()
    {
        var list = new CollectionView
        {
            ItemsSource = new[] { "a", "b" },
            ItemTemplate = new DataTemplate(() => { var l = new Label { HeightRequest = 20 }; l.SetBinding(Label.TextProperty, "."); return l; }),
        };
        var grid = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
        grid.Add(list, 0, 0);
        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true);
        host.Context.Render();

        ((SkiaView)list.Handler!.PlatformView!).Bounds.Height.Should().BeApproximately(40, 1);
    }
}
