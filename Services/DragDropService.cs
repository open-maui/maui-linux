// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Drag-and-drop event hub for both backends. On X11 it implements the XDND
/// protocol: X11Window routes XdndEnter/Position/Leave/Drop ClientMessages,
/// SelectionNotify, and PropertyNotify events here, and drop data is fetched
/// asynchronously via selection conversion (with INCR support for large
/// transfers). On Wayland, WaylandWindow pushes events in via the internal
/// Raise* methods.
/// </summary>
public partial class DragDropService : IDisposable
{
    // Process-wide singleton. Both the X11 and Wayland backends push events into
    // this instance; app code subscribes once and gets DnD events regardless of
    // which display server is in use. DI can still resolve a separate instance
    // when the consumer prefers it — Default just covers the common case.
    private static readonly Lazy<DragDropService> s_default = new(() => new DragDropService());
    public static DragDropService Default => s_default.Value;

    private nint _display;
    private nint _window;
    private bool _isDragging;
    private DragData? _currentDragData;
    private nint _dragSource;
    private nint _dragTarget;
    private bool _disposed;

    // XDND atoms
    private nint _xdndAware;
    private nint _xdndEnter;
    private nint _xdndPosition;
    private nint _xdndStatus;
    private nint _xdndLeave;
    private nint _xdndDrop;
    private nint _xdndFinished;
    private nint _xdndSelection;
    private nint _xdndActionCopy;
    private nint _xdndActionMove;
    private nint _xdndActionLink;
    private nint _xdndTypeList;

    // Common MIME types
    private nint _textPlain;
    private nint _textPlainUtf8;
    private nint _textUri;
    private nint _utf8String;
    private nint _applicationOctetStream;
    private nint _targets;
    private nint _xdndProxy;

    // INCR — the X11 large-transfer protocol atom.
    private nint _incr;

    // Pending XDND drop transfer. XdndDrop starts an asynchronous selection
    // conversion; the data arrives later via SelectionNotify (and PropertyNotify
    // chunks for INCR transfers), both routed in from the X11 window's event
    // loop. XdndFinished is only sent once the transfer completes or times out.
    private const int DropTimeoutMs = 2000;
    private bool _dropPending;
    private nint _dropTargetType;
    private long _dropDeadline; // Environment.TickCount64 deadline
    private bool _incrActive;
    private MemoryStream? _incrBuffer;

    /// <summary>
    /// Gets whether a drag operation is in progress.
    /// </summary>
    public bool IsDragging => _isDragging;

    /// <summary>
    /// Event raised when a drag enters the window.
    /// </summary>
    public event EventHandler<DragEventArgs>? DragEnter;

    /// <summary>
    /// Event raised when dragging over the window.
    /// </summary>
    public event EventHandler<DragEventArgs>? DragOver;

    /// <summary>
    /// Event raised when a drag leaves the window.
    /// </summary>
    public event EventHandler? DragLeave;

    /// <summary>
    /// Event raised when a drop occurs.
    /// </summary>
    public event EventHandler<DropEventArgs>? Drop;

    /// <summary>
    /// Initializes the drag drop service for the specified window.
    /// </summary>
    public void Initialize(nint display, nint window)
    {
        _display = display;
        _window = window;

        InitializeAtoms();
        SetXdndAware();
    }

    private void InitializeAtoms()
    {
        _xdndAware = XInternAtom(_display, "XdndAware", false);
        _xdndEnter = XInternAtom(_display, "XdndEnter", false);
        _xdndPosition = XInternAtom(_display, "XdndPosition", false);
        _xdndStatus = XInternAtom(_display, "XdndStatus", false);
        _xdndLeave = XInternAtom(_display, "XdndLeave", false);
        _xdndDrop = XInternAtom(_display, "XdndDrop", false);
        _xdndFinished = XInternAtom(_display, "XdndFinished", false);
        _xdndSelection = XInternAtom(_display, "XdndSelection", false);
        _xdndActionCopy = XInternAtom(_display, "XdndActionCopy", false);
        _xdndActionMove = XInternAtom(_display, "XdndActionMove", false);
        _xdndActionLink = XInternAtom(_display, "XdndActionLink", false);
        _xdndTypeList = XInternAtom(_display, "XdndTypeList", false);

        _textPlain = XInternAtom(_display, "text/plain", false);
        _textPlainUtf8 = XInternAtom(_display, "text/plain;charset=utf-8", false);
        _textUri = XInternAtom(_display, "text/uri-list", false);
        _utf8String = XInternAtom(_display, "UTF8_STRING", false);
        _applicationOctetStream = XInternAtom(_display, "application/octet-stream", false);
        _targets = XInternAtom(_display, "TARGETS", false);
        _xdndProxy = XInternAtom(_display, "XdndProxy", false);
        _incr = XInternAtom(_display, "INCR", false);
    }

