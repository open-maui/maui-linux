// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;
using KeyModifiers = Microsoft.Maui.Platform.KeyModifiers;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// Keyboard activation of a tappable Border (MAUI 11, Windows' GesturePlatformManager): a
/// Border with a single-tap primary-button TapGestureRecognizer, enabled and not
/// InputTransparent, is a tab stop with the focus visual, and Enter or Space (no modifier, not
/// auto-repeated) raises its taps at Point.Zero.
/// </summary>
[Collection("LinuxApplication.Current")]
public class BorderKeyboardActivationTests
{
    private static void Raise(HeadlessMauiHost host, string evt, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        var field = host.DisplayWindow.GetType().GetField(evt, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        ((EventHandler<KeyEventArgs>?)field.GetValue(host.DisplayWindow))?.Invoke(host.DisplayWindow, new KeyEventArgs(key, modifiers));
    }

    private static void Press(HeadlessMauiHost host, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        Raise(host, "KeyDown", key, modifiers);
        Raise(host, "KeyUp", key, modifiers);
    }

    private static SkiaBorder Platform(Border border) => (SkiaBorder)border.Handler!.PlatformView!;

    private static Border Tappable(TapGestureRecognizer tap) => new()
    {
        HeightRequest = 60,
        Content = new Label { Text = "Card" },
        GestureRecognizers = { tap },
    };

    [Fact]
    public void Tab_focuses_a_tappable_border_with_the_focus_visual_and_enter_or_space_taps_it()
    {
        int commands = 0;
        var positions = new List<Point?>();
        var tap = new TapGestureRecognizer { Command = new Command(() => commands++) };
        tap.Tapped += (s, e) => positions.Add(e.GetPosition(null));
        var border = Tappable(tap);
        var before = new Entry();
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { before, border } }, withEngine: true);
        host.Context.Render();
        var platform = Platform(border);

        platform.IsTapStop.Should().BeTrue();
        host.Context.TabStops().Should().Equal(new SkiaView[] { (SkiaView)before.Handler!.PlatformView!, platform });

        host.Context.FocusedView = (SkiaView)before.Handler!.PlatformView!;
        Press(host, Key.Tab);
        host.Context.FocusedView.Should().BeSameAs(platform);
        platform.ShowsKeyboardFocusVisual.Should().BeTrue("focus from the keyboard shows the system focus visual");

        Press(host, Key.Enter);
        Press(host, Key.Space);
        Press(host, Key.NumPadEnter);
        commands.Should().Be(3);
        positions.Should().Equal(new Point?[] { Point.Zero, Point.Zero, Point.Zero }, "a keyboard tap has no pointer position");

        Press(host, Key.Enter, KeyModifiers.Control);
        Press(host, Key.Space, KeyModifiers.Shift);
        Press(host, Key.Space, KeyModifiers.Alt);
        commands.Should().Be(3, "modifier chords do not activate");

        // A held key repeats its key-down: one tap per press.
        Raise(host, "KeyDown", Key.Enter);
        Raise(host, "KeyDown", Key.Enter);
        Raise(host, "KeyDown", Key.Enter);
        Raise(host, "KeyUp", Key.Enter);
        commands.Should().Be(4);
    }

    [Fact]
    public void The_focus_visual_is_drawn_for_keyboard_focus_only()
    {
        var border = Tappable(new TapGestureRecognizer());
        border.BackgroundColor = Colors.White;
        border.Stroke = Colors.White;
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { border } }, withEngine: true);
        host.Context.Render();
        var platform = Platform(border);

        SkiaSharp.SKColor Edge()
        {
            using var bitmap = new SkiaSharp.SKBitmap(400, 200);
            using var canvas = new SkiaSharp.SKCanvas(bitmap);
            canvas.Clear(SkiaSharp.SKColors.White);
            canvas.Translate(-(float)platform.Bounds.Left, -(float)platform.Bounds.Top);
            platform.Draw(canvas);
            return bitmap.GetPixel(20, 0);
        }

        // A click focuses it without the visual.
        var b = platform.Bounds;
        host.DisplayWindow.RaisePointerPressed((float)b.Left + 20, (float)b.Top + 20);
        host.DisplayWindow.RaisePointerReleased((float)b.Left + 20, (float)b.Top + 20);
        host.Context.FocusedView.Should().BeSameAs(platform);
        platform.ShowsKeyboardFocusVisual.Should().BeFalse();
        Edge().Red.Should().BeGreaterThan(200);

        host.Context.FocusedView = null;
        Press(host, Key.Tab);
        platform.ShowsKeyboardFocusVisual.Should().BeTrue();
        Edge().Red.Should().BeLessThan(80, "the outer ring is dark on a light theme");
    }

    [Fact]
    public void Only_a_single_tap_primary_recognizer_on_an_enabled_hit_testable_border_makes_a_stop()
    {
        var tap = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        var border = Tappable(tap);
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { border } }, withEngine: true);
        host.Context.Render();
        var platform = Platform(border);

        platform.IsTapStop.Should().BeFalse("a double tap is not activated by Enter");
        tap.NumberOfTapsRequired = 1;
        platform.IsTapStop.Should().BeTrue("the stop follows NumberOfTapsRequired");
        tap.Buttons = ButtonsMask.Secondary;
        platform.IsTapStop.Should().BeFalse("a secondary-only recognizer is not activated by Enter");
        tap.Buttons = ButtonsMask.Primary | ButtonsMask.Secondary;
        platform.IsTapStop.Should().BeTrue();

        border.IsEnabled = false;
        platform.IsTapStop.Should().BeFalse();
        platform.IsFocusable.Should().BeFalse();
        border.IsEnabled = true;
        border.InputTransparent = true;
        platform.IsTapStop.Should().BeFalse("an input-transparent Border is not a stop");
        border.InputTransparent = false;
        platform.IsTapStop.Should().BeTrue();

        border.GestureRecognizers.Clear();
        platform.IsTapStop.Should().BeFalse();
        host.Context.TabStops().Should().NotContain(platform);
        border.GestureRecognizers.Add(new TapGestureRecognizer());
        platform.IsTapStop.Should().BeTrue("the stop follows the recognizers");
    }

    [Fact]
    public void Enter_on_a_disabled_border_or_a_focused_child_does_not_tap_the_border()
    {
        int taps = 0;
        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, e) => taps++;
        int clicks = 0;
        var button = new Button { Text = "Inner" };
        button.Clicked += (s, e) => clicks++;
        var border = new Border { HeightRequest = 80, Content = button, GestureRecognizers = { tap } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { border } }, withEngine: true);
        host.Context.Render();

        host.Context.FocusedView = (SkiaView)button.Handler!.PlatformView!;
        Press(host, Key.Enter);
        clicks.Should().Be(1);
        taps.Should().Be(0, "the child's key does not reach the Border's recognizer");

        host.Context.FocusedView = Platform(border);
        border.IsEnabled = false;
        Press(host, Key.Enter);
        taps.Should().Be(0);
    }
}
