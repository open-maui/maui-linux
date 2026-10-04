// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Window on Linux.
/// Maps IWindow to the Linux display window system.
/// </summary>
public partial class WindowHandler : ElementHandler<IWindow, SkiaWindow>, IWindowHandler
{
    // MAUI's window handler interface (code that looks for a window's handler casts to it).
    IWindow IWindowHandler.VirtualView => VirtualView;
    object IWindowHandler.PlatformView => PlatformView;

    public static IPropertyMapper<IWindow, WindowHandler> Mapper =
        new PropertyMapper<IWindow, WindowHandler>(ElementHandler.ElementMapper)
        {
            [nameof(IWindow.Title)] = MapTitle,
            [nameof(IWindow.Content)] = MapContent,
            [nameof(IWindow.X)] = MapX,
            [nameof(IWindow.Y)] = MapY,
            [nameof(IWindow.Width)] = MapWidth,
            [nameof(IWindow.Height)] = MapHeight,
            [nameof(IWindow.MinimumWidth)] = MapMinimumWidth,
            [nameof(IWindow.MinimumHeight)] = MapMinimumHeight,
            [nameof(IWindow.MaximumWidth)] = MapMaximumWidth,
            [nameof(IWindow.MaximumHeight)] = MapMaximumHeight,
            [nameof(IToolbarElement.Toolbar)] = MapToolbar,
            // MAUI 9+ Window.TitleBar (IWindow.TitleBar is in the Windows and Mac Catalyst builds only).
            [nameof(Microsoft.Maui.Controls.Window.TitleBar)] = MapTitleBar,
            ["TitleBarDragRectangles"] = MapTitleBarDragRectangles,
        };

    public static CommandMapper<IWindow, WindowHandler> CommandMapper =
        new(ElementHandler.ElementCommandMapper)
        {
            [nameof(IWindow.RequestDisplayDensity)] = MapRequestDisplayDensity,
        };

    public WindowHandler() : base(Mapper, CommandMapper)
    {
    }

    public WindowHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaWindow CreatePlatformElement()
    {
        return new SkiaWindow();
    }

    protected override void ConnectHandler(SkiaWindow platformView)
    {
        base.ConnectHandler(platformView);
        platformView.CloseRequested += OnCloseRequested;
        platformView.SizeChanged += OnSizeChanged;
    }

    protected override void DisconnectHandler(SkiaWindow platformView)
    {
        platformView.CloseRequested -= OnCloseRequested;
        platformView.SizeChanged -= OnSizeChanged;
        base.DisconnectHandler(platformView);
    }

