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

    // Multi-window registry. The first entry is the PRIMARY window; the legacy
    // single-window members below (MainWindow, RenderingEngine, RootView,
    // FocusedView) forward to it so all existing consumers keep working.
    private readonly List<WindowContext> _windowContexts = new();
    private GtkHostWindow? _gtkWindow;
    private bool _disposed;
    private bool _useGtk;

    // The context whose native window currently has OS keyboard focus.
    private WindowContext? _focusedContext;

    // The context that hosts the active modal dialog / context menu. Latched
    // when a dialog first appears (focused window at that moment, else
    // primary) and cleared when no dialog is active. Dialogs are app-modal:
    // they render in and receive input from this window only.
    private WindowContext? _dialogHostContext;

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
                // A view requesting a redraw through the static path doesn't
                // know which window owns it; invalidating every window's
                // engine is cheap and correct (views with a RenderContext
                // invalidate their own engine directly instead).
                Current?.InvalidateAllWindows();
            }
        }
        finally
        {
            Volatile.Write(ref _isRedrawing, 0);
        }
    }

    /// <summary>
    /// The primary window's context (first live window). Null before
    /// Initialize and after the last window closes.
    /// </summary>
    public WindowContext? PrimaryContext => _windowContexts.Count > 0 ? _windowContexts[0] : null;

    /// <summary>
    /// All live window contexts. Index 0 is the primary window.
    /// </summary>
    public IReadOnlyList<WindowContext> WindowContexts => _windowContexts;

    /// <summary>
    /// The context whose native window currently has OS keyboard focus, when
    /// known; falls back to the primary context.
    /// </summary>
    public WindowContext? FocusedContext => _focusedContext ?? PrimaryContext;

    /// <summary>
    /// Gets the main (primary) window. Forwards to the primary context.
    /// </summary>
    public IDisplayWindow? MainWindow => PrimaryContext?.DisplayWindow;

    /// <summary>
    /// Gets the primary window's rendering engine. Forwards to the primary context.
    /// </summary>
    public SkiaRenderingEngine? RenderingEngine => PrimaryContext?.RenderingEngine;

    /// <summary>
    /// Gets or sets the primary window's root view. Forwards to the primary
    /// context (creating a bare context when none exists yet, which preserves
    /// pre-Initialize assignment behavior for embedding/tests).
    /// </summary>
    public SkiaView? RootView
    {
        get => PrimaryContext?.RootView;
        set
        {
            var ctx = PrimaryContext ?? AttachWindowContext(null, null, raisesMauiLifecycle: false);
            ctx.RootView = value;
        }
    }

    /// <summary>
    /// Gets or sets the currently focused view — the focused window's focused
    /// view. Forwards to the focused (or primary) context; setter semantics
    /// (OnFocusLost/OnFocusGained) live in <see cref="WindowContext.FocusedView"/>.
    /// </summary>
    public SkiaView? FocusedView
    {
        get => FocusedContext?.FocusedView;
        set
        {
            var ctx = FocusedContext;
            if (ctx != null)
                ctx.FocusedView = value;
        }
    }

    /// <summary>
    /// Creates and registers a window context. The first registered context
    /// becomes the primary window.
    /// </summary>
    internal WindowContext AttachWindowContext(
        IDisplayWindow? displayWindow,
        SkiaRenderingEngine? renderingEngine,
        bool raisesMauiLifecycle)
    {
        var ctx = new WindowContext(this, displayWindow, renderingEngine)
        {
            RaisesMauiLifecycle = raisesMauiLifecycle,
        };
        _windowContexts.Add(ctx);
        return ctx;
    }

    /// <summary>
    /// Invalidates every live window's rendering engine.
    /// </summary>
    internal void InvalidateAllWindows()
    {
        for (int i = 0; i < _windowContexts.Count; i++)
            _windowContexts[i].RenderingEngine?.InvalidateAll();
    }

    /// <summary>
    /// Called by a context when its native window gains OS keyboard focus.
    /// Tracks the focused context and (on Wayland) re-points the clipboard
    /// routing at the focused window's connection.
    /// </summary>
    internal void NotifyContextFocused(WindowContext ctx)
    {
        _focusedContext = ctx;

        // Wayland clipboard/data-device state is per-connection; route the
        // static ClipboardService entry points at the focused window so copy/
        // paste follows keyboard focus across windows.
        if (ctx.DisplayWindow is WaylandWindow wayland)
            wayland.ActivateClipboardRouting();
    }

    /// <summary>
    /// Dialog routing rule: dialogs and context menus are app-modal but must
    /// appear (and take input) in exactly one window. With one window this is
    /// always true — the exact single-window behavior. With several, the host
    /// is latched to the focused window when the dialog appears (else primary)
    /// and released when no dialog remains.
    /// </summary>
    internal bool IsDialogHost(WindowContext ctx)
    {
        if (_windowContexts.Count <= 1)
            return true;

        bool anyDialog = LinuxDialogService.HasActiveDialog || LinuxDialogService.HasContextMenu;
        if (!anyDialog)
        {
            _dialogHostContext = null;
            return true;
        }

        _dialogHostContext ??= FocusedContext ?? PrimaryContext;
        return _dialogHostContext == ctx;
    }

    /// <summary>
    /// Per-frame dialog routing maintenance: latch/release the dialog host and
    /// mirror it onto each engine's RendersDialogs flag so the dialog draws in
    /// exactly one window. Single-window: every engine keeps RendersDialogs
    /// true, matching historical behavior.
    /// </summary>
    private void UpdateDialogRouting()
    {
        bool anyDialog = LinuxDialogService.HasActiveDialog || LinuxDialogService.HasContextMenu;
        if (!anyDialog)
            _dialogHostContext = null;
        else if (_windowContexts.Count > 1)
            _dialogHostContext ??= FocusedContext ?? PrimaryContext;

        for (int i = 0; i < _windowContexts.Count; i++)
        {
            var engine = _windowContexts[i].RenderingEngine;
            if (engine != null)
            {
                engine.RendersDialogs = _windowContexts.Count <= 1
                    || _dialogHostContext == null
                    || _windowContexts[i] == _dialogHostContext;
            }
        }
    }

    /// <summary>
    /// Called by a context when its native window's close button is pressed,
    /// before the window stops. Clears cross-window trackers that reference
    /// the closing tree.
    /// </summary>
    internal void HandleContextCloseRequested(WindowContext ctx)
    {
        if (ctx.IsPrimary)
        {
            // Native drag-and-drop is wired to the primary window; drop any
            // tracked drag target — its view tree is going away.
            var left = _dropTargetTracker.Clear();
            if (left != null)
                Handlers.GestureManager.ProcessDragLeave(left);
        }
    }

    /// <summary>
    /// Removes contexts whose native windows have stopped: notifies MAUI
    /// (IWindow.Destroying), disposes the engine and native window, and drops
    /// them from the registry. When the primary closes while secondaries
    /// live, the next context is promoted to primary (the forwarders follow
    /// automatically) and the process keeps running; the app exits only when
    /// the LAST window closes (the run loop's condition).
    /// </summary>
    internal int ReapClosedContexts()
    {
        int reaped = 0;
        for (int i = _windowContexts.Count - 1; i >= 0; i--)
        {
            var ctx = _windowContexts[i];
            if (ctx.DisplayWindow == null || ctx.DisplayWindow.IsRunning)
                continue;

            _windowContexts.RemoveAt(i);
            if (_focusedContext == ctx) _focusedContext = null;
            if (_dialogHostContext == ctx) _dialogHostContext = null;

            ctx.NotifyDestroying();
            try
            {
                ctx.Dispose();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("LinuxApplication", "Window context dispose failed", ex);
            }
            reaped++;
        }
        return reaped;
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
                InvalidateAllWindows();
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
        var mainWindow = DisplayServerFactory.CreateWindow(
            options.Title ?? "MAUI Application",
            options.Width,
            options.Height,
            options.DisplayServer);

        // SkiaWebView reparents WebKitGTK widgets into the host window using raw X11
        // calls; only valid when the main window actually is X11. On native Wayland
        // the WebView falls back to its own toplevel via GTK.
        if (mainWindow is IX11Surface x11)
        {
            SkiaWebView.SetMainWindow(x11.Display, x11.Handle);
        }

        // Set window icon (X11 _NET_WM_ICON + GTK default icon + .desktop file for GNOME).
        // SetIcon is a no-op on Wayland; the .desktop entry is what GNOME/KDE actually use.
        string? iconPath = ResolveIconPath(options.IconPath);
        if (!string.IsNullOrEmpty(iconPath))
        {
            mainWindow.SetIcon(iconPath);
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

        var renderingEngine = new SkiaRenderingEngine(mainWindow);
        renderingEngine.DpiScale = DpiScale;

        // Register the primary window context. WireInput subscribes all input
        // handlers through Guarded (a view exception unwinding into a native
        // callback aborts the process — hard invariant), plus Resized/Exposed
        // and close/focus tracking. The primary/startup window does not raise
        // MAUI window lifecycle events (Created/Activated) — the historical
        // bootstrap never did — but DOES get Destroying on close.
        var ctx = AttachWindowContext(mainWindow, renderingEngine, raisesMauiLifecycle: false);
        ctx.WireInput();

        // Route native drag-and-drop into MAUI DropGestureRecognizers
        // (additive — DragDropService.Default subscribers are unaffected).
        // Drag-and-drop targets the PRIMARY window's tree only (v1): on X11
        // only the primary window announces XdndAware, and the Wayland drag
        // events carry no window identity through DragDropService.
        WireDragDropRouting();
    }

    private void InitializeGtk(LinuxApplicationOptions options)
    {
        // GTK mode is single-window: one bare context carries the view state
        // (root/focus/hover/capture); rendering goes through GtkHostWindow.
        if (PrimaryContext == null)
            AttachWindowContext(null, null, raisesMauiLifecycle: false);
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
            // Running from an AppImage: ProcessPath points into the ephemeral
            // FUSE mount (/tmp/.mount_*), so a desktop entry written here would
            // go stale the moment the AppImage unmounts — a dead launcher icon.
            // The AppImage runtime sets $APPIMAGE to the real on-disk path, and
            // the AppImage's own first-run installer owns desktop integration;
            // skip ours entirely.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPIMAGE")))
            {
                DiagnosticLog.Debug("LinuxApplication", "Running from AppImage — skipping desktop entry (AppImage installer owns it)");
                return;
            }

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
        MainWindow?.SetTitle(title);
    }
}
