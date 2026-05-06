// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Dispatching;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux;

public partial class LinuxApplication
{
    /// <summary>
    /// Runs a MAUI application on Linux.
    /// This is the main entry point for Linux apps.
    /// </summary>
    /// <param name="app">The MauiApp to run.</param>
    /// <param name="args">Command line arguments.</param>
    public static void Run(MauiApp app, string[] args)
    {
        Run(app, args, null);
    }

    /// <summary>
    /// Runs a MAUI application on Linux with options.
    /// </summary>
    /// <param name="app">The MauiApp to run.</param>
    /// <param name="args">Command line arguments.</param>
    /// <param name="configure">Optional configuration action.</param>
    public static void Run(MauiApp app, string[] args, Action<LinuxApplicationOptions>? configure)
    {
        // Force X11 backend for GTK/WebKitGTK - MUST be set before any GTK code runs
        Environment.SetEnvironmentVariable("GDK_BACKEND", "x11");

        // Pre-initialize GTK for WebView compatibility (even when using X11 mode)
        int argc = 0;
        IntPtr argv = IntPtr.Zero;
        if (!GtkNative.gtk_init_check(ref argc, ref argv))
        {
            DiagnosticLog.Warn("LinuxApplication", "GTK initialization failed - WebView may not work");
        }
        else
        {
            DiagnosticLog.Debug("LinuxApplication", "GTK pre-initialized for WebView support");
        }

        // Set application name for desktop integration (taskbar, etc.)
        // Try to get the name from environment or use executable name
        string? appName = Environment.GetEnvironmentVariable("APPIMAGE_NAME");
        if (string.IsNullOrEmpty(appName))
        {
            appName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "MauiApp");
        }
        string prgName = appName.Replace(" ", "");
        GtkNative.g_set_prgname(prgName);
        GtkNative.g_set_application_name(appName);
        DiagnosticLog.Debug("LinuxApplication", $"Set application name: {appName} (prgname: {prgName})");

        // Initialize dispatcher
        LinuxDispatcher.Initialize();
        DispatcherProvider.SetCurrent(LinuxDispatcherProvider.Instance);
        DiagnosticLog.Debug("LinuxApplication", "Dispatcher initialized");

        var options = app.Services.GetService<LinuxApplicationOptions>()
                      ?? new LinuxApplicationOptions();
        configure?.Invoke(options);
        ParseCommandLineOptions(args, options);

        var linuxApp = new LinuxApplication();
        try
        {
            linuxApp.Initialize(options);

            // Create MAUI context
            var mauiContext = new LinuxMauiContext(app.Services, linuxApp);

            // Get the application and render it
            var application = app.Services.GetService<IApplication>();
            SkiaView? rootView = null;

            if (application is Application mauiApplication)
            {
                // Force Application.Current to be this instance
                var currentProperty = typeof(Application).GetProperty("Current");
                if (currentProperty != null && currentProperty.CanWrite)
                {
                    currentProperty.SetValue(null, mauiApplication);
                }

                // Set initial theme based on system theme
                var systemTheme = SystemThemeService.Instance.CurrentTheme;
                DiagnosticLog.Debug("LinuxApplication", $"System theme detected at startup: {systemTheme}");
                if (systemTheme == SystemTheme.Dark)
                {
                    mauiApplication.UserAppTheme = AppTheme.Dark;
                    DiagnosticLog.Debug("LinuxApplication", "Set initial UserAppTheme to Dark based on system theme");
                }
                else
                {
                    mauiApplication.UserAppTheme = AppTheme.Light;
                    DiagnosticLog.Debug("LinuxApplication", "Set initial UserAppTheme to Light based on system theme");
                }

                // Initialize GTK theme service and apply initial CSS
                GtkThemeService.ApplyTheme();

                // Handle user-initiated theme changes
                ((BindableObject)mauiApplication).PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == "UserAppTheme")
                    {
                        DiagnosticLog.Debug("LinuxApplication", $"User theme changed to: {mauiApplication.UserAppTheme}");

                        // Apply GTK CSS for dialogs, menus, and window decorations
                        GtkThemeService.ApplyTheme();

                        // Force property re-evaluation first so AppThemeBindings settle
                        linuxApp.RefreshPageForThemeChange();

                        // Then refresh shell colors (reads now-updated bound values)
                        LinuxViewRenderer.CurrentSkiaShell?.RefreshTheme();

                        // Invalidate to redraw - use correct method based on mode
                        if (linuxApp._useGtk)
                        {
                            linuxApp._gtkWindow?.RequestRedraw();
                        }
                        else
                        {
                            linuxApp._renderingEngine?.InvalidateAll();
                        }
                    }
                };

