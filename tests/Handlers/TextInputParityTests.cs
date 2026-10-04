// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// Entry, Editor and SearchBar follow MAUI's text-input contract through the
/// Linux handlers: MaxLength truncates text set from code (0 means no text, -1
/// unlimited), an app's null Text is not replaced by "", TextTransform shapes
/// the shown text, the Keyboard reaches the platform view (and the input
/// method as a content type), and platform focus is reported to IsFocused.
/// </summary>
public class TextInputParityTests
{
    private const string Lorem = "Lorem ipsum dolor sit amet";

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Entry_MaxLength_truncates_text_set_from_code(int maxLength)
    {
        var entry = new Entry { MaxLength = maxLength, Text = Lorem };
        var platform = HeadlessMauiContext.Realize<SkiaEntry>(entry);

        platform.Text.Should().Be(Lorem.Substring(0, maxLength));
        entry.Text.Should().Be(Lorem.Substring(0, maxLength));

        entry.Text = "Another text";
        platform.Text.Should().Be("Another text".Substring(0, maxLength));
        entry.Text.Should().Be("Another text".Substring(0, maxLength));
    }

    [Fact]
    public void Entry_MaxLength_lowered_later_truncates_and_negative_is_unlimited()
    {
        var entry = new Entry { MaxLength = -1, Text = Lorem };
        var platform = HeadlessMauiContext.Realize<SkiaEntry>(entry);
        platform.Text.Should().Be(Lorem);

        entry.MaxLength = 3;

        platform.Text.Should().Be("Lor");
        entry.Text.Should().Be("Lor");
    }

    [Fact]
    public void Editor_MaxLength_truncates_text_set_from_code()
    {
        var editor = new Editor { MaxLength = 5, Text = Lorem };
        var platform = HeadlessMauiContext.Realize<SkiaEditor>(editor);

        platform.Text.Should().Be("Lorem");
        editor.Text.Should().Be("Lorem");
    }

    [Fact]
    public void SearchBar_MaxLength_truncates_the_query()
    {
        var searchBar = new SearchBar { MaxLength = 2, Text = Lorem };
        var platform = HeadlessMauiContext.Realize<SkiaSearchBar>(searchBar);

        platform.Text.Should().Be("Lo");
        searchBar.Text.Should().Be("Lo");
    }

    [Fact]
    public void Null_text_is_not_replaced_by_empty_and_raises_no_TextChanged()
    {
        var entry = new Entry { Text = "Hello" };
        var editor = new Editor { Text = "Hello" };
        var searchBar = new SearchBar { Text = "Hello" };
        var entryPlatform = HeadlessMauiContext.Realize<SkiaEntry>(entry);
        var editorPlatform = HeadlessMauiContext.Realize<SkiaEditor>(editor);
        var searchPlatform = HeadlessMauiContext.Realize<SkiaSearchBar>(searchBar);

        entry.Text = null;
        editor.Text = null;
        searchBar.Text = null;

        entryPlatform.Text.Should().BeEmpty();
        editorPlatform.Text.Should().BeEmpty();
        searchPlatform.Text.Should().BeEmpty();
        entry.Text.Should().BeNull();
        editor.Text.Should().BeNull();
        searchBar.Text.Should().BeNull();

        int changes = 0;
        entry.TextChanged += (_, _) => changes++;
        editor.TextChanged += (_, _) => changes++;
        entryPlatform.Text = null!;
        editorPlatform.Text = null!;
        changes.Should().Be(0, "null and empty text are the same text to an app");
        editorPlatform.Text.Should().BeEmpty("the platform reports empty text, never null");
    }

    [Fact]
    public void TextTransform_shapes_the_shown_text()
    {
        var entry = new Entry { Text = "Hello", TextTransform = TextTransform.Uppercase };
        var platform = HeadlessMauiContext.Realize<SkiaEntry>(entry);
        platform.Text.Should().Be("HELLO");

        entry.TextTransform = TextTransform.Lowercase;
        platform.Text.Should().Be("hello");

        var searchBar = new SearchBar { Text = "Query", TextTransform = TextTransform.Uppercase };
        HeadlessMauiContext.Realize<SkiaSearchBar>(searchBar).Text.Should().Be("QUERY");

        var editor = new Editor { Text = "Notes", TextTransform = TextTransform.Uppercase };
        HeadlessMauiContext.Realize<SkiaEditor>(editor).Text.Should().Be("NOTES");
    }

    [Fact]
    public void Keyboard_reaches_the_platform_view_and_the_input_method_content_type()
    {
        var entry = new Entry { Keyboard = Keyboard.Numeric };
        var editor = new Editor { Keyboard = Keyboard.Email };
        var searchBar = new SearchBar { Keyboard = Keyboard.Url };
        var entryPlatform = HeadlessMauiContext.Realize<SkiaEntry>(entry);
        var editorPlatform = HeadlessMauiContext.Realize<SkiaEditor>(editor);
        var searchPlatform = HeadlessMauiContext.Realize<SkiaSearchBar>(searchBar);

        entryPlatform.Keyboard.Should().BeSameAs(Keyboard.Numeric);
        editorPlatform.Keyboard.Should().BeSameAs(Keyboard.Email);
        searchPlatform.Keyboard.Should().BeSameAs(Keyboard.Url);

        ((IInputContext)entryPlatform).ContentType.Purpose.Should().Be(TextInputPurpose.Number);
        var editorType = ((IInputContext)editorPlatform).ContentType;
        editorType.Purpose.Should().Be(TextInputPurpose.Email);
        editorType.Hints.Should().HaveFlag(TextInputHints.Multiline);

        entry.Keyboard = Keyboard.Telephone;
        entryPlatform.Keyboard.Should().BeSameAs(Keyboard.Telephone);
        ((IInputContext)entryPlatform).ContentType.Purpose.Should().Be(TextInputPurpose.Phone);
    }

