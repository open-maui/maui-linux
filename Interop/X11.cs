// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Interop;

internal static partial class X11
{
    private const string LibX11 = "libX11.so.6";

    public const int ZPixmap = 2;

    // Event types
    public const int ClientMessage = 33;

    // Event masks for XSendEvent
    public const long SubstructureRedirectMask = 1L << 20;
    public const long SubstructureNotifyMask = 1L << 19;

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

    [LibraryImport(LibX11)]
    public static partial IntPtr XCreateSimpleWindow(
        IntPtr display, IntPtr parent,
        int x, int y, uint width, uint height,
        uint borderWidth, ulong border, ulong background);

    [LibraryImport(LibX11)]
    public static partial IntPtr XCreateWindow(
        IntPtr display, IntPtr parent,
        int x, int y, uint width, uint height, uint borderWidth,
        int depth, uint windowClass, IntPtr visual,
        ulong valueMask, ref XSetWindowAttributes attributes);

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
    public static partial int XIconifyWindow(IntPtr display, IntPtr window, int screen);

    [LibraryImport(LibX11)]
    public static partial int XMoveResizeWindow(IntPtr display, IntPtr window, int x, int y, uint width, uint height);

    [LibraryImport(LibX11, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int XStoreName(IntPtr display, IntPtr window, string windowName);

    [LibraryImport(LibX11)]
    public static partial int XRaiseWindow(IntPtr display, IntPtr window);

    [LibraryImport(LibX11)]
    public static partial int XLowerWindow(IntPtr display, IntPtr window);

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
    public static partial int XSendEvent(
        IntPtr display, IntPtr window,
        [MarshalAs(UnmanagedType.Bool)] bool propagate,
        long eventMask, ref XEvent eventSend);

    [LibraryImport(LibX11)]
    public static partial ulong XKeycodeToKeysym(IntPtr display, int keycode, int index);

    [LibraryImport(LibX11)]
    public static partial int XLookupString(
        ref XKeyEvent keyEvent, IntPtr bufferReturn, int bytesBuffer,
        out ulong keysymReturn, IntPtr statusInOut);

    [LibraryImport(LibX11)]
    public static partial int XGrabKeyboard(
        IntPtr display, IntPtr grabWindow,
        [MarshalAs(UnmanagedType.Bool)] bool ownerEvents,
        int pointerMode, int keyboardMode, ulong time);

    [LibraryImport(LibX11)]
    public static partial int XUngrabKeyboard(IntPtr display, ulong time);

    [LibraryImport(LibX11)]
    public static partial int XGrabPointer(
        IntPtr display, IntPtr grabWindow,
        [MarshalAs(UnmanagedType.Bool)] bool ownerEvents,
        uint eventMask, int pointerMode, int keyboardMode,
        IntPtr confineTo, IntPtr cursor, ulong time);

    [LibraryImport(LibX11)]
    public static partial int XUngrabPointer(IntPtr display, ulong time);

    [LibraryImport(LibX11)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool XQueryPointer(
        IntPtr display, IntPtr window,
        out IntPtr rootReturn, out IntPtr childReturn,
        out int rootX, out int rootY,
        out int winX, out int winY,
        out uint maskReturn);

    [LibraryImport(LibX11)]
    public static partial int XWarpPointer(
        IntPtr display, IntPtr srcWindow, IntPtr destWindow,
        int srcX, int srcY, uint srcWidth, uint srcHeight,
        int destX, int destY);

    [LibraryImport(LibX11, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr XInternAtom(IntPtr display, string atomName, [MarshalAs(UnmanagedType.Bool)] bool onlyIfExists);

    [LibraryImport(LibX11)]
    public static partial int XChangeProperty(
        IntPtr display, IntPtr window, IntPtr property, IntPtr type,
        int format, int mode, IntPtr data, int nelements);

    [LibraryImport(LibX11)]
    public static partial int XGetWindowProperty(
        IntPtr display, IntPtr window, IntPtr property,
        long longOffset, long longLength,
        [MarshalAs(UnmanagedType.Bool)] bool delete, IntPtr reqType,
        out IntPtr actualTypeReturn, out int actualFormatReturn,
        out IntPtr nitemsReturn, out IntPtr bytesAfterReturn,
        out IntPtr propReturn);

    [LibraryImport(LibX11)]
    public static partial int XDeleteProperty(IntPtr display, IntPtr window, IntPtr property);

    [LibraryImport(LibX11)]
    public static partial int XSetSelectionOwner(IntPtr display, IntPtr selection, IntPtr owner, ulong time);

    [LibraryImport(LibX11)]
    public static partial IntPtr XGetSelectionOwner(IntPtr display, IntPtr selection);

    [LibraryImport(LibX11)]
    public static partial int XConvertSelection(
        IntPtr display, IntPtr selection, IntPtr target,
        IntPtr property, IntPtr requestor, ulong time);

    [LibraryImport(LibX11)]
    public static partial int XFree(IntPtr data);

    [LibraryImport(LibX11)]
    public static partial IntPtr XCreateGC(IntPtr display, IntPtr drawable, ulong valueMask, IntPtr values);

    [LibraryImport(LibX11)]
    public static partial int XFreeGC(IntPtr display, IntPtr gc);

    [LibraryImport(LibX11)]
    public static partial int XCopyArea(
        IntPtr display, IntPtr src, IntPtr dest, IntPtr gc,
        int srcX, int srcY, uint width, uint height, int destX, int destY);

    [LibraryImport(LibX11)]
    public static partial IntPtr XCreateFontCursor(IntPtr display, uint shape);

    [LibraryImport(LibX11)]
    public static partial int XFreeCursor(IntPtr display, IntPtr cursor);

    [LibraryImport(LibX11)]
    public static partial int XDefineCursor(IntPtr display, IntPtr window, IntPtr cursor);

    [LibraryImport(LibX11)]
    public static partial int XUndefineCursor(IntPtr display, IntPtr window);

    [LibraryImport(LibX11)]
    public static partial int XConnectionNumber(IntPtr display);

    [LibraryImport(LibX11)]
    public static partial IntPtr XCreateImage(
        IntPtr display, IntPtr visual, uint depth, int format, int offset,
        IntPtr data, uint width, uint height, int bitmapPad, int bytesPerLine);

    [LibraryImport(LibX11)]
    public static partial int XPutImage(
        IntPtr display, IntPtr drawable, IntPtr gc, IntPtr image,
        int srcX, int srcY, int destX, int destY, uint width, uint height);

    [LibraryImport(LibX11)]
    public static partial int XDestroyImage(IntPtr image);

    [LibraryImport(LibX11)]
    public static partial IntPtr XDefaultGC(IntPtr display, int screen);
}
