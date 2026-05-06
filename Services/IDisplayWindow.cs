// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Cross-platform contract for a top-level window backed by either X11 or Wayland.
/// Concrete implementations are in <see cref="X11Window"/> and <see cref="WaylandWindow"/>.
/// X11-only operations (e.g. WebKitGTK reparenting, RandR queries) live on
/// <see cref="IX11Surface"/>; consumers that need them should pattern-match.
/// </summary>
public interface IDisplayWindow : IDisposable
{
    int Width { get; }
    int Height { get; }
    bool IsRunning { get; }

    event EventHandler<KeyEventArgs>? KeyDown;
    event EventHandler<KeyEventArgs>? KeyUp;
    event EventHandler<TextInputEventArgs>? TextInput;
    event EventHandler<PointerEventArgs>? PointerMoved;
    event EventHandler<PointerEventArgs>? PointerPressed;
    event EventHandler<PointerEventArgs>? PointerReleased;
    event EventHandler<ScrollEventArgs>? Scroll;
    event EventHandler? Exposed;
    event EventHandler<(int Width, int Height)>? Resized;
    event EventHandler? CloseRequested;
    event EventHandler? FocusGained;
    event EventHandler? FocusLost;

    void Show();
    void Hide();
    void SetTitle(string title);
    void Resize(int width, int height);
    void SetCursor(CursorType cursorType);
    void SetIcon(string iconPath);
    void SetWMClass(string resName, string resClass);

    void ProcessEvents();
    void Stop();

    /// <summary>
    /// Returns the file descriptor that becomes readable when the display server has
    /// events pending. Pass to poll() to block efficiently in the event loop.
    /// </summary>
    int GetFileDescriptor();

    /// <summary>
    /// Presents an ARGB32 pixel buffer. On X11 this issues XPutImage; on Wayland this
    /// copies into the wl_shm buffer and commits the surface.
    /// </summary>
    void Present(IntPtr pixels, int width, int height, int stride);

    /// <summary>
    /// X11-specific lifecycle hook to fire deferred resize events. No-op on Wayland.
    /// </summary>
    void FlushDeferredResize();

    /// <summary>
    /// X11-specific hook to acknowledge _NET_WM_SYNC_REQUEST during live resize. No-op on Wayland.
    /// </summary>
    void AcknowledgeSync();
}

/// <summary>
/// X11-specific extensions to <see cref="IDisplayWindow"/>. Implemented only by
/// <see cref="X11Window"/>; consumers that depend on raw X11 handles (e.g.
/// WebKitGTK reparenting, XRandR) should pattern-match against this interface.
/// </summary>
public interface IX11Surface
{
    /// <summary>The X11 Display* this window was created against.</summary>
    IntPtr Display { get; }

    /// <summary>The X11 Window XID.</summary>
    IntPtr Handle { get; }
}
