// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Window;

// Two extension protocols layered on top of xdg-shell:
//
//   zxdg_decoration_manager_v1: lets us request server-side decorations (titlebar,
//   borders) from the compositor. KDE/Sway honor this; GNOME defaults to client-side
//   only and will respond with mode=client_side, leaving us responsible for drawing
//   our own titlebar (a follow-up task — for now, on GNOME-Wayland the user is
//   expected to use MAUI_PREFER_X11=1 until CSD lands).
//
//   wp_fractional_scale_v1: lets the compositor tell us the exact scale factor it
//   wants (in 120ths). Without it we'd render at integer scale and the compositor
//   would up/downsample, producing fuzzy text on 1.25x/1.5x displays.
//
// Both are wayland-protocols extensions, not in libwayland-client. We define minimal
// wl_interface stubs ourselves (mirroring how xdg-shell is declared in the main file).
public partial class WaylandWindow
{
    #region zxdg_decoration_manager_v1

    private const uint ZXDG_DECORATION_MANAGER_V1_GET_TOPLEVEL_DECORATION = 1;
    private const uint ZXDG_TOPLEVEL_DECORATION_V1_DESTROY = 0;
    private const uint ZXDG_TOPLEVEL_DECORATION_V1_SET_MODE = 1;

    private const uint ZXDG_TOPLEVEL_DECORATION_V1_MODE_CLIENT_SIDE = 1;
    private const uint ZXDG_TOPLEVEL_DECORATION_V1_MODE_SERVER_SIDE = 2;

    private static IntPtr _zxdg_decoration_manager_v1_interface;
    private static IntPtr _zxdg_toplevel_decoration_v1_interface;
    private static GCHandle _decorationManagerHandle;
    private static GCHandle _decorationHandle;
    private static IntPtr _decorationManagerName;
    private static IntPtr _decorationName;

    private IntPtr _decorationManager;
    private IntPtr _toplevelDecoration;

    // Client-side decoration state. Set true when:
    //   1. Compositor has no zxdg_decoration_manager_v1 (must draw our own).
    //   2. Compositor responds to set_mode(server_side) with configure(client_side)
    //      — GNOME/Mutter does this unconditionally.
    //   3. MAUI_PREFER_CSD=1 is set (testing override on KDE/Sway).
    // Read by SkiaRenderingEngine to reserve the titlebar strip and by the pointer
    // handler to intercept titlebar clicks for move/resize/buttons.
    private bool _useCsd;
    public bool UseCsd => _useCsd;

    // Cached titlebar button bounds (in logical pixels). Recomputed each frame
    // by WaylandCsdRenderer.DrawTitlebar; consulted by the pointer hit-test
    // before forwarding events to MAUI views.
    internal SKRect CsdCloseButtonBounds;
    internal SKRect CsdMaximizeButtonBounds;
    internal SKRect CsdMinimizeButtonBounds;

    // Logical titlebar height. Kept as a constant so SkiaRenderingEngine and the
    // pointer hit-test agree on the inset without a back-channel.
    internal const float CsdTitlebarHeightLogical = 32f;

    private delegate void ZxdgToplevelDecorationV1ConfigureDelegate(IntPtr data, IntPtr proxy, uint mode);

    [StructLayout(LayoutKind.Sequential)]
    private struct ToplevelDecorationListener
    {
        public IntPtr Configure;
    }

    private ToplevelDecorationListener _decorationListener;
    // Rooted delegate — the listener struct only stores a raw function pointer,
    // which does not keep the delegate (and its native thunk) alive.
    private ZxdgToplevelDecorationV1ConfigureDelegate? _decorationConfigureDelegate;
    private GCHandle _decorationListenerHandle;

    private static void LoadDecorationInterfaces()
    {
        if (_zxdg_decoration_manager_v1_interface != IntPtr.Zero) return;

        // Build the decoration object first so the manager can reference it in
        // get_toplevel_decoration's types table.
        _zxdg_toplevel_decoration_v1_interface = BuildInterface("zxdg_toplevel_decoration_v1", 1,
            methods: new MessageDef[]
            {
                new("destroy", "", Array.Empty<IntPtr>()),
                new("set_mode", "u", new[] { IntPtr.Zero }),
                new("unset_mode", "", Array.Empty<IntPtr>()),
            },
            events: new MessageDef[]
            {
                new("configure", "u", new[] { IntPtr.Zero }),
            });

        _zxdg_decoration_manager_v1_interface = BuildInterface("zxdg_decoration_manager_v1", 1,
            methods: new MessageDef[]
            {
                new("destroy", "", Array.Empty<IntPtr>()),
                new("get_toplevel_decoration", "no",
                    new[] { _zxdg_toplevel_decoration_v1_interface, _xdg_toplevel_interface }),
            },
            events: Array.Empty<MessageDef>());
    }

