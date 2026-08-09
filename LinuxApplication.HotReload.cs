// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux;

public partial class LinuxApplication
{
    /// <summary>
    /// Entry point for the .NET hot-reload handler (see
    /// <see cref="Diagnostics.HotReloadService"/>). Marshals to the UI thread and
    /// rebuilds the affected page's Skia tree so XAML/C# edits become visible
    /// without a restart. The hot-reload agent may call this from a background
    /// thread, so anything touching the view tree is deferred onto the GLib main
    /// loop when we are not already on it.
    /// </summary>
    internal static void OnHotReload(Type[]? updatedTypes)
    {
        var app = Current;
        if (app == null) return;

        int gtkThread = _gtkThreadId;
        if (gtkThread != 0 && Environment.CurrentManagedThreadId != gtkThread)
        {
            // Off the UI thread — queue onto the main loop and return immediately.
            GLibNative.IdleAdd(() =>
            {
                app.ReRenderForHotReload(updatedTypes);
                return false; // one-shot
            });
        }
        else
        {
            app.ReRenderForHotReload(updatedTypes);
        }
    }

    private void ReRenderForHotReload(Type[]? updatedTypes)
    {
        try
        {
            var shell = LinuxViewRenderer.CurrentSkiaShell;
            if (shell != null)
            {
                // Rebuilds every section's page from its template — a fresh
                // InitializeComponent picks up hot-reloaded XAML and C# — and
                // re-swaps the active page, preserving the selected section/item.
                shell.ReRenderContentTrees();
            }
            else
            {
                // Non-Shell root (direct ContentPage / NavigationPage): we don't
                // retain the type + DI context needed to re-instantiate the page,
                // so structural XAML reload isn't wired for this case. C# method-
                // body edits already take effect via CoreCLR on the next call; a
                // redraw surfaces any that affect drawing/layout on the next frame.
                // TODO: to support structural XAML reload for non-Shell roots we
                // would need to retain the root page's Type + IMauiContext and
                // rebuild a fresh instance here (as CreateShellContentPage does),
                // then swap RootView and re-parent it into MAUI's Window.
                DiagnosticLog.Debug("HotReload",
                    "No active SkiaShell; redraw-only (non-Shell root structural XAML reload not wired).");
            }

            if (_useGtk)
                _gtkWindow?.RequestRedraw();
            else
                _renderingEngine?.InvalidateAll();

            DiagnosticLog.Debug("HotReload",
                $"Re-rendered after delta ({updatedTypes?.Length ?? 0} type(s)).");
        }
        catch (Exception ex)
        {
            // Never propagate into the reload callback.
            DiagnosticLog.Error("HotReload", "Re-render failed", ex);
        }
    }
}
