// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Border on Linux using Skia rendering.
/// </summary>
public partial class BorderHandler : ViewHandler<IBorderView, SkiaBorder>
{
    public static IPropertyMapper<IBorderView, BorderHandler> Mapper =
        new PropertyMapper<IBorderView, BorderHandler>(ViewHandler.ViewMapper)
        {
            [nameof(IBorderView.Content)] = MapContent,
            [nameof(IBorderStroke.Stroke)] = MapStroke,
            [nameof(IBorderStroke.StrokeThickness)] = MapStrokeThickness,
            [nameof(IView.Background)] = MapBackground,
            [nameof(IPadding.Padding)] = MapPadding,
        };

    public static CommandMapper<IBorderView, BorderHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
        };

    public BorderHandler() : base(Mapper, CommandMapper)
    {
    }

    public BorderHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaBorder CreatePlatformView()
    {
        return new SkiaBorder();
    }

    protected override void ConnectHandler(SkiaBorder platformView)
    {
        base.ConnectHandler(platformView);
    }

    protected override void DisconnectHandler(SkiaBorder platformView)
    {
        base.DisconnectHandler(platformView);
    }

    public static void MapContent(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.ClearChildren();

        if (border.PresentedContent?.Handler?.PlatformView is SkiaView skiaContent)
        {
            handler.PlatformView.AddChild(skiaContent);
        }
    }

    public static void MapStroke(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        if (border.Stroke is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.Stroke = solidPaint.Color.ToSKColor();
        }
    }

    public static void MapStrokeThickness(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.StrokeThickness = (float)border.StrokeThickness;
    }

    public static void MapBackground(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        if (border.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }

    public static void MapPadding(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        var padding = border.Padding;
        handler.PlatformView.PaddingLeft = (float)padding.Left;
        handler.PlatformView.PaddingTop = (float)padding.Top;
        handler.PlatformView.PaddingRight = (float)padding.Right;
        handler.PlatformView.PaddingBottom = (float)padding.Bottom;
    }
}
