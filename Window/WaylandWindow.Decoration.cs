// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Services;

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

    private const uint ZXDG_TOPLEVEL_DECORATION_V1_MODE_SERVER_SIDE = 2;

    private static IntPtr _zxdg_decoration_manager_v1_interface;
    private static IntPtr _zxdg_toplevel_decoration_v1_interface;
    private static GCHandle _decorationManagerHandle;
    private static GCHandle _decorationHandle;
    private static IntPtr _decorationManagerName;
    private static IntPtr _decorationName;

    private IntPtr _decorationManager;
    private IntPtr _toplevelDecoration;

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
        if (_decorationManager == IntPtr.Zero || _xdgToplevel == IntPtr.Zero)
            return;

        // get_toplevel_decoration: signature "no" → NULL placeholder for new_id, then the toplevel object.
        _toplevelDecoration = wl_proxy_marshal_constructor(
            _decorationManager,
            ZXDG_DECORATION_MANAGER_V1_GET_TOPLEVEL_DECORATION,
            _zxdg_toplevel_decoration_v1_interface,
            IntPtr.Zero,
            _xdgToplevel);

        if (_toplevelDecoration == IntPtr.Zero) return;

        // Request server-side mode. The compositor may downgrade to client-side
        // (GNOME does); we accept whatever it picks since the configure event
        // is informational and CSD-on-GNOME is a follow-up.
        wl_proxy_marshal(_toplevelDecoration, ZXDG_TOPLEVEL_DECORATION_V1_SET_MODE,
            ZXDG_TOPLEVEL_DECORATION_V1_MODE_SERVER_SIDE);
    }

    private void DisposeDecoration()
    {
        if (_toplevelDecoration != IntPtr.Zero)
        {
            wl_proxy_marshal(_toplevelDecoration, ZXDG_TOPLEVEL_DECORATION_V1_DESTROY);
            wl_proxy_destroy(_toplevelDecoration);
            _toplevelDecoration = IntPtr.Zero;
        }
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

        _fractionalScaleListener = new FractionalScaleListener
        {
            PreferredScale = Marshal.GetFunctionPointerForDelegate<FractionalScalePreferredDelegate>(OnFractionalScalePreferred),
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
}
