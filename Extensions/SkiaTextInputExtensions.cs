// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Handlers;

namespace Microsoft.Maui.Platform;

/// <summary>
/// MAUI's platform extensions for an entry (<c>UpdateText(platformEntry, entry)</c>,
/// <c>UpdateIsPassword</c>, <c>UpdateKeyboard</c>, ...) on OpenMaui's <see cref="SkiaEntry"/>.
/// Each does what OpenMaui's <c>EntryHandler</c> mapping of that property does; the handler
/// calls them.
/// </summary>
public static class SkiaEntryExtensions
{
    /// <summary>
    /// Shows the text as the entry displays it (TextTransform applied, cut to MaxLength); the
    /// text the entry then holds becomes the view's Text, as on MAUI's platforms.
    /// </summary>
    public static void UpdateText(this SkiaEntry platformEntry, ITextInput textInput)
    {
        platformEntry.SetDisplayText(textInput);
        TextInputText.UpdateVirtualText(textInput, platformEntry.Text);
    }

    /// <summary>Puts the display text in the entry; true when it replaced different text.</summary>
    internal static bool SetDisplayText(this SkiaEntry platformEntry, ITextInput textInput)
    {
        var text = TextInputText.GetDisplayText(textInput);
        if (platformEntry.Text == text)
            return false;
        platformEntry.Text = text;
        platformEntry.Invalidate();
        return true;
    }

    /// <summary>Applies the text color (a null color keeps the current one).</summary>
    public static void UpdateTextColor(this SkiaEntry platformEntry, ITextStyle textStyle)
    {
        if (textStyle.TextColor is not null)
            platformEntry.TextColor = textStyle.TextColor;
    }

    /// <summary>Applies the font: size, family and FontAttributes.</summary>
    public static void UpdateFont(this SkiaEntry platformEntry, ITextStyle textStyle)
    {
        var font = textStyle.Font;
        if (font.Size > 0)
            platformEntry.FontSize = font.Size;
        if (!string.IsNullOrEmpty(font.Family))
            platformEntry.FontFamily = font.Family;
        platformEntry.FontAttributes = TextStyleMapping.ToFontAttributes(font);
    }

    /// <summary>MAUI's signature with a font manager; the Skia view resolves the family itself.</summary>
    public static void UpdateFont(this SkiaEntry platformEntry, ITextStyle textStyle, IFontManager fontManager) =>
        platformEntry.UpdateFont(textStyle);

    /// <summary>Applies the character spacing.</summary>
    public static void UpdateCharacterSpacing(this SkiaEntry platformEntry, ITextStyle textStyle)
    {
        platformEntry.CharacterSpacing = textStyle.CharacterSpacing;
    }

    /// <summary>Applies the placeholder text (none shows nothing).</summary>
    public static void UpdatePlaceholder(this SkiaEntry platformEntry, IPlaceholder placeholder)
    {
        platformEntry.Placeholder = placeholder.Placeholder ?? string.Empty;
    }

    /// <summary>Applies the placeholder color (a null color keeps the current one).</summary>
    public static void UpdatePlaceholderColor(this SkiaEntry platformEntry, IPlaceholder placeholder)
    {
        if (placeholder.PlaceholderColor is not null)
            platformEntry.PlaceholderColor = placeholder.PlaceholderColor;
    }

    /// <summary>Applies IsReadOnly.</summary>
    public static void UpdateIsReadOnly(this SkiaEntry platformEntry, ITextInput textInput)
    {
        platformEntry.IsReadOnly = textInput.IsReadOnly;
    }

    /// <summary>Applies MaxLength.</summary>
    public static void UpdateMaxLength(this SkiaEntry platformEntry, ITextInput textInput)
    {
        platformEntry.MaxLength = textInput.MaxLength;
    }

    /// <summary>Applies the keyboard (on the desktop, the input method's content type).</summary>
    public static void UpdateKeyboard(this SkiaEntry platformEntry, ITextInput textInput)
    {
        platformEntry.Keyboard = textInput.Keyboard;
    }

    /// <summary>Applies IsPassword (the text is masked).</summary>
    public static void UpdateIsPassword(this SkiaEntry platformEntry, IEntry entry)
    {
        platformEntry.IsPassword = entry.IsPassword;
    }

    /// <summary>Applies the return key type.</summary>
    public static void UpdateReturnType(this SkiaEntry platformEntry, IEntry entry)
    {
        platformEntry.ReturnType = entry.ReturnType;
    }

    /// <summary>Shows the clear button while editing when ClearButtonVisibility is WhileEditing.</summary>
    public static void UpdateClearButtonVisibility(this SkiaEntry platformEntry, IEntry entry)
    {
        platformEntry.ShowClearButton = entry.ClearButtonVisibility == ClearButtonVisibility.WhileEditing;
    }

