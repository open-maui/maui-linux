// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// ZIndex orders overlapping children, as on every platform: SfListView's
/// sticky group header, added before the rows that scroll under it, sits
/// on a higher ZIndex to stay on top.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ZIndexTests
{
    [Fact]
    public void A_child_with_a_higher_ZIndex_draws_and_hits_on_top()
    {
        var header = new BoxView { Color = Colors.Red, ZIndex = 1 };
        var row = new BoxView { Color = Colors.Blue };
        var tapped = "";
        var headerTap = new TapGestureRecognizer();
        headerTap.Tapped += (_, _) => tapped = "header";
        header.GestureRecognizers.Add(headerTap);
        var grid = new Grid { WidthRequest = 100, HeightRequest = 100, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Children = { header, row } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true);
        host.Context.Render();

        var (r, _, b, _) = host.DisplayWindow.PixelAt(50, 50);
        ((int)r).Should().BeGreaterThan(200, "the header (ZIndex 1) is on top");
        ((int)b).Should().BeLessThan(50);

        host.DisplayWindow.RaisePointerPressed(50, 50);
        host.DisplayWindow.RaisePointerReleased(50, 50);
        tapped.Should().Be("header");
    }
}
