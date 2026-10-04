// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Input;
using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// Controls inside a CollectionView row take the pointer, as on the other platforms: the list
/// kept every press, so only a recognizer on the row's root ever fired (CiteLynq's article card,
/// whose inner Border opens the article, did nothing).
/// </summary>
[Collection("LinuxApplication.Current")]
public class CollectionRowInputTests
{
    // CiteLynq's FeedItemCard: a ContentView whose Border, bound to the control, opens the item.
    private sealed class Card : ContentView
    {
        public static readonly BindableProperty OpenCommandProperty = BindableProperty.Create(nameof(OpenCommand), typeof(ICommand), typeof(Card));
        public ICommand? OpenCommand { get => (ICommand?)GetValue(OpenCommandProperty); set => SetValue(OpenCommandProperty, value); }
        public static readonly BindableProperty ItemProperty = BindableProperty.Create(nameof(Item), typeof(object), typeof(Card));
        public object? Item { get => GetValue(ItemProperty); set => SetValue(ItemProperty, value); }

        public Card()
        {
            var root = new Border { Margin = new Thickness(16, 6), Padding = new Thickness(16, 12), HeightRequest = 80, Content = new Label { Text = "row" } };
            var tap = new TapGestureRecognizer();
            tap.SetBinding(TapGestureRecognizer.CommandProperty, nameof(OpenCommand));
            tap.SetBinding(TapGestureRecognizer.CommandParameterProperty, nameof(Item));
            root.GestureRecognizers.Add(tap);
            Content = root;
            root.BindingContext = this;
        }
    }

    private static void Tap(HeadlessMauiHost host, SkiaView view)
    {
        var r = view.Bounds;
        float x = (float)(r.X + r.Width / 2), y = (float)(r.Y + r.Height / 2);
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        host.Context.Render();
    }

    [Fact]
    public void A_tap_recognizer_inside_a_row_fires_with_the_rows_item()
    {
        object? opened = null;
        var open = new Command<object>(p => opened = p);
        var list = new CollectionView
        {
            ItemsSource = new[] { "a", "b", "c" },
            SelectionMode = SelectionMode.None,
            ItemTemplate = new DataTemplate(() =>
            {
                var card = new Card { OpenCommand = open };
                card.SetBinding(Card.ItemProperty, ".");
                return card;
            }),
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new RefreshView { Content = list } }, withEngine: true);
        host.Context.Render();

