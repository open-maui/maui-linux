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
    private static int _isRedrawing;
    private static int _loopCounter = 0;

    private IDisplayWindow? _mainWindow;
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
        // Fast-path peek (advisory only; the atomic claim happens in RequestRedrawInternal).
        if (Volatile.Read(ref _isRedrawing) != 0)
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
        // Atomic claim: only one caller proceeds; the rest see we're already redrawing
        // and bail. Using CompareExchange instead of a plain bool eliminates the TOCTOU
        // window between the check and the assignment.
        if (Interlocked.CompareExchange(ref _isRedrawing, 1, 0) != 0)
            return;

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
            Volatile.Write(ref _isRedrawing, 0);
        }
    }

    /// <summary>
    /// Gets the main window.
    /// </summary>
    public IDisplayWindow? MainWindow => _mainWindow;

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
            if (_rootView != null)
            {
                // Attach the render context so views in this tree can resolve
                // typefaces and request invalidation without a global lookup.
                if (_renderingEngine != null)
                    _rootView.RenderContext = _renderingEngine;

                if (_mainWindow != null)
                {
                    _rootView.Arrange(new Microsoft.Maui.Graphics.Rect(
                        0, 0,
                        _mainWindow.Width,
                        _mainWindow.Height));
                }
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
    /// Gets the HiDPI scale factor detected at startup.
    /// </summary>
    public float DpiScale { get; private set; } = 1.0f;

    /// <summary>
    /// Initializes the application with the specified options.
    /// </summary>
    public void Initialize(LinuxApplicationOptions options)
    {
        // Reuse the scale detected before gtk_init_check (XCURSOR_SIZE setup happens there).
        // Fall back to a fresh detection if Initialize is called outside the Run flow.
        if (EarlyDpiScale is float earlyScale)
        {
            DpiScale = earlyScale;
        }
        else
        {
            var hiDpi = new HiDpiService();
            hiDpi.Initialize();
            DpiScale = hiDpi.ScaleFactor;
        }

        if (DpiScale > 1.0f)
        {
            DiagnosticLog.Debug("LinuxApplication", $"HiDPI detected: scale={DpiScale:F2}");

            // Only apply HiDPI scaling for X11 mode. GTK mode uses native widgets
            // (e.g., WebKitGTK) that handle their own rendering at physical pixels,
            // so canvas scaling would create a mismatch.
            if (!options.UseGtk && options.Width == 800 && options.Height == 600)
            {
                options.Width = (int)(options.Width * DpiScale);
                options.Height = (int)(options.Height * DpiScale);
                DiagnosticLog.Debug("LinuxApplication", $"Scaled window to {options.Width}x{options.Height}");
            }
        }

        // Apply gesture configuration
        Handlers.GestureManager.SwipeMinDistance = options.SwipeMinDistance;
        Handlers.GestureManager.SwipeMaxTime = options.SwipeMaxTime;
        Handlers.GestureManager.SwipeDirectionThreshold = options.SwipeDirectionThreshold;
        Handlers.GestureManager.PanMinDistance = options.PanMinDistance;
        Handlers.GestureManager.PinchScrollScale = options.PinchScrollScale;

        // Apply rendering configuration
        SkiaRenderingEngine.MaxDirtyRegions = options.MaxDirtyRegions;
        SkiaRenderingEngine.RegionMergeThreshold = options.RegionMergeThreshold;

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
        // Display server resolution order:
        //   1. options.DisplayServer if set to a concrete value (programmatic override)
        //   2. WAYLAND_DISPLAY env var present and MAUI_PREFER_X11 not set → Wayland
        //   3. otherwise X11/XWayland
        _mainWindow = DisplayServerFactory.CreateWindow(
            options.Title ?? "MAUI Application",
            options.Width,
            options.Height,
            options.DisplayServer);

        // SkiaWebView reparents WebKitGTK widgets into the host window using raw X11
        // calls; only valid when the main window actually is X11. On native Wayland
        // the WebView falls back to its own toplevel via GTK.
        if (_mainWindow is IX11Surface x11)
        {
            SkiaWebView.SetMainWindow(x11.Display, x11.Handle);
        }

        // Set window icon (X11 _NET_WM_ICON + GTK default icon + .desktop file for GNOME).
        // SetIcon is a no-op on Wayland; the .desktop entry is what GNOME/KDE actually use.
        string? iconPath = ResolveIconPath(options.IconPath);
        if (!string.IsNullOrEmpty(iconPath))
        {
            _mainWindow.SetIcon(iconPath);
            try
            {
                GtkNative.gtk_window_set_default_icon_from_file(iconPath, IntPtr.Zero);
                DiagnosticLog.Debug("LinuxApplication", "Set GTK default icon: " + iconPath);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Debug("LinuxApplication", "Failed to set GTK default icon", ex);
            }
            InstallDesktopEntry(iconPath);
        }

        _renderingEngine = new SkiaRenderingEngine(_mainWindow);
        _renderingEngine.DpiScale = DpiScale;

        _mainWindow.Resized += OnWindowResized;
        _mainWindow.Exposed += OnWindowExposed;
        // All input handlers run synchronously inside native (Wayland/X11)
        // event callbacks; route them through Guarded so a view exception is
        // logged instead of aborting the process through the native frame.
        _mainWindow.KeyDown += Guarded<KeyEventArgs>("key-down", OnKeyDown);
        _mainWindow.KeyUp += Guarded<KeyEventArgs>("key-up", OnKeyUp);
        _mainWindow.TextInput += Guarded<TextInputEventArgs>("text-input", OnTextInput);
        _mainWindow.PointerMoved += Guarded<PointerEventArgs>("pointer-moved", OnPointerMoved);
        _mainWindow.PointerPressed += Guarded<PointerEventArgs>("pointer-pressed", OnPointerPressed);
        _mainWindow.PointerReleased += Guarded<PointerEventArgs>("pointer-released", OnPointerReleased);
        _mainWindow.Scroll += Guarded<ScrollEventArgs>("scroll", OnScroll);
        _mainWindow.CloseRequested += OnCloseRequested;

        // Route native drag-and-drop into MAUI DropGestureRecognizers
        // (additive — DragDropService.Default subscribers are unaffected).
        WireDragDropRouting();
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
            _gtkWindow.SkiaSurface.PointerPressed += Guarded<(double X, double Y, int Button)>("gtk-pointer-pressed", OnGtkPointerPressed);
            _gtkWindow.SkiaSurface.PointerReleased += Guarded<(double X, double Y, int Button)>("gtk-pointer-released", OnGtkPointerReleased);
            _gtkWindow.SkiaSurface.PointerMoved += Guarded<(double X, double Y)>("gtk-pointer-moved", OnGtkPointerMoved);
            _gtkWindow.SkiaSurface.KeyPressed += Guarded<(uint KeyVal, uint KeyCode, uint State)>("gtk-key-pressed", OnGtkKeyPressed);
            _gtkWindow.SkiaSurface.KeyReleased += Guarded<(uint KeyVal, uint KeyCode, uint State)>("gtk-key-released", OnGtkKeyReleased);
            _gtkWindow.SkiaSurface.Scrolled += Guarded<(double X, double Y, double DeltaX, double DeltaY, uint State)>("gtk-scroll", OnGtkScrolled);
            _gtkWindow.SkiaSurface.TextInput += Guarded<string>("gtk-text-input", OnGtkTextInput);
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

    private static void InstallDesktopEntry(string iconPath)
    {
        try
        {
            string appName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "MauiApp");
            string wmClass = appName.Replace(" ", "").Replace("_", "");
            string desktopDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "applications");
            Directory.CreateDirectory(desktopDir);

            string desktopFile = Path.Combine(desktopDir, $"{wmClass.ToLowerInvariant()}.desktop");
            string fullIconPath = Path.GetFullPath(iconPath);
            string content = $"""
                [Desktop Entry]
                Type=Application
                Name={appName}
                Icon={fullIconPath}
                Exec={Environment.ProcessPath} %U
                Terminal=false
                StartupWMClass={wmClass}
                """;
            // Only write if changed to avoid unnecessary disk writes
            if (!File.Exists(desktopFile) || File.ReadAllText(desktopFile) != content)
            {
                File.WriteAllText(desktopFile, content);
                DiagnosticLog.Debug("LinuxApplication", $"Installed desktop entry: {desktopFile}");
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("LinuxApplication", "Failed to install desktop entry", ex);
        }
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
