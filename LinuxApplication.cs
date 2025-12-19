// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        if (_focusedView != null)
        {
            _focusedView.OnKeyDown(e);
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
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
        if (_rootView != null)
        {
            var hitView = _rootView.HitTest(e.X, e.Y);
            
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
        if (_rootView != null)
        {
            var hitView = _rootView.HitTest(e.X, e.Y);
            if (hitView != null)
            {
                // Update focus
                if (hitView.IsFocusable)
                {
                    FocusedView = hitView;
                }

                hitView.OnPointerPressed(e);
            }
            else
            {
                FocusedView = null;
            }
        }
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        if (_rootView != null)
        {
            var hitView = _rootView.HitTest(e.X, e.Y);
            hitView?.OnPointerReleased(e);
        }
    }

    private void OnScroll(object? sender, ScrollEventArgs e)
    {
        if (_rootView != null)
        {
            var hitView = _rootView.HitTest(e.X, e.Y);
            // Bubble scroll events up to find a ScrollView
            var view = hitView;
            while (view != null)
            {
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
