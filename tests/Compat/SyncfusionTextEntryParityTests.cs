// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Syncfusion.Maui.Inputs;
using Xunit;
using Colors = Microsoft.Maui.Graphics.Colors;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// SfNumericEntry and SfMaskedEntry key, text, clipboard, wheel and hover
/// handling, which the Windows build drives from its TextBox's events and the
/// platform-neutral build leaves unconnected (SfTextBoxBridge).
/// </summary>
[Collection(CompatHost.Collection)]
public sealed class SyncfusionTextEntryParityTests
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private static void Settle(CompatHost host)
    {
        for (int i = 0; i < 3; i++)
            host.Render();
    }

    /// <summary>Renders until <paramref name="condition"/> holds (work the control posts lands through the main loop).</summary>
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

    private static void Raise<T>(CompatHost host, string name, T args) where T : EventArgs
    {
        var field = host.DisplayWindow.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((EventHandler<T>?)field.GetValue(host.DisplayWindow))?.Invoke(host.DisplayWindow, args);
    }

    private static void Key(CompatHost host, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        Raise(host, "KeyDown", new KeyEventArgs(key, modifiers));
        Raise(host, "KeyUp", new KeyEventArgs(key, modifiers));
        Settle(host);
    }

    private static void Type(CompatHost host, string text)
    {
        foreach (char ch in text)
        {
            host.DisplayWindow.RaiseTextInput(ch.ToString());
            Settle(host);
        }
    }

    private static void Click(CompatHost host, float x, float y)
    {
        host.DisplayWindow.RaisePointerMoved(x, y);
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        Settle(host);
    }

    private static Entry TextBoxOf(View control) => (Entry)control.GetType().GetField("textBox", Any)!.GetValue(control)!;

    private static (CompatHost Host, Entry TextBox) Show(View control)
    {
        control.WidthRequest = 220;
        control.HeightRequest = 40;
        control.HorizontalOptions = LayoutOptions.Start;
        control.VerticalOptions = LayoutOptions.Start;
        var page = new ContentPage
        {
            BackgroundColor = Colors.White,
            Content = new VerticalStackLayout { Padding = 20, Children = { control, new Button { Text = "After" } } },
        };
        var host = new CompatHost(page, b => b.UseLinuxSyncfusion(), 400, 300);
        Settle(host);
        var textBox = TextBoxOf(control);
        var (x, y) = CompatHost.CenterOf(textBox);
        Click(host, x, y);
        host.Context.FocusedView.Should().BeSameAs(CompatHost.PlatformOf(textBox), "clicking the text box focuses it");
        return (host, textBox);
    }

    private static void WithClipboard(string? text, Action body)
    {
        var get = SfTextBoxBridgeMember<Func<string?>>("GetClipboardText");
        var set = SfTextBoxBridgeMember<Action<string?>>("SetClipboardText");
        string? clipboard = text;
        SetSfTextBoxBridgeMember("GetClipboardText", new Func<string?>(() => clipboard));
        SetSfTextBoxBridgeMember("SetClipboardText", new Action<string?>(t => clipboard = t));
        try
        {
            body();
        }
        finally
        {
            SetSfTextBoxBridgeMember("GetClipboardText", get);
            SetSfTextBoxBridgeMember("SetClipboardText", set);
        }
    }

    private static System.Type Bridge =>
        typeof(LinuxSyncfusionBuilderExtensions).Assembly.GetType("Microsoft.Maui.Platform.Linux.Syncfusion.SfTextBoxBridge")!;

    private static T SfTextBoxBridgeMember<T>(string name) => (T)Bridge.GetProperty(name, Any)!.GetValue(null)!;

    private static void SetSfTextBoxBridgeMember(string name, object value) => Bridge.GetProperty(name, Any)!.SetValue(null, value);

    // ---- SfNumericEntry -------------------------------------------------------

    [Fact]
    public void Numeric_entry_steps_with_the_arrow_and_page_keys_and_the_wheel()
    {
        var numeric = new SfNumericEntry { Value = 5, SmallChange = 1, LargeChange = 10, Minimum = 0, Maximum = 100 };
        var (host, textBox) = Show(numeric);
        using var _ = host;

        Key(host, Microsoft.Maui.Platform.Key.Up);
        numeric.Value.Should().Be(6);
        Key(host, Microsoft.Maui.Platform.Key.PageUp);
        numeric.Value.Should().Be(16);
        Key(host, Microsoft.Maui.Platform.Key.Down);
        numeric.Value.Should().Be(15);
        Key(host, Microsoft.Maui.Platform.Key.PageDown);
        numeric.Value.Should().Be(5);

        var (x, y) = CompatHost.CenterOf(textBox);
        Raise(host, "Scroll", new ScrollEventArgs(x, y, 0, -1));
        Settle(host);
        numeric.Value.Should().Be(6, "the wheel away from the user steps up by SmallChange");
        Raise(host, "Scroll", new ScrollEventArgs(x, y, 0, 1));
        Settle(host);
        numeric.Value.Should().Be(5);
    }

    [Fact]
    public void Numeric_entry_takes_only_number_characters_and_commits_on_enter()
    {
        var numeric = new SfNumericEntry { Value = null, AllowNull = true, Culture = new System.Globalization.CultureInfo("en-US"), CustomFormat = "0.##" };
        var (host, textBox) = Show(numeric);
        using var _ = host;

        Type(host, "1a2 b.5x");
        textBox.Text.Should().Be("12.5", "letters and spaces are refused, digits and the separator are inserted");
        Key(host, Microsoft.Maui.Platform.Key.Enter);
        numeric.Value.Should().Be(12.5);

        Key(host, Microsoft.Maui.Platform.Key.End);
        Key(host, Microsoft.Maui.Platform.Key.Backspace);
        RenderUntil(host, () => textBox.Text == "12.");
        textBox.Text.Should().Be("12.", "the control applies a backspace on the main loop, as on Windows");
        Type(host, "-");
        textBox.Text.Should().StartWith("-", "the minus key negates the number");
    }

    [Fact]
    public void Numeric_entry_pastes_and_cuts_through_its_number_rules()
    {
        var numeric = new SfNumericEntry { Value = null, AllowNull = true, Culture = new System.Globalization.CultureInfo("en-US") };
        var (host, textBox) = Show(numeric);
        using var _ = host;

        WithClipboard("4x2", () =>
        {
            Key(host, Microsoft.Maui.Platform.Key.V, KeyModifiers.Control);
            textBox.Text.Should().Be("42", "the pasted text keeps only its number");

            var skia = (SkiaEntry)CompatHost.PlatformOf(textBox);
            skia.SelectAll();
            Key(host, Microsoft.Maui.Platform.Key.X, KeyModifiers.Control);
            SfTextBoxBridgeMember<Func<string?>>("GetClipboardText")().Should().Be("42");
        });
    }

    // ---- SfMaskedEntry --------------------------------------------------------

    [Fact]
    public void Masked_entry_types_through_its_mask_and_refuses_other_characters()
    {
        var masked = new SfMaskedEntry { MaskType = MaskedEntryMaskType.Simple, Mask = "(000) 000-0000", PromptChar = '_' };
        var (host, textBox) = Show(masked);
        using var _ = host;

        Type(host, "55a5");
        textBox.Text.Should().Be("(555) ___-____", "a letter has no place in a digit mask");
        Type(host, "1234567");
        textBox.Text.Should().Be("(555) 123-4567");

        Key(host, Microsoft.Maui.Platform.Key.Backspace);
        textBox.Text.Should().Be("(555) 123-456_", "backspace clears the mask position");
    }

    [Fact]
    public void Masked_entry_pastes_through_its_mask()
    {
        var masked = new SfMaskedEntry { MaskType = MaskedEntryMaskType.Simple, Mask = "00-00", PromptChar = '_' };
        var (host, textBox) = Show(masked);
        using var _ = host;

        WithClipboard("1x234", () => Key(host, Microsoft.Maui.Platform.Key.V, KeyModifiers.Control));
        textBox.Text.Should().Be("12-34");
    }

    // ---- Hover ----------------------------------------------------------------

    [Fact]
    public void Hovering_the_text_box_puts_the_entry_in_its_pointer_over_state()
    {
        var numeric = new SfNumericEntry { Value = 1 };
        var states = new VisualStateGroup { Name = "CommonStates" };
        states.States.Add(new VisualState { Name = "Normal" });
        var over = new VisualState { Name = "PointerOver" };
        over.Setters.Add(new Setter { Property = VisualElement.BackgroundColorProperty, Value = Colors.Red });
        states.States.Add(over);
        VisualStateManager.SetVisualStateGroups(numeric, new VisualStateGroupList { states });

        numeric.WidthRequest = 220;
        numeric.HeightRequest = 40;
        numeric.HorizontalOptions = LayoutOptions.Start;
        numeric.VerticalOptions = LayoutOptions.Start;
        var page = new ContentPage { Content = new VerticalStackLayout { Padding = 20, Children = { numeric } } };
        using var host = new CompatHost(page, b => b.UseLinuxSyncfusion(), 400, 300);
        Settle(host);

        var (x, y) = CompatHost.CenterOf(TextBoxOf(numeric));
        host.DisplayWindow.RaisePointerMoved(x, y);
        Settle(host);
        numeric.BackgroundColor.Should().Be(Colors.Red);
        host.DisplayWindow.RaisePointerMoved(390, 290);
        Settle(host);
        numeric.BackgroundColor.Should().NotBe(Colors.Red);
    }
}
