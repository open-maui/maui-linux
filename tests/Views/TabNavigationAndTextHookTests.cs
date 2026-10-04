// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;
using KeyModifiers = Microsoft.Maui.Platform.KeyModifiers;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// Tab and Shift+Tab move focus between the page's focusable views (WinUI's
/// default tab navigation); SkiaEntry's text, paste and cut seams and
/// MAUI-style programmatic selection; a view's accessible children refresh.
/// </summary>
[Collection("LinuxApplication.Current")]
public class TabNavigationAndTextHookTests
{
    private static void Key(HeadlessMauiHost host, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        var field = host.DisplayWindow.GetType().GetField("KeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        ((EventHandler<KeyEventArgs>?)field.GetValue(host.DisplayWindow))?.Invoke(host.DisplayWindow, new KeyEventArgs(key, modifiers));
    }

    private static SkiaView Platform(VisualElement view) => (SkiaView)view.Handler!.PlatformView!;

    [Fact]
    public void Tab_and_shift_tab_cycle_through_the_pages_focusable_views()
    {
        var first = new Entry();
        var label = new Label { Text = "Not a stop" };
        var button = new Button { Text = "OK" };
        var hidden = new Entry { IsVisible = false };
        var last = new Entry();
        var page = new ContentPage { Content = new VerticalStackLayout { first, label, button, hidden, last } };
        using var host = new HeadlessMauiHost(page, withEngine: true);
        host.Context.Render();

        Key(host, Microsoft.Maui.Platform.Key.Tab);
        host.Context.FocusedView.Should().BeSameAs(Platform(first), "with nothing focused, Tab focuses the first stop");
        Key(host, Microsoft.Maui.Platform.Key.Tab);
        host.Context.FocusedView.Should().BeSameAs(Platform(button), "labels are not stops");
        Key(host, Microsoft.Maui.Platform.Key.Tab);
        host.Context.FocusedView.Should().BeSameAs(Platform(last), "hidden views are skipped");
        Key(host, Microsoft.Maui.Platform.Key.Tab);
        host.Context.FocusedView.Should().BeSameAs(Platform(first), "Tab wraps around");
        Key(host, Microsoft.Maui.Platform.Key.Tab, KeyModifiers.Shift);
        host.Context.FocusedView.Should().BeSameAs(Platform(last), "Shift+Tab goes back");
    }

    [Fact]
    public void Ctrl_tab_does_not_move_focus()
    {
        var first = new Entry();
        var second = new Entry();
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { first, second } }, withEngine: true);
        host.Context.Render();
        host.Context.FocusedView = Platform(first);

        Key(host, Microsoft.Maui.Platform.Key.Tab, KeyModifiers.Control);
        host.Context.FocusedView.Should().BeSameAs(Platform(first));
    }

    [Fact]
    public void A_text_input_observer_can_take_typed_text()
    {
        var entry = new SkiaEntry { Text = "ab" };
        entry.CursorPosition = 2;
        entry.TextInputting += (_, e) =>
        {
            if (e.Text == "x")
                e.Handled = true;
        };

        entry.OnTextInput(new TextInputEventArgs("x"));
        entry.Text.Should().Be("ab", "the observer took the text");
        entry.OnTextInput(new TextInputEventArgs("c"));
        entry.Text.Should().Be("abc");
        entry.OnTextCommitted("x");
        entry.Text.Should().Be("abc", "committed input-method text goes through the observer too");
    }

    [Fact]
    public void Paste_and_cut_observers_can_take_the_clipboard_action()
    {
        var entry = new SkiaEntry { Text = "hello" };
        bool pasted = false, cut = false;
        entry.Pasting += (_, e) => { pasted = true; e.Handled = true; };
        entry.Cutting += (_, e) => { cut = true; e.Handled = true; };
        entry.SelectAll();

        entry.OnKeyDown(new KeyEventArgs(Microsoft.Maui.Platform.Key.X, KeyModifiers.Control));
        cut.Should().BeTrue();
        entry.Text.Should().Be("hello", "the observer handled the cut");
        entry.OnKeyDown(new KeyEventArgs(Microsoft.Maui.Platform.Key.V, KeyModifiers.Control));
        pasted.Should().BeTrue();
        entry.Text.Should().Be("hello");
    }

    [Fact]
    public void A_selection_set_from_code_starts_at_the_caret()
    {
        var entry = new SkiaEntry { Text = "hello world" };
        entry.CursorPosition = 6;
        entry.SelectionLength = 5;
        entry.Selection.Should().Be((6, 5));

        entry.OnTextInput(new TextInputEventArgs("there"));
        entry.Text.Should().Be("hello there", "typing replaces the selected range, as MAUI's SelectionLength defines it");
    }

    private sealed class CountingView : SkiaView
    {
        public int Reads;

        protected override List<IAccessible> GetAccessibleChildren()
        {
            Reads++;
            return base.GetAccessibleChildren();
        }

        protected override void OnDraw(SkiaSharp.SKCanvas canvas, SkiaSharp.SKRect bounds)
        {
        }
    }

    [Fact]
    public void Invalidating_accessible_children_makes_them_read_again()
    {
        var view = new CountingView();
        IAccessible accessible = view;
        _ = accessible.Children;
        _ = accessible.Children;
        view.Reads.Should().Be(1, "the children are cached");

        view.InvalidateAccessibleChildren();
        _ = accessible.Children;
        view.Reads.Should().Be(2);
    }
}