    private void RequestServerSideDecorations()
    {
        // MAUI_PREFER_CSD=1 forces CSD on for testing under compositors that would
        // otherwise hand back server-side decorations (KDE, Sway). The CSD path
        // still draws and hit-tests our titlebar; the compositor just won't add
        // its own on top because we never request server-side mode.
        var preferCsd = Environment.GetEnvironmentVariable("MAUI_PREFER_CSD");
        bool forceCsd = !string.IsNullOrEmpty(preferCsd) && preferCsd != "0";

        // No decoration manager at all (e.g. GNOME 3.x pre-extension, custom
        // compositors) means there's no protocol to negotiate SSD over —
        // we must draw client-side.
        if (_decorationManager == IntPtr.Zero || _xdgToplevel == IntPtr.Zero)
        {
            _useCsd = true;
            DiagnosticLog.Debug("WaylandWindow", "No zxdg_decoration_manager_v1; using CSD");
            return;
        }

        if (forceCsd)
        {
            _useCsd = true;
            DiagnosticLog.Debug("WaylandWindow", "MAUI_PREFER_CSD=1; using CSD (skipping SSD request)");
            return;
        }

        // get_toplevel_decoration: signature "no" → NULL placeholder for new_id, then the toplevel object.
        _toplevelDecoration = wl_proxy_marshal_constructor(
            _decorationManager,
            ZXDG_DECORATION_MANAGER_V1_GET_TOPLEVEL_DECORATION,
            _zxdg_toplevel_decoration_v1_interface,
            IntPtr.Zero,
            _xdgToplevel);

        if (_toplevelDecoration == IntPtr.Zero)
        {
            // Couldn't construct a toplevel decoration object — fall back to CSD.
            _useCsd = true;
            DiagnosticLog.Debug("WaylandWindow", "get_toplevel_decoration failed; using CSD");
            return;
        }

        // Subscribe to configure so we can detect when the compositor downgrades
        // to client-side (Mutter/GNOME always does). The event fires on every
        // mode change, including the first response to our set_mode request.
        _decorationConfigureDelegate = OnDecorationConfigure; // rooted; see field comment
        _decorationListener = new ToplevelDecorationListener
        {
            Configure = Marshal.GetFunctionPointerForDelegate(_decorationConfigureDelegate),
        };
        _decorationListenerHandle = GCHandle.Alloc(_decorationListener, GCHandleType.Pinned);
        wl_proxy_add_listener(_toplevelDecoration, _decorationListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));