    // True while a property mapper is pushing a value from the MAUI Window
    // into SkiaWindow. SkiaWindow raises SizeChanged for every Width/Height
    // set; echoing that back through IWindow.FrameChanged would overwrite the
    // window's (possibly unset, NaN) size with the wrapper's clamped default.
    // Real native sizes reach MAUI through WindowContext.OnWindowResized.
    private bool _syncingFromVirtualView;

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        VirtualView?.Destroying();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_syncingFromVirtualView) return;
        VirtualView?.FrameChanged(new Rect(0, 0, e.Width, e.Height));
    }

    /// <summary>
    /// MAUI window dimensions are logical doubles that are NaN when unset
    /// (and +Infinity for the maximum defaults); a raw <c>(int)</c> cast of
    /// those yields int.MinValue, which then trips SkiaWindow's clamps.
    /// Returns <paramref name="fallback"/> for any non-finite value.
    /// </summary>
    private static int ToPixels(double value, int fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return fallback;
        return (int)Math.Round(value);
    }

    public static void MapTitle(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Title = window.Title ?? "MAUI Application";
    }

    public static void MapContent(WindowHandler handler, IWindow window)
    {
        DiagnosticLog.Debug("WindowHandler", $"MapContent - PlatformView={handler.PlatformView != null}");
        if (handler.PlatformView is null) return;

        var content = window.Content;
        DiagnosticLog.Debug("WindowHandler", $"MapContent - content type={content?.GetType().Name}, handler={content?.Handler?.GetType().Name}");
        // A new root page brings its own toolbar (a FlyoutPage's or the Shell's) or none.
        handler.UpdateToolbar();
        // The visual diagnostics overlay is ready once the window has content, as MAUI's
        // Windows handler initializes it in MapContent.
        window.VisualDiagnosticsOverlay?.Initialize();
        if (content?.Handler?.PlatformView is SkiaView skiaContent)
        {
            DiagnosticLog.Debug("WindowHandler", $"MapContent - setting SkiaView content: {skiaContent.GetType().Name}");
            handler.PlatformView.Content = skiaContent;
        }
        else if (content?.Handler != null)
        {
            DiagnosticLog.Warn("WindowHandler", $"MapContent - content has no SkiaView! Handler={content.Handler}, PlatformView={content.Handler.PlatformView}");
        }
        else
        {
            // The window handler is attached before the page is rendered
            // (WindowContext adopts the MAUI window first so alerts can
            // subscribe); the page's SkiaView arrives when it gets its handler.
            DiagnosticLog.Debug("WindowHandler", "MapContent - content not rendered yet");
        }
    }

    /// <summary>
    /// The window's toolbar: the one a NavigationPage put on the window, else the one its root
    /// page carries (a FlyoutPage's or the Shell's), realized as on MAUI's platforms, where the
    /// window's navigation root holds the platform toolbar (NavigationRootManager.SetToolbar).
    /// </summary>
    public static void MapToolbar(WindowHandler handler, IWindow window) => handler.UpdateToolbar();

    /// <summary>
    /// The window's TitleBar (MAUI 9+ <c>Window.TitleBar</c>): realized and shown in a strip
    /// above the page, as Windows puts it in the window's title bar. With client-side
    /// decorations it fills the decoration's title area (the window buttons stay on top), and
    /// its leading, main and trailing content take presses while the rest of it moves the
    /// window; otherwise the strip is the top of the client area. A hidden TitleBar
    /// (IsVisible false) takes no space, as Windows collapses it.
    /// </summary>
    public static void MapTitleBar(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is not { } platform)
            return;
        SkiaView? view = null;
        if ((window as Microsoft.Maui.Controls.Window)?.TitleBar is { } titleBar && handler.MauiContext is { } context)
        {
            try
            {
                view = titleBar.Handler?.PlatformView as SkiaView
                    ?? MauiHandlerExtensions.ToHandler(titleBar, context)?.PlatformView as SkiaView;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("WindowHandler", $"Realizing the window's TitleBar {titleBar.GetType().Name} failed", ex);
            }
        }
        if (ReferenceEquals(platform.TitleBar, view))
            return;
        platform.TitleBar = view;
        WindowOverlayPatches.RequestRedraw(window);
    }

    /// <summary>
    /// Windows hands the title bar's drag rectangles to the system. Here the decoration asks
    /// the TitleBar's passthrough elements at each press (WindowContext.IsTitleBarPassthrough),
    /// so the window only redraws.
    /// </summary>
    public static void MapTitleBarDragRectangles(WindowHandler handler, IWindow window) =>
        WindowOverlayPatches.RequestRedraw(window);

    /// <summary>Points <see cref="SkiaWindow.Toolbar"/> at the toolbar the window shows now.</summary>
    internal void UpdateToolbar()
    {
        if (PlatformView is not { } platform || VirtualView is not { } window || MauiContext is not { } context)
            return;
        var toolbar = (window as IToolbarElement)?.Toolbar ?? (window.Content as IToolbarElement)?.Toolbar;
        platform.Toolbar = toolbar != null ? ToolbarHandler.Realize(toolbar, context) : null;
    }

    /// <summary>
    /// Re-resolves the toolbar of the window <paramref name="page"/> is in, after the page's
    /// own toolbar changed (a FlyoutPage or Shell at the window's root carries it).
    /// </summary>
    internal static void UpdateToolbar(IElement? page)
    {
        ((page as VisualElement)?.Window?.Handler as WindowHandler)?.UpdateToolbar();
    }

    /// <summary>
    /// The window's display density (Window.DisplayDensity, MAUI's RequestDisplayDensity
    /// command): the scale of the monitor the window is on, the app's when the window is not
    /// shown (yet), 1 without a display.
    /// </summary>
    public static void MapRequestDisplayDensity(IWindowHandler handler, IWindow window, object? args)
    {
        if (args is DisplayDensityRequest request)
            request.SetResult(GetDisplayDensity(window));
    }

    internal static float GetDisplayDensity(IWindow? window)
    {
        var app = LinuxApplication.Current;
        if (app == null)
            return 1f;
        if (window != null)
        {
            foreach (var context in app.WindowContexts)
            {
                if (ReferenceEquals(context.MauiWindow, window))
                    return context.Scale > 0f ? context.Scale : 1f;
            }
        }
        return app.DpiScale > 0f ? app.DpiScale : 1f;
    }

    public static void MapX(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.X = ToPixels(window.X, handler.PlatformView.X);
    }

    public static void MapY(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Y = ToPixels(window.Y, handler.PlatformView.Y);
    }

    public static void MapWidth(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler._syncingFromVirtualView = true;
        try { handler.PlatformView.Width = ToPixels(window.Width, handler.PlatformView.Width); }
        finally { handler._syncingFromVirtualView = false; }
    }

    public static void MapHeight(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler._syncingFromVirtualView = true;
        try { handler.PlatformView.Height = ToPixels(window.Height, handler.PlatformView.Height); }
        finally { handler._syncingFromVirtualView = false; }
    }

    public static void MapMinimumWidth(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MinWidth = ToPixels(window.MinimumWidth, handler.PlatformView.MinWidth);
    }

    public static void MapMinimumHeight(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MinHeight = ToPixels(window.MinimumHeight, handler.PlatformView.MinHeight);
    }

    public static void MapMaximumWidth(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MaxWidth = ToPixels(window.MaximumWidth, handler.PlatformView.MaxWidth);
    }

    public static void MapMaximumHeight(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MaxHeight = ToPixels(window.MaximumHeight, handler.PlatformView.MaxHeight);
    }
}

