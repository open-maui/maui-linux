// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Interface for Input Method Editor (IME) services.
/// Provides support for complex text input methods like CJK languages.
/// </summary>
public interface IInputMethodService
{
    /// <summary>
    /// Gets whether IME is currently active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets the current pre-edit (composition) text.
    /// </summary>
    string PreEditText { get; }

    /// <summary>
    /// Gets the cursor position within the pre-edit text.
    /// </summary>
    int PreEditCursorPosition { get; }

    /// <summary>
    /// Initializes the IME service for the given window.
    /// </summary>
    /// <param name="windowHandle">The native window handle.</param>
    void Initialize(nint windowHandle);

    /// <summary>
    /// Sets focus to the specified input context.
    /// </summary>
    /// <param name="context">The input context to focus.</param>
    void SetFocus(IInputContext? context);

    /// <summary>
    /// Sets the cursor location for candidate window positioning.
    /// </summary>
    /// <param name="x">X coordinate in screen space.</param>
    /// <param name="y">Y coordinate in screen space.</param>
    /// <param name="width">Width of the cursor area.</param>
    /// <param name="height">Height of the cursor area.</param>
    void SetCursorLocation(int x, int y, int width, int height);

    /// <summary>
    /// Processes a key event through the IME.
    /// </summary>
    /// <param name="keyCode">The key code.</param>
    /// <param name="modifiers">Key modifiers.</param>
    /// <param name="isKeyDown">True for key press, false for key release.</param>
    /// <returns>True if the IME handled the event.</returns>
    bool ProcessKeyEvent(uint keyCode, KeyModifiers modifiers, bool isKeyDown);

    /// <summary>
    /// Resets the IME state, canceling any composition.
    /// </summary>
    void Reset();

    /// <summary>
    /// Shuts down the IME service.
    /// </summary>
    void Shutdown();

    /// <summary>
    /// Event raised when text is committed from IME.
    /// </summary>
    event EventHandler<TextCommittedEventArgs>? TextCommitted;

    /// <summary>
    /// Event raised when pre-edit (composition) text changes.
    /// </summary>
    event EventHandler<PreEditChangedEventArgs>? PreEditChanged;

    /// <summary>
    /// Event raised when pre-edit is completed or cancelled.
    /// </summary>
    event EventHandler? PreEditEnded;
}

/// <summary>
/// Represents an input context that can receive IME input.
/// </summary>
public interface IInputContext
{
    /// <summary>
    /// Gets or sets the current text content.
    /// </summary>
    string Text { get; set; }

    /// <summary>
    /// Gets or sets the cursor position.
    /// </summary>
    int CursorPosition { get; set; }

    /// <summary>
    /// Gets the selection start position.
    /// </summary>
    int SelectionStart { get; }

    /// <summary>
    /// Gets the selection length.
    /// </summary>
    int SelectionLength { get; }

    /// <summary>
    /// Called when text is committed from the IME.
    /// </summary>
    /// <param name="text">The committed text.</param>
    void OnTextCommitted(string text);

    /// <summary>
    /// Called when pre-edit text changes.
    /// </summary>
    /// <param name="preEditText">The current pre-edit text.</param>
    /// <param name="cursorPosition">Cursor position within pre-edit text.</param>
    void OnPreEditChanged(string preEditText, int cursorPosition);

    /// <summary>
    /// Called when pre-edit mode ends.
    /// </summary>
    void OnPreEditEnded();
}

/// <summary>
/// Event args for text committed events.
/// </summary>
public class TextCommittedEventArgs : EventArgs
{
    /// <summary>
    /// The committed text.
    /// </summary>
    public string Text { get; }

    public TextCommittedEventArgs(string text)
    {
        Text = text;
    }
}

/// <summary>
/// Event args for pre-edit changed events.
/// </summary>
public class PreEditChangedEventArgs : EventArgs
{
    /// <summary>
    /// The current pre-edit text.
    /// </summary>
    public string PreEditText { get; }

    /// <summary>
    /// Cursor position within the pre-edit text.
    /// </summary>
    public int CursorPosition { get; }

    /// <summary>
    /// Formatting attributes for the pre-edit text.
    /// </summary>
    public IReadOnlyList<PreEditAttribute> Attributes { get; }

    public PreEditChangedEventArgs(string preEditText, int cursorPosition, IReadOnlyList<PreEditAttribute>? attributes = null)
    {
        PreEditText = preEditText;
        CursorPosition = cursorPosition;
        Attributes = attributes ?? Array.Empty<PreEditAttribute>();
    }
}

/// <summary>
/// Represents formatting for a portion of pre-edit text.
/// </summary>
public class PreEditAttribute
{
    /// <summary>
    /// Start position in the pre-edit text.
    /// </summary>
    public int Start { get; set; }

    /// <summary>
    /// Length of the attributed range.
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// The attribute type.
    /// </summary>
    public PreEditAttributeType Type { get; set; }
}

/// <summary>
/// Types of pre-edit text attributes.
/// </summary>
public enum PreEditAttributeType
{
    /// <summary>
    /// Normal text (no special formatting).
    /// </summary>
    None,

    /// <summary>
    /// Underlined text (typical for composition).
    /// </summary>
    Underline,

    /// <summary>
    /// Highlighted/selected text.
    /// </summary>
    Highlighted,

    /// <summary>
    /// Reverse video (selected clause in some IMEs).
    /// </summary>
    Reverse
}

/// <summary>
/// Key modifiers for IME processing.
/// </summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1 << 0,
    Control = 1 << 1,
    Alt = 1 << 2,
    Super = 1 << 3,
    CapsLock = 1 << 4,
    NumLock = 1 << 5
}
