// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>
/// The Xlib side of <see cref="X11ClientDecorations"/>: property writes and client messages to
/// the root window. <see cref="X11Window"/> implements it with Xlib; tests record the calls.
/// </summary>
internal interface IX11DecorationHost
{
    /// <summary>The atom for a name (interned).</summary>
    long Atom(string name);

    /// <summary>Replaces a 32-bit-format property of the window.</summary>
    void SetProperty32(string property, string type, long[] values);

    /// <summary>Sends a format-32 client message about the window to the root window (SubstructureRedirect|SubstructureNotify).</summary>
    void SendRootClientMessage(string messageType, long[] data);

    /// <summary>Releases the implicit pointer grab of the press (EWMH asks for it before _NET_WM_MOVERESIZE).</summary>
    void UngrabPointer(ulong time);

    /// <summary>Whether the window manager lists the atom in _NET_SUPPORTED.</summary>
    bool WmSupports(string atom);

    /// <summary>Iconifies the window.</summary>
    void Iconify();

    /// <summary>Closes the window as WM_DELETE_WINDOW would.</summary>
    void RequestClose();

    /// <summary>Flushes the request queue.</summary>
    void Flush();
}

/// <summary>
/// OpenMaui's client-side decorations on X11, used while the window's MAUI window has a TitleBar
/// (as Windows extends content into the title bar): the window asks the window manager to drop
/// its frame (_MOTIF_WM_HINTS decorations = 0) and draws the titlebar itself. Drags on the
/// titlebar and the window edges hand the pointer to the window manager with
/// _NET_WM_MOVERESIZE; the window buttons maximize (_NET_WM_STATE), minimize and close; a
/// double-click on the titlebar toggles maximize; a right-click on it opens the window manager's
/// window menu (_GTK_SHOW_WINDOW_MENU, when the window manager supports it). No shadow is drawn,
/// so _GTK_FRAME_EXTENTS is not set.
/// </summary>
internal sealed class X11ClientDecorations
{
    // _MOTIF_WM_HINTS (MwmHints): flags, functions, decorations, input_mode, status.
    public const long MwmHintsFunctions = 1;
    public const long MwmHintsDecorations = 2;

    // _NET_WM_MOVERESIZE directions (EWMH).
    public const int NetWmMoveResizeSizeTopLeft = 0;
    public const int NetWmMoveResizeSizeTop = 1;
    public const int NetWmMoveResizeSizeTopRight = 2;
    public const int NetWmMoveResizeSizeRight = 3;
    public const int NetWmMoveResizeSizeBottomRight = 4;
    public const int NetWmMoveResizeSizeBottom = 5;
    public const int NetWmMoveResizeSizeBottomLeft = 6;
    public const int NetWmMoveResizeSizeLeft = 7;
    public const int NetWmMoveResizeMove = 8;

    /// <summary>EWMH source indication: a normal application.</summary>
    private const long SourceApplication = 1;

    private readonly IX11DecorationHost _host;
    private bool _applied;

    public X11ClientDecorations(IX11DecorationHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>The decoration state the renderer and hit-test share.</summary>
    public ClientDecorations State { get; } = new();

    /// <summary>The _MOTIF_WM_HINTS value: only the decorations field set, on or off.</summary>
    public static long[] MotifHints(bool decorated) =>
        new long[] { MwmHintsDecorations, 0, decorated ? 1 : 0, 0, 0 };

    /// <summary>
    /// Turns the client-side decorations on (window manager frame off) or off (frame back).
    /// Returns true when the state changed.
    /// </summary>
    public bool SetWanted(bool wanted)
    {
        if (_applied && State.Active == wanted)
            return false;
        // A window that never had a TitleBar keeps its frame untouched: no hint is written.
        if (!_applied && !wanted)
            return false;
        _applied = true;
        State.Active = wanted;
        State.ResetClickTracking();
        _host.SetProperty32("_MOTIF_WM_HINTS", "_MOTIF_WM_HINTS", MotifHints(decorated: !wanted));
        _host.Flush();
        return true;
    }

    /// <summary>
    /// A button press at a logical window point (<paramref name="rootX"/>/<paramref name="rootY"/>
    /// are the press's root-window pixels, <paramref name="time"/> its X timestamp). True when the
    /// decorations took it (the views do not get it).
    /// </summary>
    public bool HandlePress(PointerButton button, float x, float y, int rootX, int rootY, ulong time,
        float logicalWidth, float logicalHeight, bool isMaximized)
    {
        if (!State.Active)
            return false;
        var hit = State.HitTest(x, y, logicalWidth, logicalHeight, isMaximized);

        if (button == PointerButton.Right)
        {
            State.ResetClickTracking();
            return hit.Region == CsdRegion.Move && ShowWindowMenu(rootX, rootY, time);
        }
        if (button != PointerButton.Left)
            return false;

        switch (hit.Region)
        {
            case CsdRegion.ResizeEdge:
                State.ResetClickTracking();
                BeginMoveResize(ClientDecorations.ToNetWmMoveResizeDirection(hit.Edge), rootX, rootY, 1, time);
                return true;
            case CsdRegion.Close:
                State.ResetClickTracking();
                _host.RequestClose();
                return true;
            case CsdRegion.Maximize:
                State.ResetClickTracking();
                SetMaximized(!isMaximized);
                return true;
            case CsdRegion.Minimize:
                State.ResetClickTracking();
                _host.Iconify();
                _host.Flush();
                return true;
            case CsdRegion.Move:
                if (State.RegisterMoveClick(time, x, y))
                    SetMaximized(!isMaximized);
                else
                    BeginMoveResize(NetWmMoveResizeMove, rootX, rootY, 1, time);
                return true;
            default:
                State.ResetClickTracking();
                return false;
        }
    }

    /// <summary>Hands an interactive move or resize to the window manager.</summary>
    public void BeginMoveResize(int direction, int rootX, int rootY, int button, ulong time)
    {
        _host.UngrabPointer(time);
        _host.SendRootClientMessage("_NET_WM_MOVERESIZE",
            new long[] { rootX, rootY, direction, button, SourceApplication });
        _host.Flush();
    }

    /// <summary>Asks the window manager to maximize or restore the window (_NET_WM_STATE, both axes).</summary>
    public void SetMaximized(bool maximize)
    {
        _host.SendRootClientMessage("_NET_WM_STATE", new long[]
        {
            maximize ? 1 : 0,
            _host.Atom("_NET_WM_STATE_MAXIMIZED_VERT"),
            _host.Atom("_NET_WM_STATE_MAXIMIZED_HORZ"),
            SourceApplication,
            0,
        });
        _host.Flush();
    }

    /// <summary>Opens the window manager's window menu at a root point, when it supports _GTK_SHOW_WINDOW_MENU.</summary>
    public bool ShowWindowMenu(int rootX, int rootY, ulong time)
    {
        if (!_host.WmSupports("_GTK_SHOW_WINDOW_MENU"))
            return false;
        _host.UngrabPointer(time);
        _host.SendRootClientMessage("_GTK_SHOW_WINDOW_MENU", new long[] { 0, rootX, rootY, 0, 0 });
        _host.Flush();
        return true;
    }
}
