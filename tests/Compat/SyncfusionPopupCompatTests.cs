// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using SkiaSharp;
using Syncfusion.Maui.Popup;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// SfPopup on OpenMaui.Controls.Linux.Syncfusion: Syncfusion's window overlay,
/// empty in the platform-neutral build, shows the popup over the page,
/// centred or next to a view, with its header, message and footer; its
/// buttons work, a press outside closes it unless StaysOpen, and it closes.
/// </summary>
[Collection(CompatHost.Collection)]
public sealed class SyncfusionPopupCompatTests
{
    private static readonly string Frames = Path.Combine(Path.GetTempPath(), "openmaui-popup-frames");

    /// <summary>PopupStyle.PopupBackground's default.</summary>
    private static readonly SKColor PopupBackground = SKColor.Parse("#EEE8F4");

    private static (ContentPage Page, Button Anchor) PageWithAnchor()
    {
        var anchor = new Button
        {
            Text = "Anchor",
            WidthRequest = 120,
            HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(40, 60, 0, 0),
        };
        var page = new ContentPage
        {
            BackgroundColor = Colors.White,
            Content = new Grid { Children = { anchor } },
        };
        return (page, anchor);
    }

    private static SfPopup Popup(bool footer = true) => new()
    {
        HeaderTitle = "Title",
        Message = "Hello from the popup",
        ShowFooter = footer,
        WidthRequest = 300,
        HeightRequest = 200,
        AnimationMode = PopupAnimationMode.None,
    };

    /// <summary>
    /// The host as a running app has it: SfPopup finds the window's page (its
    /// logical parent, which animations and resources resolve through)
    /// through IPlatformApplication.Current, which LinuxApplication.Run sets.
    /// </summary>
    private sealed class PopupHost : IDisposable
    {
        public PopupHost(Page page)
        {
            Host = new CompatHost(page, b => b.UseLinuxSyncfusion(), 800, 600);
            IPlatformApplication.Current = Host.LinuxApp;
        }

        public CompatHost Host { get; }

        public void Dispose()
        {
            if (ReferenceEquals(IPlatformApplication.Current, Host.LinuxApp))
                IPlatformApplication.Current = null;
            Host.Dispose();
        }
    }

    private static void Settle(CompatHost host)
    {
        host.Render();
        Thread.Sleep(100);
        host.Render();
        host.Render();
    }

    private static void Save(CompatHost host, string name)
    {
        Directory.CreateDirectory(Frames);
        host.SaveFrame(Path.Combine(Frames, name + ".png"));
    }

    [Fact]
    public void Show_draws_the_popup_centred_over_the_page()
    {
        var (page, _) = PageWithAnchor();
        using var app = new PopupHost(page);
        var host = app.Host;
        host.Render();

        var popup = Popup();
        popup.Show();
        Settle(host);
        Save(host, "centred");

        popup.IsOpen.Should().BeTrue();
        // The overlay colour dims the page around the popup.
        host.CountPixelsNot(SKColors.White, new SKRectI(0, 0, 100, 100)).Should().BeGreaterThan(9000);
        // The popup's body (PopupBackground) is centred: (250, 200) to (550, 400).
        PopupPixels(host, new SKRectI(260, 210, 540, 390)).Should().BeGreaterThan(30000);
        PopupPixels(host, new SKRectI(0, 0, 240, 600)).Should().Be(0);
    }

    private static void Click(CompatHost host, double x, double y)
    {
        host.DisplayWindow.RaisePointerPressed((float)x, (float)y);
        host.DisplayWindow.RaisePointerReleased((float)x, (float)y);
    }

    /// <summary>
    /// Renders until the open/close animations (300 ms by default) are over,
    /// pumping the animation ticker between frames as the app's run loop does.
    /// </summary>
    private static void SettleAnimation(CompatHost host)
    {
        for (int i = 0; i < 12; i++)
        {
            Thread.Sleep(40);
            Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll();
            host.Render();
        }
        host.Render();
    }

    /// <summary>The bounding box of the popup's background in the last frame.</summary>
    private static SKRectI PopupRect(CompatHost host)
    {
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (int y = 0; y < host.DisplayWindow.Height; y++)
            for (int x = 0; x < host.DisplayWindow.Width; x++)
            {
                var (r, g, b, _) = host.DisplayWindow.PixelAt(x, y);
                if (Math.Abs(r - PopupBackground.Red) + Math.Abs(g - PopupBackground.Green) + Math.Abs(b - PopupBackground.Blue) > 6)
                    continue;
                left = Math.Min(left, x); top = Math.Min(top, y);
                right = Math.Max(right, x + 1); bottom = Math.Max(bottom, y + 1);
            }
        return right < 0 ? SKRectI.Empty : new SKRectI(left, top, right, bottom);
    }

