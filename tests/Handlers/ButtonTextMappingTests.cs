// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// The plain ButtonHandler maps text, colour and font for any ITextButton, as MAUI's does:
/// only TextButtonHandler (registered for Controls.Button) used to, so a button resolved to
/// ButtonHandler showed an empty 14pt caption.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ButtonTextMappingTests
{
    private static SkiaButton ConnectPlainHandler(Button button)
    {
        var handler = new ButtonHandler();
        handler.SetMauiContext(HeadlessMauiContext.Instance);
        button.Handler = handler;
        return handler.PlatformView;
    }

    [Fact]
    public void ButtonHandler_maps_text_colour_font_and_spacing_of_an_ITextButton()
    {
        var button = new Button
        {
            Text = "Save",
            TextColor = Colors.Orange,
            FontSize = 21,
            FontFamily = "Serif",
            FontAttributes = FontAttributes.Bold | FontAttributes.Italic,
            CharacterSpacing = 3,
        };

        var view = ConnectPlainHandler(button);

        view.Text.Should().Be("Save");
        view.TextColor.Should().Be(Colors.Orange);
        view.FontSize.Should().Be(21);
        view.FontFamily.Should().Be("Serif");
        view.FontAttributes.Should().Be(FontAttributes.Bold | FontAttributes.Italic);
        view.CharacterSpacing.Should().Be(3);
    }

    [Fact]
    public void ButtonHandler_follows_text_and_font_changes()
    {
        var button = new Button { Text = "Before", FontSize = 12 };
        var view = ConnectPlainHandler(button);

        button.Text = "After";
        button.FontSize = 30;
        button.FontAttributes = FontAttributes.Italic;

        view.Text.Should().Be("After");
        view.FontSize.Should().Be(30);
        view.FontAttributes.Should().Be(FontAttributes.Italic);
    }

    [Fact]
    public void A_Controls_Button_carries_its_TextTransform_to_the_view()
    {
        var button = new Button { Text = "Mixed Case", TextTransform = TextTransform.Uppercase };
        var view = HeadlessMauiContext.Realize<SkiaButton>(button);

        view.Text.Should().Be("Mixed Case", "the view applies the transform when it draws");
        view.TextTransform.Should().Be(TextTransform.Uppercase);

        button.TextTransform = TextTransform.Lowercase;
        view.TextTransform.Should().Be(TextTransform.Lowercase);
    }
}
