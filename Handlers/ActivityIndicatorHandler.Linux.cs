// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Linux handler for ActivityIndicator control.
/// </summary>
public partial class ActivityIndicatorHandler : ViewHandler<IActivityIndicator, SkiaActivityIndicator>
{
    public static IPropertyMapper<IActivityIndicator, ActivityIndicatorHandler> Mapper = new PropertyMapper<IActivityIndicator, ActivityIndicatorHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IActivityIndicator.IsRunning)] = MapIsRunning,
        [nameof(IActivityIndicator.Color)] = MapColor,
        [nameof(IView.IsEnabled)] = MapIsEnabled,
    };

    public static CommandMapper<IActivityIndicator, ActivityIndicatorHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

    public ActivityIndicatorHandler() : base(Mapper, CommandMapper) { }

    protected override SkiaActivityIndicator CreatePlatformView() => new SkiaActivityIndicator();

    public static void MapIsRunning(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
    {
        handler.PlatformView.IsRunning = activityIndicator.IsRunning;
    }

    public static void MapColor(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
    {
        if (activityIndicator.Color != null)
            handler.PlatformView.Color = activityIndicator.Color.ToSKColor();
        handler.PlatformView.Invalidate();
    }

    public static void MapIsEnabled(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
    {
        handler.PlatformView.IsEnabled = activityIndicator.IsEnabled;
        handler.PlatformView.Invalidate();
    }
}