    private void SetXdndAware()
    {
        // Set XdndAware property to indicate we support XDND version 5.
        // Format-32 property data is passed to Xlib as C longs (8 bytes on
        // x64), so the buffer must be a long, not an int.
        long version = 5;
        XChangeProperty(_display, _window, _xdndAware, XA_ATOM, 32,
            PropModeReplace, ref version, 1);
    }

    /// <summary>
    /// Processes an X11 client message for drag and drop.
    /// </summary>
    public bool ProcessClientMessage(nint messageType, nint[] data)
    {
        if (messageType == _xdndEnter)
        {
            return HandleXdndEnter(data);
        }
        else if (messageType == _xdndPosition)
        {
            return HandleXdndPosition(data);
        }
        else if (messageType == _xdndLeave)
        {
            return HandleXdndLeave(data);
        }
        else if (messageType == _xdndDrop)
        {
            return HandleXdndDrop(data);
        }
        else if (messageType == _xdndStatus)
        {
            return HandleXdndStatus(data);
        }
        else if (messageType == _xdndFinished)
        {
            return HandleXdndFinishedReply(data);
        }

        return false;
    }

    private bool HandleXdndEnter(nint[] data)
    {
        _dragSource = data[0];
        int version = (int)((data[1] >> 24) & 0xFF);
        bool hasTypeList = (data[1] & 1) != 0;

        var types = new List<nint>();

        if (hasTypeList)
        {
            // Get types from XdndTypeList property
            types = GetTypeList(_dragSource);
        }
        else
        {
            // Types are in the message
            for (int i = 2; i < 5; i++)
            {
                if (data[i] != IntPtr.Zero)
                {
                    types.Add(data[i]);
                }
            }
        }

        _currentDragData = new DragData
        {
            SourceWindow = _dragSource,
            SupportedTypes = types.ToArray()
        };

        DragEnter?.Invoke(this, new DragEventArgs(_currentDragData, 0, 0));
        return true;
    }

    private bool HandleXdndPosition(nint[] data)
    {
        if (_currentDragData == null) return false;

        int x = (int)((data[2] >> 16) & 0xFFFF);
        int y = (int)(data[2] & 0xFFFF);
        nint action = data[4];

        var eventArgs = new DragEventArgs(_currentDragData, x, y)
        {
            AllowedAction = GetDragAction(action)
        };

        DragOver?.Invoke(this, eventArgs);

        // Send XdndStatus reply
        SendXdndStatus(eventArgs.Accepted, eventArgs.AcceptedAction);

        return true;
    }

