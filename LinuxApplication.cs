// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
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

/// <summary>
/// Main Linux application class that bootstraps the MAUI application.
/// </summary>
public class LinuxApplication : IDisposable
{
    private static int _invalidateCount;
    private static int _requestRedrawCount;
    private static int _drawCount;
    private static int _gtkThreadId;
    private static DateTime _lastCounterReset = DateTime.Now;
    private static bool _isRedrawing;
    private static int _loopCounter = 0;

    private X11Window? _mainWindow;
    private GtkHostWindow? _gtkWindow;
    private SkiaRenderingEngine? _renderingEngine;
    private SkiaView? _rootView;
    private SkiaView? _focusedView;
    private SkiaView? _hoveredView;
    private SkiaView? _capturedView; // View that has captured pointer events during drag
    private bool _disposed;
    private bool _useGtk;

    /// <summary>
    /// Gets the current application instance.
    /// </summary>
    public static LinuxApplication? Current { get; private set; }

    /// <summary>
    /// Gets whether the application is running in GTK mode.
    /// </summary>
    public static bool IsGtkMode => Current?._useGtk ?? false;

    /// <summary>
    /// Logs an invalidate call for diagnostics.
    /// </summary>
    public static void LogInvalidate(string source)
    {
        int currentThread = Environment.CurrentManagedThreadId;
        Interlocked.Increment(ref _invalidateCount);
        if (currentThread != _gtkThreadId && _gtkThreadId != 0)
        {
            Console.WriteLine($"[DIAG] ⚠️ Invalidate from WRONG THREAD! GTK={_gtkThreadId}, Current={currentThread}, Source={source}");
        }
    }

    /// <summary>
    /// Logs a request redraw call for diagnostics.
    /// </summary>
    public static void LogRequestRedraw()
    {
        int currentThread = Environment.CurrentManagedThreadId;
        Interlocked.Increment(ref _requestRedrawCount);
        if (currentThread != _gtkThreadId && _gtkThreadId != 0)
        {
            Console.WriteLine($"[DIAG] ⚠️ RequestRedraw from WRONG THREAD! GTK={_gtkThreadId}, Current={currentThread}");
        }
    }

    private static void StartHeartbeat()
    {
        _gtkThreadId = Environment.CurrentManagedThreadId;
        Console.WriteLine($"[DIAG] GTK thread ID: {_gtkThreadId}");
        GLibNative.TimeoutAdd(250, () =>
        {
            DateTime now = DateTime.Now;
            if ((now - _lastCounterReset).TotalSeconds >= 1.0)
            {
                int invalidates = Interlocked.Exchange(ref _invalidateCount, 0);
                int redraws = Interlocked.Exchange(ref _requestRedrawCount, 0);
                int draws = Interlocked.Exchange(ref _drawCount, 0);
                Console.WriteLine($"[DIAG] ❤️ Heartbeat | Invalidate={invalidates}/s, RequestRedraw={redraws}/s, Draw={draws}/s");
                _lastCounterReset = now;
            }
            return true;
        });
    }

    /// <summary>
    /// Logs a draw call for diagnostics.
    /// </summary>
    public static void LogDraw()
    {
        Interlocked.Increment(ref _drawCount);
    }

    /// <summary>
    /// Requests a redraw of the application.
    /// </summary>
    public static void RequestRedraw()
    {
        LogRequestRedraw();
        if (_isRedrawing)
            return;

        _isRedrawing = true;
        try
        {
            if (Current != null && Current._useGtk)
            {
                Current._gtkWindow?.RequestRedraw();
            }
            else
            {
                Current?._renderingEngine?.InvalidateAll();
            }
        }
        finally
        {
            _isRedrawing = false;
        }
    }

    /// <summary>
    /// Gets the main window.
    /// </summary>
    public X11Window? MainWindow => _mainWindow;

