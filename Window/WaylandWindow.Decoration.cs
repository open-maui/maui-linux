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
//   only and will respond with mode=client_side, leaving us to draw our own titlebar
//   (WaylandCsdRenderer). A window with a TitleBar (MAUI's Window.TitleBar) asks for
//   client_side instead, so the TitleBar fills our titlebar as on Windows.
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

    // Client-side decoration state (shared with the X11 window's decorations). Active when:
    //   1. Compositor has no zxdg_decoration_manager_v1 (must draw our own).
    //   2. Compositor configures mode client_side: GNOME/Mutter does this unconditionally,
    //      and KDE/Sway do it when we ask for client_side because the window has a TitleBar.
    //   3. MAUI_PREFER_CSD=1 is set (testing override on KDE/Sway).
    // Read by SkiaRenderingEngine to reserve the titlebar strip and by the pointer
    // handler to intercept titlebar clicks for move/resize/buttons.
    private readonly ClientDecorations _csd = new();

    // MAUI_PREFER_CSD=1: no decoration object, always client-side.
    private bool _forceCsd;
    // Whether the window's MAUI window has a TitleBar (asks for client-side mode).
    private bool _clientDecorationsWanted;
    // The compositor's last zxdg_toplevel_decoration_v1.configure mode, null before the first.
    private uint? _configuredDecorationMode;

    public bool UseCsd => _csd.Active;

    /// <summary>The decoration state the renderer and the pointer hit-test share.</summary>
    internal ClientDecorations Decorations => _csd;

    ClientDecorations IClientDecoratedWindow.Decorations => _csd;

    // Cached titlebar button bounds (in logical pixels). Recomputed each frame
    // by WaylandCsdRenderer.DrawTitlebar; consulted by the pointer hit-test
    // before forwarding events to MAUI views.
    internal SKRect CsdCloseButtonBounds { get => _csd.CloseButtonBounds; set => _csd.CloseButtonBounds = value; }
    internal SKRect CsdMaximizeButtonBounds { get => _csd.MaximizeButtonBounds; set => _csd.MaximizeButtonBounds = value; }
    internal SKRect CsdMinimizeButtonBounds { get => _csd.MinimizeButtonBounds; set => _csd.MinimizeButtonBounds = value; }

    // Logical titlebar height. Kept as a constant so SkiaRenderingEngine and the
    // pointer hit-test agree on the inset without a back-channel.
    internal const float CsdTitlebarHeightLogical = ClientDecorations.DefaultTitlebarHeight;

    /// <summary>
    /// The titlebar's height this frame (logical pixels): <see cref="CsdTitlebarHeightLogical"/>,
    /// or taller when the window's TitleBar (MAUI's Window.TitleBar) is (set by the renderer).
    /// </summary>
    internal float CsdTitlebarHeight
    {
        get => _csd.TitlebarHeight;
        set => _csd.TitlebarHeight = value;
    }

    /// <summary>
    /// Asked for a left press in the titlebar (logical window coordinates): true when an
    /// interactive part of the window's TitleBar is there (MAUI's TitleBar passthrough
    /// elements, the parts Windows keeps out of the drag region), so the press reaches the
    /// view instead of moving the window. Set by the window's context.
    /// </summary>
    internal Func<float, float, bool>? CsdTitleBarPassthrough
    {
        get => _csd.TitleBarPassthrough;
        set => _csd.TitleBarPassthrough = value;
    }

    /// <summary>
    /// With a TitleBar the window asks the compositor for client-side mode, so the TitleBar
    /// fills OpenMaui's own titlebar (KDE and Sway otherwise draw their own decorations);
    /// without one it asks for server-side mode again. The compositor answers with a
    /// configure: when it keeps server-side decorations, the TitleBar stays a strip at the top
    /// of the client area. Only requests are sent and flushed here; the answer arrives through
    /// the run loop's normal dispatch.
    /// </summary>
    public void SetClientDecorationsWanted(bool wanted)
    {
        if (_clientDecorationsWanted == wanted)
            return;
        _clientDecorationsWanted = wanted;
        // Without the protocol (or with MAUI_PREFER_CSD) the window is client-decorated already.
        if (_forceCsd || _toplevelDecoration == IntPtr.Zero)
            return;
        uint mode = WaylandDecorationPolicy.RequestedMode(wanted);
        DiagnosticLog.Debug("WaylandWindow", $"TitleBar {(wanted ? "set" : "removed")}; requesting decoration mode {(mode == ZXDG_TOPLEVEL_DECORATION_V1_MODE_CLIENT_SIDE ? "client_side" : "server_side")}");
        wl_proxy_marshal(_toplevelDecoration, ZXDG_TOPLEVEL_DECORATION_V1_SET_MODE, mode);
        if (_display != IntPtr.Zero)
            wl_display_flush(_display);
    }

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
        _forceCsd = forceCsd;

        // No decoration manager at all (e.g. GNOME 3.x pre-extension, custom
        // compositors) means there's no protocol to negotiate SSD over —
        // we must draw client-side.
        if (_decorationManager == IntPtr.Zero || _xdgToplevel == IntPtr.Zero)
        {
            _csd.Active = true;
            DiagnosticLog.Debug("WaylandWindow", "No zxdg_decoration_manager_v1; using CSD");
            return;
        }

        if (forceCsd)
        {
            _csd.Active = true;
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
            _csd.Active = true;
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

        // Request server-side mode, or client-side when the window already has a TitleBar.
        // The compositor decides; we observe its choice via the configure event handler above.
        wl_proxy_marshal(_toplevelDecoration, ZXDG_TOPLEVEL_DECORATION_V1_SET_MODE,
            WaylandDecorationPolicy.RequestedMode(_clientDecorationsWanted));
    }

    private static void OnDecorationConfigure(IntPtr data, IntPtr proxy, uint mode)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        window._configuredDecorationMode = mode;
        bool wantsCsd = WaylandDecorationPolicy.UsesClientDecorations(hasDecorationProtocol: true, window._forceCsd, mode);
        if (window._clientDecorationsWanted && !wantsCsd)
            DiagnosticLog.Info("WaylandWindow", "Compositor kept server-side decorations; the window's TitleBar is shown below its titlebar");
        if (window._csd.Active == wantsCsd) return;

        window._csd.Active = wantsCsd;
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
        window.ApplyBufferScale(newScale);
    }

    /// <summary>
    /// Re-renders the window at a new device scale while keeping its logical
    /// size: the buffer becomes logical x scale pixels and the viewport keeps
    /// the compositor-visible size logical. <see cref="ScaleChanged"/> fires
    /// before the <see cref="Resized"/> that carries the new buffer size (see
    /// <see cref="Services.IScaleAwareDisplayWindow"/>). Without wp_viewporter
    /// the buffer cannot be decoupled from the surface size, so the scale is
    /// only recorded.
    /// </summary>
    internal void ApplyBufferScale(float newScale)
    {
        if (newScale <= 0f || Math.Abs(newScale - _bufferToLogicalScale) < 0.01f)
            return;
        if (_viewport == IntPtr.Zero)
        {
            DiagnosticLog.Debug("WaylandWindow", "Scale change ignored: compositor has no wp_viewporter");
            return;
        }

        float oldScale = _bufferToLogicalScale;
        var (logicalW, logicalH, bufferW, bufferH) = RescaleSize(_width, _height, oldScale, newScale);
        _bufferToLogicalScale = newScale;
        DiagnosticLog.Info("WaylandWindow", $"Scale {oldScale:0.##} -> {newScale:0.##}: logical {logicalW}x{logicalH}, buffer {bufferW}x{bufferH}");

        ScaleChanged?.Invoke(this, newScale);

        // wp_viewport.set_destination (opcode 2) is double-buffered: it applies
        // with the commit that attaches the new-size buffer.
        wl_proxy_marshal(_viewport, 2, logicalW, logicalH);

        // Before the first configure the pending size must follow too, or the
        // first configure would resize back to the old-scale buffer.
        if (!_configured)
        {
            _pendingWidth = bufferW;
            _pendingHeight = bufferH;
        }
        ResizeBuffer(bufferW, bufferH);
        Exposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Buffer size for a new scale that keeps the logical size (rounded to
    /// whole logical pixels, never below 1).
    /// </summary>
    internal static (int LogicalWidth, int LogicalHeight, int BufferWidth, int BufferHeight) RescaleSize(
        int bufferWidth, int bufferHeight, float oldScale, float newScale)
    {
        if (oldScale <= 0f) oldScale = 1f;
        int logicalW = Math.Max(1, (int)Math.Round(bufferWidth / oldScale));
        int logicalH = Math.Max(1, (int)Math.Round(bufferHeight / oldScale));
        return (logicalW, logicalH,
            Math.Max(1, (int)Math.Round(logicalW * newScale)),
            Math.Max(1, (int)Math.Round(logicalH * newScale)));
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

    /// <summary>Width of the strip along each window edge that resizes the window, in logical pixels.</summary>
    internal const float CsdResizeMargin = ClientDecorations.ResizeMargin;

    private float LogicalWidthForCsd => _bufferToLogicalScale > 0f ? _width / _bufferToLogicalScale : _width;
    private float LogicalHeightForCsd => _bufferToLogicalScale > 0f ? _height / _bufferToLogicalScale : _height;

    /// <summary>
    /// The window edge (an XDG_TOPLEVEL_RESIZE_EDGE value) under a point in logical pixels, or
    /// NONE: only while we draw the decorations and the window is not maximized. The press that
    /// starts a resize and the resize cursor shown on hover both use it.
    /// </summary>
    internal uint CsdEdgeAt(float x, float y)
    {
        if (!_csd.Active || _bufferToLogicalScale <= 0f)
            return XDG_TOPLEVEL_RESIZE_EDGE_NONE;
        return ClientDecorations.ToXdgResizeEdge(
            ClientDecorations.EdgeAt(x, y, LogicalWidthForCsd, LogicalHeightForCsd, _isMaximized));
    }

    /// <summary>
    /// CSD pointer hit-test for a left press. Returns true if the press was consumed by the
    /// titlebar (move/resize/button); false to let the event propagate to MAUI views.
    /// Coordinates are in logical pixels.
    /// </summary>
    internal bool HandleCsdPointerDown(float x, float y, uint serial)
        => HandleCsdPointerDown(x, y, serial, Microsoft.Maui.Platform.PointerButton.Left, time: 0);

    /// <summary>
    /// CSD pointer hit-test. A left press resizes on an edge, works the window buttons, moves the
    /// window on the titlebar (a double-click toggles maximize) and leaves the TitleBar's
    /// interactive parts and the page to the views; a right press on the titlebar's move area
    /// opens the compositor's window menu (xdg_toplevel.show_window_menu). Returns true when the
    /// decorations took the press. <paramref name="time"/> is the event's millisecond timestamp.
    /// </summary>
    internal bool HandleCsdPointerDown(float x, float y, uint serial, Microsoft.Maui.Platform.PointerButton button, uint time)
    {
        if (!_csd.Active) return false;

        var hit = _csd.HitTest(x, y, LogicalWidthForCsd, LogicalHeightForCsd, _isMaximized);

        if (button == Microsoft.Maui.Platform.PointerButton.Right)
        {
            _csd.ResetClickTracking();
            if (hit.Region != CsdRegion.Move)
                return false;
            ShowWindowMenu(serial, x, y);
            return true;
        }
        if (button != Microsoft.Maui.Platform.PointerButton.Left)
            return false;

        switch (hit.Region)
        {
            case CsdRegion.ResizeEdge:
                _csd.ResetClickTracking();
                RequestCsdResize(serial, ClientDecorations.ToXdgResizeEdge(hit.Edge));
                return true;
            case CsdRegion.Close:
                _csd.ResetClickTracking();
                CloseFromCsd();
                return true;
            case CsdRegion.Maximize:
                _csd.ResetClickTracking();
                ToggleMaximize();
                return true;
            case CsdRegion.Minimize:
                _csd.ResetClickTracking();
                Minimize();
                return true;
            case CsdRegion.Move:
                // time 0: the caller has no timestamp (no double-click tracking).
                if (time != 0 && _csd.RegisterMoveClick(time, x, y))
                    ToggleMaximize();
                else
                    RequestCsdMove(serial);
                return true;
            default:
                // An interactive part of the window's TitleBar (Windows' passthrough region)
                // or the page takes the press.
                _csd.ResetClickTracking();
                return false;
        }
    }

    /// <summary>Opens the compositor's window menu at a surface point (logical pixels).</summary>
    internal void ShowWindowMenu(uint serial, float x, float y)
    {
        if (_xdgToplevel == IntPtr.Zero || _seat == IntPtr.Zero) return;
        wl_proxy_marshal(_xdgToplevel, XDG_TOPLEVEL_SHOW_WINDOW_MENU, _seat, serial, (int)x, (int)y);
    }

    #endregion
}