/// <summary>
/// Skia window wrapper for Linux display servers.
/// Handles rendering of content and popup overlays automatically.
/// </summary>
public class SkiaWindow
{
    private SkiaView? _content;
    private string _title = "MAUI Application";
    private int _x, _y;
    private int _width = 800;
    private int _height = 600;
    private int _minWidth = 100;
    private int _minHeight = 100;
    private int _maxWidth = int.MaxValue;
    private int _maxHeight = int.MaxValue;

    /// <summary>
    /// The toolbar the window shows (the platform element of MAUI's Window.Toolbar, or of its
    /// root page's), null when it shows none.
    /// </summary>
    public SkiaToolbar? Toolbar { get; set; }

    /// <summary>
    /// The platform view of the window's TitleBar (MAUI's <c>Window.TitleBar</c>), shown in a
    /// strip above the page; null when the window has none.
    /// </summary>
    public SkiaView? TitleBar { get; set; }

    public SkiaView? Content
    {
        get => _content;
        set
        {
            _content = value;
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Renders the window content and popup overlays to the canvas.
    /// This should be called by the platform rendering loop.
    /// </summary>
    public void Render(SKCanvas canvas)
    {
        // Clear background
        canvas.Clear(SKColors.White);

        // Draw main content
        if (_content != null)
        {
            _content.Measure(new Size(_width, _height));
            _content.Arrange(new Rect(0, 0, _width, _height));
            _content.Draw(canvas);
        }

        // Draw popup overlays on top (dropdowns, date pickers, etc.)
        // This ensures popups always render above all other content
        SkiaView.DrawPopupOverlays(canvas);
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            TitleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int X
    {
        get => _x;
        set { _x = value; PositionChanged?.Invoke(this, EventArgs.Empty); }
    }

    public int Y
    {
        get => _y;
        set { _y = value; PositionChanged?.Invoke(this, EventArgs.Empty); }
    }

    public int Width
    {
        get => _width;
        set
        {
            _width = Math.Clamp(value, _minWidth, _maxWidth);
            SizeChanged?.Invoke(this, new SizeChangedEventArgs(_width, _height));
        }
    }

    public int Height
    {
        get => _height;
        set
        {
            _height = Math.Clamp(value, _minHeight, _maxHeight);
            SizeChanged?.Invoke(this, new SizeChangedEventArgs(_width, _height));
        }
    }

    public int MinWidth
    {
        get => _minWidth;
        set { _minWidth = value; }
    }

    public int MinHeight
    {
        get => _minHeight;
        set { _minHeight = value; }
    }

    public int MaxWidth
    {
        get => _maxWidth;
        set { _maxWidth = value; }
    }

    public int MaxHeight
    {
        get => _maxHeight;
        set { _maxHeight = value; }
    }

    public event EventHandler? ContentChanged;
    public event EventHandler? TitleChanged;
    public event EventHandler? PositionChanged;
    public event EventHandler<SizeChangedEventArgs>? SizeChanged;
    public event EventHandler? CloseRequested;

    public void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Event args for window size changes.
/// </summary>
public class SizeChangedEventArgs : EventArgs
{
    public int Width { get; }
    public int Height { get; }

    public SizeChangedEventArgs(int width, int height)
    {
        Width = width;
        Height = height;
    }
}
