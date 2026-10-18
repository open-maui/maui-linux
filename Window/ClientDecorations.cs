// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>What a press at a point of a client-decorated window lands on.</summary>
internal enum CsdRegion
{
    /// <summary>The page below the titlebar (the press goes to the views).</summary>
    Content,
    /// <summary>A window edge that resizes the window.</summary>
    ResizeEdge,
    /// <summary>The close button.</summary>
    Close,
    /// <summary>The maximize/restore button.</summary>
    Maximize,
    /// <summary>The minimize button.</summary>
    Minimize,
    /// <summary>An interactive part of the window's TitleBar (it takes the press, as on Windows).</summary>
    TitleBarContent,
    /// <summary>The rest of the titlebar: drags move the window, a double-click maximizes it.</summary>
    Move,
}

/// <summary>A window edge or corner of a client-decorated window.</summary>
internal enum CsdResizeEdge
{
    None,
    Top,
    Bottom,
    Left,
    Right,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>The result of <see cref="ClientDecorations.HitTest"/>.</summary>
internal readonly record struct CsdHit(CsdRegion Region, CsdResizeEdge Edge = CsdResizeEdge.None);

/// <summary>
/// A native window that can draw its own decorations (OpenMaui's client-side titlebar, with the
/// window's <c>Window.TitleBar</c> in it): the Wayland window when the compositor leaves the
/// decorations to the client or a TitleBar asks for them, and the X11 window when a TitleBar
/// asks for them.
/// </summary>
internal interface IClientDecoratedWindow
{
    /// <summary>The decoration state the renderer and the pointer hit-test share.</summary>
    ClientDecorations Decorations { get; }

    /// <summary>Whether the window is maximized (the maximize button shows "restore", the edges do not resize).</summary>
    bool IsMaximized { get; }

    /// <summary>The window title, drawn in the titlebar when the window has no TitleBar.</summary>
    string Title { get; }

    /// <summary>
    /// Tells the window whether its MAUI window has a (visible) TitleBar. With one the window
    /// draws its own decorations, as Windows extends the content into the title bar: on X11 it
    /// asks the window manager for an undecorated window; on Wayland it asks the compositor for
    /// client-side mode (the strip stays when the compositor keeps server-side decorations).
    /// </summary>
    void SetClientDecorationsWanted(bool wanted);
}

/// <summary>
/// The client-side decoration state shared by the X11 and Wayland windows: whether the window
/// draws its decorations, the titlebar's height and window-button bounds (written by the
/// renderer each frame), and the hit-test that sorts a press into resize, button, TitleBar
/// content, move or page. All coordinates are logical pixels with the origin at the window's
/// top-left corner.
/// </summary>
internal sealed class ClientDecorations
{
    /// <summary>The titlebar's height without a TitleBar (logical pixels).</summary>
    public const float DefaultTitlebarHeight = 32f;

    /// <summary>Width of the strip along each window edge that resizes the window (logical pixels).</summary>
    public const float ResizeMargin = 6f;

    /// <summary>Most milliseconds between the two presses of a titlebar double-click.</summary>
    public const ulong DoubleClickMilliseconds = 400;

    /// <summary>Farthest the second press of a double-click may land from the first (logical pixels).</summary>
    public const float DoubleClickSlop = 4f;

    /// <summary>Whether the window draws its own decorations now.</summary>
    public bool Active { get; set; }

    /// <summary>The titlebar's height this frame: <see cref="DefaultTitlebarHeight"/>, or taller with a TitleBar (set by the renderer).</summary>
    public float TitlebarHeight { get; set; } = DefaultTitlebarHeight;

    public SKRect CloseButtonBounds { get; set; }
    public SKRect MaximizeButtonBounds { get; set; }
    public SKRect MinimizeButtonBounds { get; set; }

    /// <summary>
    /// Asked for a press in the titlebar: true when an interactive part of the window's TitleBar
    /// (MAUI's passthrough elements, the parts Windows keeps out of the drag region) is there.
    /// Set by the window's context.
    /// </summary>
    public Func<float, float, bool>? TitleBarPassthrough { get; set; }

    private ulong _lastMoveClickTime;
    private float _lastMoveClickX = float.NaN;
    private float _lastMoveClickY = float.NaN;

    /// <summary>The window edge under a point (None over the inside, and always when maximized).</summary>
    public static CsdResizeEdge EdgeAt(float x, float y, float width, float height, bool isMaximized)
    {
        if (isMaximized || width <= 0f || height <= 0f)
            return CsdResizeEdge.None;
        bool atLeft = x <= ResizeMargin;
        bool atRight = x >= width - ResizeMargin;
        bool atTop = y <= ResizeMargin;
        bool atBottom = y >= height - ResizeMargin;
        if (atTop && atLeft) return CsdResizeEdge.TopLeft;
        if (atTop && atRight) return CsdResizeEdge.TopRight;
        if (atBottom && atLeft) return CsdResizeEdge.BottomLeft;
        if (atBottom && atRight) return CsdResizeEdge.BottomRight;
        if (atTop) return CsdResizeEdge.Top;
        if (atBottom) return CsdResizeEdge.Bottom;
        if (atLeft) return CsdResizeEdge.Left;
        if (atRight) return CsdResizeEdge.Right;
        return CsdResizeEdge.None;
    }

