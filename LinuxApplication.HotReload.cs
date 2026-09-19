// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux;

public partial class LinuxApplication
{
    // Root context retained at startup so a hot-reload delta can rebuild
    // non-Shell roots (raw ContentPage/NavigationPage as the window page).
    // Only populated when a hot-reload agent is attached, so this stays
    // empty (and the rebuild path inert) in Release / normal runs.
    private static LinuxViewRenderer? s_hotReloadRenderer;
    private static Microsoft.Maui.Controls.Window? s_hotReloadWindow;
    private static Page? s_hotReloadRootPage;

    /// <summary>
    /// Retains the renderer, MAUI window, and root page so a hot-reload delta
    /// can rebuild the root when the app has no Shell. No-op unless a
    /// hot-reload agent is attached.
    /// </summary>
    internal static void TrackRootForHotReload(
        LinuxViewRenderer renderer, Microsoft.Maui.Controls.Window? window, Page page)
    {
        if (!Diagnostics.HotReloadService.IsActive) return;
        s_hotReloadRenderer = renderer;
        s_hotReloadWindow = window;
        s_hotReloadRootPage = page;
    }

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
            else if (!TryRebuildNonShellRoot())
            {
                // No rebuildable root retained. C# method-body edits already take
                // effect via CoreCLR on the next call; the redraw below surfaces
                // any that affect drawing/layout on the next frame.
                DiagnosticLog.Debug("HotReload",
                    "No active SkiaShell and no tracked root page; redraw-only.");
            }

            if (_useGtk)
                _gtkWindow?.RequestRedraw();
            else
                InvalidateAllWindows();

            DiagnosticLog.Debug("HotReload",
                $"Re-rendered after delta ({updatedTypes?.Length ?? 0} type(s)).");
        }
        catch (Exception ex)
        {
            // Never propagate into the reload callback.
            DiagnosticLog.Error("HotReload", "Re-render failed", ex);
        }
    }

    /// <summary>
    /// Structural reload for non-Shell roots: builds a fresh instance of the
    /// tracked root page (a fresh InitializeComponent picks up the new XAML),
    /// re-parents it into the MAUI Window, renders it through the retained
    /// renderer, and swaps the root SkiaView. NavigationPage roots restart at
    /// their root page — pushed pages are popped, which matches MAUI's own
    /// replace-with-fresh-instance semantics for structural deltas
    /// (MauiHotReloadHelper.GetReplacedView never reconstructs runtime nav
    /// stacks either).
    /// </summary>
    private bool TryRebuildNonShellRoot()
    {
        var renderer = s_hotReloadRenderer;
        var oldPage = s_hotReloadRootPage;
        if (renderer == null || oldPage == null)
            return false;

        var newPage = BuildFreshRootPage(renderer, oldPage);
        if (newPage == null)
            return false;

        // Disappearing before the swap so the outgoing instance unwires its
        // subscriptions (mirrors SkiaShell.SendPageLifecycle ordering).
        try
        {
            (oldPage as IPageController)?.SendDisappearing();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("HotReload", "SendDisappearing threw", ex);
        }

        // Re-parent into the Window before rendering: bindings and resources
        // resolve through the Application-rooted parent chain during handler
        // mapping, and Page.SendAppearing silently no-ops without it.
        var window = s_hotReloadWindow;
        if (window != null)
            window.Page = newPage;

        var newView = renderer.RenderPage(newPage);
        if (newView == null)
        {
            DiagnosticLog.Error("HotReload",
                $"Re-render of {newPage.GetType().Name} produced no view; keeping previous tree");
            if (window != null)
                window.Page = oldPage;
            return false;
        }

        // Pointers into the old tree must not survive the swap. Hot reload
        // tracks the PRIMARY window's root only (v1 — secondary windows get
        // C# method-body edits via CoreCLR but no structural rebuild).
        var primaryCtx = PrimaryContext;
        if (primaryCtx != null)
        {
            primaryCtx.FocusedView = null;
            primaryCtx.HoveredView = null;
            primaryCtx.CapturedView = null;
        }

        RootView = newView;
        var mainWindow = MainWindow;
        if (_useGtk && _gtkWindow != null)
        {
            PerformGtkLayout(_gtkWindow.Width, _gtkWindow.Height);
        }
        else if (mainWindow != null)
        {
            // RootView's setter only arranges; measure first like OnWindowResized.
            newView.Measure(new Microsoft.Maui.Graphics.Size(mainWindow.Width, mainWindow.Height));
            newView.Arrange(new Microsoft.Maui.Graphics.Rect(0, 0, mainWindow.Width, mainWindow.Height));
        }

        try
        {
            (newPage as IPageController)?.SendAppearing();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("HotReload", "SendAppearing threw", ex);
        }

        oldPage.Handler?.DisconnectHandler();
        s_hotReloadRootPage = newPage;
        DiagnosticLog.Info("HotReload", $"Rebuilt non-Shell root: {newPage.GetType().Name}");
        return true;
    }

    private static Page? BuildFreshRootPage(LinuxViewRenderer renderer, Page oldPage)
    {
        try
        {
            if (oldPage.GetType() == typeof(NavigationPage) && oldPage is NavigationPage oldNav)
            {
                // Plain `new NavigationPage(rootPage)` root: the type worth
                // rebuilding is the page inside. Re-wrap it, carrying over the
                // code-set bar styling a bare re-instantiation can't restore.
                var stack = oldNav.Navigation.NavigationStack;
                var innerType = stack.Count > 0 ? stack[0]?.GetType() : null;
                if (innerType == null) return null;

                var inner = CreatePageInstance(renderer, innerType);
                if (inner == null) return null;

                return new NavigationPage(inner)
                {
                    Title = oldNav.Title,
                    BarBackgroundColor = oldNav.BarBackgroundColor,
                    BarTextColor = oldNav.BarTextColor,
                };
            }

            // Subclassed NavigationPage / ContentPage / TabbedPage / FlyoutPage:
            // the subclass constructor rebuilds its own structure.
            return CreatePageInstance(renderer, oldPage.GetType());
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("HotReload", "Root page rebuild failed", ex);
            return null;
        }
    }

    private static Page? CreatePageInstance(LinuxViewRenderer renderer, Type pageType)
    {
        // DI-first (constructor injection), matching CreateShellContentPage.
        try
        {
            return ActivatorUtilities.CreateInstance(renderer.Services, pageType) as Page;
        }
        catch (Exception diEx)
        {
            DiagnosticLog.Debug("HotReload", $"DI construction failed for {pageType.Name}: {diEx.Message}");
        }

        try
        {
            return Activator.CreateInstance(pageType) as Page;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("HotReload", $"Cannot construct {pageType.Name}", ex);
            return null;
        }
    }
}
