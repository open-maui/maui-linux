// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for SkiaSharp's SKCanvasView on Linux.
/// Provides the native rendering host by routing PaintSurface through OpenMaui's
/// SkiaSharp rendering pipeline. This enables all SkiaSharp-based MAUI controls
/// (LiveCharts, Microcharts, custom SKCanvasView drawings, etc.) to work on Linux.
/// </summary>
public partial class SKCanvasViewHandler : ViewHandler<SKCanvasView, SkiaSKCanvasView>
{
    public static IPropertyMapper<SKCanvasView, SKCanvasViewHandler> Mapper =
        new PropertyMapper<SKCanvasView, SKCanvasViewHandler>(ViewHandler.ViewMapper)
        {
            [nameof(ISKCanvasView.IgnorePixelScaling)] = MapIgnorePixelScaling,
            [nameof(ISKCanvasView.EnableTouchEvents)] = MapEnableTouchEvents,
        };

    public static CommandMapper<SKCanvasView, SKCanvasViewHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
            ["InvalidateSurface"] = OnInvalidateSurface,
        };

    public SKCanvasViewHandler() : base(Mapper, CommandMapper) { }

    public SKCanvasViewHandler(IPropertyMapper? mapper = null, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper) { }

    protected override SkiaSKCanvasView CreatePlatformView() => new SkiaSKCanvasView();

    protected override void ConnectHandler(SkiaSKCanvasView platformView)
    {
        base.ConnectHandler(platformView);

        if (VirtualView is ISKCanvasView canvasView)
        {
            platformView.CanvasView = canvasView;
        }
        else
        {
            DiagnosticLog.Error("SKCanvasViewHandler", $"VirtualView {VirtualView?.GetType().Name} does not implement ISKCanvasView");
        }

        // Sync size requests
        if (VirtualView is Microsoft.Maui.Controls.VisualElement ve)
        {
            if (ve.WidthRequest >= 0)
                platformView.WidthRequest = ve.WidthRequest;
            if (ve.HeightRequest >= 0)
                platformView.HeightRequest = ve.HeightRequest;
        }
    }

    protected override void DisconnectHandler(SkiaSKCanvasView platformView)
    {
        platformView.CanvasView = null;
        base.DisconnectHandler(platformView);
    }

    public static void MapIgnorePixelScaling(SKCanvasViewHandler handler, SKCanvasView canvasView)
    {
        // On Linux we always use 1:1 pixel mapping, no scaling needed
        handler.PlatformView?.InvalidateCanvas();
    }

    public static void MapEnableTouchEvents(SKCanvasViewHandler handler, SKCanvasView canvasView)
    {
        // Touch events are handled through OpenMaui's gesture system
    }

    public static void OnInvalidateSurface(SKCanvasViewHandler handler, SKCanvasView canvasView, object? args)
    {
        handler.PlatformView?.InvalidateCanvas();
    }
}
