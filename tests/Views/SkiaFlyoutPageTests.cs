// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using PointerButton = Microsoft.Maui.Platform.PointerButton;
using FlyoutLayoutBehavior = Microsoft.Maui.Platform.FlyoutLayoutBehavior;

/// <summary>
/// SkiaFlyoutPage on its own (presentation, scrim, split vs popover layout,
/// edge-drag gesture) and through the FlyoutPageHandler with a MAUI
/// FlyoutPage (IsPresented both ways, Detail replacement, layout behavior).
/// </summary>
[Collection(HeadlessMaui.Collection)]
public class SkiaFlyoutPageTests
{
    private sealed class ColorView : SkiaView
    {
        private readonly SKColor _color;
        public ColorView(SKColor color) { _color = color; }
        protected override void OnDraw(SKCanvas canvas, SKRect bounds)
        {
            using var paint = new SKPaint { Color = _color, Style = SKPaintStyle.Fill };
            canvas.DrawRect(bounds, paint);
        }
    }

    private static SkiaFlyoutPage CreatePage(out ColorView flyout, out ColorView detail)
    {
        flyout = new ColorView(SKColors.Green);
        detail = new ColorView(SKColors.Blue);
        var page = new SkiaFlyoutPage { Flyout = flyout, Detail = detail, FlyoutWidth = 200 };
        Layout(page);
        return page;
    }

    private static void Layout(SkiaFlyoutPage page)
    {
        page.Measure(new Size(600, 400));
        page.Arrange(new Rect(0, 0, 600, 400));
    }

    private static SKBitmap Render(SkiaView view)
    {
        var bitmap = new SKBitmap(600, 400);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        view.Draw(canvas);
        return bitmap;
    }

    #region Platform view

    [Fact]
    public void Defaults_are_closed_popover()
    {
        var page = new SkiaFlyoutPage();

        page.IsPresented.Should().BeFalse();
        page.IsSplit.Should().BeFalse();
        page.GestureEnabled.Should().BeTrue();
        page.FlyoutLayoutBehavior.Should().Be(FlyoutLayoutBehavior.Default);
    }

    [Fact]
    public void IsPresented_toggles_the_flyout_and_raises_IsPresentedChanged()
    {
        var page = CreatePage(out var flyout, out _);
        int changed = 0;
        page.IsPresentedChanged += (s, e) => changed++;

        page.IsPresented = true;
        Layout(page);
        flyout.Bounds.Left.Should().Be(0, "a presented flyout sits at the left edge");

        page.IsPresented = false;
        Layout(page);
        flyout.Bounds.Left.Should().Be(-200, "a closed flyout is parked off-screen");

        changed.Should().Be(2);
    }

    [Fact]
    public void Presented_flyout_is_drawn_over_the_detail()
    {
        var page = CreatePage(out _, out _);

        using (var closed = Render(page))
            closed.GetPixel(100, 200).Should().Be(SKColors.Blue);

        page.IsPresented = true;
        Layout(page);

        using var open = Render(page);
        open.GetPixel(100, 200).Should().Be(SKColors.Green);
        // The detail beyond the flyout is dimmed by the scrim, not hidden.
        var scrimmed = open.GetPixel(500, 200);
        scrimmed.Blue.Should().BeGreaterThan(scrimmed.Red);
        scrimmed.Should().NotBe(SKColors.Blue);
    }

    [Fact]
    public void Tap_on_the_scrim_closes_the_flyout()
    {
        var page = CreatePage(out _, out _);
        page.IsPresented = true;
        Layout(page);

        page.HitTest(500, 200).Should().BeSameAs(page, "the scrim belongs to the flyout page");
        var e = new PointerEventArgs(500, 200, PointerButton.Left);
        page.OnPointerPressed(e);

        page.IsPresented.Should().BeFalse();
        e.Handled.Should().BeTrue();
    }

    [Fact]
    public void Tap_inside_the_open_flyout_reaches_the_flyout_content()
    {
        var page = CreatePage(out var flyout, out _);
        page.IsPresented = true;
        Layout(page);

        page.HitTest(100, 200).Should().BeSameAs(flyout);
    }

    [Fact]
    public void Detail_replacement_swaps_the_child_view()
    {
        var page = CreatePage(out _, out var detail);
        var replacement = new ColorView(SKColors.Red);

        page.Detail = replacement;
        Layout(page);

        page.Detail.Should().BeSameAs(replacement);
        page.Children.Should().Contain(replacement).And.NotContain(detail);
        using var bitmap = Render(page);
        bitmap.GetPixel(300, 200).Should().Be(SKColors.Red);
    }

