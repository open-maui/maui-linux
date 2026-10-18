// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls.Linux.Tests.Golden;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using PointerButton = Microsoft.Maui.Platform.PointerButton;

/// <summary>
/// SkiaCarouselView's settling as Windows' carousel ScrollViewer sets it: SnapPointsType (None,
/// Mandatory, MandatorySingle) and SnapPointsAlignment (Start, Center, End), IsBounceEnabled at
/// the ends, PeekAreaInsets on each side, and a looping carousel of two items with a peek.
/// </summary>
public class SkiaCarouselViewSnapTests
{
    private const float Width = 400, Height = 300;

    private sealed class Clock
    {
        public DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        public void Advance(double seconds) => Now = Now.AddSeconds(seconds);
    }

    private static (SkiaCarouselView Carousel, Clock Clock) Create(int count, Action<SkiaCarouselView>? configure = null)
    {
        var clock = new Clock();
        var carousel = new SkiaCarouselView { ShowIndicators = false };
        carousel.Clock = () => clock.Now;
        for (int i = 0; i < count; i++)
            carousel.AddItem(new SkiaLabel { Text = $"Item{i}" });
        configure?.Invoke(carousel);
        carousel.Measure(new Size(Width, Height));
        carousel.Arrange(new Rect(0, 0, Width, Height));
        return (carousel, clock);
    }

    /// <summary>A horizontal drag from <paramref name="fromX"/> to <paramref name="toX"/> lasting <paramref name="seconds"/>, then a release.</summary>
    private static void Drag(SkiaCarouselView carousel, Clock clock, float fromX, float toX, double seconds, bool release = true)
    {
        carousel.OnPointerPressed(new PointerEventArgs(fromX, Height / 2, PointerButton.Left));
        clock.Advance(seconds);
        carousel.OnPointerMoved(new PointerEventArgs(toX, Height / 2, PointerButton.Left));
        if (release)
            carousel.OnPointerReleased(new PointerEventArgs(toX, Height / 2, PointerButton.Left));
    }

    private static float StartOf(SkiaCarouselView carousel, int index) =>
        carousel.VisibleItemPlacements().First(p => p.Index == index).Start;

    [Fact]
    public void None_comes_to_rest_where_the_drag_and_its_fling_leave_it()
    {
        var (carousel, clock) = Create(5, c => c.SnapPointsType = SnapPointsType.None);

        // 130 px in a second: 130 px/s carries it another 0.3 s (39 px).
        Drag(carousel, clock, 300, 170, 1.0);

        carousel.RestingOffset.Should().BeApproximately(169, 0.5f, "no snap point: it stops where the fling ends");
        carousel.Position.Should().Be(0, "the first item is still the one at the middle");
    }

    [Fact]
    public void Mandatory_settles_on_the_item_a_fling_reaches()
    {
        var (carousel, clock) = Create(5, c => c.SnapPointsType = SnapPointsType.Mandatory);

        // 280 px in 0.1 s (2800 px/s) carries it 840 px further: to about item 3.
        Drag(carousel, clock, 380, 100, 0.1);

        carousel.Position.Should().Be(3);
        carousel.RestingOffset.Should().BeApproximately(3 * Width, 0.5f, "on the item's snap point");
    }

    [Fact]
    public void MandatorySingle_moves_at_most_one_item_per_gesture()
    {
        var (carousel, clock) = Create(5, c => c.SnapPointsType = SnapPointsType.MandatorySingle);

        Drag(carousel, clock, 380, 100, 0.1);
        carousel.Position.Should().Be(1, "however hard the fling, one item on");
        carousel.RestingOffset.Should().BeApproximately(Width, 0.5f);
        carousel.FinishSettling();

        // A slow drag past the middle of the next item still stops at the next one.
        Drag(carousel, clock, 395, 5, 3.0);
        carousel.Position.Should().Be(2);
    }

    [Theory]
    [InlineData(SnapPointsType.Mandatory)]
    [InlineData(SnapPointsType.MandatorySingle)]
    public void A_slow_short_drag_returns_to_the_item_it_started_on(SnapPointsType type)
    {
        var (carousel, clock) = Create(5, c => c.SnapPointsType = type);

        Drag(carousel, clock, 300, 180, 1.0);

        carousel.Position.Should().Be(0);
        carousel.RestingOffset.Should().BeApproximately(0, 0.5f);
    }

    [Theory]
    [InlineData(SnapPointsAlignment.Start, 0f)]
    [InlineData(SnapPointsAlignment.Center, 25f)]
    [InlineData(SnapPointsAlignment.End, 50f)]
    public void The_snap_alignment_lines_the_item_up_with_the_carousel(SnapPointsAlignment alignment, float expectedStart)
    {
        // Peeks of 40 (left) and 10 (right): items are 350 wide.
        var (carousel, clock) = Create(5, c =>
        {
            c.SnapPointsType = SnapPointsType.Mandatory;
            c.SnapPointsAlignment = alignment;
            c.SetPeekAreaInsets(40, 10);
        });

        Drag(carousel, clock, 390, 150, 1.0);
        carousel.FinishSettling();

        carousel.Position.Should().Be(1);
        StartOf(carousel, 1).Should().BeApproximately(expectedStart, 0.5f,
            "Start puts the item's leading edge at the carousel's, Center its middle at the middle, End its trailing edge at the trailing edge");
    }

