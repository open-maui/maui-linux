// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Reflection;
using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Syncfusion.Maui.Charts;
using Syncfusion.Maui.ListView;
using Xunit;
using Colors = Microsoft.Maui.Graphics.Colors;
using MauiPoint = Microsoft.Maui.Graphics.Point;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// The Syncfusion input the platform-neutral build leaves to the platform:
/// ctrl+wheel as a pinch, keyboard routing to the controls' keyboard
/// detectors (with focus on click), and the item-to-window point conversions
/// drag-and-drop depends on.
/// </summary>
[Collection(CompatHost.Collection)]
public sealed class SyncfusionInputCompatTests
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private static void Settle(CompatHost host)
    {
        for (int i = 0; i < 4; i++)
            host.Render();
    }

    private static void Wheel(CompatHost host, float x, float y, float deltaY, KeyModifiers modifiers = KeyModifiers.None)
    {
        var field = host.DisplayWindow.GetType().GetField("Scroll", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var handler = (EventHandler<ScrollEventArgs>?)field.GetValue(host.DisplayWindow);
        handler?.Invoke(host.DisplayWindow, new ScrollEventArgs(x, y, 0, deltaY, modifiers));
        Settle(host);
    }

    private static void Click(CompatHost host, float x, float y)
    {
        host.DisplayWindow.RaisePointerMoved(x, y);
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        Settle(host);
    }

    private static void Key(CompatHost host, Key key)
    {
        host.DisplayWindow.RaiseKeyDown(key);
        var field = host.DisplayWindow.GetType().GetField("KeyUp", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((EventHandler<KeyEventArgs>?)field.GetValue(host.DisplayWindow))?.Invoke(host.DisplayWindow, new KeyEventArgs(key));
        Settle(host);
    }

    // ---- Pinch ---------------------------------------------------------------

    public sealed record Sample(string Name, double Value);

    private static (CompatHost Host, SfCartesianChart Chart) Chart(bool pinch = true)
    {
        var chart = new SfCartesianChart
        {
            ZoomPanBehavior = new ChartZoomPanBehavior { ZoomMode = ZoomMode.X, EnablePanning = true, EnablePinchZooming = pinch },
        };
        chart.XAxes.Add(new CategoryAxis());
        chart.YAxes.Add(new NumericalAxis());
        chart.Series.Add(new ColumnSeries
        {
            ItemsSource = Enumerable.Range(0, 8).Select(i => new Sample(((char)('A' + i)).ToString(), i % 3 + 1)).ToList(),
            XBindingPath = nameof(Sample.Name), YBindingPath = nameof(Sample.Value),
            Fill = new SolidColorBrush(Colors.Red),
        });
        var host = new CompatHost(new ContentPage { BackgroundColor = Colors.White, Content = chart }, b => b.UseLinuxSyncfusion(), 400, 300);
        Settle(host);
        return (host, chart);
    }

    [Fact]
    public void Ctrl_wheel_pinches_a_chart_about_the_pointer()
    {
        var (host, chart) = Chart();
        using (host)
        {
            var xAxis = chart.XAxes[0];
            Wheel(host, 200, 150, -1, KeyModifiers.Control);
            // A pinch scales the zoom level (1.2 per notch); the plain wheel adds a
            // quarter level (1.25), so this is the pinch route.
            xAxis.ZoomFactor.Should().BeApproximately(1 / 1.2, 1e-6);

            Wheel(host, 200, 150, 1, KeyModifiers.Control);
            xAxis.ZoomFactor.Should().BeApproximately(1, 1e-6, "ctrl+wheel down pinches back out");
        }

        // Syncfusion's zoom leaves the position at 0 on the first step from the
        // full range (a touch pinch on Windows too), so compare a second step.
        double PositionAfterPinchAt(float x)
        {
            var (at, atChart) = Chart();
            using (at)
            {
                Wheel(at, 200, 150, -1, KeyModifiers.Control);
                Wheel(at, x, 150, -2, KeyModifiers.Control);
                atChart.XAxes[0].ZoomFactor.Should().BeApproximately(1 / Math.Pow(1.2, 3), 1e-6);
                return atChart.XAxes[0].ZoomPosition;
            }
        }
        PositionAfterPinchAt(330).Should().BeGreaterThan(PositionAfterPinchAt(90) + 0.1, "the zoom is centred on the pointer");
    }

    [Fact]
    public void Ctrl_wheel_follows_the_charts_pinch_setting_and_a_drag_pans_after_it()
    {
        var (off, offChart) = Chart(pinch: false);
        using (off)
        {
            Wheel(off, 200, 150, -1, KeyModifiers.Control);
            offChart.XAxes[0].ZoomFactor.Should().Be(1, "EnablePinchZooming is off");
        }

        var (host, chart) = Chart();
        using var _ = host;
        Wheel(host, 200, 150, -2, KeyModifiers.Control);
        var xAxis = chart.XAxes[0];
        var zoomed = xAxis.ZoomFactor;
        var position = xAxis.ZoomPosition;
        zoomed.Should().BeLessThan(1);

        host.DisplayWindow.RaisePointerMoved(250, 150);
        host.DisplayWindow.RaisePointerPressed(250, 150);
        for (int i = 1; i <= 8; i++)
            host.DisplayWindow.RaisePointerMoved(250 - i * 12, 150);
        host.DisplayWindow.RaisePointerReleased(154, 150);
        Settle(host);

        xAxis.ZoomFactor.Should().BeApproximately(zoomed, 1e-9);
        xAxis.ZoomPosition.Should().BeGreaterThan(position, "the pinch ended, so the drag pans");
    }

    // ---- Keyboard ------------------------------------------------------------

    private static SfListView List(IEnumerable<string> items, int height = 300) => new()
    {
        ItemsSource = items,
        ItemSize = 40,
        HeightRequest = height,
        SelectionMode = Syncfusion.Maui.ListView.SelectionMode.Single,
        ItemTemplate = new DataTemplate(() =>
        {
            var label = new Label();
            label.SetBinding(Label.TextProperty, ".");
            return label;
        }),
    };

    private static List<ListViewItem> Rows(SfListView list) =>
        list.GetVisualTreeDescendants().OfType<ListViewItem>()
            .Where(r => r.Handler?.PlatformView is SkiaView { IsVisible: true })
            .OrderBy(r => CompatHost.PlatformOf(r).ScreenBounds.Y).ToList();

    [Fact]
    public void Clicking_a_list_focuses_it_and_the_arrow_keys_move_the_selection()
    {
        var list = List(Enumerable.Range(1, 10).Select(i => $"Item {i}").ToList());
        using var host = new CompatHost(new ContentPage { Content = list }, b => b.UseLinuxSyncfusion(), 400, 400);
        Settle(host);

        var (x, y) = CompatHost.CenterOf(Rows(list)[0]);
        Click(host, x, y);
        list.SelectedItem.Should().Be("Item 1");
        host.Context.FocusedView.Should().BeSameAs(CompatHost.PlatformOf(list), "a keyboard-listening control takes focus on click");

        Key(host, Microsoft.Maui.Platform.Key.Down);
        list.SelectedItem.Should().Be("Item 2");
        Key(host, Microsoft.Maui.Platform.Key.Down);
        list.SelectedItem.Should().Be("Item 3");
        Key(host, Microsoft.Maui.Platform.Key.Up);
        list.SelectedItem.Should().Be("Item 2");
    }

    [Fact]
    public void A_focused_entry_in_a_row_keeps_focus_and_its_keys()
    {
        var entry = new Entry();
        var list = List(new List<string> { "a", "b", "c" }, 200);
        var page = new ContentPage { Content = new VerticalStackLayout { Children = { list, entry } } };
        using var host = new CompatHost(page, b => b.UseLinuxSyncfusion(), 400, 400);
        Settle(host);

        var (x, y) = CompatHost.CenterOf(Rows(list)[0]);
        Click(host, x, y);
        list.SelectedItem.Should().Be("a");

        var (ex, ey) = CompatHost.CenterOf(entry);
        Click(host, ex, ey);
        host.Context.FocusedView.Should().BeSameAs(CompatHost.PlatformOf(entry));
        Key(host, Microsoft.Maui.Platform.Key.Down);
        list.SelectedItem.Should().Be("a", "keys go to the focused entry, which is outside the list");
    }

    [Fact]
    public void Arrow_keys_change_a_focused_rating()
    {
        var rating = new Syncfusion.Maui.Inputs.SfRating { ItemCount = 5, Value = 0, WidthRequest = 250, HeightRequest = 50, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        using var host = new CompatHost(new ContentPage { Content = rating }, b => b.UseLinuxSyncfusion(), 400, 200);
        Settle(host);

        Click(host, 60, 25);
        var clicked = rating.Value;
        clicked.Should().BeGreaterThan(0, "the click rates");
        host.Context.FocusedView.Should().BeSameAs(CompatHost.PlatformOf(rating));

        Key(host, Microsoft.Maui.Platform.Key.Right);
        rating.Value.Should().BeGreaterThan(clicked);
        Key(host, Microsoft.Maui.Platform.Key.Left);
        Key(host, Microsoft.Maui.Platform.Key.Left);
        rating.Value.Should().BeLessThan(clicked);
    }

    [Theory]
    [InlineData(Microsoft.Maui.Platform.Key.A, "A")]
    [InlineData(Microsoft.Maui.Platform.Key.Z, "Z")]
    [InlineData(Microsoft.Maui.Platform.Key.D0, "Num0")]
    [InlineData(Microsoft.Maui.Platform.Key.NumPad7, "Num7")]
    [InlineData(Microsoft.Maui.Platform.Key.F12, "F12")]
    [InlineData(Microsoft.Maui.Platform.Key.Backspace, "Back")]
    [InlineData(Microsoft.Maui.Platform.Key.Control, "Ctrl")]
    [InlineData(Microsoft.Maui.Platform.Key.NumPadEnter, "Enter")]
    [InlineData(Microsoft.Maui.Platform.Key.PageDown, "PageDown")]
    [InlineData(Microsoft.Maui.Platform.Key.Comma, "None")]
    public void Keys_convert_as_on_Windows(Microsoft.Maui.Platform.Key key, string expected)
    {
        var bridge = typeof(LinuxSyncfusionBuilderExtensions).Assembly.GetType("Microsoft.Maui.Platform.Linux.Syncfusion.SfKeyboardBridge")!;
        bridge.GetMethod("ToKeyboardKey", Any)!.Invoke(null, new object[] { key })!.ToString().Should().Be(expected);
    }

    // ---- Raw points ----------------------------------------------------------

    [Fact]
    public void List_rows_report_window_points_even_when_scrolled()
    {
        var list = List(Enumerable.Range(1, 40).Select(i => $"Item {i}").ToList());
        using var host = new CompatHost(new ContentPage { Content = new VerticalStackLayout { Padding = new Thickness(0, 50, 0, 0), Children = { list } } },
            b => b.UseLinuxSyncfusion(), 400, 400);
        Settle(host);
        var getRawPoints = typeof(SfListView).Assembly.GetType("Syncfusion.Maui.ListView.ListViewItemExtensions")!.GetMethod("GetRawPoints", Any)!;

        var row = Rows(list)[1];
        var raw = (MauiPoint)getRawPoints.Invoke(null, new object?[] { row, new MauiPoint(5, 7), row.Handler!.PlatformView })!;
        var bounds = CompatHost.PlatformOf(row).ScreenBounds;
        raw.Should().Be(new MauiPoint(bounds.X + 5, bounds.Y + 7));
        raw.Y.Should().BeApproximately(50 + 40 + 7, 1, "the second row sits one row below the padding");

        list.ScrollTo(200, false);
        Settle(host);
        var scrolled = Rows(list)[0];
        var top = (MauiPoint)getRawPoints.Invoke(null, new object?[] { scrolled, new MauiPoint(0, 0), scrolled.Handler!.PlatformView })!;
        top.Y.Should().BeInRange(10, 90, "a scrolled row reports where it shows, not where it is laid out");
    }

    [Fact]
    public void Dragging_a_rows_indicator_reorders_the_list()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(1, 6).Select(i => $"Item {i}"));
        var positions = new List<MauiPoint>();
        var list = new SfListView
        {
            ItemsSource = items,
            ItemSize = 40,
            HeightRequest = 300,
            DragStartMode = DragStartMode.OnDragIndicator,
        };
        list.ItemTemplate = new DataTemplate(() =>
        {
            var label = new Label();
            label.SetBinding(Label.TextProperty, ".");
            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(Microsoft.Maui.GridLength.Star), new ColumnDefinition(40) } };
            grid.Add(label, 0);
            grid.Add(new DragIndicatorView { ListView = list, Content = new BoxView { Color = Colors.Gray } }, 1);
            return grid;
        });
        list.DragDropController!.UpdateSource = true;
        var actions = new List<DragAction>();
        list.ItemDragging += (_, e) => { positions.Add(e.Position); actions.Add(e.Action); };
        using var host = new CompatHost(new ContentPage { Content = list }, b => b.UseLinuxSyncfusion(), 400, 400);
        Settle(host);

        var indicator = list.GetVisualTreeDescendants().OfType<DragIndicatorView>()
            .OrderBy(v => CompatHost.PlatformOf(v).ScreenBounds.Y).First();
        var (x, y) = CompatHost.CenterOf(indicator);
        host.DisplayWindow.RaisePointerMoved(x, y);
        host.DisplayWindow.RaisePointerPressed(x, y);
        for (int i = 1; i <= 20; i++)
        {
            host.DisplayWindow.RaisePointerMoved(x, y + i * 6);
            host.Render();
        }
        // Rows slide aside with a 200 ms animation, and the drop waits for it;
        // the app's run loop pumps the animation ticker between frames.
        void Animate()
        {
            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(40);
                Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll();
                host.Render();
            }
        }
        Animate();
        host.DisplayWindow.RaisePointerReleased(x, y + 120);
        Animate();

        positions.Should().NotBeEmpty();
        positions.Max(p => p.Y).Should().BeGreaterThan(y + 60, "the drag follows the pointer in window coordinates");
        actions.Should().Contain(DragAction.Drop);
        items.IndexOf("Item 1").Should().BeGreaterThan(0, "the dragged row moved down");
    }

    [Fact]
    public void Tree_and_scheduler_points_come_from_the_views_window_bounds()
    {
        var first = new Label { Text = "first", HeightRequest = 30 };
        var second = new Label { Text = "second", HeightRequest = 30, Margin = new Thickness(20, 0, 0, 0) };
        var stack = new VerticalStackLayout { Padding = new Thickness(10, 40, 0, 0), Children = { first, second } };
        using var host = new CompatHost(new ContentPage { Content = stack }, b => b.UseLinuxSyncfusion(), 300, 300);
        Settle(host);
        var child = CompatHost.PlatformOf(second);
        var parent = CompatHost.PlatformOf(stack);

        var treeRaw = typeof(Syncfusion.Maui.TreeView.SfTreeView).Assembly.GetType("Syncfusion.Maui.TreeView.TreeViewItemExtensions")!
            .GetMethod("GetRawPoints", Any)!;
        var raw = (MauiPoint)treeRaw.Invoke(null, new object?[] { null, new MauiPoint(3, 4), child })!;
        raw.Should().Be(new MauiPoint(child.ScreenBounds.X + 3, child.ScreenBounds.Y + 4));

        var containerPoints = typeof(Syncfusion.Maui.Scheduler.SfScheduler).Assembly.GetType("Syncfusion.Maui.Scheduler.AppointmentsViewHelper")!
            .GetMethod("GetContainerPoints", Any)!;
        var inParent = (MauiPoint)containerPoints.Invoke(null, new object?[] { new MauiPoint(3, 4), child, parent, false, false })!;
        inParent.Should().Be(new MauiPoint(child.ScreenBounds.X - parent.ScreenBounds.X + 3, child.ScreenBounds.Y - parent.ScreenBounds.Y + 4));
        inParent.Y.Should().BeApproximately(40 + 30 + 4, 1);

        var timeline = (MauiPoint)containerPoints.Invoke(null, new object?[] { new MauiPoint(3, 4), child, parent, true, false })!;
        timeline.X.Should().BeApproximately(inParent.X - (child.Bounds.X - parent.Bounds.X), 1e-6,
            "timeline views take the child's own offset back out");
    }
}
