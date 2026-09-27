// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using SkiaSharp;
using Syncfusion.Maui.Carousel;
using Syncfusion.Maui.Core.Carousel;
using Syncfusion.Maui.Core.Internals;
using Syncfusion.Maui.Rotator;
using Syncfusion.Maui.SignaturePad;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// Syncfusion controls whose platform-neutral handlers have no platform view
/// (SfCarousel, SfRotator, SfSignaturePad, SfInteractiveScrollView) on
/// OpenMaui.Controls.Linux.Syncfusion: they create, draw their items and
/// respond to swipes, drags and clicks.
/// </summary>
[Collection(CompatHost.Collection)]
public sealed class SyncfusionNativeViewCompatTests
{
    private static readonly Color[] Palette = { Colors.Red, Colors.Lime, Colors.Blue, Colors.Orange, Colors.Magenta };

    private static SKColor Sk(Color c) => new((byte)(c.Red * 255), (byte)(c.Green * 255), (byte)(c.Blue * 255));

    private static SKRectI Around(double x, double y, int r = 6) => new((int)x - r, (int)y - r, (int)x + r, (int)y + r);

    private static void Drag(CompatHost host, float fromX, float fromY, float toX, float toY, int steps = 8)
    {
        host.DisplayWindow.RaisePointerPressed(fromX, fromY);
        for (int i = 1; i <= steps; i++)
            host.DisplayWindow.RaisePointerMoved(fromX + (toX - fromX) * i / steps, fromY + (toY - fromY) * i / steps);
        host.DisplayWindow.RaisePointerReleased(toX, toY);
    }

    private static void Click(CompatHost host, double x, double y)
    {
        host.DisplayWindow.RaisePointerPressed((float)x, (float)y);
        host.DisplayWindow.RaisePointerReleased((float)x, (float)y);
    }

    /// <summary>Renders until slide animations (300 ms) are over.</summary>
    private static void Settle(CompatHost host)
    {
        host.Render();
        Thread.Sleep(400);
        host.Render();
        host.Render();
    }

    /// <summary>
    /// The control in a fixed-size cell at the page's top-left: SfRotator and
    /// SfSignaturePad size themselves to the constraints they are given (their
    /// own MeasureOverride), as on every platform.
    /// </summary>
    private static ContentPage PageWith(View view) => new()
    {
        BackgroundColor = Colors.White,
        Content = new Grid
        {
            WidthRequest = view.WidthRequest,
            HeightRequest = view.HeightRequest,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Children = { view },
        },
    };

    #region SfCarousel

    private static SfCarousel Carousel(ViewMode mode = ViewMode.Default)
    {
        var items = Palette.Select(c => (object)new SfCarouselItem { ItemContent = new BoxView { Color = c } }).ToList();
        return new SfCarousel
        {
            ItemsSource = items,
            ItemWidth = 100,
            ItemHeight = 150,
            Duration = 0,
            ViewMode = mode,
            WidthRequest = 600,
            HeightRequest = 300,
            HorizontalOptions = LayoutOptions.Start,
        };
    }

    [Fact]
    public void Carousel_shows_the_selected_item_in_the_centre_and_its_neighbours_beside_it()
    {
        var carousel = Carousel();
        using var host = new CompatHost(PageWith(carousel), b => b.UseLinuxSyncfusion(), 700, 400);
        host.Render();

        carousel.Handler.Should().BeOfType<SfCarouselBridgeHandler>();
        host.CountPixelsNear(Sk(Colors.Red), Around(300, 150)).Should().BeGreaterThan(100, "the selected item is centred");
        host.CountPixelsNear(Sk(Colors.Lime), Around(395, 150)).Should().BeGreaterThan(100, "the next item sits to the right");
        host.CountPixelsNear(Sk(Colors.Red), Around(395, 150)).Should().Be(0);
    }

