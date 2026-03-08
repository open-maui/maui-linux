using System;
using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Native;

internal static partial class GdkNative
{
    [Flags]
    public enum GdkEventMask
    {
        ExposureMask = 2,
        PointerMotionMask = 4,
        PointerMotionHintMask = 8,
        ButtonMotionMask = 0x10,
        Button1MotionMask = 0x20,
        Button2MotionMask = 0x40,
        Button3MotionMask = 0x80,
        ButtonPressMask = 0x100,
        ButtonReleaseMask = 0x200,
        KeyPressMask = 0x400,
        KeyReleaseMask = 0x800,
        EnterNotifyMask = 0x1000,
        LeaveNotifyMask = 0x2000,
        FocusChangeMask = 0x4000,
        StructureMask = 0x8000,
        PropertyChangeMask = 0x10000,
        VisibilityNotifyMask = 0x20000,
        ProximityInMask = 0x40000,
        ProximityOutMask = 0x80000,
        SubstructureMask = 0x100000,
        ScrollMask = 0x200000,
        TouchMask = 0x400000,
        SmoothScrollMask = 0x800000,
        AllEventsMask = 0xFFFFFE
    }

    public enum GdkScrollDirection
    {
        Up,
        Down,
        Left,
        Right,
        Smooth
    }

    public struct GdkEventButton
    {
        public int Type;
        public IntPtr Window;
        public sbyte SendEvent;
        public uint Time;
        public double X;
        public double Y;
        public IntPtr Axes;
        public uint State;
        public uint Button;
        public IntPtr Device;
        public double XRoot;
        public double YRoot;
    }

    public struct GdkEventMotion
    {
        public int Type;
        public IntPtr Window;
        public sbyte SendEvent;
        public uint Time;
        public double X;
        public double Y;
        public IntPtr Axes;
        public uint State;
        public short IsHint;
        public IntPtr Device;
        public double XRoot;
        public double YRoot;
    }

    public struct GdkEventKey
    {
        public int Type;
        public IntPtr Window;
        public sbyte SendEvent;
        public uint Time;
        public uint State;
        public uint Keyval;
        public int Length;
        public IntPtr String;
        public ushort HardwareKeycode;
        public byte Group;
        public uint IsModifier;
    }

    public struct GdkEventScroll
    {
        public int Type;
        public IntPtr Window;
        public sbyte SendEvent;
        public uint Time;
        public double X;
        public double Y;
        public uint State;
        public GdkScrollDirection Direction;
        public IntPtr Device;
        public double XRoot;
        public double YRoot;
        public double DeltaX;
        public double DeltaY;
    }

    private const string Lib = "libgdk-3.so.0";

    [LibraryImport("libgdk-3.so.0")]
    public static partial IntPtr gdk_display_get_default();

    [LibraryImport("libgdk-3.so.0")]
    public static partial IntPtr gdk_display_get_name(IntPtr display);

    [LibraryImport("libgdk-3.so.0")]
    public static partial IntPtr gdk_screen_get_default();

    [LibraryImport("libgdk-3.so.0")]
    public static partial int gdk_screen_get_width(IntPtr screen);

    [LibraryImport("libgdk-3.so.0")]
    public static partial int gdk_screen_get_height(IntPtr screen);

    [LibraryImport("libgdk-3.so.0")]
    public static partial void gdk_window_invalidate_rect(IntPtr window, IntPtr rect, [MarshalAs(UnmanagedType.Bool)] bool invalidateChildren);

    [LibraryImport("libgdk-3.so.0")]
    public static partial uint gdk_keyval_to_unicode(uint keyval);
}
