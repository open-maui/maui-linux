// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Window;

// Native Wayland clipboard implementation. Replaces the wl-copy / wl-paste
// subprocess fallback in ClipboardService when the compositor exposes
// wl_data_device_manager (every modern Wayland compositor does).
//
// Wire protocol:
//   wl_data_device_manager  → bound in registry handler (WaylandWindow.cs)
//   wl_data_device          → per-seat, created in SetupClipboard
//     events: data_offer → new wl_data_offer; selection → current clipboard
//   wl_data_offer           → represents data the compositor is offering us
//     events: offer(mime_type) → list of MIMEs the source supports
//     request: receive(mime, fd) → compositor writes to fd, we read
//   wl_data_source          → represents data WE are offering
//     request: offer(mime) → declare a supported MIME
//     event: send(mime, fd) → external app pasted; write our text to fd
//     event: cancelled → we lost the selection (someone else copied); drop source
//
// The serial passed to set_selection MUST be a recent input serial. We use the
// _pointerSerial captured in OnPointerButton (Press), falling back to the
// last keyboard serial. A stale serial from pointer-enter makes the compositor
// silently ignore the selection request.
public partial class WaylandWindow
{
    #region wl_data_device_manager protocol constants

    private const uint WL_DATA_DEVICE_MANAGER_CREATE_DATA_SOURCE = 0;
    private const uint WL_DATA_DEVICE_MANAGER_GET_DATA_DEVICE = 1;

    private const uint WL_DATA_DEVICE_SET_SELECTION = 1;
    private const uint WL_DATA_DEVICE_RELEASE = 2;

    private const uint WL_DATA_SOURCE_OFFER = 0;
    private const uint WL_DATA_SOURCE_DESTROY = 1;

    private const uint WL_DATA_OFFER_RECEIVE = 1;
    private const uint WL_DATA_OFFER_DESTROY = 2;

    // MIME types we negotiate. Preferred-first order — the paste path picks the
    // first MIME the source advertises that matches one of these.
    private static readonly string[] s_textMimeTypes =
    {
        "text/plain;charset=utf-8",
        "text/plain",
        "UTF8_STRING",
        "STRING",
        "TEXT",
    };

    #endregion

    #region State

    private IntPtr _dataDeviceManager;
    private IntPtr _dataDevice;

    // Most-recently-received offer from a wl_data_device.selection event.
    // Cleared and destroyed when a new selection event arrives (compositor
    // is responsible for the previous offer's lifetime — we destroy it
    // explicitly when superseded).
    private IntPtr _currentClipboardOffer;

    // Per-offer MIME accumulator. Each data_offer event creates a new entry;
    // offer.offer events append to the entry for that offer; selection() picks
    // the relevant entry into _currentOfferMimes when the offer is promoted to
    // the active clipboard selection. Keeping the dict means DnD offers (which
    // share the data_offer event but arrive via enter() instead of selection())
    // don't pollute clipboard state.
    private readonly Dictionary<IntPtr, List<string>> _offerMimesByOffer = new();
    private List<string> _currentOfferMimes = new();

    // wl_data_source we created via SetClipboardText. Multiple may be alive at
    // once when the user copies repeatedly — Wayland requires the source to stay
    // valid until the compositor fires cancelled() on it, which happens whenever
    // someone (including us) takes the selection. Destroying a source while it
    // still owns the selection causes the compositor to fire selection(null),
    // clearing the clipboard.
    //
    // _ownedDataSource is the *most-recent* source — the one to consult for
    // self-paste short-circuits. _ownedSourceTexts maps every live source ptr
    // to its text so OnDataSourceSend can write the right bytes even for
    // older sources whose cancelled event hasn't fired yet.
    private IntPtr _ownedDataSource;
    private readonly Dictionary<IntPtr, string> _ownedSourceTexts = new();

    // Listener storage (keep delegates pinned for the lifetime of the proxy).
    private WlDataDeviceListener _dataDeviceListener;
    private GCHandle _dataDeviceListenerHandle;
    // Each wl_data_offer needs its own per-instance listener; we allocate on
    // data_offer and free on either replacement or destroy.
    private readonly Dictionary<IntPtr, GCHandle> _offerListenerHandles = new();
    // Same for outgoing data sources — keyed by the source proxy ptr.
    private readonly Dictionary<IntPtr, GCHandle> _sourceListenerHandles = new();

    #endregion

    #region Delegate types and listener structs

