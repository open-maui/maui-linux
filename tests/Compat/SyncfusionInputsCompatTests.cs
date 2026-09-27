// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using SkiaSharp;
using Syncfusion.Maui.Inputs;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// Syncfusion.Maui.Inputs drop-downs (SfComboBox, SfAutocomplete; 34.2.9,
/// platform-neutral build) through <c>UseLinuxSyncfusion()</c>: the drop-down
/// opens as a popup over the page, lists the items, takes clicks and closes.
/// </summary>
[Collection(CompatHost.Collection)]
public sealed class SyncfusionInputsCompatTests : IDisposable
{
    private readonly SynchronizationContext? _testContext = SynchronizationContext.Current;

    public void Dispose() => SynchronizationContext.SetSynchronizationContext(_testContext);

    private static readonly string[] Fruits = { "Apple", "Banana", "Cherry", "Grape", "Mango" };

    private static (CompatHost Host, Button Below) Host(View control, double top = 0)
    {
        control.HorizontalOptions = LayoutOptions.Start;
        control.VerticalOptions = LayoutOptions.Start;
        control.WidthRequest = 240;
        control.Margin = new Thickness(0, top, 0, 0);
        var below = new Button { Text = "Below", HorizontalOptions = LayoutOptions.Start, Margin = new Thickness(0, 320, 0, 0) };
        var page = new ContentPage
        {
            BackgroundColor = Colors.White,
            Content = new VerticalStackLayout { Padding = new Thickness(20), Children = { control, below } },
        };
        var host = new CompatHost(page, b => b.UseLinuxSyncfusion(), 600, 600);
        HoldDelayedWork();
        // As on the app's main thread: SfAutocomplete filters after an await,
        // whose continuation must come back to this thread (through the main
        // loop Render drains), not run on the test framework's context.
        SynchronizationContext.SetSynchronizationContext(new Microsoft.Maui.Platform.Linux.Dispatching.LinuxSynchronizationContext());
        host.Render();
        host.Render();
        return (host, below);
    }

    /// <summary>
    /// The host's inline dispatcher runs delayed work at once, so Syncfusion's
    /// long-press timer (started on every press) would fire before the release
    /// and turn each click into a long press. Views created from here on hold
    /// delayed work instead, as a click shorter than the delay does.
    /// </summary>
    private static void HoldDelayedWork() =>
        Microsoft.Maui.Dispatching.DispatcherProvider.SetCurrent(HeldDelayProvider.Instance);

    private sealed class HeldDelayProvider : Microsoft.Maui.Dispatching.IDispatcherProvider
    {
        public static readonly HeldDelayProvider Instance = new();
        private readonly HeldDelayDispatcher _dispatcher = new();
        public Microsoft.Maui.Dispatching.IDispatcher? GetForCurrentThread() => _dispatcher;
    }

    private sealed class HeldDelayDispatcher : Microsoft.Maui.Dispatching.IDispatcher
    {
        public bool IsDispatchRequired => false;
        public bool Dispatch(Action action) { action(); return true; }
        public bool DispatchDelayed(TimeSpan delay, Action action) => true;
        public Microsoft.Maui.Dispatching.IDispatcherTimer CreateTimer() =>
            CompatHost.InlineDispatcherProvider.Instance.GetForCurrentThread()!.CreateTimer();
    }

