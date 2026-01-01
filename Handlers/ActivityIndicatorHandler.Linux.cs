// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for ActivityIndicator control.
/// </summary>
public class ActivityIndicatorHandler : ViewHandler<IActivityIndicator, SkiaActivityIndicator>
{
    public static IPropertyMapper<IActivityIndicator, ActivityIndicatorHandler> Mapper = new PropertyMapper<IActivityIndicator, ActivityIndicatorHandler>(ViewHandler.ViewMapper)
    {
        ["IsRunning"] = MapIsRunning,
        ["Color"] = MapColor,
        ["Background"] = MapBackground
    };

    public static CommandMapper<IActivityIndicator, ActivityIndicatorHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

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
        if (handler.PlatformView != null)
        {
            handler.PlatformView.IsRunning = activityIndicator.IsRunning;
        }
    }

    public static void MapColor(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
    {
        if (handler.PlatformView != null && activityIndicator.Color != null)
        {
            handler.PlatformView.Color = activityIndicator.Color.ToSKColor();
        }
    }

    public static void MapBackground(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
    {
        if (handler.PlatformView != null)
        {
            if (activityIndicator.Background is SolidPaint solidPaint && solidPaint.Color != null)
            {
                handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
            }
        }
    }
}
