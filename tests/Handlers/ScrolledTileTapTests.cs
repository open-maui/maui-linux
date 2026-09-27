// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls.Shapes;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// MarketAlly.Flywheel's node strip: tiles (a sized Border around a template root that
/// carries a tap and a pointer recognizer) in a horizontal ScrollView scrolled to its
/// middle copy. A click on a tile fires its tap.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ScrolledTileTapTests
{
    [Fact]
    public async Task A_tile_in_a_scrolled_horizontal_strip_is_tapped()
    {
        var taps = new List<int>();
        var row = new HorizontalStackLayout();
        for (int i = 0; i < 12; i++)
        {
            int index = i;
            var content = new Grid { Children = { new Label { Text = $"Node {i}" } } };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => taps.Add(index);
            content.GestureRecognizers.Add(tap);
            content.GestureRecognizers.Add(new PointerGestureRecognizer());
            row.Children.Add(new Border
            {
                StrokeThickness = 2.5, StrokeShape = new RoundRectangle { CornerRadius = 16 }, Padding = 0,
                Margin = new Thickness(6, 8), WidthRequest = 120, HeightRequest = 120, Content = content,
            });
        }
        var scroll = new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = row };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new Grid { Children = { scroll } } }, withEngine: true);
        host.Context.Render();
        await scroll.ScrollToAsync(4 * 132, 0, false);
        host.Context.Render();

        // The first visible tile is tile 4, at x 6..126 (centred in the strip's height, as in MAUI).
        var y = (float)((Microsoft.Maui.Platform.SkiaView)row.Children[4].Handler!.PlatformView!).Bounds.Center.Y;
        host.DisplayWindow.RaisePointerPressed(60, y, Microsoft.Maui.Platform.PointerButton.Left);
        host.DisplayWindow.RaisePointerReleased(60, y);

        taps.Should().Equal(4);
    }
}
