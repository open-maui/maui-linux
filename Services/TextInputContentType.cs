// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// What a text field is for, as the input method sees it. Values are those of
/// <c>zwp_text_input_v3.content_purpose</c>, so the Wayland service sends them
/// unchanged; other services translate.
/// </summary>
public enum TextInputPurpose : uint
{
    Normal = 0,
    Alpha = 1,
    Digits = 2,
    Number = 3,
    Phone = 4,
    Url = 5,
    Email = 6,
    Name = 7,
    Password = 8,
    Pin = 9,
    Date = 10,
    Time = 11,
    DateTime = 12,
    Terminal = 13,
}

/// <summary>
/// How the input method should treat the text of a field. Values are those of
/// <c>zwp_text_input_v3.content_hint</c>.
/// </summary>
[Flags]
public enum TextInputHints : uint
{
    None = 0x0,
    Completion = 0x1,
    Spellcheck = 0x2,
    AutoCapitalization = 0x4,
    Lowercase = 0x8,
    Uppercase = 0x10,
    Titlecase = 0x20,
    HiddenText = 0x40,
    SensitiveData = 0x80,
    Latin = 0x100,
    Multiline = 0x200,
}

/// <summary>
/// The content type a focused <see cref="IInputContext"/> asks the input method
/// for: on desktop this is what MAUI's <see cref="Keyboard"/> becomes (a
/// numeric field gets a number purpose, a password field hidden text, and so on).
/// </summary>
public readonly record struct TextInputContentType(TextInputPurpose Purpose, TextInputHints Hints)
{
    /// <summary>A plain field with no hints.</summary>
    public static TextInputContentType Default => new(TextInputPurpose.Normal, TextInputHints.None);

    /// <summary>
    /// Maps a MAUI text input's settings to the input-method content type, as
    /// the shipped platforms map them to their soft-keyboard input types.
    /// </summary>
    public static TextInputContentType FromKeyboard(
        Keyboard? keyboard,
        bool isPassword = false,
        bool isTextPredictionEnabled = true,
        bool isSpellCheckEnabled = true,
        bool isMultiline = false)
    {
        var purpose = TextInputPurpose.Normal;
        var hints = TextInputHints.None;
        // Prediction and spell checking apply to free text only; the specialised
        // keyboards (numbers, addresses) turn them off on every platform.
        bool freeText = true;

        if (keyboard == Keyboard.Numeric)
        {
            purpose = TextInputPurpose.Number;
            freeText = false;
        }
        else if (keyboard == Keyboard.Telephone)
        {
            purpose = TextInputPurpose.Phone;
            freeText = false;
        }
        else if (keyboard == Keyboard.Email)
        {
            purpose = TextInputPurpose.Email;
            freeText = false;
        }
        else if (keyboard == Keyboard.Url)
        {
            purpose = TextInputPurpose.Url;
            freeText = false;
        }
        else if (keyboard == Keyboard.Date)
        {
            purpose = TextInputPurpose.Date;
            freeText = false;
        }
        else if (keyboard == Keyboard.Time)
        {
            purpose = TextInputPurpose.Time;
            freeText = false;
        }
        else if (keyboard == Keyboard.Password)
        {
            purpose = TextInputPurpose.Password;
            freeText = false;
        }
        else if (keyboard == Keyboard.Plain)
        {
            // Plain: no capitalization, prediction or spell checking.
            freeText = false;
        }
        else if (keyboard == Keyboard.Text || keyboard == Keyboard.Chat)
        {
            hints |= TextInputHints.AutoCapitalization;
        }
        else if (keyboard is CustomKeyboard custom)
        {
            var flags = custom.Flags;
            if ((flags & KeyboardFlags.CapitalizeCharacter) == KeyboardFlags.CapitalizeCharacter)
                hints |= TextInputHints.Uppercase;
            else if ((flags & KeyboardFlags.CapitalizeWord) == KeyboardFlags.CapitalizeWord)
                hints |= TextInputHints.Titlecase;
            else if ((flags & KeyboardFlags.CapitalizeSentence) == KeyboardFlags.CapitalizeSentence)
                hints |= TextInputHints.AutoCapitalization;
            // A custom keyboard states prediction and spell checking itself.
            if ((flags & KeyboardFlags.Suggestions) != 0 && isTextPredictionEnabled)
                hints |= TextInputHints.Completion;
            if ((flags & KeyboardFlags.Spellcheck) != 0 && isSpellCheckEnabled)
                hints |= TextInputHints.Spellcheck;
            freeText = false;
        }

        if (freeText)
        {
            if (isTextPredictionEnabled)
                hints |= TextInputHints.Completion;
            if (isSpellCheckEnabled)
                hints |= TextInputHints.Spellcheck;
        }

        if (isPassword)
        {
            purpose = TextInputPurpose.Password;
            hints = (hints & ~(TextInputHints.Completion | TextInputHints.Spellcheck | TextInputHints.AutoCapitalization))
                | TextInputHints.HiddenText | TextInputHints.SensitiveData;
        }
        else if (purpose == TextInputPurpose.Password)
        {
            hints |= TextInputHints.HiddenText | TextInputHints.SensitiveData;
        }

        if (isMultiline)
            hints |= TextInputHints.Multiline;

        return new TextInputContentType(purpose, hints);
    }
}