    [Fact]
    public void Swiping_the_carousel_selects_the_next_item()
    {
        var carousel = Carousel();
        var changes = new List<Syncfusion.Maui.Core.Carousel.SelectionChangedEventArgs>();
        bool? swipedLeft = null;
        bool swipeEnded = false;
        carousel.SelectionChanged += (_, e) => changes.Add(e);
        carousel.SwipeStarted += (_, e) => swipedLeft = e.IsSwipedLeft;
        carousel.SwipeEnded += (_, _) => swipeEnded = true;
        using var host = new CompatHost(PageWith(carousel), b => b.UseLinuxSyncfusion(), 700, 400);
        host.Render();

        Drag(host, 350, 150, 250, 150);
        host.Render();

        carousel.SelectedIndex.Should().Be(1);
        changes.Should().ContainSingle();
        changes[0].OldItem.Should().BeSameAs(carousel.ItemsSource.ElementAt(0));
        changes[0].NewItem.Should().BeSameAs(carousel.ItemsSource.ElementAt(1));
        swipedLeft.Should().BeTrue();
        swipeEnded.Should().BeTrue();
        host.CountPixelsNear(Sk(Colors.Lime), Around(300, 150)).Should().BeGreaterThan(100, "the new selection is centred");
    }

    [Fact]
    public void Clicking_a_carousel_item_selects_it_and_MoveNext_moves_on()
    {
        var carousel = Carousel();
        using var host = new CompatHost(PageWith(carousel), b => b.UseLinuxSyncfusion(), 700, 400);
        host.Render();

        Click(host, 395, 150);
        host.Render();
        carousel.SelectedIndex.Should().Be(1);

        carousel.MoveNext();
        host.Render();
        carousel.SelectedIndex.Should().Be(2);
        host.CountPixelsNear(Sk(Colors.Blue), Around(300, 150)).Should().BeGreaterThan(100);

        carousel.MovePrevious();
        carousel.SelectedIndex.Should().Be(1);
    }

    [Fact]
    public void A_linear_carousel_lays_its_items_out_in_a_strip()
    {
        var carousel = Carousel(ViewMode.Linear);
        carousel.ItemSpacing = 10;
        using var host = new CompatHost(PageWith(carousel), b => b.UseLinuxSyncfusion(), 700, 400);
        host.Render();

        host.CountPixelsNear(Sk(Colors.Red), Around(50, 150)).Should().BeGreaterThan(100);
        host.CountPixelsNear(Sk(Colors.Lime), Around(160, 150)).Should().BeGreaterThan(100);
        host.CountPixelsNear(Sk(Colors.Blue), Around(270, 150)).Should().BeGreaterThan(100);
    }

    private sealed record Swatch(Color Color);

    [Fact]
    public void Carousel_data_items_use_the_item_template()
    {
        var carousel = new SfCarousel
        {
            ItemsSource = Palette.Select(c => (object)new Swatch(c)).ToList(),
            ItemTemplate = new DataTemplate(() =>
            {
                var box = new BoxView();
                box.SetBinding(BoxView.ColorProperty, nameof(Swatch.Color));
                return box;
            }),
            ItemWidth = 100, ItemHeight = 150, Duration = 0,
            WidthRequest = 600, HeightRequest = 300, HorizontalOptions = LayoutOptions.Start,
            SelectedIndex = 2,
        };
        using var host = new CompatHost(PageWith(carousel), b => b.UseLinuxSyncfusion(), 700, 400);
        host.Render();

        host.CountPixelsNear(Sk(Colors.Blue), Around(300, 150)).Should().BeGreaterThan(100, "SelectedIndex 2 is centred");
    }

    #endregion

    #region SfRotator

    private static SfRotator Rotator()
    {
        var items = Palette.Take(3).Select((c, i) => (object)new SfRotatorItem { ItemContent = new BoxView { Color = c }, ItemText = "Item " + i }).ToList();
        return new SfRotator
        {
            ItemsSource = items,
            WidthRequest = 300,
            HeightRequest = 200,
            HorizontalOptions = LayoutOptions.Start,
        };
    }

    [Fact]
    public void Rotator_shows_the_selected_item_and_its_dots()
    {
        var rotator = Rotator();
        using var host = new CompatHost(PageWith(rotator), b => b.UseLinuxSyncfusion(), 400, 300);
        host.Render();

        rotator.Handler.Should().BeOfType<SfRotatorBridgeHandler>();
        host.CountPixelsNear(Sk(Colors.Red), Around(150, 80)).Should().BeGreaterThan(100);
        var view = (SkiaSfRotator)CompatHost.PlatformOf(rotator);
        var dot = view.DotCenter(0);
        host.CountPixelsNear(new SKColor(73, 69, 79), Around(dot.X, dot.Y, 3)).Should().BeGreaterThan(5, "the selected dot is drawn over the item");
    }

