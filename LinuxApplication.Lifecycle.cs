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
using Microsoft.Maui.Platform.Linux.Interop;
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
        // Force X11 backend for GTK/WebKitGTK - MUST be set before any GTK code runs.
        // Skip if the user has explicitly set GDK_BACKEND (e.g. for native-Wayland testing).
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GDK_BACKEND")))
        {
            Environment.SetEnvironmentVariable("GDK_BACKEND", "x11");
        }

        // Cursor sizing — must happen before gtk_init_check so GTK's own cursor loading
        // sees the right XCURSOR_SIZE. Detection is cheap; LinuxApplication.Initialize
        // reuses the result via _earlyDpiScale.
        DetectScaleAndConfigureCursor();

        // Resolve options once; we need them both for the GtkHostService bring-up
        // (below) and for the LinuxApplication initialization further down.
        var options = app.Services.GetService<LinuxApplicationOptions>() ?? new LinuxApplicationOptions();
        configure?.Invoke(options);
        ParseCommandLineOptions(args, options);

        // Initialize the GTK host service early. Idempotent: when X11 mode is selected,
        // SkiaWebView still needs a backing GTK widget hierarchy via this hidden host
        // window; when GTK mode is selected, InitializeGtk reuses this same instance
        // through GetOrCreateHostWindow, so we never create two host windows.
        GtkHostService.Instance.Initialize(
            options.Title ?? "MAUI Application",
            options.Width,
            options.Height);

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

                        // Refresh shell colors (reads bound values, which now
                        // come from the MauiView directly via SkiaView).
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
            return;
        }

        if (_mainWindow is X11Window x11)
        {
            RunX11(x11);
        }
        else if (_mainWindow is WaylandWindow wl)
        {
            RunWayland(wl);
        }
        else
        {
            throw new InvalidOperationException("No display window available");
        }
    }

    private void RunX11(X11Window window)
    {
        window.Show();
        Render();

        // Cap the wait at ~60Hz so animations get a chance to tick even when no input
        // arrives. When events are pending, poll() returns immediately and the loop
        // runs as fast as needed; when idle, CPU drops from ~100% to near zero.
        const int IdleTimeoutMs = 16;
        var pollFd = new LibcNative.PollFd
        {
            Fd = X11.XConnectionNumber(window.Display),
            Events = LibcNative.POLLIN,
        };

        DiagnosticLog.Debug("LinuxApplication", "Starting X11 event loop");
        while (window.IsRunning)
        {
            _loopCounter++;
            if (_loopCounter % 1000 == 0)
            {
                DiagnosticLog.Debug("LinuxApplication", $"Loop iteration {_loopCounter}");
            }

            window.ProcessEvents();
            window.FlushDeferredResize();
            // Process GLib events (idle callbacks, timeouts) so that
            // MainThread.BeginInvokeOnMainThread dispatches execute.
            // This is required for libraries like LiveCharts that use
            // Task.Run + InvokeOnUIThread for chart updates.
            GLibNative.ProcessPendingEvents();
            SkiaWebView.ProcessGtkEvents();
            UpdateAnimations();
            Render();
            window.AcknowledgeSync();

            // Block until an X event arrives or the frame budget elapses.
            // Skip the wait if X events are already queued.
            if (X11.XPending(window.Display) == 0)
            {
                pollFd.Revents = 0;
                LibcNative.Poll(ref pollFd, 1, IdleTimeoutMs);
            }
        }
        DiagnosticLog.Debug("LinuxApplication", "X11 event loop ended");
    }

    private void RunWayland(WaylandWindow window)
    {
        // Wayland event loop with non-blocking dispatch and frame-rate cap.
        // Full subsystem support (cursor, keyboard via xkbcommon, clipboard, IME,
        // fractional scale) is layered in across Stage 2b–2f.
        window.Show();
        Render();

        const int IdleTimeoutMs = 16;
        var pollFd = new LibcNative.PollFd
        {
            Fd = window.GetFileDescriptor(),
            Events = LibcNative.POLLIN,
        };

        DiagnosticLog.Debug("LinuxApplication", "Starting Wayland event loop");
        while (window.IsRunning)
        {
            _loopCounter++;
            if (_loopCounter % 1000 == 0)
                DiagnosticLog.Debug("LinuxApplication", $"Loop iteration {_loopCounter}");

            // Drain any events callbacks queued during the previous frame, then flush
            // outgoing requests so the compositor sees what we did.
            window.ProcessEvents();
            GLibNative.ProcessPendingEvents();
            SkiaWebView.ProcessGtkEvents();
            UpdateAnimations();
            Render();

            // Block until the compositor has something for us, or our frame budget
            // expires (animations need ticking even when idle).
            pollFd.Revents = 0;
            int polled = LibcNative.Poll(ref pollFd, 1, IdleTimeoutMs);
            if (polled > 0 && (pollFd.Revents & LibcNative.POLLIN) != 0)
            {
                window.DispatchReadEvents();
            }
        }
        DiagnosticLog.Debug("LinuxApplication", "Wayland event loop ended");
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

    private static float _earlyDpiScale;
    internal static float? EarlyDpiScale => _earlyDpiScale > 0 ? _earlyDpiScale : null;

    private static void DetectScaleAndConfigureCursor()
    {
        var hiDpi = new HiDpiService();
        hiDpi.Initialize();
        _earlyDpiScale = hiDpi.ScaleFactor;

        if (Environment.GetEnvironmentVariable("XCURSOR_SIZE") is null)
        {
            var cursorSize = (int)Math.Round(24 * Math.Max(1.0f, _earlyDpiScale));
            Environment.SetEnvironmentVariable(
                "XCURSOR_SIZE",
                cursorSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
