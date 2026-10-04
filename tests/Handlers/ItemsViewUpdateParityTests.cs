// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// CollectionView and CarouselView behaviour MAUI defines on its platforms and the device tests
/// do not reach: collection changes applied in place with ItemsUpdatingScrollMode, a horizontal
/// grid, drag to reorder (CanReorderItems, grouped rules), RemainingItemsThreshold and the
/// visible indexes of Scrolled, ItemSizingStrategy, and the CarouselView's EmptyView, scroll
/// bars and position after a change.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ItemsViewUpdateParityTests
{
    private static void Render(HeadlessMauiHost host, int frames = 3)
    {
        for (int i = 0; i < frames; i++)
            host.Context.Render();
    }

    private static DataTemplate Rows(double height = 40) => new(() =>
    {
        var label = new Label { HeightRequest = height };
        label.SetBinding(Label.TextProperty, ".");
        return label;
    });

    private static string? TextOf(SkiaView? view) => (view as SkiaLabel)?.Text;

    private static (CollectionView List, SkiaCollectionView Skia, HeadlessMauiHost Host) Host(CollectionView list)
    {
        var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true, width: 400, height: 400);
        Render(host);
        return (list, (SkiaCollectionView)list.Handler!.PlatformView!, host);
    }

    /// <summary>The label of the row at the top edge of the list.</summary>
    private static string? TopRow(SkiaCollectionView skia, int count)
    {
        var view = skia.GetItemView(skia.GetVisibleItemRange().First);
        view.Should().NotBeNull();
        view!.Bounds.Top.Should().BeApproximately(skia.Bounds.Top, 0.5, "the row starts at the list's top edge");
        return TextOf(view);
    }

    [Fact]
    public void A_change_keeps_the_rows_and_the_items_in_view()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 60).Select(i => $"i{i}"));
        var (list, skia, host) = Host(new CollectionView { ItemsSource = items, ItemTemplate = Rows() });
        using var _ = host;

        list.ScrollTo(20, position: ScrollToPosition.Start, animate: false);
        Render(host);
        TopRow(skia, items.Count).Should().Be("i20");
        var row = skia.GetItemView(20)!;

        items.Add("end");
        Render(host);
        TopRow(skia, items.Count).Should().Be("i20", "an item added below does not move the list");
        skia.GetItemView(20).Should().BeSameAs(row, "the other rows keep their views");

        items.Insert(0, "first");
        Render(host);
        TopRow(skia, items.Count).Should().Be("i20", "KeepItemsInView keeps the first visible item where it is");
        skia.GetItemView(21).Should().BeSameAs(row, "the row moved with its item");

        items.RemoveAt(0);
        items.RemoveAt(0);
        Render(host);
        TopRow(skia, items.Count).Should().Be("i20", "removing items above it does not move it either");
    }

    [Fact]
    public void A_list_at_its_start_shows_an_item_inserted_first()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 30).Select(i => $"i{i}"));
        var (_, skia, host) = Host(new CollectionView { ItemsSource = items, ItemTemplate = Rows() });
        using var _h = host;

        items.Insert(0, "new");
        Render(host);
        TopRow(skia, items.Count).Should().Be("new");
    }

    [Fact]
    public void KeepScrollOffset_keeps_the_offset_and_KeepLastItemInView_follows_the_end()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 60).Select(i => $"i{i}"));
        var (list, skia, host) = Host(new CollectionView
        {
            ItemsSource = items,
            ItemTemplate = Rows(),
            ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepScrollOffset,
        });
        using var _ = host;

        list.ScrollTo(20, position: ScrollToPosition.Start, animate: false);
        Render(host);
        items.Insert(0, "first");
        Render(host);
        TopRow(skia, items.Count).Should().Be("i19", "the offset is kept, so the rows shift under it");

        list.ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepLastItemInView;
        items.Add("last");
        Render(host);
        var last = skia.GetItemView(items.Count - 1);
        TextOf(last).Should().Be("last");
        last!.Bounds.Bottom.Should().BeApproximately(skia.Bounds.Bottom, 1, "the list scrolled to its last item");
    }

    [Fact]
    public void A_selection_survives_changes_but_not_the_removal_of_its_item()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 10).Select(i => $"i{i}"));
        var (list, _, host) = Host(new CollectionView { ItemsSource = items, ItemTemplate = Rows(), SelectionMode = SelectionMode.Single });
        using var _h = host;

        list.SelectedItem = "i3";
        items.Add("more");
        items.Insert(0, "first");
        list.SelectedItem.Should().Be("i3", "adding items keeps the selection");

        items.Remove("i3");
        list.SelectedItem.Should().BeNull("the selected item is gone");
    }

    [Fact]
    public void A_horizontal_grid_fills_its_columns_top_to_bottom()
    {
        var list = new CollectionView
        {
            ItemsLayout = new GridItemsLayout(2, ItemsLayoutOrientation.Horizontal) { HorizontalItemSpacing = 10, VerticalItemSpacing = 4 },
            HeightRequest = 104,
            ItemsSource = Enumerable.Range(0, 30).Select(i => $"i{i}").ToList(),
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label { WidthRequest = 50 };
                label.SetBinding(Label.TextProperty, ".");
                return label;
            }),
        };
        var (_, skia, host) = Host(list);
        using var _h = host;

        var first = skia.GetItemView(0)!.Bounds;
        var second = skia.GetItemView(1)!.Bounds;
        var third = skia.GetItemView(2)!.Bounds;
        second.Left.Should().BeApproximately(first.Left, 0.5, "the second item is under the first, in the same column");
        second.Top.Should().BeGreaterThanOrEqualTo(first.Bottom + 3.5, "rows are VerticalItemSpacing apart");
        third.Top.Should().BeApproximately(first.Top, 0.5, "the third item starts the next column");
        third.Left.Should().BeApproximately(first.Left + 60, 0.5, "columns are HorizontalItemSpacing apart");

        double reported = 0;
        list.Scrolled += (s, e) => reported = e.HorizontalOffset;
        var fifth = skia.GetItemView(4)!.Bounds.Left;
        var b = skia.Bounds;
        skia.OnScroll(new Microsoft.Maui.Platform.ScrollEventArgs((float)b.Center.X, (float)b.Center.Y, 0, 3));
        Render(host);
        reported.Should().BeApproximately(60, 0.5, "the grid scrolls horizontally");
        skia.GetItemView(4)!.Bounds.Left.Should().BeApproximately(fifth - 60, 0.5);
    }

    [Fact]
    public void A_vertical_grid_row_is_its_tallest_item_and_a_tap_selects_the_cell_under_it()
    {
        var list = new CollectionView
        {
            ItemsLayout = new GridItemsLayout(3, ItemsLayoutOrientation.Vertical),
            SelectionMode = SelectionMode.Single,
            ItemsSource = Enumerable.Range(0, 9).ToList(),
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label();
                label.SetBinding(Label.TextProperty, ".");
                label.SetBinding(VisualElement.HeightRequestProperty, new Binding(".", converter: new FuncConverter(v => v is int i && i == 1 ? 80.0 : 30.0)));
                return label;
            }),
        };
        var (_, skia, host) = Host(list);
        using var _h = host;

        var row2 = skia.GetItemView(3)!.Bounds;
        row2.Top.Should().BeApproximately(skia.Bounds.Top + 80, 0.5, "the first row is as tall as its tallest item");

        var cell = skia.GetItemView(5)!.Bounds;
        host.DisplayWindow.RaisePointerPressed((float)cell.Center.X, (float)cell.Center.Y);
        host.DisplayWindow.RaisePointerReleased((float)cell.Center.X, (float)cell.Center.Y);
        list.SelectedItem.Should().Be(5, "the tap lands on the cell drawn under it");
    }

    private sealed class FuncConverter : IValueConverter
    {
        private readonly Func<object?, object?> _convert;
        public FuncConverter(Func<object?, object?> convert) => _convert = convert;
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => _convert(value);
        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }

    private static void Drag(HeadlessMauiHost host, Rect from, Rect to)
    {
        var dw = host.DisplayWindow;
        dw.RaisePointerPressed((float)from.Center.X, (float)from.Center.Y);
        dw.RaisePointerMoved((float)from.Center.X, (float)from.Center.Y + 12);
        Render(host, 1);
        dw.RaisePointerMoved((float)to.Center.X, (float)to.Center.Y);
        Render(host, 1);
        dw.RaisePointerMoved((float)to.Center.X, (float)to.Center.Y + 1);
        Render(host, 1);
        dw.RaisePointerReleased((float)to.Center.X, (float)to.Center.Y + 1);
        Render(host);
    }

    [Fact]
    public void Dragging_an_item_reorders_the_collection_and_raises_ReorderCompleted()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 6).Select(i => $"i{i}"));
        int completed = 0;
        var (list, skia, host) = Host(new CollectionView { ItemsSource = items, ItemTemplate = Rows(), CanReorderItems = true, SelectionMode = SelectionMode.Single });
        using var _ = host;
        list.ReorderCompleted += (s, e) => completed++;

        Drag(host, skia.GetItemView(0)!.Bounds, skia.GetItemView(3)!.Bounds);

        items.Should().Equal("i1", "i2", "i3", "i0", "i4", "i5");
        completed.Should().Be(1);
        list.SelectedItem.Should().BeNull("a drag is not a tap");
        TextOf(skia.GetItemView(3)).Should().Be("i0", "the row shows the moved item");

        list.CanReorderItems = false;
        Drag(host, skia.GetItemView(0)!.Bounds, skia.GetItemView(3)!.Bounds);
        items[0].Should().Be("i1", "a list that cannot reorder scrolls instead");
        completed.Should().Be(1);
    }

    private sealed class Group : ObservableCollection<string>
    {
        public Group(string name, IEnumerable<string> items) : base(items) => Name = name;
        public string Name { get; }
    }

    [Fact]
    public void A_grouped_drag_moves_between_groups_only_when_CanMixGroups()
    {
        var groups = new List<Group>
        {
            new("A", new[] { "a0", "a1", "a2" }),
            new("B", new[] { "b0", "b1", "b2" }),
        };
        var (list, skia, host) = Host(new CollectionView
        {
            IsGrouped = true,
            CanReorderItems = true,
            ItemsSource = groups,
            ItemTemplate = Rows(),
            GroupHeaderTemplate = new DataTemplate(() =>
            {
                var label = new Label { HeightRequest = 30 };
                label.SetBinding(Label.TextProperty, nameof(Group.Name));
                return label;
            }),
        });
        using var _ = host;

        // Rows: A, a0, a1, a2, B, b0, b1, b2.
        Drag(host, skia.GetItemView(1)!.Bounds, skia.GetItemView(6)!.Bounds);
        groups[0].Should().Equal(new[] { "a0", "a1", "a2" }, "CanMixGroups is false: an item stays in its group");
        groups[1].Should().Equal("b0", "b1", "b2");

        Drag(host, skia.GetItemView(1)!.Bounds, skia.GetItemView(3)!.Bounds);
        groups[0].Should().Equal(new[] { "a1", "a2", "a0" }, "an item moves within its group");

        list.CanMixGroups = true;
        Drag(host, skia.GetItemView(1)!.Bounds, skia.GetItemView(6)!.Bounds);
        groups[0].Should().Equal("a2", "a0");
        groups[1].Should().Equal(new[] { "b0", "b1", "a1", "b2" }, "with CanMixGroups an item moves to another group (dropped on b1's row, it takes that place)");

        TextOf(skia.GetItemView(0)).Should().Be("A");
        Drag(host, skia.GetItemView(0)!.Bounds, skia.GetItemView(2)!.Bounds);
        groups[0].Should().Equal(new[] { "a2", "a0" }, "a group header is not dragged");
    }

    [Fact]
    public void Scrolling_reports_the_visible_items_and_RemainingItemsThresholdReached()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 40).Select(i => $"i{i}"));
        int reached = 0;
        ItemsViewScrolledEventArgs? last = null;
        var (list, skia, host) = Host(new CollectionView { ItemsSource = items, ItemTemplate = Rows(), RemainingItemsThreshold = 5 });
        using var _ = host;
        list.RemainingItemsThresholdReached += (s, e) => reached++;
        list.Scrolled += (s, e) => last = e;

        list.ScrollTo(10, position: ScrollToPosition.Start, animate: false);
        last.Should().NotBeNull();
        last!.FirstVisibleItemIndex.Should().Be(10);
        last.LastVisibleItemIndex.Should().Be(19, "ten 40 px rows fill the 400 px list");
        last.CenterItemIndex.Should().Be(15);
        reached.Should().Be(0);

        list.ScrollTo(39, position: ScrollToPosition.End, animate: false);
        last!.LastVisibleItemIndex.Should().Be(39);
        reached.Should().Be(1, "no more than five items are left after the last visible one");
    }

    [Fact]
    public void MeasureFirstItem_gives_every_row_the_first_items_size()
    {
        var list = new CollectionView
        {
            ItemSizingStrategy = ItemSizingStrategy.MeasureFirstItem,
            ItemsSource = new[] { "short", "tall" },
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label();
                label.SetBinding(Label.TextProperty, ".");
                label.SetBinding(VisualElement.HeightRequestProperty, new Binding(".", converter: new FuncConverter(v => (string)v! == "tall" ? 90.0 : 30.0)));
                return label;
            }),
        };
        var (_, skia, host) = Host(list);
        using var _h = host;

        var first = skia.GetItemView(0)!.Bounds;
        var second = skia.GetItemView(1)!.Bounds;
        second.Top.Should().BeApproximately(first.Bottom, 0.5);
        list.ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems;
        Render(host);
        list.Height.Should().BeGreaterThan(110, "measured one by one, the second row is 90 px");
    }

    [Fact]
    public void The_grid_follows_a_change_of_its_span()
    {
        var layout = new GridItemsLayout(2, ItemsLayoutOrientation.Vertical);
        var (list, skia, host) = Host(new CollectionView { ItemsLayout = layout, ItemsSource = Enumerable.Range(0, 9).ToList(), ItemTemplate = Rows() });
        using var _ = host;

        skia.GetItemView(2)!.Bounds.Left.Should().BeApproximately(skia.GetItemView(0)!.Bounds.Left, 0.5);
        layout.Span = 3;
        Render(host);
        skia.GetItemView(3)!.Bounds.Left.Should().BeApproximately(skia.GetItemView(0)!.Bounds.Left, 0.5, "three items per row now");
        skia.GetItemView(2)!.Bounds.Top.Should().BeApproximately(skia.GetItemView(0)!.Bounds.Top, 0.5);
    }

    [Fact]
    public void A_carousel_shows_its_empty_view_and_moves_as_ItemsUpdatingScrollMode_asks()
    {
        var items = new ObservableCollection<string>();
        var carousel = new CarouselView
        {
            ItemsSource = items,
            EmptyView = new Label { Text = "Nothing yet" },
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label();
                label.SetBinding(Label.TextProperty, ".");
                return label;
            }),
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = carousel }, withEngine: true, width: 400, height: 300);
        Render(host);
        var skia = (SkiaCarouselView)carousel.Handler!.PlatformView!;
        var empty = (SkiaView)((Label)carousel.EmptyView).Handler!.PlatformView!;
        skia.EmptyViewContent.Should().BeSameAs(empty, "the EmptyView is shown while there are no items");
        empty.Bounds.Width.Should().BeGreaterThan(0);

        foreach (var s in new[] { "a", "b", "c", "d" })
            items.Add(s);
        Render(host);
        skia.Items[0].Bounds.Height.Should().BeApproximately(skia.Bounds.Height, 0.5, "a CarouselView draws no dots: its item takes its whole height");

        carousel.Position = 2;
        items.Add("e");
        carousel.Position.Should().Be(0, "KeepItemsInView (the default) goes back to the first item");

        carousel.ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepScrollOffset;
        carousel.Position = 2;
        items.Insert(0, "z");
        carousel.Position.Should().Be(3, "KeepScrollOffset stays on the current item");
        carousel.CurrentItem.Should().Be("c");

        carousel.ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepLastItemInView;
        items.Add("f");
        carousel.Position.Should().Be(items.Count - 1);
        carousel.CurrentItem.Should().Be("f");

        carousel.HorizontalScrollBarVisibility = ScrollBarVisibility.Always;
        skia.HorizontalScrollBarVisibility.Should().Be(Microsoft.Maui.Platform.ScrollBarVisibility.Always);
    }

    private static CarouselView Carousel(IEnumerable<string> items) => new()
    {
        ItemsSource = items.ToList(),
        ItemTemplate = new DataTemplate(() =>
        {
            var label = new Label();
            label.SetBinding(Label.TextProperty, ".");
            return label;
        }),
    };

    [Fact]
    public void A_vertical_carousel_moves_its_items_up_and_down()
    {
        var carousel = Carousel(new[] { "a", "b", "c" });
        carousel.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 10 };
        using var host = new HeadlessMauiHost(new ContentPage { Content = carousel }, withEngine: true, width: 400, height: 300);
        Render(host);
        var skia = (SkiaCarouselView)carousel.Handler!.PlatformView!;

        skia.Items[1].Bounds.Top.Should().BeApproximately(skia.Items[0].Bounds.Bottom + 10, 0.5, "the next item is below, ItemSpacing apart");
        skia.Items[0].Bounds.Width.Should().BeApproximately(skia.Bounds.Width, 0.5);

        var b = skia.Bounds;
        var dw = host.DisplayWindow;
        dw.RaisePointerPressed((float)b.Center.X, (float)b.Center.Y + 50);
        dw.RaisePointerMoved((float)b.Center.X, (float)b.Center.Y + 20);
        dw.RaisePointerMoved((float)b.Center.X, (float)b.Center.Y - 50);
        dw.RaisePointerReleased((float)b.Center.X, (float)b.Center.Y - 50);
        Render(host);
        carousel.Position.Should().Be(1, "dragging up moves to the next item");
        carousel.CurrentItem.Should().Be("b");
    }

    [Fact]
    public void A_looping_carousel_goes_from_its_last_item_to_its_first()
    {
        var carousel = Carousel(new[] { "a", "b", "c" });
        carousel.Loop = true;
        using var host = new HeadlessMauiHost(new ContentPage { Content = carousel }, withEngine: true, width: 400, height: 300);
        Render(host);
        var skia = (SkiaCarouselView)carousel.Handler!.PlatformView!;
        carousel.ScrollTo(2, position: ScrollToPosition.Center, animate: false);
        Render(host);
        carousel.Position.Should().Be(2);

        var b = skia.Bounds;
        var dw = host.DisplayWindow;
        dw.RaisePointerPressed((float)b.Center.X + 100, (float)b.Center.Y);
        dw.RaisePointerMoved((float)b.Center.X + 50, (float)b.Center.Y);
        dw.RaisePointerMoved((float)b.Center.X - 50, (float)b.Center.Y);
        Render(host, 1);
        skia.Items[0].Bounds.Left.Should().BeApproximately(b.Right - 150, 0.5, "the first item follows the last one while dragging");
        dw.RaisePointerReleased((float)b.Center.X - 50, (float)b.Center.Y);
        Render(host);
        carousel.Position.Should().Be(0, "past the last item comes the first");
        carousel.CurrentItem.Should().Be("a");
    }
}