    private bool HandleXdndLeave(nint[] data)
    {
        // Defensive: a leave while a drop transfer is pending means the source
        // bailed — abort the transfer quietly (the source is gone, so there is
        // nobody to send XdndFinished to).
        _dropPending = false;
        _incrActive = false;
        _incrBuffer = null;

        _currentDragData = null;
        _dragSource = IntPtr.Zero;
        DragLeave?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private bool HandleXdndDrop(nint[] data)
    {
        if (_currentDragData == null) return false;

        uint timestamp = (uint)data[2];

        // Pick the preferred target: uri-list first (file drops from file
        // managers), then UTF8_STRING, then text/plain.
        _dropTargetType = 0;
        foreach (var preferred in new[] { _textUri, _utf8String, _textPlain })
        {
            foreach (var offered in _currentDragData.SupportedTypes)
            {
                if (offered == preferred) { _dropTargetType = preferred; break; }
            }
            if (_dropTargetType != 0) break;
        }
        if (_dropTargetType == 0) _dropTargetType = _textPlain;

        // Start the asynchronous transfer: ask the source to convert the
        // XdndSelection into a property on our window. The reply arrives as a
        // SelectionNotify event (routed in by X11Window), possibly followed by
        // INCR chunks via PropertyNotify. Drop is raised and XdndFinished sent
        // only when the transfer completes; CheckPendingDropTimeout (called
        // from the event pump) aborts a source that never answers.
        XConvertSelection(_display, _xdndSelection, _dropTargetType, _xdndSelection, _window, timestamp);
        XFlush(_display);

        _dropPending = true;
        _incrActive = false;
        _incrBuffer = null;
        _dropDeadline = Environment.TickCount64 + DropTimeoutMs;

        return true;
    }

    /// <summary>
    /// Routed from the X11 window's event loop when a SelectionNotify arrives.
    /// Completes a pending XDND drop transfer (or begins INCR chunking).
    /// Returns false when the event is not ours.
    /// </summary>
    public bool ProcessSelectionNotify(nint selection, nint target, nint property)
    {
        if (!_dropPending || selection != _xdndSelection) return false;

        if (property == IntPtr.Zero)
        {
            // Source refused the conversion.
            CompleteDropTransfer(null);
            return true;
        }

        var bytes = ReadPropertyBytes(property, out var actualType);
        if (actualType == _incr)
        {
            // Large transfer. Reading with delete=true removed the INCR
            // property, which signals the source to start sending chunks;
            // each chunk arrives as PropertyNotify(NewValue) on our window.
            _incrActive = true;
            _incrBuffer = new MemoryStream();
            _dropDeadline = Environment.TickCount64 + DropTimeoutMs;
            return true;
        }

        CompleteDropTransfer(bytes);
        return true;
    }

    /// <summary>
    /// Routed from the X11 window's event loop on PropertyNotify. Consumes
    /// INCR chunks for a pending drop transfer; a zero-length chunk ends the
    /// transfer. Returns false when the event is not ours.
    /// </summary>
    public bool ProcessPropertyNotify(nint atom, int state)
    {
        if (!_dropPending || !_incrActive) return false;
        if (atom != _xdndSelection || state != PropertyNewValue) return false;

        var chunk = ReadPropertyBytes(_xdndSelection, out _);
        if (chunk == null || chunk.Length == 0)
        {
            // Zero-length chunk = end of INCR transfer.
            CompleteDropTransfer(_incrBuffer?.ToArray());
            return true;
        }

        _incrBuffer!.Write(chunk, 0, chunk.Length);
        // Keep extending the deadline while the source is making progress.
        _dropDeadline = Environment.TickCount64 + DropTimeoutMs;
        return true;
    }

    /// <summary>
    /// Called from the X11 event pump each iteration. Receive side: aborts a
    /// pending drop transfer whose source stopped responding, sending an
    /// honest XdndFinished(accepted=false) so a Move-drag source keeps its
    /// data. Source side: releases a drag whose target never acknowledged our
    /// XdndDrop with XdndFinished.
    /// </summary>
    public void CheckPendingDropTimeout()
    {
        if (_dropPending && Environment.TickCount64 >= _dropDeadline)
            CompleteDropTransfer(null);

        if (_sourceDropSent && Environment.TickCount64 >= _sourceFinishDeadline)
            CleanupSourceDrag(releaseSelection: true);
    }

    private void CompleteDropTransfer(byte[]? bytes)
    {
        _dropPending = false;
        _incrActive = false;
        _incrBuffer = null;

        string? text = bytes is { Length: > 0 } ? Encoding.UTF8.GetString(bytes) : null;

        var dragData = _currentDragData ?? new DragData();
        if (text != null)
        {
            dragData.Text = text;
            if (_dropTargetType == _textUri)
            {
                var paths = ParseUriList(text);
                if (paths.Length > 0) dragData.FilePaths = paths;
            }
        }

        var eventArgs = new DropEventArgs(dragData, text);
        Drop?.Invoke(this, eventArgs);

        // Honest finish: report accepted only when data actually arrived —
        // claiming success on a failed transfer would let a Move-drag source
        // delete data our consumer never received. (We only ever reply with
        // the Copy action, so accepting received data is always safe.)
        SendXdndFinished(text != null);

        _currentDragData = null;
        _dragSource = IntPtr.Zero;
        _isDragging = false;
    }

    /// <summary>
    /// Read (and delete) a property from our window. Returns the raw bytes and
    /// the property's actual type atom, or null on failure. Deleting an INCR
    /// property is what tells the source to begin/continue chunking.
    /// </summary>
    private byte[]? ReadPropertyBytes(nint property, out nint actualType)
    {
        actualType = 0;
        int rc = XGetWindowProperty(_display, _window, property, 0, 0x1FFFFFFF, true,
            AnyPropertyType, out actualType, out int actualFormat, out nint nitems,
            out _, out nint data);
        if (rc != 0) return null;

        try
        {
            if (data == IntPtr.Zero) return Array.Empty<byte>();
            // Format-32 items are returned as C longs (8 bytes) on 64-bit.
            int byteCount = actualFormat switch
            {
                8 => (int)nitems,
                16 => (int)nitems * 2,
                32 => (int)nitems * IntPtr.Size,
                _ => 0,
            };
            var bytes = new byte[byteCount];
            if (byteCount > 0) Marshal.Copy(data, bytes, 0, byteCount);
            return bytes;
        }
        finally
        {
            if (data != IntPtr.Zero) XFree(data);
        }
    }

    // text/uri-list per RFC 2483: newline-separated URIs, '#' lines are
    // comments. Decodes file:// URIs to plain paths (same logic as the
    // Wayland DnD path in WaylandWindow.DragDrop.cs).
    private static string[] ParseUriList(string text)
    {
        var paths = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("file://", StringComparison.Ordinal))
            {
                var path = Uri.UnescapeDataString(line.Substring("file://".Length));
                // Strip leading host (file://host/path → /path)
                var slash = path.IndexOf('/');
                if (slash > 0) path = path.Substring(slash);
                paths.Add(path);
            }
        }
        return paths.ToArray();
    }

    private List<nint> GetTypeList(nint window)
    {
        var types = new List<nint>();

        nint actualType;
        int actualFormat;
        nint nitems, bytesAfter;
        nint data;

        int result = XGetWindowProperty(_display, window, _xdndTypeList, 0, 1024, false,
            XA_ATOM, out actualType, out actualFormat, out nitems, out bytesAfter, out data);

        if (result == 0 && data != IntPtr.Zero)
        {
            for (int i = 0; i < (int)nitems; i++)
            {
                nint atom = Marshal.ReadIntPtr(data, i * IntPtr.Size);
                types.Add(atom);
            }
            XFree(data);
        }

        return types;
    }

    private void SendXdndStatus(bool accepted, DragAction action)
    {
        if (_dragSource == IntPtr.Zero) return; // source already gone (BadWindow guard)
        var ev = new XClientMessageEvent
        {
            type = ClientMessage,
            window = _dragSource,
            message_type = _xdndStatus,
            format = 32
        };

        ev.data0 = _window;
        ev.data1 = accepted ? 1 : 0;
        ev.data2 = 0; // x, y of rectangle
        ev.data3 = 0; // width, height of rectangle
        ev.data4 = GetActionAtom(action);

        XSendEvent(_display, _dragSource, false, 0, ref ev);
        XFlush(_display);
    }

    private void SendXdndFinished(bool accepted)
    {
        if (_dragSource == IntPtr.Zero) return; // source already gone (BadWindow guard)
        var ev = new XClientMessageEvent
        {
            type = ClientMessage,
            window = _dragSource,
            message_type = _xdndFinished,
            format = 32
        };

        ev.data0 = _window;
        ev.data1 = accepted ? 1 : 0;
        ev.data2 = accepted ? _xdndActionCopy : IntPtr.Zero;

        XSendEvent(_display, _dragSource, false, 0, ref ev);
        XFlush(_display);
    }

    private DragAction GetDragAction(nint atom)
    {
        if (atom == _xdndActionCopy) return DragAction.Copy;
        if (atom == _xdndActionMove) return DragAction.Move;
        if (atom == _xdndActionLink) return DragAction.Link;
        return DragAction.None;
    }

    private nint GetActionAtom(DragAction action)
    {
        return action switch
        {
            DragAction.Copy => _xdndActionCopy,
            DragAction.Move => _xdndActionMove,
            DragAction.Link => _xdndActionLink,
            _ => IntPtr.Zero
        };
    }

    #region Outgoing XDND (source side)

    // Source-side drag state — kept strictly separate from the receive-side
    // (_dropPending / _incr*) fields above so acting as both source and target
    // (a self-drag) works. All driven by the same central event pump: X11Window
    // routes MotionNotify/ButtonRelease/KeyPress, XdndStatus/XdndFinished
    // ClientMessages, SelectionRequest and SelectionClear into the methods
    // below; no nested event loops.
    private bool _sourceDragActive;
    private string? _sourceDragText;
    private nint _sourceTarget;          // current XdndAware toplevel under the pointer
    private int _sourceTargetVersion;
    private bool _sourceTargetAccepts;
    private bool _sourceDropSent;
    private long _sourceFinishDeadline;  // Environment.TickCount64, armed after XdndDrop
    private ulong _lastInputTime;        // most recent X input event timestamp

    private const int XdndVersion = 5;
    private const ulong XK_Escape = 0xFF1B;

    /// <summary>
    /// Most recent input-event timestamp, maintained by X11Window's event
    /// loop. Selection ownership, pointer grabs, and XdndDrop all require a
    /// real (non-CurrentTime) event timestamp.
    /// </summary>
    internal void NoteInputTime(ulong time) => _lastInputTime = time;

    /// <summary>
    /// Backend-agnostic entry point: begin a drag-and-drop carrying
    /// <paramref name="text"/>. Routes to the native Wayland data-device drag
    /// when a Wayland window is active (see
    /// <see cref="Microsoft.Maui.Platform.Linux.Window.WaylandWindow.TryStartDrag"/>),
    /// otherwise to the X11 XDND source implementation. Returns false when no
    /// backend can start a drag (no recent pointer press, backend not wired,
    /// or a drag already in flight).
    /// </summary>
    public bool TryStartDrag(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (Microsoft.Maui.Platform.Linux.Window.WaylandWindow.TryStartDrag(text)) return true;
        return TryStartX11Drag(text);
    }

    private bool TryStartX11Drag(string text)
    {
        if (_display == IntPtr.Zero || _window == IntPtr.Zero) return false; // X11 backend not wired
        if (_sourceDragActive) return false;

        // Own the XDND selection — the drop target pulls the payload from us
        // via SelectionRequest / SelectionNotify (see ProcessSelectionRequest).
        XSetSelectionOwner(_display, _xdndSelection, _window, _lastInputTime);
        if (XGetSelectionOwner(_display, _xdndSelection) != _window)
            return false; // refused (stale timestamp)

        // Grab the pointer so motion/release keep arriving once the pointer
        // leaves our window.
        int grab = XGrabPointer(_display, _window, false,
            (uint)(ButtonReleaseMask | PointerMotionMask),
            GrabModeAsync, GrabModeAsync, IntPtr.Zero, IntPtr.Zero, _lastInputTime);
        if (grab != GrabSuccess)
        {
            XSetSelectionOwner(_display, _xdndSelection, IntPtr.Zero, _lastInputTime);
            return false;
        }

        _sourceDragActive = true;
        _sourceDragText = text;
        _sourceTarget = IntPtr.Zero;
        _sourceTargetVersion = 0;
        _sourceTargetAccepts = false;
        _sourceDropSent = false;
        _isDragging = true;
        XFlush(_display);
        return true;
    }

    /// <summary>
    /// Routed from X11Window on MotionNotify. Tracks the XdndAware window
    /// under the pointer, sending Enter/Leave on target change and Position
    /// on every move. Returns true while a source drag consumes the motion.
    /// </summary>
    public bool ProcessSourceMotion(int xRoot, int yRoot, ulong time)
    {
        _lastInputTime = time;
        if (!_sourceDragActive) return false;
        if (_sourceDropSent) return true; // drop in flight; swallow trailing motion

        var target = FindXdndAwareTarget(xRoot, yRoot, out int version);
        if (target != _sourceTarget)
        {
            if (_sourceTarget != IntPtr.Zero)
                SendXdndSourceMessage(_sourceTarget, _xdndLeave, _window, 0, 0, 0, 0);

            _sourceTarget = target;
            _sourceTargetVersion = Math.Min(version, XdndVersion);
            _sourceTargetAccepts = false;

            if (target != IntPtr.Zero)
            {
                // XdndEnter: l[1] carries the protocol version in the top byte;
                // bit 0 clear = the (≤3) targets are in l[2..4], no XdndTypeList.
                long flags = (long)_sourceTargetVersion << 24;
                SendXdndSourceMessage(target, _xdndEnter, _window, flags,
                    _utf8String, _textPlainUtf8, _textPlain);
            }
        }

        if (_sourceTarget != IntPtr.Zero)
        {
            SendXdndSourceMessage(_sourceTarget, _xdndPosition, _window, 0,
                ((long)(xRoot & 0xFFFF) << 16) | (uint)(yRoot & 0xFFFF),
                (long)time, _xdndActionCopy);
        }
        return true;
    }

    /// <summary>
    /// Routed from X11Window on ButtonRelease. Ends the source drag: sends
    /// XdndDrop when the current target accepts, XdndLeave otherwise; the
    /// pointer grab is released either way. Returns true when consumed.
    /// </summary>
    public bool ProcessSourceButtonRelease(ulong time)
    {
        _lastInputTime = time;
        if (!_sourceDragActive) return false;
        if (_sourceDropSent) return true; // already dropped; swallow

        XUngrabPointer(_display, time);

        if (_sourceTarget != IntPtr.Zero && _sourceTargetAccepts)
        {
            SendXdndSourceMessage(_sourceTarget, _xdndDrop, _window, 0, (long)time, 0, 0);
            _sourceDropSent = true;
            // Keep selection ownership: the target now converts XdndSelection
            // (answered in ProcessSelectionRequest). Cleanup happens on its
            // XdndFinished — or the timeout in CheckPendingDropTimeout.
            _sourceFinishDeadline = Environment.TickCount64 + DropTimeoutMs;
            XFlush(_display);
        }
        else
        {
            if (_sourceTarget != IntPtr.Zero)
                SendXdndSourceMessage(_sourceTarget, _xdndLeave, _window, 0, 0, 0, 0);
            CleanupSourceDrag(releaseSelection: true);
        }
        return true;
    }

    /// <summary>
    /// Routed from X11Window on KeyPress (keysym-resolved). Escape cancels an
    /// active source drag. Returns true when the key was consumed.
    /// </summary>
    public bool ProcessSourceKey(ulong keysym)
    {
        if (!_sourceDragActive || _sourceDropSent) return false;
        if (keysym != XK_Escape) return false;

        if (_sourceTarget != IntPtr.Zero)
            SendXdndSourceMessage(_sourceTarget, _xdndLeave, _window, 0, 0, 0, 0);
        XUngrabPointer(_display, _lastInputTime);
        CleanupSourceDrag(releaseSelection: true);
        return true;
    }

    // Target's reply to our XdndPosition: bit 0 of l[1] = "will accept a drop".
    private bool HandleXdndStatus(nint[] data)
    {
        if (!_sourceDragActive) return false;
        if (data[0] != _sourceTarget) return true; // stale reply from a previous target
        _sourceTargetAccepts = ((long)data[1] & 1) != 0;
        return true;
    }

    // Target finished processing our XdndDrop — release drag state and the selection.
    private bool HandleXdndFinishedReply(nint[] data)
    {
        if (!_sourceDragActive || !_sourceDropSent) return false;
        if (data[0] != _sourceTarget) return true;
        CleanupSourceDrag(releaseSelection: true);
        return true;
    }

    /// <summary>
    /// Routed from X11Window on SelectionRequest — the drop target pulling the
    /// dragged payload. Converts to the requested text target (or TARGETS) and
    /// replies with SelectionNotify; property None signals an unsupported
    /// target. Returns false when the request isn't for the XDND selection.
    /// </summary>
    public bool ProcessSelectionRequest(nint requestor, nint selection, nint target, nint property, nint time)
    {
        if (selection != _xdndSelection || _sourceDragText == null) return false;

        // Obsolete clients may pass property None; the convention is to fall
        // back to the target atom as the property name.
        if (property == IntPtr.Zero) property = target;

        bool converted = false;
        if (target == _targets)
        {
            // TARGETS introspection: the same three text targets XdndEnter offered.
            var atoms = new long[] { _utf8String, _textPlainUtf8, _textPlain };
            XChangePropertyAtoms(_display, requestor, property, XA_ATOM, 32,
                PropModeReplace, atoms, atoms.Length);
            converted = true;
        }
        else if (target == _utf8String || target == _textPlainUtf8 || target == _textPlain)
        {
            // TODO: outgoing INCR for payloads beyond the X max-request size.
            // Our text drags are small, so a single ChangeProperty suffices.
            var bytes = Encoding.UTF8.GetBytes(_sourceDragText);
            XChangePropertyBytes(_display, requestor, property, target, 8,
                PropModeReplace, bytes, bytes.Length);
            converted = true;
        }

        var reply = new XSelectionNotifyEvent
        {
            type = SelectionNotifyCode,
            display = _display,
            requestor = requestor,
            selection = selection,
            target = target,
            property = converted ? property : IntPtr.Zero,
            time = time,
        };
        XSendEvent(_display, requestor, false, 0, ref reply);
        XFlush(_display);
        return true;
    }

    /// <summary>
    /// Routed from X11Window on SelectionClear — another client took the XDND
    /// selection, which cancels any drag we were sourcing.
    /// </summary>
    public bool ProcessSelectionClear(nint selection)
    {
        if (selection != _xdndSelection || !_sourceDragActive) return false;

        if (!_sourceDropSent && _sourceTarget != IntPtr.Zero)
            SendXdndSourceMessage(_sourceTarget, _xdndLeave, _window, 0, 0, 0, 0);
        XUngrabPointer(_display, _lastInputTime);
        CleanupSourceDrag(releaseSelection: false); // ownership already gone
        return true;
    }

    private void CleanupSourceDrag(bool releaseSelection)
    {
        if (!_sourceDragActive) return;
        _sourceDragActive = false;
        _sourceDropSent = false;
        _sourceTarget = IntPtr.Zero;
        _sourceTargetVersion = 0;
        _sourceTargetAccepts = false;
        _sourceDragText = null;
        _isDragging = false;
        if (releaseSelection)
            XSetSelectionOwner(_display, _xdndSelection, IntPtr.Zero, _lastInputTime);
        XFlush(_display);
    }

    /// <summary>
    /// Walk the window tree under the given root coordinates and return the
    /// deepest window advertising XdndAware (the property lives on toplevels,
    /// so the walk usually resolves within a couple of hops). Honors a trivial
    /// XdndProxy — a WINDOW property redirecting messages to another window;
    /// the spec's full validity check (the proxy naming itself) is skipped, as
    /// no mainstream toolkit ships non-trivial proxies.
    /// </summary>
    private nint FindXdndAwareTarget(int xRoot, int yRoot, out int version)
    {
        version = 0;
        var root = XDefaultRootWindow(_display);
        nint current = root;
        nint aware = IntPtr.Zero;

        for (int depth = 0; depth < 64; depth++)
        {
            var candidate = ResolveXdndProxy(current);
            int v = (int)ReadWindowPropertyLong(candidate, _xdndAware);
            if (v > 0)
            {
                aware = candidate;
                version = v;
            }

            if (!XTranslateCoordinates(_display, root, current, xRoot, yRoot,
                    out _, out _, out nint child) || child == IntPtr.Zero)
                break;
            current = child;
        }

        return aware;
    }

    private nint ResolveXdndProxy(nint window)
    {
        if (window == IntPtr.Zero) return window;
        var proxy = (nint)ReadWindowPropertyLong(window, _xdndProxy);
        return proxy != IntPtr.Zero ? proxy : window;
    }

    // Read the first format-32 item of a property (returned as a C long); 0
    // when the property is absent or malformed.
    private long ReadWindowPropertyLong(nint window, nint property)
    {
        if (window == IntPtr.Zero) return 0;
        if (XGetWindowProperty(_display, window, property, 0, 1, false, AnyPropertyType,
                out _, out int format, out nint nitems, out _, out nint data) != 0)
            return 0;
        try
        {
            if (data == IntPtr.Zero || nitems == 0 || format != 32) return 0;
            return Marshal.ReadInt64(data);
        }
        finally
        {
            if (data != IntPtr.Zero) XFree(data);
        }
    }

    private void SendXdndSourceMessage(nint dest, nint messageType, long d0, long d1, long d2, long d3, long d4)
    {
        var ev = new XClientMessageEvent
        {
            type = ClientMessage,
            window = dest,
            message_type = messageType,
            format = 32,
            data0 = (nint)d0,
            data1 = (nint)d1,
            data2 = (nint)d2,
            data3 = (nint)d3,
            data4 = (nint)d4,
        };
        XSendEvent(_display, dest, false, 0, ref ev);
        XFlush(_display);
    }

    #endregion

    /// <summary>
    /// Starts a drag operation carrying the payload's text. Kept for API
    /// compatibility — routes to the backend-agnostic
    /// <see cref="TryStartDrag(string)"/>.
    /// </summary>
    public void StartDrag(DragData data)
    {
        if (_isDragging) return;
        if (data.Text is { Length: > 0 } text)
            TryStartDrag(text);
        // No text payload → nothing either backend can offer yet.
    }

    /// <summary>
    /// Cancels the current drag operation.
    /// </summary>
    public void CancelDrag()
    {
        if (_sourceDragActive && !_sourceDropSent)
        {
            if (_sourceTarget != IntPtr.Zero)
                SendXdndSourceMessage(_sourceTarget, _xdndLeave, _window, 0, 0, 0, 0);
            XUngrabPointer(_display, _lastInputTime);
            CleanupSourceDrag(releaseSelection: true);
            return;
        }
        _isDragging = false;
        _currentDragData = null;
    }

    // ---- Backend push API ----
    // Wayland (and any future backend that doesn't go through ProcessClientMessage)
    // calls these to drive the public events. Kept internal so app code can only
    // subscribe to the events, not synthesize them.

    internal DragEventArgs RaiseDragEnter(DragData data, int x, int y)
    {
        var args = new DragEventArgs(data, x, y);
        _isDragging = true;
        _currentDragData = data;
        DragEnter?.Invoke(this, args);
        return args;
    }

    internal DragEventArgs RaiseDragOver(DragData data, int x, int y)
    {
        var args = new DragEventArgs(data, x, y);
        DragOver?.Invoke(this, args);
        return args;
    }

    internal void RaiseDragLeave()
    {
        _isDragging = false;
        _currentDragData = null;
        DragLeave?.Invoke(this, EventArgs.Empty);
    }

    internal DropEventArgs RaiseDrop(DragData data, string? text)
    {
        var args = new DropEventArgs(data, text);
        Drop?.Invoke(this, args);
        _isDragging = false;
        _currentDragData = null;
        return args;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    #region X11 Interop

    private const int ClientMessage = 33;
    private const int SelectionNotifyCode = 31;
    private const int PropModeReplace = 0;
    private const int PropertyNewValue = 0;
    private const int GrabModeAsync = 1;
    private const int GrabSuccess = 0;
    private const long ButtonReleaseMask = 1L << 3;
    private const long PointerMotionMask = 1L << 6;
    private static readonly nint XA_ATOM = (nint)4;
    private static readonly nint AnyPropertyType = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct XClientMessageEvent
    {
        public int type;
        public ulong serial;
        public bool send_event;
        public nint display;
        public nint window;
        public nint message_type;
        public int format;
        public nint data0;
        public nint data1;
        public nint data2;
        public nint data3;
        public nint data4;
    }

    [LibraryImport("libX11.so.6", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint XInternAtom(nint display, string atomName, [MarshalAs(UnmanagedType.Bool)] bool onlyIfExists);

    // Format-32 property data crosses this boundary as C longs (8 bytes on
    // x64), hence ref long rather than ref int.
    [DllImport("libX11.so.6")]
    private static extern int XChangeProperty(nint display, nint window, nint property, nint type,
        int format, int mode, ref long data, int nelements);

    [DllImport("libX11.so.6")]
    private static extern int XGetWindowProperty(nint display, nint window, nint property,
        long offset, long length, bool delete, nint reqType,
        out nint actualType, out int actualFormat, out nint nitems, out nint bytesAfter, out nint data);

    // SelectionNotify reply sent to a requestor converting our XdndSelection.
    [StructLayout(LayoutKind.Sequential)]
    private struct XSelectionNotifyEvent
    {
        public int type;
        public ulong serial;
        public bool send_event;
        public nint display;
        public nint requestor;
        public nint selection;
        public nint target;
        public nint property;
        public nint time;
    }

    [DllImport("libX11.so.6")]
    private static extern int XSendEvent(nint display, nint window, bool propagate, long eventMask, ref XClientMessageEvent xevent);

    [DllImport("libX11.so.6", EntryPoint = "XSendEvent")]
    private static extern int XSendEvent(nint display, nint window, bool propagate, long eventMask, ref XSelectionNotifyEvent xevent);

    // Same-entry-point overloads of XChangeProperty for the different payload
    // shapes. Format-32 data crosses as C longs (8 bytes on x64).
    [DllImport("libX11.so.6", EntryPoint = "XChangeProperty")]
    private static extern int XChangePropertyBytes(nint display, nint window, nint property, nint type,
        int format, int mode, byte[] data, int nelements);

    [DllImport("libX11.so.6", EntryPoint = "XChangeProperty")]
    private static extern int XChangePropertyAtoms(nint display, nint window, nint property, nint type,
        int format, int mode, long[] data, int nelements);

    [LibraryImport("libX11.so.6")]
    private static partial int XSetSelectionOwner(nint display, nint selection, nint owner, ulong time);

    [LibraryImport("libX11.so.6")]
    private static partial nint XGetSelectionOwner(nint display, nint selection);

    [LibraryImport("libX11.so.6")]
    private static partial int XGrabPointer(nint display, nint grabWindow,
        [MarshalAs(UnmanagedType.Bool)] bool ownerEvents, uint eventMask,
        int pointerMode, int keyboardMode, nint confineTo, nint cursor, ulong time);

    [LibraryImport("libX11.so.6")]
    private static partial int XUngrabPointer(nint display, ulong time);

    [LibraryImport("libX11.so.6")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool XTranslateCoordinates(nint display, nint srcWindow, nint destWindow,
        int srcX, int srcY, out int destX, out int destY, out nint child);

    [LibraryImport("libX11.so.6")]
    private static partial nint XDefaultRootWindow(nint display);

    [LibraryImport("libX11.so.6")]
    private static partial int XConvertSelection(nint display, nint selection, nint target, nint property, nint requestor, uint time);

    [LibraryImport("libX11.so.6")]
    private static partial void XFree(nint ptr);

    [LibraryImport("libX11.so.6")]
    private static partial void XFlush(nint display);

    #endregion
}
