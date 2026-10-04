// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// CollectionView, CarouselView and SwipeView behaviour as MAUI defines it on every platform,
/// for the fixes MAUI's own device tests do not cover end to end: a SwipeView row's height, a
/// horizontal list's layout, grouping, an items source that outlives the list, collection
/// changes in a CarouselView, swipe item taps, and handlers connected the MAUI way.
/// </summary>
[Collection("LinuxApplication.Current")]
public class CollectionViewParityTests
{
    private static void Render(HeadlessMauiHost host, int frames = 3)
    {
        for (int i = 0; i < frames; i++)
            host.Context.Render();
    }

    /// <summary>
    /// CiteLynq's notification rows: a SwipeView around a padded two-line Grid. The row was laid
    /// out at the default 44 px (SkiaSwipeView asked for the whole unbounded height a row
    /// measures with), so the second label was cut off.
    /// </summary>
    [Fact]
    public void A_swipe_view_row_is_as_tall_as_its_content()
    {
        Label? second = null;
        var list = new CollectionView
        {
            ItemsSource = new[] { "n1" },
            ItemTemplate = new DataTemplate(() =>
            {
                var grid = new Grid
                {
                    Padding = new Thickness(16, 12),
                    ColumnSpacing = 12,
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
                };
                var dot = new Microsoft.Maui.Controls.Shapes.Ellipse { WidthRequest = 10, HeightRequest = 10, VerticalOptions = LayoutOptions.Center };
                Grid.SetRowSpan(dot, 2);
                grid.Add(dot);
                grid.Add(new Label { Text = "Your Daily Feed is Ready", FontSize = 15, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation }, 1, 0);
                second = new Label { Text = "1 new articles matching your interests", FontSize = 13, LineBreakMode = LineBreakMode.WordWrap, MaxLines = 3 };
                grid.Add(second, 1, 1);
                var age = new Label { Text = "6h", FontSize = 12, VerticalOptions = LayoutOptions.Start };
                Grid.SetRowSpan(age, 2);
                grid.Add(age, 2, 0);
                return new SwipeView
                {
                    RightItems = new SwipeItems { new SwipeItem { Text = "Delete", BackgroundColor = Color.FromArgb("#CC4B41") } },
                    Content = grid,
                };
            }),
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true);
        Render(host);

        var skia = (SkiaCollectionView)list.Handler!.PlatformView!;
        var row = skia.GetItemView(0)!;
        var label = (SkiaView)second!.Handler!.PlatformView!;
        row.Bounds.Height.Should().BeGreaterThan(50, "the row holds the padding and both labels, not the 44 px default");
        label.Bounds.Bottom.Should().BeLessThanOrEqualTo(row.Bounds.Bottom + 0.5, "the second label is inside its row");
        label.Bounds.Height.Should().BeGreaterThan(10, "the second label is laid out at its text height");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ListObserving(ObservableCollection<string> items)
    {
        var list = new SkiaCollectionView { ItemsSource = items };
        return new WeakReference(list);
    }

    /// <summary>
    /// A view model's collection outlives the page that shows it: it must not keep the list (and
    /// with it the handler, the item views and the page) alive, as MAUI's handlers observe it weakly.
    /// </summary>
    [Fact]
    public void An_items_source_does_not_keep_the_list_alive()
    {
        var items = new ObservableCollection<string> { "a", "b" };
        var list = ListObserving(items);

        for (int i = 0; i < 5 && list.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        list.IsAlive.Should().BeFalse("the collection holds no strong reference to the list");
        items.Invoking(c => c.Add("c")).Should().NotThrow("a change after the list is gone is ignored");
    }

    /// <summary>
    /// A horizontal ItemsLayout lays the items out in a row, the list as wide as its items (up to
    /// its room) with its own alignment, as MAUI sizes a CollectionView to its content.
    /// </summary>
    [Fact]
    public void A_horizontal_list_lays_its_items_in_a_row_and_sizes_to_them()
    {
        var items = new ObservableCollection<string> { "a", "b", "c" };
        var list = new CollectionView
        {
            ItemsLayout = LinearItemsLayout.Horizontal,
            HorizontalOptions = LayoutOptions.Start,
            ItemsSource = items,
            ItemTemplate = new DataTemplate(() => new Label { WidthRequest = 50, HeightRequest = 50 }),
        };
        var grid = new Grid { WidthRequest = 500, HeightRequest = 500 };
        grid.Add(list);
        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true);
        Render(host);

        list.Width.Should().BeApproximately(150, 1, "three 50 px items");
        var skia = (SkiaCollectionView)list.Handler!.PlatformView!;
        var first = skia.GetItemView(0)!.Bounds;
        var second = skia.GetItemView(1)!.Bounds;
        second.Left.Should().BeApproximately(first.Right, 0.5, "the second item follows the first in the row");
        second.Top.Should().BeApproximately(first.Top, 0.5);

        for (int i = 3; i < 20; i++)
            items.Add($"item {i}");
        Render(host);
        list.Width.Should().BeApproximately(500, 1, "a list wider than its room takes the room and scrolls");

        var before = skia.GetItemView(0)!.Bounds.Left;
        double reported = 0;
        list.Scrolled += (s, e) => reported = e.HorizontalOffset;
        var b = skia.Bounds;
        skia.OnScroll(new Microsoft.Maui.Platform.ScrollEventArgs((float)b.Center.X, (float)b.Center.Y, 0, 2));
        Render(host);
        skia.GetItemView(0)!.Bounds.Left.Should().BeLessThan(before, "the wheel scrolls a horizontal list sideways");
        reported.Should().BeGreaterThan(0, "Scrolled reports a horizontal offset");
    }

    private sealed class Group : List<string>
    {
        public Group(string name, IEnumerable<string> items) : base(items) => Name = name;
        public string Name { get; }
    }

    /// <summary>
    /// IsGrouped shows each group's header (GroupHeaderTemplate, bound to the group) above its
    /// items; a header is not selectable, and ScrollTo(index, groupIndex) reaches the item.
    /// </summary>
    [Fact]
    public void A_grouped_list_shows_group_headers_and_scrolls_to_an_item_of_a_group()
    {
        var groups = Enumerable.Range(0, 10)
            .Select(g => new Group($"G{g}", Enumerable.Range(0, 5).Select(i => $"G{g}-{i}")))
            .ToList();
        object? selected = null;
        var list = new CollectionView
        {
            IsGrouped = true,
            SelectionMode = SelectionMode.Single,
            ItemsSource = groups,
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label { HeightRequest = 40 };
                label.SetBinding(Label.TextProperty, ".");
                return label;
            }),
            GroupHeaderTemplate = new DataTemplate(() =>
            {
                var label = new Label { HeightRequest = 30 };
                label.SetBinding(Label.TextProperty, nameof(Group.Name));
                return label;
            }),
        };
        list.SelectionChanged += (s, e) => selected = e.CurrentSelection.FirstOrDefault();
        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true);
        Render(host);

        var skia = (SkiaCollectionView)list.Handler!.PlatformView!;
        LabelOf(skia.GetItemView(0)).Should().Be("G0", "the first row is the first group's header");
        LabelOf(skia.GetItemView(1)).Should().Be("G0-0");
        LabelOf(skia.GetItemView(6)).Should().Be("G1", "the next group's header follows the five items");

        var header = skia.GetItemView(0)!.Bounds;
        host.DisplayWindow.RaisePointerPressed((float)header.Center.X, (float)header.Center.Y);
        host.DisplayWindow.RaisePointerReleased((float)header.Center.X, (float)header.Center.Y);
        Render(host);
        selected.Should().BeNull("a group header is not an item");
        list.SelectedItem.Should().BeNull();

        list.ScrollTo(index: 3, groupIndex: 4, position: ScrollToPosition.Start, animate: false);
        Render(host);
        var target = skia.GetItemView(4 * 6 + 1 + 3);
        LabelOf(target).Should().Be("G4-3");
        target!.Bounds.Top.Should().BeApproximately(skia.Bounds.Top, 1, "ScrollTo Start puts the item at the top");
    }

    private static string? LabelOf(SkiaView? view) => (view as SkiaLabel)?.Text;

    /// <summary>
    /// A CarouselView shows items added to its collection after it is shown (MAUI's handlers
    /// observe the collection), and a disconnected one stops observing it.
    /// </summary>
    [Fact]
    public void A_carousel_follows_its_collection_until_disconnected()
    {
        var items = new ObservableCollection<string> { "a", "b" };
        var carousel = new CarouselView
        {
            ItemsSource = items,
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label();
                label.SetBinding(Label.TextProperty, ".");
                return label;
            }),
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = carousel }, withEngine: true);
        Render(host);

        var skia = (SkiaCarouselView)carousel.Handler!.PlatformView!;
        skia.ItemCount.Should().Be(2);
        items.Add("c");
        skia.ItemCount.Should().Be(3, "an item added later is shown");

        carousel.Handler!.DisconnectHandler();
        items.Add("d");
        skia.ItemCount.Should().Be(0, "a disconnected carousel releases its items and no longer follows the collection");
    }

    /// <summary>A tap on an open swipe item invokes the MAUI SwipeItem (its Invoked event and Command).</summary>
    [Fact]
    public void Tapping_a_swipe_item_invokes_it()
    {
        int invoked = 0, executed = 0;
        var delete = new SwipeItem { Text = "Delete", BackgroundColor = Colors.Red, Command = new Command(() => executed++) };
        delete.Invoked += (s, e) => invoked++;
        var swipe = new SwipeView
        {
            RightItems = new SwipeItems { delete },
            Content = new Grid { HeightRequest = 60, Children = { new Label { Text = "row" } } },
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { swipe } }, withEngine: true);
        Render(host);

        delete.Handler.Should().BeOfType<SwipeItemMenuItemHandler>("a swipe item gets its Linux handler");
        swipe.Open(OpenSwipeItem.RightItems, false);
        Render(host);

        var b = ((SkiaView)swipe.Handler!.PlatformView!).Bounds;
        host.DisplayWindow.RaisePointerPressed((float)b.Right - 10, (float)b.Center.Y);
        host.DisplayWindow.RaisePointerReleased((float)b.Right - 10, (float)b.Center.Y);
        Render(host);

        invoked.Should().Be(1, "the item's Invoked event fires");
        executed.Should().Be(1, "the item's Command runs");
    }

    /// <summary>
    /// A handler connected the way MAUI connects one (<c>view.Handler = new XHandler()</c>) gives
    /// its Skia view the MAUI view, as OpenMaui's own factory does, so it gets a frame and Loaded.
    /// </summary>
    [Fact]
    public void A_handler_connected_the_maui_way_knows_its_view()
    {
        using var host = new HeadlessMauiHost(new ContentPage(), withEngine: true);
        var list = new CollectionView();
        var handler = new CollectionViewHandler();
        handler.SetMauiContext(host.MauiContext);
        list.Handler = handler;

        ((SkiaView)handler.PlatformView!).MauiView.Should().BeSameAs(list);
    }
}
