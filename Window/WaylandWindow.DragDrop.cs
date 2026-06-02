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

    #region Per-instance event handlers (called from the static thunks in
    //        WaylandWindow.Clipboard.cs)

    private void HandleDnDEnter(uint serial, IntPtr surface, int x, int y, IntPtr offer)
    {
        _dndEnterSerial = serial;
        _currentDnDOffer = offer;
        _dndX = x; _dndY = y;

        // Pick the first MIME we recognize — same precedence table the clipboard
        // uses. text/uri-list takes top spot when present because file drops
        // (the most common cross-app DnD) deliver it.
        _dndAcceptedMime = null;
        if (_offerMimesByOffer.TryGetValue(offer, out var mimes))
        {
            string[] preferred = { "text/uri-list", "text/plain;charset=utf-8", "text/plain", "UTF8_STRING", "STRING" };
            foreach (var p in preferred)
                if (mimes.Contains(p)) { _dndAcceptedMime = p; break; }
        }

        // Tell the compositor whether we accept and which action we prefer.
        // Empty mime here is the protocol-defined "reject" signal.
        wl_proxy_marshal_uint_string(offer, WL_DATA_OFFER_ACCEPT, serial, _dndAcceptedMime ?? string.Empty);
        wl_proxy_marshal(offer, WL_DATA_OFFER_SET_ACTIONS,
            WL_DATA_DEVICE_MANAGER_DND_ACTION_COPY | WL_DATA_DEVICE_MANAGER_DND_ACTION_MOVE,
            WL_DATA_DEVICE_MANAGER_DND_ACTION_COPY);

        // Raise DragEnter so consumers can decide whether to accept. If they
        // refuse, we'll re-accept with null mime in HandleDnDMotion. Most
        // consumers leave Accepted=true and the default-Copy action stands.
        var args = DragDropService.Default.RaiseDragEnter(MakeDragData(offer), x, y);
        if (!args.Accepted)
        {
            _dndAcceptedMime = null;
            wl_proxy_marshal_uint_string(offer, WL_DATA_OFFER_ACCEPT, serial, string.Empty);
        }
    }

    private void HandleDnDMotion(uint time, int x, int y)
    {
        _dndX = x; _dndY = y;
        if (_currentDnDOffer == IntPtr.Zero) return;

        // Re-raise DragOver — gives the consumer a chance to flip Accepted on
        // a per-position basis (e.g. drop only allowed in certain regions).
        var args = DragDropService.Default.RaiseDragOver(MakeDragData(_currentDnDOffer), x, y);
        // We don't re-issue accept/set_actions on every motion — the compositor
        // remembers the last value from enter. Only flip when Accepted changes.
        // (Apps that flicker the accepted state would need an extra cached
        // bool; deferring until a real consumer needs it.)
    }

    private void HandleDnDLeave()
    {
        if (_currentDnDOffer != IntPtr.Zero)
        {
            wl_proxy_marshal(_currentDnDOffer, WL_DATA_OFFER_DESTROY);
            wl_proxy_destroy(_currentDnDOffer);
            if (_offerListenerHandles.Remove(_currentDnDOffer, out var pinned) && pinned.IsAllocated)
                pinned.Free();
            _offerMimesByOffer.Remove(_currentDnDOffer);
            _currentDnDOffer = IntPtr.Zero;
        }
        _dndAcceptedMime = null;
        DragDropService.Default.RaiseDragLeave();
    }

    private void HandleDnDDrop()
    {
        if (_currentDnDOffer == IntPtr.Zero || _dndAcceptedMime == null)
        {
            // User dropped on us but we rejected. Spec requires finish anyway
            // for v3+; for safety, just destroy.
            HandleDnDLeave();
            return;
        }

        // Open a pipe; compositor writes the dragged bytes to our fd.
        var fds = new int[2];
        if (libc_pipe(fds) != 0)
        {
            HandleDnDLeave();
            return;
        }
        int readFd = fds[0], writeFd = fds[1];
        libc_fcntl(readFd, F_SETFL, O_NONBLOCK);

        wl_proxy_marshal_string_fd(_currentDnDOffer, WL_DATA_OFFER_RECEIVE, _dndAcceptedMime, writeFd);
        libc_close(writeFd);
        wl_display_flush(_display);

        // Read on a background task — same pattern as clipboard receive — and
        // marshal the resulting drop event back to the UI thread.
        var offer = _currentDnDOffer;
        var data = MakeDragData(offer);
        var x = _dndX; var y = _dndY;
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
                        if (_dndAcceptedMime == "text/uri-list")
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
                    DragDropService.Default.RaiseDrop(data, text);
                }
                finally
                {
                    // wl_data_offer.finish + destroy. Required on v3+.
                    if (offer != IntPtr.Zero)
                    {
                        try { wl_proxy_marshal(offer, WL_DATA_OFFER_FINISH); } catch { }
                        try { wl_proxy_marshal(offer, WL_DATA_OFFER_DESTROY); } catch { }
                        try { wl_proxy_destroy(offer); } catch { }
                        if (_offerListenerHandles.Remove(offer, out var pinned) && pinned.IsAllocated)
                            pinned.Free();
                        _offerMimesByOffer.Remove(offer);
                        if (_currentDnDOffer == offer) _currentDnDOffer = IntPtr.Zero;
                    }
                    _dndAcceptedMime = null;
                    wl_display_flush(_display);
                }
            }

            if (Microsoft.Maui.Platform.Linux.Dispatching.LinuxDispatcher.IsMainThread)
                RaiseAndFinish();
            else
                Microsoft.Maui.Platform.Linux.Dispatching.LinuxDispatcher.Main?.Dispatch(RaiseAndFinish);
        });
    }

    private static DragData MakeDragData(IntPtr offer)
    {
        var dd = new DragData
        {
            SourceWindow = offer,    // we have no X11 window ptr here, repurpose for the offer
        };
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
        if (w._seat == IntPtr.Zero || w._pointerSerial == 0) return false;
        if (w._surface == IntPtr.Zero) return false;

        var source = wl_proxy_marshal_constructor(
            w._dataDeviceManager,
            WL_DATA_DEVICE_MANAGER_CREATE_DATA_SOURCE,
            _wl_data_source_interface,
            IntPtr.Zero);
        if (source == IntPtr.Zero) return false;

        w._ownedDataSource = source;
        w._ownedSourceTexts[source] = text ?? string.Empty;

        // Source listeners are shared with clipboard — wire up the same template.
        var pinned = System.Runtime.InteropServices.GCHandle.Alloc(w._sourceListenerTemplate, System.Runtime.InteropServices.GCHandleType.Pinned);
        w._sourceListenerHandles[source] = pinned;
        wl_proxy_add_listener(source, pinned.AddrOfPinnedObject(), System.Runtime.InteropServices.GCHandle.ToIntPtr(w._thisHandle));

        // Offer the same text MIMEs the clipboard does. set_actions advertises
        // what we can produce; the compositor matches against the destination's
        // accepted actions.
        foreach (var mime in s_textMimeTypes)
            wl_proxy_marshal(source, WL_DATA_SOURCE_OFFER, mime);
        // wl_data_source.set_actions opcode is 2 in v3+. Skip on v1 — older
        // compositors would protocol-error on it, but since we bind v3 in the
        // registry handler this is safe.
        wl_proxy_marshal(source, /*WL_DATA_SOURCE_SET_ACTIONS*/ 2u,
            WL_DATA_DEVICE_MANAGER_DND_ACTION_COPY | WL_DATA_DEVICE_MANAGER_DND_ACTION_MOVE);

        // start_drag(source, origin_surface, icon_surface (null), serial).
        // Icon surface is optional; passing null gives the compositor's default
        // (or no) drag icon. A future enhancement could render a thumbnail.
        wl_proxy_marshal_obj_obj_obj_uint(w._dataDevice, WL_DATA_DEVICE_START_DRAG, source, w._surface, IntPtr.Zero, w._pointerSerial);

        wl_display_flush(w._display);
        return true;
    }

    #endregion
}