        Tap(host, ((SkiaCollectionView)list.Handler!.PlatformView!).GetItemView(1)!);
        opened.Should().Be("b");
    }

    [Fact]
    public void A_button_in_a_row_is_clicked_and_a_recognizer_on_the_row_root_still_fires()
    {
        var clicked = new List<string>();
        var tapped = new List<string>();
        var list = new CollectionView
        {
            ItemsSource = new[] { "a", "b" },
            ItemTemplate = new DataTemplate(() =>
            {
                var button = new Button { Text = "go", WidthRequest = 80, HeightRequest = 40, HorizontalOptions = LayoutOptions.End };
                button.Clicked += (s, e) => clicked.Add((string)((Button)s!).BindingContext);
                var row = new Grid { HeightRequest = 60, Children = { new Label { Text = "row" }, button } };
                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, e) => tapped.Add((string)((Grid)s!).BindingContext);
                row.GestureRecognizers.Add(tap);
                return row;
            }),
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true);
        host.Context.Render();
        var skia = (SkiaCollectionView)list.Handler!.PlatformView!;

        var row1 = (SkiaLayoutView)skia.GetItemView(1)!;
        Tap(host, row1.Children.OfType<SkiaButton>().Single());
        clicked.Should().Equal("b");
        tapped.Should().BeEmpty("the button took the tap");

        // The row's own recognizer, off the button.
        var r = row1.Bounds;
        host.DisplayWindow.RaisePointerPressed((float)r.X + 10, (float)(r.Y + r.Height / 2));
        host.DisplayWindow.RaisePointerReleased((float)r.X + 10, (float)(r.Y + r.Height / 2));
        host.Context.Render();
        tapped.Should().Equal("b");
    }
    [Fact]
    public void A_pointer_recognizer_inside_a_row_gets_the_pointer_and_the_item_is_still_selected()
    {
        var events = new List<string>();
        var list = new CollectionView
        {
            ItemsSource = new[] { "a", "b", "c" },
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label { Text = "row", HeightRequest = 30 };
                var pointer = new PointerGestureRecognizer();
                pointer.PointerEntered += (s, e) => events.Add("entered " + ((Label)s!).BindingContext);
                pointer.PointerExited += (s, e) => events.Add("exited " + ((Label)s!).BindingContext);
                pointer.PointerPressed += (s, e) => events.Add("pressed " + ((Label)s!).BindingContext);
                pointer.PointerReleased += (s, e) => events.Add("released " + ((Label)s!).BindingContext);
                label.GestureRecognizers.Add(pointer);
                return new VerticalStackLayout { Padding = new Thickness(0, 10), Children = { label } };
            }),
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true);
        host.Context.Render();
        var skia = (SkiaCollectionView)list.Handler!.PlatformView!;
        var label1 = ((SkiaLayoutView)skia.GetItemView(1)!).Children.Single();
        var r = label1.Bounds;
        float x = (float)(r.X + r.Width / 2), y = (float)(r.Y + r.Height / 2);

        host.DisplayWindow.RaisePointerMoved(x, y);
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        host.Context.Render();

        events.Should().Equal(new[] { "entered b", "pressed b", "released b" },
            "the row content under the pointer gets its pointer events, as on Windows");
        list.SelectedItem.Should().Be("b", "the list still selects the item the press was on");

        // Over the row's padding (outside the label), then off the list.
        host.DisplayWindow.RaisePointerMoved(x, (float)r.Y - 5);
        events.Should().EndWith("exited b");
        events.Clear();
        host.DisplayWindow.RaisePointerMoved(x, (float)skia.GetItemView(2)!.Bounds.Center.Y);
        events.Should().Equal("entered c");
    }

    [Fact]
    public void Scrolling_a_list_by_dragging_cancels_the_row_contents_press()
    {
        var routed = new List<SkiaView.RoutedPointerKind>();
        bool subscribed = false;
        var list = new CollectionView
        {
            ItemsSource = Enumerable.Range(0, 50).Select(i => i.ToString()).ToArray(),
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() => new Label { HeightRequest = 40, Text = "row" }),
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true);
        host.Context.Render();
        var skia = (SkiaCollectionView)list.Handler!.PlatformView!;
        var row = skia.GetItemView(2)!;
        row.PointerRouted += (_, e) => { if (subscribed) routed.Add(e.Kind); };
        subscribed = true;
        var r = row.Bounds;
        float x = (float)r.Center.X, y = (float)r.Center.Y;

        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerMoved(x, y - 60);
        host.DisplayWindow.RaisePointerMoved(x, y - 120);
        host.DisplayWindow.RaisePointerReleased(x, y - 120);
        host.Context.Render();

        routed.Should().StartWith(new[] { SkiaView.RoutedPointerKind.Entered, SkiaView.RoutedPointerKind.Pressed });
        routed.Should().ContainInOrder(SkiaView.RoutedPointerKind.Exited, SkiaView.RoutedPointerKind.Released);
        routed.Count(k => k == SkiaView.RoutedPointerKind.Released).Should().Be(1, "the scroll ended the press once");
        list.SelectedItem.Should().BeNull("a drag that scrolls the list is not a tap");
    }

    [Fact]
    public void A_pointer_recognizer_in_a_ListView_cell_gets_the_pointer_and_the_row_is_still_selected()
    {
        int pressed = 0, released = 0;
        var list = new ListView
        {
            ItemsSource = new[] { "a", "b" },
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label { Text = "row" };
                var pointer = new PointerGestureRecognizer();
                pointer.PointerPressed += (_, _) => pressed++;
                pointer.PointerReleased += (_, _) => released++;
                label.GestureRecognizers.Add(pointer);
                return new ViewCell { View = new Grid { HeightRequest = 44, Children = { label } } };
            }),
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true);
        host.Context.Render();
        var skia = (SkiaCollectionView)list.Handler!.PlatformView!;
        var c = skia.GetItemView(1)!.Bounds.Center;

        host.DisplayWindow.RaisePointerPressed((float)c.X, (float)c.Y);
        host.DisplayWindow.RaisePointerReleased((float)c.X, (float)c.Y);
        host.Context.Render();

        pressed.Should().Be(1);
        released.Should().Be(1);
        list.SelectedItem.Should().Be("b");
    }
}
