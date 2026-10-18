// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>
/// wl_touch: a touchscreen. Wayland compositors do not turn touch into pointer events (unlike
/// an X server's pointer emulation), so without a wl_touch a touchscreen does nothing. The
/// first finger down drives the window's pointer events, marked
/// <see cref="PointerDeviceKind.Touch"/>; further fingers are ignored until it lifts (OpenMaui's
/// views take one pointer).
/// </summary>
public partial class WaylandWindow
{
    private static IntPtr _wl_touch_interface;

    // wl_seat.get_touch
    private const uint WL_SEAT_GET_TOUCH = 2;
    private const uint WL_SEAT_CAPABILITY_TOUCH = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct WlTouchListener
    {
        public IntPtr Down;
        public IntPtr Up;
        public IntPtr Motion;
        public IntPtr Frame;
        public IntPtr Cancel;
        public IntPtr Shape;        // v6
        public IntPtr Orientation;  // v6
    }

    private delegate void TouchDownDelegate(IntPtr data, IntPtr touch, uint serial, uint time, IntPtr surface, int id, int x, int y);
    private delegate void TouchUpDelegate(IntPtr data, IntPtr touch, uint serial, uint time, int id);
    private delegate void TouchMotionDelegate(IntPtr data, IntPtr touch, uint time, int id, int x, int y);
    private delegate void TouchFrameDelegate(IntPtr data, IntPtr touch);
    private delegate void TouchCancelDelegate(IntPtr data, IntPtr touch);
    private delegate void TouchShapeDelegate(IntPtr data, IntPtr touch, int id, int major, int minor);
    private delegate void TouchOrientationDelegate(IntPtr data, IntPtr touch, int id, int orientation);

    private IntPtr _touch;
    private WlTouchListener _touchListener;
    private GCHandle _touchListenerHandle;
    // Rooted: libwayland holds the function pointers for the proxy's lifetime (see SetupPointer).
    private TouchDownDelegate? _touchDownDelegate;
    private TouchUpDelegate? _touchUpDelegate;
    private TouchMotionDelegate? _touchMotionDelegate;
    private TouchFrameDelegate? _touchFrameDelegate;
    private TouchCancelDelegate? _touchCancelDelegate;
    private TouchShapeDelegate? _touchShapeDelegate;
    private TouchOrientationDelegate? _touchOrientationDelegate;

    // The finger driving the pointer events, or null.
    private int? _primaryTouchId;
    private float _touchX, _touchY;

    private static void LoadTouchInterface(IntPtr libwaylandClient)
    {
        _wl_touch_interface = dlsym(libwaylandClient, "wl_touch_interface");
    }

    /// <summary>Binds the seat's touchscreen, once the seat says it has one.</summary>
    private void SetupTouch(IntPtr seat)
    {
        if (_touch != IntPtr.Zero || _wl_touch_interface == IntPtr.Zero)
            return;
        _touch = wl_proxy_marshal_constructor(seat, WL_SEAT_GET_TOUCH, _wl_touch_interface, IntPtr.Zero);
        if (_touch == IntPtr.Zero)
            return;

        // Every event of the bound version needs a slot (libwayland aborts on a NULL one).
        _touchDownDelegate = TouchDown;
        _touchUpDelegate = TouchUp;
        _touchMotionDelegate = TouchMotion;
        _touchFrameDelegate = TouchFrame;
        _touchCancelDelegate = TouchCancel;
        _touchShapeDelegate = TouchShape;
        _touchOrientationDelegate = TouchOrientation;
        _touchListener = new WlTouchListener
        {
            Down = Marshal.GetFunctionPointerForDelegate(_touchDownDelegate),
            Up = Marshal.GetFunctionPointerForDelegate(_touchUpDelegate),
            Motion = Marshal.GetFunctionPointerForDelegate(_touchMotionDelegate),
            Frame = Marshal.GetFunctionPointerForDelegate(_touchFrameDelegate),
            Cancel = Marshal.GetFunctionPointerForDelegate(_touchCancelDelegate),
            Shape = Marshal.GetFunctionPointerForDelegate(_touchShapeDelegate),
            Orientation = Marshal.GetFunctionPointerForDelegate(_touchOrientationDelegate),
        };
        _touchListenerHandle = GCHandle.Alloc(_touchListener, GCHandleType.Pinned);
        wl_proxy_add_listener(_touch, _touchListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));
        DiagnosticLog.Debug("WaylandWindow", "wl_touch bound");
    }

    private void ReleaseTouchListener()
    {
        if (_touchListenerHandle.IsAllocated)
            _touchListenerHandle.Free();
    }

    private static WaylandWindow? WindowOf(IntPtr data)
    {
        var handle = GCHandle.FromIntPtr(data);
        return handle.IsAllocated ? handle.Target as WaylandWindow : null;
    }

    private PointerEventArgs TouchArgs(PointerButton button = PointerButton.None) =>
        new((int)_touchX, (int)_touchY, button) { PointerDeviceType = PointerDeviceKind.Touch };

    private void SetTouchPoint(int x, int y)
    {
        // Surface-logical wl_fixed, scaled to buffer pixels as the pointer's are (see PointerEnter).
        var s = _bufferToLogicalScale;
        _touchX = (x / 256.0f) * s;
        _touchY = (y / 256.0f) * s;
    }

    private static void TouchDown(IntPtr data, IntPtr touch, uint serial, uint time, IntPtr surface, int id, int x, int y)
    {
        if (WindowOf(data) is not { } window || window._primaryTouchId.HasValue)
            return;
        window._primaryTouchId = id;
        window.SetTouchPoint(x, y);
        // A touch serial starts a drag-and-drop or an interactive move as a button press does.
        window._pointerButtonSerial = serial;

        if (window.UseCsd)
        {
            float invScale = window._bufferToLogicalScale > 0f ? 1f / window._bufferToLogicalScale : 1f;
            if (window.HandleCsdPointerDown(window._touchX * invScale, window._touchY * invScale, serial))
            {
                window._primaryTouchId = null;
                return;
            }
        }

        // The finger arrives where it presses: the views see it over them before the press.
        window.PointerMoved?.Invoke(window, window.TouchArgs());
        window.PointerPressed?.Invoke(window, window.TouchArgs(PointerButton.Left));
    }

    private static void TouchUp(IntPtr data, IntPtr touch, uint serial, uint time, int id)
    {
        if (WindowOf(data) is not { } window || window._primaryTouchId != id)
            return;
        window._primaryTouchId = null;
        window.PointerReleased?.Invoke(window, window.TouchArgs(PointerButton.Left));
    }

    private static void TouchMotion(IntPtr data, IntPtr touch, uint time, int id, int x, int y)
    {
        if (WindowOf(data) is not { } window || window._primaryTouchId != id)
            return;
        window.SetTouchPoint(x, y);
        window.PointerMoved?.Invoke(window, window.TouchArgs());
    }

    private static void TouchFrame(IntPtr data, IntPtr touch) { }

    /// <summary>The compositor took the touch over (a gesture of its own): the press ends where it is.</summary>
    private static void TouchCancel(IntPtr data, IntPtr touch)
    {
        if (WindowOf(data) is not { } window || !window._primaryTouchId.HasValue)
            return;
        window._primaryTouchId = null;
        window.PointerReleased?.Invoke(window, window.TouchArgs(PointerButton.Left));
    }

    private static void TouchShape(IntPtr data, IntPtr touch, int id, int major, int minor) { }
    private static void TouchOrientation(IntPtr data, IntPtr touch, int id, int orientation) { }
}
