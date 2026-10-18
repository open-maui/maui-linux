// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Maui.Platform.Linux.Interop;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>
/// Window activation on X11 (MAUI's Application.ActivateWindow) and startup notification.
/// Activation asks an EWMH window manager with _NET_ACTIVE_WINDOW, carrying the timestamp of the
/// app's last user interaction so focus-stealing prevention can judge it; without EWMH the window
/// raises and focuses itself. A minimized (unmapped) window is mapped first.
/// </summary>
public partial class X11Window
{
    // The X server time of the app's last key press, button press or touch, across all its
    // windows (one server, one clock): the timestamp _NET_ACTIVE_WINDOW carries.
    private static ulong s_lastUserTime;

    // DESKTOP_STARTUP_ID belongs to the first window the process maps.
    private static bool s_startupIdTaken;
    private string? _startupId;

    private bool _isMapped;
    private bool _focusOnMap;

    /// <summary>The timestamp of the app's last user interaction (0 = none seen yet).</summary>
    internal static ulong LastUserTime => s_lastUserTime;

    private static void NoteUserTime(ulong time)
    {
        if (time != 0)
            s_lastUserTime = time;
    }

    /// <summary>Asks the window manager to raise and focus the window.</summary>
    public bool Activate()
    {
        if (_display == IntPtr.Zero || _window == IntPtr.Zero)
            return false;

        bool ewmh = ((IX11DecorationHost)this).WmSupports("_NET_ACTIVE_WINDOW");
        var steps = X11Activation.Plan(ewmh, _isMapped);
        foreach (var step in steps)
        {
            switch (step)
            {
                case X11ActivationStep.MapRaised:
                    X11.XMapRaised(_display, _window);
                    break;
                case X11ActivationStep.SendNetActiveWindow:
                    ((IX11DecorationHost)this).SendRootClientMessage("_NET_ACTIVE_WINDOW",
                        X11Activation.NetActiveWindowData(s_lastUserTime));
                    break;
                case X11ActivationStep.RaiseWindow:
                    X11.XRaiseWindow(_display, _window);
                    break;
                case X11ActivationStep.SetInputFocus:
                    X11.XSetInputFocus(_display, _window, X11.RevertToParent, s_lastUserTime);
                    break;
                case X11ActivationStep.FocusOnMap:
                    _focusOnMap = true;
                    break;
            }
        }
        X11.XFlush(_display);
        DiagnosticLog.Debug("X11Window", $"Activate: {string.Join(", ", steps)} (user time {s_lastUserTime})");
        return true;
    }

    /// <summary>Completes an activation that had to wait for the window to become viewable.</summary>
    private void FocusIfRequestedOnMap()
    {
        if (!_focusOnMap)
            return;
        _focusOnMap = false;
        X11.XSetInputFocus(_display, _window, X11.RevertToParent, s_lastUserTime);
        X11.XFlush(_display);
    }

    /// <summary>
    /// Takes the launcher's DESKTOP_STARTUP_ID for the first window: _NET_STARTUP_ID on the
    /// window before it maps (the window manager matches the launch, including its timestamp,
    /// for focus-stealing prevention), and the launch time as the first user time.
    /// </summary>
    private unsafe void ApplyStartupId()
    {
        if (s_startupIdTaken)
            return;
        s_startupIdTaken = true;

        var id = Environment.GetEnvironmentVariable("DESKTOP_STARTUP_ID");
        if (string.IsNullOrEmpty(id))
            return;
        // Not inherited by processes this app starts.
        Environment.SetEnvironmentVariable("DESKTOP_STARTUP_ID", null);
        _startupId = id;

        if (X11Activation.TryParseStartupTime(id, out var launchTime))
            NoteUserTime(launchTime);

        var bytes = Encoding.UTF8.GetBytes(id);
        fixed (byte* ptr = bytes)
        {
            X11.XChangeProperty(_display, _window,
                X11.XInternAtom(_display, "_NET_STARTUP_ID", false),
                X11.XInternAtom(_display, "UTF8_STRING", false),
                8, 0 /* PropModeReplace */, (IntPtr)ptr, bytes.Length);
        }
        DiagnosticLog.Debug("X11Window", $"Startup id {id}");
    }

    /// <summary>
    /// Ends the launch's startup notification (the launcher's busy cursor) once the window is
    /// shown: a "remove" message to the root window in _NET_STARTUP_INFO_BEGIN/_NET_STARTUP_INFO pieces.
    /// </summary>
    private void CompleteStartupNotification()
    {
        var id = _startupId;
        if (id == null)
            return;
        _startupId = null;

        var begin = X11.XInternAtom(_display, "_NET_STARTUP_INFO_BEGIN", false);
        var more = X11.XInternAtom(_display, "_NET_STARTUP_INFO", false);
        const long PropertyChangeMask = 1L << 22;
        bool first = true;
        foreach (var chunk in X11Activation.StartupRemoveChunks(id))
        {
            var (l0, l1, l2) = X11Activation.PackFormat8(chunk);
            var xev = new XEvent();
            xev.ClientMessageEvent.Type = X11.ClientMessage;
            xev.ClientMessageEvent.Window = _window;
            xev.ClientMessageEvent.MessageType = first ? begin : more;
            xev.ClientMessageEvent.Format = 8;
            xev.ClientMessageEvent.Data.L0 = l0;
            xev.ClientMessageEvent.Data.L1 = l1;
            xev.ClientMessageEvent.Data.L2 = l2;
            X11.XSendEvent(_display, _rootWindow, false, PropertyChangeMask, ref xev);
            first = false;
        }
    }
}