    /// <summary>Applies IsTextPredictionEnabled.</summary>
    public static void UpdateIsTextPredictionEnabled(this SkiaEntry platformEntry, ITextInput textInput)
    {
        platformEntry.IsTextPredictionEnabled = textInput.IsTextPredictionEnabled;
    }

    /// <summary>Applies IsSpellCheckEnabled.</summary>
    public static void UpdateIsSpellCheckEnabled(this SkiaEntry platformEntry, ITextInput textInput)
    {
        platformEntry.IsSpellCheckEnabled = textInput.IsSpellCheckEnabled;
    }

    /// <summary>Moves the caret; the view gets the position capped to the text.</summary>
    public static void UpdateCursorPosition(this SkiaEntry platformEntry, ITextInput textInput)
    {
        platformEntry.CursorPosition = textInput.CursorPosition;
        if (textInput.CursorPosition != platformEntry.CursorPosition)
            textInput.CursorPosition = platformEntry.CursorPosition;
    }

    /// <summary>Applies the selection length (capped to the text after the caret) and reports it to the view.</summary>
    public static void UpdateSelectionLength(this SkiaEntry platformEntry, ITextInput textInput)
    {
        platformEntry.SelectionLength = TextInputText.ClampSelectionLength(
            platformEntry.Text, platformEntry.CursorPosition, textInput.SelectionLength);
        TextInputText.ReportSelection(textInput, platformEntry.CursorPosition, platformEntry.SelectionLength);
    }

    /// <summary>Applies the horizontal text alignment.</summary>
    public static void UpdateHorizontalTextAlignment(this SkiaEntry platformEntry, ITextAlignment textAlignment)
    {
        platformEntry.HorizontalTextAlignment = textAlignment.HorizontalTextAlignment switch
        {
            TextAlignment.Center => TextAlignment.Center,
            TextAlignment.End => TextAlignment.End,
            _ => TextAlignment.Start,
        };
    }

    /// <summary>Applies the vertical text alignment.</summary>
    public static void UpdateVerticalTextAlignment(this SkiaEntry platformEntry, ITextAlignment textAlignment)
    {
        platformEntry.VerticalTextAlignment = textAlignment.VerticalTextAlignment switch
        {
            TextAlignment.Start => TextAlignment.Start,
            TextAlignment.End => TextAlignment.End,
            _ => TextAlignment.Center,
        };
    }
}

/// <summary>
/// MAUI's platform extensions for an editor on OpenMaui's <see cref="SkiaEditor"/>. Each does
/// what OpenMaui's <c>EditorHandler</c> mapping of that property does; the handler calls them.
/// </summary>
public static class SkiaEditorExtensions
{
    /// <summary>
    /// Shows the text as the editor displays it (TextTransform applied, cut to MaxLength); the
    /// text the editor then holds becomes the view's Text, as on MAUI's platforms.
    /// </summary>
    public static void UpdateText(this SkiaEditor platformEditor, ITextInput textInput)
    {
        platformEditor.SetDisplayText(textInput);
        TextInputText.UpdateVirtualText(textInput, platformEditor.Text);
    }

    /// <summary>Puts the display text in the editor; true when it replaced different text.</summary>
    internal static bool SetDisplayText(this SkiaEditor platformEditor, ITextInput textInput)
    {
        var text = TextInputText.GetDisplayText(textInput);
        if (platformEditor.Text == text)
            return false;
        platformEditor.Text = text;
        platformEditor.Invalidate();
        return true;
    }

    /// <summary>Applies the text color (a null color keeps the current one).</summary>
    public static void UpdateTextColor(this SkiaEditor platformEditor, ITextStyle textStyle)
    {
        if (textStyle.TextColor is not null)
            platformEditor.TextColor = textStyle.TextColor;
    }

    /// <summary>Applies the font: size, family and FontAttributes.</summary>
    public static void UpdateFont(this SkiaEditor platformEditor, ITextStyle textStyle)
    {
        var font = textStyle.Font;
        if (font.Size > 0)
            platformEditor.FontSize = font.Size;
        if (!string.IsNullOrEmpty(font.Family))
            platformEditor.FontFamily = font.Family;
        platformEditor.FontAttributes = TextStyleMapping.ToFontAttributes(font);
    }

    /// <summary>MAUI's signature with a font manager; the Skia view resolves the family itself.</summary>
    public static void UpdateFont(this SkiaEditor platformEditor, ITextStyle textStyle, IFontManager fontManager) =>
        platformEditor.UpdateFont(textStyle);

    /// <summary>Applies the character spacing.</summary>
    public static void UpdateCharacterSpacing(this SkiaEditor platformEditor, ITextStyle textStyle)
    {
        platformEditor.CharacterSpacing = textStyle.CharacterSpacing;
    }