    // wl_data_device events: data_offer(new_id), enter(...), leave(), motion(...),
    // drop(), selection(?wl_data_offer).
    private delegate void WlDataDeviceDataOfferDelegate(IntPtr data, IntPtr device, IntPtr offer);
    private delegate void WlDataDeviceEnterDelegate(IntPtr data, IntPtr device, uint serial, IntPtr surface, int x, int y, IntPtr offer);
    private delegate void WlDataDeviceLeaveDelegate(IntPtr data, IntPtr device);
    private delegate void WlDataDeviceMotionDelegate(IntPtr data, IntPtr device, uint time, int x, int y);
    private delegate void WlDataDeviceDropDelegate(IntPtr data, IntPtr device);
    private delegate void WlDataDeviceSelectionDelegate(IntPtr data, IntPtr device, IntPtr offer);

    [StructLayout(LayoutKind.Sequential)]
    private struct WlDataDeviceListener
    {
        public IntPtr DataOffer;
        public IntPtr Enter;
        public IntPtr Leave;
        public IntPtr Motion;
        public IntPtr Drop;
        public IntPtr Selection;
    }

    // wl_data_offer events: offer(mime), source_actions(uint), action(uint).
    private delegate void WlDataOfferOfferDelegate(IntPtr data, IntPtr offer, IntPtr mimePtr);
    private delegate void WlDataOfferSourceActionsDelegate(IntPtr data, IntPtr offer, uint actions);
    private delegate void WlDataOfferActionDelegate(IntPtr data, IntPtr offer, uint dndAction);

    [StructLayout(LayoutKind.Sequential)]
    private struct WlDataOfferListener
    {
        public IntPtr Offer;
        public IntPtr SourceActions;
        public IntPtr Action;
    }

    // wl_data_source events: target(?mime), send(mime, fd), cancelled(),
    // dnd_drop_performed(), dnd_finished(), action(uint).
    private delegate void WlDataSourceTargetDelegate(IntPtr data, IntPtr source, IntPtr mimePtr);
    private delegate void WlDataSourceSendDelegate(IntPtr data, IntPtr source, IntPtr mimePtr, int fd);
    private delegate void WlDataSourceCancelledDelegate(IntPtr data, IntPtr source);
    private delegate void WlDataSourceDndDelegate(IntPtr data, IntPtr source);
    private delegate void WlDataSourceActionDelegate(IntPtr data, IntPtr source, uint action);

    [StructLayout(LayoutKind.Sequential)]
    private struct WlDataSourceListener
    {
        public IntPtr Target;
        public IntPtr Send;
        public IntPtr Cancelled;
        public IntPtr DndDropPerformed;
        public IntPtr DndFinished;
        public IntPtr Action;
    }

    // Cached, pinned delegate instances (one set per WaylandWindow). Re-used
    // across each new wl_data_offer / wl_data_source so we don't churn GCHandles
    // on hot paths.
    private WlDataOfferOfferDelegate? _offerOfferDelegate;
    private WlDataOfferSourceActionsDelegate? _offerSourceActionsDelegate;
    private WlDataOfferActionDelegate? _offerActionDelegate;
    private WlDataOfferListener _offerListenerTemplate;

    private WlDataSourceTargetDelegate? _sourceTargetDelegate;
    private WlDataSourceSendDelegate? _sourceSendDelegate;
    private WlDataSourceCancelledDelegate? _sourceCancelledDelegate;
    private WlDataSourceDndDelegate? _sourceDndDropPerformedDelegate;
    private WlDataSourceDndDelegate? _sourceDndFinishedDelegate;
    private WlDataSourceActionDelegate? _sourceActionDelegate;
    private WlDataSourceListener _sourceListenerTemplate;

    #endregion

    #region libc bindings (pipe / read / write / close / fcntl)

    [DllImport("libc", EntryPoint = "pipe", SetLastError = true)]
    private static extern int libc_pipe(int[] fds);

    [DllImport("libc", EntryPoint = "read", SetLastError = true)]
    private static extern nint libc_read(int fd, byte[] buf, nuint count);

    [DllImport("libc", EntryPoint = "write", SetLastError = true)]
    private static extern nint libc_write(int fd, byte[] buf, nuint count);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int libc_close(int fd);

