// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;
using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using PointerButton = Microsoft.Maui.Platform.PointerButton;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// A layout with a TapGestureRecognizer (a dismissable backdrop, a tappable
/// card) must still let the controls on it take their own input, while taps
/// on its plain content reach its recognizer.
/// </summary>
[Collection("LinuxApplication.Current")]
public class TappableContainerHitTests
{
    private static void Click(HeadlessMauiHost host, Point at)
    {
        var hit = host.RootView!.HitTest((float)at.X, (float)at.Y);
        hit.Should().NotBeNull();
        hit!.OnPointerPressed(new Microsoft.Maui.Platform.PointerEventArgs((float)at.X, (float)at.Y, PointerButton.Left));
        hit.OnPointerReleased(new Microsoft.Maui.Platform.PointerEventArgs((float)at.X, (float)at.Y, PointerButton.Left));
    }

    private static Point Centre(View view)
    {
        var b = ((SkiaView)view.Handler!.PlatformView!).Bounds;
        return new Point(b.Center.X, b.Center.Y);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_button_on_a_tappable_container_gets_its_click(bool border)
    {
        int clicks = 0, taps = 0;
        var button = new Button { Text = "Start" };
        button.Clicked += (_, _) => clicks++;
        var label = new Label { Text = "Welcome" };
        var stack = new VerticalStackLayout { label, button };
        View container = border ? new Border { Content = stack } : new Grid { stack };
        container.GestureRecognizers.Add(new TapGestureRecognizer().With(t => t.Tapped += (_, _) => taps++));

        using var host = new HeadlessMauiHost(new ContentPage { Content = container }, withEngine: true);
        host.Context.Render();

        Click(host, Centre(button));
        clicks.Should().Be(1);
        taps.Should().Be(0, "a click on a control is not a tap on the container behind it");

        Click(host, Centre(label));
        taps.Should().Be(1, "plain content still passes taps to the container");
    }

    [Fact]
    public void A_secondary_button_recognizer_fires_on_right_clicks_only_and_leaves_left_clicks_to_the_parent()
    {
        // GitCleaner's activity row: a right-click context menu recognizer on
        // the row, inside a list item whose left click navigates.
        int contextMenus = 0, itemTaps = 0;
        var label = new Label { Text = "maui-linux", HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        var row = new Grid { Children = { label } };
        row.GestureRecognizers.Add(new TapGestureRecognizer { Buttons = ButtonsMask.Secondary }.With(t => t.Tapped += (_, _) => contextMenus++));
        var item = new ContentView { Content = row };
        item.GestureRecognizers.Add(new TapGestureRecognizer().With(t => t.Tapped += (_, _) => itemTaps++));

        using var host = new HeadlessMauiHost(new ContentPage { Content = item }, withEngine: true);
        host.Context.Render();
        var at = Centre(label);

        host.DisplayWindow.RaisePointerPressed((float)at.X, (float)at.Y, Microsoft.Maui.Platform.PointerButton.Left);
        host.DisplayWindow.RaisePointerReleased((float)at.X, (float)at.Y);
        contextMenus.Should().Be(0, "a left click is not a secondary tap");
        itemTaps.Should().Be(1, "the left click reaches the item's own recognizer");

        host.DisplayWindow.RaisePointerPressed((float)at.X, (float)at.Y, Microsoft.Maui.Platform.PointerButton.Right);
        host.DisplayWindow.RaisePointerReleased((float)at.X, (float)at.Y);
        contextMenus.Should().Be(1, "a right click fires the context menu");
        itemTaps.Should().Be(1, "a right click is not a primary tap");
    }
}
