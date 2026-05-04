// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Interop;

/// <summary>
/// P/Invoke declarations for X11 library functions.
/// </summary>
internal static partial class X11
{
    private const string LibX11 = "libX11.so.6";
    private const string LibXext = "libXext.so.6";

    #region Display and Screen

    [LibraryImport(LibX11)]
    public static partial IntPtr XOpenDisplay(IntPtr displayName);

    [LibraryImport(LibX11)]
    public static partial int XCloseDisplay(IntPtr display);

    [LibraryImport(LibX11)]
    public static partial int XDefaultScreen(IntPtr display);

    [LibraryImport(LibX11)]
    public static partial IntPtr XRootWindow(IntPtr display, int screenNumber);

    [LibraryImport(LibX11)]
    public static partial int XDisplayWidth(IntPtr display, int screenNumber);

    [LibraryImport(LibX11)]
    public static partial int XDisplayHeight(IntPtr display, int screenNumber);

    [LibraryImport(LibX11)]
    public static partial int XDefaultDepth(IntPtr display, int screenNumber);

    [LibraryImport(LibX11)]
    public static partial IntPtr XDefaultVisual(IntPtr display, int screenNumber);

    [LibraryImport(LibX11)]
    public static partial IntPtr XDefaultColormap(IntPtr display, int screenNumber);

    [LibraryImport(LibX11)]
    public static partial int XFlush(IntPtr display);

    [LibraryImport(LibX11)]
    public static partial int XSync(IntPtr display, [MarshalAs(UnmanagedType.Bool)] bool discard);

    #endregion

    #region Window Creation and Management

    [LibraryImport(LibX11)]
    public static partial IntPtr XCreateSimpleWindow(
        IntPtr display,
        IntPtr parent,
        int x, int y,
        uint width, uint height,
        uint borderWidth,
        ulong border,
        ulong background);

    [LibraryImport(LibX11)]
    public static partial IntPtr XCreateWindow(
        IntPtr display,
        IntPtr parent,
        int x, int y,
        uint width, uint height,
        uint borderWidth,
        int depth,
        uint windowClass,
        IntPtr visual,
        ulong valueMask,
        ref XSetWindowAttributes attributes);

    [LibraryImport(LibX11)]
    public static partial int XDestroyWindow(IntPtr display, IntPtr window);

    [LibraryImport(LibX11)]
    public static partial int XMapWindow(IntPtr display, IntPtr window);

    [LibraryImport(LibX11)]
    public static partial int XUnmapWindow(IntPtr display, IntPtr window);

    [LibraryImport(LibX11)]
    public static partial int XMoveWindow(IntPtr display, IntPtr window, int x, int y);

    [LibraryImport(LibX11)]
    public static partial int XResizeWindow(IntPtr display, IntPtr window, uint width, uint height);

    [LibraryImport(LibX11)]
    public static partial int XMoveResizeWindow(IntPtr display, IntPtr window, int x, int y, uint width, uint height);

