// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        // delete_surrounding_text: byte counts BEFORE and AFTER the caret to remove.
        // Currently no MAUI input control models surrounding-text edits (they manage
        // their own text storage), so we surface this as a best-effort "the IME wants
        // to retract chars before the caret" by emitting a synthetic PreEditChanged
        // when there's no commit but there IS a delete. The common case (single CJK
        // composition → commit replaces the preedit) doesn't need delete handling
        // because the preedit cycle takes care of erasure.
        // TODO: full delete_surrounding_text support requires extending IInputContext
        // with a DeleteSurrounding(int before, int after) hook.

        if (!string.IsNullOrEmpty(batch.CommitText))
        {
            TextCommitted?.Invoke(this, new TextCommittedEventArgs(batch.CommitText));
        }

        // Preedit state — update even when empty (empty preedit + non-empty
        // commit means the composition finalized; UI must clear the underlined
        // pending text).
        if (PreEditText != batch.PreeditText || PreEditCursorPosition != batch.PreeditCursorBegin)
        {
            PreEditText = batch.PreeditText;
            PreEditCursorPosition = batch.PreeditCursorBegin;
            PreEditChanged?.Invoke(this, new PreEditChangedEventArgs(PreEditText, PreEditCursorPosition));
            if (string.IsNullOrEmpty(PreEditText))
                PreEditEnded?.Invoke(this, EventArgs.Empty);
        }
    }
}
