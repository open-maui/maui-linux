// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Window;

// Wayland primary-selection (zwp_primary_selection_v1) — the middle-click paste
// clipboard. Distinct from the Ctrl+C/Ctrl+V wl_data_device clipboard: pasting
// goes to primary on text selection (no explicit copy), pastes happen on a
// middle-mouse-button click. Old X11 convention preserved on Wayland.
//
// Wire protocol mirrors wl_data_device almost exactly; the only structural
// difference is that primary-selection has no DnD events, so the device
// listener has just data_offer + selection (vs. wl_data_device's six events).
// We keep the layout 1:1 with WaylandWindow.Clipboard.cs so the two can stay
// in sync as compositor implementations evolve.
public partial class WaylandWindow
{
    #region zwp_primary_selection_v1 protocol constants

    private const uint ZWP_PRIMARY_SELECTION_DEVICE_MANAGER_V1_CREATE_SOURCE = 0;
    private const uint ZWP_PRIMARY_SELECTION_DEVICE_MANAGER_V1_GET_DEVICE = 1;

    private const uint ZWP_PRIMARY_SELECTION_DEVICE_V1_SET_SELECTION = 0;
    private const uint ZWP_PRIMARY_SELECTION_DEVICE_V1_DESTROY = 1;

    private const uint ZWP_PRIMARY_SELECTION_SOURCE_V1_OFFER = 0;
    private const uint ZWP_PRIMARY_SELECTION_SOURCE_V1_DESTROY = 1;

    private const uint ZWP_PRIMARY_SELECTION_OFFER_V1_RECEIVE = 0;
    private const uint ZWP_PRIMARY_SELECTION_OFFER_V1_DESTROY = 1;

    #endregion

    #region Interface tables (dlsym'd from libopenmaui_wl.so)

    private static IntPtr _zwp_primary_selection_device_manager_v1_interface;
    private static IntPtr _zwp_primary_selection_device_v1_interface;
    private static IntPtr _zwp_primary_selection_source_v1_interface;
    private static IntPtr _zwp_primary_selection_offer_v1_interface;

    #endregion

    #region State

    private IntPtr _primarySelectionDeviceManager;
    private IntPtr _primarySelectionDevice;
    private IntPtr _currentPrimaryOffer;
    private readonly Dictionary<IntPtr, List<string>> _primaryOfferMimesByOffer = new();
    private List<string> _currentPrimaryOfferMimes = new();

    private IntPtr _ownedPrimarySource;
    private readonly Dictionary<IntPtr, string> _ownedPrimarySourceTexts = new();

    private GCHandle _primaryDeviceListenerHandle;
    private readonly Dictionary<IntPtr, GCHandle> _primaryOfferListenerHandles = new();
    private readonly Dictionary<IntPtr, GCHandle> _primarySourceListenerHandles = new();

    #endregion

    #region Delegate types and listener structs

    // primary-selection-device events: data_offer(new_id), selection(?offer).
    private delegate void PrimaryDeviceDataOfferDelegate(IntPtr data, IntPtr device, IntPtr offer);
    private delegate void PrimaryDeviceSelectionDelegate(IntPtr data, IntPtr device, IntPtr offer);

    [StructLayout(LayoutKind.Sequential)]
    private struct PrimaryDeviceListener
    {
        public IntPtr DataOffer;
        public IntPtr Selection;
    }

    // primary-selection-offer events: offer(mime).
    private delegate void PrimaryOfferOfferDelegate(IntPtr data, IntPtr offer, IntPtr mimePtr);

    [StructLayout(LayoutKind.Sequential)]
    private struct PrimaryOfferListener
    {
        public IntPtr Offer;
    }

    // primary-selection-source events: send(mime, fd), cancelled().
    private delegate void PrimarySourceSendDelegate(IntPtr data, IntPtr source, IntPtr mimePtr, int fd);
    private delegate void PrimarySourceCancelledDelegate(IntPtr data, IntPtr source);

    [StructLayout(LayoutKind.Sequential)]
    private struct PrimarySourceListener
    {
        public IntPtr Send;
        public IntPtr Cancelled;
    }

