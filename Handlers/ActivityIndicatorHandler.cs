// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for ActivityIndicator on Linux using Skia rendering.
/// Maps IActivityIndicator interface to SkiaActivityIndicator platform view.
/// </summary>
public partial class ActivityIndicatorHandler : ViewHandler<IActivityIndicator, SkiaActivityIndicator>
{
    public static IPropertyMapper<IActivityIndicator, ActivityIndicatorHandler> Mapper = new PropertyMapper<IActivityIndicator, ActivityIndicatorHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IActivityIndicator.IsRunning)] = MapIsRunning,
        [nameof(IActivityIndicator.Color)] = MapColor,
        [nameof(IView.Background)] = MapBackground,
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

    public static void MapIsRunning(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsRunning = activityIndicator.IsRunning;
    }

    public static void MapColor(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
    {
        if (handler.PlatformView is null) return;

        if (activityIndicator.Color is not null)
            handler.PlatformView.Color = activityIndicator.Color.ToSKColor();
    }

    public static void MapBackground(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
    {
        if (handler.PlatformView is null) return;

        if (activityIndicator.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }
}
