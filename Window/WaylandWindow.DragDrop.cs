// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Window;

// Wayland native drag-and-drop. Reuses the wl_data_device_manager binding the
// clipboard partial sets up — the DnD path differs only in *which* events fire
// (enter/leave/motion/drop instead of selection) and the data lifecycle
// (offer arrives via enter, not selection).
//
// Wire protocol (incoming, when something is dragged INTO our window):
//   wl_data_device.data_offer(new_id)              ← offer object created
//   wl_data_offer.offer(mime)                       ← MIMEs advertised
//   wl_data_device.enter(serial, surface, x, y, offer)  ← pointer crossed in
//     — we must wl_data_offer.accept(mime) here to indicate willingness
//     — and wl_data_offer.set_actions(actions, preferred) for v3+
//   wl_data_device.motion(time, x, y)               ← drag moving inside
//   wl_data_device.drop()                            ← user released LMB
//     — we read via wl_data_offer.receive(mime, fd) and read from the fd
//     — finish with wl_data_offer.finish(); destroy
//   wl_data_device.leave()                          ← drag left without drop
//
// Outgoing (start_drag):
//   create wl_data_source, offer MIMEs
//   wl_data_device.start_drag(source, origin_surface, icon_surface, serial)
//   compositor fires wl_data_source.send(mime, fd) when remote accepts
//   wl_data_source.dnd_drop_performed / dnd_finished signal completion
public partial class WaylandWindow
{
    #region Protocol extra constants (DnD only)

    private const uint WL_DATA_DEVICE_START_DRAG = 0;
    private const uint WL_DATA_OFFER_ACCEPT = 0;
    private const uint WL_DATA_OFFER_FINISH = 3;
    private const uint WL_DATA_OFFER_SET_ACTIONS = 4;

    // wl_data_device_manager.dnd_action enum (bitfield).
    private const uint WL_DATA_DEVICE_MANAGER_DND_ACTION_NONE = 0;
    private const uint WL_DATA_DEVICE_MANAGER_DND_ACTION_COPY = 1;
    private const uint WL_DATA_DEVICE_MANAGER_DND_ACTION_MOVE = 2;
    private const uint WL_DATA_DEVICE_MANAGER_DND_ACTION_ASK = 4;

    #endregion

    #region Incoming DnD state

    // The offer the compositor delivered via wl_data_device.enter(). Distinct
    // from _currentClipboardOffer — both can be live concurrently (e.g. user is
    // dragging a file in while the clipboard still holds an earlier copy).
    private IntPtr _currentDnDOffer;
    private uint _dndEnterSerial;
    private string? _dndAcceptedMime;
    private int _dndX, _dndY;

    #endregion

    #region Outgoing DnD state

    // Drag sources are tracked separately from clipboard sources: the clipboard
    // self-paste short-circuit consults _ownedDataSource/_ownedSourceTexts and
    // must never see drag payloads. Sources are destroyed only in their
    // cancelled (failed drag) or dnd_finished (successful drag) callbacks —
    // never synchronously.
    private IntPtr _activeDragSource;
    private readonly Dictionary<IntPtr, string> _dragSourceTexts = new();

    #endregion

    #region Per-instance event handlers (called from the static thunks in
    //        WaylandWindow.Clipboard.cs)

    private void HandleDnDEnter(uint serial, IntPtr surface, int x, int y, IntPtr offer)
    {
        _dndEnterSerial = serial;
        _currentDnDOffer = offer;
        // x/y arrive as wl_fixed (256ths of a logical pixel); convert to buffer
        // pixels the same way PointerEnter does.
        var scale = _bufferToLogicalScale;
        _dndX = (int)((x / 256.0f) * scale);
        _dndY = (int)((y / 256.0f) * scale);

        _dndAcceptedMime = null;

        // A null offer is legal (a drag carrying no data): nothing to accept or
        // negotiate — just raise DragEnter so enter/leave stay paired.
        if (offer == IntPtr.Zero)
        {
            DragDropService.Default.RaiseDragEnter(MakeDragData(IntPtr.Zero), _dndX, _dndY);
            return;
        }

        // Pick the first MIME we recognize — same precedence table the clipboard
        // uses. text/uri-list takes top spot when present because file drops
        // (the most common cross-app DnD) deliver it.
        if (_offerMimesByOffer.TryGetValue(offer, out var mimes))
        {
            string[] preferred = { "text/uri-list", "text/plain;charset=utf-8", "text/plain", "UTF8_STRING", "STRING" };
            foreach (var p in preferred)
                if (mimes.Contains(p)) { _dndAcceptedMime = p; break; }
        }

        // Tell the compositor whether we accept and which action we prefer.
        // A NULL mime (not empty string — that's a real mime) is the
        // protocol-defined "reject" signal. set_actions is v3+ only.
        wl_proxy_marshal_uint_string(offer, WL_DATA_OFFER_ACCEPT, serial, _dndAcceptedMime);
        if (_dataDeviceManagerVersion >= 3)
            wl_proxy_marshal(offer, WL_DATA_OFFER_SET_ACTIONS,
                WL_DATA_DEVICE_MANAGER_DND_ACTION_COPY | WL_DATA_DEVICE_MANAGER_DND_ACTION_MOVE,
                WL_DATA_DEVICE_MANAGER_DND_ACTION_COPY);

        // Raise DragEnter so consumers can veto. Accepted defaults to true, so
        // the acceptance above stands unless a handler explicitly opts out.
        var args = DragDropService.Default.RaiseDragEnter(MakeDragData(offer), _dndX, _dndY);
        if (!args.Accepted)
        {
            _dndAcceptedMime = null;
            wl_proxy_marshal_uint_string(offer, WL_DATA_OFFER_ACCEPT, serial, null);
        }
    }

