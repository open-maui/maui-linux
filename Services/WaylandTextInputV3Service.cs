// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// IInputMethodService backed by the native zwp_text_input_v3 Wayland protocol.
/// Used on native Wayland sessions whenever the compositor exposes
/// zwp_text_input_manager_v3 (every modern compositor does — Mutter/GNOME, KWin,
/// Sway, Hyprland). On X11 or compositors without the protocol the factory
/// falls back to IBus-over-DBus, which still works on Wayland but doesn't talk
/// directly to compositor-integrated IMEs (GNOME Pinyin/Hangul/Anthy, etc.).
///
/// State plumbing:
///   App side                       This service                    WaylandWindow
///   SetFocus(entry)            →   EnableTextInput()           →   text_input.enable+commit
///   SetCursorLocation(x,y,w,h) →   SetCursorRectangle(...)     →   text_input.set_cursor_rectangle+commit
///   SetFocus(null)             →   DisableTextInput()          →   text_input.disable+commit
///                                  TextInputBatchApplied event ←   done(serial) handler
///   PreEditChanged, TextCommitted (raised on the main thread; subscribers
///   surface preedit under the caret and append commit text into the focused
///   input control).
/// </summary>
public class WaylandTextInputV3Service : IInputMethodService
{
    private IInputContext? _context;
    private WaylandWindow? _wlWindow;

    public bool IsActive => _wlWindow?.ImeEnabled ?? false;
    public string PreEditText { get; private set; } = string.Empty;
    public int PreEditCursorPosition { get; private set; }

    public event EventHandler<TextCommittedEventArgs>? TextCommitted;
    public event EventHandler<PreEditChangedEventArgs>? PreEditChanged;
    public event EventHandler? PreEditEnded;

    /// <summary>
    /// True when a WaylandWindow has bound zwp_text_input_v3 and is ready to
    /// route IME requests. Used by InputMethodServiceFactory to decide whether
    /// to instantiate this service or fall back to IBus.
    /// </summary>
    public static bool IsAvailable()
        => WaylandWindow.ActiveTextInputWindow != null
           && WaylandWindow.ActiveTextInputWindow.NativeTextInputAvailable;

    public void Initialize(IntPtr windowHandle)
    {
        _wlWindow = WaylandWindow.ActiveTextInputWindow;
        if (_wlWindow != null)
        {
            _wlWindow.TextInputBatchApplied += OnTextInputBatchApplied;
        }
        else
        {
            DiagnosticLog.Warn("WaylandTextInputV3Service", "No active WaylandWindow at Initialize() — IME will be a no-op until one wires up");
        }
    }

    public void SetFocus(IInputContext? context)
    {
        // If the active window wasn't ready at Initialize() time, late-bind now
        // so that focus-gain doesn't silently drop on the floor.
        if (_wlWindow == null)
        {
            _wlWindow = WaylandWindow.ActiveTextInputWindow;
            if (_wlWindow != null)
                _wlWindow.TextInputBatchApplied += OnTextInputBatchApplied;
        }
        if (_wlWindow == null) return;

        _context = context;
        if (context != null)
            _wlWindow.EnableTextInput();
        else
            _wlWindow.DisableTextInput();
    }

    public void SetCursorLocation(int x, int y, int width, int height)
    {
        _wlWindow?.SetCursorRectangle(x, y, width, height);
    }

    /// <summary>
    /// text-input-v3 is a state-machine protocol — key events go DIRECTLY to the
    /// compositor's IME backend (Mutter/Plasma route them before forwarding the
    /// resulting commit_string to us). The app's keyboard handler is unaware of
    /// composition keys and so should never consult ProcessKeyEvent — returning
    /// false is safe and matches the no-op contract.
    /// </summary>
    public bool ProcessKeyEvent(uint keyCode, KeyModifiers modifiers, bool isKeyDown) => false;

    public void Reset()
    {
        // Disable + re-enable cancels any in-progress composition. Only do this
        // if we still believe we're enabled — calling disable on a non-enabled
        // input_v3 is a protocol error.
        if (_wlWindow != null && _wlWindow.ImeEnabled)
        {
            _wlWindow.DisableTextInput();
            if (_context != null)
                _wlWindow.EnableTextInput();
        }
        PreEditText = string.Empty;
        PreEditCursorPosition = 0;
        PreEditEnded?.Invoke(this, EventArgs.Empty);
    }

