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
}