    [LibraryImport(LibX11, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int XStoreName(IntPtr display, IntPtr window, string windowName);

    [LibraryImport(LibX11)]
    public static partial int XRaiseWindow(IntPtr display, IntPtr window);

    [LibraryImport(LibX11)]
    public static partial int XLowerWindow(IntPtr display, IntPtr window);

    #endregion

    #region Event Handling

    [LibraryImport(LibX11)]
    public static partial int XSelectInput(IntPtr display, IntPtr window, long eventMask);

    [LibraryImport(LibX11)]
    public static partial int XNextEvent(IntPtr display, out XEvent eventReturn);

    [LibraryImport(LibX11)]
    public static partial int XPeekEvent(IntPtr display, out XEvent eventReturn);

    [LibraryImport(LibX11)]
    public static partial int XPending(IntPtr display);

    [LibraryImport(LibX11)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool XCheckTypedWindowEvent(IntPtr display, IntPtr window, int eventType, out XEvent eventReturn);

    [LibraryImport(LibX11)]
    public static partial int XSendEvent(IntPtr display, IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool propagate, long eventMask, ref XEvent eventSend);

    #endregion

    #region Keyboard

    [LibraryImport(LibX11)]
    public static partial ulong XKeycodeToKeysym(IntPtr display, int keycode, int index);

    [LibraryImport(LibX11)]
    public static partial int XLookupString(ref XKeyEvent keyEvent, IntPtr bufferReturn, int bytesBuffer, out ulong keysymReturn, IntPtr statusInOut);

    [LibraryImport(LibX11)]
    public static partial int XGrabKeyboard(IntPtr display, IntPtr grabWindow, [MarshalAs(UnmanagedType.Bool)] bool ownerEvents, int pointerMode, int keyboardMode, ulong time);

    [LibraryImport(LibX11)]
    public static partial int XUngrabKeyboard(IntPtr display, ulong time);

    #endregion

    #region Mouse/Pointer

    [LibraryImport(LibX11)]
    public static partial int XGrabPointer(IntPtr display, IntPtr grabWindow, [MarshalAs(UnmanagedType.Bool)] bool ownerEvents, uint eventMask, int pointerMode, int keyboardMode, IntPtr confineTo, IntPtr cursor, ulong time);

    [LibraryImport(LibX11)]
    public static partial int XUngrabPointer(IntPtr display, ulong time);

    [LibraryImport(LibX11)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool XQueryPointer(IntPtr display, IntPtr window, out IntPtr rootReturn, out IntPtr childReturn, out int rootX, out int rootY, out int winX, out int winY, out uint maskReturn);

    [LibraryImport(LibX11)]
    public static partial int XWarpPointer(IntPtr display, IntPtr srcWindow, IntPtr destWindow, int srcX, int srcY, uint srcWidth, uint srcHeight, int destX, int destY);

    #endregion

    #region Atoms and Properties

    [LibraryImport(LibX11, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr XInternAtom(IntPtr display, string atomName, [MarshalAs(UnmanagedType.Bool)] bool onlyIfExists);

    [LibraryImport(LibX11)]
    public static partial int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type, int format, int mode, IntPtr data, int nelements);

    [LibraryImport(LibX11)]
    public static partial int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property, long longOffset, long longLength, [MarshalAs(UnmanagedType.Bool)] bool delete, IntPtr reqType, out IntPtr actualTypeReturn, out int actualFormatReturn, out IntPtr nitemsReturn, out IntPtr bytesAfterReturn, out IntPtr propReturn);

    [LibraryImport(LibX11)]
    public static partial int XDeleteProperty(IntPtr display, IntPtr window, IntPtr property);

    #endregion

    #region Clipboard/Selection

    [LibraryImport(LibX11)]
    public static partial int XSetSelectionOwner(IntPtr display, IntPtr selection, IntPtr owner, ulong time);

    [LibraryImport(LibX11)]
    public static partial IntPtr XGetSelectionOwner(IntPtr display, IntPtr selection);

    [LibraryImport(LibX11)]
    public static partial int XConvertSelection(IntPtr display, IntPtr selection, IntPtr target, IntPtr property, IntPtr requestor, ulong time);

    #endregion

    #region Memory

    [LibraryImport(LibX11)]
    public static partial int XFree(IntPtr data);

    #endregion

    #region Graphics Context

    [LibraryImport(LibX11)]
    public static partial IntPtr XCreateGC(IntPtr display, IntPtr drawable, ulong valueMask, IntPtr values);

    [LibraryImport(LibX11)]
    public static partial int XFreeGC(IntPtr display, IntPtr gc);

    [LibraryImport(LibX11)]
    public static partial int XCopyArea(IntPtr display, IntPtr src, IntPtr dest, IntPtr gc, int srcX, int srcY, uint width, uint height, int destX, int destY);

    #endregion

    #region Cursor

    [LibraryImport(LibX11)]
    public static partial IntPtr XCreateFontCursor(IntPtr display, uint shape);

    [LibraryImport(LibX11)]
    public static partial int XFreeCursor(IntPtr display, IntPtr cursor);

    [LibraryImport(LibX11)]
    public static partial int XDefineCursor(IntPtr display, IntPtr window, IntPtr cursor);

    [LibraryImport(LibX11)]
    public static partial int XUndefineCursor(IntPtr display, IntPtr window);

    #endregion

    #region Connection

    [LibraryImport(LibX11)]
    public static partial int XConnectionNumber(IntPtr display);

    #endregion

    #region Image Functions

    [LibraryImport(LibX11)]
    public static partial IntPtr XCreateImage(IntPtr display, IntPtr visual, uint depth, int format,
        int offset, IntPtr data, uint width, uint height, int bitmapPad, int bytesPerLine);

    [LibraryImport(LibX11)]
    public static partial int XPutImage(IntPtr display, IntPtr drawable, IntPtr gc, IntPtr image,
        int srcX, int srcY, int destX, int destY, uint width, uint height);

    [LibraryImport(LibX11)]
    public static partial int XDestroyImage(IntPtr image);


    [LibraryImport(LibX11)]
    public static partial IntPtr XDefaultGC(IntPtr display, int screen);

    public const int ZPixmap = 2;

    #endregion

}

#region X11 Structures

[StructLayout(LayoutKind.Sequential)]
public struct XSetWindowAttributes
{
    public IntPtr BackgroundPixmap;
    public ulong BackgroundPixel;
    public IntPtr BorderPixmap;
    public ulong BorderPixel;
    public int BitGravity;
    public int WinGravity;
    public int BackingStore;
    public ulong BackingPlanes;
    public ulong BackingPixel;
    public int SaveUnder;
    public long EventMask;
    public long DoNotPropagateMask;
    public int OverrideRedirect;
    public IntPtr Colormap;
    public IntPtr Cursor;
}

[StructLayout(LayoutKind.Explicit, Size = 192)]
public struct XEvent
{
    [FieldOffset(0)] public int Type;
    [FieldOffset(0)] public XKeyEvent KeyEvent;
    [FieldOffset(0)] public XButtonEvent ButtonEvent;
    [FieldOffset(0)] public XMotionEvent MotionEvent;
    [FieldOffset(0)] public XConfigureEvent ConfigureEvent;
    [FieldOffset(0)] public XExposeEvent ExposeEvent;
    [FieldOffset(0)] public XClientMessageEvent ClientMessageEvent;
    [FieldOffset(0)] public XCrossingEvent CrossingEvent;
    [FieldOffset(0)] public XFocusChangeEvent FocusChangeEvent;
}

[StructLayout(LayoutKind.Sequential)]
public struct XKeyEvent
{
    public int Type;
    public ulong Serial;
    public int SendEvent;
    public IntPtr Display;
    public IntPtr Window;
    public IntPtr Root;
    public IntPtr Subwindow;
    public ulong Time;
    public int X, Y;
    public int XRoot, YRoot;
    public uint State;
    public uint Keycode;
    public int SameScreen;
}

[StructLayout(LayoutKind.Sequential)]
public struct XButtonEvent
{
    public int Type;
    public ulong Serial;
    public int SendEvent;
    public IntPtr Display;
    public IntPtr Window;
    public IntPtr Root;
    public IntPtr Subwindow;
    public ulong Time;
    public int X, Y;
    public int XRoot, YRoot;
    public uint State;
    public uint Button;
    public int SameScreen;
}

[StructLayout(LayoutKind.Sequential)]
public struct XMotionEvent
{
    public int Type;
    public ulong Serial;
    public int SendEvent;
    public IntPtr Display;
    public IntPtr Window;
    public IntPtr Root;
    public IntPtr Subwindow;
    public ulong Time;
    public int X, Y;
    public int XRoot, YRoot;
    public uint State;
    public byte IsHint;
    public int SameScreen;
}

[StructLayout(LayoutKind.Sequential)]
public struct XConfigureEvent
{
    public int Type;
    public ulong Serial;
    public int SendEvent;
    public IntPtr Display;
    public IntPtr Event;
    public IntPtr Window;
    public int X, Y;
    public int Width, Height;
    public int BorderWidth;
    public IntPtr Above;
    public int OverrideRedirect;
}

[StructLayout(LayoutKind.Sequential)]
public struct XExposeEvent
{
    public int Type;
    public ulong Serial;
    public int SendEvent;
    public IntPtr Display;
    public IntPtr Window;
    public int X, Y;
    public int Width, Height;
    public int Count;
}

[StructLayout(LayoutKind.Sequential)]
public struct XClientMessageEvent
{
    public int Type;
    public ulong Serial;
    public int SendEvent;
    public IntPtr Display;
    public IntPtr Window;
    public IntPtr MessageType;
    public int Format;
    public ClientMessageData Data;
}

[StructLayout(LayoutKind.Explicit)]
public struct ClientMessageData
{
    [FieldOffset(0)] public long L0;
    [FieldOffset(8)] public long L1;
    [FieldOffset(16)] public long L2;
    [FieldOffset(24)] public long L3;
    [FieldOffset(32)] public long L4;
}

[StructLayout(LayoutKind.Sequential)]
public struct XCrossingEvent
{
    public int Type;
    public ulong Serial;
    public int SendEvent;
    public IntPtr Display;
    public IntPtr Window;
    public IntPtr Root;
    public IntPtr Subwindow;
    public ulong Time;
    public int X, Y;
    public int XRoot, YRoot;
    public int Mode;
    public int Detail;
    public int SameScreen;
    public int Focus;
    public uint State;
}

[StructLayout(LayoutKind.Sequential)]
public struct XFocusChangeEvent
{
    public int Type;
    public ulong Serial;
    public int SendEvent;
    public IntPtr Display;
    public IntPtr Window;
    public int Mode;
    public int Detail;
}

#endregion

#region X11 Constants

public static class XEventType
{
    public const int KeyPress = 2;
    public const int KeyRelease = 3;
    public const int ButtonPress = 4;
    public const int ButtonRelease = 5;
    public const int MotionNotify = 6;
    public const int EnterNotify = 7;
    public const int LeaveNotify = 8;
    public const int FocusIn = 9;
    public const int FocusOut = 10;
    public const int Expose = 12;
    public const int ConfigureNotify = 22;
    public const int ClientMessage = 33;
}

public static class XEventMask
{
    public const long KeyPressMask = 1L << 0;
    public const long KeyReleaseMask = 1L << 1;
    public const long ButtonPressMask = 1L << 2;
    public const long ButtonReleaseMask = 1L << 3;
    public const long EnterWindowMask = 1L << 4;
    public const long LeaveWindowMask = 1L << 5;
    public const long PointerMotionMask = 1L << 6;
    public const long ExposureMask = 1L << 15;
    public const long StructureNotifyMask = 1L << 17;
    public const long FocusChangeMask = 1L << 21;
}

public static class XWindowClass
{
    public const uint InputOutput = 1;
    public const uint InputOnly = 2;
}

public static class XCursorShape
{
    public const uint XC_left_ptr = 68;
    public const uint XC_hand2 = 60;
    public const uint XC_xterm = 152;
    public const uint XC_watch = 150;
    public const uint XC_crosshair = 34;
}


#endregion
