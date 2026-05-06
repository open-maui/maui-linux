// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for SkiaSharp's SKGLView on Linux.
/// Provides a software-rendered fallback for GL-targeting SkiaSharp controls.
/// Controls that use SKGLView for GPU-accelerated rendering will fall back
/// gracefully to CPU rendering through OpenMaui's pipeline.
/// </summary>
public partial class SKGLViewHandler : ViewHandler<SKGLView, SkiaSKGLView>
{
    public static IPropertyMapper<SKGLView, SKGLViewHandler> Mapper =
        new PropertyMapper<SKGLView, SKGLViewHandler>(ViewHandler.ViewMapper)
        {
            [nameof(ISKGLView.IgnorePixelScaling)] = MapIgnorePixelScaling,
            [nameof(ISKGLView.HasRenderLoop)] = MapHasRenderLoop,
            [nameof(ISKGLView.EnableTouchEvents)] = MapEnableTouchEvents,
        };

    public static CommandMapper<SKGLView, SKGLViewHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
            ["InvalidateSurface"] = OnInvalidateSurface,
        };

    public SKGLViewHandler() : base(Mapper, CommandMapper) { }

    public SKGLViewHandler(IPropertyMapper? mapper = null, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper) { }

    protected override SkiaSKGLView CreatePlatformView() => new SkiaSKGLView();

    protected override void ConnectHandler(SkiaSKGLView platformView)
    {
        base.ConnectHandler(platformView);

        if (VirtualView is ISKGLView glView)
        {
            platformView.GLView = glView;
        }

        if (VirtualView is Microsoft.Maui.Controls.VisualElement ve)
        {
            if (ve.WidthRequest >= 0)
                platformView.WidthRequest = ve.WidthRequest;
            if (ve.HeightRequest >= 0)
                platformView.HeightRequest = ve.HeightRequest;
        }
    }

    protected override void DisconnectHandler(SkiaSKGLView platformView)
    {
        platformView.GLView = null;
        base.DisconnectHandler(platformView);
    }

    public static void MapIgnorePixelScaling(SKGLViewHandler handler, SKGLView glView)
    {
        handler.PlatformView?.InvalidateCanvas();
    }

    public static void MapHasRenderLoop(SKGLViewHandler handler, SKGLView glView)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.HasRenderLoop = glView.HasRenderLoop;
        }
    }

    public static void MapEnableTouchEvents(SKGLViewHandler handler, SKGLView glView)
    {
        // Touch events handled through OpenMaui's gesture system
    }

    public static void OnInvalidateSurface(SKGLViewHandler handler, SKGLView glView, object? args)
    {
        handler.PlatformView?.InvalidateCanvas();
    }
}