    private void HandleDnDMotion(uint time, int x, int y)
    {
        // Same wl_fixed → buffer-pixel conversion as HandleDnDEnter.
        var scale = _bufferToLogicalScale;
        _dndX = (int)((x / 256.0f) * scale);
        _dndY = (int)((y / 256.0f) * scale);
        if (_currentDnDOffer == IntPtr.Zero) return;

        // Re-raise DragOver — gives the consumer a chance to flip Accepted on
        // a per-position basis (e.g. drop only allowed in certain regions).
        var args = DragDropService.Default.RaiseDragOver(MakeDragData(_currentDnDOffer), _dndX, _dndY);
        // We don't re-issue accept/set_actions on every motion — the compositor
        // remembers the last value from enter. Only flip when Accepted changes.
        // (Apps that flicker the accepted state would need an extra cached
        // bool; deferring until a real consumer needs it.)
    }

    private void HandleDnDLeave()
    {
        // After a drop, ownership of the offer has transferred to HandleDnDDrop
        // and _currentDnDOffer is already zero — a trailing leave from the
        // compositor must not double-destroy it.
        if (_currentDnDOffer != IntPtr.Zero)
        {
            DestroyDnDOffer(_currentDnDOffer, sendFinish: false);
            _currentDnDOffer = IntPtr.Zero;
        }
        _dndAcceptedMime = null;
        DragDropService.Default.RaiseDragLeave();
    }

