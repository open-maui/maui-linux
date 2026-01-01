// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Shell on Linux using Skia rendering.
/// </summary>
public partial class ShellHandler : ViewHandler<Shell, SkiaShell>
{
    public static IPropertyMapper<Shell, ShellHandler> Mapper = new PropertyMapper<Shell, ShellHandler>(ViewHandler.ViewMapper)
    {
    };

    public static CommandMapper<Shell, ShellHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public ShellHandler() : base(Mapper, CommandMapper)
    {
    }

    public ShellHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaShell CreatePlatformView()
    {
        return new SkiaShell();
    }

    protected override void ConnectHandler(SkiaShell platformView)
    {
        base.ConnectHandler(platformView);
        platformView.FlyoutIsPresentedChanged += OnFlyoutIsPresentedChanged;
        platformView.Navigated += OnNavigated;

        // Subscribe to Shell navigation events
        if (VirtualView != null)
        {
            VirtualView.Navigating += OnShellNavigating;
            VirtualView.Navigated += OnShellNavigated;
        }
    }

    protected override void DisconnectHandler(SkiaShell platformView)
    {
        platformView.FlyoutIsPresentedChanged -= OnFlyoutIsPresentedChanged;
        platformView.Navigated -= OnNavigated;

        if (VirtualView != null)
        {
            VirtualView.Navigating -= OnShellNavigating;
            VirtualView.Navigated -= OnShellNavigated;
        }

        base.DisconnectHandler(platformView);
    }

    private void OnFlyoutIsPresentedChanged(object? sender, EventArgs e)
    {
        // Sync flyout state to virtual view
    }

    private void OnNavigated(object? sender, ShellNavigationEventArgs e)
    {
        // Handle platform navigation events
    }

    private void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        Console.WriteLine($"[ShellHandler] Shell Navigating to: {e.Target?.Location}");

        // Route to platform view
        if (PlatformView != null && e.Target?.Location != null)
        {
            var route = e.Target.Location.ToString().TrimStart('/');
            Console.WriteLine($"[ShellHandler] Routing to: {route}");
            PlatformView.GoToAsync(route);
        }
    }

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        Console.WriteLine($"[ShellHandler] Shell Navigated to: {e.Current?.Location}");
    }
}
