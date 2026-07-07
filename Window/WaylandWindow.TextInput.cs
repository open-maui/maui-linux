// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Window;

// Native zwp_text_input_v3 IME support. Replaces DBus-based IBus (Fcitx5 via DBus
// still works on Wayland because IBus is compositor-agnostic, but text-input-v3
// is the protocol the compositor expects for native IMEs like GNOME Hangul/Pinyin
// or Fcitx5-on-Wayland-natively). The compositor mediates the IME ↔ client
// dialogue, so input methods that don't run a separate daemon (everything baked
// into Mutter/Plasma) only work via this protocol.
//
// Wire protocol (zwp_text_input_v3):
//   manager.get_text_input(seat) → text_input
//   text_input.enable / disable           ← we tell compositor when our focused
//                                            widget wants IME input
//   text_input.set_surrounding_text       ← context for input method
//   text_input.set_cursor_rectangle       ← where to draw the candidate popup
//   text_input.set_content_type           ← purpose + hints
//   text_input.commit                     ← every state batch ends with this
//
// Events arrive in batches terminated by done(serial). Within a batch we collect
// preedit_string / commit_string / delete_surrounding_text, then apply them
// atomically when done() fires AND the done serial matches our last commit
// serial. Mismatched serials mean the compositor was responding to a stale
// state — drop the batch (per spec).
public partial class WaylandWindow
{
    #region Constants

    private const uint ZWP_TEXT_INPUT_MANAGER_V3_GET_TEXT_INPUT = 1;

    private const uint ZWP_TEXT_INPUT_V3_DESTROY = 0;
    private const uint ZWP_TEXT_INPUT_V3_ENABLE = 1;
    private const uint ZWP_TEXT_INPUT_V3_DISABLE = 2;
    private const uint ZWP_TEXT_INPUT_V3_SET_SURROUNDING_TEXT = 3;
    private const uint ZWP_TEXT_INPUT_V3_SET_TEXT_CHANGE_CAUSE = 4;
    private const uint ZWP_TEXT_INPUT_V3_SET_CONTENT_TYPE = 5;
    private const uint ZWP_TEXT_INPUT_V3_SET_CURSOR_RECTANGLE = 6;
    private const uint ZWP_TEXT_INPUT_V3_COMMIT = 7;

    // content_purpose / content_hint default values used when our focused input
    // doesn't request anything specific. "normal" purpose + "none" hints work
    // for plain entries.
    private const uint TI3_PURPOSE_NORMAL = 0;
    private const uint TI3_HINT_NONE = 0;

    #endregion

    #region State

    private static IntPtr _zwp_text_input_manager_v3_interface;
    private static IntPtr _zwp_text_input_v3_interface;

    private IntPtr _textInputManager;
    private IntPtr _textInput;

    // Serial that ticks every time we send commit(). The compositor echoes it
    // back on done() so we can drop responses to stale state.
    private uint _textInputCommitSerial;
    // Serial from the most recent done() we processed — used as a sanity check
    // and to detect events arriving in the wrong order.
    private uint _textInputDoneSerial;

    // Pending batch state, accumulated as preedit/commit/delete events arrive,
    // applied on done() if the serial matches.
    private string _pendingPreedit = string.Empty;
    private int _pendingPreeditCursorBegin;
    private int _pendingPreeditCursorEnd;
    private string _pendingCommit = string.Empty;
    private uint _pendingDeleteBeforeBytes;
    private uint _pendingDeleteAfterBytes;
    private bool _batchHasData;

    // set_surrounding_text / set_text_change_cause are double-buffered: stage
    // them here and flush in CommitTextInput() so they ride whatever commit
    // goes out next (enable, cursor-rectangle, or their own) instead of
    // forcing an extra commit per update.
    private string _stagedSurroundingText = string.Empty;
    private int _stagedSurroundingCursorBytes;
    private int _stagedSurroundingAnchorBytes;
    private TextInputChangeCause _stagedChangeCause;
    private bool _surroundingTextDirty;

    private bool _imeEnabled;
    public bool ImeEnabled => _imeEnabled;
    public bool NativeTextInputAvailable => _textInput != IntPtr.Zero;

