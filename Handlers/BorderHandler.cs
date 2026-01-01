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
            ["StrokeShape"] = MapStrokeShape,  // StrokeShape is on Border, not IBorderStroke
            [nameof(IView.Background)] = MapBackground,
            ["BackgroundColor"] = MapBackgroundColor,
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
        if (handler.PlatformView is null || handler.MauiContext is null) return;

        handler.PlatformView.ClearChildren();

        var content = border.PresentedContent;
        if (content != null)
        {
            // Create handler for content if it doesn't exist
            if (content.Handler == null)
            {
                Console.WriteLine($"[BorderHandler] Creating handler for content: {content.GetType().Name}");
                content.Handler = content.ToHandler(handler.MauiContext);
            }

            if (content.Handler?.PlatformView is SkiaView skiaContent)
            {
                Console.WriteLine($"[BorderHandler] Adding content: {skiaContent.GetType().Name}");
                handler.PlatformView.AddChild(skiaContent);
            }
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

    public static void MapBackgroundColor(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        if (border is VisualElement ve && ve.BackgroundColor != null)
        {
            handler.PlatformView.BackgroundColor = ve.BackgroundColor.ToSKColor();
            handler.PlatformView.Invalidate();
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

    public static void MapStrokeShape(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        // StrokeShape is on the Border control class, not IBorderView interface
        if (border is not Border borderControl) return;

        var shape = borderControl.StrokeShape;
        if (shape is Microsoft.Maui.Controls.Shapes.RoundRectangle roundRect)
        {
            // RoundRectangle can have different corner radii, but we use a uniform one
            // Take the top-left corner as the uniform radius
            var cornerRadius = roundRect.CornerRadius;
            handler.PlatformView.CornerRadius = (float)cornerRadius.TopLeft;
        }
        else if (shape is Microsoft.Maui.Controls.Shapes.Rectangle)
        {
            handler.PlatformView.CornerRadius = 0;
        }
        else if (shape is Microsoft.Maui.Controls.Shapes.Ellipse)
        {
            // For ellipse, use half the min dimension as corner radius
            // This will be applied during rendering when bounds are known
            handler.PlatformView.CornerRadius = float.MaxValue; // Marker for "fully rounded"
        }

        handler.PlatformView.Invalidate();
    }
}