    public void Shutdown()
    {
        if (_wlWindow != null)
        {
            _wlWindow.TextInputBatchApplied -= OnTextInputBatchApplied;
            if (_wlWindow.ImeEnabled)
                _wlWindow.DisableTextInput();
            _wlWindow = null;
        }
        _context = null;
        PreEditText = string.Empty;
        PreEditCursorPosition = 0;
    }

    private void OnTextInputBatchApplied(object? sender, TextInputBatch batch)
    {
        // Spec-defined order: delete_surrounding_text first, then commit_string,
        // then preedit_string. Delete trims the anchor around the caret, commit
        // inserts the finalized text at the new caret, preedit shows whatever
        // composition is still active under the (new) caret.

        if (batch.DeleteBeforeBytes > 0 || batch.DeleteAfterBytes > 0)
        {
            if (_context != null)
            {
                var ctxText = _context.Text ?? string.Empty;
                var caret = Math.Clamp(_context.CursorPosition, 0, ctxText.Length);
                var beforeChars = Utf8BytesToCharsBeforeCaret(ctxText, caret, (int)batch.DeleteBeforeBytes);
                var afterChars = Utf8BytesToCharsAfterCaret(ctxText, caret, (int)batch.DeleteAfterBytes);
                if (beforeChars > 0 || afterChars > 0)
                    _context.DeleteSurrounding(beforeChars, afterChars);
            }
        }

        if (!string.IsNullOrEmpty(batch.CommitText))
        {
            TextCommitted?.Invoke(this, new TextCommittedEventArgs(batch.CommitText));
            _context?.OnTextCommitted(batch.CommitText);
        }

        // Preedit state — update even when empty (empty preedit + non-empty
        // commit means the composition finalized; UI must clear the underlined
        // pending text).
        if (PreEditText != batch.PreeditText || PreEditCursorPosition != batch.PreeditCursorBegin)
        {
            PreEditText = batch.PreeditText;
            PreEditCursorPosition = batch.PreeditCursorBegin;
            PreEditChanged?.Invoke(this, new PreEditChangedEventArgs(PreEditText, PreEditCursorPosition));
            _context?.OnPreEditChanged(PreEditText, PreEditCursorPosition);
            if (string.IsNullOrEmpty(PreEditText))
            {
                PreEditEnded?.Invoke(this, EventArgs.Empty);
                _context?.OnPreEditEnded();
            }
        }
    }

    /// <summary>
    /// Convert "N UTF-8 bytes immediately before the caret" to the equivalent
    /// number of UTF-16 code units in <paramref name="text"/>. Walks back one
    /// code point at a time, accumulating its UTF-8 byte cost, so a byte count
    /// that lands mid-multi-byte-character clamps to the previous boundary
    /// rather than producing a half-decoded char.
    /// </summary>
    internal static int Utf8BytesToCharsBeforeCaret(string text, int caret, int byteCount)
    {
        if (byteCount <= 0 || caret <= 0 || string.IsNullOrEmpty(text)) return 0;
        caret = Math.Min(caret, text.Length);

        int chars = 0;
        int bytesSoFar = 0;
        int i = caret;
        while (i > 0 && bytesSoFar < byteCount)
        {
            int step = 1;
            if (i >= 2 && char.IsLowSurrogate(text[i - 1]) && char.IsHighSurrogate(text[i - 2]))
                step = 2;
            i -= step;
            int cpBytes = Encoding.UTF8.GetByteCount(text.AsSpan(i, step));
            if (bytesSoFar + cpBytes > byteCount) break;   // clamp at boundary
            bytesSoFar += cpBytes;
            chars += step;
        }
        return chars;
    }

    internal static int Utf8BytesToCharsAfterCaret(string text, int caret, int byteCount)
    {
        if (byteCount <= 0 || string.IsNullOrEmpty(text)) return 0;
        caret = Math.Clamp(caret, 0, text.Length);

        int chars = 0;
        int bytesSoFar = 0;
        int i = caret;
        while (i < text.Length && bytesSoFar < byteCount)
        {
            int step = 1;
            if (i + 1 < text.Length && char.IsHighSurrogate(text[i]) && char.IsLowSurrogate(text[i + 1]))
                step = 2;
            int cpBytes = Encoding.UTF8.GetByteCount(text.AsSpan(i, step));
            if (bytesSoFar + cpBytes > byteCount) break;   // clamp at boundary
            bytesSoFar += cpBytes;
            i += step;
            chars += step;
        }
        return chars;
    }
}
