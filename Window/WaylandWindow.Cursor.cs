// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Window;

// Wayland cursor support via libwayland-cursor. Cursors are loaded from the
// user's theme (XCURSOR_THEME, fallback "default"), at the size requested by
// XCURSOR_SIZE — both already configured early in LinuxApplication.Run() so
// HiDPI cursors work without per-app overrides.
public partial class WaylandWindow
{
    private const string LibWaylandCursor = "libwayland-cursor.so.0";

    [LibraryImport(LibWaylandCursor, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr wl_cursor_theme_load(string? name, int size, IntPtr shm);

    [LibraryImport(LibWaylandCursor)]
    private static partial void wl_cursor_theme_destroy(IntPtr theme);

    [LibraryImport(LibWaylandCursor, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr wl_cursor_theme_get_cursor(IntPtr theme, string name);

    [LibraryImport(LibWaylandCursor)]
    private static partial IntPtr wl_cursor_image_get_buffer(IntPtr image);

    // wl_pointer.set_cursor: opcode 0, args (serial: uint, surface: object, hotspot_x: int, hotspot_y: int)
    [LibraryImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal")]
    private static partial void wl_proxy_marshal_set_cursor(
        IntPtr proxy, uint opcode, uint serial, IntPtr surface, int hotspotX, int hotspotY);

    // Layout of struct wl_cursor (libwayland-cursor.h). The `images` field is a
    // pointer to an array of `wl_cursor_image*`; we read images[0] for the first
    // (and usually only) frame. Animated cursors would walk the array on a timer.
    [StructLayout(LayoutKind.Sequential)]
    private struct WlCursor
    {
        public uint ImageCount;
        public IntPtr Images;     // wl_cursor_image**
        public IntPtr Name;       // const char*
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WlCursorImage
    {
        public uint Width;
        public uint Height;
        public uint HotspotX;
        public uint HotspotY;
        public uint Delay;
    }

    // Cached state. The theme is loaded once on first SetCursor; the cursor surface
    // is created lazily so apps that never call SetCursor incur no extra wl_surface.
    private IntPtr _cursorTheme;
    private IntPtr _cursorSurface;
    private CursorType _appliedCursor = (CursorType)(-1); // force first-set to apply

    // XDG cursor names per the cursor-spec, with sensible aliases for older themes.
    private static readonly Dictionary<CursorType, string[]> CursorNames = new()
    {
        [CursorType.Arrow] = new[] { "default", "left_ptr", "arrow" },
        [CursorType.Hand] = new[] { "pointer", "hand2", "hand1" },
        [CursorType.Text] = new[] { "text", "xterm", "ibeam" },
        [CursorType.SizeWestEast] = new[] { "ew-resize", "col-resize", "sb_h_double_arrow", "h_double_arrow" },
        [CursorType.SizeNorthSouth] = new[] { "ns-resize", "row-resize", "sb_v_double_arrow", "v_double_arrow" },
        [CursorType.SizeAll] = new[] { "move", "all-scroll", "fleur" },
    };

    private void EnsureCursorTheme()
    {
        if (_cursorTheme != IntPtr.Zero || _shm == IntPtr.Zero)
            return;

        int size = 24;
        var sizeEnv = Environment.GetEnvironmentVariable("XCURSOR_SIZE");
        if (!string.IsNullOrEmpty(sizeEnv) && int.TryParse(sizeEnv, out var parsed) && parsed > 0)
            size = parsed;

        var themeName = Environment.GetEnvironmentVariable("XCURSOR_THEME");
        _cursorTheme = wl_cursor_theme_load(themeName, size, _shm);
    }

    private void EnsureCursorSurface()
    {
        if (_cursorSurface != IntPtr.Zero || _compositor == IntPtr.Zero)
            return;

        // wl_compositor.create_surface: opcode 0 → returns new wl_surface
        _cursorSurface = wl_proxy_marshal_constructor(
            _compositor, 0, _wl_surface_interface, IntPtr.Zero);
    }

    // Resize cursors for the edges we resize when drawing our own decorations
    // (XDG_TOPLEVEL_RESIZE_EDGE_* → cursor-spec names, with X11 core-cursor aliases).
    private static readonly Dictionary<uint, string[]> ResizeCursorNames = new()
    {
        [XDG_TOPLEVEL_RESIZE_EDGE_TOP] = new[] { "n-resize", "top_side" },
        [XDG_TOPLEVEL_RESIZE_EDGE_BOTTOM] = new[] { "s-resize", "bottom_side" },
        [XDG_TOPLEVEL_RESIZE_EDGE_LEFT] = new[] { "w-resize", "left_side" },
        [XDG_TOPLEVEL_RESIZE_EDGE_RIGHT] = new[] { "e-resize", "right_side" },
        [XDG_TOPLEVEL_RESIZE_EDGE_TOP_LEFT] = new[] { "nw-resize", "top_left_corner" },
        [XDG_TOPLEVEL_RESIZE_EDGE_TOP_RIGHT] = new[] { "ne-resize", "top_right_corner" },
        [XDG_TOPLEVEL_RESIZE_EDGE_BOTTOM_LEFT] = new[] { "sw-resize", "bottom_left_corner" },
        [XDG_TOPLEVEL_RESIZE_EDGE_BOTTOM_RIGHT] = new[] { "se-resize", "bottom_right_corner" },
    };

    // The resize edge the pointer is over (NONE when over the content): while set, the edge's
    // resize cursor shows and the app's SetCursor is only remembered.
    private uint _hoverResizeEdge;

    /// <summary>
    /// Shows the resize cursor while the pointer is over a window edge we resize (our own
    /// decorations), as every other Linux client-side decoration does; the strip was otherwise
    /// indistinguishable from the content and the window looked as if it could not be resized.
    /// </summary>
    private void UpdateResizeCursor(float logicalX, float logicalY)
    {
        uint edge = CsdEdgeAt(logicalX, logicalY);
        if (edge == _hoverResizeEdge)
            return;
        _hoverResizeEdge = edge;
        if (edge == XDG_TOPLEVEL_RESIZE_EDGE_NONE)
            TryApplyCursor(_pendingCursor);
        else if (ResizeCursorNames.TryGetValue(edge, out var names))
            TryApplyCursorNames(names);
    }

    private bool TryApplyCursor(CursorType cursorType)
    {
        if (!TryApplyCursorNames(CursorNames[cursorType]))
            return false;
        _appliedCursor = cursorType;
        return true;
    }

    private bool TryApplyCursorNames(string[] names)
    {
        if (_pointer == IntPtr.Zero || _pointerSerial == 0)
            return false;

        EnsureCursorTheme();
        if (_cursorTheme == IntPtr.Zero)
            return false;

        IntPtr cursor = IntPtr.Zero;
        foreach (var name in names)
        {
            cursor = wl_cursor_theme_get_cursor(_cursorTheme, name);
            if (cursor != IntPtr.Zero)
                break;
        }
        if (cursor == IntPtr.Zero)
            return false;

        // Read wl_cursor.images[0] — pointer-to-pointer dereference.
        var cursorStruct = Marshal.PtrToStructure<WlCursor>(cursor);
        if (cursorStruct.ImageCount == 0 || cursorStruct.Images == IntPtr.Zero)
            return false;
        var firstImagePtr = Marshal.ReadIntPtr(cursorStruct.Images);
        if (firstImagePtr == IntPtr.Zero)
            return false;

        var image = Marshal.PtrToStructure<WlCursorImage>(firstImagePtr);
        var buffer = wl_cursor_image_get_buffer(firstImagePtr);
        if (buffer == IntPtr.Zero)
            return false;

        EnsureCursorSurface();
        if (_cursorSurface == IntPtr.Zero)
            return false;

        // Attach the cursor bitmap to its surface and commit.
        // wl_surface.attach: opcode 1 (buffer, x, y); damage_buffer: opcode 9; commit: opcode 6
        wl_proxy_marshal(_cursorSurface, 1, buffer, 0, 0);
        wl_proxy_marshal(_cursorSurface, 9, 0, 0, (int)image.Width, (int)image.Height);
        wl_proxy_marshal(_cursorSurface, 6);

        // wl_pointer.set_cursor: opcode 0
        wl_proxy_marshal_set_cursor(_pointer, 0, _pointerSerial, _cursorSurface,
            (int)image.HotspotX, (int)image.HotspotY);
        return true;
    }

    private void DisposeCursor()
    {
        if (_cursorSurface != IntPtr.Zero)
        {
            wl_proxy_destroy(_cursorSurface);
            _cursorSurface = IntPtr.Zero;
        }
        if (_cursorTheme != IntPtr.Zero)
        {
            wl_cursor_theme_destroy(_cursorTheme);
            _cursorTheme = IntPtr.Zero;
        }
    }
}