    [Fact]
    public void Split_layout_pins_the_flyout_and_offsets_the_detail()
    {
        var page = CreatePage(out var flyout, out var detail);

        page.FlyoutLayoutBehavior = FlyoutLayoutBehavior.Split;
        Layout(page);

        page.IsSplit.Should().BeTrue();
        page.IsPresented.Should().BeTrue();
        flyout.Bounds.Left.Should().Be(0);
        detail.Bounds.Left.Should().Be(200);
        detail.Bounds.Width.Should().Be(400);

        using var bitmap = Render(page);
        bitmap.GetPixel(100, 200).Should().Be(SKColors.Green);
        bitmap.GetPixel(500, 200).Should().Be(SKColors.Blue, "split layout draws no scrim");
    }

    [Fact]
    public void Split_layout_cannot_be_closed_by_IsPresented_or_scrim_taps()
    {
        var page = CreatePage(out _, out var detail);
        page.FlyoutLayoutBehavior = FlyoutLayoutBehavior.Split;
        Layout(page);

        page.IsPresented = false;
        page.IsPresented.Should().BeTrue();

        page.HitTest(500, 200).Should().BeSameAs(detail, "there is no scrim to hit in split layout");
        page.OnPointerPressed(new PointerEventArgs(500, 200, PointerButton.Left));
        page.IsPresented.Should().BeTrue();
    }

    [Fact]
    public void Popover_layout_gives_the_detail_the_full_width()
    {
        var page = CreatePage(out _, out var detail);
        page.FlyoutLayoutBehavior = FlyoutLayoutBehavior.Split;
        Layout(page);

        page.FlyoutLayoutBehavior = FlyoutLayoutBehavior.Popover;
        Layout(page);

        page.IsSplit.Should().BeFalse();
        detail.Bounds.Left.Should().Be(0);
        detail.Bounds.Width.Should().Be(600);
    }

    [Fact]
    public void Edge_drag_opens_the_flyout()
    {
        var page = CreatePage(out _, out _);
        int changed = 0;
        page.IsPresentedChanged += (s, e) => changed++;

        page.OnPointerPressed(new PointerEventArgs(10, 200, PointerButton.Left));
        page.OnPointerMoved(new PointerEventArgs(180, 200));
        page.OnPointerReleased(new PointerEventArgs(180, 200, PointerButton.Left));

        page.IsPresented.Should().BeTrue();
        changed.Should().Be(1);
    }

    [Fact]
    public void Short_edge_drag_snaps_back_closed()
    {
        var page = CreatePage(out _, out _);

        page.OnPointerPressed(new PointerEventArgs(10, 200, PointerButton.Left));
        page.OnPointerMoved(new PointerEventArgs(60, 200));
        page.OnPointerReleased(new PointerEventArgs(60, 200, PointerButton.Left));

        page.IsPresented.Should().BeFalse();
    }

    [Fact]
    public void Drag_from_the_middle_does_not_open_the_flyout()
    {
        var page = CreatePage(out _, out _);

        page.OnPointerPressed(new PointerEventArgs(300, 200, PointerButton.Left));
        page.OnPointerMoved(new PointerEventArgs(550, 200));
        page.OnPointerReleased(new PointerEventArgs(550, 200, PointerButton.Left));

        page.IsPresented.Should().BeFalse();
    }

    [Fact]
    public void Drag_left_closes_the_open_flyout()
    {
        var page = CreatePage(out _, out _);
        page.IsPresented = true;
        Layout(page);

        page.OnPointerPressed(new PointerEventArgs(150, 200, PointerButton.Left));
        page.OnPointerMoved(new PointerEventArgs(20, 200));
        page.OnPointerReleased(new PointerEventArgs(20, 200, PointerButton.Left));

        page.IsPresented.Should().BeFalse();
    }

    [Fact]
    public void Edge_drag_is_ignored_when_gestures_are_disabled()
    {
        var page = CreatePage(out _, out _);
        page.GestureEnabled = false;

        page.OnPointerPressed(new PointerEventArgs(10, 200, PointerButton.Left));
        page.OnPointerMoved(new PointerEventArgs(180, 200));
        page.OnPointerReleased(new PointerEventArgs(180, 200, PointerButton.Left));

        page.IsPresented.Should().BeFalse();
    }

