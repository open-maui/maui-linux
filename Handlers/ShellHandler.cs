// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Shell on Linux using Skia rendering.
/// </summary>
public partial class ShellHandler : ViewHandler<IView, SkiaShell>
{
    public static IPropertyMapper<IView, ShellHandler> Mapper = new PropertyMapper<IView, ShellHandler>(ViewHandler.ViewMapper)
    {
    };

    public static CommandMapper<IView, ShellHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
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
    }

    protected override void DisconnectHandler(SkiaShell platformView)
    {
        platformView.FlyoutIsPresentedChanged -= OnFlyoutIsPresentedChanged;
        platformView.Navigated -= OnNavigated;
        base.DisconnectHandler(platformView);
    }

    private void OnFlyoutIsPresentedChanged(object? sender, EventArgs e)
    {
        // Sync flyout state to virtual view
    }

    private void OnNavigated(object? sender, ShellNavigationEventArgs e)
    {
        // Handle navigation events
    }
}
