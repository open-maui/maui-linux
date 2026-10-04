// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform;

/// <summary>
/// MAUI's text-input rules shared by the Skia text views and their handlers.
/// </summary>
internal static class TextInputText
{
    /// <summary>
    /// <paramref name="text"/> cut to <paramref name="maxLength"/> characters;
    /// a negative maximum is unlimited and 0 leaves no text (MAUI's MaxLength).
    /// </summary>
    public static string TrimToMaxLength(string text, int maxLength)
    {
        if (maxLength < 0 || text.Length <= maxLength)
            return text;
        return text.Substring(0, maxLength);
    }

    /// <summary>
    /// True when the two texts differ in a way an app can see: null and "" are
    /// the same text (MAUI's platforms cannot hold null).
    /// </summary>
    public static bool Differs(string? a, string? b) =>
        !string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// Writes text the platform view now holds back to the MAUI view, as MAUI's
    /// <c>TextInputExtensions.UpdateText</c> does: only when it differs, so an
    /// app's null Text is never replaced by "" (and no TextChanged is raised).
    /// </summary>
    public static void UpdateVirtualText(ITextInput textInput, string? platformText)
    {
        if (Differs(textInput.Text, platformText))
            textInput.Text = platformText ?? string.Empty;
    }

    /// <summary>
    /// The text a MAUI view shows: its Text with its TextTransform applied (the
    /// Controls input views), trimmed to its MaxLength.
    /// </summary>
    public static string GetDisplayText(ITextInput textInput)
    {
        var text = textInput.Text ?? string.Empty;
        if (textInput is Microsoft.Maui.Controls.InputView inputView && inputView.TextTransform != TextTransform.Default)
            text = inputView.UpdateFormsText(text, inputView.TextTransform) ?? string.Empty;
        return TrimToMaxLength(text, textInput.MaxLength);
    }

    /// <summary>
    /// MAUI's selection length rule (TextInputExtensions.GetSelectionLength): never
    /// past the end of the text from the caret.
    /// </summary>
    public static int ClampSelectionLength(string? text, int cursorPosition, int selectionLength) =>
        Math.Clamp(selectionLength, 0, Math.Max(0, (text ?? string.Empty).Length - cursorPosition));

    /// <summary>
    /// Reports the platform's caret and selection to the MAUI view, as MAUI's
    /// platforms do from their selection-changed events (a capped CursorPosition or
    /// SelectionLength, the caret moved by a replaced text). Only differences are
    /// written.
    /// </summary>
    public static void ReportSelection(ITextInput textInput, int cursorPosition, int selectionLength)
    {
        if (textInput.CursorPosition != cursorPosition)
            textInput.CursorPosition = cursorPosition;
        if (textInput.SelectionLength != selectionLength)
            textInput.SelectionLength = selectionLength;
    }
}
