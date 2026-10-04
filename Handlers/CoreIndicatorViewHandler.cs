// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI's IndicatorViewHandler on Linux: the handler of a core <see cref="IIndicatorView"/>
/// (a library's own indicator view, or any view that implements the interface without
/// deriving from Controls' IndicatorView). Its platform view is the same
/// <see cref="SkiaIndicatorView"/> OpenMaui's <see cref="IndicatorViewHandler"/> uses for a
/// Controls IndicatorView, and it maps MAUI's IIndicatorView properties: Count, Position
/// (both ways: clicking an indicator sets <see cref="IIndicatorView.Position"/>), HideSingle,
/// MaximumVisible, IndicatorSize, IndicatorColor, SelectedIndicatorColor and IndicatorsShape.
/// </summary>
public partial class CoreIndicatorViewHandler : LinuxViewHandler<IIndicatorView, SkiaIndicatorView>, IIndicatorViewHandler
{
    private bool _isUpdatingPosition;

    public static IPropertyMapper<IIndicatorView, IIndicatorViewHandler> Mapper = new PropertyMapper<IIndicatorView, IIndicatorViewHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IIndicatorView.Count)] = MapCount,
        [nameof(IIndicatorView.Position)] = MapPosition,
        [nameof(IIndicatorView.HideSingle)] = MapHideSingle,
        [nameof(IIndicatorView.MaximumVisible)] = MapMaximumVisible,
        [nameof(IIndicatorView.IndicatorSize)] = MapIndicatorSize,
        [nameof(IIndicatorView.IndicatorColor)] = MapIndicatorColor,
        [nameof(IIndicatorView.SelectedIndicatorColor)] = MapSelectedIndicatorColor,
        [nameof(IIndicatorView.IndicatorsShape)] = MapIndicatorShape,
    };

    public static CommandMapper<IIndicatorView, IIndicatorViewHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public CoreIndicatorViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public CoreIndicatorViewHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper, CommandMapper)
    {
    }

    public CoreIndicatorViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    IIndicatorView IIndicatorViewHandler.VirtualView => VirtualView;

    object IIndicatorViewHandler.PlatformView => PlatformView;

    protected override SkiaIndicatorView CreatePlatformView() => new();

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

    private void OnPlatformPositionChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingPosition || VirtualView is null || PlatformView is null)
            return;
        _isUpdatingPosition = true;
        try { VirtualView.Position = PlatformView.Position; }
        finally { _isUpdatingPosition = false; }
    }

    static SkiaIndicatorView? IndicatorPlatformView(IIndicatorViewHandler handler) => handler.PlatformView as SkiaIndicatorView;

    public static void MapCount(IIndicatorViewHandler handler, IIndicatorView indicatorView)
    {
        if (IndicatorPlatformView(handler) is not { } platform)
            return;
        platform.Count = indicatorView.Count;
        // The count may have clamped the selection; show the view's position again.
        MapPosition(handler, indicatorView);
    }

    public static void MapPosition(IIndicatorViewHandler handler, IIndicatorView indicatorView)
    {
        if (IndicatorPlatformView(handler) is not { } platform)
            return;
        if (handler is CoreIndicatorViewHandler own)
        {
            if (own._isUpdatingPosition)
                return;
            own._isUpdatingPosition = true;
            try { platform.Position = indicatorView.Position; }
            finally { own._isUpdatingPosition = false; }
            return;
        }
        platform.Position = indicatorView.Position;
    }

    public static void MapHideSingle(IIndicatorViewHandler handler, IIndicatorView indicatorView)
    {
        if (IndicatorPlatformView(handler) is { } platform)
        {
            platform.HideSingle = indicatorView.HideSingle;
            platform.InvalidateMeasure();
            platform.Invalidate();
        }
    }

    public static void MapMaximumVisible(IIndicatorViewHandler handler, IIndicatorView indicatorView)
    {
        if (IndicatorPlatformView(handler) is { } platform)
        {
            platform.MaximumVisible = indicatorView.MaximumVisible;
            platform.InvalidateMeasure();
            platform.Invalidate();
        }
    }

    public static void MapIndicatorSize(IIndicatorViewHandler handler, IIndicatorView indicatorView)
    {
        if (IndicatorPlatformView(handler) is { } platform)
        {
            platform.IndicatorSize = indicatorView.IndicatorSize;
            platform.SelectedIndicatorSize = indicatorView.IndicatorSize;
            platform.InvalidateMeasure();
            platform.Invalidate();
        }
    }

    public static void MapIndicatorColor(IIndicatorViewHandler handler, IIndicatorView indicatorView)
    {
        if (IndicatorPlatformView(handler) is { } platform && indicatorView.IndicatorColor is SolidPaint { Color: { } color })
            platform.IndicatorColor = color;
    }

    public static void MapSelectedIndicatorColor(IIndicatorViewHandler handler, IIndicatorView indicatorView)
    {
        if (IndicatorPlatformView(handler) is { } platform && indicatorView.SelectedIndicatorColor is SolidPaint { Color: { } color })
            platform.SelectedIndicatorColor = color;
    }

    /// <summary>
    /// MAUI's platform page controls draw circles unless the shape is a rectangle (Controls'
    /// IndicatorView gives a Rectangle for <c>IndicatorShape.Square</c>, an Ellipse otherwise).
    /// </summary>
    public static void MapIndicatorShape(IIndicatorViewHandler handler, IIndicatorView indicatorView)
    {
        if (IndicatorPlatformView(handler) is not { } platform)
            return;
        platform.IndicatorShape = IsSquare(indicatorView.IndicatorsShape) ? IndicatorShape.Square : IndicatorShape.Circle;
        platform.Invalidate();
    }

    static bool IsSquare(IShape? shape) =>
        shape is Microsoft.Maui.Controls.Shapes.Rectangle or Microsoft.Maui.Controls.Shapes.RoundRectangle;
}
