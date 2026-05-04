// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for BoxView on Linux.
/// </summary>
public partial class BoxViewHandler : ViewHandler<BoxView, SkiaBoxView>
{
    public static IPropertyMapper<BoxView, BoxViewHandler> Mapper =
        new PropertyMapper<BoxView, BoxViewHandler>(ViewMapper)
        {
            [nameof(BoxView.Color)] = MapColor,
            [nameof(BoxView.CornerRadius)] = MapCornerRadius,
            [nameof(IView.Background)] = MapBackground,
            ["BackgroundColor"] = MapBackgroundColor,
        };

    public BoxViewHandler() : base(Mapper)
    {
    }

    protected override SkiaBoxView CreatePlatformView()
    {
        return new SkiaBoxView();
    }

    protected override void ConnectHandler(SkiaBoxView platformView)
    {
        base.ConnectHandler(platformView);

        // Map size requests from MAUI BoxView
        if (VirtualView is BoxView boxView)
        {
            if (boxView.WidthRequest >= 0)
                platformView.WidthRequest = boxView.WidthRequest;
            if (boxView.HeightRequest >= 0)
                platformView.HeightRequest = boxView.HeightRequest;
        }
    }

    public static void MapColor(BoxViewHandler handler, BoxView boxView)
    {
        if (boxView.Color != null)
        {
            handler.PlatformView.Color = boxView.Color;
        }
    }

    public static void MapCornerRadius(BoxViewHandler handler, BoxView boxView)
    {
        handler.PlatformView.CornerRadius = boxView.CornerRadius;
    }

    public static void MapBackground(BoxViewHandler handler, BoxView boxView)
    {
        if (boxView.Background is SolidColorBrush solidBrush && solidBrush.Color != null)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color;
            handler.PlatformView.Invalidate();
        }
    }

    public static void MapBackgroundColor(BoxViewHandler handler, BoxView boxView)
    {
        if (boxView.BackgroundColor != null)
        {
            handler.PlatformView.BackgroundColor = boxView.BackgroundColor;
            handler.PlatformView.Invalidate();
        }
    }
}
