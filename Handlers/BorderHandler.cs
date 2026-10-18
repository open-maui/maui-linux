// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Border on Linux using Skia rendering.
/// </summary>
public partial class BorderHandler : LinuxViewHandler<IBorderView, SkiaBorder>, IBorderHandler
{
    IBorderView IBorderHandler.VirtualView => VirtualView;
    object IBorderHandler.PlatformView => PlatformView;

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
            [nameof(IBorderStroke.Shape)] = MapShape,
            [nameof(IBorderStroke.StrokeDashPattern)] = MapStrokeDashPattern,
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
        VisualStateBridge.Attach(VirtualView, platformView);
        if (VirtualView is View view)
        {
            platformView.MauiView = view;
        }
        // A Border with a primary single-tap recognizer is a tab stop that Enter and Space
        // activate (MAUI 11's GesturePlatformManager on Windows).
        platformView.AttachTapStop(VirtualView as View);

        // Explicitly map size requests since they may be set before handler creation.
        // Background/BackgroundColor are deliberately NOT echoed here: when MauiView
        // is wired (above) the SkiaBorder reads them live and OnMauiViewPropertyChanged
        // refreshes the cached SKColor on changes. Echoing through the public setter
        // would route to ve.SetValue() at LocalValue specificity and permanently
        // override any AppThemeBinding on the Border's BackgroundColor.
        if (VirtualView is VisualElement ve)
        {
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
        platformView.AttachTapStop(null);
        platformView.MauiView = null;
        VisualStateBridge.Detach(platformView);
        base.DisconnectHandler(platformView);
    }

    /// <summary>MAUI's <c>MapContent</c> signature; maps the property on a Linux <see cref="BorderHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapContent(IBorderHandler handler, IBorderView border)
    {
        if (handler is BorderHandler linuxHandler)
            MapContentCore(linuxHandler, border);
    }

