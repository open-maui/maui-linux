// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

using PointerButton = Microsoft.Maui.Platform.PointerButton;

/// <summary>
/// The window's chrome as on Windows: MAUI's window overlays (Window.AddOverlay and the visual
/// diagnostics adorners) draw over the page and get its taps, and Window.TitleBar is shown in a
/// strip above the page, which starts below it.
/// </summary>
[Collection("LinuxApplication.Current")]
public class WindowChromeTests
{
    public WindowChromeTests() => WindowOverlayPatches.Install();

    /// <summary>An overlay element that fills a rectangle.</summary>
    private sealed class BoxElement : IWindowOverlayElement
    {
        public Rect Box { get; init; }
        public Color Color { get; init; } = Colors.Red;
        public bool Contains(Point point) => Box.Contains(point);
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.FillColor = Color;
            canvas.FillRectangle(Box);
        }
    }

    private static bool IsRed((byte R, byte G, byte B, byte A) p) => p.R > 200 && p.G < 60 && p.B < 60;

    private static bool IsWhite((byte R, byte G, byte B, byte A) p) => p.R > 240 && p.G > 240 && p.B > 240;

    [Fact]
    public void An_added_overlay_draws_over_the_page_and_stops_when_removed()
    {
        using var host = new HeadlessMauiHost(new ContentPage { BackgroundColor = Colors.White }, withEngine: true);
        host.Context.Render();
        IsWhite(host.DisplayWindow.PixelAt(60, 60)).Should().BeTrue();

        var overlay = new WindowOverlay(host.Window);
        overlay.AddWindowElement(new BoxElement { Box = new Rect(40, 40, 50, 50) });
        host.Window.AddOverlay(overlay).Should().BeTrue();
        host.Context.Render();

        IsRed(host.DisplayWindow.PixelAt(60, 60)).Should().BeTrue("the overlay's element is drawn over the page");
        IsWhite(host.DisplayWindow.PixelAt(120, 120)).Should().BeTrue("only the element is drawn");

        overlay.IsVisible = false;
        host.Context.Render();
        IsWhite(host.DisplayWindow.PixelAt(60, 60)).Should().BeTrue("a hidden overlay draws nothing");

        overlay.IsVisible = true;
        host.Context.Render();
        IsRed(host.DisplayWindow.PixelAt(60, 60)).Should().BeTrue();

        host.Window.RemoveOverlay(overlay).Should().BeTrue();
        host.Context.Render();
        IsWhite(host.DisplayWindow.PixelAt(60, 60)).Should().BeTrue("a removed overlay is gone");
    }

    [Fact]
    public void Taps_reach_the_overlay_and_a_touch_handling_overlay_keeps_them_from_the_page()
    {
        int clicks = 0;
        var button = new Button { Text = "Hit", WidthRequest = 200, HeightRequest = 60,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        button.Clicked += (_, _) => clicks++;
        using var host = new HeadlessMauiHost(new ContentPage { BackgroundColor = Colors.White, Content = button }, withEngine: true);
        host.Context.Render();

        var overlay = new WindowOverlay(host.Window);
        var element = new BoxElement { Box = new Rect(0, 0, 50, 50) };
        overlay.AddWindowElement(element);
        host.Window.AddOverlay(overlay);
        var taps = new List<WindowOverlayTappedEventArgs>();
        overlay.Tapped += (_, e) => taps.Add(e);
        host.Context.Render();

        // A tap outside the element: the overlay hears it, the page gets it.
        host.DisplayWindow.RaisePointerPressed(150, 30);
        host.DisplayWindow.RaisePointerReleased(150, 30);
        taps.Should().HaveCount(1);
        taps[0].Point.Should().Be(new Point(150, 30));
        taps[0].WindowOverlayElements.Should().BeEmpty("drawable touch handling is off");
        clicks.Should().Be(1);

        // With drawable touch handling, a tap on the element is the overlay's alone.
        overlay.EnableDrawableTouchHandling = true;
        host.DisplayWindow.RaisePointerPressed(20, 20);
        host.DisplayWindow.RaisePointerReleased(20, 20);
        taps.Should().HaveCount(2);
        taps[1].WindowOverlayElements.Should().ContainSingle().Which.Should().BeSameAs(element);
        clicks.Should().Be(1, "the element took the tap");

        // DisableUITouchEventPassthrough: every tap is the overlay's.
        overlay.EnableDrawableTouchHandling = false;
        overlay.DisableUITouchEventPassthrough = true;
        host.DisplayWindow.RaisePointerPressed(150, 30);
        host.DisplayWindow.RaisePointerReleased(150, 30);
        taps.Should().HaveCount(3);
        taps[2].VisualTreeElements.Should().Contain(button, "MAUI lists the views under the tap");
        clicks.Should().Be(1);
    }

    [Fact]
    public void The_visual_diagnostics_overlay_draws_an_adorner_over_the_view_where_it_is()
    {
        var label = new Label { Text = "Adorned", WidthRequest = 100, HeightRequest = 40, TranslationX = 30,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(20, 50, 0, 0) };
        using var host = new HeadlessMauiHost(new ContentPage { BackgroundColor = Colors.White, Content = label }, withEngine: true);
        host.Context.Render();

        var diagnostics = (VisualDiagnosticsOverlay)host.Window.VisualDiagnosticsOverlay;
        diagnostics.IsPlatformViewInitialized.Should().BeTrue("the window handler initializes it with the window's content, as on Windows");
        diagnostics.AddAdorner(new RectangleAdorner(label, fillColor: Colors.Blue, strokeColor: Colors.Blue)).Should().BeTrue();
        host.Context.Render();

        // The label sits at (20, 50) and is drawn 30 to the right (TranslationX).
        var box = WindowOverlayPatches.GetBoundingBox(label);
        box.Left.Should().BeApproximately(50, 1);
        box.Top.Should().BeApproximately(50, 1);
        var inside = host.DisplayWindow.PixelAt(100, 70);
        (inside.B > 200 && inside.R < 60).Should().BeTrue($"the adorner covers the view, got {inside}");
        IsWhite(host.DisplayWindow.PixelAt(35, 70)).Should().BeTrue("the adorner follows the translated view");

        diagnostics.RemoveAdorners();
        host.Context.Render();
        var after = host.DisplayWindow.PixelAt(100, 70);
        (after.B > 200 && after.R < 60).Should().BeFalse("the adorner is gone");
    }

    [Fact]
    public void MAUI_view_geometry_helpers_read_the_Skia_view()
    {
        var label = new Label { Text = "x", WidthRequest = 40, HeightRequest = 20, Scale = 2,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(100, 100, 0, 0) };
        using var host = new HeadlessMauiHost(new ContentPage { Content = label }, withEngine: true);
        host.Context.Render();

        var transform = WindowOverlayPatches.GetViewTransform(label);
        transform.M11.Should().BeApproximately(2, 0.001f);
        transform.M22.Should().BeApproximately(2, 0.001f);
        var box = WindowOverlayPatches.GetBoundingBox(label);
        box.Width.Should().BeApproximately(80, 0.5);
        box.Left.Should().BeApproximately(80, 0.5, "scaled about its centre");
        WindowOverlayPatches.GetHostedWindow(label).Should().BeSameAs(host.Window);
        WindowOverlayPatches.GetHostedWindow(new Label()).Should().BeNull();
    }

    [Fact]
    public void Window_TitleBar_is_shown_above_the_page_and_takes_its_presses()
    {
        int clicks = 0;
        var titleButton = new Button { Text = "Go", WidthRequest = 80 };
        titleButton.Clicked += (_, _) => clicks++;
        var page = new ContentPage { BackgroundColor = Colors.White };
        using var host = new HeadlessMauiHost(page, withEngine: true);
        host.Context.Render();
        page.Height.Should().BeApproximately(600, 1);

        host.Window.TitleBar = new TitleBar
        {
            HeightRequest = 48,
            Title = "My app",
            Content = new BoxView { Color = Colors.Red },
            TrailingContent = titleButton,
        };
        host.Context.Render();
        host.Context.Render();
        var skiaWindow = (WindowHandler)host.Window.Handler!;
        skiaWindow.PlatformView.TitleBar.Should().NotBeNull("the window handler realizes the TitleBar");
        var tbView = skiaWindow.PlatformView.TitleBar!;
        tbView.Bounds.Height.Should().BeApproximately(48, 1, $"type {tbView.GetType().Name} visible {tbView.IsVisible} bounds {tbView.Bounds}");

        IsRed(host.DisplayWindow.PixelAt(200, 10)).Should().BeTrue("the TitleBar's content fills the strip above the page");
        IsWhite(host.DisplayWindow.PixelAt(200, 60)).Should().BeTrue("the page starts below it");
        page.Height.Should().BeApproximately(552, 1, "the page gets the window minus the title bar");

        var buttonView = (SkiaView)titleButton.Handler!.PlatformView!;
        var bounds = buttonView.Bounds;
        bounds.Bottom.Should().BeLessThanOrEqualTo(0, "the TitleBar is laid out above the page's origin");
        // Window pixels: the strip is the top 48.
        float x = (float)bounds.Center.X, y = (float)bounds.Center.Y + 48;
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        clicks.Should().Be(1, "the TitleBar's content takes its presses");

        // With client-side decorations, the TitleBar's leading, main and trailing content take
        // presses (Windows' passthrough region) and the rest of the bar moves the window.
        host.Context.IsTitleBarPassthrough(x, y).Should().BeTrue("the trailing content is interactive");
        host.Context.IsTitleBarPassthrough(20, 10).Should().BeFalse("the title area moves the window");

        ((TitleBar)host.Window.TitleBar!).IsVisible = false;
        host.Context.Render();
        host.Context.Render();
        IsWhite(host.DisplayWindow.PixelAt(200, 10)).Should().BeTrue("a hidden TitleBar takes no space");
        page.Height.Should().BeApproximately(600, 1);
    }
}
