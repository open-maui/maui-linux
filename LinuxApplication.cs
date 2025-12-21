// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Window;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.Platform.Linux;

/// <summary>
/// Main Linux application class that bootstraps the MAUI application.
/// </summary>
public class LinuxApplication : IDisposable
{
    private X11Window? _mainWindow;
    private SkiaRenderingEngine? _renderingEngine;
    private SkiaView? _rootView;
    private SkiaView? _focusedView;
    private SkiaView? _hoveredView;
    private SkiaView? _capturedView; // View that has captured pointer events during drag
    private bool _disposed;

    /// <summary>
    /// Gets the current application instance.
    /// </summary>
    public static LinuxApplication? Current { get; private set; }

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
        var options = app.Services.GetService<LinuxApplicationOptions>()
                      ?? new LinuxApplicationOptions();
        configure?.Invoke(options);
        ParseCommandLineOptions(args, options);

        using var linuxApp = new LinuxApplication();
        linuxApp.Initialize(options);

        // Create MAUI context
        var mauiContext = new Hosting.LinuxMauiContext(app.Services, linuxApp);

        // Get the application and render it
        var application = app.Services.GetService<IApplication>();
        SkiaView? rootView = null;

        if (application is Microsoft.Maui.Controls.Application mauiApplication)
        {
            // Force Application.Current to be this instance
            // The constructor sets Current = this, but we ensure it here
            var currentProperty = typeof(Microsoft.Maui.Controls.Application).GetProperty("Current");
            if (currentProperty != null && currentProperty.CanWrite)
            {
                currentProperty.SetValue(null, mauiApplication);
            }

            if (mauiApplication.MainPage != null)
            {
                // Create a MAUI Window and add it to the application
                // This ensures Shell.Current works (it reads from Application.Current.Windows[0].Page)
                var mainPage = mauiApplication.MainPage;

                // Always ensure we have a window with the Shell/Page
                var windowsField = typeof(Microsoft.Maui.Controls.Application).GetField("_windows",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var windowsList = windowsField?.GetValue(mauiApplication) as System.Collections.Generic.List<Microsoft.Maui.Controls.Window>;

                if (windowsList != null && windowsList.Count == 0)
                {
                    var mauiWindow = new Microsoft.Maui.Controls.Window(mainPage);
                    windowsList.Add(mauiWindow);
                    mauiWindow.Parent = mauiApplication;
                }
                else if (windowsList != null && windowsList.Count > 0 && windowsList[0].Page == null)
                {
                    // Window exists but has no page - set it
                    windowsList[0].Page = mainPage;
                }

                var renderer = new Hosting.LinuxViewRenderer(mauiContext);
                rootView = renderer.RenderPage(mainPage);

                // Update window title based on app name (NavigationPage.Title takes precedence)
                string windowTitle = "OpenMaui App";
                if (mainPage is Microsoft.Maui.Controls.NavigationPage navPage)
                {
                    // Prefer NavigationPage.Title (app name) over CurrentPage.Title (page name) for window title
                    windowTitle = navPage.Title ?? windowTitle;
                }
                else if (mainPage is Microsoft.Maui.Controls.Shell shell)
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

        // Fallback to demo if no view
        if (rootView == null)
        {
            rootView = Hosting.LinuxProgramHost.CreateDemoView();
        }

        linuxApp.RootView = rootView;
        linuxApp.Run();
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
        // Create the main window
        _mainWindow = new X11Window(
            options.Title ?? "MAUI Application",
            options.Width,
            options.Height);

        // Create the rendering engine
        _renderingEngine = new SkiaRenderingEngine(_mainWindow);

        // Wire up events
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

        // Register platform services
        RegisterServices();
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
        if (_mainWindow == null)
            throw new InvalidOperationException("Application not initialized");

        _mainWindow.Show();

        // Initial render
        Render();

        // Run the event loop
        while (_mainWindow.IsRunning)
        {
            _mainWindow.ProcessEvents();

            // Update animations and render
            UpdateAnimations();
            Render();

            // Small delay to prevent 100% CPU usage
            Thread.Sleep(1);
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
            }

            hitView?.OnPointerMoved(e);
        }
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        Console.WriteLine($"[LinuxApplication] OnPointerPressed at ({e.X}, {e.Y})");

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

/// <summary>
/// Options for Linux application initialization.
/// </summary>
public class LinuxApplicationOptions
{
    /// <summary>
    /// Gets or sets the window title.
    /// </summary>
    public string? Title { get; set; } = "MAUI Application";

    /// <summary>
    /// Gets or sets the initial window width.
    /// </summary>
    public int Width { get; set; } = 800;

    /// <summary>
    /// Gets or sets the initial window height.
    /// </summary>
    public int Height { get; set; } = 600;

    /// <summary>
    /// Gets or sets whether to use hardware acceleration.
    /// </summary>
    public bool UseHardwareAcceleration { get; set; } = true;

    /// <summary>
    /// Gets or sets the display server type.
    /// </summary>
    public DisplayServerType DisplayServer { get; set; } = DisplayServerType.Auto;

    /// <summary>
    /// Gets or sets whether to force demo mode instead of loading the application's pages.
    /// </summary>
    public bool ForceDemo { get; set; } = false;
}

/// <summary>
/// Display server type options.
/// </summary>
public enum DisplayServerType
{
    /// <summary>
    /// Automatically detect the display server.
    /// </summary>
    Auto,

    /// <summary>
    /// Use X11 (Xorg).
    /// </summary>
    X11,

    /// <summary>
    /// Use Wayland.
    /// </summary>
    Wayland
}
