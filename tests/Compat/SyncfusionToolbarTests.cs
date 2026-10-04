// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using SkiaSharp;
using Syncfusion.Maui.Core;
using Syncfusion.Maui.Toolbar;
using Xunit;
using Xunit.Abstractions;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// SfToolbar under the mouse and keyboard, as on Windows: the item under the mouse shows its hover
/// background, the "more" menu's items highlight under it, and the items are tab stops that Enter
/// activates.
/// </summary>
[Collection(CompatHost.Collection)]
public sealed class SyncfusionToolbarTests
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly ITestOutputHelper _out;
    public SyncfusionToolbarTests(ITestOutputHelper output) => _out = output;

    private static SfToolbar Toolbar(params string[] names)
    {
        var toolbar = new SfToolbar { HeightRequest = 56, WidthRequest = 400, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        foreach (var name in names)
            toolbar.Items.Add(new SfToolbarItem { Name = name, Text = name, Size = new Size(80, 40) });
        return toolbar;
    }

    private static CompatHost Host(View content, int width = 500, int height = 300)
    {
        var host = new CompatHost(new ContentPage { BackgroundColor = Colors.White, Content = new Grid { Children = { content } } }, b => b.UseLinuxSyncfusion(), width, height);
        Pump(host);
        return host;
    }

    private static void Pump(CompatHost host, int frames = 3)
    {
        for (int i = 0; i < frames; i++)
        {
            Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll();
            host.Render();
        }
    }

    private static View ItemView(SfToolbarItem item) =>
        (View)typeof(SfToolbarItem).GetProperty("DefaultView", Any)!.GetValue(item)!;

    private static bool IsHovered(SfToolbarItem item) =>
        (bool)ItemView(item).GetType().GetProperty("IsHovered", Any)!.GetValue(ItemView(item))!;

    [Fact]
    public void The_item_under_the_mouse_shows_its_hover_background_and_tool_tip()
    {
        var toolbar = Toolbar("Bold", "Italic", "Underline");
        using var host = Host(toolbar);
        IPlatformApplication.Current = host.LinuxApp; // the tool tip is an SfPopup, which finds the window's page through it
        try
        {
            var bold = (SfToolbarItem)toolbar.Items[0];
            var italic = (SfToolbarItem)toolbar.Items[1];
            bold.ToolTipText = "Bold text";
            var b = CompatHost.PlatformOf(ItemView(bold)).ScreenBounds;
            var i = CompatHost.PlatformOf(ItemView(italic)).ScreenBounds;
            // A spot of the item away from its text: the toolbar's background until hovered.
            int px = (int)b.Left + 6, py = (int)b.Top + 6;
            var background = host.DisplayWindow.PixelAt(px, py);

            host.DisplayWindow.RaisePointerMoved((float)b.Center.X, (float)b.Center.Y);
            Pump(host);
            IsHovered(bold).Should().BeTrue("the item under the mouse is hovered (Windows' SfToolbarLayout.HandleTouch)");
            var hovered = host.DisplayWindow.PixelAt(px, py);
            (hovered.R + hovered.G + hovered.B).Should().BeLessThan(background.R + background.G + background.B - 15, "its hover background is drawn");
            TooltipOpen(toolbar).Should().BeTrue("hovering an item shows its tool tip, as on Windows");

            host.DisplayWindow.RaisePointerMoved((float)i.Center.X, (float)i.Center.Y);
            Pump(host);
            IsHovered(bold).Should().BeFalse("the hover moves with the mouse");
            IsHovered(italic).Should().BeTrue();

            host.DisplayWindow.RaisePointerMoved(450, 250); // off the toolbar
            Pump(host);
            IsHovered(italic).Should().BeFalse("leaving the toolbar clears the hover");
            TooltipOpen(toolbar).Should().BeFalse("and closes the tool tip");
            host.DisplayWindow.PixelAt(px, py).Should().Be(background, "and its background goes");
        }
        finally
        {
            IPlatformApplication.Current = null;
        }
    }

    private static bool TooltipOpen(SfToolbar toolbar)
    {
        var layout = typeof(SfToolbar).GetProperty("ToolbarLayout", Any)?.GetValue(toolbar)
            ?? toolbar.GetType().BaseType!.GetProperty("ToolbarLayout", Any)!.GetValue(toolbar)!;
        var tooltip = layout.GetType().GetField("toolbarTooltip", Any)!.GetValue(layout)!;
        var popup = (Syncfusion.Maui.Popup.SfPopup?)tooltip.GetType().GetField("popup", Any)!.GetValue(tooltip);
        return popup?.IsOpen == true;
    }

    private static IEnumerable<Element> Descendants(Element root)
    {
        foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
        {
            yield return child;
            foreach (var d in Descendants(child))
                yield return d;
        }
    }

    [Fact]
    public void The_more_menu_highlights_the_item_under_the_mouse()
    {
        var toolbar = Toolbar("One", "Two", "Three", "Four", "Five", "Six");
        toolbar.WidthRequest = 260;
        toolbar.OverflowMode = ToolbarItemOverflowMode.MoreButton;
        using var host = Host(toolbar, 500, 500);
        IPlatformApplication.Current = host.LinuxApp; // SfPopup finds the window's page through it, as in a running app
        try
        {
        var more = Descendants(toolbar).OfType<View>().First(v => v.GetType().Name == "ToolbarIconButton" && v.IsVisible && v.Width > 0);
        var (mx, my) = CompatHost.CenterOf(more);
        _out.WriteLine($"more at {mx},{my}");
        host.DisplayWindow.RaisePointerMoved(mx, my);
        host.DisplayWindow.RaisePointerPressed(mx, my);
        host.DisplayWindow.RaisePointerReleased(mx, my);
        Pump(host, 5);

        var layout = (View?)typeof(SfToolbar).GetField("moreItemLayout", Any)!.GetValue(toolbar);
        layout.Should().NotBeNull("the more button opens the more menu");
        var views = ((IVisualTreeElement)layout!).GetVisualChildren().OfType<View>().Where(v => v.GetType().Name == "MoreItemView").ToList();
        _out.WriteLine($"more items {views.Count}");
        views.Should().NotBeEmpty("the more button opens the more menu");
        var first = views[0];
        var r = CompatHost.PlatformOf(first).ScreenBounds;
        _out.WriteLine($"first {r}");
        int px = (int)r.Right - 6, py = (int)r.Top + 4;
        var background = host.DisplayWindow.PixelAt(px, py);

        host.DisplayWindow.RaisePointerMoved((float)r.Center.X, (float)r.Center.Y);
        Pump(host, 5);
        var hovered = host.DisplayWindow.PixelAt(px, py);
        _out.WriteLine($"background {background} hovered {hovered}");
        first.GetVisualTreeDescendants().OfType<SfEffectsView>().Should().ContainSingle("Windows gives each more item an effects view");
        (hovered.R + hovered.G + hovered.B).Should().BeLessThan(background.R + background.G + background.B - 15, "the item under the mouse is highlighted");

        host.DisplayWindow.RaisePointerMoved((float)r.Center.X, (float)r.Bottom + 200);
        Pump(host, 5);
        host.DisplayWindow.PixelAt(px, py).Should().Be(background, "leaving the item removes the highlight");
        }
        finally
        {
            IPlatformApplication.Current = null;
        }
    }

    [Fact]
    public void The_items_are_tab_stops_and_Enter_activates_the_focused_one()
    {
        var entry = new Entry { WidthRequest = 100 };
        var toolbar = Toolbar("Bold", "Italic");
        using var host = Host(new VerticalStackLayout { entry, toolbar });
        var tapped = new List<string?>();
        toolbar.Tapped += (_, e) => tapped.Add(e.NewToolbarItem?.Name);

        var stops = host.Context.TabStops();
        stops.Should().Contain(CompatHost.PlatformOf(ItemView((SfToolbarItem)toolbar.Items[0])), "Windows makes each item a tab stop (OnHandlerChanged sets IsTabStop)");
        stops.Should().Contain(CompatHost.PlatformOf(ItemView((SfToolbarItem)toolbar.Items[1])));

        host.Context.FocusedView = CompatHost.PlatformOf(entry);
        host.DisplayWindow.RaiseKeyDown(Key.Tab);
        Pump(host, 1);
        host.Context.FocusedView.Should().BeSameAs(CompatHost.PlatformOf(ItemView((SfToolbarItem)toolbar.Items[0])), "Tab moves from the entry to the first item");
        host.DisplayWindow.RaiseKeyDown(Key.Tab);
        Pump(host, 1);
        host.Context.FocusedView.Should().BeSameAs(CompatHost.PlatformOf(ItemView((SfToolbarItem)toolbar.Items[1])));

        host.DisplayWindow.RaiseKeyDown(Key.Enter);
        Pump(host, 1);
        tapped.Should().Equal(new[] { "Italic" }, "Enter on the focused item taps it");
    }
}
