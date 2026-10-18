// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Interop;

/// <summary>
/// The parts of the X Input Extension 2 (libXi) OpenMaui uses: version negotiation, event
/// selection for touch (XI 2.2) and reading XIDeviceEvent payloads out of GenericEvent cookies.
/// </summary>
internal static partial class XInput2
{
    private const string LibXi = "libXi.so.6";
    private const string LibX11 = "libX11.so.6";

    /// <summary>X event type carrying extension events (XGenericEventCookie).</summary>
    public const int GenericEvent = 35;

    // XI2 event types (XI2.h).
    public const int XI_ButtonPress = 4;
    public const int XI_ButtonRelease = 5;
    public const int XI_Motion = 6;
    public const int XI_TouchBegin = 18;
    public const int XI_TouchUpdate = 19;
    public const int XI_TouchEnd = 20;
    public const int XI_TouchOwnership = 21;
    public const int XI_LASTEVENT = 26;

    // Device ids for XISelectEvents.
    public const int XIAllDevices = 0;
    public const int XIAllMasterDevices = 1;

    // XIDeviceEvent.flags.
    public const int XIPointerEmulated = 1 << 16;
    public const int XITouchPendingEnd = 1 << 16;
    public const int XITouchEmulatingPointer = 1 << 17;

    [StructLayout(LayoutKind.Sequential)]
    public struct XIEventMask
    {
        public int DeviceId;
        public int MaskLen;
        public IntPtr Mask; // unsigned char*
    }

    /// <summary>XGenericEventCookie (Xlib.h); overlays the XEvent the cookie arrived in.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XGenericEventCookie
    {
        public int Type;
        public nuint Serial;
        public int SendEvent;
        public IntPtr Display;
        public int Extension;
        public int EvType;
        public uint Cookie;
        public IntPtr Data;
    }

    /// <summary>
    /// The leading fields of XIDeviceEvent (XI2.h) up to <c>flags</c>; the button, valuator,
    /// modifier and group state that follow are not read.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XIDeviceEventHead
    {
        public int Type;
        public nuint Serial;
        public int SendEvent;
        public IntPtr Display;
        public int Extension;
        public int EvType;
        public nuint Time;
        public int DeviceId;
        public int SourceId;
        public int Detail;
        public IntPtr Root;
        public IntPtr Event;
        public IntPtr Child;
        public double RootX;
        public double RootY;
        public double EventX;
        public double EventY;
        public int Flags;
    }

    [LibraryImport(LibX11, StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool XQueryExtension(IntPtr display, string name, out int majorOpcode, out int firstEvent, out int firstError);

    [LibraryImport(LibX11)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool XGetEventData(IntPtr display, XGenericEventCookie* cookie);

    [LibraryImport(LibX11)]
    public static unsafe partial void XFreeEventData(IntPtr display, XGenericEventCookie* cookie);

    /// <summary>Negotiates the XI2 version; on return the pair holds the version the server supports. 0 = Success.</summary>
    [LibraryImport(LibXi)]
    public static partial int XIQueryVersion(IntPtr display, ref int major, ref int minor);

    [LibraryImport(LibXi)]
    public static unsafe partial int XISelectEvents(IntPtr display, IntPtr window, XIEventMask* masks, int numMasks);

    /// <summary>The XI2 event mask bytes selecting <paramref name="eventTypes"/> (XISetMask).</summary>
    public static byte[] BuildMask(params int[] eventTypes)
    {
        var mask = new byte[(XI_LASTEVENT >> 3) + 1];
        foreach (var type in eventTypes)
            mask[type >> 3] |= (byte)(1 << (type & 7));
        return mask;
    }

    /// <summary>Reads the head of the XIDeviceEvent a cookie's data points at.</summary>
    public static unsafe X11TouchEvent ReadTouchEvent(IntPtr data)
    {
        var e = (XIDeviceEventHead*)data;
        return new X11TouchEvent(
            e->EvType,
            e->DeviceId,
            e->SourceId,
            unchecked((uint)e->Detail),
            e->Event,
            e->EventX,
            e->EventY,
            e->RootX,
            e->RootY,
            e->Flags,
            (ulong)e->Time);
    }
}

/// <summary>
/// One XI2 touch event as the X11 window consumes it: the event type (XI_TouchBegin/Update/End),
/// the touch sequence id (XIDeviceEvent.detail), the window it was delivered to, its window
/// and root coordinates in device pixels (fractional), flags and X server time.
/// </summary>
internal readonly record struct X11TouchEvent(
    int EvType,
    int DeviceId,
    int SourceId,
    uint TouchId,
    IntPtr EventWindow,
    double EventX,
    double EventY,
    double RootX,
    double RootY,
    int Flags,
    ulong Time);
