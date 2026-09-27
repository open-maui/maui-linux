// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Input;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Views;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// The pure parts of the WebView's hardware-keycode plumbing (backend keycode
/// to XKB keycode, pairing a key press with its text) and of the zero-copy
/// frame path's GPU matching. End-to-end behaviour runs in WebViewHandlerTests.
/// </summary>
public class WpeKeycodeTests
{
    [Theory]
    [InlineData(30u, 38u)]   // KEY_A
    [InlineData(105u, 113u)] // KEY_LEFT
    [InlineData(1u, 9u)]     // KEY_ESC
    [InlineData(0u, 0u)]     // KEY_RESERVED: unknown
    public void Wayland_evdev_codes_become_xkb_keycodes(uint evdev, uint xkb)
    {
        KeyMapping.EvdevToXkbKeycode(evdev).Should().Be(xkb);
    }

    [Theory]
    [InlineData(38u, 38u)]
    [InlineData(8u, 8u)]
    [InlineData(7u, 0u)]
    [InlineData(0u, 0u)]
    public void X11_keycodes_already_are_xkb_keycodes(uint x11, uint xkb)
    {
        KeyMapping.X11ToXkbKeycode(x11).Should().Be(xkb);
    }

    [Fact]
    public void Key_event_args_keep_their_constructors_and_default_to_no_keycode()
    {
        new KeyEventArgs(Key.A).HardwareKeycode.Should().Be(0u);
        new KeyEventArgs(Key.A, KeyModifiers.Shift) { HardwareKeycode = 38 }.HardwareKeycode.Should().Be(38u);
    }

    [Fact]
    public void Text_from_a_printable_key_carries_its_keycode()
    {
        var tracker = new WpeKeycodeTracker();
        tracker.PrintableKeyDown(38);
        tracker.TakeForText("a").Should().Be(38u);
    }

    [Fact]
    public void Keycode_is_consumed_by_the_first_text()
    {
        var tracker = new WpeKeycodeTracker();
        tracker.PrintableKeyDown(38);
        tracker.TakeForText("a");
        tracker.TakeForText("a").Should().Be(0u, "a later IME commit must not reuse the key");
    }

    [Fact]
    public void Multi_character_text_has_no_keycode()
    {
        var tracker = new WpeKeycodeTracker();
        tracker.PrintableKeyDown(38);
        tracker.TakeForText("ab").Should().Be(0u);
    }

    [Fact]
    public void A_single_non_bmp_character_keeps_the_keycode()
    {
        var tracker = new WpeKeycodeTracker();
        tracker.PrintableKeyDown(38);
        tracker.TakeForText("\U0001F600").Should().Be(38u);
    }

    [Fact]
    public void Releasing_the_key_before_text_arrives_drops_the_keycode()
    {
        var tracker = new WpeKeycodeTracker();
        tracker.PrintableKeyDown(38);
        tracker.KeyUp(38);
        tracker.TakeForText("a").Should().Be(0u);
    }

    [Fact]
    public void Releasing_another_key_keeps_the_pending_keycode()
    {
        var tracker = new WpeKeycodeTracker();
        tracker.PrintableKeyDown(38);
        tracker.KeyUp(50); // e.g. Shift released between press and text
        tracker.TakeForText("A").Should().Be(38u);
    }

    [Fact]
    public void Text_without_a_key_press_has_no_keycode()
    {
        new WpeKeycodeTracker().TakeForText("x").Should().Be(0u);
    }

    [Fact]
    public void Same_drm_node_is_the_same_gpu()
    {
        DmaBufFrameImporter.SameGpu("/dev/dri/renderD128", "/dev/dri/renderD128").Should().BeTrue();
    }

    [Fact]
    public void Unknown_drm_nodes_are_not_the_same_gpu()
    {
        DmaBufFrameImporter.SameGpu("/dev/dri/renderD900", "/dev/dri/renderD901").Should().BeFalse();
    }
}