    // Listener storage. The struct only stores raw function pointers; the
    // delegate fields below are what keep the delegates (and their native
    // thunks) alive for libwayland.
    private TextInputV3Listener _textInputListener;
    private GCHandle _textInputListenerHandle;
    private TextInputEnterDelegate? _textInputEnterDelegate;
    private TextInputLeaveDelegate? _textInputLeaveDelegate;
    private TextInputPreeditStringDelegate? _textInputPreeditStringDelegate;
    private TextInputCommitStringDelegate? _textInputCommitStringDelegate;
    private TextInputDeleteSurroundingTextDelegate? _textInputDeleteSurroundingTextDelegate;
    private TextInputDoneDelegate? _textInputDoneDelegate;

    #endregion

    #region Delegate types and listener struct

    // zwp_text_input_v3 events.
    private delegate void TextInputEnterDelegate(IntPtr data, IntPtr proxy, IntPtr surface);
    private delegate void TextInputLeaveDelegate(IntPtr data, IntPtr proxy, IntPtr surface);
    private delegate void TextInputPreeditStringDelegate(IntPtr data, IntPtr proxy, IntPtr textPtr, int cursorBegin, int cursorEnd);
    private delegate void TextInputCommitStringDelegate(IntPtr data, IntPtr proxy, IntPtr textPtr);
    private delegate void TextInputDeleteSurroundingTextDelegate(IntPtr data, IntPtr proxy, uint beforeLength, uint afterLength);
    private delegate void TextInputDoneDelegate(IntPtr data, IntPtr proxy, uint serial);

    [StructLayout(LayoutKind.Sequential)]
    private struct TextInputV3Listener
    {
        public IntPtr Enter;
        public IntPtr Leave;
        public IntPtr PreeditString;
        public IntPtr CommitString;
        public IntPtr DeleteSurroundingText;
        public IntPtr Done;
    }

    #endregion

    #region Setup / teardown

    /// <summary>
    /// Wire the per-seat zwp_text_input_v3. Guarded so it only runs once both
    /// the manager and the seat are present. Called from the registry handler
    /// when the manager binds, and from SetupSeat as a safety net for the case
    /// where the seat global arrives later than the text_input_manager.
    /// </summary>
    internal void SetupTextInput()
    {
        if (_textInput != IntPtr.Zero) return;
        if (_textInputManager == IntPtr.Zero || _seat == IntPtr.Zero) return;

        // get_text_input: signature "no" (new_id, object). Use the same
        // marshal_constructor form as wl_data_device_manager.get_data_device.
        _textInput = wl_proxy_marshal_constructor(
            _textInputManager,
            ZWP_TEXT_INPUT_MANAGER_V3_GET_TEXT_INPUT,
            _zwp_text_input_v3_interface,
            IntPtr.Zero,
            _seat);

        if (_textInput == IntPtr.Zero)
        {
            DiagnosticLog.Warn("WaylandWindow", "Failed to create zwp_text_input_v3; IME will fall back to IBus over DBus");
            return;
        }

        // Root the delegates in fields first — GetFunctionPointerForDelegate
        // does not keep its delegate alive, and libwayland holds these pointers
        // for the lifetime of the text_input proxy.
        _textInputEnterDelegate = OnTextInputEnter;
        _textInputLeaveDelegate = OnTextInputLeave;
        _textInputPreeditStringDelegate = OnTextInputPreeditString;
        _textInputCommitStringDelegate = OnTextInputCommitString;
        _textInputDeleteSurroundingTextDelegate = OnTextInputDeleteSurroundingText;
        _textInputDoneDelegate = OnTextInputDone;
        _textInputListener = new TextInputV3Listener
        {
            Enter = Marshal.GetFunctionPointerForDelegate(_textInputEnterDelegate),
            Leave = Marshal.GetFunctionPointerForDelegate(_textInputLeaveDelegate),
            PreeditString = Marshal.GetFunctionPointerForDelegate(_textInputPreeditStringDelegate),
            CommitString = Marshal.GetFunctionPointerForDelegate(_textInputCommitStringDelegate),
            DeleteSurroundingText = Marshal.GetFunctionPointerForDelegate(_textInputDeleteSurroundingTextDelegate),
            Done = Marshal.GetFunctionPointerForDelegate(_textInputDoneDelegate),
        };
        _textInputListenerHandle = GCHandle.Alloc(_textInputListener, GCHandleType.Pinned);
        wl_proxy_add_listener(_textInput, _textInputListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));

        // Register as the active text-input backend so WaylandTextInputV3Service
        // can route through us.
        s_activeTextInputWindow = this;

