// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Hosting;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for IndicatorView on Linux using Skia rendering.
/// Maps IndicatorView to SkiaIndicatorView platform view.
/// </summary>
public partial class IndicatorViewHandler : ViewHandler<IndicatorView, SkiaIndicatorView>
{
    private bool _isUpdatingPosition;

    public static IPropertyMapper<IndicatorView, IndicatorViewHandler> Mapper =
        new PropertyMapper<IndicatorView, IndicatorViewHandler>(ViewHandler.ViewMapper)
        {
            [nameof(IndicatorView.Count)] = MapCount,
            [nameof(IndicatorView.Position)] = MapPosition,
            [nameof(IndicatorView.IndicatorColor)] = MapIndicatorColor,
            [nameof(IndicatorView.SelectedIndicatorColor)] = MapSelectedIndicatorColor,
            [nameof(IndicatorView.IndicatorSize)] = MapIndicatorSize,
            [nameof(IndicatorView.IndicatorsShape)] = MapIndicatorsShape,
            [nameof(IndicatorView.MaximumVisible)] = MapMaximumVisible,
            [nameof(IndicatorView.HideSingle)] = MapHideSingle,
            [nameof(IndicatorView.ItemsSource)] = MapItemsSource,
        };

    public static CommandMapper<IndicatorView, IndicatorViewHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
        };

    public IndicatorViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public IndicatorViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaIndicatorView CreatePlatformView()
    {
        return new SkiaIndicatorView();
    }

    protected override void ConnectHandler(SkiaIndicatorView platformView)
    {
        base.ConnectHandler(platformView);
        // SkiaIndicatorView doesn't have position changed event, but we can add one if needed
    }

    protected override void DisconnectHandler(SkiaIndicatorView platformView)
    {
        base.DisconnectHandler(platformView);
    }

    public static void MapCount(IndicatorViewHandler handler, IndicatorView indicatorView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Count = indicatorView.Count;
    }

    public static void MapPosition(IndicatorViewHandler handler, IndicatorView indicatorView)
    {
        if (handler.PlatformView is null || handler._isUpdatingPosition) return;

        try
        {
            handler._isUpdatingPosition = true;
            handler.PlatformView.Position = indicatorView.Position;
        }
        finally
        {
            handler._isUpdatingPosition = false;
        }
    }

    public static void MapIndicatorColor(IndicatorViewHandler handler, IndicatorView indicatorView)
    {
        if (handler.PlatformView is null) return;

        if (indicatorView.IndicatorColor is not null)
        {
            handler.PlatformView.IndicatorColor = indicatorView.IndicatorColor.ToSKColor();
        }
    }

    public static void MapSelectedIndicatorColor(IndicatorViewHandler handler, IndicatorView indicatorView)
    {
        if (handler.PlatformView is null) return;

        if (indicatorView.SelectedIndicatorColor is not null)
        {
            handler.PlatformView.SelectedIndicatorColor = indicatorView.SelectedIndicatorColor.ToSKColor();
        }
    }

    public static void MapIndicatorSize(IndicatorViewHandler handler, IndicatorView indicatorView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IndicatorSize = (float)indicatorView.IndicatorSize;
        handler.PlatformView.SelectedIndicatorSize = (float)indicatorView.IndicatorSize;
    }

    public static void MapIndicatorsShape(IndicatorViewHandler handler, IndicatorView indicatorView)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.IndicatorShape = indicatorView.IndicatorsShape switch
        {
            Controls.IndicatorShape.Circle => Platform.IndicatorShape.Circle,
            Controls.IndicatorShape.Square => Platform.IndicatorShape.Square,
            _ => Platform.IndicatorShape.Circle
        };
    }

    public static void MapMaximumVisible(IndicatorViewHandler handler, IndicatorView indicatorView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MaximumVisible = indicatorView.MaximumVisible;
    }

    public static void MapHideSingle(IndicatorViewHandler handler, IndicatorView indicatorView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.HideSingle = indicatorView.HideSingle;
    }

    public static void MapItemsSource(IndicatorViewHandler handler, IndicatorView indicatorView)
    {
        if (handler.PlatformView is null) return;

        // Count items from ItemsSource
        int count = 0;
        if (indicatorView.ItemsSource is System.Collections.ICollection collection)
        {
            count = collection.Count;
        }
        else if (indicatorView.ItemsSource is System.Collections.IEnumerable enumerable)
        {
            foreach (var _ in enumerable)
            {
                count++;
            }
        }

        handler.PlatformView.Count = count;
    }
}