    [Obsolete("Use the IBorderHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapContent(BorderHandler handler, IBorderView border) => MapContentCore(handler, border);

    private static void MapContentCore(BorderHandler handler, IBorderView border)
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

    /// <summary>MAUI's <c>MapStroke</c> signature; maps the property on a Linux <see cref="BorderHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapStroke(IBorderHandler handler, IBorderView border)
    {
        if (handler is BorderHandler linuxHandler)
            MapStrokeCore(linuxHandler, border);
    }

    [Obsolete("Use the IBorderHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapStroke(BorderHandler handler, IBorderView border) => MapStrokeCore(handler, border);

    private static void MapStrokeCore(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        if (border.Stroke is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.Stroke = solidPaint.Color;
        }
    }

    /// <summary>MAUI's <c>MapStrokeThickness</c> signature; maps the property on a Linux <see cref="BorderHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapStrokeThickness(IBorderHandler handler, IBorderView border)
    {
        if (handler is BorderHandler linuxHandler)
            MapStrokeThicknessCore(linuxHandler, border);
    }

    [Obsolete("Use the IBorderHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapStrokeThickness(BorderHandler handler, IBorderView border) => MapStrokeThicknessCore(handler, border);

    private static void MapStrokeThicknessCore(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.StrokeThickness = border.StrokeThickness;
    }

    /// <summary>MAUI's <c>MapBackground</c> signature; maps the property on a Linux <see cref="BorderHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapBackground(IBorderHandler handler, IBorderView border)
    {
        if (handler is BorderHandler linuxHandler)
            MapBackgroundCore(linuxHandler, border);
    }

    [Obsolete("Use the IBorderHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapBackground(BorderHandler handler, IBorderView border) => MapBackgroundCore(handler, border);

    private static void MapBackgroundCore(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        // When MauiView is wired the SkiaBorder reads Background/BackgroundColor
        // live and OnMauiViewPropertyChanged refreshes the cache on bind updates.
        // Echoing back via the public setter would route to ve.SetValue() at
        // LocalValue specificity and clobber any AppThemeBinding on the property.
        if (handler.PlatformView.MauiView != null)
        {
            handler.PlatformView.Invalidate();
            return;
        }

        if (border.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
    }

    public static void MapBackgroundColor(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        // See note in MapBackground — never echo BackgroundColor back when MauiView
        // is wired; doing so destroys AppThemeBindings on the Border's BackgroundColor.
        if (handler.PlatformView.MauiView != null)
        {
            handler.PlatformView.Invalidate();
            return;
        }

        if (border is VisualElement ve)
        {
            var bgColor = ve.BackgroundColor;
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

    /// <summary>
    /// MAUI's BorderHandler.MapShape: the border's <see cref="IBorderStroke.Shape"/> (a
    /// Controls Border's StrokeShape, or any IBorderView's shape).
    /// </summary>
    public static void MapShape(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;
        ApplyShape(handler, border.Shape);
    }

    /// <summary>MAUI's <c>MapStrokeDashPattern</c> signature; maps the property on a Linux <see cref="BorderHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapStrokeDashPattern(IBorderHandler handler, IBorderView border)
    {
        if (handler is BorderHandler linuxHandler)
            MapStrokeDashPatternCore(linuxHandler, border);
    }

    [Obsolete("Use the IBorderHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapStrokeDashPattern(BorderHandler handler, IBorderView border) => MapStrokeDashPatternCore(handler, border);

    /// <summary>
    /// MAUI's BorderHandler.MapStrokeDashPattern: the dash pattern of the stroke (for a
    /// Controls Border, its StrokeDashArray).
    /// </summary>
    private static void MapStrokeDashPatternCore(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        if (border is Border)
        {
            MapStrokeDashArray(handler, border);
            return;
        }

        var pattern = border.StrokeDashPattern;
        DoubleCollection? dashArray = null;
        if (pattern is { Length: > 0 })
        {
            dashArray = new DoubleCollection();
            foreach (var value in pattern)
                dashArray.Add(value);
        }
        handler.PlatformView.StrokeDashArray = dashArray;
        handler.PlatformView.Invalidate();
    }

    /// <summary>MAUI's <c>MapStrokeShape</c> signature; maps the property on a Linux <see cref="BorderHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapStrokeShape(IBorderHandler handler, IBorderView border)
    {
        if (handler is BorderHandler linuxHandler)
            MapStrokeShapeCore(linuxHandler, border);
    }

    [Obsolete("Use the IBorderHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapStrokeShape(BorderHandler handler, IBorderView border) => MapStrokeShapeCore(handler, border);

    private static void MapStrokeShapeCore(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        // StrokeShape is on the Border control class, not IBorderView interface
        if (border is not Border borderControl) return;

        ApplyShape(handler, borderControl.StrokeShape);
    }

    private static void ApplyShape(BorderHandler handler, IShape? shape)
    {
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

    /// <summary>MAUI's <c>MapStrokeDashOffset</c> signature; maps the property on a Linux <see cref="BorderHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapStrokeDashOffset(IBorderHandler handler, IBorderView border)
    {
        if (handler is BorderHandler linuxHandler)
            MapStrokeDashOffsetCore(linuxHandler, border);
    }

    [Obsolete("Use the IBorderHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapStrokeDashOffset(BorderHandler handler, IBorderView border) => MapStrokeDashOffsetCore(handler, border);

    private static void MapStrokeDashOffsetCore(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        // StrokeDashOffset is on Border class
        if (border is Border borderControl)
        {
            handler.PlatformView.StrokeDashOffset = borderControl.StrokeDashOffset;
        }
        handler.PlatformView.Invalidate();
    }

    /// <summary>MAUI's <c>MapStrokeLineCap</c> signature; maps the property on a Linux <see cref="BorderHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapStrokeLineCap(IBorderHandler handler, IBorderView border)
    {
        if (handler is BorderHandler linuxHandler)
            MapStrokeLineCapCore(linuxHandler, border);
    }

    [Obsolete("Use the IBorderHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapStrokeLineCap(BorderHandler handler, IBorderView border) => MapStrokeLineCapCore(handler, border);

    private static void MapStrokeLineCapCore(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        if (border is IBorderStroke borderStroke)
        {
            handler.PlatformView.StrokeLineCap = borderStroke.StrokeLineCap;
        }
        handler.PlatformView.Invalidate();
    }

    /// <summary>MAUI's <c>MapStrokeLineJoin</c> signature; maps the property on a Linux <see cref="BorderHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapStrokeLineJoin(IBorderHandler handler, IBorderView border)
    {
        if (handler is BorderHandler linuxHandler)
            MapStrokeLineJoinCore(linuxHandler, border);
    }

    [Obsolete("Use the IBorderHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapStrokeLineJoin(BorderHandler handler, IBorderView border) => MapStrokeLineJoinCore(handler, border);

    private static void MapStrokeLineJoinCore(BorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is null) return;

        if (border is IBorderStroke borderStroke)
        {
            handler.PlatformView.StrokeLineJoin = borderStroke.StrokeLineJoin;
        }
        handler.PlatformView.Invalidate();
    }

    /// <summary>MAUI's <c>MapStrokeMiterLimit</c> signature; maps the property on a Linux <see cref="BorderHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapStrokeMiterLimit(IBorderHandler handler, IBorderView border)
    {
        if (handler is BorderHandler linuxHandler)
            MapStrokeMiterLimitCore(linuxHandler, border);
    }

    [Obsolete("Use the IBorderHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapStrokeMiterLimit(BorderHandler handler, IBorderView border) => MapStrokeMiterLimitCore(handler, border);

    private static void MapStrokeMiterLimitCore(BorderHandler handler, IBorderView border)
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
