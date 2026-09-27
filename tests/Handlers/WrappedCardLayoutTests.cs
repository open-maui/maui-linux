// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// MAToolTip's card: a width-capped Border (stroke and padding) holding a title
/// and a word-wrapping description. The description must get the height its
/// wrapped lines need at the width it is given.
/// </summary>
[Collection("LinuxApplication.Current")]
public class WrappedCardLayoutTests
{
    [Theory]
    [InlineData("Stages every file you have ticked so it is included in the next commit you make.")]
    [InlineData("Discards the changes in the ticked files and restores them to the last committed version. This cannot be undone.")]
    [InlineData("Short tip.")]
    public void A_wrapped_description_in_a_capped_card_is_not_clipped(string description)
    {
        var title = new Label { Text = "Stage ticked files", FontSize = 13, FontAttributes = FontAttributes.Bold };
        var body = new Label { Text = description, FontSize = 12, LineBreakMode = LineBreakMode.WordWrap };
        var card = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            StrokeThickness = 1,
            Padding = new Thickness(12, 8),
            Content = new VerticalStackLayout { Spacing = 2, Children = { title, body } },
            MaximumWidthRequest = 320,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new Grid { Children = { card } } }, withEngine: true);
        host.Context.Render();

        var bodyView = (SkiaView)body.Handler!.PlatformView!;
        var needed = bodyView.Measure(new Microsoft.Maui.Graphics.Size(bodyView.Bounds.Width, double.PositiveInfinity)).Height;
        bodyView.Bounds.Height.Should().BeGreaterThanOrEqualTo(needed - 0.5, "the description gets the height its lines need at its width");

        var cardView = (SkiaView)card.Handler!.PlatformView!;
        bodyView.Bounds.Bottom.Should().BeLessThanOrEqualTo(cardView.Bounds.Bottom - 8 - 1 + 0.5, "the bottom padding is kept");
    }
}
