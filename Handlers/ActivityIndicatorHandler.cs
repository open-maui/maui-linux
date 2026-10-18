// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for ActivityIndicator on Linux using Skia rendering.
/// Maps IActivityIndicator interface to SkiaActivityIndicator platform view.
/// </summary>
public partial class ActivityIndicatorHandler : LinuxViewHandler<IActivityIndicator, SkiaActivityIndicator>, IActivityIndicatorHandler
{
    IActivityIndicator IActivityIndicatorHandler.VirtualView => VirtualView;
    object IActivityIndicatorHandler.PlatformView => PlatformView;

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

    /// <summary>MAUI's <c>MapIsRunning</c> signature; maps the property on a Linux <see cref="ActivityIndicatorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapIsRunning(IActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
    {
        if (handler is ActivityIndicatorHandler linuxHandler)
            MapIsRunningCore(linuxHandler, activityIndicator);
    }

    [Obsolete("Use the IActivityIndicatorHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapIsRunning(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator) => MapIsRunningCore(handler, activityIndicator);

    /// <summary>It spins only while running and visible, as MAUI's handlers do on every platform.</summary>
    private static void MapIsRunningCore(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
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

    /// <summary>MAUI's <c>MapColor</c> signature; maps the property on a Linux <see cref="ActivityIndicatorHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapColor(IActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
    {
        if (handler is ActivityIndicatorHandler linuxHandler)
            MapColorCore(linuxHandler, activityIndicator);
    }

    [Obsolete("Use the IActivityIndicatorHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapColor(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator) => MapColorCore(handler, activityIndicator);

    private static void MapColorCore(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
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
