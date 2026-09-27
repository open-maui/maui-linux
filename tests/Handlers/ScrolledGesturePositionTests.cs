// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// Pointer events leave a view in window coordinates, whatever ScrollView it is scrolled
/// in: gesture positions and SkiaView.PointerRouted were in the scrolled content's space.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ScrolledGesturePositionTests
{
    [Fact]
    public async Task A_tap_in_scrolled_content_reports_window_and_element_positions()
    {
        Point? inWindow = null, inBox = null;
        var box = new BoxView { HeightRequest = 100, Color = Colors.Red };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, e) => { inWindow = e.GetPosition(null); inBox = e.GetPosition(box); };
        box.GestureRecognizers.Add(tap);
        var stack = new VerticalStackLayout { Children = { new BoxView { HeightRequest = 300 }, box, new BoxView { HeightRequest = 800 } } };
        var scroll = new ScrollView { Content = stack };
        using var host = new HeadlessMauiHost(new ContentPage { Content = scroll }, withEngine: true);
        host.Context.Render();
        await scroll.ScrollToAsync(0, 250, false);
        host.Context.Render();

        var routed = new List<float>();
        var platform = (Microsoft.Maui.Platform.SkiaView)box.Handler!.PlatformView!;
        platform.PointerRouted += (_, e) => routed.Add(e.Pointer.Y);

        // The box's content top is 300; scrolled by 250 it is drawn from y 50.
        host.DisplayWindow.RaisePointerPressed(40, 80, Microsoft.Maui.Platform.PointerButton.Left);
        host.DisplayWindow.RaisePointerReleased(40, 80);

        inWindow!.Value.Y.Should().BeApproximately(80, 0.5);
        inBox!.Value.Y.Should().BeApproximately(30, 0.5);
        routed.Should().NotBeEmpty().And.OnlyContain(y => Math.Abs(y - 80) < 0.5);
        platform.ScreenBounds.Contains(40, 80).Should().BeTrue();
    }
}