    /// <summary>Applies the placeholder text (no placeholder stays null, as on MAUI's Android and iOS editors).</summary>
    public static void UpdatePlaceholder(this SkiaEditor platformEditor, IPlaceholder placeholder)
    {
        platformEditor.Placeholder = placeholder.Placeholder;
    }

    /// <summary>Applies the placeholder color (a null color keeps the current one).</summary>
    public static void UpdatePlaceholderColor(this SkiaEditor platformEditor, IPlaceholder placeholder)
    {
        if (placeholder.PlaceholderColor is not null)
            platformEditor.PlaceholderColor = placeholder.PlaceholderColor;
    }

    /// <summary>Applies IsReadOnly.</summary>
    public static void UpdateIsReadOnly(this SkiaEditor platformEditor, ITextInput textInput)
    {
        platformEditor.IsReadOnly = textInput.IsReadOnly;
    }

    /// <summary>Applies MaxLength.</summary>
    public static void UpdateMaxLength(this SkiaEditor platformEditor, ITextInput textInput)
    {
        platformEditor.MaxLength = textInput.MaxLength;
    }

    /// <summary>Applies the keyboard (on the desktop, the input method's content type).</summary>
    public static void UpdateKeyboard(this SkiaEditor platformEditor, ITextInput textInput)
    {
        platformEditor.Keyboard = textInput.Keyboard;
    }

    /// <summary>Applies IsTextPredictionEnabled.</summary>
    public static void UpdateIsTextPredictionEnabled(this SkiaEditor platformEditor, ITextInput textInput)
    {
        platformEditor.IsTextPredictionEnabled = textInput.IsTextPredictionEnabled;
    }

    /// <summary>Applies IsSpellCheckEnabled.</summary>
    public static void UpdateIsSpellCheckEnabled(this SkiaEditor platformEditor, ITextInput textInput)
    {
        platformEditor.IsSpellCheckEnabled = textInput.IsSpellCheckEnabled;
    }

    /// <summary>Moves the caret; the view gets the position capped to the text.</summary>
    public static void UpdateCursorPosition(this SkiaEditor platformEditor, ITextInput textInput)
    {
        platformEditor.CursorPosition = textInput.CursorPosition;
        if (textInput.CursorPosition != platformEditor.CursorPosition)
            textInput.CursorPosition = platformEditor.CursorPosition;
    }

    /// <summary>Applies the selection length (capped to the text after the caret) and reports it to the view.</summary>
    public static void UpdateSelectionLength(this SkiaEditor platformEditor, ITextInput textInput)
    {
        platformEditor.SelectionLength = TextInputText.ClampSelectionLength(
            platformEditor.Text, platformEditor.CursorPosition, textInput.SelectionLength);
        TextInputText.ReportSelection(textInput, platformEditor.CursorPosition, platformEditor.SelectionLength);
    }

    /// <summary>Applies the horizontal text alignment.</summary>
    public static void UpdateHorizontalTextAlignment(this SkiaEditor platformEditor, ITextAlignment textAlignment)
    {
        platformEditor.HorizontalTextAlignment = textAlignment.HorizontalTextAlignment switch
        {
            TextAlignment.Center => TextAlignment.Center,
            TextAlignment.End => TextAlignment.End,
            _ => TextAlignment.Start,
        };
    }

    /// <summary>Applies the vertical text alignment (an editor's text starts at the top by default).</summary>
    public static void UpdateVerticalTextAlignment(this SkiaEditor platformEditor, ITextAlignment textAlignment)
    {
        platformEditor.VerticalTextAlignment = textAlignment.VerticalTextAlignment switch
        {
            TextAlignment.Center => TextAlignment.Center,
            TextAlignment.End => TextAlignment.End,
            _ => TextAlignment.Start,
        };
    }
}

/// <summary>
/// MAUI's platform extensions for a search bar on OpenMaui's <see cref="SkiaSearchBar"/>. Each
/// does what OpenMaui's <c>SearchBarHandler</c> mapping of that property does; the handler calls them.
/// </summary>
public static class SkiaSearchBarExtensions
{
    /// <summary>
    /// Shows the query as the search bar displays it (TextTransform applied, cut to MaxLength);
    /// the query the search bar then holds becomes the view's Text, as on MAUI's platforms.
    /// </summary>
    public static void UpdateText(this SkiaSearchBar platformSearchBar, ISearchBar searchBar)
    {
        platformSearchBar.SetDisplayText(searchBar);
        TextInputText.UpdateVirtualText(searchBar, platformSearchBar.Text);
    }

    /// <summary>Puts the display text in the search bar; true when it replaced different text.</summary>
    internal static bool SetDisplayText(this SkiaSearchBar platformSearchBar, ISearchBar searchBar)
    {
        var text = TextInputText.GetDisplayText(searchBar);
        if (platformSearchBar.Text == text)
            return false;
        platformSearchBar.Text = text;
        return true;
    }