        DiagnosticLog.Debug("WaylandWindow", "Native zwp_text_input_v3 wired");
    }

    private void DisposeTextInput()
    {
        if (_textInput != IntPtr.Zero)
        {
            wl_proxy_marshal(_textInput, ZWP_TEXT_INPUT_V3_DESTROY);
            wl_proxy_destroy(_textInput);
            _textInput = IntPtr.Zero;
        }
        if (_textInputListenerHandle.IsAllocated)
            _textInputListenerHandle.Free();
        if (_textInputManager != IntPtr.Zero)
        {
            wl_proxy_destroy(_textInputManager);
            _textInputManager = IntPtr.Zero;
        }
        if (s_activeTextInputWindow == this)
            s_activeTextInputWindow = null;
    }

    #endregion

    #region zwp_text_input_v3 event handlers

    // enter/leave fire as our wl_surface gains/loses keyboard focus. They tell
    // the IME which surface to attach popups to. We don't have to do anything
    // structural here — focus state in the view tree is tracked separately.
    private static void OnTextInputEnter(IntPtr data, IntPtr proxy, IntPtr surface) { }
    private static void OnTextInputLeave(IntPtr data, IntPtr proxy, IntPtr surface)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;
        // On leave, drop any pending preedit so we don't keep ghost composition
        // text after focus moves to a non-text view.
        window._pendingPreedit = string.Empty;
        window._pendingPreeditCursorBegin = 0;
        window._pendingPreeditCursorEnd = 0;
        window._batchHasData = true;
    }

    private static void OnTextInputPreeditString(IntPtr data, IntPtr proxy, IntPtr textPtr, int cursorBegin, int cursorEnd)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;
        window._pendingPreedit = Marshal.PtrToStringUTF8(textPtr) ?? string.Empty;
        window._pendingPreeditCursorBegin = cursorBegin;
        window._pendingPreeditCursorEnd = cursorEnd;
        window._batchHasData = true;
    }

    private static void OnTextInputCommitString(IntPtr data, IntPtr proxy, IntPtr textPtr)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;
        window._pendingCommit = Marshal.PtrToStringUTF8(textPtr) ?? string.Empty;
        window._batchHasData = true;
    }

    private static void OnTextInputDeleteSurroundingText(IntPtr data, IntPtr proxy, uint beforeLength, uint afterLength)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;
        window._pendingDeleteBeforeBytes = beforeLength;
        window._pendingDeleteAfterBytes = afterLength;
        window._batchHasData = true;
    }

    private static void OnTextInputDone(IntPtr data, IntPtr proxy, uint serial)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        window._textInputDoneSerial = serial;

        // Drop stale batches: if the compositor's done.serial doesn't match
        // our latest commit, this is a response to state we already updated.
        // (Spec: any change between commit and matching done is informational
        // only and must be discarded.)
        if (serial != window._textInputCommitSerial)
        {
            window._batchHasData = false;
            return;
        }

        if (!window._batchHasData)
            return;

        // Apply the batch: delete-around → commit → preedit. The order matters:
        // delete first removes the surrounding text, then commit inserts new,
        // then preedit shows the new composition under the caret.
        var deleteBefore = window._pendingDeleteBeforeBytes;
        var deleteAfter = window._pendingDeleteAfterBytes;
        var commit = window._pendingCommit;
        var preedit = window._pendingPreedit;
        var preeditCursorBegin = window._pendingPreeditCursorBegin;
        var preeditCursorEnd = window._pendingPreeditCursorEnd;

        // Reset the batch buffers now (handlers below may trigger UI work that
        // schedules a new commit which will start a fresh batch).
        window._pendingDeleteBeforeBytes = 0;
        window._pendingDeleteAfterBytes = 0;
        window._pendingCommit = string.Empty;
        window._pendingPreedit = string.Empty;
        window._pendingPreeditCursorBegin = 0;
        window._pendingPreeditCursorEnd = 0;
        window._batchHasData = false;

        // Marshal into the service via the static accessor — the service
        // forwards into the focused MAUI input control on the main thread.
        window.TextInputBatchApplied?.Invoke(window, new TextInputBatch(deleteBefore, deleteAfter, commit, preedit, preeditCursorBegin, preeditCursorEnd));
    }

    #endregion

    #region Public API (called from WaylandTextInputV3Service)

    private static WaylandWindow? s_activeTextInputWindow;

    public static WaylandWindow? ActiveTextInputWindow => s_activeTextInputWindow;

    /// <summary>
    /// Fired on the main thread when a done() batch is applied. Subscribers
    /// (WaylandTextInputV3Service) translate the (delete, commit, preedit)
    /// triple into IInputMethodService events.
    /// </summary>
    public event EventHandler<TextInputBatch>? TextInputBatchApplied;

    /// <summary>
    /// Tell the compositor that the focused widget wants IME input. The next
    /// commit() will activate the IME state machine; the compositor then sends
    /// preedit_string / commit_string events as the user types into the IME.
    /// </summary>
    public void EnableTextInput()
    {
        if (_textInput == IntPtr.Zero || _imeEnabled) return;
        wl_proxy_marshal(_textInput, ZWP_TEXT_INPUT_V3_ENABLE);
        wl_proxy_marshal(_textInput, ZWP_TEXT_INPUT_V3_SET_CONTENT_TYPE, TI3_HINT_NONE, TI3_PURPOSE_NORMAL);
        CommitTextInput();
        _imeEnabled = true;
    }

    public void DisableTextInput()
    {
        if (_textInput == IntPtr.Zero || !_imeEnabled) return;
        // disable resets all double-buffered state compositor-side; drop any
        // staged surrounding text so it can't leak into a later enable.
        _surroundingTextDirty = false;
        _stagedSurroundingText = string.Empty;
        wl_proxy_marshal(_textInput, ZWP_TEXT_INPUT_V3_DISABLE);
        CommitTextInput();
        _imeEnabled = false;
    }

    /// <summary>
    /// Inform the compositor where the caret is so the IME can position its
    /// candidate popup. Coordinates are in *surface-logical* pixels. Call this
    /// whenever the focused widget's caret moves; auto-commits so the update
    /// reaches the IME between keystrokes.
    /// </summary>
    public void SetCursorRectangle(int x, int y, int width, int height)
    {
        if (_textInput == IntPtr.Zero || !_imeEnabled) return;
        wl_proxy_marshal(_textInput, ZWP_TEXT_INPUT_V3_SET_CURSOR_RECTANGLE, x, y, width, height);
        CommitTextInput();
    }

    /// <summary>
    /// Stage the focused control's surrounding text for the IME.
    /// <paramref name="cursorBytes"/> and <paramref name="anchorBytes"/> are
    /// byte offsets into the UTF-8 encoding of <paramref name="text"/>; the
    /// caller windows the text to stay under the 4096-byte wire cap. Rides the
    /// next commit(): auto-commits when the IME is already enabled, and when
    /// staged just before EnableTextInput the enable's own commit carries it.
    /// </summary>
    public void SetSurroundingText(string text, int cursorBytes, int anchorBytes, TextInputChangeCause cause)
    {
        if (_textInput == IntPtr.Zero) return;
        _stagedSurroundingText = text;
        _stagedSurroundingCursorBytes = cursorBytes;
        _stagedSurroundingAnchorBytes = anchorBytes;
        _stagedChangeCause = cause;
        _surroundingTextDirty = true;
        if (_imeEnabled)
            CommitTextInput();
    }

    private void CommitTextInput()
    {
        if (_surroundingTextDirty)
        {
            wl_proxy_marshal_string_int_int(_textInput, ZWP_TEXT_INPUT_V3_SET_SURROUNDING_TEXT,
                _stagedSurroundingText, _stagedSurroundingCursorBytes, _stagedSurroundingAnchorBytes);
            wl_proxy_marshal(_textInput, ZWP_TEXT_INPUT_V3_SET_TEXT_CHANGE_CAUSE, (uint)_stagedChangeCause);
            _surroundingTextDirty = false;
        }
        unchecked { _textInputCommitSerial++; }
        wl_proxy_marshal(_textInput, ZWP_TEXT_INPUT_V3_COMMIT);
        wl_display_flush(_display);
    }

    #endregion
}

/// <summary>
/// Snapshot of a zwp_text_input_v3 done() batch — the atomic unit that
/// WaylandTextInputV3Service translates into IInputMethodService events.
/// </summary>
public readonly record struct TextInputBatch(
    uint DeleteBeforeBytes,
    uint DeleteAfterBytes,
    string CommitText,
    string PreeditText,
    int PreeditCursorBegin,
    int PreeditCursorEnd);

/// <summary>
/// zwp_text_input_v3.change_cause — who changed the text since the last
/// set_surrounding_text: the input method's own commit/delete batch, or
/// anything else (user typing, caret moves, programmatic updates).
/// </summary>
public enum TextInputChangeCause : uint
{
    InputMethod = 0,
    Other = 1,
}
