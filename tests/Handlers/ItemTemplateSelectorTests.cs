// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// An items view whose ItemTemplate is a DataTemplateSelector gets each item's row from the
/// template the selector chooses (CiteLynq's Daily page: a header row, then feed rows). Calling
/// CreateContent on the selector itself gave an empty Label for every row.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ItemTemplateSelectorTests
{
    private sealed class RowSelector : DataTemplateSelector
    {
        public DataTemplate Header { get; } = new(() => new Border { HeightRequest = 120, Content = new Label { Text = "header" } });
        public DataTemplate Item { get; } = new(() =>
        {
            var label = new Label();
            label.SetBinding(Label.TextProperty, ".");
            return new Grid { HeightRequest = 30, Children = { label } };
        });

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
            => item is int ? Header : Item;
    }

    [Fact]
    public void A_CollectionView_makes_each_row_from_the_template_the_selector_chooses()
    {
        var list = new CollectionView
        {
            ItemsSource = new object[] { 0, "first", "second" },
            ItemTemplate = new RowSelector(),
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true);
        host.Context.Render();

        var skia = (SkiaCollectionView)list.Handler!.PlatformView!;
        skia.GetItemView(0).Should().BeOfType<SkiaBorder>("the header row uses the selector's Header template");
        skia.GetItemView(1).Should().BeAssignableTo<SkiaLayoutView>("item rows use its Item template");
        skia.GetItemView(1)!.MauiView.Should().BeOfType<Grid>().Which.BindingContext.Should().Be("first");
    }

    [Fact]
    public void A_CarouselView_makes_each_page_from_the_template_the_selector_chooses()
    {
        var carousel = new CarouselView
        {
            ItemsSource = new object[] { 0, "first" },
            ItemTemplate = new RowSelector(),
            HeightRequest = 200,
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = carousel }, withEngine: true);
        host.Context.Render();

        var skia = (SkiaView)carousel.Handler!.PlatformView!;
        var maui = new List<Type?>();
        void Walk(SkiaView v) { maui.Add(v.MauiView?.GetType()); foreach (var c in v is SkiaLayoutView l ? l.Children : v.Children) Walk(c); }
        Walk(skia);
        maui.Should().Contain(typeof(Border)).And.Contain(typeof(Grid));
    }

    // A control that binds its own parts to itself (CiteLynq's SignUpCard: Root.BindingContext = this).
    private sealed class SelfBoundCard : ContentView
    {
        public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(SelfBoundCard));
        public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public Label TitleLabel { get; } = new();

        public SelfBoundCard()
        {
            TitleLabel.SetBinding(Label.TextProperty, nameof(Title));
            var root = new Border { Content = TitleLabel };
            Content = root;
            root.BindingContext = this;
        }
    }

    [Fact]
    public void A_row_keeps_the_binding_context_its_parts_set_for_themselves()
    {
        var list = new CollectionView
        {
            ItemsSource = new[] { "Join today" },
            ItemTemplate = new DataTemplate(() =>
            {
                var card = new SelfBoundCard();
                card.SetBinding(SelfBoundCard.TitleProperty, ".");
                return card;
            }),
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true);
        host.Context.Render();

        var card = (SelfBoundCard)((SkiaCollectionView)list.Handler!.PlatformView!).GetItemView(0)!.MauiView!;
        card.BindingContext.Should().Be("Join today");
        card.TitleLabel.Text.Should().Be("Join today", "the label binds to the card, its own context, not the row's item");
    }
}