    // Cached delegate references — same pattern as wl_data_device. One set per
    // window, reused across each offer/source instance.
    private PrimaryDeviceListener _primaryDeviceListener;
    private PrimaryOfferOfferDelegate? _primaryOfferOfferDelegate;
    private PrimaryOfferListener _primaryOfferListenerTemplate;
    private PrimarySourceSendDelegate? _primarySourceSendDelegate;
    private PrimarySourceCancelledDelegate? _primarySourceCancelledDelegate;
    private PrimarySourceListener _primarySourceListenerTemplate;

    #endregion

    #region Setup / teardown

    /// <summary>
    /// Wire the per-seat zwp_primary_selection_device_v1. Idempotent — guarded
    /// so it only runs once both the manager and the seat are present. Called
    /// from the registry handler and from SetupSeat as a retry.
    /// </summary>
    internal void SetupPrimarySelection()
    {
        if (_primarySelectionDevice != IntPtr.Zero) return;
        if (_primarySelectionDeviceManager == IntPtr.Zero || _seat == IntPtr.Zero) return;
        if (_zwp_primary_selection_device_v1_interface == IntPtr.Zero) return;

        _primarySelectionDevice = wl_proxy_marshal_constructor(
            _primarySelectionDeviceManager,
            ZWP_PRIMARY_SELECTION_DEVICE_MANAGER_V1_GET_DEVICE,
            _zwp_primary_selection_device_v1_interface,
            IntPtr.Zero,
            _seat);

        if (_primarySelectionDevice == IntPtr.Zero)
        {
            DiagnosticLog.Warn("WaylandWindow", "Failed to create zwp_primary_selection_device_v1");
            return;
        }

        _primaryDeviceListener = new PrimaryDeviceListener
        {
            DataOffer = Marshal.GetFunctionPointerForDelegate<PrimaryDeviceDataOfferDelegate>(OnPrimaryDeviceDataOffer),
            Selection = Marshal.GetFunctionPointerForDelegate<PrimaryDeviceSelectionDelegate>(OnPrimaryDeviceSelection),
        };
        _primaryDeviceListenerHandle = GCHandle.Alloc(_primaryDeviceListener, GCHandleType.Pinned);
        wl_proxy_add_listener(_primarySelectionDevice, _primaryDeviceListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));

        _primaryOfferOfferDelegate = OnPrimaryOfferOffer;
        _primaryOfferListenerTemplate = new PrimaryOfferListener
        {
            Offer = Marshal.GetFunctionPointerForDelegate(_primaryOfferOfferDelegate),
        };

        _primarySourceSendDelegate = OnPrimarySourceSend;
        _primarySourceCancelledDelegate = OnPrimarySourceCancelled;
        _primarySourceListenerTemplate = new PrimarySourceListener
        {
            Send = Marshal.GetFunctionPointerForDelegate(_primarySourceSendDelegate),
            Cancelled = Marshal.GetFunctionPointerForDelegate(_primarySourceCancelledDelegate),
        };

        s_activePrimarySelectionWindow = this;

