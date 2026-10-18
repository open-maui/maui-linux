// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Interop;

namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>What the X11 window does with one XI2 touch event.</summary>
internal enum X11TouchAction
{
    /// <summary>Nothing: another window's touch, a second finger, or the rest of a touch the decorations took.</summary>
    None,
    /// <summary>The first finger went down over the content: move there, then press (left button, touch).</summary>
    Press,
    /// <summary>The first finger moved.</summary>
    Move,
    /// <summary>The first finger lifted.</summary>
    Release,
    /// <summary>The first finger went down on the client-drawn decorations, which took it as a click.</summary>
    Decorations,
}

/// <summary>
/// XI2 touch sequences to OpenMaui's single pointer, as the Wayland window does with wl_touch:
/// the first finger down drives press/move/release (marked touch); other fingers are ignored
/// until it lifts. A first finger the decorations take (titlebar move, edge resize, window
/// buttons) produces no pointer events for the rest of its sequence.
/// </summary>
internal sealed class X11TouchTracker
{
    private uint? _primary;
    private bool _primaryOnDecorations;

    /// <summary>Whether a tracked finger is down (core pointer button 1/motion are then emulation, not a mouse).</summary>
    public bool IsTouchDown => _primary.HasValue;

    /// <summary>The first finger's position in window device pixels.</summary>
    public float X { get; private set; }

    /// <summary>The first finger's position in window device pixels.</summary>
    public float Y { get; private set; }

    /// <summary>Whether an XI version negotiated with the server delivers touch events (2.2 or later).</summary>
    public static bool SupportsTouch(int major, int minor) => major > 2 || (major == 2 && minor >= 2);

    /// <summary>
    /// Classifies one touch event for <paramref name="window"/>. <paramref name="decorationsPress"/>
    /// is offered the first finger's press and returns true when the decorations took it.
    /// </summary>
    public X11TouchAction Handle(in X11TouchEvent e, IntPtr window, Func<X11TouchEvent, bool>? decorationsPress)
    {
        if (e.EventWindow != window)
            return X11TouchAction.None;

        switch (e.EvType)
        {
            case XInput2.XI_TouchBegin:
                if (_primary.HasValue)
                    return X11TouchAction.None;
                _primary = e.TouchId;
                SetPoint(e);
                if (decorationsPress != null && decorationsPress(e))
                {
                    _primaryOnDecorations = true;
                    return X11TouchAction.Decorations;
                }
                _primaryOnDecorations = false;
                return X11TouchAction.Press;

            case XInput2.XI_TouchUpdate:
                if (_primary != e.TouchId || _primaryOnDecorations)
                    return X11TouchAction.None;
                SetPoint(e);
                return X11TouchAction.Move;

            case XInput2.XI_TouchEnd:
                if (_primary != e.TouchId)
                    return X11TouchAction.None;
                _primary = null;
                if (_primaryOnDecorations)
                {
                    _primaryOnDecorations = false;
                    return X11TouchAction.None;
                }
                SetPoint(e);
                return X11TouchAction.Release;

            default:
                return X11TouchAction.None;
        }
    }

    private void SetPoint(in X11TouchEvent e)
    {
        X = (float)e.EventX;
        Y = (float)e.EventY;
    }
}