    [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int libc_fcntl(int fd, int cmd, int arg);

    private const int F_SETFL = 4;
    private const int O_NONBLOCK = 0x800;

    #endregion

    #region Setup / teardown

    /// <summary>
    /// Wire the per-seat wl_data_device. Safe to call multiple times — guarded
    /// so it only runs once both wl_data_device_manager and wl_seat are present.
    /// Called from the registry handler after each candidate global binding.
    /// </summary>
    internal void SetupClipboard()
    {
        if (_dataDevice != IntPtr.Zero) return;
        if (_dataDeviceManager == IntPtr.Zero || _seat == IntPtr.Zero) return;

        // get_data_device: signature "no" (new_id, object). Use the marshal_constructor
        // form with the NULL placeholder + seat argument, same as we do for
        // xdg_surface.get_toplevel.
        _dataDevice = wl_proxy_marshal_constructor(
            _dataDeviceManager,
            WL_DATA_DEVICE_MANAGER_GET_DATA_DEVICE,
            _wl_data_device_interface,
            IntPtr.Zero,
            _seat);

        if (_dataDevice == IntPtr.Zero)
        {
            DiagnosticLog.Warn("WaylandWindow", "Failed to create wl_data_device; clipboard will fall back to wl-copy/wl-paste");
            return;
        }

        _dataDeviceListener = new WlDataDeviceListener
        {
            DataOffer = Marshal.GetFunctionPointerForDelegate<WlDataDeviceDataOfferDelegate>(OnDataDeviceDataOffer),
            Enter = Marshal.GetFunctionPointerForDelegate<WlDataDeviceEnterDelegate>(OnDataDeviceEnter),
            Leave = Marshal.GetFunctionPointerForDelegate<WlDataDeviceLeaveDelegate>(OnDataDeviceLeave),
            Motion = Marshal.GetFunctionPointerForDelegate<WlDataDeviceMotionDelegate>(OnDataDeviceMotion),
            Drop = Marshal.GetFunctionPointerForDelegate<WlDataDeviceDropDelegate>(OnDataDeviceDrop),
            Selection = Marshal.GetFunctionPointerForDelegate<WlDataDeviceSelectionDelegate>(OnDataDeviceSelection),
        };
        _dataDeviceListenerHandle = GCHandle.Alloc(_dataDeviceListener, GCHandleType.Pinned);
        wl_proxy_add_listener(_dataDevice, _dataDeviceListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));

        // Pre-build the offer/source listener templates so each new proxy can
        // reuse one set of pinned delegate function pointers.
        _offerOfferDelegate = OnDataOfferOffer;
        _offerSourceActionsDelegate = OnDataOfferSourceActions;
        _offerActionDelegate = OnDataOfferAction;
        _offerListenerTemplate = new WlDataOfferListener
        {
            Offer = Marshal.GetFunctionPointerForDelegate(_offerOfferDelegate),
            SourceActions = Marshal.GetFunctionPointerForDelegate(_offerSourceActionsDelegate),
            Action = Marshal.GetFunctionPointerForDelegate(_offerActionDelegate),
        };

        _sourceTargetDelegate = OnDataSourceTarget;
        _sourceSendDelegate = OnDataSourceSend;
        _sourceCancelledDelegate = OnDataSourceCancelled;
        _sourceDndDropPerformedDelegate = OnDataSourceDndNoOp;
        _sourceDndFinishedDelegate = OnDataSourceDndNoOp;
        _sourceActionDelegate = OnDataSourceAction;
        _sourceListenerTemplate = new WlDataSourceListener
        {
            Target = Marshal.GetFunctionPointerForDelegate(_sourceTargetDelegate),
            Send = Marshal.GetFunctionPointerForDelegate(_sourceSendDelegate),
            Cancelled = Marshal.GetFunctionPointerForDelegate(_sourceCancelledDelegate),
            DndDropPerformed = Marshal.GetFunctionPointerForDelegate(_sourceDndDropPerformedDelegate),
            DndFinished = Marshal.GetFunctionPointerForDelegate(_sourceDndFinishedDelegate),
            Action = Marshal.GetFunctionPointerForDelegate(_sourceActionDelegate),
        };

        // Register the active window as the global clipboard backend so
        // ClipboardService can route into us. Last-active wins; for single-window
        // apps this is the only window.
        s_activeClipboardWindow = this;

        DiagnosticLog.Debug("WaylandWindow", "Native wl_data_device clipboard wired");
    }

