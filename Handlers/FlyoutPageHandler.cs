// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for FlyoutPage on Linux using Skia rendering.
/// Maps IFlyoutView interface to SkiaFlyoutPage platform view.
/// </summary>
public partial class FlyoutPageHandler : ViewHandler<IFlyoutView, SkiaFlyoutPage>
{
    public static IPropertyMapper<IFlyoutView, FlyoutPageHandler> Mapper = new PropertyMapper<IFlyoutView, FlyoutPageHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IFlyoutView.IsPresented)] = MapIsPresented,
        [nameof(IFlyoutView.FlyoutWidth)] = MapFlyoutWidth,
        [nameof(IFlyoutView.IsGestureEnabled)] = MapIsGestureEnabled,
        [nameof(IFlyoutView.FlyoutBehavior)] = MapFlyoutBehavior,
    };

    public static CommandMapper<IFlyoutView, FlyoutPageHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public FlyoutPageHandler() : base(Mapper, CommandMapper)
    {
    }

    public FlyoutPageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaFlyoutPage CreatePlatformView()
    {
        return new SkiaFlyoutPage();
    }

    protected override void ConnectHandler(SkiaFlyoutPage platformView)
    {
        base.ConnectHandler(platformView);
        platformView.IsPresentedChanged += OnIsPresentedChanged;
    }

    protected override void DisconnectHandler(SkiaFlyoutPage platformView)
    {
        platformView.IsPresentedChanged -= OnIsPresentedChanged;
        platformView.Flyout = null;
        platformView.Detail = null;
        base.DisconnectHandler(platformView);
    }

    private void OnIsPresentedChanged(object? sender, EventArgs e)
    {
        // Sync back to the virtual view
    }

    public static void MapIsPresented(FlyoutPageHandler handler, IFlyoutView flyoutView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsPresented = flyoutView.IsPresented;
    }

    public static void MapFlyoutWidth(FlyoutPageHandler handler, IFlyoutView flyoutView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.FlyoutWidth = (float)flyoutView.FlyoutWidth;
    }

    public static void MapIsGestureEnabled(FlyoutPageHandler handler, IFlyoutView flyoutView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.GestureEnabled = flyoutView.IsGestureEnabled;
    }

    public static void MapFlyoutBehavior(FlyoutPageHandler handler, IFlyoutView flyoutView)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.FlyoutLayoutBehavior = flyoutView.FlyoutBehavior switch
        {
            Microsoft.Maui.FlyoutBehavior.Disabled => FlyoutLayoutBehavior.Default,
            Microsoft.Maui.FlyoutBehavior.Flyout => FlyoutLayoutBehavior.Popover,
            Microsoft.Maui.FlyoutBehavior.Locked => FlyoutLayoutBehavior.Split,
            _ => FlyoutLayoutBehavior.Default
        };
    }
}
