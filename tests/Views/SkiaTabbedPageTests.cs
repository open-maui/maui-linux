// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using SkiaSharp;
using Xunit;
using AndroidTabbedPage = Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.TabbedPage;
using ToolbarPlacement = Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.ToolbarPlacement;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using PointerButton = Microsoft.Maui.Platform.PointerButton;

/// <summary>
/// SkiaTabbedPage on its own (tab strip drawing, selection, hit testing,
/// placement) and through the TabbedPageHandler with a MAUI TabbedPage
/// (CurrentPage both ways, CurrentPageChanged, children changes, placement).
/// </summary>
[Collection(HeadlessMaui.Collection)]
public class SkiaTabbedPageTests
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

    private static SkiaTabbedPage CreatePage(out ColorView first, out ColorView second)
    {
        var page = new SkiaTabbedPage();
        first = new ColorView(SKColors.Red);
        second = new ColorView(SKColors.Blue);
        page.AddTab("First", first);
        page.AddTab("Second", second);
        page.Measure(new Size(400, 300));
        page.Arrange(new Rect(0, 0, 400, 300));
        return page;
    }

    private static SKBitmap Render(SkiaView view, int width = 400, int height = 300)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        view.Draw(canvas);
        return bitmap;
    }

    #region Platform view

    [Fact]
    public void AddTab_registers_tabs_and_selects_the_first()
    {
        var page = CreatePage(out var first, out _);

        page.Tabs.Should().HaveCount(2);
        page.Tabs.Select(t => t.Title).Should().Equal("First", "Second");
        page.SelectedIndex.Should().Be(0);
        page.SelectedTab!.Content.Should().BeSameAs(first);
        page.Children.Should().HaveCount(2);
    }

    [Fact]
    public void Tab_strip_renders_titles()
    {
        var page = CreatePage(out _, out _);
        page.TabBarBackgroundColor = Colors.Black;
        page.SelectedTabColor = Colors.White;
        page.UnselectedTabColor = Colors.White;

        using var bitmap = Render(page);

        // Text glyphs lighten the black strip in both tab cells.
        int leftLit = 0, rightLit = 0;
        for (int y = 0; y < (int)page.TabBarHeight; y++)
        {
            for (int x = 0; x < 400; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red > 128 && p.Green > 128 && p.Blue > 128)
                {
                    if (x < 200) leftLit++; else rightLit++;
                }
            }
        }
        leftLit.Should().BeGreaterThan(20, "the first tab title is drawn");
        rightLit.Should().BeGreaterThan(20, "the second tab title is drawn");
    }

    [Fact]
    public void Selecting_a_tab_switches_the_drawn_content()
    {
        var page = CreatePage(out _, out _);

        using (var before = Render(page))
            before.GetPixel(200, 200).Should().Be(SKColors.Red);

        page.SelectedIndex = 1;

        using var after = Render(page);
        after.GetPixel(200, 200).Should().Be(SKColors.Blue);
    }

    [Fact]
    public void SelectedIndexChanged_fires_once_per_change()
    {
        var page = CreatePage(out _, out _);
        int fired = 0;
        page.SelectedIndexChanged += (s, e) => fired++;

        page.SelectedIndex = 1;
        page.SelectedIndex = 1;
        page.SelectedIndex = 0;

        fired.Should().Be(2);
    }

    [Fact]
    public void SelectedIndex_ignores_out_of_range_values()
    {
        var page = CreatePage(out _, out _);

        page.SelectedIndex = 5;
        page.SelectedIndex = -1;

        page.SelectedIndex.Should().Be(0);
    }

    [Fact]
    public void Pointer_press_on_the_tab_strip_selects_that_tab()
    {
        var page = CreatePage(out _, out _);

        var e = new PointerEventArgs(300, page.TabBarHeight / 2, PointerButton.Left);
        page.OnPointerPressed(e);

        page.SelectedIndex.Should().Be(1);
        e.Handled.Should().BeTrue();
    }

    [Fact]
    public void Pointer_press_on_the_content_does_not_change_selection()
    {
        var page = CreatePage(out _, out _);

        page.OnPointerPressed(new PointerEventArgs(300, 200, PointerButton.Left));

        page.SelectedIndex.Should().Be(0);
    }

    [Fact]
    public void Bottom_placement_moves_the_strip_and_the_content()
    {
        var page = CreatePage(out var first, out _);

        page.TabBarOnBottom = true;
        page.Arrange(new Rect(0, 0, 400, 300));

        page.TabBarBounds.Top.Should().Be(300 - page.TabBarHeight);
        first.Bounds.Top.Should().Be(0);
        first.Bounds.Height.Should().Be(300 - page.TabBarHeight);

        // A press in the bottom strip selects; the old top strip is content now.
        page.OnPointerPressed(new PointerEventArgs(300, 300 - page.TabBarHeight / 2, PointerButton.Left));
        page.SelectedIndex.Should().Be(1);
    }

    [Fact]
    public void Top_placement_arranges_content_below_the_strip()
    {
        var page = CreatePage(out var first, out _);

        page.TabBarBounds.Top.Should().Be(0);
        first.Bounds.Top.Should().Be(page.TabBarHeight);
        first.Bounds.Height.Should().Be(300 - page.TabBarHeight);
    }

    [Fact]
    public void HitTest_returns_the_page_for_the_strip_and_the_selected_content_otherwise()
    {
        var page = CreatePage(out var first, out var second);

        page.HitTest(100, page.TabBarHeight / 2).Should().BeSameAs(page);
        page.HitTest(100, 200).Should().BeSameAs(first);

        page.SelectedIndex = 1;
        page.HitTest(100, 200).Should().BeSameAs(second);
    }

    [Fact]
    public void RemoveTab_clamps_the_selection()
    {
        var page = CreatePage(out _, out _);
        page.SelectedIndex = 1;

        page.RemoveTab(1);

        page.Tabs.Should().HaveCount(1);
        page.SelectedIndex.Should().Be(0);
        page.Children.Should().HaveCount(1);
    }

    [Fact]
    public void Draw_with_no_tabs_does_not_throw()
    {
        var page = new SkiaTabbedPage();
        page.Arrange(new Rect(0, 0, 200, 100));

        var exception = Record.Exception(() => { using var _ = Render(page, 200, 100); });

        exception.Should().BeNull();
    }

    #endregion

    #region Handler

    private static (TabbedPage page, SkiaTabbedPage platform) CreateMauiTabbedPage(params string[] titles)
    {
        var ctx = HeadlessMaui.CreateContext();
        var page = new TabbedPage();
        foreach (var title in titles)
            page.Children.Add(new ContentPage { Title = title, Content = new Label { Text = title } });

        var handler = (TabbedPageHandler)Microsoft.Maui.Platform.Linux.Hosting.MauiHandlerExtensions.ToHandler(page, ctx);
        var platform = (SkiaTabbedPage)handler.PlatformView!;
        platform.Measure(new Size(400, 300));
        platform.Arrange(new Rect(0, 0, 400, 300));
        return (page, platform);
    }

    [Fact]
    public void Handler_creates_a_tab_per_child_page_with_its_title()
    {
        var (page, platform) = CreateMauiTabbedPage("Alpha", "Beta", "Gamma");

        platform.Tabs.Select(t => t.Title).Should().Equal("Alpha", "Beta", "Gamma");
        platform.SelectedIndex.Should().Be(0);
        platform.SelectedTab!.Content.Should().BeSameAs(page.Children[0].Handler!.PlatformView);
    }

    [Fact]
    public void Handler_setting_CurrentPage_selects_the_tab_and_raises_CurrentPageChanged()
    {
        var (page, platform) = CreateMauiTabbedPage("Alpha", "Beta");
        int changed = 0;
        page.CurrentPageChanged += (s, e) => changed++;

        page.CurrentPage = page.Children[1];

        platform.SelectedIndex.Should().Be(1);
        changed.Should().Be(1);
    }

    [Fact]
    public void Handler_pointer_press_on_the_strip_updates_CurrentPage_and_raises_CurrentPageChanged()
    {
        var (page, platform) = CreateMauiTabbedPage("Alpha", "Beta");
        int changed = 0;
        page.CurrentPageChanged += (s, e) => changed++;

        platform.OnPointerPressed(new PointerEventArgs(300, platform.TabBarHeight / 2, PointerButton.Left));

        page.CurrentPage.Should().BeSameAs(page.Children[1]);
        platform.SelectedIndex.Should().Be(1);
        changed.Should().Be(1);
    }

    [Fact]
    public void Handler_adding_a_child_page_adds_a_tab()
    {
        var (page, platform) = CreateMauiTabbedPage("Alpha");

        page.Children.Add(new ContentPage { Title = "Beta", Content = new Label { Text = "Beta" } });

        platform.Tabs.Select(t => t.Title).Should().Equal("Alpha", "Beta");
    }

    [Fact]
    public void Handler_maps_ToolbarPlacement_Bottom_to_a_bottom_strip()
    {
        var (page, platform) = CreateMauiTabbedPage("Alpha", "Beta");
        platform.TabBarOnBottom.Should().BeFalse();

        AndroidTabbedPage.SetToolbarPlacement(page, ToolbarPlacement.Bottom);

        platform.TabBarOnBottom.Should().BeTrue();
    }

    [Fact]
    public void Handler_maps_bar_colors()
    {
        var (page, platform) = CreateMauiTabbedPage("Alpha", "Beta");

        page.BarBackgroundColor = Colors.DarkGreen;
        page.SelectedTabColor = Colors.Yellow;
        page.UnselectedTabColor = Colors.Gray;

        platform.TabBarBackgroundColor.Should().Be(Colors.DarkGreen);
        platform.SelectedTabColor.Should().Be(Colors.Yellow);
        platform.IndicatorColor.Should().Be(Colors.Yellow);
        platform.UnselectedTabColor.Should().Be(Colors.Gray);
    }

    #endregion
}