    [Fact]
    public void Swiping_the_rotator_slides_to_the_next_item()
    {
        var rotator = Rotator();
        var indices = new List<double>();
        rotator.SelectedIndexChanged += (_, e) => indices.Add(e.Index);
        using var host = new CompatHost(PageWith(rotator), b => b.UseLinuxSyncfusion(), 400, 300);
        host.Render();

        Drag(host, 250, 80, 100, 80);
        Settle(host);

        rotator.SelectedIndex.Should().Be(1);
        indices.Should().Equal(1);
        host.CountPixelsNear(Sk(Colors.Lime), Around(150, 80)).Should().BeGreaterThan(100);

        // Back again, the other way.
        Drag(host, 100, 80, 250, 80);
        Settle(host);
        rotator.SelectedIndex.Should().Be(0);
    }

    [Fact]
    public void Clicking_a_rotator_dot_selects_its_item_and_clicking_the_item_raises_ItemTapped()
    {
        var rotator = Rotator();
        int taps = 0;
        rotator.ItemTapped += (_, _) => taps++;
        using var host = new CompatHost(PageWith(rotator), b => b.UseLinuxSyncfusion(), 400, 300);
        host.Render();

        var dot = ((SkiaSfRotator)CompatHost.PlatformOf(rotator)).DotCenter(2);
        Click(host, dot.X, dot.Y);
        Settle(host);
        rotator.SelectedIndex.Should().Be(2);
        host.CountPixelsNear(Sk(Colors.Blue), Around(150, 80)).Should().BeGreaterThan(100);
        taps.Should().Be(0);

        Click(host, 150, 80);
        taps.Should().Be(1);
    }

    [Fact]
    public void Rotator_Next_loops_and_thumbnails_navigate()
    {
        var rotator = Rotator();
        rotator.NavigationStripMode = Syncfusion.Maui.Core.Rotator.NavigationStripMode.Thumbnail;
        rotator.ShowNavigationButton = true;
        rotator.WidthRequest = 360;
        using var host = new CompatHost(PageWith(rotator), b => b.UseLinuxSyncfusion(), 400, 300);
        host.Render();

        var view = (SkiaSfRotator)CompatHost.PlatformOf(rotator);
        var thumb = view.ThumbRect(1);
        host.CountPixelsNear(Sk(Colors.Lime), new SKRectI((int)thumb.Left + 4, (int)thumb.Top + 4, (int)thumb.Right - 4, (int)thumb.Bottom - 4))
            .Should().BeGreaterThan(200, "a thumbnail shows its item");

        Click(host, thumb.MidX, thumb.MidY);
        Settle(host);
        rotator.SelectedIndex.Should().Be(1);

        rotator.Next();
        Settle(host);
        rotator.SelectedIndex.Should().Be(2);
        rotator.Next();
        Settle(host);
        rotator.SelectedIndex.Should().Be(0, "EnableLooping wraps around");

        rotator.EnableLooping = false;
        rotator.Previous();
        Settle(host);
        rotator.SelectedIndex.Should().Be(0, "without looping the first item has no previous");
    }

    #endregion

    #region SfSignaturePad

