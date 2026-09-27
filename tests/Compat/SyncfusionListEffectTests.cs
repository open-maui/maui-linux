// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Xunit;
using Xunit.Abstractions;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// An effect on a view inside an SfListView row sees the pointer, as it does outside a
/// list (MarketAlly.TouchEffect's Linux head listens to SkiaView.PointerRouted; Strikeline's
/// Outlook watchlist rows did not respond while its sign-in and PIN buttons did).
/// </summary>
[Collection("LinuxApplication.Current")]
public sealed class SyncfusionListEffectTests(ITestOutputHelper output)
{
    public sealed class ProbeEffect : RoutingEffect { }

    public sealed class ProbePlatformEffect : Microsoft.Maui.Controls.Platform.PlatformEffect
    {
        public static readonly List<string> Seen = new();
        protected override void OnAttached()
        {
            lock (Seen) Seen.Add("attached");
            if (Control is SkiaView view)
                view.PointerRouted += (_, e) => { lock (Seen) Seen.Add(e.Kind.ToString()); };
        }
        protected override void OnDetached() { }
    }

    [Fact]
    public void An_effect_in_a_list_row_sees_press_and_release()
    {
        ProbePlatformEffect.Seen.Clear();
        var rows = new[] { "AAPL", "COST", "GME" };
        var list = new Syncfusion.Maui.ListView.SfListView
        {
            ItemsSource = rows,
            AutoFitMode = Syncfusion.Maui.ListView.AutoFitMode.DynamicHeight,
            SelectionMode = Syncfusion.Maui.ListView.SelectionMode.None,
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label { FontSize = 18 };
                label.SetBinding(Label.TextProperty, ".");
                var grid = new Grid { Margin = 4, Padding = 2, Children = { label } };
                grid.Effects.Add(new ProbeEffect());
                return new ContentView { Content = grid };
            }),
        };
        using var host = new CompatHost(new ContentPage { Content = list },
            b => { b.UseLinuxSyncfusion(); b.ConfigureEffects(e => e.Add<ProbeEffect, ProbePlatformEffect>()); }, 400, 400);
        host.Render();
        host.Render();

        lock (ProbePlatformEffect.Seen) ProbePlatformEffect.Seen.Should().Contain("attached");
        host.DisplayWindow.RaisePointerMoved(100, 15);
        host.DisplayWindow.RaisePointerPressed(100, 15);
        host.DisplayWindow.RaisePointerReleased(100, 15);
        host.Render();

        output.WriteLine(string.Join(", ", ProbePlatformEffect.Seen));
        ProbePlatformEffect.Seen.Should().Contain(new[] { "Pressed", "Released" });
    }

    public sealed class RowProbe : RoutingEffect { }

    /// <summary>What TouchEffect's Linux head does: a press, then a release inside ScreenBounds.</summary>
    public sealed class RowProbePlatformEffect : Microsoft.Maui.Controls.Platform.PlatformEffect
    {
        public static readonly List<string> Taps = new();
        public static readonly List<object?> Attached = new();
        protected override void OnAttached()
        {
            lock (Attached) Attached.Add(((BindableObject)Element).BindingContext);
            if (Control is not SkiaView view) return;
            bool pressed = false;
            view.PointerRouted += (_, e) =>
            {
                if (e.Kind == SkiaView.RoutedPointerKind.Pressed) pressed = true;
                else if (e.Kind == SkiaView.RoutedPointerKind.Released && pressed)
                {
                    pressed = false;
                    if (view.ScreenBounds.Contains(e.Pointer.X, e.Pointer.Y))
                        lock (Taps) Taps.Add((string)((BindableObject)Element).BindingContext);
                }
            };
        }
        protected override void OnDetached() { }
    }

    [Fact]
    public async Task A_row_of_a_scrolled_list_is_tapped_where_it_is_drawn()
    {
        RowProbePlatformEffect.Taps.Clear();
        var rows = Enumerable.Range(0, 20).Select(i => $"R{i}").ToArray();
        var list = new Syncfusion.Maui.ListView.SfListView
        {
            ItemsSource = rows,
            ItemSize = 50,
            SelectionMode = Syncfusion.Maui.ListView.SelectionMode.None,
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label { FontSize = 18 };
                label.SetBinding(Label.TextProperty, ".");
                var grid = new Grid { Margin = 4, Padding = 2, Children = { label } };
                grid.Effects.Add(new RowProbe());
                return new ContentView { Content = grid };
            }),
        };
        using var host = new CompatHost(new ContentPage { Content = new Grid { Padding = new Thickness(0, 100, 0, 0), Children = { list } } },
            b => { b.UseLinuxSyncfusion(); b.ConfigureEffects(e => e.Add<RowProbe, RowProbePlatformEffect>()); }, 400, 500);
        host.Render();
        host.Render();

        var scroller = list.GetVisualTreeDescendants().OfType<ScrollView>().First();
        await scroller.ScrollToAsync(0, 60, false);
        for (int i = 0; i < 3; i++) { Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll(); host.Render(); }

        // Row 3 is drawn at 100 + 150 - 60 = 190 .. 240 in the window. The routed pointer is
        // in window coordinates, as ScreenBounds is: it was in the list's scrolled content
        // space, 60 px lower, so the release fell outside the row and the tap was cancelled.
        host.DisplayWindow.RaisePointerMoved(100, 215);
        host.DisplayWindow.RaisePointerPressed(100, 215);
        host.DisplayWindow.RaisePointerReleased(100, 215);
        host.Render();


        RowProbePlatformEffect.Taps.Should().Equal("R3");
    }

    /// <summary>TouchEffect's pattern: a bound attached property adds the effect when it changes.</summary>
    public static class BoundProbe
    {
        public static readonly BindableProperty TagProperty = BindableProperty.CreateAttached("Tag", typeof(string), typeof(BoundProbe), null,
            propertyChanged: (b, _, _) =>
            {
                if (b is VisualElement v && !v.Effects.OfType<RowProbe>().Any())
                    v.Effects.Add(new RowProbe());
            });
    }

    [Fact]
    public void An_effect_added_by_a_bound_attached_property_attaches_in_a_list_row()
    {
        RowProbePlatformEffect.Taps.Clear(); RowProbePlatformEffect.Attached.Clear();
        var grids = new List<Grid>();
        var list = new Syncfusion.Maui.ListView.SfListView
        {
            ItemsSource = new[] { "A", "B", "C" },
            ItemSize = 50,
            SelectionMode = Syncfusion.Maui.ListView.SelectionMode.None,
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label();
                label.SetBinding(Label.TextProperty, ".");
                var grid = new Grid { Children = { label } };
                grid.SetBinding(BoundProbe.TagProperty, ".");
                lock (grids) grids.Add(grid);
                return new ContentView { Content = grid };
            }),
        };
        using var host = new CompatHost(new ContentPage { Content = list },
            b => { b.UseLinuxSyncfusion(); b.ConfigureEffects(e => e.Add<RowProbe, RowProbePlatformEffect>()); }, 400, 400);
        host.Render();
        host.Render();

        var shown = grids.Where(g => g.Handler != null).ToList();
        shown.Should().NotBeEmpty();
        // Each shown row's effect resolved to its platform effect, which attached to the row.
        foreach (var g in shown)
            RowProbePlatformEffect.Attached.Should().Contain(g.BindingContext);
    }
}
