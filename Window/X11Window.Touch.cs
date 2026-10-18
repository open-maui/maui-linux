// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Interop;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>
/// XInput 2.2 touch. Without it an X server turns a touchscreen into emulated core button and
/// motion events with no device information, so OpenMaui reported touch as mouse. With touch
/// selected through XI2 on the window, the server delivers XI_TouchBegin/Update/End to us and no
/// longer emulates the pointer for those touches to this window (XI 2.2 "Pointer emulation":
/// emulation is for clients that do not select touch). Like the Wayland window, the first finger
/// drives press/move/release marked <see cref="PointerDeviceKind.Touch"/>; other fingers are
/// ignored until it lifts. The window only selects touch on its own window (no touch grab), so it
/// never has to accept or reject sequences with XIAllowTouchEvents; a window manager holding a
/// passive grab passes the sequence on when it rejects it.
/// </summary>
public partial class X11Window
{
    private int _xiOpcode = -1;
    private readonly X11TouchTracker _touch = new();

    /// <summary>Whether XI2 touch events are selected on this window.</summary>
    internal bool HasXInput2Touch => _xiOpcode >= 0;

    private unsafe void InitializeXInput2()
    {
        if (Environment.GetEnvironmentVariable("OPENMAUI_X11_NO_XI2") == "1")
        {
            DiagnosticLog.Info("X11Window", "XInput2 touch disabled by OPENMAUI_X11_NO_XI2");
            return;
        }

        try
        {
            if (!XInput2.XQueryExtension(_display, "XInputExtension", out int opcode, out _, out _))
            {
                DiagnosticLog.Info("X11Window", "XInputExtension not present; touch arrives as emulated mouse events");
                return;
            }

            int major = 2, minor = 2;
            if (XInput2.XIQueryVersion(_display, ref major, ref minor) != 0 || !X11TouchTracker.SupportsTouch(major, minor))
            {
                DiagnosticLog.Info("X11Window", $"XInput {major}.{minor} has no touch events (needs 2.2); touch arrives as emulated mouse events");
                return;
            }

            var mask = XInput2.BuildMask(XInput2.XI_TouchBegin, XInput2.XI_TouchUpdate, XInput2.XI_TouchEnd);
            int status;
            fixed (byte* maskPtr = mask)
            {
                var eventMask = new XInput2.XIEventMask
                {
                    DeviceId = XInput2.XIAllMasterDevices,
                    MaskLen = mask.Length,
                    Mask = (IntPtr)maskPtr,
                };
                status = XInput2.XISelectEvents(_display, _window, &eventMask, 1);
            }
            if (status != 0)
            {
                DiagnosticLog.Info("X11Window", $"XISelectEvents for touch failed ({status}); touch arrives as emulated mouse events");
                return;
            }

            _xiOpcode = opcode;
            DiagnosticLog.Debug("X11Window", $"XInput {major}.{minor} touch selected (opcode {opcode})");
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            DiagnosticLog.Info("X11Window", $"libXi unavailable ({ex.Message}); touch arrives as emulated mouse events");
        }
    }

    /// <summary>
    /// Core button 1 while a finger is down is pointer emulation of that touch, which the views
    /// already received as touch.
    /// </summary>
    private bool IsTouchEmulatedButton(uint button) => button == 1 && _touch.IsTouchDown;

    private unsafe void HandleGenericEvent(ref XEvent xEvent)
    {
        if (_xiOpcode < 0)
            return;
        fixed (XEvent* ev = &xEvent)
        {
            var cookie = (XInput2.XGenericEventCookie*)ev;
            if (cookie->Extension != _xiOpcode)
                return;
            if (!XInput2.XGetEventData(_display, cookie))
                return;
            try
            {
                if (cookie->EvType is XInput2.XI_TouchBegin or XInput2.XI_TouchUpdate or XInput2.XI_TouchEnd
                    && cookie->Data != IntPtr.Zero)
                {
                    HandleTouch(XInput2.ReadTouchEvent(cookie->Data));
                }
            }
            finally
            {
                XInput2.XFreeEventData(_display, cookie);
            }
        }
    }

    private void HandleTouch(in X11TouchEvent touch)
    {
        if (touch.EvType == XInput2.XI_TouchBegin && touch.EventWindow == _window)
        {
            DragDropService.Default.NoteInputTime(touch.Time);
            NoteUserTime(touch.Time);
        }

        switch (_touch.Handle(touch, _window, TouchPressOnDecorations))
        {
            case X11TouchAction.Press:
                // The finger arrives where it presses: the views see it over them before the press.
                PointerMoved?.Invoke(this, TouchArgs());
                PointerPressed?.Invoke(this, TouchArgs(PointerButton.Left));
                break;
            case X11TouchAction.Move:
                PointerMoved?.Invoke(this, TouchArgs());
                break;
            case X11TouchAction.Release:
                PointerReleased?.Invoke(this, TouchArgs(PointerButton.Left));
                break;
        }
    }

    /// <summary>A touch on the client-drawn titlebar, buttons or edges acts as a left click there.</summary>
    private bool TouchPressOnDecorations(X11TouchEvent touch)
    {
        if (!_decorations.State.Active)
            return false;
        float scale = _scale > 0f ? _scale : 1f;
        return _decorations.HandlePress(PointerButton.Left,
            (float)(touch.EventX / scale), (float)(touch.EventY / scale),
            (int)Math.Round(touch.RootX), (int)Math.Round(touch.RootY), touch.Time,
            LogicalWidth, LogicalHeight, _isMaximized);
    }

    // Device pixels, as the core pointer path reports them.
    private PointerEventArgs TouchArgs(PointerButton button = PointerButton.None) =>
        new(_touch.X, _touch.Y, button) { PointerDeviceType = PointerDeviceKind.Touch };
}
