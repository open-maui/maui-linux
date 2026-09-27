// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// A layout change asked for during layout (a SizeChanged handler moving the
/// view, as MAToolTip places its card) is laid out before the frame is drawn,
/// so the view never shows for a frame where it was.
/// </summary>
[Collection("LinuxApplication.Current")]
public class LayoutSettleTests
{
    [Fact]
    public void A_margin_set_from_SizeChanged_applies_in_the_same_frame()
    {
        var card = new Border
        {
            Content = new Label { Text = "tip" },
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
        };
        card.SizeChanged += (_, _) =>
        {
            if (card.Width > 0) card.Margin = new Thickness(100, 60, 0, 0);
        };
        var host = new Grid();
        using var h = new HeadlessMauiHost(new ContentPage { Content = host }, withEngine: true);
        h.Context.Render();

        host.Children.Add(card);
        h.Context.Render();

        var view = (SkiaView)card.Handler!.PlatformView!;
        view.Bounds.Left.Should().BeApproximately(100, 0.5);
        view.Bounds.Top.Should().BeApproximately(60, 0.5);
    }
}