    #endregion

    #region Handler

    private static (FlyoutPage page, SkiaFlyoutPage platform) CreateMauiFlyoutPage(
        Microsoft.Maui.Controls.FlyoutLayoutBehavior behavior = Microsoft.Maui.Controls.FlyoutLayoutBehavior.Popover)
    {
        var ctx = HeadlessMaui.CreateContext();
        var page = new FlyoutPage
        {
            Flyout = new ContentPage { Title = "Menu", Content = new Label { Text = "Menu" } },
            Detail = new ContentPage { Title = "Detail", Content = new Label { Text = "Detail" } },
            FlyoutLayoutBehavior = behavior,
        };
        var handler = (FlyoutPageHandler)Microsoft.Maui.Platform.Linux.Hosting.MauiHandlerExtensions.ToHandler(page, ctx);
        var platform = (SkiaFlyoutPage)handler.PlatformView!;
        platform.Measure(new Size(600, 400));
        platform.Arrange(new Rect(0, 0, 600, 400));
        return (page, platform);
    }

    [Fact]
    public void Handler_renders_flyout_and_detail_pages()
    {
        var (page, platform) = CreateMauiFlyoutPage();

        platform.Flyout.Should().BeSameAs(page.Flyout.Handler!.PlatformView);
        platform.Detail.Should().BeSameAs(page.Detail.Handler!.PlatformView);
    }

    [Fact]
    public void Handler_IsPresented_flows_from_MAUI_to_the_platform()
    {
        var (page, platform) = CreateMauiFlyoutPage();

        page.IsPresented = true;
        platform.IsPresented.Should().BeTrue();

        page.IsPresented = false;
        platform.IsPresented.Should().BeFalse();
    }

    [Fact]
    public void Handler_IsPresented_flows_from_the_platform_to_MAUI()
    {
        var (page, platform) = CreateMauiFlyoutPage();
        int changed = 0;
        page.IsPresentedChanged += (s, e) => changed++;

        platform.IsPresented = true;

        page.IsPresented.Should().BeTrue();
        changed.Should().Be(1);
    }

    [Fact]
    public void Handler_Detail_replacement_presents_the_new_page()
    {
        var (page, platform) = CreateMauiFlyoutPage();
        var newDetail = new ContentPage { Title = "Other", Content = new Label { Text = "Other" } };

        page.Detail = newDetail;

        platform.Detail.Should().BeSameAs(newDetail.Handler!.PlatformView);
    }

    [Fact]
    public void Handler_maps_FlyoutLayoutBehavior()
    {
        var (page, platform) = CreateMauiFlyoutPage();

        page.FlyoutLayoutBehavior = Microsoft.Maui.Controls.FlyoutLayoutBehavior.Split;
        platform.IsSplit.Should().BeTrue();
        platform.IsPresented.Should().BeTrue();
        page.IsPresented.Should().BeTrue();

        page.FlyoutLayoutBehavior = Microsoft.Maui.Controls.FlyoutLayoutBehavior.Popover;
        platform.IsSplit.Should().BeFalse();
    }

    [Fact]
    public void Handler_Default_behavior_follows_MAUI_ShouldShowSplitMode()
    {
        // On the desktop idiom MAUI's Default means split (and it refuses
        // IsPresented = false); the platform must agree with MAUI here.
        var (page, platform) = CreateMauiFlyoutPage(Microsoft.Maui.Controls.FlyoutLayoutBehavior.Default);

        platform.IsSplit.Should().Be(((IFlyoutPageController)page).ShouldShowSplitMode);
    }

    [Fact]
    public void Handler_maps_IsGestureEnabled()
    {
        var (page, platform) = CreateMauiFlyoutPage();

        page.IsGestureEnabled = false;

        platform.GestureEnabled.Should().BeFalse();
    }

    [Fact]
    public void Handler_edge_drag_opens_the_flyout_and_updates_MAUI()
    {
        var (page, platform) = CreateMauiFlyoutPage();

        platform.OnPointerPressed(new PointerEventArgs(10, 200, PointerButton.Left));
        platform.OnPointerMoved(new PointerEventArgs(400, 200));
        platform.OnPointerReleased(new PointerEventArgs(400, 200, PointerButton.Left));

        platform.IsPresented.Should().BeTrue();
        page.IsPresented.Should().BeTrue();
    }

    #endregion
}