    /// <summary>The accept button in the footer of the popup opened last.</summary>
    private static Syncfusion.Maui.Core.ButtonBase OpenPopupAcceptButton()
    {
        const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
        var open = (System.Collections.IList)typeof(SfPopup).Assembly.GetType("Syncfusion.Maui.Popup.PopupExtension")!
            .GetField("OpenPopups", Any)!.GetValue(null)!;
        var popup = open[open.Count - 1]!;
        var popupView = typeof(SfPopup).GetField("PopupView", Any)!.GetValue(popup)!;
        var footer = popupView.GetType().GetField("FooterView", Any)!.GetValue(popupView)!;
        return (Syncfusion.Maui.Core.ButtonBase)footer.GetType().GetField("AcceptButton", Any)!.GetValue(footer)!;
    }

    private static int PopupPixels(CompatHost host, SKRectI rect) => host.CountPixelsNear(PopupBackground, rect, 6);

    [Fact]
    public void ShowRelativeToView_places_the_popup_below_the_anchor()
    {
        var (page, anchor) = PageWithAnchor();
        using var app = new PopupHost(page);
        var host = app.Host;
        host.Render();

        var popup = Popup(footer: false);
        popup.ShowRelativeToView(anchor, PopupRelativePosition.AlignBottom);
        Settle(host);
        Save(host, "relative");

        // The anchor spans (40, 60) to (160, 100): the popup's top-left is at (40, 100).
        var anchorBounds = CompatHost.RectOf(anchor);
        anchorBounds.Left.Should().Be(40);
        anchorBounds.Bottom.Should().Be(100);
        PopupPixels(host, new SKRectI(50, 110, 330, 290)).Should().BeGreaterThan(40000);
        PopupPixels(host, new SKRectI(0, 0, 800, 95)).Should().Be(0);
        PopupPixels(host, new SKRectI(345, 0, 800, 600)).Should().Be(0);
    }

    [Fact]
    public void The_accept_button_closes_the_popup_and_runs_its_command()
    {
        var (page, _) = PageWithAnchor();
        using var app = new PopupHost(page);
        var host = app.Host;
        host.Render();

        bool accepted = false, closed = false;
        var popup = Popup();
        popup.AcceptCommand = new Command(() => accepted = true);
        popup.Closed += (_, _) => closed = true;
        popup.Show();
        Settle(host);

        // ACCEPT sits at the footer's end: (481, 356) in the centred 300x200 popup.
        Click(host, 481, 356);
        Settle(host);
        Save(host, "accepted");

        accepted.Should().BeTrue();
        popup.IsOpen.Should().BeFalse();
        closed.Should().BeTrue();
        PopupPixels(host, host.WindowRect).Should().Be(0);
        host.CountPixelsNear(SKColors.White, new SKRectI(0, 0, 30, 30)).Should().Be(900, "the overlay colour is gone");
    }

    [Fact]
    public void A_press_outside_closes_the_popup_and_does_not_reach_the_page()
    {
        var (page, anchor) = PageWithAnchor();
        int anchorClicks = 0;
        anchor.Clicked += (_, _) => anchorClicks++;
        using var app = new PopupHost(page);
        var host = app.Host;
        host.Render();

        var popup = Popup();
        popup.Show();
        Settle(host);

        // A press on the popup itself keeps it open.
        Click(host, 300, 280);
        Settle(host);
        popup.IsOpen.Should().BeTrue();

        // The anchor is under the overlay: the press closes the popup instead.
        host.Tap(anchor);
        Settle(host);

        popup.IsOpen.Should().BeFalse();
        anchorClicks.Should().Be(0);
        PopupPixels(host, host.WindowRect).Should().Be(0);

        // With the popup gone, the page takes presses again.
        host.Tap(anchor);
        anchorClicks.Should().Be(1);
    }