        DiagnosticLog.Debug("WaylandWindow", "Native zwp_primary_selection_v1 wired");
    }

    private void DisposePrimarySelection()
    {
        ClearCurrentPrimaryOffer();
        DropOwnedPrimarySource();

        if (_primarySelectionDevice != IntPtr.Zero)
        {
            wl_proxy_marshal(_primarySelectionDevice, ZWP_PRIMARY_SELECTION_DEVICE_V1_DESTROY);
            wl_proxy_destroy(_primarySelectionDevice);
            _primarySelectionDevice = IntPtr.Zero;
        }
        if (_primaryDeviceListenerHandle.IsAllocated)
            _primaryDeviceListenerHandle.Free();
        if (_primarySelectionDeviceManager != IntPtr.Zero)
        {
            wl_proxy_destroy(_primarySelectionDeviceManager);
            _primarySelectionDeviceManager = IntPtr.Zero;
        }
        if (s_activePrimarySelectionWindow == this)
            s_activePrimarySelectionWindow = null;
    }

    #endregion

    #region zwp_primary_selection_device_v1 event handlers

    private static void OnPrimaryDeviceDataOffer(IntPtr data, IntPtr device, IntPtr offer)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        window._primaryOfferMimesByOffer[offer] = new List<string>();

        var pinned = GCHandle.Alloc(window._primaryOfferListenerTemplate, GCHandleType.Pinned);
        window._primaryOfferListenerHandles[offer] = pinned;
        wl_proxy_add_listener(offer, pinned.AddrOfPinnedObject(), GCHandle.ToIntPtr(window._thisHandle));
    }

    private static void OnPrimaryDeviceSelection(IntPtr data, IntPtr device, IntPtr offer)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        // Just like the data_device selection event: we own the prior offer.
        window.ClearCurrentPrimaryOffer();
        window._currentPrimaryOffer = offer;

        if (offer != IntPtr.Zero && window._primaryOfferMimesByOffer.TryGetValue(offer, out var mimes))
            window._currentPrimaryOfferMimes = mimes;
        else
            window._currentPrimaryOfferMimes = new List<string>();
    }

    private void ClearCurrentPrimaryOffer()
    {
        if (_currentPrimaryOffer != IntPtr.Zero)
        {
            if (_primaryOfferListenerHandles.Remove(_currentPrimaryOffer, out var pinned) && pinned.IsAllocated)
                pinned.Free();
            _primaryOfferMimesByOffer.Remove(_currentPrimaryOffer);
            wl_proxy_marshal(_currentPrimaryOffer, ZWP_PRIMARY_SELECTION_OFFER_V1_DESTROY);
            wl_proxy_destroy(_currentPrimaryOffer);
            _currentPrimaryOffer = IntPtr.Zero;
        }
        _currentPrimaryOfferMimes = new List<string>();
    }

    #endregion

    #region zwp_primary_selection_offer_v1 event handlers

    private static void OnPrimaryOfferOffer(IntPtr data, IntPtr offer, IntPtr mimePtr)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        var mime = Marshal.PtrToStringUTF8(mimePtr);
        if (string.IsNullOrEmpty(mime)) return;

        if (window._primaryOfferMimesByOffer.TryGetValue(offer, out var list))
            list.Add(mime);
    }

    #endregion

    #region zwp_primary_selection_source_v1 event handlers (we own the source)

    private static void OnPrimarySourceSend(IntPtr data, IntPtr source, IntPtr mimePtr, int fd)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) { try { libc_close(fd); } catch { } return; }
        var window = (WaylandWindow)handle.Target!;

        try
        {
            if (!window._ownedPrimarySourceTexts.TryGetValue(source, out var text))
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
            DiagnosticLog.Error("WaylandWindow", $"OnPrimarySourceSend write failed: {ex.Message}");
        }
        finally
        {
            libc_close(fd);
        }
    }

    private static void OnPrimarySourceCancelled(IntPtr data, IntPtr source)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        window.DestroyPrimarySource(source);
        if (window._ownedPrimarySource == source)
            window._ownedPrimarySource = IntPtr.Zero;
    }

    private void DestroyPrimarySource(IntPtr source)
    {
        if (source == IntPtr.Zero) return;
        if (_primarySourceListenerHandles.Remove(source, out var pinned) && pinned.IsAllocated)
            pinned.Free();
        _ownedPrimarySourceTexts.Remove(source);
        wl_proxy_marshal(source, ZWP_PRIMARY_SELECTION_SOURCE_V1_DESTROY);
        wl_proxy_destroy(source);
    }

    private void DropOwnedPrimarySource()
    {
        foreach (var ptr in _ownedPrimarySourceTexts.Keys.ToArray())
            DestroyPrimarySource(ptr);
        _ownedPrimarySource = IntPtr.Zero;
    }

    #endregion

    #region Public API (called from PrimarySelectionService)

    private static WaylandWindow? s_activePrimarySelectionWindow;

    /// <summary>
    /// True when a WaylandWindow has wired its primary-selection device and is
    /// ready to service primary-selection requests. False on X11 or compositors
    /// that don't expose zwp_primary_selection_device_manager_v1.
    /// </summary>
    public static bool NativePrimarySelectionAvailable =>
        s_activePrimarySelectionWindow != null
        && s_activePrimarySelectionWindow._primarySelectionDevice != IntPtr.Zero;

    /// <summary>
    /// Push <paramref name="text"/> to the primary selection. Mirrors text
    /// selection in most desktop apps — middle-click in any cooperating app
    /// will paste this. Returns false when the native path is unavailable.
    /// </summary>
    public static bool TrySetPrimarySelectionText(string text)
    {
        var w = s_activePrimarySelectionWindow;
        if (w == null || w._primarySelectionDevice == IntPtr.Zero || w._primarySelectionDeviceManager == IntPtr.Zero) return false;
        if (w._seat == IntPtr.Zero) return false;

        var source = wl_proxy_marshal_constructor(
            w._primarySelectionDeviceManager,
            ZWP_PRIMARY_SELECTION_DEVICE_MANAGER_V1_CREATE_SOURCE,
            _zwp_primary_selection_source_v1_interface,
            IntPtr.Zero);
        if (source == IntPtr.Zero) return false;

        // Same lifetime contract as wl_data_source — keep the old one alive until
        // its cancelled event fires; just record the new one as latest.
        w._ownedPrimarySource = source;
        w._ownedPrimarySourceTexts[source] = text ?? string.Empty;

        var pinned = GCHandle.Alloc(w._primarySourceListenerTemplate, GCHandleType.Pinned);
        w._primarySourceListenerHandles[source] = pinned;
        wl_proxy_add_listener(source, pinned.AddrOfPinnedObject(), GCHandle.ToIntPtr(w._thisHandle));

        foreach (var mime in s_textMimeTypes)
            wl_proxy_marshal(source, ZWP_PRIMARY_SELECTION_SOURCE_V1_OFFER, mime);

        var serial = Math.Max(w._pointerSerial, w._keyboardSerial);
        wl_proxy_marshal(w._primarySelectionDevice, ZWP_PRIMARY_SELECTION_DEVICE_V1_SET_SELECTION, source, serial);

        wl_display_flush(w._display);
        return true;
    }

    /// <summary>
    /// Read whatever's currently in the primary selection. Returns null when
    /// nothing is offered or the native path is unavailable.
    /// </summary>
    public static Task<string?> TryGetPrimarySelectionTextAsync()
    {
        var w = s_activePrimarySelectionWindow;
        if (w == null) return Task.FromResult<string?>(null);

        // Self-paste short-circuit — same reasoning as the clipboard path: if
        // we'd block waiting for our own source.send to fulfil, return buffered.
        if (w._ownedPrimarySource != IntPtr.Zero
            && w._ownedPrimarySourceTexts.TryGetValue(w._ownedPrimarySource, out var ownText)
            && w._currentPrimaryOffer != IntPtr.Zero
            && SelectionLooksLikeOurs(w._currentPrimaryOfferMimes))
        {
            return Task.FromResult<string?>(ownText);
        }

        if (w._currentPrimaryOffer == IntPtr.Zero)
            return Task.FromResult<string?>(null);

        string? chosen = null;
        foreach (var preferred in s_textMimeTypes)
        {
            if (w._currentPrimaryOfferMimes.Contains(preferred))
            {
                chosen = preferred;
                break;
            }
        }
        if (chosen == null) return Task.FromResult<string?>(null);

        var fds = new int[2];
        if (libc_pipe(fds) != 0)
        {
            DiagnosticLog.Warn("WaylandWindow", "pipe() failed for primary-selection receive");
            return Task.FromResult<string?>(null);
        }
        int readFd = fds[0], writeFd = fds[1];

        libc_fcntl(readFd, F_SETFL, O_NONBLOCK);

        wl_proxy_marshal_string_fd(w._currentPrimaryOffer, ZWP_PRIMARY_SELECTION_OFFER_V1_RECEIVE, chosen, writeFd);
        libc_close(writeFd);
        wl_display_flush(w._display);

        return Task.Run(() => ReadAllFromFd(readFd));
    }

    #endregion
}
