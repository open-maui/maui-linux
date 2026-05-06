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