                // Handle system theme changes (e.g., GNOME/KDE dark mode toggle)
                SystemThemeService.Instance.ThemeChanged += (s, e) =>
                {
                    DiagnosticLog.Debug("LinuxApplication", $"System theme changed to: {e.NewTheme}");

                    // Update MAUI's UserAppTheme to match system theme
                    // This will trigger the PropertyChanged handler which does the refresh
                    var newAppTheme = e.NewTheme == SystemTheme.Dark ? AppTheme.Dark : AppTheme.Light;
                    if (mauiApplication.UserAppTheme != newAppTheme)
                    {
                        DiagnosticLog.Debug("LinuxApplication", $"Setting UserAppTheme to {newAppTheme} to match system");
                        mauiApplication.UserAppTheme = newAppTheme;
                    }
                    else
                    {
                        // If UserAppTheme didn't change (user manually set it), still refresh
                        linuxApp.RefreshPageForThemeChange();
                        LinuxViewRenderer.CurrentSkiaShell?.RefreshTheme();
                        if (linuxApp._useGtk)
                        {
                            linuxApp._gtkWindow?.RequestRedraw();
                        }
                        else
                        {
                            linuxApp._renderingEngine?.InvalidateAll();
                        }
                    }
                };

                // Get the main page via CreateWindow (the standard MAUI pattern)
                Page? mainPage = null;

                try
                {
                    // Use IApplication interface to call CreateWindow without reflection
                    var appInterface = (IApplication)mauiApplication;
                    var mauiWindow = appInterface.CreateWindow(null!) as Microsoft.Maui.Controls.Window;

                    if (mauiWindow != null)
                    {
                        DiagnosticLog.Debug("LinuxApplication", $"Got Window from CreateWindow: {mauiWindow.GetType().Name}");
                        mainPage = mauiWindow.Page;
                        DiagnosticLog.Debug("LinuxApplication", $"Window.Page: {mainPage?.GetType().Name}");

                        // Ensure window is registered with the application
                        if (!appInterface.Windows.Contains(mauiWindow))
                        {
                            mauiApplication.OpenWindow(mauiWindow);
                        }
                    }
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Error("LinuxApplication", $"CreateWindow failed: {ex.Message}");
                }

                // Fall back to MainPage if CreateWindow didn't produce a page
                if (mainPage == null && mauiApplication.MainPage != null)
                {
                    DiagnosticLog.Debug("LinuxApplication", $"Falling back to MainPage: {mauiApplication.MainPage.GetType().Name}");
                    mainPage = mauiApplication.MainPage;

                    if (mauiApplication.Windows.Count == 0)
                    {
                        var mauiWindow = new Microsoft.Maui.Controls.Window(mainPage);
                        mauiApplication.OpenWindow(mauiWindow);
                    }
                    else if (mauiApplication.Windows[0] is Microsoft.Maui.Controls.Window w && w.Page == null)
                    {
                        w.Page = mainPage;
                    }
                }

