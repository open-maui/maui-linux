// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Layout on Linux using Skia rendering.
/// Maps ILayout interface to SkiaLayoutView platform view.
/// </summary>
public partial class LayoutHandler : ViewHandler<ILayout, SkiaLayoutView>
{
    public static IPropertyMapper<ILayout, LayoutHandler> Mapper = new PropertyMapper<ILayout, LayoutHandler>(ViewHandler.ViewMapper)
    {
        [nameof(ILayout.ClipsToBounds)] = MapClipsToBounds,
        [nameof(IView.Background)] = MapBackground,
    };

    public static CommandMapper<ILayout, LayoutHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
        ["Add"] = MapAdd,
        ["Remove"] = MapRemove,
        ["Clear"] = MapClear,
        ["Insert"] = MapInsert,
        ["Update"] = MapUpdate,
    };

    public LayoutHandler() : base(Mapper, CommandMapper)
    {
    }

    public LayoutHandler(IPropertyMapper? mapper = null, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaLayoutView CreatePlatformView()
    {
        return new SkiaStackLayout();
    }

    public static void MapClipsToBounds(LayoutHandler handler, ILayout layout)
    {
        if (handler.PlatformView == null) return;
        handler.PlatformView.ClipToBounds = layout.ClipsToBounds;
    }

    public static void MapBackground(LayoutHandler handler, ILayout layout)
    {
        if (handler.PlatformView is null) return;

        if (layout.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }

    public static void MapAdd(LayoutHandler handler, ILayout layout, object? arg)
    {
        if (handler.PlatformView == null || arg is not LayoutHandlerUpdate update)
            return;

        var index = update.Index;
        var child = update.View;

        if (child?.Handler?.PlatformView is SkiaView skiaView)
        {
            if (index >= 0 && index < handler.PlatformView.Children.Count)
                handler.PlatformView.InsertChild(index, skiaView);
            else
                handler.PlatformView.AddChild(skiaView);
        }
    }

    public static void MapRemove(LayoutHandler handler, ILayout layout, object? arg)
    {
        if (handler.PlatformView == null || arg is not LayoutHandlerUpdate update)
            return;

        var index = update.Index;
        if (index >= 0 && index < handler.PlatformView.Children.Count)
        {
            handler.PlatformView.RemoveChildAt(index);
        }
    }

    public static void MapClear(LayoutHandler handler, ILayout layout, object? arg)
    {
        handler.PlatformView?.ClearChildren();
    }

    public static void MapInsert(LayoutHandler handler, ILayout layout, object? arg)
    {
        MapAdd(handler, layout, arg);
    }

    public static void MapUpdate(LayoutHandler handler, ILayout layout, object? arg)
    {
        // Force re-layout
        handler.PlatformView?.InvalidateMeasure();
    }
}

/// <summary>
/// Update payload for layout changes.
/// </summary>
public class LayoutHandlerUpdate
{
    public int Index { get; }
    public IView? View { get; }

    public LayoutHandlerUpdate(int index, IView? view)
    {
        Index = index;
        View = view;
    }
}

/// <summary>
/// Handler for StackLayout on Linux.
/// </summary>
public partial class StackLayoutHandler : LayoutHandler
{
    public static new IPropertyMapper<IStackLayout, StackLayoutHandler> Mapper = new PropertyMapper<IStackLayout, StackLayoutHandler>(LayoutHandler.Mapper)
    {
        [nameof(IStackLayout.Spacing)] = MapSpacing,
    };

    public StackLayoutHandler() : base(Mapper)
    {
    }

    protected override SkiaLayoutView CreatePlatformView()
    {
        return new SkiaStackLayout();
    }

    public static void MapSpacing(StackLayoutHandler handler, IStackLayout layout)
    {
        if (handler.PlatformView is SkiaStackLayout stackLayout)
        {
            stackLayout.Spacing = (float)layout.Spacing;
        }
    }
}

/// <summary>
/// Handler for Grid on Linux.
/// </summary>
public partial class GridHandler : LayoutHandler
{
    public static new IPropertyMapper<IGridLayout, GridHandler> Mapper = new PropertyMapper<IGridLayout, GridHandler>(LayoutHandler.Mapper)
    {
        [nameof(IGridLayout.RowSpacing)] = MapRowSpacing,
        [nameof(IGridLayout.ColumnSpacing)] = MapColumnSpacing,
    };

    public GridHandler() : base(Mapper)
    {
    }

    protected override SkiaLayoutView CreatePlatformView()
    {
        return new SkiaGrid();
    }

    public static void MapRowSpacing(GridHandler handler, IGridLayout layout)
    {
        if (handler.PlatformView is SkiaGrid grid)
        {
            grid.RowSpacing = (float)layout.RowSpacing;
        }
    }

    public static void MapColumnSpacing(GridHandler handler, IGridLayout layout)
    {
        if (handler.PlatformView is SkiaGrid grid)
        {
            grid.ColumnSpacing = (float)layout.ColumnSpacing;
        }
    }
}
