// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

#pragma warning disable CS0618 // ListView is deprecated but still supported on Linux

/// <summary>
/// Items-view behaviour Windows defines and the device tests do not reach: a touch reorder
/// drag that waits for a press-and-hold (a mouse drag starts at once), the CarouselView's
/// IsBounceEnabled, snap points and per-side PeekAreaInsets reaching the platform view, and the
/// classic ListView following changes of its items in place (rows kept, scroll position and
/// selection kept) in both caching strategies and grouped.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ItemsViewGestureParityTests
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

    private static (CollectionView List, SkiaCollectionView Skia, HeadlessMauiHost Host) Host(CollectionView list)
    {
        var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true, width: 400, height: 400);
        Render(host);
        return (list, (SkiaCollectionView)list.Handler!.PlatformView!, host);
    }

    // --- Reorder: touch waits for a press-and-hold ----------------------------------------

    private sealed class TestClock
    {
        public DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    }

    private static (ObservableCollection<string> Items, CollectionView List, SkiaCollectionView Skia, HeadlessMauiHost Host, TestClock Clock) Reorderable()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 6).Select(i => $"i{i}"));
        var (list, skia, host) = Host(new CollectionView { ItemsSource = items, ItemTemplate = Rows(), CanReorderItems = true });
        var clock = new TestClock();
        skia.ReorderClock = () => clock.Now;
        return (items, list, skia, host, clock);
    }

    [Fact]
    public void A_mouse_reorder_drag_starts_as_soon_as_the_pointer_moves()
    {
        var (items, _, skia, host, _) = Reorderable();
        using var _h = host;
        var from = skia.GetItemView(0)!.Bounds;
        var to = skia.GetItemView(3)!.Bounds;
        var dw = host.DisplayWindow;

        dw.RaisePointerPressed((float)from.Center.X, (float)from.Center.Y);
        dw.RaisePointerMoved((float)from.Center.X, (float)from.Center.Y + 12);
        skia.IsReordering.Should().BeTrue("a mouse drag needs no hold");
        Render(host, 1);
        dw.RaisePointerMoved((float)to.Center.X, (float)to.Center.Y);
        Render(host, 1);
        dw.RaisePointerMoved((float)to.Center.X, (float)to.Center.Y + 1);
        Render(host, 1);
        dw.RaisePointerReleased((float)to.Center.X, (float)to.Center.Y + 1);
        Render(host);

        items.Should().Equal("i1", "i2", "i3", "i0", "i4", "i5");
    }

    [Fact]
    public void A_finger_that_moves_at_once_pans_instead_of_reordering()
    {
        var (items, _, skia, host, _) = Reorderable();
        using var _h = host;
        var from = skia.GetItemView(0)!.Bounds;
        var to = skia.GetItemView(3)!.Bounds;
        var dw = host.DisplayWindow;

        dw.RaiseTouchPressed((float)from.Center.X, (float)from.Center.Y);
        dw.RaiseTouchMoved((float)from.Center.X, (float)from.Center.Y + 12);
        skia.IsReordering.Should().BeFalse("on Windows a touch reorder starts with a press-and-hold");
        Render(host, 1);
        dw.RaiseTouchMoved((float)to.Center.X, (float)to.Center.Y);
        Render(host, 1);
        dw.RaiseTouchReleased((float)to.Center.X, (float)to.Center.Y);
        Render(host);

        skia.IsReordering.Should().BeFalse();
        items.Should().Equal("i0", "i1", "i2", "i3", "i4", "i5");
    }

    [Fact]
    public void A_finger_held_on_an_item_drags_it_to_a_new_place()
    {
        var (items, _, skia, host, clock) = Reorderable();
        using var _h = host;
        var from = skia.GetItemView(0)!.Bounds;
        var to = skia.GetItemView(3)!.Bounds;
        var dw = host.DisplayWindow;

        dw.RaiseTouchPressed((float)from.Center.X, (float)from.Center.Y);
        // A finger wobbles a little while it rests: within the tolerance it is still a hold.
        dw.RaiseTouchMoved((float)from.Center.X + 2, (float)from.Center.Y + 3);
        clock.Now += SkiaItemsView.TouchReorderHoldDelay + TimeSpan.FromMilliseconds(50);
        dw.RaiseTouchMoved((float)from.Center.X, (float)from.Center.Y + 12);
        skia.IsReordering.Should().BeTrue("the hold is over: the item follows the finger");
        Render(host, 1);
        dw.RaiseTouchMoved((float)to.Center.X, (float)to.Center.Y);
        Render(host, 1);
        dw.RaiseTouchMoved((float)to.Center.X, (float)to.Center.Y + 1);
        Render(host, 1);
        dw.RaiseTouchReleased((float)to.Center.X, (float)to.Center.Y + 1);
        Render(host);

        items.Should().Equal("i1", "i2", "i3", "i0", "i4", "i5");
    }

    [Fact]
    public void Pointer_events_say_which_device_sent_them()
    {
        new Microsoft.Maui.Platform.PointerEventArgs(1, 2).PointerDeviceType.Should().Be(PointerDeviceKind.Mouse, "the default");

        var (_, skia, host) = Host(new CollectionView { ItemsSource = new[] { "a" }, ItemTemplate = Rows() });
        using var _h = host;
        var seen = new List<PointerDeviceKind>();
        var rowView = skia.GetItemView(0)!;
        // The row's content gets the list's pointer events, as on Windows (PointerRouted).
        rowView.PointerRouted += (s, e) => seen.Add(e.Pointer.PointerDeviceType);
        var row = rowView.Bounds;

        host.DisplayWindow.RaiseTouchPressed((float)row.Center.X, (float)row.Center.Y);
        host.DisplayWindow.RaiseTouchReleased((float)row.Center.X, (float)row.Center.Y);

        seen.Should().NotBeEmpty().And.OnlyContain(k => k == PointerDeviceKind.Touch, "the routing keeps the device of the event");
    }

    // --- CarouselView: bounce, snap points, peek -------------------------------------------

    private static CarouselView Carousel(params string[] items) => new()
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
    public void A_carousel_takes_its_bounce_snap_points_and_peek_sides()
    {
        var carousel = Carousel("a", "b", "c");
        using var host = new HeadlessMauiHost(new ContentPage { Content = carousel }, withEngine: true, width: 400, height: 300);
        Render(host);
        var skia = (SkiaCarouselView)carousel.Handler!.PlatformView!;

        skia.IsBounceEnabled.Should().BeTrue("CarouselView bounces by default");
        skia.SnapPointsType.Should().Be(SnapPointsType.MandatorySingle, "a CarouselView's default layout");
        skia.SnapPointsAlignment.Should().Be(SnapPointsAlignment.Center);

        carousel.IsBounceEnabled = false;
        skia.IsBounceEnabled.Should().BeFalse();

        var layout = new LinearItemsLayout(ItemsLayoutOrientation.Horizontal)
        {
            SnapPointsType = SnapPointsType.Mandatory,
            SnapPointsAlignment = SnapPointsAlignment.Start,
        };
        carousel.ItemsLayout = layout;
        skia.SnapPointsType.Should().Be(SnapPointsType.Mandatory);
        skia.SnapPointsAlignment.Should().Be(SnapPointsAlignment.Start);
        layout.SnapPointsAlignment = SnapPointsAlignment.End;
        layout.SnapPointsType = SnapPointsType.None;
        skia.SnapPointsAlignment.Should().Be(SnapPointsAlignment.End, "a change of the layout is followed");
        skia.SnapPointsType.Should().Be(SnapPointsType.None);

        carousel.PeekAreaInsets = new Thickness(40, 0, 10, 0);
        skia.LeadingPeekAreaInset.Should().Be(40);
        skia.TrailingPeekAreaInset.Should().Be(10, "the right side is its own, no longer the left one again");
        Render(host);
        skia.Items[0].Bounds.Width.Should().BeApproximately(350, 0.5);

        carousel.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical);
        carousel.PeekAreaInsets = new Thickness(0, 30, 0, 5);
        skia.LeadingPeekAreaInset.Should().Be(30, "a vertical carousel peeks at its top");
        skia.TrailingPeekAreaInset.Should().Be(5, "and at its bottom");
    }

    [Fact]
    public void A_looping_carousel_of_two_items_shows_both_neighbours()
    {
        var carousel = Carousel("a", "b");
        carousel.Loop = true;
        carousel.PeekAreaInsets = new Thickness(40, 0, 40, 0);
        using var host = new HeadlessMauiHost(new ContentPage { Content = carousel }, withEngine: true, width: 400, height: 300);
        Render(host);
        var skia = (SkiaCarouselView)carousel.Handler!.PlatformView!;

        var placements = skia.VisibleItemPlacements();
        placements.Count(p => p.Index == 1).Should().Be(2, "the other item peeks in on both sides");
        placements.Should().Contain(p => p.Index == 1 && p.Start < 0);
        placements.Should().Contain(p => p.Index == 1 && p.Start > 300);
    }

    // --- ListView: changes in place ------------------------------------------------------

    private static (ListView List, SkiaCollectionView Skia, ListViewHandler Handler, HeadlessMauiHost Host) HostList(ListView list)
    {
        var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true, width: 400, height: 400);
        Render(host);
        return (list, (SkiaCollectionView)list.Handler!.PlatformView!, (ListViewHandler)list.Handler!, host);
    }

    private static string? RowText(SkiaCollectionView skia, int index) => (skia.GetItemView(index) as SkiaCellView)?.Text;

    private static int FirstShown(SkiaCollectionView skia) => skia.GetVisibleItemRange().First;

    private static void ScrollToRow(SkiaCollectionView skia, HeadlessMauiHost host, int index)
    {
        skia.ScrollToIndex(index, Microsoft.Maui.Controls.ScrollToPosition.Start, animate: false);
        Render(host);
    }

    private sealed class ResettableCollection<T> : ObservableCollection<T>
    {
        public ResettableCollection(IEnumerable<T> items) : base(items) { }
        public void RaiseReset() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    [Theory]
    [InlineData(ListViewCachingStrategy.RetainElement)]
    [InlineData(ListViewCachingStrategy.RecycleElement)]
    public void A_ListView_change_keeps_the_rows_the_scroll_position_and_the_selection(ListViewCachingStrategy strategy)
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 60).Select(i => $"i{i}"));
        var (list, skia, handler, host) = HostList(new ListView(strategy) { ItemsSource = items, RowHeight = 40 });
        using var _h = host;

        ScrollToRow(skia, host, 20);
        FirstShown(skia).Should().Be(20);
        list.SelectedItem = "i22";
        var row20 = skia.GetItemView(20);
        var row22 = skia.GetItemView(22);
        row20.Should().NotBeNull();

        items.Add("end");
        Render(host);
        FirstShown(skia).Should().Be(20, "an item added below does not move the list");
        skia.GetItemView(20).Should().BeSameAs(row20, "the other rows keep their cells and views");

        items.Insert(0, "first");
        Render(host);
        FirstShown(skia).Should().Be(21, "the first row shown stays in view, now one further down");
        RowText(skia, 21).Should().Be("i20");
        skia.GetItemView(21).Should().BeSameAs(row20, "the row moved with its item");
        skia.GetItemView(23).Should().BeSameAs(row22);
        handler.Rows[21].Index.Should().Be(21, "the row knows its new place (taps report it)");
        list.SelectedItem.Should().Be("i22");
        (skia.SelectedItem as ListViewHandler.Row)?.Item.Should().Be("i22", "the selection stays on its item");

        items.RemoveAt(0);
        items.RemoveAt(0);
        Render(host);
        FirstShown(skia).Should().Be(19);
        skia.GetItemView(19).Should().BeSameAs(row20);

        items[25] = "replaced";
        Render(host);
        RowText(skia, 25).Should().Be("replaced");
        skia.GetItemView(19).Should().BeSameAs(row20, "a replaced item remakes only its row");

        items.Move(items.Count - 1, 0);
        Render(host);
        RowText(skia, FirstShown(skia)).Should().Be("i20", "a move keeps the items in view");
        skia.GetItemView(FirstShown(skia)).Should().BeSameAs(row20);
        (skia.SelectedItem as ListViewHandler.Row)?.Item.Should().Be("i22");
    }

    [Fact]
    public void A_ListView_reset_keeps_the_scroll_offset()
    {
        var items = new ResettableCollection<string>(Enumerable.Range(0, 60).Select(i => $"i{i}"));
        var (_, skia, _, host) = HostList(new ListView { ItemsSource = items, RowHeight = 40 });
        using var _h = host;

        ScrollToRow(skia, host, 20);
        items.RaiseReset();
        Render(host);

        FirstShown(skia).Should().Be(20, "a reset makes the rows again but keeps the offset, as Windows' ListView does");
        RowText(skia, 20).Should().Be("i20");
    }

    private sealed class Group : ObservableCollection<string>
    {
        public Group(string name, IEnumerable<string> items) : base(items) => Name = name;
        public string Name { get; }
        public override string ToString() => Name;
    }

    [Theory]
    [InlineData(ListViewCachingStrategy.RetainElement)]
    [InlineData(ListViewCachingStrategy.RecycleElement)]
    public void A_grouped_ListView_change_keeps_the_rows_and_the_scroll_position(ListViewCachingStrategy strategy)
    {
        var groups = new ObservableCollection<Group>
        {
            new("A", Enumerable.Range(0, 20).Select(i => $"a{i}")),
            new("B", Enumerable.Range(0, 20).Select(i => $"b{i}")),
            new("C", Enumerable.Range(0, 20).Select(i => $"c{i}")),
        };
        var (_, skia, handler, host) = HostList(new ListView(strategy)
        {
            ItemsSource = groups,
            IsGroupingEnabled = true,
            GroupDisplayBinding = new Binding(nameof(Group.Name)),
            RowHeight = 40,
        });
        using var _h = host;

        // Rows: A, a0..a19, B, b0..b19, C, ...; row 25 is b3.
        ScrollToRow(skia, host, 25);
        RowText(skia, 25).Should().Be("b3");
        var b3 = skia.GetItemView(25);
        var headerB = skia.GetItemView(21);

        groups[0].Insert(0, "a-new");
        Render(host);
        FirstShown(skia).Should().Be(26, "an item added to a group above keeps the rows in view where they are");
        skia.GetItemView(26).Should().BeSameAs(b3);
        handler.Rows[26].GroupIndex.Should().Be(1);
        handler.Rows[26].Index.Should().Be(3);

        groups[1].RemoveAt(0);
        Render(host);
        skia.GetItemView(25).Should().BeSameAs(b3, "the row moved up with its item");
        handler.Rows[25].Index.Should().Be(2);
        skia.GetItemView(22).Should().BeSameAs(headerB, "the group's header keeps its row");

        groups.Add(new Group("D", new[] { "d0" }));
        Render(host);
        skia.GetItemView(25).Should().BeSameAs(b3, "a group added below changes nothing in view");
        handler.Rows.Last().Item.Should().Be("d0");
    }
}
