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
/// Maps IndicatorView to SkiaIndicatorView platform view. A core <see cref="IIndicatorView"/>
/// that is not a Controls IndicatorView gets <see cref="CoreIndicatorViewHandler"/>.
/// </summary>
public partial class IndicatorViewHandler : LinuxViewHandler<IndicatorView, SkiaIndicatorView>, IIndicatorViewHandler
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

    IIndicatorView IIndicatorViewHandler.VirtualView => VirtualView;

    object IIndicatorViewHandler.PlatformView => PlatformView;

    protected override SkiaIndicatorView CreatePlatformView()
    {
        return new SkiaIndicatorView();
    }

    protected override void ConnectHandler(SkiaIndicatorView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.PositionChanged += OnPlatformPositionChanged;
    }

    protected override void DisconnectHandler(SkiaIndicatorView platformView)
    {
        platformView.PositionChanged -= OnPlatformPositionChanged;
        base.DisconnectHandler(platformView);
    }

    // A click on an indicator selects it: the IndicatorView's Position follows (and with it a
    // CarouselView bound to it), as MAUI's platform page controls report a tap.
    private void OnPlatformPositionChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingPosition || VirtualView is null || PlatformView is null)
            return;
        _isUpdatingPosition = true;
        try { VirtualView.Position = PlatformView.Position; }
        finally { _isUpdatingPosition = false; }
    }

    /// <summary>MAUI's <c>MapCount</c> signature; maps the property on a Linux <see cref="IndicatorViewHandler"/>.</summary>
    public static void MapCount(IIndicatorViewHandler handler, IIndicatorView indicator)
    {
        if (handler is IndicatorViewHandler linuxHandler && indicator is IndicatorView linuxView)
            MapCountCore(linuxHandler, linuxView);
    }

    private static void MapCountCore(IndicatorViewHandler handler, IndicatorView indicatorView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Count = indicatorView.Count;
    }

    /// <summary>MAUI's <c>MapPosition</c> signature; maps the property on a Linux <see cref="IndicatorViewHandler"/>.</summary>
    public static void MapPosition(IIndicatorViewHandler handler, IIndicatorView indicator)
    {
        if (handler is IndicatorViewHandler linuxHandler && indicator is IndicatorView linuxView)
            MapPositionCore(linuxHandler, linuxView);
    }

    private static void MapPositionCore(IndicatorViewHandler handler, IndicatorView indicatorView)
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

    /// <summary>MAUI's <c>MapIndicatorColor</c> signature; maps the property on a Linux <see cref="IndicatorViewHandler"/>.</summary>
    public static void MapIndicatorColor(IIndicatorViewHandler handler, IIndicatorView indicator)
    {
        if (handler is IndicatorViewHandler linuxHandler && indicator is IndicatorView linuxView)
            MapIndicatorColorCore(linuxHandler, linuxView);
    }

    private static void MapIndicatorColorCore(IndicatorViewHandler handler, IndicatorView indicatorView)
    {
        if (handler.PlatformView is null) return;

        if (indicatorView.IndicatorColor is not null)
        {
            handler.PlatformView.IndicatorColor = indicatorView.IndicatorColor;
        }
    }

    /// <summary>MAUI's <c>MapSelectedIndicatorColor</c> signature; maps the property on a Linux <see cref="IndicatorViewHandler"/>.</summary>
    public static void MapSelectedIndicatorColor(IIndicatorViewHandler handler, IIndicatorView indicator)
    {
        if (handler is IndicatorViewHandler linuxHandler && indicator is IndicatorView linuxView)
            MapSelectedIndicatorColorCore(linuxHandler, linuxView);
    }

    private static void MapSelectedIndicatorColorCore(IndicatorViewHandler handler, IndicatorView indicatorView)
    {
        if (handler.PlatformView is null) return;

        if (indicatorView.SelectedIndicatorColor is not null)
        {
            handler.PlatformView.SelectedIndicatorColor = indicatorView.SelectedIndicatorColor;
        }
    }

    /// <summary>MAUI's <c>MapIndicatorSize</c> signature; maps the property on a Linux <see cref="IndicatorViewHandler"/>.</summary>
    public static void MapIndicatorSize(IIndicatorViewHandler handler, IIndicatorView indicator)
    {
        if (handler is IndicatorViewHandler linuxHandler && indicator is IndicatorView linuxView)
            MapIndicatorSizeCore(linuxHandler, linuxView);
    }

    private static void MapIndicatorSizeCore(IndicatorViewHandler handler, IndicatorView indicatorView)
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
            Controls.IndicatorShape.Circle => SkiaIndicatorShape.Circle,
            Controls.IndicatorShape.Square => SkiaIndicatorShape.Square,
            _ => SkiaIndicatorShape.Circle
        };
    }

    /// <summary>MAUI's <c>MapMaximumVisible</c> signature; maps the property on a Linux <see cref="IndicatorViewHandler"/>.</summary>
    public static void MapMaximumVisible(IIndicatorViewHandler handler, IIndicatorView indicator)
    {
        if (handler is IndicatorViewHandler linuxHandler && indicator is IndicatorView linuxView)
            MapMaximumVisibleCore(linuxHandler, linuxView);
    }

    private static void MapMaximumVisibleCore(IndicatorViewHandler handler, IndicatorView indicatorView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MaximumVisible = indicatorView.MaximumVisible;
    }

    /// <summary>MAUI's <c>MapHideSingle</c> signature; maps the property on a Linux <see cref="IndicatorViewHandler"/>.</summary>
    public static void MapHideSingle(IIndicatorViewHandler handler, IIndicatorView indicator)
    {
        if (handler is IndicatorViewHandler linuxHandler && indicator is IndicatorView linuxView)
            MapHideSingleCore(linuxHandler, linuxView);
    }

    private static void MapHideSingleCore(IndicatorViewHandler handler, IndicatorView indicatorView)
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

    /// <summary>MAUI's <c>MapIndicatorShape</c>: the indicators' shape (IndicatorView.IndicatorsShape).</summary>
    public static void MapIndicatorShape(IIndicatorViewHandler handler, IIndicatorView indicator)
    {
        if (handler is IndicatorViewHandler linuxHandler && indicator is IndicatorView indicatorView)
            MapIndicatorsShape(linuxHandler, indicatorView);
    }
}