    /// <summary>Applies the text color (a null color keeps the current one).</summary>
    public static void UpdateTextColor(this SkiaSearchBar platformSearchBar, ISearchBar searchBar)
    {
        if (searchBar.TextColor is not null)
            platformSearchBar.TextColor = searchBar.TextColor;
    }

    /// <summary>Applies the font: size, family and FontAttributes.</summary>
    public static void UpdateFont(this SkiaSearchBar platformSearchBar, ISearchBar searchBar)
    {
        var font = searchBar.Font;
        if (font.Size > 0)
            platformSearchBar.FontSize = font.Size;
        if (!string.IsNullOrEmpty(font.Family))
            platformSearchBar.FontFamily = font.Family;
        platformSearchBar.FontAttributes = TextStyleMapping.ToFontAttributes(font);
    }

    /// <summary>MAUI's signature with a font manager; the Skia view resolves the family itself.</summary>
    public static void UpdateFont(this SkiaSearchBar platformSearchBar, ISearchBar searchBar, IFontManager fontManager) =>
        platformSearchBar.UpdateFont(searchBar);

    /// <summary>Applies the character spacing.</summary>
    public static void UpdateCharacterSpacing(this SkiaSearchBar platformSearchBar, ISearchBar searchBar)
    {
        platformSearchBar.CharacterSpacing = searchBar.CharacterSpacing;
    }

    /// <summary>Applies the horizontal text alignment.</summary>
    public static void UpdateHorizontalTextAlignment(this SkiaSearchBar platformSearchBar, ISearchBar searchBar)
    {
        platformSearchBar.HorizontalTextAlignment = searchBar.HorizontalTextAlignment;
    }

    /// <summary>Applies the vertical text alignment.</summary>
    public static void UpdateVerticalTextAlignment(this SkiaSearchBar platformSearchBar, ISearchBar searchBar)
    {
        platformSearchBar.VerticalTextAlignment = searchBar.VerticalTextAlignment;
    }

    /// <summary>Applies the placeholder text (none shows nothing).</summary>
    public static void UpdatePlaceholder(this SkiaSearchBar platformSearchBar, ISearchBar searchBar)
    {
        platformSearchBar.Placeholder = searchBar.Placeholder ?? string.Empty;
    }

    /// <summary>Applies the placeholder color (a null color keeps the current one).</summary>
    public static void UpdatePlaceholderColor(this SkiaSearchBar platformSearchBar, ISearchBar searchBar)
    {
        if (searchBar.PlaceholderColor is not null)
            platformSearchBar.PlaceholderColor = searchBar.PlaceholderColor;
    }

    /// <summary>Applies the cancel (clear) button color.</summary>
    public static void UpdateCancelButtonColor(this SkiaSearchBar platformSearchBar, ISearchBar searchBar)
    {
        if (searchBar.CancelButtonColor is not null)
            platformSearchBar.ClearButtonColor = searchBar.CancelButtonColor;
    }

    /// <summary>Applies IsReadOnly.</summary>
    public static void UpdateIsReadOnly(this SkiaSearchBar platformSearchBar, ISearchBar searchBar)
    {
        platformSearchBar.IsReadOnly = searchBar.IsReadOnly;
    }

    /// <summary>Applies MaxLength.</summary>
    public static void UpdateMaxLength(this SkiaSearchBar platformSearchBar, ISearchBar searchBar)
    {
        platformSearchBar.MaxLength = searchBar.MaxLength;
    }

    /// <summary>Applies the keyboard (on the desktop, the input method's content type).</summary>
    public static void UpdateKeyboard(this SkiaSearchBar platformSearchBar, ISearchBar searchBar)
    {
        platformSearchBar.Keyboard = searchBar.Keyboard;
    }

    /// <summary>Applies the return key type.</summary>
    public static void UpdateReturnType(this SkiaSearchBar platformSearchBar, ISearchBar searchBar)
    {
        platformSearchBar.ReturnType = searchBar.ReturnType;
    }

    /// <summary>Applies IsTextPredictionEnabled.</summary>
    public static void UpdateIsTextPredictionEnabled(this SkiaSearchBar platformSearchBar, ISearchBar searchBar)
    {
        platformSearchBar.IsTextPredictionEnabled = searchBar.IsTextPredictionEnabled;
    }

    /// <summary>Applies IsSpellCheckEnabled.</summary>
    public static void UpdateIsSpellCheckEnabled(this SkiaSearchBar platformSearchBar, ISearchBar searchBar)
    {
        platformSearchBar.IsSpellCheckEnabled = searchBar.IsSpellCheckEnabled;
    }
}