                if (mainPage != null)
                {
                    var renderer = new LinuxViewRenderer(mauiContext);
                    rootView = renderer.RenderPage(mainPage);

                    string windowTitle = "OpenMaui App";
                    if (mainPage is NavigationPage navPage)
                    {
                        windowTitle = navPage.Title ?? windowTitle;
                    }
                    else if (mainPage is Shell shell)
                    {
                        windowTitle = shell.Title ?? windowTitle;
                    }
                    else
                    {
                        windowTitle = mainPage.Title ?? windowTitle;
                    }
                    linuxApp.SetWindowTitle(windowTitle);
                }
            }

            if (rootView == null)
            {
                rootView = LinuxProgramHost.CreateDemoView();
            }

            linuxApp.RootView = rootView;
            linuxApp.Run();
        }
        finally
        {
            linuxApp?.Dispose();
        }
    }

    private static void ParseCommandLineOptions(string[] args, LinuxApplicationOptions options)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--title" when i + 1 < args.Length:
                    options.Title = args[++i];
                    break;
                case "--width" when i + 1 < args.Length && int.TryParse(args[i + 1], out var w):
                    options.Width = w;
                    i++;
                    break;
                case "--height" when i + 1 < args.Length && int.TryParse(args[i + 1], out var h):
                    options.Height = h;
                    i++;
                    break;
            }
        }
    }

    /// <summary>
    /// Shows the main window and runs the event loop.
    /// </summary>
    public void Run()
    {
        if (_useGtk)
        {
            RunGtk();
        }
        else
        {
            RunX11();
        }
    }

    private void RunX11()
    {
        if (_mainWindow == null)
            throw new InvalidOperationException("Application not initialized");

        _mainWindow.Show();
        Render();

        DiagnosticLog.Debug("LinuxApplication", "Starting event loop");
        while (_mainWindow.IsRunning)
        {
            _loopCounter++;
            if (_loopCounter % 1000 == 0)
            {
                DiagnosticLog.Debug("LinuxApplication", $"Loop iteration {_loopCounter}");
            }

            _mainWindow.ProcessEvents();
            _mainWindow.FlushDeferredResize();
            // Process GLib events (idle callbacks, timeouts) so that
            // MainThread.BeginInvokeOnMainThread dispatches execute.
            // This is required for libraries like LiveCharts that use
            // Task.Run + InvokeOnUIThread for chart updates.
            GLibNative.ProcessPendingEvents();
            SkiaWebView.ProcessGtkEvents();
            UpdateAnimations();
            Render();
            _mainWindow.AcknowledgeSync();
            Thread.Sleep(1);
        }
        DiagnosticLog.Debug("LinuxApplication", "Event loop ended");
    }

    private void RunGtk()
    {
        if (_gtkWindow == null)
            throw new InvalidOperationException("Application not initialized");

        StartHeartbeat();
        PerformGtkLayout(_gtkWindow.Width, _gtkWindow.Height);
        _gtkWindow.RequestRedraw();
        _gtkWindow.Run();
        GtkHostService.Instance.Shutdown();
    }

    private void PerformGtkLayout(int width, int height)
    {
        if (_rootView != null)
        {
            _rootView.Measure(new Microsoft.Maui.Graphics.Size(width, height));
            _rootView.Arrange(new Microsoft.Maui.Graphics.Rect(0, 0, width, height));
        }
    }

    /// <summary>
    /// Forces all views to refresh their theme-dependent properties.
    /// This is needed because AppThemeBinding may not automatically trigger
    /// property mappers on all platforms.
    /// </summary>
    private void RefreshPageForThemeChange()
    {
        DiagnosticLog.Debug("LinuxApplication", "RefreshPageForThemeChange - forcing property updates");

        // First, try to trigger MAUI's RequestedThemeChanged event using reflection
        // This ensures AppThemeBinding bindings re-evaluate
        TriggerMauiThemeChanged();

        if (_rootView == null) return;

        // Traverse the visual tree and force theme-dependent properties to update
        RefreshViewTheme(_rootView);
    }

    /// <summary>
    /// Called after theme change to refresh views.
    /// Note: MAUI's Application.UserAppTheme setter automatically triggers RequestedThemeChanged
    /// via WeakEventManager, which AppThemeBinding subscribes to. This method handles
    /// any additional platform-specific refresh needed.
    /// </summary>
    private void TriggerMauiThemeChanged()
    {
        var app = Application.Current;
        if (app == null) return;

        DiagnosticLog.Debug("LinuxApplication", $"Theme is now: {app.UserAppTheme}, RequestedTheme: {app.RequestedTheme}");
    }

    private void RefreshViewTheme(SkiaView view)
    {
        // Get the associated MAUI view and handler
        var mauiView = view.MauiView;
        var handler = mauiView?.Handler;

        if (handler != null && mauiView != null)
        {
            // Force key properties to be re-mapped
            // This ensures theme-dependent bindings are re-evaluated
            try
            {
                // Background/BackgroundColor - both need updating for AppThemeBinding
                handler.UpdateValue(nameof(IView.Background));
                handler.UpdateValue("BackgroundColor");

                // For ImageButton, force Source to be re-mapped
                if (mauiView is Microsoft.Maui.Controls.ImageButton)
                {
                    handler.UpdateValue(nameof(IImageSourcePart.Source));
                }

                // For Image, force Source to be re-mapped
                if (mauiView is Microsoft.Maui.Controls.Image)
                {
                    handler.UpdateValue(nameof(IImageSourcePart.Source));
                }

                // For views with text colors
                if (mauiView is ITextStyle)
                {
                    handler.UpdateValue(nameof(ITextStyle.TextColor));
                }

                // For Entry/Editor placeholder colors
                if (mauiView is IPlaceholder)
                {
                    handler.UpdateValue(nameof(IPlaceholder.PlaceholderColor));
                }

                // For Border stroke
                if (mauiView is IBorderStroke)
                {
                    handler.UpdateValue(nameof(IBorderStroke.Stroke));
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("LinuxApplication", $"Error refreshing theme for {mauiView.GetType().Name}: {ex.Message}");
            }
        }

        // Special handling for Shell - force flyout color properties to re-map
        if (mauiView is Shell && handler != null)
        {
            try
            {
                handler.UpdateValue(nameof(Shell.FlyoutBackgroundColor));
                handler.UpdateValue(nameof(Shell.FlyoutBackground));
                handler.UpdateValue("ForegroundColor");
                handler.UpdateValue("TitleColor");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Debug("LinuxApplication", $"Error refreshing Shell flyout colors: {ex.Message}", ex);
            }

            // Refresh flyout header and footer views (they aren't part of the normal children)
            if (view is SkiaShell skiaShell)
            {
                if (skiaShell.FlyoutHeaderView != null)
                    RefreshViewTheme(skiaShell.FlyoutHeaderView);
                if (skiaShell.FlyoutFooterView != null)
                    RefreshViewTheme(skiaShell.FlyoutFooterView);
            }
        }

        // Special handling for ItemsViews (CollectionView, ListView)
        // Their item views are cached separately and need to be refreshed
        if (view is SkiaItemsView itemsView)
        {
            itemsView.RefreshTheme();
        }

        // Special handling for NavigationPage - it stores content in _currentPage
        if (view is SkiaNavigationPage navPage && navPage.CurrentPage != null)
        {
            RefreshViewTheme(navPage.CurrentPage);
            navPage.Invalidate(); // Force redraw of navigation page
        }

        // Special handling for SkiaPage - refresh via MauiPage handler and process Content
        if (view is SkiaPage page)
        {
            // Refresh page properties via handler if MauiPage is set
            var pageHandler = page.MauiPage?.Handler;
            if (pageHandler != null)
            {
                try
                {
                    DiagnosticLog.Debug("LinuxApplication", $"Refreshing page theme: {page.MauiPage?.GetType().Name}");
                    pageHandler.UpdateValue(nameof(IView.Background));
                    pageHandler.UpdateValue("BackgroundColor");
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Error("LinuxApplication", $"Error refreshing page theme: {ex.Message}");
                }
            }

            page.Invalidate(); // Force redraw to pick up theme-aware background
            if (page.Content != null)
            {
                RefreshViewTheme(page.Content);
            }
        }

        // Recursively process children
        // Note: SkiaLayoutView hides SkiaView.Children with 'new', so we need to cast
        IReadOnlyList<SkiaView> children = view is SkiaLayoutView layout ? layout.Children : view.Children;
        foreach (var child in children)
        {
            RefreshViewTheme(child);
        }
    }

    private void Render()
    {
        if (_renderingEngine != null && _rootView != null)
        {
            _renderingEngine.Render(_rootView);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _renderingEngine?.Dispose();
            _mainWindow?.Dispose();

            if (Current == this)
                Current = null;

            _disposed = true;
        }
    }
}
