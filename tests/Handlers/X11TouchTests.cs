// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Interop;
using Microsoft.Maui.Platform.Linux.Window;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// XInput 2.2 touch on X11, headless: the event mask, the XIDeviceEvent and cookie layouts the
/// window reads, and the first-finger tracking that turns touch sequences into OpenMaui's pointer
/// (as the Wayland window does with wl_touch).
/// </summary>
public class X11TouchTests
{
    private static readonly IntPtr Window = (IntPtr)0x4200001;
    private static readonly IntPtr OtherWindow = (IntPtr)0x4200099;

    private static X11TouchEvent Touch(int evType, uint id, double x, double y, IntPtr? window = null, ulong time = 1000) =>
        new(evType, DeviceId: 2, SourceId: 11, TouchId: id, EventWindow: window ?? Window,
            EventX: x, EventY: y, RootX: x + 100, RootY: y + 50, Flags: 0, Time: time);

    [Fact]
    public void The_touch_mask_selects_begin_update_and_end()
    {
        var mask = XInput2.BuildMask(XInput2.XI_TouchBegin, XInput2.XI_TouchUpdate, XInput2.XI_TouchEnd);

        mask.Should().HaveCount(4); // XIMaskLen(XI_LASTEVENT)
        mask[0].Should().Be(0);
        mask[1].Should().Be(0);
        mask[2].Should().Be(0b0001_1100); // bits 18, 19, 20
        mask[3].Should().Be(0);
    }

    [Fact]
    public void Touch_events_need_XInput_2_2()
    {
        X11TouchTracker.SupportsTouch(2, 0).Should().BeFalse();
        X11TouchTracker.SupportsTouch(2, 1).Should().BeFalse();
        X11TouchTracker.SupportsTouch(2, 2).Should().BeTrue();
        X11TouchTracker.SupportsTouch(2, 4).Should().BeTrue();
        X11TouchTracker.SupportsTouch(3, 0).Should().BeTrue();
    }

    [Fact]
    public void The_cookie_and_device_event_layouts_match_Xlib_on_LP64()
    {
        Marshal.OffsetOf<XInput2.XGenericEventCookie>(nameof(XInput2.XGenericEventCookie.Extension)).ToInt32().Should().Be(32);
        Marshal.OffsetOf<XInput2.XGenericEventCookie>(nameof(XInput2.XGenericEventCookie.EvType)).ToInt32().Should().Be(36);
        Marshal.OffsetOf<XInput2.XGenericEventCookie>(nameof(XInput2.XGenericEventCookie.Cookie)).ToInt32().Should().Be(40);
        Marshal.OffsetOf<XInput2.XGenericEventCookie>(nameof(XInput2.XGenericEventCookie.Data)).ToInt32().Should().Be(48);
        Marshal.SizeOf<XInput2.XGenericEventCookie>().Should().BeLessThanOrEqualTo(192); // fits in an XEvent

        Marshal.OffsetOf<XInput2.XIDeviceEventHead>(nameof(XInput2.XIDeviceEventHead.Time)).ToInt32().Should().Be(40);
        Marshal.OffsetOf<XInput2.XIDeviceEventHead>(nameof(XInput2.XIDeviceEventHead.DeviceId)).ToInt32().Should().Be(48);
        Marshal.OffsetOf<XInput2.XIDeviceEventHead>(nameof(XInput2.XIDeviceEventHead.Detail)).ToInt32().Should().Be(56);
        Marshal.OffsetOf<XInput2.XIDeviceEventHead>(nameof(XInput2.XIDeviceEventHead.Event)).ToInt32().Should().Be(72);
        Marshal.OffsetOf<XInput2.XIDeviceEventHead>(nameof(XInput2.XIDeviceEventHead.RootX)).ToInt32().Should().Be(88);
        Marshal.OffsetOf<XInput2.XIDeviceEventHead>(nameof(XInput2.XIDeviceEventHead.EventX)).ToInt32().Should().Be(104);
        Marshal.OffsetOf<XInput2.XIDeviceEventHead>(nameof(XInput2.XIDeviceEventHead.Flags)).ToInt32().Should().Be(120);

        Marshal.SizeOf<XInput2.XIEventMask>().Should().Be(16);
    }

