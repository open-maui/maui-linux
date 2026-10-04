// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// A CollectionView's Header and Footer are views that scroll with the items, as on every MAUI
/// platform: a View header, a string, or a HeaderTemplate's content bound to the header object.
/// A View header was drawn as its type name in a fixed band ("Microsoft.Maui.Controls.
/// VerticalStackLayout" above CiteLynq's library list), and templates were ignored.
/// </summary>
[Collection("LinuxApplication.Current")]
public class CollectionHeaderFooterTests
{
    private sealed class Model
    {
        public string Status => "12 books";
    }

    private static DataTemplate Row() => new(() =>
    {
        var label = new Label { HeightRequest = 50 };
        label.SetBinding(Label.TextProperty, ".");
        return label;
    });

    [Fact]
    public void A_view_header_is_rendered_bound_and_scrolls_with_the_items()
    {
        var status = new Label();
        status.SetBinding(Label.TextProperty, nameof(Model.Status));
        var header = new VerticalStackLayout { Padding = new Thickness(16, 10), Children = { status } };
        var list = new CollectionView
        {
            BindingContext = new Model(),
            ItemsSource = Enumerable.Range(0, 40).Select(i => $"Item {i}").ToList(),
            ItemTemplate = Row(),
            Header = header,
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true);
        host.Context.Render();

        var skia = (SkiaCollectionView)list.Handler!.PlatformView!;
        status.Text.Should().Be("12 books", "the header inherits the list's BindingContext");
        skia.HeaderView.Should().BeSameAs(header.Handler!.PlatformView, "the header is its own view, not its type name");
        var headerTop = header.Handler!.PlatformView is SkiaView hv ? hv.Bounds.Top : double.NaN;
        var firstRow = skia.GetItemView(0)!;
        firstRow.Bounds.Top.Should().BeGreaterThanOrEqualTo(((SkiaView)header.Handler.PlatformView!).Bounds.Bottom - 0.5, "the first item is under the header");

        host.DisplayWindow.RaiseScroll(400, 300, 3);
        host.Context.Render();
        ((SkiaView)header.Handler.PlatformView!).Bounds.Top.Should().BeLessThan(headerTop, "the header scrolls with the items");
    }

    [Fact]
    public void A_string_header_and_a_templated_footer_are_shown()
    {
        var list = new CollectionView
        {
            ItemsSource = new[] { "a", "b" },
            ItemTemplate = Row(),
            Header = "Books",
            Footer = new Model(),
            FooterTemplate = new DataTemplate(() =>
            {
                var label = new Label();
                label.SetBinding(Label.TextProperty, nameof(Model.Status));
                return label;
            }),
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true);
        host.Context.Render();

        var skia = (SkiaCollectionView)list.Handler!.PlatformView!;
        skia.HeaderView.Should().BeOfType<SkiaLabel>().Which.Text.Should().Be("Books");
        skia.FooterView.Should().BeOfType<SkiaLabel>().Which.Text.Should().Be("12 books", "the template's content is bound to the footer object");
        skia.FooterView!.Bounds.Top.Should().BeGreaterThanOrEqualTo(skia.GetItemView(1)!.Bounds.Bottom - 0.5, "the footer follows the last item");
    }

    [Fact]
    public void A_button_in_the_header_is_clicked_and_a_tap_on_a_row_selects_that_row()
    {
        int clicked = 0;
        var button = new Button { Text = "Refresh", HeightRequest = 40 };
        button.Clicked += (s, e) => clicked++;
        object? selected = null;
        var list = new CollectionView
        {
            ItemsSource = new[] { "a", "b", "c" },
            ItemTemplate = Row(),
            SelectionMode = SelectionMode.Single,
            Header = new VerticalStackLayout { Children = { button } },
        };
        list.SelectionChanged += (s, e) => selected = e.CurrentSelection.FirstOrDefault();
        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true);
        host.Context.Render();
        var skia = (SkiaCollectionView)list.Handler!.PlatformView!;

        void Tap(SkiaView view)
        {
            var b = view.Bounds;
            host.DisplayWindow.RaisePointerPressed((float)b.Center.X, (float)b.Center.Y);
            host.DisplayWindow.RaisePointerReleased((float)b.Center.X, (float)b.Center.Y);
            host.Context.Render();
        }

        Tap((SkiaView)button.Handler!.PlatformView!);
        clicked.Should().Be(1);
        selected.Should().BeNull("a tap in the header selects no item");

        Tap(skia.GetItemView(1)!);
        selected.Should().Be("b", "rows under the header map to their own items");
    }
}