    [Fact]
    public void Asymmetric_peek_insets_are_honoured_on_each_side()
    {
        // As on Windows, where PeekAreaInsets pads the carousel's list: the item is the carousel
        // less both peeks, the first item can rest after the leading peek and the last one
        // before the trailing peek, and an item in between rests where its snap alignment puts it.
        var (carousel, _) = Create(3, c => c.SetPeekAreaInsets(40, 10));

        carousel.LeadingPeekAreaInset.Should().Be(40);
        carousel.TrailingPeekAreaInset.Should().Be(10);
        carousel.Items[0].Bounds.Width.Should().BeApproximately(350, 0.5, "the item is the carousel less both peeks");

        carousel.SnapPointsAlignment = SnapPointsAlignment.End;
        carousel.ScrollTo(0, animate: false);
        StartOf(carousel, 0).Should().BeApproximately(40, 0.5f, "the first item after the 40 px leading peek");
        StartOf(carousel, 1).Should().BeApproximately(390, 0.5f, "the next one in the 10 px trailing peek");

        carousel.SnapPointsAlignment = SnapPointsAlignment.Start;
        carousel.ScrollTo(2, animate: false);
        (StartOf(carousel, 2) + 350).Should().BeApproximately(Width - 10, 0.5f, "the last item ends before the 10 px trailing peek");
        StartOf(carousel, 1).Should().BeApproximately(40 - 350, 0.5f, "the previous one shows in the 40 px leading peek");

        carousel.SnapPointsAlignment = SnapPointsAlignment.Center;
        carousel.ScrollTo(1, animate: false);
        StartOf(carousel, 1).Should().BeApproximately(25, 0.5f, "a middle item is centred: (40 + 10) / 2 of each neighbour");
    }

    [Fact]
    public void Bounce_stretches_past_the_first_item_and_springs_back()
    {
        var (carousel, clock) = Create(3, c => c.IsBounceEnabled = true);

        Drag(carousel, clock, 100, 200, 1.0, release: false);
        carousel.ScrollOffset.Should().BeLessThan(0, "the drag stretches past the first item");
        carousel.ScrollOffset.Should().BeGreaterThan(-100, "with resistance: less than the drag");

        carousel.OnPointerReleased(new PointerEventArgs(200, Height / 2, PointerButton.Left));
        carousel.IsSettling.Should().BeTrue("it springs back");
        carousel.RestingOffset.Should().BeApproximately(0, 0.5f);
        carousel.Position.Should().Be(0);
    }

    [Fact]
    public void Bounce_stretches_past_the_last_item_too()
    {
        var (carousel, clock) = Create(3, c => c.IsBounceEnabled = true);
        carousel.ScrollTo(2, animate: false);

        Drag(carousel, clock, 300, 200, 1.0, release: false);
        carousel.ScrollOffset.Should().BeGreaterThan(2 * Width);

        carousel.OnPointerReleased(new PointerEventArgs(200, Height / 2, PointerButton.Left));
        carousel.RestingOffset.Should().BeApproximately(2 * Width, 0.5f);
        carousel.Position.Should().Be(2);
    }

    [Fact]
    public void Without_bounce_the_carousel_stops_at_its_ends()
    {
        var (carousel, clock) = Create(3, c => c.IsBounceEnabled = false);

        Drag(carousel, clock, 100, 200, 1.0, release: false);
        carousel.ScrollOffset.Should().Be(0);
        carousel.OnPointerReleased(new PointerEventArgs(200, Height / 2, PointerButton.Left));

        carousel.ScrollTo(2, animate: false);
        Drag(carousel, clock, 300, 200, 1.0, release: false);
        carousel.ScrollOffset.Should().BeApproximately(2 * Width, 0.5f);
    }

    [Fact]
    public void A_looping_carousel_does_not_bounce()
    {
        var (carousel, clock) = Create(3, c => { c.Loop = true; c.IsBounceEnabled = true; });

        Drag(carousel, clock, 100, 200, 1.0, release: false);
        carousel.ScrollOffset.Should().BeApproximately(-100, 0.5f, "it goes round to the last item, one for one");
    }

    [Fact]
    public void A_looping_carousel_of_two_items_with_a_peek_shows_the_other_on_both_sides()
    {
        var (carousel, _) = Create(2, c => { c.Loop = true; c.PeekAreaInsets = 40; });

        var placements = carousel.VisibleItemPlacements();
        placements.Should().Contain(p => p.Index == 0 && Math.Abs(p.Start - 40) < 0.5, "the current item between the peeks");
        placements.Should().Contain(p => p.Index == 1 && p.Start < 0 && p.Start + 320 > 0, "the other item peeks in before it");
        placements.Should().Contain(p => p.Index == 1 && p.Start > 0 && p.Start < Width, "and after it");
    }

    [Fact]
    public void A_looping_carousel_of_two_items_draws_the_other_in_both_peeks()
    {
        var carousel = new SkiaCarouselView { ShowIndicators = false, Loop = true, PeekAreaInsets = 40 };
        carousel.AddItem(new SkiaBoxView { Color = Colors.Red });
        carousel.AddItem(new SkiaBoxView { Color = Colors.Blue });

        using var bmp = GoldenHarness.Render(carousel, (int)Width, (int)Height, 1f);

        bmp.GetPixel(200, 150).Red.Should().BeGreaterThan(200, "the current item is in the middle");
        var left = bmp.GetPixel(20, 150);
        var right = bmp.GetPixel(380, 150);
        left.Blue.Should().BeGreaterThan(200); left.Red.Should().BeLessThan(60);
        right.Blue.Should().BeGreaterThan(200, "the second item shows in the trailing peek too"); right.Red.Should().BeLessThan(60);
    }
}