    private void DisposeClipboard()
    {
        ClearCurrentOffer();
        DropOwnedSource();

        if (_dataDevice != IntPtr.Zero)
        {
            wl_proxy_marshal(_dataDevice, WL_DATA_DEVICE_RELEASE);
            wl_proxy_destroy(_dataDevice);
            _dataDevice = IntPtr.Zero;
        }
        if (_dataDeviceListenerHandle.IsAllocated)
            _dataDeviceListenerHandle.Free();
        if (_dataDeviceManager != IntPtr.Zero)
        {
            wl_proxy_destroy(_dataDeviceManager);
            _dataDeviceManager = IntPtr.Zero;
        }
        if (s_activeClipboardWindow == this)
            s_activeClipboardWindow = null;
    }

    #endregion

    #region wl_data_device event handlers

    private static void OnDataDeviceDataOffer(IntPtr data, IntPtr device, IntPtr offer)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        // The compositor created `offer` for us automatically (event signature "n").
        // Track its MIME accumulator before any offer.offer events fire, then
        // attach the listener that populates it.
        window._offerMimesByOffer[offer] = new List<string>();

        var pinned = GCHandle.Alloc(window._offerListenerTemplate, GCHandleType.Pinned);
        window._offerListenerHandles[offer] = pinned;
        wl_proxy_add_listener(offer, pinned.AddrOfPinnedObject(), GCHandle.ToIntPtr(window._thisHandle));
    }

    // wl_data_device DnD events. Implemented in WaylandWindow.DragDrop.cs.
    // Forwarded through to per-window instance handlers.
    private static void OnDataDeviceEnter(IntPtr data, IntPtr device, uint serial, IntPtr surface, int x, int y, IntPtr offer)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        ((WaylandWindow)handle.Target!).HandleDnDEnter(serial, surface, x, y, offer);
    }
    private static void OnDataDeviceLeave(IntPtr data, IntPtr device)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        ((WaylandWindow)handle.Target!).HandleDnDLeave();
    }
    private static void OnDataDeviceMotion(IntPtr data, IntPtr device, uint time, int x, int y)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        ((WaylandWindow)handle.Target!).HandleDnDMotion(time, x, y);
    }
    private static void OnDataDeviceDrop(IntPtr data, IntPtr device)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        ((WaylandWindow)handle.Target!).HandleDnDDrop();
    }

    private static void OnDataDeviceSelection(IntPtr data, IntPtr device, IntPtr offer)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        // Compositor hands us ownership of the prior offer here — we must destroy
        // it ourselves. offer == IntPtr.Zero means the clipboard was cleared.
        window.ClearCurrentOffer();
        window._currentClipboardOffer = offer;

        // Promote the per-offer MIME list to the current selection.
        if (offer != IntPtr.Zero && window._offerMimesByOffer.TryGetValue(offer, out var mimes))
            window._currentOfferMimes = mimes;
        else
            window._currentOfferMimes = new List<string>();
    }

    private void ClearCurrentOffer()
    {
        if (_currentClipboardOffer != IntPtr.Zero)
        {
            if (_offerListenerHandles.Remove(_currentClipboardOffer, out var pinned) && pinned.IsAllocated)
                pinned.Free();
            _offerMimesByOffer.Remove(_currentClipboardOffer);
            wl_proxy_marshal(_currentClipboardOffer, WL_DATA_OFFER_DESTROY);
            wl_proxy_destroy(_currentClipboardOffer);
            _currentClipboardOffer = IntPtr.Zero;
        }
        _currentOfferMimes = new List<string>();
    }

    #endregion

    #region wl_data_offer event handlers

    private static void OnDataOfferOffer(IntPtr data, IntPtr offer, IntPtr mimePtr)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        var mime = Marshal.PtrToStringUTF8(mimePtr);
        if (string.IsNullOrEmpty(mime)) return;

        if (window._offerMimesByOffer.TryGetValue(offer, out var list))
            list.Add(mime);
    }

    private static void OnDataOfferSourceActions(IntPtr data, IntPtr offer, uint actions) { }
    private static void OnDataOfferAction(IntPtr data, IntPtr offer, uint dndAction) { }

    #endregion

    #region wl_data_source event handlers (we own the source on copy)

    private static void OnDataSourceTarget(IntPtr data, IntPtr source, IntPtr mimePtr) { }
    private static void OnDataSourceDndNoOp(IntPtr data, IntPtr source) { }
    private static void OnDataSourceAction(IntPtr data, IntPtr source, uint action) { }

    private static void OnDataSourceSend(IntPtr data, IntPtr source, IntPtr mimePtr, int fd)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) { try { libc_close(fd); } catch { } return; }
        var window = (WaylandWindow)handle.Target!;

        try
        {
            // Pull the text for THIS specific source (the latest copy may have
            // superseded it but its cancelled event hasn't fired yet — pasting
            // apps may still be reading from the older selection).
            if (!window._ownedSourceTexts.TryGetValue(source, out var text))
                text = string.Empty;
            var bytes = Encoding.UTF8.GetBytes(text);
            int offset = 0;
            while (offset < bytes.Length)
            {
                var chunk = new byte[bytes.Length - offset];
                Array.Copy(bytes, offset, chunk, 0, chunk.Length);
                var written = libc_write(fd, chunk, (nuint)chunk.Length);
                if (written <= 0) break;
                offset += (int)written;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WaylandWindow", $"OnDataSourceSend write failed: {ex.Message}");
        }
        finally
        {
            libc_close(fd);
        }
    }

    private static void OnDataSourceCancelled(IntPtr data, IntPtr source)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        window.DestroySource(source);
        if (window._ownedDataSource == source)
            window._ownedDataSource = IntPtr.Zero;
    }

    private void DestroySource(IntPtr source)
    {
        if (source == IntPtr.Zero) return;
        if (_sourceListenerHandles.Remove(source, out var pinned) && pinned.IsAllocated)
            pinned.Free();
        _ownedSourceTexts.Remove(source);
        wl_proxy_marshal(source, WL_DATA_SOURCE_DESTROY);
        wl_proxy_destroy(source);
    }

    private void DropOwnedSource()
    {
        // Destroy ALL outstanding owned sources. Called only from Dispose; during
        // normal operation we let OnDataSourceCancelled clean up individual ones.
        foreach (var ptr in _ownedSourceTexts.Keys.ToArray())
            DestroySource(ptr);
        _ownedDataSource = IntPtr.Zero;
    }

    #endregion

    #region Public API (called from ClipboardService)

    // Single-window app assumption: the most recently initialized WaylandWindow
    // backs the clipboard. Set/cleared in SetupClipboard / DisposeClipboard.
    private static WaylandWindow? s_activeClipboardWindow;

    /// <summary>
    /// Heuristic: does this offer's MIME list look like one WE published?
    /// Compositors mirror back our offered MIMEs verbatim, so if all five of
    /// our text MIME variants are present we're almost certainly looking at
    /// our own selection (no external app advertises all of UTF8_STRING +
    /// STRING + TEXT alongside the modern text/plain pair).
    /// </summary>
    private static bool SelectionLooksLikeOurs(List<string> mimes)
    {
        foreach (var m in s_textMimeTypes)
            if (!mimes.Contains(m)) return false;
        return true;
    }

    /// <summary>
    /// True when a WaylandWindow has wired its wl_data_device and is ready to
    /// service native clipboard requests. Consulted by ClipboardService before
    /// routing into the native path.
    /// </summary>
    public static bool NativeClipboardAvailable =>
        s_activeClipboardWindow != null && s_activeClipboardWindow._dataDevice != IntPtr.Zero;

    /// <summary>
    /// Set clipboard text natively via wl_data_source.
    /// Returns false when the native path is unavailable (caller falls back to wl-copy).
    /// </summary>
    public static bool TrySetClipboardText(string text)
    {
        var w = s_activeClipboardWindow;
        if (w == null || w._dataDevice == IntPtr.Zero || w._dataDeviceManager == IntPtr.Zero) return false;
        if (w._seat == IntPtr.Zero) return false;

        // create_data_source: signature "n", emits a new wl_data_source.
        var source = wl_proxy_marshal_constructor(
            w._dataDeviceManager,
            WL_DATA_DEVICE_MANAGER_CREATE_DATA_SOURCE,
            _wl_data_source_interface,
            IntPtr.Zero);
        if (source == IntPtr.Zero) return false;

        // CRITICAL: do NOT destroy the previous source here. The compositor will
        // fire wl_data_source.cancelled() on the old source when our new
        // set_selection takes effect — OnDataSourceCancelled destroys it then.
        // Destroying synchronously races with the new set_selection and the
        // compositor reacts by clearing the selection (selection: offer=0).
        w._ownedDataSource = source;
        w._ownedSourceTexts[source] = text ?? string.Empty;

        // Listener — pin a fresh copy of the template per source so each can
        // be freed independently when cancelled.
        var pinned = GCHandle.Alloc(w._sourceListenerTemplate, GCHandleType.Pinned);
        w._sourceListenerHandles[source] = pinned;
        wl_proxy_add_listener(source, pinned.AddrOfPinnedObject(), GCHandle.ToIntPtr(w._thisHandle));

        // Offer every MIME variant — pasting apps pick whichever they prefer.
        foreach (var mime in s_textMimeTypes)
            wl_proxy_marshal(source, WL_DATA_SOURCE_OFFER, mime);

        // set_selection requires a recent input serial. We use the freshest
        // input serial available — pointer or keyboard, whichever is more
        // recent. Ctrl+C is a keyboard event so the keyboard serial path
        // matters; mouse-driven copy uses the pointer path.
        var serial = Math.Max(w._pointerSerial, w._keyboardSerial);
        wl_proxy_marshal(w._dataDevice, WL_DATA_DEVICE_SET_SELECTION, source, serial);

        // Flush so the compositor sees the set_selection right away rather than
        // batching it until the next event loop iteration.
        wl_display_flush(w._display);
        return true;
    }

    /// <summary>
    /// Read clipboard text natively via wl_data_offer.receive on the current
    /// selection. Returns null if no clipboard offer is present or no text MIME
    /// matched; the caller then falls back to wl-paste.
    /// </summary>
    public static Task<string?> TryGetClipboardTextAsync()
    {
        var w = s_activeClipboardWindow;
        if (w == null) return Task.FromResult<string?>(null);

        // Self-paste short-circuit: if WE own the current clipboard (we wrote it
        // with TrySetClipboardText), reading back through the wayland pipe would
        // deadlock — the compositor needs to dispatch wl_data_source.send to us,
        // but our main thread is blocked waiting for the pipe read result. Return
        // the buffered text directly. The "we own it" check is: we have an active
        // owned source AND the current selection's MIMEs match what we offered
        // (compositor mirrors our offered MIMEs in the new selection).
        if (w._ownedDataSource != IntPtr.Zero
            && w._ownedSourceTexts.TryGetValue(w._ownedDataSource, out var ownText)
            && w._currentClipboardOffer != IntPtr.Zero
            && SelectionLooksLikeOurs(w._currentOfferMimes))
        {
            return Task.FromResult<string?>(ownText);
        }

        if (w._currentClipboardOffer == IntPtr.Zero)
            return Task.FromResult<string?>(null);

        // Find the best MIME match.
        string? chosen = null;
        foreach (var preferred in s_textMimeTypes)
        {
            if (w._currentOfferMimes.Contains(preferred))
            {
                chosen = preferred;
                break;
            }
        }
        if (chosen == null) return Task.FromResult<string?>(null);

        // Create a pipe; give the write-end to the compositor via receive(),
        // read the read-end ourselves on a background task.
        var fds = new int[2];
        if (libc_pipe(fds) != 0)
        {
            DiagnosticLog.Warn("WaylandWindow", "pipe() failed for clipboard receive");
            return Task.FromResult<string?>(null);
        }
        int readFd = fds[0], writeFd = fds[1];

        // Non-blocking read so a buggy source can't deadlock us forever.
        libc_fcntl(readFd, F_SETFL, O_NONBLOCK);

        wl_proxy_marshal_string_fd(w._currentClipboardOffer, WL_DATA_OFFER_RECEIVE, chosen, writeFd);
        // The compositor takes ownership of writeFd; close our copy so EOF
        // arrives at readFd when the source side closes its end.
        libc_close(writeFd);
        wl_display_flush(w._display);

        return Task.Run(() => ReadAllFromFd(readFd));
    }

    private static string? ReadAllFromFd(int fd)
    {
        try
        {
            var buffer = new byte[4096];
            var ms = new MemoryStream();
            const int timeoutMs = 250;
            var start = Environment.TickCount;

            while (Environment.TickCount - start < timeoutMs)
            {
                var n = libc_read(fd, buffer, (nuint)buffer.Length);
                if (n > 0)
                {
                    ms.Write(buffer, 0, (int)n);
                    continue;
                }
                if (n == 0) break; // EOF — source closed its end
                // n < 0 → EAGAIN under non-blocking; spin briefly waiting for more.
                System.Threading.Thread.Sleep(2);
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WaylandWindow", $"ReadAllFromFd failed: {ex.Message}");
            return null;
        }
        finally
        {
            libc_close(fd);
        }
    }

    #endregion
}