    [Fact]
    public void A_device_event_decodes_from_its_C_layout()
    {
        // An XIDeviceEvent as libXi hands it out (XGetEventData), written at the C offsets.
        var buffer = Marshal.AllocHGlobal(200);
        try
        {
            for (int i = 0; i < 200; i++)
                Marshal.WriteByte(buffer, i, 0);
            Marshal.WriteInt32(buffer, 0, XInput2.GenericEvent);
            Marshal.WriteInt32(buffer, 36, XInput2.XI_TouchUpdate);
            Marshal.WriteInt64(buffer, 40, 123456789L);           // time
            Marshal.WriteInt32(buffer, 48, 2);                     // deviceid (master pointer)
            Marshal.WriteInt32(buffer, 52, 14);                    // sourceid (touchscreen)
            Marshal.WriteInt32(buffer, 56, 77);                    // detail = touch id
            Marshal.WriteInt64(buffer, 64, 0x1EE);                 // root
            Marshal.WriteInt64(buffer, 72, Window.ToInt64());      // event
            Marshal.WriteInt64(buffer, 88, BitConverter.DoubleToInt64Bits(1250.5));
            Marshal.WriteInt64(buffer, 96, BitConverter.DoubleToInt64Bits(830.25));
            Marshal.WriteInt64(buffer, 104, BitConverter.DoubleToInt64Bits(250.5));
            Marshal.WriteInt64(buffer, 112, BitConverter.DoubleToInt64Bits(130.25));
            Marshal.WriteInt32(buffer, 120, XInput2.XITouchEmulatingPointer);

            var e = XInput2.ReadTouchEvent(buffer);

            e.EvType.Should().Be(XInput2.XI_TouchUpdate);
            e.Time.Should().Be(123456789UL);
            e.DeviceId.Should().Be(2);
            e.SourceId.Should().Be(14);
            e.TouchId.Should().Be(77u);
            e.EventWindow.Should().Be(Window);
            e.RootX.Should().Be(1250.5);
            e.RootY.Should().Be(830.25);
            e.EventX.Should().Be(250.5);
            e.EventY.Should().Be(130.25);
            e.Flags.Should().Be(XInput2.XITouchEmulatingPointer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Fact]
    public void The_first_finger_presses_moves_and_releases_at_its_device_pixels()
    {
        var tracker = new X11TouchTracker();

        tracker.Handle(Touch(XInput2.XI_TouchBegin, 5, 10.5, 20.25), Window, null).Should().Be(X11TouchAction.Press);
        tracker.IsTouchDown.Should().BeTrue();
        tracker.X.Should().Be(10.5f);
        tracker.Y.Should().Be(20.25f);

        tracker.Handle(Touch(XInput2.XI_TouchUpdate, 5, 40, 60), Window, null).Should().Be(X11TouchAction.Move);
        tracker.X.Should().Be(40f);
        tracker.Y.Should().Be(60f);

        tracker.Handle(Touch(XInput2.XI_TouchEnd, 5, 42, 61), Window, null).Should().Be(X11TouchAction.Release);
        tracker.X.Should().Be(42f);
        tracker.IsTouchDown.Should().BeFalse();
    }

    [Fact]
    public void Other_fingers_are_ignored_until_the_first_lifts()
    {
        var tracker = new X11TouchTracker();
        tracker.Handle(Touch(XInput2.XI_TouchBegin, 1, 10, 10), Window, null);

        tracker.Handle(Touch(XInput2.XI_TouchBegin, 2, 300, 300), Window, null).Should().Be(X11TouchAction.None);
        tracker.Handle(Touch(XInput2.XI_TouchUpdate, 2, 310, 310), Window, null).Should().Be(X11TouchAction.None);
        tracker.X.Should().Be(10f, "the second finger does not move the pointer");
        tracker.Handle(Touch(XInput2.XI_TouchEnd, 2, 310, 310), Window, null).Should().Be(X11TouchAction.None);
        tracker.IsTouchDown.Should().BeTrue();

        tracker.Handle(Touch(XInput2.XI_TouchEnd, 1, 10, 10), Window, null).Should().Be(X11TouchAction.Release);

        // A finger that goes down after the first lifted drives the pointer.
        tracker.Handle(Touch(XInput2.XI_TouchBegin, 3, 50, 50), Window, null).Should().Be(X11TouchAction.Press);
    }

    [Fact]
    public void Touches_of_other_windows_are_ignored()
    {
        var tracker = new X11TouchTracker();
        tracker.Handle(Touch(XInput2.XI_TouchBegin, 1, 10, 10, OtherWindow), Window, null).Should().Be(X11TouchAction.None);
        tracker.IsTouchDown.Should().BeFalse();
    }

    [Fact]
    public void A_finger_the_decorations_take_produces_no_pointer_events()
    {
        var tracker = new X11TouchTracker();
        X11TouchEvent? offered = null;

        var action = tracker.Handle(Touch(XInput2.XI_TouchBegin, 9, 400, 12), Window, e => { offered = e; return true; });

        action.Should().Be(X11TouchAction.Decorations);
        offered.Should().NotBeNull();
        offered!.Value.RootX.Should().Be(500, "the decorations get root coordinates for _NET_WM_MOVERESIZE");
        tracker.Handle(Touch(XInput2.XI_TouchUpdate, 9, 420, 14), Window, null).Should().Be(X11TouchAction.None);
        tracker.Handle(Touch(XInput2.XI_TouchBegin, 10, 100, 100), Window, null).Should().Be(X11TouchAction.None,
            "the titlebar finger is still the first finger");
        tracker.Handle(Touch(XInput2.XI_TouchEnd, 9, 420, 14), Window, null).Should().Be(X11TouchAction.None);
        tracker.IsTouchDown.Should().BeFalse();

        tracker.Handle(Touch(XInput2.XI_TouchBegin, 11, 100, 100), Window, _ => false).Should().Be(X11TouchAction.Press);
    }
}
