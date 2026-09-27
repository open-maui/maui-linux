// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// The shape of MASpotlightTour's corner navigator: round Borders holding
/// centred Labels, and a step counter whose text changes after first layout.
/// </summary>
[Collection("LinuxApplication.Current")]
public class NavigatorShapeLayoutTests
{
    private static Rect BoundsOf(View v) => ((SkiaView)v.Handler!.PlatformView!).Bounds;

    [Fact]
    public void A_centred_label_in_a_border_is_centred()
    {
        var label = new Label { Text = "Done", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        var border = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            WidthRequest = 120,
            HeightRequest = 60,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Content = label,
        };

        using var host = new HeadlessMauiHost(new ContentPage { Content = border }, withEngine: true);
        host.Context.Render();

        var b = BoundsOf(border);
        var l = BoundsOf(label);
        l.Width.Should().BeLessThan(b.Width);
        l.Height.Should().BeLessThan(b.Height);
        l.Center.X.Should().BeApproximately(b.Center.X, 1);
        l.Center.Y.Should().BeApproximately(b.Center.Y, 1);
    }

    [Fact]
    public void A_label_whose_text_grows_after_layout_is_remeasured()
    {
        var counter = new Label { Text = "", VerticalOptions = LayoutOptions.Center };
        var stack = new HorizontalStackLayout
        {
            Spacing = 4,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Children = { new BoxView { WidthRequest = 40, HeightRequest = 40 }, counter, new BoxView { WidthRequest = 40, HeightRequest = 40 } },
        };

        using var host = new HeadlessMauiHost(new ContentPage { Content = stack }, withEngine: true);
        host.Context.Render();

        counter.Text = "1 / 2";
        host.Context.Render();

        var l = BoundsOf(counter);
        l.Height.Should().BeLessThan(30, "the counter stays on one line");
        l.Width.Should().BeGreaterThan(20);
    }

    [Fact]
    public void A_counter_in_a_border_shown_after_layout_stays_on_one_line()
    {
        var counter = new Label { Text = "", FontSize = 14, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center };
        var navigator = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 25 },
            Padding = new Thickness(6),
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            IsVisible = false,
            Content = new HorizontalStackLayout
            {
                Spacing = 4,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Border { WidthRequest = 36, HeightRequest = 36, Content = new Label { Text = "<", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center } },
                    counter,
                    new Border { WidthRequest = 36, HeightRequest = 36, Content = new Label { Text = ">", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center } },
                },
            },
        };
        var host = new Grid { navigator };

        using var app = new HeadlessMauiHost(new ContentPage { Content = host }, withEngine: true);
        app.Context.Render();

        counter.Text = "1 / 2";
        navigator.IsVisible = true;
        app.Context.Render();

        var l = BoundsOf(counter);
        l.Height.Should().BeLessThan(30, "the counter stays on one line");
        l.Width.Should().BeGreaterThan(20);
    }

    [Fact]
    public void An_unconstrained_MAUI_measure_of_a_realised_border_reports_its_content()
    {
        // The stat-card pattern: reset the requests, measure unconstrained,
        // then pin every card to the largest size.
        var card = new Border
        {
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children = { new BoxView { WidthRequest = 32, HeightRequest = 32 }, new Label { Text = "20", FontSize = 36 }, new Label { Text = "Repositories" } },
            },
        };
        var grid = new Grid { card };
        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true);
        host.Context.Render();

        card.WidthRequest = -1;
        card.HeightRequest = -1;
#pragma warning disable CS0618
        var size = card.Measure(double.PositiveInfinity, double.PositiveInfinity);