    [Fact]
    public void Dragging_on_the_signature_pad_draws_a_stroke()
    {
        var pad = new SfSignaturePad { WidthRequest = 300, HeightRequest = 200, StrokeColor = Colors.Blue, HorizontalOptions = LayoutOptions.Start };
        int started = 0, completed = 0;
        pad.DrawStarted += (_, _) => started++;
        pad.DrawCompleted += (_, _) => completed++;
        using var host = new CompatHost(PageWith(pad), b => b.UseLinuxSyncfusion(), 400, 300);
        host.Render();
        pad.Handler.Should().BeOfType<SfSignaturePadBridgeHandler>();
        host.CountPixelsNear(Sk(Colors.Blue), host.WindowRect).Should().Be(0);

        Drag(host, 30, 100, 270, 60, steps: 24);
        host.Render();

        started.Should().Be(1);
        completed.Should().Be(1);
        host.CountPixelsNear(Sk(Colors.Blue), new SKRectI(20, 40, 290, 120)).Should().BeGreaterThan(400, "the stroke is inked");
        pad.GetSignaturePoints().Should().ContainSingle().Which.Count.Should().Be(2 * 26, "x and y of every pointer event in the stroke");

        pad.Clear();
        host.Render();
        host.CountPixelsNear(Sk(Colors.Blue), host.WindowRect).Should().Be(0);
    }

    [Fact]
    public void A_cancelled_DrawStarted_draws_nothing()
    {
        var pad = new SfSignaturePad { WidthRequest = 300, HeightRequest = 200, StrokeColor = Colors.Blue, HorizontalOptions = LayoutOptions.Start };
        pad.DrawStarted += (_, e) => e.Cancel = true;
        using var host = new CompatHost(PageWith(pad), b => b.UseLinuxSyncfusion(), 400, 300);
        host.Render();

        Drag(host, 30, 100, 270, 60, steps: 12);
        host.Render();

        host.CountPixelsNear(Sk(Colors.Blue), host.WindowRect).Should().Be(0);
    }

    [Fact]
    public async Task The_signature_exports_to_a_png_image_source()
    {
        var pad = new SfSignaturePad { WidthRequest = 300, HeightRequest = 200, StrokeColor = Colors.Red, HorizontalOptions = LayoutOptions.Start };
        using var host = new CompatHost(PageWith(pad), b => b.UseLinuxSyncfusion(), 400, 300);
        host.Render();
        Drag(host, 30, 100, 270, 60, steps: 24);
        host.Render();

        var source = pad.ToImageSource();

        var stream = source.Should().BeOfType<StreamImageSource>().Subject;
        await using var png = await stream.Stream(CancellationToken.None);
        using var bitmap = SKBitmap.Decode(png);
        bitmap.Width.Should().Be(300);
        bitmap.Height.Should().Be(200);
        int red = 0, clear = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Alpha > 200 && p.Red > 200 && p.Green < 60) red++;
                if (p.Alpha == 0) clear++;
            }
        red.Should().BeGreaterThan(400, "the strokes are in the image");
        clear.Should().BeGreaterThan(40000, "the background is transparent, as the native export's");
    }

    #endregion

    #region SfInteractiveScrollView

    [Fact]
    public async Task The_interactive_scroll_view_shows_and_scrolls_its_content()
    {
        var stack = new VerticalStackLayout();
        foreach (var color in Palette)
            stack.Add(new BoxView { Color = color, HeightRequest = 100, WidthRequest = 200 });
        var scroller = new SfInteractiveScrollView { Content = stack, HeightRequest = 200, WidthRequest = 300, HorizontalOptions = LayoutOptions.Start };
        using var host = new CompatHost(PageWith(scroller), b => b.UseLinuxSyncfusion(), 400, 300);
        host.Render();
        host.Render();

        scroller.Handler.Should().BeOfType<SfInteractiveScrollViewBridgeHandler>();
        host.CountPixelsNear(Sk(Colors.Red), Around(100, 50)).Should().BeGreaterThan(100);
        scroller.ViewportHeight.Should().Be(200);

        var done = scroller.ScrollToAsync(0, 200, false);
        host.Render();
        (await Task.WhenAny(done, Task.Delay(2000))).Should().BeSameAs(done, "ScrollToAsync completes");
        scroller.ScrollY.Should().Be(200);
        host.CountPixelsNear(Sk(Colors.Blue), Around(100, 50)).Should().BeGreaterThan(100, "the third box is at the top");

        // The mouse wheel scrolls it too, and the control follows.
        var platform = (SkiaScrollView)CompatHost.PlatformOf(scroller);
        platform.OnScroll(new Microsoft.Maui.Platform.ScrollEventArgs(100, 100, 0, 1));
        host.Render();
        scroller.ScrollY.Should().BeGreaterThan(200);
    }

    #endregion
}
