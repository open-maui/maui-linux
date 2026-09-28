// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// A hidden view has nothing on screen to repaint: its invalidations ask for no frame. A hidden
/// indeterminate progress bar (Strikeline's sync indicator) kept an idle window drawing whole
/// frames at 50 a second. Hiding a view still repaints the area it covered, once.
/// </summary>
[Collection("LinuxApplication.Current")]
public class HiddenViewRepaintTests
{
    [Fact]
    public void A_hidden_view_that_keeps_invalidating_asks_for_no_frame()
    {
        var hidden = new BoxView { Color = Colors.Red, HeightRequest = 20, IsVisible = false };
        var holder = new Grid { IsVisible = false, Children = { new BoxView { HeightRequest = 20 } } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { new Label { Text = "Outlook" }, hidden, holder } }, withEngine: true);
        host.Context.Render();
        host.Context.Render();
        var engine = host.Context.RenderingEngine!;
        engine.NeedsFrame.Should().BeFalse("the page is drawn and idle");

        for (int i = 0; i < 10; i++)
        {
            ((Microsoft.Maui.Platform.SkiaView)hidden.Handler!.PlatformView!).Invalidate();
            ((Microsoft.Maui.Platform.SkiaView)holder.Children[0].Handler!.PlatformView!).Invalidate();
        }
        engine.NeedsFrame.Should().BeFalse("neither view is on screen");
    }

    [Fact]
    public void Hiding_a_view_repaints_where_it_was_and_showing_it_draws_it()
    {
        var box = new BoxView { Color = Colors.Red, WidthRequest = 50, HeightRequest = 50, HorizontalOptions = LayoutOptions.Start };
        using var host = new HeadlessMauiHost(new ContentPage { BackgroundColor = Colors.White, Content = new VerticalStackLayout { box } }, withEngine: true);
        host.Context.Render();
        host.Context.Render();
        host.DisplayWindow.PixelAt(25, 25).Should().Be(((byte)255, (byte)0, (byte)0, (byte)255));

        box.IsVisible = false;
        host.Context.RenderingEngine!.NeedsFrame.Should().BeTrue("the area the box covered must be repainted");
        host.Context.Render();
        var (r, g, b, _) = host.DisplayWindow.PixelAt(25, 25);
        (r > 240 && g > 240 && b > 240).Should().BeTrue($"the box is gone, got {r},{g},{b}");

        box.IsVisible = true;
        host.Context.Render();
        host.Context.Render();
        host.DisplayWindow.PixelAt(25, 25).Should().Be(((byte)255, (byte)0, (byte)0, (byte)255));
    }
}
