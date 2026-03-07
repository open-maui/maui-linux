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
public partial class LinuxApplication : IDisposable
{
    private static int _invalidateCount;
    private static int _requestRedrawCount;
    private static int _drawCount;
    private static int _gtkThreadId;
    public static int GtkThreadId => _gtkThreadId;
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
            DiagnosticLog.Warn("LinuxApplication", $"Invalidate from WRONG THREAD! GTK={_gtkThreadId}, Current={currentThread}, Source={source}");
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
            DiagnosticLog.Warn("LinuxApplication", $"RequestRedraw from WRONG THREAD! GTK={_gtkThreadId}, Current={currentThread}");
        }
    }

    private static void StartHeartbeat()
    {
        _gtkThreadId = Environment.CurrentManagedThreadId;
        DiagnosticLog.Info("LinuxApplication", $"GTK thread ID: {_gtkThreadId}");
        GLibNative.TimeoutAdd(250, () =>
        {
            if (!DiagnosticLog.IsEnabled)
                return true;
            DateTime now = DateTime.Now;
            if ((now - _lastCounterReset).TotalSeconds >= 1.0)
            {
                int invalidates = Interlocked.Exchange(ref _invalidateCount, 0);
                int redraws = Interlocked.Exchange(ref _requestRedrawCount, 0);
                int draws = Interlocked.Exchange(ref _drawCount, 0);
                DiagnosticLog.Debug("LinuxApplication", $"Heartbeat | Invalidate={invalidates}/s, RequestRedraw={redraws}/s, Draw={draws}/s");
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
    /// Thread-safe - will marshal to GTK thread if needed.
    /// </summary>
    public static void RequestRedraw()
    {
        LogRequestRedraw();
        if (_isRedrawing)
            return;

        // Check if we're on the GTK thread
        int currentThread = Environment.CurrentManagedThreadId;
        if (_gtkThreadId != 0 && currentThread != _gtkThreadId)
        {
            // We're on a background thread - use IdleAdd to marshal to GTK thread
            GLibNative.IdleAdd(() =>
            {
                RequestRedrawInternal();
                return false; // Don't repeat
            });
            return;
        }

        RequestRedrawInternal();
    }

    private static void RequestRedrawInternal()
    {
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
                _rootView.Arrange(new Microsoft.Maui.Graphics.Rect(
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
                var oldFocus = _focusedView;
                _focusedView = value;

                // Call OnFocusLost on the old view (this sets IsFocused = false and invalidates)
                oldFocus?.OnFocusLost();

                // Call OnFocusGained on the new view (this sets IsFocused = true and invalidates)
                _focusedView?.OnFocusGained();
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
        // This callback will work for both GTK and X11 modes
        LinuxDialogService.SetInvalidateCallback(() =>
        {
            if (_useGtk)
            {
                _gtkWindow?.RequestRedraw();
            }
            else
            {
                _renderingEngine?.InvalidateAll();
            }
        });
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
}
