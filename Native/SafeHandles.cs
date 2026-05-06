// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Maui.Platform.Linux.Native;

/// <summary>
/// Safe handle wrapper for GTK widget pointers.
/// Releases the widget via <c>gtk_widget_destroy</c> when disposed.
/// </summary>
internal partial class SafeGtkWidgetHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    [LibraryImport("libgtk-3.so.0")]
    private static partial void gtk_widget_destroy(IntPtr widget);

    /// <summary>
    /// Initializes a new <see cref="SafeGtkWidgetHandle"/> that owns the handle.
    /// </summary>
    public SafeGtkWidgetHandle() : base(ownsHandle: true)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="SafeGtkWidgetHandle"/> wrapping an existing pointer.
    /// </summary>
    /// <param name="existingHandle">The existing GTK widget pointer.</param>
    /// <param name="ownsHandle">Whether this safe handle is responsible for releasing the resource.</param>
    public SafeGtkWidgetHandle(IntPtr existingHandle, bool ownsHandle = true) : base(ownsHandle)
    {
        SetHandle(existingHandle);
    }

    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        gtk_widget_destroy(handle);
        return true;
    }
}

/// <summary>
/// Safe handle wrapper for GObject pointers.
/// Releases the object via <c>g_object_unref</c> when disposed.
/// Suitable for any GObject-derived type including GtkCssProvider, GdkPixbuf, etc.
/// </summary>
internal partial class SafeGObjectHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    [LibraryImport("libgobject-2.0.so.0")]
    private static partial void g_object_unref(IntPtr obj);

    /// <summary>
    /// Initializes a new <see cref="SafeGObjectHandle"/> that owns the handle.
    /// </summary>
    public SafeGObjectHandle() : base(ownsHandle: true)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="SafeGObjectHandle"/> wrapping an existing pointer.
    /// </summary>
    /// <param name="existingHandle">The existing GObject pointer.</param>
    /// <param name="ownsHandle">Whether this safe handle is responsible for releasing the resource.</param>
    public SafeGObjectHandle(IntPtr existingHandle, bool ownsHandle = true) : base(ownsHandle)
    {
        SetHandle(existingHandle);
    }

    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        g_object_unref(handle);
        return true;
    }
}

/// <summary>
/// Safe handle wrapper for X11 <c>Display*</c> pointers.
/// Releases the display connection via <c>XCloseDisplay</c> when disposed.
/// </summary>
internal partial class SafeX11DisplayHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    [LibraryImport("libX11.so.6")]
    private static partial int XCloseDisplay(IntPtr display);

    /// <summary>
    /// Initializes a new <see cref="SafeX11DisplayHandle"/> that owns the handle.
    /// </summary>
    public SafeX11DisplayHandle() : base(ownsHandle: true)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="SafeX11DisplayHandle"/> wrapping an existing pointer.
    /// </summary>
    /// <param name="existingHandle">The existing X11 Display pointer.</param>
    /// <param name="ownsHandle">Whether this safe handle is responsible for releasing the resource.</param>
    public SafeX11DisplayHandle(IntPtr existingHandle, bool ownsHandle = true) : base(ownsHandle)
    {
        SetHandle(existingHandle);
    }

    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        XCloseDisplay(handle);
        return true;
    }
}

/// <summary>
/// Safe handle wrapper for X11 Cursor resources.
/// Releases the cursor via <c>XFreeCursor</c> when disposed.
/// Requires the associated <c>Display*</c> to be provided at construction time,
/// as X11 cursor cleanup requires both the display and cursor handles.
/// </summary>
internal partial class SafeX11CursorHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    [LibraryImport("libX11.so.6")]
    private static partial int XFreeCursor(IntPtr display, IntPtr cursor);

    private readonly IntPtr _display;

    /// <summary>
    /// Initializes a new <see cref="SafeX11CursorHandle"/> that owns the handle.
    /// </summary>
    /// <param name="display">
    /// The X11 Display pointer required for releasing the cursor.
    /// The caller must ensure the display remains valid for the lifetime of this handle.
    /// </param>
    public SafeX11CursorHandle(IntPtr display) : base(ownsHandle: true)
    {
        _display = display;
    }

    /// <summary>
    /// Initializes a new <see cref="SafeX11CursorHandle"/> wrapping an existing cursor.
    /// </summary>
    /// <param name="display">
    /// The X11 Display pointer required for releasing the cursor.
    /// The caller must ensure the display remains valid for the lifetime of this handle.
    /// </param>
    /// <param name="existingHandle">The existing X11 Cursor handle.</param>
    /// <param name="ownsHandle">Whether this safe handle is responsible for releasing the resource.</param>
    public SafeX11CursorHandle(IntPtr display, IntPtr existingHandle, bool ownsHandle = true) : base(ownsHandle)
    {
        _display = display;
        SetHandle(existingHandle);
    }

    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        if (_display != IntPtr.Zero)
        {
            XFreeCursor(_display, handle);
        }
        return true;
    }
}

/// <summary>
/// Safe handle wrapper for <c>GtkCssProvider*</c> pointers.
/// Since GtkCssProvider is a GObject, this releases it via <c>g_object_unref</c> when disposed.
/// </summary>
internal partial class SafeCssProviderHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    [LibraryImport("libgobject-2.0.so.0")]
    private static partial void g_object_unref(IntPtr obj);

    /// <summary>
    /// Initializes a new <see cref="SafeCssProviderHandle"/> that owns the handle.
    /// </summary>
    public SafeCssProviderHandle() : base(ownsHandle: true)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="SafeCssProviderHandle"/> wrapping an existing pointer.
    /// </summary>
    /// <param name="existingHandle">The existing GtkCssProvider pointer.</param>
    /// <param name="ownsHandle">Whether this safe handle is responsible for releasing the resource.</param>
    public SafeCssProviderHandle(IntPtr existingHandle, bool ownsHandle = true) : base(ownsHandle)
    {
        SetHandle(existingHandle);
    }

    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        g_object_unref(handle);
        return true;
    }
}
