// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Hosting;

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
        if (args is IWindow window)
        {
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
    /// Opens a window and creates its handler.
    /// </summary>
    public void OpenWindow(IWindow window)
    {
        if (!_windows.Contains(window))
        {
            _windows.Add(window);
        }
    }

    /// <summary>
    /// Closes a window and cleans up its handler.
    /// </summary>
    public void CloseWindow(IWindow window)
    {
        _windows.Remove(window);

        if (_windows.Count == 0)
        {
            // Last window closed, stop the application
            LinuxApplication.Current?.MainWindow?.Stop();
        }
    }

    /// <summary>
    /// Gets the main window of the application.
    /// </summary>
    public IWindow? MainWindow => _windows.Count > 0 ? _windows[0] : null;
}
