// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
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
            ["StrokeDashArray"] = MapStrokeDashArray,
            ["StrokeDashOffset"] = MapStrokeDashOffset,
            [nameof(IBorderStroke.StrokeLineCap)] = MapStrokeLineCap,
            [nameof(IBorderStroke.StrokeLineJoin)] = MapStrokeLineJoin,
            [nameof(IBorderStroke.StrokeMiterLimit)] = MapStrokeMiterLimit,
            ["StrokeShape"] = MapStrokeShape,  // StrokeShape is on Border, not IBorderStroke
            [nameof(IView.Background)] = MapBackground,
            ["BackgroundColor"] = MapBackgroundColor,
            [nameof(IPadding.Padding)] = MapPadding,
            ["WidthRequest"] = MapWidthRequest,
            ["HeightRequest"] = MapHeightRequest,
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
        if (VirtualView is View view)
        {
            platformView.MauiView = view;
        }
        platformView.Tapped += OnPlatformViewTapped;

        // Explicitly map properties since they may be set before handler creation
        if (VirtualView is VisualElement ve)
        {
            if (ve.BackgroundColor != null)
            {
                platformView.BackgroundColor = ve.BackgroundColor;
            }
            else if (ve.Background is SolidColorBrush brush && brush.Color != null)
            {
                platformView.BackgroundColor = brush.Color;
            }
            if (ve.WidthRequest >= 0)
            {
                platformView.WidthRequest = ve.WidthRequest;
            }
            if (ve.HeightRequest >= 0)
            {
                platformView.HeightRequest = ve.HeightRequest;
            }
        }
    }

    protected override void DisconnectHandler(SkiaBorder platformView)
    {
        platformView.Tapped -= OnPlatformViewTapped;
        platformView.MauiView = null;
        base.DisconnectHandler(platformView);
    }

    private void OnPlatformViewTapped(object? sender, EventArgs e)
    {
        if (VirtualView is View view)
        {
            GestureManager.ProcessTap(view, 0.0, 0.0);
        }
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
                DiagnosticLog.Debug("BorderHandler", $"Creating handler for content: {content.GetType().Name}");
                content.Handler = content.ToViewHandler(handler.MauiContext);
            }

            if (content.Handler?.PlatformView is SkiaView skiaContent)
            {
                DiagnosticLog.Debug("BorderHandler", $"Adding content: {skiaContent.GetType().Name}");
                handler.PlatformView.AddChild(skiaContent);
            }
        }
    }

    public static void MapStroke(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        if (border.Stroke is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.Stroke = solidPaint.Color;
        }
    }

    public static void MapStrokeThickness(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.StrokeThickness = border.StrokeThickness;
    }

    public static void MapBackground(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        if (border.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
    }

    public static void MapBackgroundColor(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        if (border is VisualElement ve)
        {
            var bgColor = ve.BackgroundColor;
            DiagnosticLog.Debug("BorderHandler", $"MapBackgroundColor: {bgColor}");
            if (bgColor != null)
            {
                handler.PlatformView.BackgroundColor = bgColor;
                handler.PlatformView.Invalidate();
            }
        }
    }

    public static void MapPadding(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        var padding = border.Padding;
        handler.PlatformView.PaddingLeft = padding.Left;
        handler.PlatformView.PaddingTop = padding.Top;
        handler.PlatformView.PaddingRight = padding.Right;
        handler.PlatformView.PaddingBottom = padding.Bottom;
    }

    public static void MapStrokeShape(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        // StrokeShape is on the Border control class, not IBorderView interface
        if (border is not Border borderControl) return;

        var shape = borderControl.StrokeShape;

        // Pass the shape directly to the platform view for full shape support
        handler.PlatformView.StrokeShape = shape;

        // Also set CornerRadius for backward compatibility when StrokeShape is RoundRectangle
        if (shape is Microsoft.Maui.Controls.Shapes.RoundRectangle roundRect)
        {
            var cornerRadius = roundRect.CornerRadius;
            handler.PlatformView.CornerRadius = cornerRadius.TopLeft;
        }
        else if (shape is Microsoft.Maui.Controls.Shapes.Rectangle)
        {
            handler.PlatformView.CornerRadius = 0.0;
        }
        else if (shape is Microsoft.Maui.Controls.Shapes.Ellipse)
        {
            handler.PlatformView.CornerRadius = double.MaxValue;
        }

        handler.PlatformView.Invalidate();
    }

    public static void MapStrokeDashArray(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        // StrokeDashArray is on Border class
        if (border is Border borderControl && borderControl.StrokeDashArray != null)
        {
            var dashArray = new DoubleCollection();
            foreach (var value in borderControl.StrokeDashArray)
            {
                dashArray.Add(value);
            }
            handler.PlatformView.StrokeDashArray = dashArray;
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapStrokeDashOffset(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        // StrokeDashOffset is on Border class
        if (border is Border borderControl)
        {
            handler.PlatformView.StrokeDashOffset = borderControl.StrokeDashOffset;
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapStrokeLineCap(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        if (border is IBorderStroke borderStroke)
        {
            handler.PlatformView.StrokeLineCap = borderStroke.StrokeLineCap;
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapStrokeLineJoin(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        if (border is IBorderStroke borderStroke)
        {
            handler.PlatformView.StrokeLineJoin = borderStroke.StrokeLineJoin;
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapStrokeMiterLimit(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        if (border is IBorderStroke borderStroke)
        {
            handler.PlatformView.StrokeMiterLimit = borderStroke.StrokeMiterLimit;
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapWidthRequest(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        if (border is VisualElement ve && ve.WidthRequest >= 0)
        {
            handler.PlatformView.WidthRequest = ve.WidthRequest;
            handler.PlatformView.InvalidateMeasure();
        }
    }

    public static void MapHeightRequest(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        if (border is VisualElement ve && ve.HeightRequest >= 0)
        {
            handler.PlatformView.HeightRequest = ve.HeightRequest;
            handler.PlatformView.InvalidateMeasure();
        }
    }
}
