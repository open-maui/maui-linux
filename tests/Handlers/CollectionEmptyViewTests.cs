// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// A CollectionView's EmptyView that is a view (an icon, a message and an "Ask a question"
/// button on CiteLynq's Questions page) is shown in the list's area while it has no items, and
/// its button works; only a string EmptyView was drawn before.
/// </summary>
[Collection("LinuxApplication.Current")]
public class CollectionEmptyViewTests
{
    private sealed class Model
    {
        public string Message => "No questions yet";
    }

    [Fact]
    public void A_view_empty_view_is_shown_takes_clicks_and_goes_when_items_arrive()
    {
        int asked = 0;
        var message = new Label();
        message.SetBinding(Label.TextProperty, nameof(Model.Message));
        var ask = new Button { Text = "Ask a question", HorizontalOptions = LayoutOptions.Center };
        ask.Clicked += (s, e) => asked++;
        var empty = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 8, Children = { message, ask } };
        var items = new ObservableCollection<string>();
        var list = new CollectionView
        {
            BindingContext = new Model(),
            ItemsSource = items,
            EmptyView = empty,
            ItemTemplate = new DataTemplate(() => new Label { HeightRequest = 40 }),
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true);
        host.Context.Render();

        message.Text.Should().Be("No questions yet", "the empty view inherits the list's BindingContext");
        var skiaList = (SkiaCollectionView)list.Handler!.PlatformView!;
        skiaList.EmptyViewContent.Should().NotBeNull();
        var button = (SkiaView)ask.Handler!.PlatformView!;
        button.Bounds.Height.Should().BeGreaterThan(0, "the empty view was laid out");
        button.Bounds.Center.Y.Should().BeApproximately(300, 60, "centred in the list, as its VerticalOptions say");

        var b = button.Bounds;
        host.DisplayWindow.RaisePointerPressed((float)b.Center.X, (float)b.Center.Y);
        host.DisplayWindow.RaisePointerReleased((float)b.Center.X, (float)b.Center.Y);
        host.Context.Render();
        asked.Should().Be(1, "the empty view's button is clicked");

        items.Add("first");
        host.Context.Render();
        skiaList.HitTest((float)b.Center.X, (float)b.Center.Y).Should().NotBeSameAs(button, "with items the empty view is gone");
    }
}
