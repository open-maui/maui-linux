// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.Platform.Linux;

/// <summary>
/// Multi-window support: the platform side of Application.OpenWindow /
/// CloseWindow. A MAUI app calls
/// <c>Application.Current.OpenWindow(new Window(page))</c>; MAUI invokes the
/// ApplicationHandler's OpenWindow command, which resolves the requested
/// Window via IApplication.CreateWindow and lands in
/// <see cref="OpenMauiWindow"/> below to create the native window, render the
/// page, and join the run loop.
/// </summary>
public partial class LinuxApplication
{
    /// <summary>
    /// The MAUI context created at startup. Required to render pages for
    /// windows opened after launch.
    /// </summary>
    internal IMauiContext? MauiContext { get; set; }

    /// <summary>
    /// Opens a native window for the given MAUI IWindow: creates the native
    /// toplevel on the same display server as the primary window, renders the
    /// window's Page through LinuxViewRenderer into a fresh WindowContext,
    /// wires per-window input (through Guarded), sizes from Window.Width /
    /// Height when set, and raises IWindow.Created. The window starts pumping
    /// and rendering on the next run-loop iteration.
    ///
    /// The startup window is a special case: its native window already exists
    /// (created by Initialize before the Application handler was attached), so
    /// the first OpenWindow with an un-adopted primary simply adopts it.
    /// </summary>
    internal void OpenMauiWindow(IWindow window)
    {
        if (window == null)
            return;

        // Adopt the startup window into the primary context (see remarks).
        var primary = PrimaryContext;
        if (primary != null && primary.MauiWindow == null)
        {
            primary.MauiWindow = window;
            DiagnosticLog.Debug("LinuxApplication", "Adopted startup window into primary context");
            return;
        }

        // Already open? (OpenWindow on an existing window activates on other
        // platforms; we just no-op for v1.)
        for (int i = 0; i < _windowContexts.Count; i++)
        {
            if (_windowContexts[i].MauiWindow == window)
            {
                DiagnosticLog.Debug("LinuxApplication", "OpenWindow: window already open; ignoring");
                return;
            }
        }

        if (_useGtk)
        {
            // GTK mode hosts a single GtkHostWindow; secondary toplevels are
            // not supported there (v1 limitation — X11/Wayland modes only).
            DiagnosticLog.Warn("LinuxApplication", "OpenWindow is not supported in GTK mode; ignoring");
            return;
        }

        var mauiContext = MauiContext;
        if (mauiContext == null)
        {
            DiagnosticLog.Warn("LinuxApplication", "OpenWindow: no MAUI context available; ignoring");
            return;
        }

        var page = (window as Microsoft.Maui.Controls.Window)?.Page;
        string title = FirstNonEmpty(window.Title, page?.Title, "OpenMaui App");

        // Window.Width/Height are logical units (NaN when unset). Native
        // window sizes are physical pixels; scale like the primary path does.
        float scale = DpiScale > 0 ? DpiScale : 1f;
        int width = ResolveDimension(window.Width, 800, scale);
        int height = ResolveDimension(window.Height, 600, scale);

        WindowContext? ctx = null;
        try
        {
            // Same display server as the primary: DisplayServerFactory caches
            // the detected/fallback server type from the first window. Each
            // native window owns its own display connection (X11 XOpenDisplay
            // per window; Wayland wl_display_connect per window with its own
            // registry/seat binding — see WaylandWindow.Initialize), so
            // windows pump independently in the shared run loop.
            var native = DisplayServerFactory.CreateWindow(title, width, height);

            var engine = new SkiaRenderingEngine(native)
            {
                DpiScale = DpiScale,
            };

            ctx = AttachWindowContext(native, engine, raisesMauiLifecycle: true);
            ctx.MauiWindow = window;
            ctx.WireInput();

            // Render the window's page into this context's tree.
            SkiaView? root = null;
            if (page != null)
            {
                // Note: LinuxViewRenderer keeps "current renderer/shell"
                // statics for theme refresh and sample navigation; those are
                // last-write-wins and the theme walker covers all windows.
                var renderer = new LinuxViewRenderer(mauiContext);
                root = renderer.RenderPage(page);
            }

            if (root != null)
            {
                ctx.RootView = root;
            }
            else
            {
                DiagnosticLog.Warn("LinuxApplication", "OpenWindow: window has no renderable Page; showing empty window");
            }

            ctx.NotifyCreated();
            native.Show();
            ctx.Render();

            DiagnosticLog.Info("LinuxApplication",
                $"Opened window '{title}' ({width}x{height}); {_windowContexts.Count} window(s) live");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("LinuxApplication", "OpenWindow failed", ex);
            if (ctx != null)
            {
                _windowContexts.Remove(ctx);
                try { ctx.Dispose(); } catch { /* best effort */ }
            }
        }
    }

    /// <summary>
    /// Closes the native window presenting the given MAUI IWindow. The actual
    /// teardown (IWindow.Destroying, engine/native dispose, registry removal)
    /// happens in the run loop's ReapClosedContexts; the app exits when the
    /// last window closes.
    /// </summary>
    internal void CloseMauiWindow(IWindow window)
    {
        for (int i = 0; i < _windowContexts.Count; i++)
        {
            var ctx = _windowContexts[i];
            if (ctx.MauiWindow == window)
            {
                HandleContextCloseRequested(ctx);
                ctx.DisplayWindow?.Stop();
                return;
            }
        }
        DiagnosticLog.Debug("LinuxApplication", "CloseWindow: no native window found for IWindow");
    }

    private static string FirstNonEmpty(string? a, string? b, string fallback)
        => !string.IsNullOrEmpty(a) ? a! : !string.IsNullOrEmpty(b) ? b! : fallback;

    private static int ResolveDimension(double requestedLogical, int defaultLogical, float scale)
    {
        double logical = requestedLogical > 0
            && !double.IsNaN(requestedLogical)
            && !double.IsInfinity(requestedLogical)
                ? requestedLogical
                : defaultLogical;
        return Math.Max(1, (int)Math.Round(logical * scale));
    }
}