    /// <summary>
    /// Gets the rendering engine.
    /// </summary>
    public SkiaRenderingEngine? RenderingEngine => _renderingEngine;

    /// <summary>
    /// Gets or sets the root view.
    /// </summary>
    public SkiaView? RootView
    {
        get => _rootView;
        set
        {
            _rootView = value;
            if (_rootView != null && _mainWindow != null)
            {
                _rootView.Arrange(new SkiaSharp.SKRect(
                    0, 0,
                    _mainWindow.Width,
                    _mainWindow.Height));
            }
        }
    }

    /// <summary>
    /// Gets or sets the currently focused view.
    /// </summary>
    public SkiaView? FocusedView
    {
        get => _focusedView;
        set
        {
            if (_focusedView != value)
            {
                if (_focusedView != null)
                {
                    _focusedView.IsFocused = false;
                }

                _focusedView = value;

                if (_focusedView != null)
                {
                    _focusedView.IsFocused = true;
                }
            }
        }
    }

    /// <summary>
    /// Creates a new Linux application.
    /// </summary>
    public LinuxApplication()
    {
        Current = this;

        // Set up dialog service invalidation callback
        LinuxDialogService.SetInvalidateCallback(() => _renderingEngine?.InvalidateAll());
    }

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
            Console.WriteLine("[LinuxApplication] Warning: GTK initialization failed - WebView may not work");
        }
        else
        {
            Console.WriteLine("[LinuxApplication] GTK pre-initialized for WebView support");
        }

        // Initialize dispatcher
        LinuxDispatcher.Initialize();
        DispatcherProvider.SetCurrent(LinuxDispatcherProvider.Instance);
        Console.WriteLine("[LinuxApplication] Dispatcher initialized");

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

                // Handle user-initiated theme changes
                ((BindableObject)mauiApplication).PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == "UserAppTheme")
                    {
                        Console.WriteLine($"[LinuxApplication] User theme changed to: {mauiApplication.UserAppTheme}");
                        LinuxViewRenderer.CurrentSkiaShell?.RefreshTheme();
                        linuxApp._renderingEngine?.InvalidateAll();
                    }
                };

                // Handle system theme changes (e.g., GNOME/KDE dark mode toggle)
                SystemThemeService.Instance.ThemeChanged += (s, e) =>
                {
                    Console.WriteLine($"[LinuxApplication] System theme changed to: {e.NewTheme}");
                    // Notify MAUI framework that system theme changed
                    // This will cause AppThemeBinding to re-evaluate
                    LinuxViewRenderer.CurrentSkiaShell?.RefreshTheme();
                    linuxApp._renderingEngine?.InvalidateAll();
                };

                if (mauiApplication.MainPage != null)
                {
                    var mainPage = mauiApplication.MainPage;

                    var windowsField = typeof(Application).GetField("_windows",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    var windowsList = windowsField?.GetValue(mauiApplication) as List<Microsoft.Maui.Controls.Window>;

                    if (windowsList != null && windowsList.Count == 0)
                    {
                        var mauiWindow = new Microsoft.Maui.Controls.Window(mainPage);
                        windowsList.Add(mauiWindow);
                        mauiWindow.Parent = mauiApplication;
                    }
                    else if (windowsList != null && windowsList.Count > 0 && windowsList[0].Page == null)
                    {
                        windowsList[0].Page = mainPage;
                    }

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
    /// Initializes the application with the specified options.
    /// </summary>
    public void Initialize(LinuxApplicationOptions options)
    {
        _useGtk = options.UseGtk;
        if (_useGtk)
        {
            InitializeGtk(options);
        }
        else
        {
            InitializeX11(options);
        }
        RegisterServices();
    }

    private void InitializeX11(LinuxApplicationOptions options)
    {
        _mainWindow = new X11Window(
            options.Title ?? "MAUI Application",
            options.Width,
            options.Height);

        // Set up WebView main window
        SkiaWebView.SetMainWindow(_mainWindow.Display, _mainWindow.Handle);

        // Set window icon
        string? iconPath = ResolveIconPath(options.IconPath);
        if (!string.IsNullOrEmpty(iconPath))
        {
            _mainWindow.SetIcon(iconPath);
        }

        _renderingEngine = new SkiaRenderingEngine(_mainWindow);

        _mainWindow.Resized += OnWindowResized;
        _mainWindow.Exposed += OnWindowExposed;
        _mainWindow.KeyDown += OnKeyDown;
        _mainWindow.KeyUp += OnKeyUp;
        _mainWindow.TextInput += OnTextInput;
        _mainWindow.PointerMoved += OnPointerMoved;
        _mainWindow.PointerPressed += OnPointerPressed;
        _mainWindow.PointerReleased += OnPointerReleased;
        _mainWindow.Scroll += OnScroll;
        _mainWindow.CloseRequested += OnCloseRequested;
    }

    private void InitializeGtk(LinuxApplicationOptions options)
    {
        _gtkWindow = GtkHostService.Instance.GetOrCreateHostWindow(
            options.Title ?? "MAUI Application",
            options.Width,
            options.Height);

        string? iconPath = ResolveIconPath(options.IconPath);
        if (!string.IsNullOrEmpty(iconPath))
        {
            GtkHostService.Instance.SetWindowIcon(iconPath);
        }

        if (_gtkWindow.SkiaSurface != null)
        {
            _gtkWindow.SkiaSurface.DrawRequested += OnGtkDrawRequested;
            _gtkWindow.SkiaSurface.PointerPressed += OnGtkPointerPressed;
            _gtkWindow.SkiaSurface.PointerReleased += OnGtkPointerReleased;
            _gtkWindow.SkiaSurface.PointerMoved += OnGtkPointerMoved;
            _gtkWindow.SkiaSurface.KeyPressed += OnGtkKeyPressed;
            _gtkWindow.SkiaSurface.KeyReleased += OnGtkKeyReleased;
            _gtkWindow.SkiaSurface.Scrolled += OnGtkScrolled;
            _gtkWindow.SkiaSurface.TextInput += OnGtkTextInput;
        }
        _gtkWindow.Resized += OnGtkResized;
    }

    private static string? ResolveIconPath(string? explicitPath)
    {
        if (!string.IsNullOrEmpty(explicitPath))
        {
            if (Path.IsPathRooted(explicitPath))
            {
                return File.Exists(explicitPath) ? explicitPath : null;
            }
            string resolved = Path.Combine(AppContext.BaseDirectory, explicitPath);
            return File.Exists(resolved) ? resolved : null;
        }

        string baseDir = AppContext.BaseDirectory;

        // Check for appicon.meta (generated icon)
        string metaPath = Path.Combine(baseDir, "appicon.meta");
        if (File.Exists(metaPath))
        {
            string? generated = MauiIconGenerator.GenerateIcon(metaPath);
            if (!string.IsNullOrEmpty(generated) && File.Exists(generated))
            {
                return generated;
            }
        }

        // Check for appicon.png
        string pngPath = Path.Combine(baseDir, "appicon.png");
        if (File.Exists(pngPath)) return pngPath;

        // Check for appicon.svg
        string svgPath = Path.Combine(baseDir, "appicon.svg");
        if (File.Exists(svgPath)) return svgPath;

        return null;
    }

    private void RegisterServices()
    {
        // Platform services would be registered with the DI container here
        // For now, we create singleton instances
    }

    /// <summary>
    /// Sets the window title.
    /// </summary>
    public void SetWindowTitle(string title)
    {
        _mainWindow?.SetTitle(title);
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

        Console.WriteLine("[LinuxApplication] Starting event loop");
        while (_mainWindow.IsRunning)
        {
            _loopCounter++;
            if (_loopCounter % 1000 == 0)
            {
                Console.WriteLine($"[LinuxApplication] Loop iteration {_loopCounter}");
            }

            _mainWindow.ProcessEvents();
            SkiaWebView.ProcessGtkEvents();
            UpdateAnimations();
            Render();
            Thread.Sleep(1);
        }
        Console.WriteLine("[LinuxApplication] Event loop ended");
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
            _rootView.Measure(new SKSize(width, height));
            _rootView.Arrange(new SKRect(0, 0, width, height));
        }
    }

    private void UpdateAnimations()
    {
        // Update cursor blink for entry controls
        if (_focusedView is SkiaEntry entry)
        {
            entry.UpdateCursorBlink();
        }
    }

    private void Render()
    {
        if (_renderingEngine != null && _rootView != null)
        {
            _renderingEngine.Render(_rootView);
        }
    }

    private void OnWindowResized(object? sender, (int Width, int Height) size)
    {
        if (_rootView != null)
        {
            // Re-measure with new available size, then arrange
            var availableSize = new SkiaSharp.SKSize(size.Width, size.Height);
            _rootView.Measure(availableSize);
            _rootView.Arrange(new SkiaSharp.SKRect(0, 0, size.Width, size.Height));
        }
        _renderingEngine?.InvalidateAll();
    }

    private void OnWindowExposed(object? sender, EventArgs e)
    {
        Render();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            LinuxDialogService.TopDialog?.OnKeyDown(e);
            return;
        }

        if (_focusedView != null)
        {
            _focusedView.OnKeyDown(e);
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            LinuxDialogService.TopDialog?.OnKeyUp(e);
            return;
        }

        if (_focusedView != null)
        {
            _focusedView.OnKeyUp(e);
        }
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_focusedView != null)
        {
            _focusedView.OnTextInput(e);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        // Route to context menu if one is active
        if (LinuxDialogService.HasContextMenu)
        {
            LinuxDialogService.ActiveContextMenu?.OnPointerMoved(e);
            return;
        }

        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            LinuxDialogService.TopDialog?.OnPointerMoved(e);
            return;
        }

        if (_rootView != null)
        {
            // If a view has captured the pointer, send all events to it
            if (_capturedView != null)
            {
                _capturedView.OnPointerMoved(e);
                return;
            }

            // Check for popup overlay first
            var popupOwner = SkiaView.GetPopupOwnerAt(e.X, e.Y);
            var hitView = popupOwner ?? _rootView.HitTest(e.X, e.Y);

            // Track hover state changes
            if (hitView != _hoveredView)
            {
                _hoveredView?.OnPointerExited(e);
                _hoveredView = hitView;
                _hoveredView?.OnPointerEntered(e);

                // Update cursor based on view's cursor type
                CursorType cursor = hitView?.CursorType ?? CursorType.Arrow;
                _mainWindow?.SetCursor(cursor);
            }

            hitView?.OnPointerMoved(e);
        }
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        Console.WriteLine($"[LinuxApplication] OnPointerPressed at ({e.X}, {e.Y})");

        // Route to context menu if one is active
        if (LinuxDialogService.HasContextMenu)
        {
            LinuxDialogService.ActiveContextMenu?.OnPointerPressed(e);
            return;
        }

        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            LinuxDialogService.TopDialog?.OnPointerPressed(e);
            return;
        }

        if (_rootView != null)
        {
            // Check for popup overlay first
            var popupOwner = SkiaView.GetPopupOwnerAt(e.X, e.Y);
            var hitView = popupOwner ?? _rootView.HitTest(e.X, e.Y);
            Console.WriteLine($"[LinuxApplication] HitView: {hitView?.GetType().Name ?? "null"}, rootView: {_rootView.GetType().Name}");

            if (hitView != null)
            {
                // Capture pointer to this view for drag operations
                _capturedView = hitView;

                // Update focus
                if (hitView.IsFocusable)
                {
                    FocusedView = hitView;
                }

                Console.WriteLine($"[LinuxApplication] Calling OnPointerPressed on {hitView.GetType().Name}");
                hitView.OnPointerPressed(e);
            }
            else
            {
                // Close any open popups when clicking outside
                if (SkiaView.HasActivePopup && _focusedView != null)
                {
                    _focusedView.OnFocusLost();
                }
                FocusedView = null;
            }
        }
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            LinuxDialogService.TopDialog?.OnPointerReleased(e);
            return;
        }

        if (_rootView != null)
        {
            // If a view has captured the pointer, send release to it
            if (_capturedView != null)
            {
                _capturedView.OnPointerReleased(e);
                _capturedView = null; // Release capture
                return;
            }

            // Check for popup overlay first
            var popupOwner = SkiaView.GetPopupOwnerAt(e.X, e.Y);
            var hitView = popupOwner ?? _rootView.HitTest(e.X, e.Y);
            hitView?.OnPointerReleased(e);
        }
    }

    private void OnScroll(object? sender, ScrollEventArgs e)
    {
        Console.WriteLine($"[LinuxApplication] OnScroll - X={e.X}, Y={e.Y}, DeltaX={e.DeltaX}, DeltaY={e.DeltaY}");
        if (_rootView != null)
        {
            var hitView = _rootView.HitTest(e.X, e.Y);
            Console.WriteLine($"[LinuxApplication] HitView: {hitView?.GetType().Name ?? "null"}");
            // Bubble scroll events up to find a ScrollView
            var view = hitView;
            while (view != null)
            {
                Console.WriteLine($"[LinuxApplication] Bubbling to: {view.GetType().Name}");
                if (view is SkiaScrollView scrollView)
                {
                    scrollView.OnScroll(e);
                    return;
                }
                view.OnScroll(e);
                if (e.Handled) return;
                view = view.Parent;
            }
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        _mainWindow?.Stop();
    }

    // GTK Event Handlers
    private void OnGtkDrawRequested(object? sender, EventArgs e)
    {
        Console.WriteLine("[DIAG] >>> OnGtkDrawRequested ENTER");
        LogDraw();
        var surface = _gtkWindow?.SkiaSurface;
        if (surface?.Canvas != null && _rootView != null)
        {
            var bgColor = Application.Current?.UserAppTheme == AppTheme.Dark
                ? new SKColor(32, 33, 36)
                : SKColors.White;
            surface.Canvas.Clear(bgColor);
            Console.WriteLine("[DIAG] Drawing rootView...");
            _rootView.Draw(surface.Canvas);
            Console.WriteLine("[DIAG] Drawing dialogs...");
            var bounds = new SKRect(0, 0, surface.Width, surface.Height);
            LinuxDialogService.DrawDialogs(surface.Canvas, bounds);
            Console.WriteLine("[DIAG] <<< OnGtkDrawRequested EXIT");
        }
    }

    private void OnGtkResized(object? sender, (int Width, int Height) size)
    {
        PerformGtkLayout(size.Width, size.Height);
        _gtkWindow?.RequestRedraw();
    }

    private void OnGtkPointerPressed(object? sender, (double X, double Y, int Button) e)
    {
        string buttonName = e.Button == 1 ? "Left" : e.Button == 2 ? "Middle" : e.Button == 3 ? "Right" : $"Unknown({e.Button})";
        Console.WriteLine($"[LinuxApplication.GTK] PointerPressed at ({e.X:F1}, {e.Y:F1}), Button={e.Button} ({buttonName})");

        if (LinuxDialogService.HasContextMenu)
        {
            var button = e.Button == 1 ? PointerButton.Left : e.Button == 2 ? PointerButton.Middle : PointerButton.Right;
            var args = new PointerEventArgs((float)e.X, (float)e.Y, button);
            LinuxDialogService.ActiveContextMenu?.OnPointerPressed(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        if (_rootView == null)
        {
            Console.WriteLine("[LinuxApplication.GTK] _rootView is null!");
            return;
        }

        var hitView = _rootView.HitTest((float)e.X, (float)e.Y);
        Console.WriteLine($"[LinuxApplication.GTK] HitView: {hitView?.GetType().Name ?? "null"}");

        if (hitView != null)
        {
            if (hitView.IsFocusable && _focusedView != hitView)
            {
                _focusedView?.OnFocusLost();
                _focusedView = hitView;
                _focusedView.OnFocusGained();
            }
            _capturedView = hitView;
            var button = e.Button == 1 ? PointerButton.Left : e.Button == 2 ? PointerButton.Middle : PointerButton.Right;
            var args = new PointerEventArgs((float)e.X, (float)e.Y, button);
            Console.WriteLine("[DIAG] >>> Before OnPointerPressed");
            hitView.OnPointerPressed(args);
            Console.WriteLine("[DIAG] <<< After OnPointerPressed, calling RequestRedraw");
            _gtkWindow?.RequestRedraw();
            Console.WriteLine("[DIAG] <<< After RequestRedraw, returning from handler");
        }
    }

    private void OnGtkPointerReleased(object? sender, (double X, double Y, int Button) e)
    {
        Console.WriteLine("[DIAG] >>> OnGtkPointerReleased ENTER");
        if (_rootView == null) return;

        if (_capturedView != null)
        {
            var button = e.Button == 1 ? PointerButton.Left : e.Button == 2 ? PointerButton.Middle : PointerButton.Right;
            var args = new PointerEventArgs((float)e.X, (float)e.Y, button);
            Console.WriteLine($"[DIAG] Calling OnPointerReleased on {_capturedView.GetType().Name}");
            _capturedView.OnPointerReleased(args);
            Console.WriteLine("[DIAG] OnPointerReleased returned");
            _capturedView = null;
            _gtkWindow?.RequestRedraw();
            Console.WriteLine("[DIAG] <<< OnGtkPointerReleased EXIT (captured path)");
        }
        else
        {
            var hitView = _rootView.HitTest((float)e.X, (float)e.Y);
            if (hitView != null)
            {
                var button = e.Button == 1 ? PointerButton.Left : e.Button == 2 ? PointerButton.Middle : PointerButton.Right;
                var args = new PointerEventArgs((float)e.X, (float)e.Y, button);
                hitView.OnPointerReleased(args);
                _gtkWindow?.RequestRedraw();
            }
        }
    }

    private void OnGtkPointerMoved(object? sender, (double X, double Y) e)
    {
        if (LinuxDialogService.HasContextMenu)
        {
            var args = new PointerEventArgs((float)e.X, (float)e.Y);
            LinuxDialogService.ActiveContextMenu?.OnPointerMoved(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        if (_rootView == null) return;

        if (_capturedView != null)
        {
            var args = new PointerEventArgs((float)e.X, (float)e.Y);
            _capturedView.OnPointerMoved(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        var hitView = _rootView.HitTest((float)e.X, (float)e.Y);
        if (hitView != _hoveredView)
        {
            var args = new PointerEventArgs((float)e.X, (float)e.Y);
            _hoveredView?.OnPointerExited(args);
            _hoveredView = hitView;
            _hoveredView?.OnPointerEntered(args);
            _gtkWindow?.RequestRedraw();
        }

        if (hitView != null)
        {
            var args = new PointerEventArgs((float)e.X, (float)e.Y);
            hitView.OnPointerMoved(args);
        }
    }

    private void OnGtkKeyPressed(object? sender, (uint KeyVal, uint KeyCode, uint State) e)
    {
        if (_focusedView != null)
        {
            var key = ConvertGdkKey(e.KeyVal);
            var modifiers = ConvertGdkModifiers(e.State);
            var args = new KeyEventArgs(key, modifiers);
            _focusedView.OnKeyDown(args);
            _gtkWindow?.RequestRedraw();
        }
    }

    private void OnGtkKeyReleased(object? sender, (uint KeyVal, uint KeyCode, uint State) e)
    {
        if (_focusedView != null)
        {
            var key = ConvertGdkKey(e.KeyVal);
            var modifiers = ConvertGdkModifiers(e.State);
            var args = new KeyEventArgs(key, modifiers);
            _focusedView.OnKeyUp(args);
            _gtkWindow?.RequestRedraw();
        }
    }

    private void OnGtkScrolled(object? sender, (double X, double Y, double DeltaX, double DeltaY, uint State) e)
    {
        if (_rootView == null) return;

        // Convert GDK state to KeyModifiers
        var modifiers = ConvertGdkStateToModifiers(e.State);
        bool isCtrlPressed = (modifiers & KeyModifiers.Control) != 0;

        var hitView = _rootView.HitTest((float)e.X, (float)e.Y);

        // Check for pinch gesture (Ctrl+Scroll) first
        if (isCtrlPressed && hitView?.MauiView != null)
        {
            if (Handlers.GestureManager.ProcessScrollAsPinch(hitView.MauiView, e.X, e.Y, e.DeltaY, true))
            {
                _gtkWindow?.RequestRedraw();
                return;
            }
        }

        while (hitView != null)
        {
            if (hitView is SkiaScrollView scrollView)
            {
                var args = new ScrollEventArgs((float)e.X, (float)e.Y, (float)e.DeltaX, (float)e.DeltaY, modifiers);
                scrollView.OnScroll(args);
                _gtkWindow?.RequestRedraw();
                break;
            }
            hitView = hitView.Parent;
        }
    }

    private static KeyModifiers ConvertGdkStateToModifiers(uint state)
    {
        var modifiers = KeyModifiers.None;
        // GDK modifier masks
        const uint GDK_SHIFT_MASK = 1 << 0;
        const uint GDK_CONTROL_MASK = 1 << 2;
        const uint GDK_MOD1_MASK = 1 << 3;  // Alt
        const uint GDK_SUPER_MASK = 1 << 26;
        const uint GDK_LOCK_MASK = 1 << 1;  // Caps Lock

        if ((state & GDK_SHIFT_MASK) != 0) modifiers |= KeyModifiers.Shift;
        if ((state & GDK_CONTROL_MASK) != 0) modifiers |= KeyModifiers.Control;
        if ((state & GDK_MOD1_MASK) != 0) modifiers |= KeyModifiers.Alt;
        if ((state & GDK_SUPER_MASK) != 0) modifiers |= KeyModifiers.Super;
        if ((state & GDK_LOCK_MASK) != 0) modifiers |= KeyModifiers.CapsLock;

        return modifiers;
    }

    private void OnGtkTextInput(object? sender, string text)
    {
        if (_focusedView != null)
        {
            var args = new TextInputEventArgs(text);
            _focusedView.OnTextInput(args);
            _gtkWindow?.RequestRedraw();
        }
    }

    private static Key ConvertGdkKey(uint keyval)
    {
        return keyval switch
        {
            65288 => Key.Backspace,
            65289 => Key.Tab,
            65293 => Key.Enter,
            65307 => Key.Escape,
            65360 => Key.Home,
            65361 => Key.Left,
            65362 => Key.Up,
            65363 => Key.Right,
            65364 => Key.Down,
            65365 => Key.PageUp,
            65366 => Key.PageDown,
            65367 => Key.End,
            65535 => Key.Delete,
            >= 32 and <= 126 => (Key)keyval,
            _ => Key.Unknown
        };
    }

    private static KeyModifiers ConvertGdkModifiers(uint state)
    {
        var modifiers = KeyModifiers.None;
        if ((state & 1) != 0) modifiers |= KeyModifiers.Shift;
        if ((state & 4) != 0) modifiers |= KeyModifiers.Control;
        if ((state & 8) != 0) modifiers |= KeyModifiers.Alt;
        return modifiers;
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
