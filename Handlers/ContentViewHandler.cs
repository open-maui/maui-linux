// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for ContentView on Linux using Skia rendering.
/// ContentView is a simple container with a single Content child.
/// </summary>
public partial class ContentViewHandler : ViewHandler<IContentView, SkiaContentView>
{
    public static IPropertyMapper<IContentView, ContentViewHandler> Mapper =
        new PropertyMapper<IContentView, ContentViewHandler>(ViewHandler.ViewMapper)
        {
            [nameof(IContentView.Content)] = MapContent,
            [nameof(IView.Background)] = MapBackground,
            ["BackgroundColor"] = MapBackgroundColor,
            [nameof(IPadding.Padding)] = MapPadding,
            ["WidthRequest"] = MapWidthRequest,
            ["HeightRequest"] = MapHeightRequest,
        };

    public static CommandMapper<IContentView, ContentViewHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper);

    public ContentViewHandler() : base(Mapper, CommandMapper) { }

    public ContentViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper) { }

    protected override SkiaContentView CreatePlatformView() => new SkiaContentView();

    protected override void ConnectHandler(SkiaContentView platformView)
    {
        base.ConnectHandler(platformView);

        if (VirtualView is VisualElement ve)
        {
            if (ve.BackgroundColor != null)
                platformView.BackgroundColor = ve.BackgroundColor;
            if (ve.WidthRequest >= 0)
                platformView.WidthRequest = ve.WidthRequest;
            if (ve.HeightRequest >= 0)
                platformView.HeightRequest = ve.HeightRequest;
        }

        MapContent(this, VirtualView);
    }

    public static void MapContent(ContentViewHandler handler, IContentView contentView)
    {
        if (handler.PlatformView is null || handler.MauiContext is null) return;

        handler.PlatformView.ClearChildren();

        var content = contentView.PresentedContent ?? contentView.Content;
        if (content is IView view)
        {
            try
            {
                if (view is Element element && element.Handler == null)
                {
                    element.Handler = view.ToViewHandler(handler.MauiContext);
                }

                if (view.Handler?.PlatformView is SkiaView skiaChild)
                {
                    handler.PlatformView.AddChild(skiaChild);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("ContentViewHandler", $"Failed to render content ({view.GetType().Name}): {ex.Message}");
            }
        }
    }

    public static void MapBackground(ContentViewHandler handler, IContentView contentView)
    {
        if (handler.PlatformView is null) return;
        if (contentView is VisualElement ve && ve.Background is SolidColorBrush scb)
        {
            handler.PlatformView.BackgroundColor = scb.Color;
        }
    }

    public static void MapBackgroundColor(ContentViewHandler handler, IContentView contentView)
    {
        if (handler.PlatformView is null) return;
        if (contentView is VisualElement ve && ve.BackgroundColor != null)
        {
            handler.PlatformView.BackgroundColor = ve.BackgroundColor;
        }
    }

    public static void MapPadding(ContentViewHandler handler, IContentView contentView)
    {
        if (handler.PlatformView is null) return;
        if (contentView is IPadding paddable)
        {
            handler.PlatformView.Padding = paddable.Padding;
        }
    }

    public static void MapWidthRequest(ContentViewHandler handler, IContentView contentView)
    {
        if (handler.PlatformView is null) return;
        if (contentView is VisualElement ve && ve.WidthRequest >= 0)
        {
            handler.PlatformView.WidthRequest = ve.WidthRequest;
            handler.PlatformView.InvalidateMeasure();
        }
    }

    public static void MapHeightRequest(ContentViewHandler handler, IContentView contentView)
    {
        if (handler.PlatformView is null) return;
        if (contentView is VisualElement ve && ve.HeightRequest >= 0)
        {
            handler.PlatformView.HeightRequest = ve.HeightRequest;
            handler.PlatformView.InvalidateMeasure();
        }
    }
}
