// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for MAUI Application on Linux.
/// Bridges the MAUI Application lifecycle with LinuxApplication.
/// </summary>
public partial class ApplicationHandler : ElementHandler<IApplication, LinuxApplicationContext>
{
    public static IPropertyMapper<IApplication, ApplicationHandler> Mapper =
        new PropertyMapper<IApplication, ApplicationHandler>(ElementHandler.ElementMapper)
        {
        };

    public static CommandMapper<IApplication, ApplicationHandler> CommandMapper =
        new(ElementHandler.ElementCommandMapper)
        {
            [nameof(IApplication.OpenWindow)] = MapOpenWindow,
            [nameof(IApplication.CloseWindow)] = MapCloseWindow,
        };

    public ApplicationHandler() : base(Mapper, CommandMapper)
    {
    }

    public ApplicationHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override LinuxApplicationContext CreatePlatformElement()
    {
        return new LinuxApplicationContext();
    }

    protected override void ConnectHandler(LinuxApplicationContext platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Application = VirtualView;
    }

    protected override void DisconnectHandler(LinuxApplicationContext platformView)
    {
        platformView.Application = null;
        base.DisconnectHandler(platformView);
    }

    public static void MapOpenWindow(ApplicationHandler handler, IApplication application, object? args)
    {
        // Application.OpenWindow(Window) does NOT pass the window itself: it
        // stashes the window under a GUID and invokes this command with an
        // OpenWindowRequest whose persisted state carries the id. Round-trip
        // through IApplication.CreateWindow (with that state as the activation
        // state) to resolve the actual Window instance — the standard MAUI
        // multi-window contract on every platform.
        if (args is Microsoft.Maui.Handlers.OpenWindowRequest request)
        {
            var mauiContext = handler.MauiContext;
            if (mauiContext == null)
            {
                DiagnosticLog.Warn("ApplicationHandler", "OpenWindow: handler has no MauiContext");
                return;
            }

            var state = request.State != null
                ? new ActivationState(mauiContext, request.State)
                : new ActivationState(mauiContext);
            var window = application.CreateWindow(state);
            if (window != null)
            {
                handler.PlatformView?.OpenWindow(window);
            }
        }
        else if (args is IWindow window)
        {
            // Direct-IWindow form kept for programmatic callers.
            handler.PlatformView?.OpenWindow(window);
        }
    }

    public static void MapCloseWindow(ApplicationHandler handler, IApplication application, object? args)
    {
        if (args is IWindow window)
        {
            handler.PlatformView?.CloseWindow(window);
        }
    }
}

/// <summary>
/// Platform context for the MAUI Application on Linux.
/// Manages windows and the application lifecycle.
/// </summary>
public class LinuxApplicationContext
{
    private readonly List<IWindow> _windows = new();
    private IApplication? _application;

    /// <summary>
    /// Gets or sets the MAUI Application.
    /// </summary>
    public IApplication? Application
    {
        get => _application;
        set
        {
            _application = value;
            if (_application != null)
            {
                // Initialize windows from the application
                foreach (var window in _application.Windows)
                {
                    if (!_windows.Contains(window))
                    {
                        _windows.Add(window);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the list of open windows.
    /// </summary>
    public IReadOnlyList<IWindow> Windows => _windows;

    /// <summary>
    /// Opens a window: tracks it and asks LinuxApplication to create/adopt the
    /// native window (multi-window support). The startup window is adopted
    /// into the already-created primary native window; subsequent windows get
    /// a fresh native toplevel, rendered page, and per-window input routing.
    /// </summary>
    public void OpenWindow(IWindow window)
    {
        if (!_windows.Contains(window))
        {
            _windows.Add(window);
        }

        LinuxApplication.Current?.OpenMauiWindow(window);
    }

    /// <summary>
    /// Closes a window: stops its native window (the run loop then raises
    /// IWindow.Destroying and disposes the context; the app exits when the
    /// LAST window closes — closing the primary alone does not).
    /// </summary>
    public void CloseWindow(IWindow window)
    {
        _windows.Remove(window);

        var app = LinuxApplication.Current;
        if (app != null)
        {
            app.CloseMauiWindow(window);
            return;
        }

        if (_windows.Count == 0)
        {
            // No platform app (shouldn't happen in production): preserve the
            // historical stop-on-last behavior.
            LinuxApplication.Current?.MainWindow?.Stop();
        }
    }

    /// <summary>
    /// Gets the main window of the application.
    /// </summary>
    public IWindow? MainWindow => _windows.Count > 0 ? _windows[0] : null;
}
