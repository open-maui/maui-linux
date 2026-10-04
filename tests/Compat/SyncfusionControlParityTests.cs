// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Reflection;
using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using SkiaSharp;
using Syncfusion.Maui.Buttons;
using Syncfusion.Maui.Calendar;
using Syncfusion.Maui.Carousel;
using Syncfusion.Maui.Core;
using Syncfusion.Maui.Core.Carousel;
using Syncfusion.Maui.Core.Internals;
using Syncfusion.Maui.Graphics.Internals;
using Syncfusion.Maui.Inputs;
using Syncfusion.Maui.Picker;
using Syncfusion.Maui.Sliders;
using Xunit;
using Colors = Microsoft.Maui.Graphics.Colors;
using MauiKey = Microsoft.Maui.Platform.Key;
using PlatformKeyEventArgs = Microsoft.Maui.Platform.KeyEventArgs;
using KeyModifiers = Microsoft.Maui.Platform.KeyModifiers;
using ScrollEventArgs = Microsoft.Maui.Platform.ScrollEventArgs;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// Windows behaviour of Syncfusion's controls that the platform-neutral build
/// leaves out and the Linux bridge restores: carousel virtualization, picker
/// column drags, pan velocity, shift+wheel, calendar flow direction, Tab
/// stops, semantics, drop-down chips and sizing, and the text input layout's
/// inner field.
/// </summary>
[Collection(CompatHost.Collection)]
public sealed class SyncfusionControlParityTests
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private static readonly Color[] Palette = { Colors.Red, Colors.Lime, Colors.Blue, Colors.Orange, Colors.Magenta };

    private static void Settle(CompatHost host)
    {
        for (int i = 0; i < 3; i++)
            host.Render();
    }

    private static void Raise<T>(CompatHost host, string name, T args) where T : EventArgs
    {
        var field = host.DisplayWindow.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((EventHandler<T>?)field.GetValue(host.DisplayWindow))?.Invoke(host.DisplayWindow, args);
    }

    private static void Key(CompatHost host, MauiKey key, KeyModifiers modifiers = KeyModifiers.None)
    {
        Raise(host, "KeyDown", new PlatformKeyEventArgs(key, modifiers));
        Raise(host, "KeyUp", new PlatformKeyEventArgs(key, modifiers));
        Settle(host);
    }

    private static void Click(CompatHost host, float x, float y)
    {
        host.DisplayWindow.RaisePointerMoved(x, y);
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        Settle(host);
    }

    private static SKColor Sk(Color c) => new((byte)(c.Red * 255), (byte)(c.Green * 255), (byte)(c.Blue * 255));

    private static SKRectI Around(double x, double y, int r = 6) => new((int)x - r, (int)y - r, (int)x + r, (int)y + r);

    private static ContentPage PageWith(View view) => new()
    {
        BackgroundColor = Colors.White,
        Content = new Grid
        {
            WidthRequest = view.WidthRequest,
            HeightRequest = view.HeightRequest,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Children = { view },
        },
    };

    // ---- SfCarousel EnableVirtualization ---------------------------------------

    [Fact]
    public void A_virtualizing_carousel_builds_only_the_items_in_view()
    {
        int built = 0;
        var items = Enumerable.Range(0, 60).Select(i => (object)Palette[i % Palette.Length]).ToList();
        var carousel = new SfCarousel
        {
            ItemsSource = items,
            ItemTemplate = new DataTemplate(() =>
            {
                built++;
                var box = new BoxView();
                box.SetBinding(BoxView.ColorProperty, ".");
                return box;
            }),
            ItemWidth = 100,
            ItemHeight = 150,
            Duration = 0,
            WidthRequest = 600,
            HeightRequest = 300,
            HorizontalOptions = LayoutOptions.Start,
            EnableVirtualization = true,
        };
        using var host = new CompatHost(PageWith(carousel), b => b.UseLinuxSyncfusion(), 700, 400);
        Settle(host);

        var handler = carousel.Handler.Should().BeOfType<SfCarouselBridgeHandler>().Subject;
        int realized = (int)typeof(SfCarouselBridgeHandler).GetProperty("RealizedCount", Any)!.GetValue(handler)!;
        realized.Should().BeInRange(2, 30, "only the items around the selection get views");
        built.Should().BeLessThan(60);
        host.CountPixelsNear(Sk(Colors.Red), Around(300, 150)).Should().BeGreaterThan(100, "the selected item is shown");

        carousel.SelectedIndex = 40;
        Settle(host);
        host.CountPixelsNear(Sk(Palette[40 % Palette.Length]), Around(300, 150)).Should().BeGreaterThan(100, "the item scrolled to is built when it comes into view");
        built.Should().BeLessThan(60);
    }

    // ---- Picker column drag ----------------------------------------------------

    [Fact]
    public void Dragging_a_picker_column_selects_the_item_left_at_the_centre()
    {
        var column = new PickerColumn { ItemsSource = Enumerable.Range(0, 20).Select(i => $"Item {i}").ToList(), SelectedIndex = 0 };
        var picker = new SfPicker { WidthRequest = 200, HeightRequest = 250, HorizontalOptions = LayoutOptions.Start };
        picker.Columns.Add(column);
        using var host = new CompatHost(PageWith(picker), b => b.UseLinuxSyncfusion(), 400, 400);
        Settle(host);

        var columnView = picker.GetVisualTreeDescendants().OfType<View>().First(v => v.GetType().FullName == "Syncfusion.Maui.Picker.PickerView");
        var bounds = CompatHost.PlatformOf(columnView).ScreenBounds;
        var scroll = (ScrollView)columnView.Parent;
        var viewport = CompatHost.PlatformOf(scroll).ScreenBounds;
        float x = (float)viewport.Center.X, y = (float)viewport.Center.Y;
        host.DisplayWindow.RaisePointerMoved(x, y);
        host.DisplayWindow.RaisePointerPressed(x, y);
        for (int i = 1; i <= 10; i++)
        {
            host.DisplayWindow.RaisePointerMoved(x, y - i * 15);
            host.Render();
        }
        host.DisplayWindow.RaisePointerReleased(x, y - 150);
        Settle(host);

        column.SelectedIndex.Should().BeGreaterThan(0, "dragging the column up by three rows moves the selection down the list");
        bounds.Height.Should().BeGreaterThan(0);
    }

    // ---- Pan velocity (inertia) ------------------------------------------------

    private sealed class PanProbe : SfView, IPanGestureListener
    {
        public PanProbe() => this.AddGestureListener(this);

        public List<PanEventArgs> Pans { get; } = new();

        public void OnPan(PanEventArgs e) => Pans.Add(e);
    }

    [Fact]
    public void A_pan_released_while_moving_reports_its_velocity()
    {
        var probe = new PanProbe { WidthRequest = 300, HeightRequest = 200, BackgroundColor = Colors.LightGray };
        using var host = new CompatHost(PageWith(probe), b => b.UseLinuxSyncfusion(), 400, 300);
        Settle(host);

        var nowProperty = typeof(LinuxSyncfusionBuilderExtensions).Assembly
            .GetType("Microsoft.Maui.Platform.Linux.Syncfusion.SfPanWheelBridge")!.GetProperty("Now", Any)!;
        var original = nowProperty.GetValue(null);
        long ticks = 0;
        long perMs = System.Diagnostics.Stopwatch.Frequency / 1000;
        nowProperty.SetValue(null, new Func<long>(() => ticks));
        try
        {
            host.DisplayWindow.RaisePointerMoved(250, 100);
            host.DisplayWindow.RaisePointerPressed(250, 100);
            for (int i = 1; i <= 5; i++)
            {
                ticks += 10 * perMs;
                host.DisplayWindow.RaisePointerMoved(250 - i * 20, 100);
            }
            host.DisplayWindow.RaisePointerReleased(150, 100);
            Settle(host);
            var fling = probe.Pans.Last();
            fling.Status.Should().Be(GestureStatus.Completed);
            fling.Velocity.X.Should().BeApproximately(-2000, 1, "100 px left in 50 ms is 2000 px/s, as WinUI's inertia reports it");

            probe.Pans.Clear();
            host.DisplayWindow.RaisePointerPressed(250, 100);
            ticks += 10 * perMs;
            host.DisplayWindow.RaisePointerMoved(200, 100);
            ticks += 500 * perMs; // held still before the release
            host.DisplayWindow.RaisePointerReleased(200, 100);
            Settle(host);
            probe.Pans.Last().Velocity.Should().Be(Point.Zero, "a drag that stopped has no inertia");
        }
        finally
        {
            nowProperty.SetValue(null, original);
        }
    }

    // ---- SfInteractiveScrollView shift+wheel ---------------------------------

    [Fact]
    public void Shift_and_the_wheel_scroll_the_interactive_scroll_view_sideways()
    {
        var row = new HorizontalStackLayout();
        foreach (var color in Palette)
            row.Add(new BoxView { Color = color, WidthRequest = 200, HeightRequest = 100 });
        var scroller = new SfInteractiveScrollView { Content = row, Orientation = Microsoft.Maui.ScrollOrientation.Both, HeightRequest = 200, WidthRequest = 300, HorizontalOptions = LayoutOptions.Start };
        using var host = new CompatHost(PageWith(scroller), b => b.UseLinuxSyncfusion(), 400, 300);
        Settle(host);

        var platform = (SkiaScrollView)CompatHost.PlatformOf(scroller);
        platform.OnScroll(new ScrollEventArgs(100, 100, 0, 1, KeyModifiers.Shift));
        Settle(host);
        platform.ScrollX.Should().BeGreaterThan(0, "with Shift the wheel scrolls horizontally, as with Windows' vertical scrolling turned off");
        platform.ScrollY.Should().Be(0);
    }

    // ---- SfCalendar FlowDirection ----------------------------------------------

    [Fact]
    public void Switching_a_shown_calendar_to_right_to_left_lays_it_out_again()
    {
        var calendar = new SfCalendar { WidthRequest = 350, HeightRequest = 350, HorizontalOptions = LayoutOptions.Start };
        using var host = new CompatHost(PageWith(calendar), b => b.UseLinuxSyncfusion(), 400, 400);
        Settle(host);

        calendar.FlowDirection = Microsoft.Maui.FlowDirection.RightToLeft;
        Settle(host);

        var header = typeof(SfCalendar).GetField("headerLayout", Any)!.GetValue(calendar)!;
        var label = (VisualElement)header.GetType().GetField("headerTextLabel", Any)!.GetValue(header)!;
        label.FlowDirection.Should().Be(Microsoft.Maui.FlowDirection.RightToLeft, "the Windows change handler lays the header out for the new direction");
    }

    // ---- Tab stops -------------------------------------------------------------

    [Fact]
    public void Tab_reaches_syncfusion_buttons_and_segmented_controls()
    {
        var entry = new Entry();
        var button = new SfButton { Text = "Go", WidthRequest = 120, HeightRequest = 40 };
        var segmented = new SfSegmentedControl { ItemsSource = new[] { "One", "Two" }, WidthRequest = 200, HeightRequest = 40 };
        var page = new ContentPage { Content = new VerticalStackLayout { Children = { entry, button, segmented } } };
        using var host = new CompatHost(page, b => b.UseLinuxSyncfusion(), 400, 300);
        Settle(host);

        CompatHost.PlatformOf(button).IsFocusable.Should().BeTrue("Windows makes SfButton's native view a tab stop");
        CompatHost.PlatformOf(segmented).IsFocusable.Should().BeTrue();

        var (ex, ey) = CompatHost.CenterOf(entry);
        Click(host, ex, ey);
        host.Context.FocusedView.Should().BeSameAs(CompatHost.PlatformOf(entry));
        Key(host, MauiKey.Tab);
        host.Context.FocusedView.Should().BeSameAs(CompatHost.PlatformOf(button));
        Key(host, MauiKey.Tab);
        host.Context.FocusedView.Should().BeSameAs(CompatHost.PlatformOf(segmented));
        Key(host, MauiKey.Tab, KeyModifiers.Shift);
        host.Context.FocusedView.Should().BeSameAs(CompatHost.PlatformOf(button), "Shift+Tab goes back");
    }

    // ---- Semantics ---------------------------------------------------------------

    private sealed class SemanticProbe : SfView
    {
        public List<string> Parts { get; } = new() { "First", "Second" };
        public int Clicked { get; private set; }

        protected override List<SemanticsNode>? GetSemanticsNodesCore(double width, double height) =>
            Parts.Select((text, i) => new SemanticsNode
            {
                Id = i,
                Text = text,
                Bounds = new Rect(i * 50, 0, 50, 20),
                IsTouchEnabled = i == 0,
                OnClick = _ => Clicked++,
            }).ToList();

        public void Refresh() => typeof(SfView).GetMethod("InvalidateSemantics", Any)!.Invoke(this, null);
    }

    [Fact]
    public void A_syncfusion_views_drawn_parts_are_accessible_and_refresh()
    {
        var probe = new SemanticProbe { WidthRequest = 200, HeightRequest = 40 };
        using var host = new CompatHost(PageWith(probe), b => b.UseLinuxSyncfusion(), 300, 200);
        Settle(host);

        IAccessible accessible = CompatHost.PlatformOf(probe);
        accessible.Children.Select(c => c.AccessibleName).Should().Equal("First", "Second");
        accessible.Children[0].Role.Should().Be(AccessibleRole.Button);
        accessible.Children[1].Role.Should().Be(AccessibleRole.Label);
        accessible.Children[0].DoAction("click").Should().BeTrue();
        probe.Clicked.Should().Be(1);

        probe.Parts.Add("Third");
        accessible.Children.Should().HaveCount(2, "the list is cached until the view invalidates its semantics");
        probe.Refresh();
        accessible.Children.Select(c => c.AccessibleName).Should().Equal("First", "Second", "Third");
    }

    // ---- RTL slider touch (not mirrored on Linux) ------------------------------

    [Fact]
    public void A_right_to_left_slider_takes_the_value_under_the_pointer()
    {
        var slider = new SfSlider { Minimum = 0, Maximum = 100, Value = 50, WidthRequest = 300, HeightRequest = 60, FlowDirection = Microsoft.Maui.FlowDirection.RightToLeft };
        using var host = new CompatHost(PageWith(slider), b => b.UseLinuxSyncfusion(), 400, 200);
        Settle(host);

        var bounds = CompatHost.PlatformOf(slider).ScreenBounds;
        Click(host, (float)(bounds.Left + 30), (float)bounds.Center.Y);
        slider.Value.Should().BeGreaterThan(75, "in a right-to-left slider the left end is the maximum, and OpenMaui does not mirror pointer coordinates");
    }

    // ---- Drop-downs --------------------------------------------------------------

    [Fact]
    public void Backspace_in_an_empty_combo_box_removes_the_last_chip()
    {
        var items = new List<string> { "Apple", "Banana", "Cherry" };
        var combo = new SfComboBox
        {
            ItemsSource = items,
            SelectionMode = ComboBoxSelectionMode.Multiple,
            IsEditable = true,
            WidthRequest = 300,
            HorizontalOptions = LayoutOptions.Start,
        };
        combo.SelectedItems = new ObservableCollection<object> { "Apple", "Banana" };
        var page = new ContentPage { Content = new VerticalStackLayout { Padding = 20, Children = { combo } } };
        using var host = new CompatHost(page, b => b.UseLinuxSyncfusion(), 500, 400);
        Settle(host);

        var input = (Entry)typeof(SfDropdownEntry).GetProperty("InputView", Any)!.GetValue(combo)!;
        input.Focus();
        host.Context.FocusedView = CompatHost.PlatformOf(input);
        Settle(host);

        Key(host, MauiKey.Backspace);
        Key(host, MauiKey.Backspace);
        combo.SelectedItems!.Count.Should().Be(1, "the first Backspace highlights the last chip, the second deletes it");
    }

    [Fact]
    public void A_combo_box_drop_down_is_as_wide_as_the_box_and_takes_its_selection_colour()
    {
        var combo = new SfComboBox { ItemsSource = new List<string> { "One", "Two" }, WidthRequest = 240, HorizontalOptions = LayoutOptions.Start, IsEditable = true, SelectionTextHighlightColor = Colors.Red };
        var page = new ContentPage { Content = new VerticalStackLayout { Padding = 20, Children = { combo } } };
        using var host = new CompatHost(page, b => b.UseLinuxSyncfusion(), 500, 400);
        Settle(host);

        var dropDownView = typeof(SfDropdownEntry).GetProperty("DropDownView", Any)!.GetValue(combo)!;
        ((double)dropDownView.GetType().GetProperty("PopupWidth", Any)!.GetValue(dropDownView)!).Should().Be(240);
        var input = (Entry)typeof(SfDropdownEntry).GetProperty("InputView", Any)!.GetValue(combo)!;
        ((SkiaEntry)CompatHost.PlatformOf(input)).SelectionColor.Should().Be(Colors.Red);
    }

    // ---- SfTextInputLayout --------------------------------------------------------

    [Fact]
    public void A_text_input_layout_takes_its_entrys_own_box_away()
    {
        var entry = new Entry { Text = "Hello" };
        var layout = new SfTextInputLayout { Hint = "Name", Content = entry, WidthRequest = 250, HorizontalOptions = LayoutOptions.Start };
        var page = new ContentPage { Content = new VerticalStackLayout { Padding = 20, Children = { layout } } };
        using var host = new CompatHost(page, b => b.UseLinuxSyncfusion(), 400, 300);
        Settle(host);

        var skia = (SkiaEntry)CompatHost.PlatformOf(entry);
        skia.EntryBackgroundColor.Alpha.Should().Be(0, "Windows strips the TextBox's background");
        skia.BorderWidth.Should().Be(0);
        skia.Padding.Should().Be(new Thickness(0));
    }

    [Fact]
    public void A_numeric_entrys_text_box_follows_the_controls_flow_direction()
    {
        var numeric = new SfNumericEntry { Value = 3, WidthRequest = 200, FlowDirection = Microsoft.Maui.FlowDirection.RightToLeft };
        using var host = new CompatHost(PageWith(numeric), b => b.UseLinuxSyncfusion(), 300, 200);
        Settle(host);
        var textBox = (Entry)typeof(SfNumericEntry).GetField("textBox", Any)!.GetValue(numeric)!;
        CompatHost.PlatformOf(textBox).FlowDirection.Should().Be(Microsoft.Maui.FlowDirection.RightToLeft,
            "the Windows build sets the TextBox wrapper's FlowDirection; OpenMaui maps the inherited direction");
    }

    // ---- Focus kept inside a control's own buttons --------------------------------

    [Fact]
    public void Clicking_a_numeric_entrys_spin_button_keeps_its_text_box_focused()
    {
        var numeric = new SfNumericEntry { Value = 5, UpDownPlacementMode = NumericEntryUpDownPlacementMode.Inline, WidthRequest = 220, HeightRequest = 40 };
        using var host = new CompatHost(PageWith(numeric), b => b.UseLinuxSyncfusion(), 300, 200);
        Settle(host);
        var textBox = (Entry)typeof(SfNumericEntry).GetField("textBox", Any)!.GetValue(numeric)!;
        var (tx, ty) = CompatHost.CenterOf(textBox);
        Click(host, tx, ty);
        host.Context.FocusedView.Should().BeSameAs(CompatHost.PlatformOf(textBox));

        var bounds = CompatHost.PlatformOf(numeric).ScreenBounds;
        Click(host, (float)(bounds.Right - 12), (float)bounds.Center.Y);
        numeric.Value.Should().NotBe(5, "the click hit a spin button");
        host.Context.FocusedView.Should().BeSameAs(CompatHost.PlatformOf(textBox),
            "focus stays in the text box, as the Windows build's SetInputViewFocus restores it");
    }

    [Fact]
    public void Clicking_a_text_input_layouts_password_toggle_keeps_its_entry_focused()
    {
        var entry = new Entry { Text = "secret", IsPassword = true };
        var layout = new SfTextInputLayout { Hint = "Password", Content = entry, EnablePasswordVisibilityToggle = true, WidthRequest = 250 };
        using var host = new CompatHost(PageWith(layout), b => b.UseLinuxSyncfusion(), 300, 200);
        Settle(host);
        var (ex, ey) = CompatHost.CenterOf(entry);
        Click(host, ex, ey);
        host.Context.FocusedView.Should().BeSameAs(CompatHost.PlatformOf(entry));

        var bounds = CompatHost.PlatformOf(layout).ScreenBounds;
        Click(host, (float)(bounds.Right - 20), ey);
        entry.IsPassword.Should().BeFalse("the click hit the visibility toggle");
        host.Context.FocusedView.Should().BeSameAs(CompatHost.PlatformOf(entry),
            "the entry keeps focus, which the Windows build ensures by cancelling the focus loss (WindowsEntry_LosingFocus)");
    }

    // ---- Picker keyboard ------------------------------------------------------------

    [Fact]
    public void A_clicked_picker_column_takes_the_arrow_keys()
    {
        var column = new PickerColumn { ItemsSource = Enumerable.Range(0, 20).Select(i => $"Item {i}").ToList(), SelectedIndex = 0 };
        var picker = new SfPicker { WidthRequest = 200, HeightRequest = 250, HorizontalOptions = LayoutOptions.Start };
        picker.Columns.Add(column);
        using var host = new CompatHost(PageWith(picker), b => b.UseLinuxSyncfusion(), 400, 400);
        Settle(host);

        var columnView = picker.GetVisualTreeDescendants().OfType<View>().First(v => v.GetType().FullName == "Syncfusion.Maui.Picker.PickerView");
        var viewport = CompatHost.PlatformOf((ScrollView)columnView.Parent).ScreenBounds;
        Click(host, (float)viewport.Center.X, (float)viewport.Center.Y);
        int before = column.SelectedIndex;
        Key(host, MauiKey.Down);
        column.SelectedIndex.Should().Be(before + 1, "the column's keyboard listener moves the selection (the Windows ScrollViewer's key hook does nothing more)");
    }

    // ---- SfPopup right to left --------------------------------------------------------

    [Fact]
    public void A_right_to_left_popup_lays_its_view_out_right_to_left()
    {
        var page = new ContentPage { BackgroundColor = Colors.White, Content = new Grid() };
        using var host = new CompatHost(page, b => b.UseLinuxSyncfusion(), 800, 600);
        IPlatformApplication.Current = host.LinuxApp;
        try
        {
            Settle(host);
            var popup = new Syncfusion.Maui.Popup.SfPopup
            {
                HeaderTitle = "Title",
                Message = "Hello",
                WidthRequest = 300,
                HeightRequest = 200,
                AnimationMode = Syncfusion.Maui.Popup.PopupAnimationMode.None,
                FlowDirection = Microsoft.Maui.FlowDirection.RightToLeft,
            };
            popup.Show();
            Settle(host);

            var popupView = (View)typeof(Syncfusion.Maui.Popup.SfPopup).GetField("PopupView", Any)!.GetValue(popup)!;
            CompatHost.PlatformOf(popupView).FlowDirection.Should().Be(Microsoft.Maui.FlowDirection.RightToLeft,
                "the Windows build's UpdateRTL sets the native container's direction; here the popup view's own direction is mapped");
            popup.IsOpen = false;
            Settle(host);
        }
        finally
        {
            IPlatformApplication.Current = null;
        }
    }
}