        // Request server-side mode. The compositor decides; we observe its
        // choice via the configure event handler above.
        wl_proxy_marshal(_toplevelDecoration, ZXDG_TOPLEVEL_DECORATION_V1_SET_MODE,
            ZXDG_TOPLEVEL_DECORATION_V1_MODE_SERVER_SIDE);
    }

    private static void OnDecorationConfigure(IntPtr data, IntPtr proxy, uint mode)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        bool wantsCsd = mode == ZXDG_TOPLEVEL_DECORATION_V1_MODE_CLIENT_SIDE;
        if (window._useCsd == wantsCsd) return;

        window._useCsd = wantsCsd;
        DiagnosticLog.Debug("WaylandWindow", $"Decoration mode = {(wantsCsd ? "client_side (CSD)" : "server_side")}; redrawing");

        // The titlebar inset reservation changes the available content area, so
        // we need a full redraw + remeasure. The XdgToplevelConfigure path already
        // wires Exposed → RenderEngine, so just trigger that.
        window.Exposed?.Invoke(window, EventArgs.Empty);
    }

    private void DisposeDecoration()
    {
        if (_toplevelDecoration != IntPtr.Zero)
        {
            wl_proxy_marshal(_toplevelDecoration, ZXDG_TOPLEVEL_DECORATION_V1_DESTROY);
            wl_proxy_destroy(_toplevelDecoration);
            _toplevelDecoration = IntPtr.Zero;
        }
        if (_decorationListenerHandle.IsAllocated)
            _decorationListenerHandle.Free();
        if (_decorationManager != IntPtr.Zero)
        {
            wl_proxy_destroy(_decorationManager);
            _decorationManager = IntPtr.Zero;
        }
    }

    #endregion

    #region wp_fractional_scale_manager_v1

    private const uint WP_FRACTIONAL_SCALE_MANAGER_V1_GET_FRACTIONAL_SCALE = 1;
    private const uint WP_FRACTIONAL_SCALE_V1_DESTROY = 0;

    private static IntPtr _wp_fractional_scale_manager_v1_interface;
    private static IntPtr _wp_fractional_scale_v1_interface;

    internal static IntPtr _wp_viewporter_interface;
    internal static IntPtr _wp_viewport_interface;
    private static GCHandle _fractionalScaleManagerHandle;
    private static GCHandle _fractionalScaleHandle;
    private static IntPtr _fractionalScaleManagerName;
    private static IntPtr _fractionalScaleName;

    private IntPtr _fractionalScaleManager;
    private IntPtr _fractionalScale;
    private float _preferredScale = 1.0f;

    public float PreferredScale => _preferredScale;

    private delegate void FractionalScalePreferredDelegate(IntPtr data, IntPtr proxy, uint scale);

    [StructLayout(LayoutKind.Sequential)]
    private struct FractionalScaleListener
    {
        public IntPtr PreferredScale; // function pointer
    }

    private FractionalScaleListener _fractionalScaleListener;
    // Rooted delegate — same reasoning as _decorationConfigureDelegate.
    private FractionalScalePreferredDelegate? _fractionalScalePreferredDelegate;
    private GCHandle _fractionalScaleListenerHandle;

    private static void LoadFractionalScaleInterfaces()
    {
        if (_wp_fractional_scale_manager_v1_interface != IntPtr.Zero) return;

        _wp_fractional_scale_v1_interface = BuildInterface("wp_fractional_scale_v1", 1,
            methods: new MessageDef[]
            {
                new("destroy", "", Array.Empty<IntPtr>()),
            },
            events: new MessageDef[]
            {
                new("preferred_scale", "u", new[] { IntPtr.Zero }),
            });

        _wp_fractional_scale_manager_v1_interface = BuildInterface("wp_fractional_scale_manager_v1", 1,
            methods: new MessageDef[]
            {
                new("destroy", "", Array.Empty<IntPtr>()),
                new("get_fractional_scale", "no",
                    new[] { _wp_fractional_scale_v1_interface, _wl_surface_interface }),
            },
            events: Array.Empty<MessageDef>());
    }

    private void RequestFractionalScale()
    {
        if (_fractionalScaleManager == IntPtr.Zero || _surface == IntPtr.Zero)
            return;

        // get_fractional_scale: signature "no" → NULL placeholder for new_id, then the surface.
        _fractionalScale = wl_proxy_marshal_constructor(
            _fractionalScaleManager,
            WP_FRACTIONAL_SCALE_MANAGER_V1_GET_FRACTIONAL_SCALE,
            _wp_fractional_scale_v1_interface,
            IntPtr.Zero,
            _surface);

        if (_fractionalScale == IntPtr.Zero) return;

        _fractionalScalePreferredDelegate = OnFractionalScalePreferred; // rooted; see field comment
        _fractionalScaleListener = new FractionalScaleListener
        {
            PreferredScale = Marshal.GetFunctionPointerForDelegate(_fractionalScalePreferredDelegate),
        };
        _fractionalScaleListenerHandle = GCHandle.Alloc(_fractionalScaleListener, GCHandleType.Pinned);
        wl_proxy_add_listener(_fractionalScale, _fractionalScaleListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));
    }

    private static void OnFractionalScalePreferred(IntPtr data, IntPtr proxy, uint scale)
    {
        // The compositor sends scale in 120ths (e.g. 180 = 1.5x).
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        var newScale = scale / 120f;
        if (Math.Abs(window._preferredScale - newScale) < 0.01f) return;

        window._preferredScale = newScale;
        DiagnosticLog.Debug("WaylandWindow", $"Compositor preferred scale: {newScale:F2} (raw {scale})");

        // Forward to the application-wide HiDpiService so SkiaRenderingEngine
        // and views observe the updated scale on the next frame.
        // (HiDpiService is exposed via LinuxApplication.DpiScale; surfacing this
        // wires through to the render loop in Stage 2g once the cross-window
        // scale change pathway is in place.)
    }

    private void DisposeFractionalScale()
    {
        if (_fractionalScale != IntPtr.Zero)
        {
            wl_proxy_marshal(_fractionalScale, WP_FRACTIONAL_SCALE_V1_DESTROY);
            wl_proxy_destroy(_fractionalScale);
            _fractionalScale = IntPtr.Zero;
        }
        if (_fractionalScaleListenerHandle.IsAllocated)
            _fractionalScaleListenerHandle.Free();
        if (_fractionalScaleManager != IntPtr.Zero)
        {
            wl_proxy_destroy(_fractionalScaleManager);
            _fractionalScaleManager = IntPtr.Zero;
        }
    }

    #endregion

    #region CSD actions (invoked from pointer hit-test in CSD mode)

    // All of these require a fresh pointer-button serial. Caller must use the
    // serial captured in OnPointerButton when state == PRESSED, not the older
    // pointer-enter serial. See WaylandWindow.cs for the capture site.
    internal void RequestCsdMove(uint serial)
    {
        if (_xdgToplevel == IntPtr.Zero || _seat == IntPtr.Zero) return;
        xdg_toplevel_move(_xdgToplevel, _seat, serial);
    }

    internal void RequestCsdResize(uint serial, uint edge)
    {
        if (_xdgToplevel == IntPtr.Zero || _seat == IntPtr.Zero) return;
        xdg_toplevel_resize(_xdgToplevel, _seat, serial, edge);
    }

    internal void ToggleMaximize()
    {
        if (_xdgToplevel == IntPtr.Zero) return;
        if (_isMaximized)
            xdg_toplevel_unset_maximized(_xdgToplevel);
        else
            xdg_toplevel_set_maximized(_xdgToplevel);
    }

    internal void Minimize()
    {
        if (_xdgToplevel == IntPtr.Zero) return;
        xdg_toplevel_set_minimized(_xdgToplevel);
    }

    internal void CloseFromCsd()
    {
        // No xdg_toplevel.close request exists; mirror what XdgToplevelClose
        // does when the compositor asks us to close.
        CloseRequested?.Invoke(this, EventArgs.Empty);
        _isRunning = false;
    }

    /// <summary>
    /// CSD pointer hit-test. Returns true if the press was consumed by the
    /// titlebar (move/resize/button); false to let the event propagate to
    /// MAUI views. Coordinates are in logical pixels.
    /// </summary>
    internal bool HandleCsdPointerDown(float x, float y, uint serial)
    {
        if (!_useCsd) return false;

        // Edge resize hit-test (only when not maximized — maximized windows
        // can't be resized by edge drag, that would just unmaximize awkwardly).
        if (!_isMaximized)
        {
            const float resizeMargin = 6f;
            float logicalWidth = _width / _bufferToLogicalScale;
            float logicalHeight = _height / _bufferToLogicalScale;

            bool atLeft = x <= resizeMargin;
            bool atRight = x >= logicalWidth - resizeMargin;
            bool atTop = y <= resizeMargin;
            bool atBottom = y >= logicalHeight - resizeMargin;

            uint edge = 0;
            if (atTop && atLeft) edge = XDG_TOPLEVEL_RESIZE_EDGE_TOP_LEFT;
            else if (atTop && atRight) edge = XDG_TOPLEVEL_RESIZE_EDGE_TOP_RIGHT;
            else if (atBottom && atLeft) edge = XDG_TOPLEVEL_RESIZE_EDGE_BOTTOM_LEFT;
            else if (atBottom && atRight) edge = XDG_TOPLEVEL_RESIZE_EDGE_BOTTOM_RIGHT;
            else if (atTop) edge = XDG_TOPLEVEL_RESIZE_EDGE_TOP;
            else if (atBottom) edge = XDG_TOPLEVEL_RESIZE_EDGE_BOTTOM;
            else if (atLeft) edge = XDG_TOPLEVEL_RESIZE_EDGE_LEFT;
            else if (atRight) edge = XDG_TOPLEVEL_RESIZE_EDGE_RIGHT;

            if (edge != 0)
            {
                RequestCsdResize(serial, edge);
                return true;
            }
        }

        // Below the titlebar → event is for the content area; don't consume.
        if (y > CsdTitlebarHeightLogical)
            return false;

        // Button hits take priority over titlebar drag.
        if (CsdCloseButtonBounds.Contains(x, y))
        {
            CloseFromCsd();
            return true;
        }
        if (CsdMaximizeButtonBounds.Contains(x, y))
        {
            ToggleMaximize();
            return true;
        }
        if (CsdMinimizeButtonBounds.Contains(x, y))
        {
            Minimize();
            return true;
        }

        // Any other point in the titlebar starts an interactive move.
        // Double-click on the titlebar should toggle maximize, but we don't
        // have double-click detection plumbed yet — leave as v2.
        RequestCsdMove(serial);
        return true;
    }

    #endregion
}