    /// <summary>The drop-down's item labels (the control's internal SfListView rows).</summary>
    private static Element ListViewOf(View control) =>
        (Element)control.GetType().BaseType!
            .GetProperty("ListView", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(control)!;

    private static List<Label> ItemLabels(View control)
    {
        return ListViewOf(control).GetVisualTreeDescendants().OfType<Label>()
            .Where(l => l.Handler?.PlatformView is SkiaView { Bounds.Height: > 0 } && l.IsVisible)
            .ToList();
    }

    /// <summary>True when a popup overlay covers the point just below the control.</summary>
    private static bool IsPopupOpenBelow(View control)
    {
        var bounds = CompatHost.PlatformOf(control).ScreenBounds;
        return SkiaView.GetPopupOwnerAt((float)bounds.Center.X, (float)bounds.Bottom + 10) != null;
    }

    private static string TextOf(Label label) => label.FormattedText?.ToString() ?? label.Text ?? "";

    /// <summary>Renders until <paramref name="condition"/> holds (awaited work lands through the main loop).</summary>
    private static void RenderUntil(CompatHost host, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        do
        {
            host.Render();
            if (condition())
                return;
            Thread.Sleep(10);
        }
        while (DateTime.UtcNow < deadline);
    }

    private static void Click(CompatHost host, double x, double y)
    {
        host.DisplayWindow.RaisePointerPressed((float)x, (float)y);
        host.DisplayWindow.RaisePointerReleased((float)x, (float)y);
        host.Render();
        host.Render();
    }

    private static void Wheel(CompatHost host, double x, double y, float deltaY)
    {
        var field = host.DisplayWindow.GetType().GetField("Scroll", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        (field.GetValue(host.DisplayWindow) as EventHandler<ScrollEventArgs>)?.Invoke(host.DisplayWindow, new ScrollEventArgs((float)x, (float)y, 0, deltaY));
    }

    private static void ClickItem(CompatHost host, View control, string text)
    {
        var label = ItemLabels(control).First(l => TextOf(l) == text);
        var (x, y) = CompatHost.CenterOf(label);
        Click(host, x, y);
    }

    [Fact]
    public void Clicking_a_combo_box_opens_its_drop_down_below_it_with_the_items()
    {
        var combo = new SfComboBox { ItemsSource = Fruits };
        var (host, _) = Host(combo);
        using var _host = host;
        var comboRect = CompatHost.PlatformOf(combo).ScreenBounds;

        host.Tap(combo);
        host.Render();
        host.Render();
        if (Environment.GetEnvironmentVariable("DROPDOWN_FRAME") is { Length: > 0 } frame) host.SaveFrame(frame);

        combo.IsDropDownOpen.Should().BeTrue();
        IsPopupOpenBelow(combo).Should().BeTrue("the drop-down shows as a popup over the page");
        var labels = ItemLabels(combo);
        labels.Select(TextOf).Should().Contain(new[] { "Apple", "Banana", "Cherry" });
        var first = CompatHost.PlatformOf(labels.First(l => TextOf(l) == "Apple")).ScreenBounds;
        first.Top.Should().BeGreaterThanOrEqualTo(comboRect.Bottom - 1, "the list opens below the control");
        first.Left.Should().BeApproximately(comboRect.Left, 20);

        // The list draws over the page: its text is dark on the popup's white.
        var popupArea = new SKRectI((int)comboRect.Left, (int)comboRect.Bottom + 2, (int)comboRect.Right, (int)comboRect.Bottom + 120);
        host.CountPixelsNot(SKColors.White, popupArea).Should().BeGreaterThan(100, "the item text draws");
    }

    [Fact]
    public void Clicking_an_item_selects_it_and_closes_the_drop_down()
    {
        var combo = new SfComboBox { ItemsSource = Fruits };
        var (host, _) = Host(combo);
        using var _host = host;

        host.Tap(combo);
        host.Render();
        host.Render();
        ClickItem(host, combo, "Cherry");

        combo.SelectedItem.Should().Be("Cherry");
        combo.IsDropDownOpen.Should().BeFalse();
        IsPopupOpenBelow(combo).Should().BeFalse();
    }

    [Fact]
    public void Clicking_outside_closes_the_drop_down_and_reaches_the_view_there()
    {
        var combo = new SfComboBox { ItemsSource = Fruits };
        var (host, below) = Host(combo);
        using var _host = host;
        int clicked = 0;
        below.Clicked += (_, _) => clicked++;

        host.Tap(combo);
        host.Render();
        combo.IsDropDownOpen.Should().BeTrue();

        var (x, y) = CompatHost.CenterOf(below);
        Click(host, x, y);

        combo.IsDropDownOpen.Should().BeFalse();
        IsPopupOpenBelow(combo).Should().BeFalse();
        clicked.Should().Be(1, "the press outside still reaches the view under it");
        combo.SelectedItem.Should().BeNull();
    }

    [Fact]
    public void Clicking_the_combo_box_again_closes_the_drop_down()
    {
        var combo = new SfComboBox { ItemsSource = Fruits };
        var (host, _) = Host(combo);
        using var _host = host;

        host.Tap(combo);
        host.Render();
        combo.IsDropDownOpen.Should().BeTrue();

        host.Tap(combo);
        host.Render();

        combo.IsDropDownOpen.Should().BeFalse();
        IsPopupOpenBelow(combo).Should().BeFalse();
    }

    [Fact]
    public void Opening_from_code_shows_the_drop_down_and_closing_hides_it()
    {
        var combo = new SfComboBox { ItemsSource = Fruits };
        var (host, _) = Host(combo);
        using var _host = host;

        combo.IsDropDownOpen = true;
        host.Render();
        host.Render();
        IsPopupOpenBelow(combo).Should().BeTrue();
        ItemLabels(combo).Should().NotBeEmpty();

        combo.IsDropDownOpen = false;
        host.Render();
        IsPopupOpenBelow(combo).Should().BeFalse();
    }

    [Fact]
    public void A_drop_down_near_the_bottom_opens_above_the_control()
    {
        var combo = new SfComboBox { ItemsSource = Fruits };
        var (host, _) = Host(combo, top: 500);
        using var _host = host;
        var comboRect = CompatHost.PlatformOf(combo).ScreenBounds;

        host.Tap(combo);
        host.Render();
        host.Render();

        var label = ItemLabels(combo).First(l => TextOf(l) == "Apple");
        CompatHost.PlatformOf(label).ScreenBounds.Bottom.Should().BeLessThanOrEqualTo(comboRect.Top + 1, "there is no room below");
    }

    [Fact]
    public void The_wheel_over_the_drop_down_scrolls_its_list()
    {
        var items = Enumerable.Range(1, 40).Select(i => $"Item {i}").ToArray();
        var combo = new SfComboBox { ItemsSource = items, MaxDropDownHeight = 200 };
        var (host, _) = Host(combo);
        using var _host = host;
        host.Tap(combo);
        host.Render();
        host.Render();
        var listScroll = ListViewOf(combo).GetVisualTreeDescendants().OfType<ScrollView>().First();
        listScroll.ScrollY.Should().Be(0);

        var comboRect = CompatHost.PlatformOf(combo).ScreenBounds;
        Wheel(host, comboRect.Center.X, comboRect.Bottom + 60, 3);
        host.Render();
        host.Render();

        combo.IsDropDownOpen.Should().BeTrue();
        listScroll.ScrollY.Should().BeGreaterThan(0, "the wheel scrolls the list, not the page");
    }

    [Fact]
    public void An_autocomplete_suggests_matches_and_a_click_picks_one()
    {
        var auto = new SfAutocomplete { ItemsSource = Fruits, TextSearchMode = AutocompleteTextSearchMode.Contains };
        var (host, _) = Host(auto);
        using var _host = host;

        host.Tap(auto);
        host.Render();
        host.DisplayWindow.RaiseTextInput("an");
        RenderUntil(host, () => IsPopupOpenBelow(auto));

        auto.IsDropDownOpen.Should().BeTrue("typing opens the suggestions");
        IsPopupOpenBelow(auto).Should().BeTrue();
        var suggestions = ItemLabels(auto).Select(TextOf).ToList();
        suggestions.Should().Contain(new[] { "Banana", "Mango" });

        ClickItem(host, auto, "Mango");

        auto.SelectedItem.Should().Be("Mango");
        auto.IsDropDownOpen.Should().BeFalse();
        IsPopupOpenBelow(auto).Should().BeFalse();
    }

    [Fact]
    public void Arrow_keys_and_enter_pick_from_an_open_drop_down()
    {
        var combo = new SfComboBox { ItemsSource = Fruits };
        var (host, _) = Host(combo);
        using var _host = host;

        host.Tap(combo);
        host.Render();
        combo.IsDropDownOpen.Should().BeTrue();

        host.DisplayWindow.RaiseKeyDown(Key.Down);
        host.DisplayWindow.RaiseKeyDown(Key.Down);
        host.Render();
        host.DisplayWindow.RaiseKeyDown(Key.Enter);
        host.Render();

        combo.SelectedItem.Should().Be("Banana");
        combo.IsDropDownOpen.Should().BeFalse();
    }

    [Fact]
    public void Escape_closes_an_open_drop_down()
    {
        var combo = new SfComboBox { ItemsSource = Fruits };
        var (host, _) = Host(combo);
        using var _host = host;

        host.Tap(combo);
        host.Render();
        combo.IsDropDownOpen.Should().BeTrue();

        host.DisplayWindow.RaiseKeyDown(Key.Escape);
        host.Render();

        combo.IsDropDownOpen.Should().BeFalse();
        IsPopupOpenBelow(combo).Should().BeFalse();
        combo.SelectedItem.Should().BeNull();
    }

    [Fact]
    public void Arrow_keys_and_enter_pick_an_autocomplete_suggestion()
    {
        var auto = new SfAutocomplete { ItemsSource = Fruits, TextSearchMode = AutocompleteTextSearchMode.Contains };
        var (host, _) = Host(auto);
        using var _host = host;

        host.Tap(auto);
        host.Render();
        host.DisplayWindow.RaiseTextInput("an");
        RenderUntil(host, () => IsPopupOpenBelow(auto));
        auto.IsDropDownOpen.Should().BeTrue();

        host.DisplayWindow.RaiseKeyDown(Key.Down);
        host.DisplayWindow.RaiseKeyDown(Key.Down);
        host.Render();
        if (Environment.GetEnvironmentVariable("DROPDOWN_FRAME") is { Length: > 0 } frame) host.SaveFrame(frame);
        host.DisplayWindow.RaiseKeyDown(Key.Enter);
        host.Render();

        auto.SelectedItem.Should().Be("Mango");
        auto.IsDropDownOpen.Should().BeFalse();
    }
}