    [Fact]
    public void StaysOpen_keeps_the_popup_open_on_a_press_outside()
    {
        var (page, _) = PageWithAnchor();
        using var app = new PopupHost(page);
        var host = app.Host;
        host.Render();

        var popup = Popup();
        popup.StaysOpen = true;
        popup.Show();
        Settle(host);

        Click(host, 700, 550);
        Settle(host);

        popup.IsOpen.Should().BeTrue();
        PopupPixels(host, new SKRectI(260, 210, 540, 390)).Should().BeGreaterThan(30000);

        popup.Dismiss();
        Settle(host);
        popup.IsOpen.Should().BeFalse();
        PopupPixels(host, host.WindowRect).Should().Be(0);
    }

    [Fact]
    public void Without_the_overlay_the_page_shows_and_a_press_outside_still_closes()
    {
        var (page, _) = PageWithAnchor();
        using var app = new PopupHost(page);
        var host = app.Host;
        host.Render();

        var popup = Popup();
        popup.ShowOverlayAlways = false;
        popup.Show();
        Settle(host);

        host.CountPixelsNear(SKColors.White, new SKRectI(0, 0, 30, 30)).Should().Be(900, "no overlay colour");
        PopupPixels(host, new SKRectI(260, 210, 540, 390)).Should().BeGreaterThan(30000);

        Click(host, 700, 550);
        Settle(host);
        popup.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void The_open_and_close_animations_run_to_completion()
    {
        var (page, _) = PageWithAnchor();
        using var app = new PopupHost(page);
        var host = app.Host;
        host.Render();

        var popup = Popup();
        popup.AnimationMode = PopupAnimationMode.Zoom;
        popup.Show();
        SettleAnimation(host);
        Save(host, "zoomed-in");

        PopupPixels(host, new SKRectI(260, 210, 540, 390)).Should().BeGreaterThan(30000);
        host.CountPixelsNot(SKColors.White, new SKRectI(0, 0, 100, 100)).Should().BeGreaterThan(9000);

        popup.IsOpen = false;
        SettleAnimation(host);
        Save(host, "zoomed-out");

        PopupPixels(host, host.WindowRect).Should().Be(0);
        host.CountPixelsNear(SKColors.White, new SKRectI(0, 0, 30, 30)).Should().Be(900);
    }

    [Fact]
    public void Content_template_views_take_input()
    {
        var (page, _) = PageWithAnchor();
        using var app = new PopupHost(page);
        var host = app.Host;
        host.Render();

        int clicks = 0;
        Button? inner = null;
        var popup = Popup(footer: false);
        popup.ContentTemplate = new DataTemplate(() =>
        {
            inner = new Button { Text = "Inside", WidthRequest = 120, HeightRequest = 40, BackgroundColor = Colors.Red };
            inner.Clicked += (_, _) => clicks++;
            return new Grid { Children = { inner } };
        });
        popup.Show();
        Settle(host);
        Save(host, "template");

        inner.Should().NotBeNull();
        var (x, y) = CompatHost.CenterOf(inner!);
        // The 120 px button keeps its width (centred in the Grid, as MAUI places an explicit size).
        host.CountPixelsNear(SKColors.Red, new SKRectI((int)x - 55, (int)y - 5, (int)x - 25, (int)y + 5)).Should().Be(300);
        Click(host, x, y);
        Settle(host);

        clicks.Should().Be(1);
        popup.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void A_popup_declared_in_the_page_opens_through_IsOpen()
    {
        var popup = Popup();
        var page = new ContentPage
        {
            BackgroundColor = Colors.White,
            Content = new Grid { Children = { new Label { Text = "Page" }, popup } },
        };
        using var app = new PopupHost(page);
        var host = app.Host;
        host.Render();
        PopupPixels(host, host.WindowRect).Should().Be(0);

        popup.IsOpen = true;
        Settle(host);

        PopupPixels(host, new SKRectI(260, 210, 540, 390)).Should().BeGreaterThan(30000);
    }

    [Fact]
    public async Task The_static_Show_returns_the_button_pressed()
    {
        var (page, _) = PageWithAnchor();
        using var app = new PopupHost(page);
        var host = app.Host;
        host.Render();

        var result = SfPopup.Show("Delete", "Delete the file?", "YES", "NO");
        SettleAnimation(host);
        Save(host, "static");
        result.IsCompleted.Should().BeFalse();

        PopupRect(host).Width.Should().BeGreaterThan(100);
        var accept = OpenPopupAcceptButton();
        accept.Text.Should().Be("YES");
        var (x, y) = CompatHost.CenterOf(accept);
        Click(host, x, y);
        SettleAnimation(host);

        (await result.WaitAsync(TimeSpan.FromSeconds(1))).Should().BeTrue();
    }

    [Fact]
    public void A_two_button_footer_places_decline_then_accept_inside_the_popup()
    {
        var (page, _) = PageWithAnchor();
        using var app = new PopupHost(page);
        var host = app.Host;
        host.Render();

        _ = SfPopup.Show("Delete", "Delete the file?", "YES", "NO");
        SettleAnimation(host);

        // The footer builds its columns into its grid after the grid is shown.
        var accept = OpenPopupAcceptButton();
        var grid = (Grid)accept.Parent;
        var decline = grid.Children.OfType<Syncfusion.Maui.Core.ButtonBase>().Single(b => !ReferenceEquals(b, accept));
        decline.Text.Should().Be("NO");
        var gridBounds = ((Microsoft.Maui.Platform.SkiaView)grid.Handler!.PlatformView!).Bounds;
        var a = ((Microsoft.Maui.Platform.SkiaView)accept.Handler!.PlatformView!).Bounds;
        var d = ((Microsoft.Maui.Platform.SkiaView)decline.Handler!.PlatformView!).Bounds;
        a.Left.Should().BeApproximately(d.Right + 8, 0.5);
        a.Right.Should().BeLessThan(gridBounds.Right);
    }

    [Fact]
    public void An_open_popup_recentres_when_the_window_resizes()
    {
        var (page, _) = PageWithAnchor();
        using var app = new PopupHost(page);
        var host = app.Host;
        host.Render();

        var popup = Popup();
        popup.Show();
        Settle(host);
        PopupPixels(host, new SKRectI(260, 210, 540, 390)).Should().BeGreaterThan(30000);

        host.DisplayWindow.RaiseResized(1000, 800);
        Settle(host);
        Save(host, "resized");

        // Centred in 1000x800: (350, 300) to (650, 500).
        PopupPixels(host, new SKRectI(360, 310, 640, 490)).Should().BeGreaterThan(30000);
        PopupPixels(host, new SKRectI(0, 0, 340, 800)).Should().Be(0);
        host.CountPixelsNot(SKColors.White, new SKRectI(900, 700, 1000, 800)).Should().Be(10000, "the overlay covers the grown window");
    }

    [Fact]
    public void The_blur_overlay_blurs_the_page_and_takes_presses()
    {
        var (page, anchor) = PageWithAnchor();
        int anchorClicks = 0;
        anchor.Clicked += (_, _) => anchorClicks++;
        using var app = new PopupHost(page);
        var host = app.Host;
        host.Render();
        var label = CompatHost.RectOf(anchor);
        int sharpText = host.CountPixelsNear(new SKColor(30, 30, 30), label, 60);
        sharpText.Should().BeGreaterThan(20);

        var popup = Popup();
        popup.OverlayMode = PopupOverlayMode.Blur;
        popup.PopupStyle = new PopupStyle { BlurIntensity = PopupBlurIntensity.ExtraDark };
        popup.Show();
        Settle(host);
        Save(host, "blur");

        host.CountPixelsNear(new SKColor(30, 30, 30), label, 60).Should().BeLessThan(sharpText / 4, "the anchor's text is blurred");
        PopupPixels(host, new SKRectI(260, 210, 540, 390)).Should().BeGreaterThan(30000);

        host.Tap(anchor);
        Settle(host);
        anchorClicks.Should().Be(0);
        popup.IsOpen.Should().BeFalse();
        host.CountPixelsNear(new SKColor(30, 30, 30), label, 60).Should().Be(sharpText, "the blur goes with the popup");
    }

    [Fact]
    public void HasShadow_draws_a_shadow_around_the_popup()
    {
        var (page, _) = PageWithAnchor();
        using var app = new PopupHost(page);
        var host = app.Host;
        host.Render();

        var popup = Popup();
        popup.ShowOverlayAlways = false;
        popup.PopupStyle = new PopupStyle { HasShadow = true };
        popup.Show();
        Settle(host);
        Save(host, "shadow");

        // Just below the popup's bottom edge (y = 400), clear of its rounded corners.
        host.CountPixelsNot(SKColors.White, new SKRectI(300, 402, 500, 408)).Should().BeGreaterThan(1000);

        popup.PopupStyle.HasShadow = false;
        popup.IsOpen = false;
        Settle(host);
        popup.Show();
        Settle(host);
        host.CountPixelsNot(SKColors.White, new SKRectI(300, 402, 500, 408)).Should().Be(0);
    }
}
