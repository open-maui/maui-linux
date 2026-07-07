// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public interface IInputContext
{
    string Text { get; set; }

    int CursorPosition { get; set; }

    int SelectionStart { get; }

    int SelectionLength { get; }

    /// <summary>
    /// True when the content must never be shared with the input method as
    /// surrounding text (password / sensitive entries). Services skip
    /// set_surrounding_text-style updates for sensitive contexts. Default
    /// false so existing IInputContext implementations don't break.
    /// </summary>
    bool IsSurroundingTextSensitive => false;

    void OnTextCommitted(string text);

    void OnPreEditChanged(string preEditText, int cursorPosition);

    void OnPreEditEnded();

    /// <summary>
    /// IME asked us to remove characters from the caret's immediate surroundings
    /// (Wayland <c>zwp_text_input_v3.delete_surrounding_text</c> or the equivalent
    /// IBus signal). Counts are UTF-16 code units in <see cref="Text"/> — the
    /// input-method service is responsible for converting protocol-level UTF-8
    /// byte counts before calling this. Default no-op so external IInputContext
    /// implementations don't break when this method is added.
    /// </summary>
    void DeleteSurrounding(int beforeChars, int afterChars) { }
}
