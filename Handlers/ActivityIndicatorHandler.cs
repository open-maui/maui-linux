// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for ActivityIndicator on Linux using Skia rendering.
/// Maps IActivityIndicator interface to SkiaActivityIndicator platform view.
/// </summary>
public partial class ActivityIndicatorHandler : LinuxViewHandler<IActivityIndicator, SkiaActivityIndicator>
{
    public static IPropertyMapper<IActivityIndicator, ActivityIndicatorHandler> Mapper = new PropertyMapper<IActivityIndicator, ActivityIndicatorHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IActivityIndicator.IsRunning)] = MapIsRunning,
        [nameof(IView.Visibility)] = MapVisibility,
        [nameof(IActivityIndicator.Color)] = MapColor,
        [nameof(IView.Background)] = MapBackground,
        [nameof(IView.IsEnabled)] = MapIsEnabled,
    };

    public static CommandMapper<IActivityIndicator, ActivityIndicatorHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public ActivityIndicatorHandler() : base(Mapper, CommandMapper)
    {
    }

    public ActivityIndicatorHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaActivityIndicator CreatePlatformView()
    {
        return new SkiaActivityIndicator();
    }

    protected override void ConnectHandler(SkiaActivityIndicator platformView)
    {
        base.ConnectHandler(platformView);

        // Sync properties
        if (VirtualView != null)
        {
            MapIsRunning(this, VirtualView);
            MapColor(this, VirtualView);
            MapIsEnabled(this, VirtualView);
        }
    }

    /// <summary>It spins only while running and visible, as MAUI's handlers do on every platform.</summary>
    public static void MapIsRunning(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsRunning = activityIndicator.IsRunning && activityIndicator.Visibility == Visibility.Visible;
    }

    /// <summary>The base visibility mapping, then the spin, which depends on it.</summary>
    public static void MapVisibility(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
    {
        LinuxViewMappers.MapVisibility(handler, activityIndicator);
        MapIsRunning(handler, activityIndicator);
    }

    public static void MapColor(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
    {
        if (handler.PlatformView is null) return;

        if (activityIndicator.Color is not null)
            handler.PlatformView.Color = activityIndicator.Color;
    }

    public static void MapBackground(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
    {
        if (handler.PlatformView is null) return;

        if (activityIndicator.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
    }

    public static void MapIsEnabled(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsEnabled = activityIndicator.IsEnabled;
        handler.PlatformView.Invalidate();
    }
}