#pragma warning restore CS0618

        size.Height.Should().BeGreaterThan(100, "32 + 8 + a 36pt line + 8 + a line, plus 32 of padding");
        size.Width.Should().BeGreaterThan(60);
    }

    [Fact]
    public void MAUI_frames_are_relative_to_the_parent()
    {
        // Apps sum X/Y up the tree to find a view's window position (popups,
        // menus); that only works when each frame is parent-relative.
        var label = new Label { Text = "x", HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        var inner = new Grid { Padding = new Thickness(10, 20, 0, 0), Children = { label } };
        var outer = new Grid { Padding = new Thickness(30, 40, 0, 0), Children = { inner } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = outer }, withEngine: true);
        host.Context.Render();

        label.Frame.X.Should().BeApproximately(10, 0.5);
        label.Frame.Y.Should().BeApproximately(20, 0.5);
        inner.Frame.X.Should().BeApproximately(30, 0.5);
        var abs = ((SkiaView)label.Handler!.PlatformView!).Bounds;
        (label.Frame.X + inner.Frame.X + outer.Frame.X).Should().BeApproximately(abs.X, 0.5);
    }

    [Fact]
    public void A_BoxView_coloured_by_BackgroundColor_paints_over_its_sibling()
    {
        // MALayout's collapse strip: a full-size BoxView, then a small centred
        // grip BoxView coloured through BackgroundColor with a CornerRadius.
        var strip = new BoxView { BackgroundColor = Color.FromArgb("#383838") };
        var grip = new BoxView
        {
            WidthRequest = 6, HeightRequest = 40, CornerRadius = 2,
            BackgroundColor = Color.FromArgb("#808080"),
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
        };
        var grid = new Grid { WidthRequest = 24, HeightRequest = 200, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Children = { strip, grip } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true);
        host.Context.Render();

        var b = ((SkiaView)grip.Handler!.PlatformView!).Bounds;
        var (r, g, bl, _) = host.DisplayWindow.PixelAt((int)b.Center.X, (int)b.Center.Y);
        ((int)r).Should().BeInRange(0x70, 0x90, "the grip's grey, not the strip's");
    }

    [Fact]
    public void A_start_aligned_truncating_label_with_a_margin_gets_its_full_text_width()
    {
        // MALayout's PaneHeader: a bold title with a left margin in a Star
        // column, beside a fixed 40 px close-button column.
        var title = new Label
        {
            Text = "Activity", FontSize = 16, FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation, Margin = new Thickness(12, 0, 0, 0),
        };
        var header = new Grid { HeightRequest = 40 };
        header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        header.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(40)));
        header.Add(title, 0, 0);
        header.Add(new BoxView { WidthRequest = 32, HeightRequest = 32 }, 1, 0);

        using var host = new HeadlessMauiHost(new ContentPage { Content = new Grid { WidthRequest = 300, HorizontalOptions = LayoutOptions.Start, Children = { header } } }, withEngine: true);
        host.Context.Render();

        var label = (SkiaLabel)title.Handler!.PlatformView!;
        label.Bounds.Width.Should().BeGreaterThanOrEqualTo(label.DesiredSize.Width - 0.5, "the label is not squeezed below its text");
        label.Bounds.Width.Should().BeGreaterThan(55, "'Activity' in bold 16pt is wider than that");
    }

    [Fact]
    public void A_label_measures_the_same_before_and_after_it_has_a_render_context()
    {
        // A pane's contents are measured as the pane opens, before they are
        // attached to the rendering engine; drawing uses the engine's fonts.
        using var host = new HeadlessMauiHost(new ContentPage(), withEngine: true);
        var label = new SkiaLabel { Text = "Activity", FontSize = 16, FontAttributes = FontAttributes.Bold };

        var detached = label.Measure(new Microsoft.Maui.Graphics.Size(400, 40)).Width;
        label.RenderContext = host.Context.RenderingEngine;
        var attached = label.Measure(new Microsoft.Maui.Graphics.Size(400, 40)).Width;

        detached.Should().BeApproximately(attached, 0.5);
    }

    [Fact]
    public void A_Border_filled_through_its_Background_brush_paints_it()
    {
        // MAToolbar's toggle button marks its checked state by setting the
        // surface Border's Background (a brush) in code.
        var border = new Border
        {
            WidthRequest = 60, HeightRequest = 40, StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
            Content = new Label { Text = " " },
        };
        using var host = new HeadlessMauiHost(new ContentPage { BackgroundColor = Colors.White, Content = border }, withEngine: true);
        host.Context.Render();

        border.Background = Colors.Red; // implicit SolidColorBrush, as `_border.Background = color`
        host.Context.Render();

        var b = ((SkiaView)border.Handler!.PlatformView!).Bounds;
        var (r, g, bl, _) = host.DisplayWindow.PixelAt((int)b.Left + 5, (int)b.Top + 5);
        ((int)r).Should().BeGreaterThan(200);
        ((int)g).Should().BeLessThan(60);
    }

    [Fact]
    public void A_truncating_label_given_its_measured_width_draws_its_whole_text()
    {
        // Measured by advance width, it must not truncate at that width: bold
        // glyph ink overhangs the advance, which truncated "Activity".
        var label = new SkiaLabel
        {
            Text = "Activity", FontSize = 16, FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.TailTruncation,
        };
        var desired = label.Measure(new Microsoft.Maui.Graphics.Size(400, 40));
        using var font = Microsoft.Maui.Platform.Linux.Rendering.SkiaFontFactory.Create(
            label.Fonts.GetTypeface("Sans", new SkiaSharp.SKFontStyle(SkiaSharp.SKFontStyleWeight.Bold, SkiaSharp.SKFontStyleWidth.Normal, SkiaSharp.SKFontStyleSlant.Upright)), 16);

        label.FitSingleLine("Activity", font, (float)desired.Width).Should().Be("Activity");
        label.FitSingleLine("Activity", font, (float)desired.Width - 10).Should().EndWith("...");
    }
}