    [Theory]
    [InlineData(false, false, false, TextInputPurpose.Normal, TextInputHints.Completion | TextInputHints.Spellcheck)]
    [InlineData(true, false, false, TextInputPurpose.Password, TextInputHints.HiddenText | TextInputHints.SensitiveData)]
    [InlineData(false, true, true, TextInputPurpose.Normal, TextInputHints.None)]
    public void Default_keyboard_content_type_follows_password_and_prediction(
        bool isPassword, bool noPrediction, bool noSpellCheck, TextInputPurpose purpose, TextInputHints hints)
    {
        var type = TextInputContentType.FromKeyboard(Keyboard.Default, isPassword, !noPrediction, !noSpellCheck);
        type.Should().Be(new TextInputContentType(purpose, hints));
    }

    [Fact]
    public void Specialised_keyboards_turn_off_prediction_and_plain_has_no_hints()
    {
        TextInputContentType.FromKeyboard(Keyboard.Email).Should().Be(new TextInputContentType(TextInputPurpose.Email, TextInputHints.None));
        TextInputContentType.FromKeyboard(Keyboard.Plain).Should().Be(TextInputContentType.Default);
        TextInputContentType.FromKeyboard(Keyboard.Text).Hints.Should().HaveFlag(TextInputHints.AutoCapitalization);
        TextInputContentType.FromKeyboard(Keyboard.Create(KeyboardFlags.CapitalizeWord))
            .Should().Be(new TextInputContentType(TextInputPurpose.Normal, TextInputHints.Titlecase));
    }

    [Fact]
    public void IBus_gets_its_own_purpose_and_hint_values()
    {
        var (purpose, hints) = IBusInputMethodService.ToIBusContentType(
            new TextInputContentType(TextInputPurpose.Phone, TextInputHints.Completion | TextInputHints.Spellcheck));
        purpose.Should().Be(4u);
        hints.Should().Be((1u << 0) | (1u << 2));

        var (pwPurpose, pwHints) = IBusInputMethodService.ToIBusContentType(
            TextInputContentType.FromKeyboard(Keyboard.Default, isPassword: true));
        pwPurpose.Should().Be(8u);
        pwHints.Should().Be((1u << 1) | (1u << 11));
    }

    [Fact]
    public void SearchBar_maps_the_text_input_properties_to_its_field()
    {
        var searchBar = new SearchBar
        {
            Text = "Query",
            IsReadOnly = true,
            IsTextPredictionEnabled = false,
            IsSpellCheckEnabled = false,
            VerticalTextAlignment = TextAlignment.End,
            FontAttributes = FontAttributes.Italic,
            SearchIconColor = Colors.Red,
            CursorPosition = 2,
            SelectionLength = 3,
        };
        var platform = HeadlessMauiContext.Realize<SkiaSearchBar>(searchBar);

        platform.IsReadOnly.Should().BeTrue();
        platform.IsTextPredictionEnabled.Should().BeFalse();
        platform.IsSpellCheckEnabled.Should().BeFalse();
        platform.VerticalTextAlignment.Should().Be(TextAlignment.End);
        platform.FontAttributes.Should().HaveFlag(FontAttributes.Italic);
        platform.IconColor.Should().Be(Colors.Red);
        platform.CursorPosition.Should().Be(2);
        platform.SelectionLength.Should().Be(3);
    }

    [Fact]
    public void SearchBar_reports_the_caret_after_typing()
    {
        var searchBar = new SearchBar();
        var platform = HeadlessMauiContext.Realize<SkiaSearchBar>(searchBar);

        platform.OnTextInput(new TextInputEventArgs("a"));
        platform.OnTextInput(new TextInputEventArgs("b"));

        searchBar.Text.Should().Be("ab");
        searchBar.CursorPosition.Should().Be(2);

        platform.Text = "Hello";
        searchBar.CursorPosition.Should().Be(5, "a caret at the end stays at the end when the query is replaced");
    }

    [Fact]
    public void Read_only_SearchBar_keeps_its_query_on_escape()
    {
        var searchBar = new SearchBar { Text = "Query", IsReadOnly = true };
        var platform = HeadlessMauiContext.Realize<SkiaSearchBar>(searchBar);

        platform.OnKeyDown(new KeyEventArgs(Key.Escape));

        searchBar.Text.Should().Be("Query");
    }

    [Fact]
    public void Platform_focus_is_reported_to_IsFocused()
    {
        var searchBar = new SearchBar();
        var platform = HeadlessMauiContext.Realize<SkiaSearchBar>(searchBar);

        platform.OnFocusGained();
        searchBar.IsFocused.Should().BeTrue();

        platform.OnFocusLost();
        searchBar.IsFocused.Should().BeFalse();
    }
}