    /// <summary>
    /// Sorts a press at (<paramref name="x"/>, <paramref name="y"/>) in a window of the given
    /// logical size: a resize edge first (not when maximized), then the window buttons, then an
    /// interactive part of the TitleBar, then the rest of the titlebar (move); below the
    /// titlebar it is the page's. Always <see cref="CsdRegion.Content"/> while not <see cref="Active"/>.
    /// </summary>
    public CsdHit HitTest(float x, float y, float width, float height, bool isMaximized)
    {
        if (!Active)
            return new CsdHit(CsdRegion.Content);
        var edge = EdgeAt(x, y, width, height, isMaximized);
        if (edge != CsdResizeEdge.None)
            return new CsdHit(CsdRegion.ResizeEdge, edge);
        if (y > TitlebarHeight)
            return new CsdHit(CsdRegion.Content);
        if (CloseButtonBounds.Contains(x, y))
            return new CsdHit(CsdRegion.Close);
        if (MaximizeButtonBounds.Contains(x, y))
            return new CsdHit(CsdRegion.Maximize);
        if (MinimizeButtonBounds.Contains(x, y))
            return new CsdHit(CsdRegion.Minimize);
        if (TitleBarPassthrough?.Invoke(x, y) == true)
            return new CsdHit(CsdRegion.TitleBarContent);
        return new CsdHit(CsdRegion.Move);
    }

    /// <summary>
    /// Records a left press on the titlebar's move area and returns true when it completes a
    /// double-click (the second press within <see cref="DoubleClickMilliseconds"/> and
    /// <see cref="DoubleClickSlop"/> of the first); a completed double-click is forgotten, so a
    /// third press starts over. <paramref name="timeMilliseconds"/> is the event's timestamp.
    /// </summary>
    public bool RegisterMoveClick(ulong timeMilliseconds, float x, float y)
    {
        bool isDouble = !float.IsNaN(_lastMoveClickX)
            && timeMilliseconds >= _lastMoveClickTime
            && timeMilliseconds - _lastMoveClickTime <= DoubleClickMilliseconds
            && Math.Abs(x - _lastMoveClickX) <= DoubleClickSlop
            && Math.Abs(y - _lastMoveClickY) <= DoubleClickSlop;
        if (isDouble)
        {
            ResetClickTracking();
            return true;
        }
        _lastMoveClickTime = timeMilliseconds;
        _lastMoveClickX = x;
        _lastMoveClickY = y;
        return false;
    }

    /// <summary>Forgets the last titlebar press (a press elsewhere breaks a double-click).</summary>
    public void ResetClickTracking()
    {
        _lastMoveClickTime = 0;
        _lastMoveClickX = float.NaN;
        _lastMoveClickY = float.NaN;
    }

    /// <summary>The xdg_toplevel.resize edge value for an edge.</summary>
    public static uint ToXdgResizeEdge(CsdResizeEdge edge) => edge switch
    {
        CsdResizeEdge.Top => 1,
        CsdResizeEdge.Bottom => 2,
        CsdResizeEdge.Left => 4,
        CsdResizeEdge.TopLeft => 5,
        CsdResizeEdge.BottomLeft => 6,
        CsdResizeEdge.Right => 8,
        CsdResizeEdge.TopRight => 9,
        CsdResizeEdge.BottomRight => 10,
        _ => 0,
    };

    /// <summary>The EWMH _NET_WM_MOVERESIZE direction for an edge (-1 for none).</summary>
    public static int ToNetWmMoveResizeDirection(CsdResizeEdge edge) => edge switch
    {
        CsdResizeEdge.TopLeft => X11ClientDecorations.NetWmMoveResizeSizeTopLeft,
        CsdResizeEdge.Top => X11ClientDecorations.NetWmMoveResizeSizeTop,
        CsdResizeEdge.TopRight => X11ClientDecorations.NetWmMoveResizeSizeTopRight,
        CsdResizeEdge.Right => X11ClientDecorations.NetWmMoveResizeSizeRight,
        CsdResizeEdge.BottomRight => X11ClientDecorations.NetWmMoveResizeSizeBottomRight,
        CsdResizeEdge.Bottom => X11ClientDecorations.NetWmMoveResizeSizeBottom,
        CsdResizeEdge.BottomLeft => X11ClientDecorations.NetWmMoveResizeSizeBottomLeft,
        CsdResizeEdge.Left => X11ClientDecorations.NetWmMoveResizeSizeLeft,
        _ => -1,
    };
}

/// <summary>
/// How the Wayland window negotiates decorations over zxdg_decoration_manager_v1: the mode it
/// asks for, and whether it draws its own decorations given the compositor's answer.
/// </summary>
internal static class WaylandDecorationPolicy
{
    /// <summary>zxdg_toplevel_decoration_v1 mode client_side.</summary>
    public const uint ModeClientSide = 1;

    /// <summary>zxdg_toplevel_decoration_v1 mode server_side.</summary>
    public const uint ModeServerSide = 2;

    /// <summary>
    /// The mode to request from the compositor: client-side when the window has a TitleBar (it
    /// is drawn in OpenMaui's own titlebar, as Windows extends content into the title bar),
    /// otherwise server-side (the compositor's own decorations, the behaviour without a TitleBar).
    /// </summary>
    public static uint RequestedMode(bool titleBarWanted) => titleBarWanted ? ModeClientSide : ModeServerSide;

    /// <summary>
    /// Whether the window draws its own decorations: always without the decoration protocol or
    /// with MAUI_PREFER_CSD; otherwise as the compositor's last configure says (before the first
    /// configure, false: the compositor has not answered yet). A compositor that answers
    /// server_side to a client_side request keeps its decorations and the TitleBar is shown as a
    /// strip at the top of the client area.
    /// </summary>
    public static bool UsesClientDecorations(bool hasDecorationProtocol, bool forceCsd, uint? configuredMode)
    {
        if (!hasDecorationProtocol || forceCsd)
            return true;
        return configuredMode == ModeClientSide;
    }
}
