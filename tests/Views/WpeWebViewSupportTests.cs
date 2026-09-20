// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Input;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// Headless tests for the pieces of the WPE WebView that do not need WPE
/// installed: keysym translation for WebKit input, and backend selection.
/// </summary>
public class WpeWebViewSupportTests
{
    [Theory]
    [InlineData(Key.A, false, 'a')]
    [InlineData(Key.A, true, 'A')]
    [InlineData(Key.Z, false, 'z')]
    [InlineData(Key.D0, false, '0')]
    [InlineData(Key.D9, true, '9')]
    [InlineData(Key.Space, false, ' ')]
    public void Printable_keys_map_to_ascii_keysyms(Key key, bool shifted, char expected)
    {
        KeyMapping.ToKeysym(key, shifted).Should().Be((uint)expected);
    }

    [Theory]
    [InlineData(Key.Backspace, 0xff08u)]
    [InlineData(Key.Tab, 0xff09u)]
    [InlineData(Key.Enter, 0xff0du)]
    [InlineData(Key.Escape, 0xff1bu)]
    [InlineData(Key.Delete, 0xffffu)]
    [InlineData(Key.Left, 0xff51u)]
    [InlineData(Key.Down, 0xff54u)]
    [InlineData(Key.Home, 0xff50u)]
    [InlineData(Key.PageDown, 0xff56u)]
    [InlineData(Key.Shift, 0xffe1u)]
    [InlineData(Key.Control, 0xffe3u)]
    [InlineData(Key.F1, 0xffbeu)]
    public void Control_keys_map_to_x11_keysyms(Key key, uint expected)
    {
        KeyMapping.ToKeysym(key, shifted: false).Should().Be(expected);
    }

    [Fact]
    public void NumPad_digits_map_to_keypad_keysyms()
    {
        KeyMapping.ToKeysym(Key.NumPad0, false).Should().Be(0xffb0u);
        KeyMapping.ToKeysym(Key.NumPad9, false).Should().Be(0xffb9u);
    }

    [Fact]
    public void Unknown_key_maps_to_zero()
    {
        KeyMapping.ToKeysym(Key.Unknown, false).Should().Be(0u);
    }

    [Fact]
    public void Keysym_round_trips_through_FromKeysym()
    {
        foreach (var key in new[] { Key.Backspace, Key.Enter, Key.Left, Key.Home, Key.A, Key.D5, Key.NumPad3, Key.F1 })
        {
            var sym = KeyMapping.ToKeysym(key, false);
            KeyMapping.FromKeysym(sym).Should().Be(key, $"{key} -> 0x{sym:x} -> {key}");
        }
    }

    [Theory]
    [InlineData(Key.A, true)]
    [InlineData(Key.D7, true)]
    [InlineData(Key.Space, true)]
    [InlineData(Key.NumPad2, true)]
    [InlineData(Key.Enter, false)]
    [InlineData(Key.Backspace, false)]
    [InlineData(Key.Left, false)]
    [InlineData(Key.Control, false)]
    [InlineData(Key.F5, false)]
    public void Printable_classification_separates_text_from_control_keys(Key key, bool printable)
    {
        KeyMapping.IsPrintable(key).Should().Be(printable);
    }

    [Theory]
    [InlineData("webkitgtk", WebViewBackend.Kind.WebKitGtk)]
    [InlineData("gtk", WebViewBackend.Kind.WebKitGtk)]
    [InlineData("GTK", WebViewBackend.Kind.WebKitGtk)]
    public void Environment_variable_forces_webkitgtk(string value, WebViewBackend.Kind expected)
    {
        var previous = Environment.GetEnvironmentVariable(WebViewBackend.EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(WebViewBackend.EnvironmentVariable, value);
            WebViewBackend.Resolve().Should().Be(expected);
        }
        finally
        {
            Environment.SetEnvironmentVariable(WebViewBackend.EnvironmentVariable, previous);
        }
    }

    [Fact]
    public void Auto_and_unknown_values_resolve_to_availability()
    {
        var previous = Environment.GetEnvironmentVariable(WebViewBackend.EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(WebViewBackend.EnvironmentVariable, "auto");
            var auto = WebViewBackend.Resolve();
            Environment.SetEnvironmentVariable(WebViewBackend.EnvironmentVariable, "bogus");
            WebViewBackend.Resolve().Should().Be(auto, "an unknown value is ignored");
            Environment.SetEnvironmentVariable(WebViewBackend.EnvironmentVariable, null);
            WebViewBackend.Resolve().Should().Be(auto);
            // Whatever auto picked must agree with the availability probe.
            auto.Should().Be(Microsoft.Maui.Platform.Linux.Views.WpeWebView.IsSupported ? WebViewBackend.Kind.Wpe : WebViewBackend.Kind.WebKitGtk);
        }
        finally
        {
            Environment.SetEnvironmentVariable(WebViewBackend.EnvironmentVariable, previous);
        }
    }
}