    private void HandleDnDDrop()
    {
        // Capture and clear the shared state up front: ownership of the offer
        // transfers to this drop routine, so a leave or a new drag's enter
        // arriving while the pipe read is in flight can neither double-destroy
        // the offer nor clobber the accepted mime. Only the captured locals are
        // used from here on.
        var offer = _currentDnDOffer;
        var acceptedMime = _dndAcceptedMime;
        var dropX = _dndX;
        var dropY = _dndY;
        _currentDnDOffer = IntPtr.Zero;
        _dndAcceptedMime = null;

        if (offer == IntPtr.Zero || acceptedMime == null)
        {
            // Dropped on us but nothing was accepted — destroy without finish
            // (finish on an unaccepted offer is a protocol error).
            DestroyDnDOffer(offer, sendFinish: false);
            DragDropService.Default.RaiseDragLeave();
            return;
        }

        // Open a pipe; compositor writes the dragged bytes to our fd.
        var fds = new int[2];
        if (libc_pipe(fds) != 0)
        {
            DestroyDnDOffer(offer, sendFinish: false);
            DragDropService.Default.RaiseDragLeave();
            return;
        }
        int readFd = fds[0], writeFd = fds[1];
        libc_fcntl(readFd, F_SETFL, O_NONBLOCK);

        wl_proxy_marshal_string_fd(offer, WL_DATA_OFFER_RECEIVE, acceptedMime, writeFd);
        libc_close(writeFd);
        wl_display_flush(_display);

        // Read on a background task — same pattern as clipboard receive — and
        // marshal the resulting drop event back to the UI thread.
        var data = MakeDragData(offer);
        Task.Run(() =>
        {
            var text = ReadAllFromFd(readFd);

            void RaiseAndFinish()
            {
                try
                {
                    if (text != null)
                    {
                        data.Text = text;

                        // text/uri-list is the standard cross-toolkit file-drop
                        // format: newline-separated URIs, '#' lines are
                        // comments per RFC 2483. Surface decoded file:// paths
                        // on DragData.FilePaths so consumers don't have to
                        // re-parse.
                        if (acceptedMime == "text/uri-list")
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
                            if (paths.Count > 0) data.FilePaths = paths.ToArray();
                        }
                    }
                    DragDropService.Default.RaiseDrop(data, text, dropX, dropY);
                }
                finally
                {
                    // wl_data_offer.finish + destroy (finish is v3+; gated in
                    // DestroyDnDOffer).
                    DestroyDnDOffer(offer, sendFinish: true);
                    wl_display_flush(_display);
                }
            }

            // Marshal back to the main thread; with no dispatcher (teardown)
            // run inline rather than leak the offer.
            if (Microsoft.Maui.Platform.Linux.Dispatching.LinuxDispatcher.Main is { } dispatcher)
                dispatcher.Dispatch(RaiseAndFinish);
            else
                RaiseAndFinish();
        });
    }

    /// <summary>
    /// Release a DnD offer: optionally send finish (v3+ only, and only legal
    /// after an accepted drop), then destroy the proxy and free its listener
    /// handle and MIME bookkeeping.
    /// </summary>
    private void DestroyDnDOffer(IntPtr offer, bool sendFinish)
    {
        if (offer == IntPtr.Zero) return;
        if (sendFinish && _dataDeviceManagerVersion >= 3)
            try { wl_proxy_marshal(offer, WL_DATA_OFFER_FINISH); } catch { }
        try { wl_proxy_marshal(offer, WL_DATA_OFFER_DESTROY); } catch { }
        try { wl_proxy_destroy(offer); } catch { }
        if (_offerListenerHandles.Remove(offer, out var pinned) && pinned.IsAllocated)
            pinned.Free();
        _offerMimesByOffer.Remove(offer);
    }

    private DragData MakeDragData(IntPtr offer)
    {
        var dd = new DragData { WaylandOffer = offer };
        if (offer != IntPtr.Zero && _offerMimesByOffer.TryGetValue(offer, out var mimes))
            dd.SupportedMimeTypes = mimes.ToArray();
        return dd;
    }

    #endregion

    #region Outgoing DnD — start a drag from this window

    /// <summary>
    /// Begin a Wayland drag-and-drop carrying <paramref name="text"/>. Returns
    /// false if the wl_data_device isn't ready, the seat hasn't been bound, or
    /// the most recent pointer serial isn't usable. The drag remains live until
    /// the compositor fires <c>wl_data_source.cancelled</c> or
    /// <c>dnd_finished</c>; either way the source's lifecycle is the same as
    /// for clipboard sources (see <see cref="WaylandWindow"/>'s clipboard part
    /// for the lifetime contract).
    /// </summary>
    public static bool TryStartDrag(string text)
    {
        var w = s_activeClipboardWindow;
        if (w == null || w._dataDevice == IntPtr.Zero || w._dataDeviceManager == IntPtr.Zero) return false;
        if (w._seat == IntPtr.Zero || w._pointerButtonSerial == 0) return false;
        if (w._surface == IntPtr.Zero) return false;
        if (_wl_data_source_interface == IntPtr.Zero) return false;

        var source = wl_proxy_marshal_constructor(
            w._dataDeviceManager,
            WL_DATA_DEVICE_MANAGER_CREATE_DATA_SOURCE,
            _wl_data_source_interface,
            IntPtr.Zero);
        if (source == IntPtr.Zero) return false;

        // Track drag sources separately from clipboard sources — a drag source
        // must never become _ownedDataSource, or the clipboard's self-paste
        // short-circuit would return the dragged text instead of the clipboard.
        w._activeDragSource = source;
        w._dragSourceTexts[source] = text ?? string.Empty;

        // Source listeners are shared with clipboard — wire up the same template.
        var pinned = System.Runtime.InteropServices.GCHandle.Alloc(w._sourceListenerTemplate, System.Runtime.InteropServices.GCHandleType.Pinned);
        w._sourceListenerHandles[source] = pinned;
        wl_proxy_add_listener(source, pinned.AddrOfPinnedObject(), System.Runtime.InteropServices.GCHandle.ToIntPtr(w._thisHandle));

        // Offer the same text MIMEs the clipboard does. set_actions advertises
        // what we can produce; the compositor matches against the destination's
        // accepted actions.
        foreach (var mime in s_textMimeTypes)
            wl_proxy_marshal(source, WL_DATA_SOURCE_OFFER, mime);
        // wl_data_source.set_actions (opcode 2) is v3+ only — on a lower bound
        // version it is a protocol error, so gate on the actual bound version.
        if (w._dataDeviceManagerVersion >= 3)
            wl_proxy_marshal(source, /*WL_DATA_SOURCE_SET_ACTIONS*/ 2u,
                WL_DATA_DEVICE_MANAGER_DND_ACTION_COPY | WL_DATA_DEVICE_MANAGER_DND_ACTION_MOVE);

        // start_drag(source, origin_surface, icon_surface (null), serial).
        // Icon surface is optional; passing null gives the compositor's default
        // (or no) drag icon. A future enhancement could render a thumbnail.
        // The serial must be the button PRESS that initiated the gesture —
        // _pointerButtonSerial — not _pointerSerial, which pointer-enter also
        // overwrites; a stale serial makes the compositor silently ignore the
        // drag and the source would leak.
        wl_proxy_marshal_obj_obj_obj_uint(w._dataDevice, WL_DATA_DEVICE_START_DRAG, source, w._surface, IntPtr.Zero, w._pointerButtonSerial);

        wl_display_flush(w._display);
        return true;
    }

    #endregion
}
