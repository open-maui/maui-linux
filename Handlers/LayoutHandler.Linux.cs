// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Linux handler for Layout controls.
/// </summary>
public partial class LayoutHandler : ViewHandler<ILayout, SkiaLayoutView>
{
    /// <summary>
    /// Maps the property mapper for the handler.
    /// </summary>
    public static IPropertyMapper<ILayout, LayoutHandler> Mapper = new PropertyMapper<ILayout, LayoutHandler>(ViewHandler.ViewMapper)
    {
        [nameof(ILayout.Background)] = MapBackground,
        [nameof(ILayout.ClipsToBounds)] = MapClipsToBounds,
    };

    /// <summary>
    /// Maps the command mapper for the handler.
    /// </summary>
    public static CommandMapper<ILayout, LayoutHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
        ["Add"] = MapAdd,
        ["Remove"] = MapRemove,
        ["Clear"] = MapClear,
        ["Insert"] = MapInsert,
        ["Update"] = MapUpdate,
        ["UpdateZIndex"] = MapUpdateZIndex,
    };

    public LayoutHandler() : base(Mapper, CommandMapper)
    {
    }

    public LayoutHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper, CommandMapper)
    {
    }

    public LayoutHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaLayoutView CreatePlatformView()
    {
        // Return a concrete SkiaStackLayout as the default layout
        return new SkiaStackLayout();
    }

    public static void MapBackground(LayoutHandler handler, ILayout layout)
    {
        var background = layout.Background;
        if (background is SolidColorBrush solidBrush && solidBrush.Color != null)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color.ToSKColor();
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapClipsToBounds(LayoutHandler handler, ILayout layout)
    {
        handler.PlatformView.ClipToBounds = layout.ClipsToBounds;
        handler.PlatformView.Invalidate();
    }

    public static void MapAdd(LayoutHandler handler, ILayout layout, object? arg)
    {
        if (arg is LayoutHandlerUpdate update)
        {
            var childHandler = update.View.Handler;
            if (childHandler?.PlatformView is SkiaView skiaView)
            {
                handler.PlatformView.InsertChild(update.Index, skiaView);
            }
        }
    }

    public static void MapRemove(LayoutHandler handler, ILayout layout, object? arg)
    {
        if (arg is LayoutHandlerUpdate update)
        {
            handler.PlatformView.RemoveChildAt(update.Index);
        }
    }

    public static void MapClear(LayoutHandler handler, ILayout layout, object? arg)
    {
        handler.PlatformView.ClearChildren();
    }

    public static void MapInsert(LayoutHandler handler, ILayout layout, object? arg)
    {
        if (arg is LayoutHandlerUpdate update)
        {
            var childHandler = update.View.Handler;
            if (childHandler?.PlatformView is SkiaView skiaView)
            {
                handler.PlatformView.InsertChild(update.Index, skiaView);
            }
        }
    }

    public static void MapUpdate(LayoutHandler handler, ILayout layout, object? arg)
    {
        handler.PlatformView.InvalidateMeasure();
        handler.PlatformView.Invalidate();
    }

    public static void MapUpdateZIndex(LayoutHandler handler, ILayout layout, object? arg)
    {
        // Z-index is handled by child order for now
        handler.PlatformView.Invalidate();
    }
}

/// <summary>
/// Update information for layout operations.
/// </summary>
public class LayoutHandlerUpdate
{
    public int Index { get; }
    public IView View { get; }

    public LayoutHandlerUpdate(int index, IView view)
    {
        Index = index;
        View = view;
    }
}

/// <summary>
/// Linux handler for StackLayout.
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
            stackLayout.Invalidate();
        }
    }
}

/// <summary>
/// Linux handler for HorizontalStackLayout.
/// </summary>
public class HorizontalStackLayoutHandler : StackLayoutHandler
{
    protected override SkiaLayoutView CreatePlatformView()
    {
        return new SkiaStackLayout { Orientation = StackOrientation.Horizontal };
    }
}

/// <summary>
/// Linux handler for VerticalStackLayout.
/// </summary>
public class VerticalStackLayoutHandler : StackLayoutHandler
{
    protected override SkiaLayoutView CreatePlatformView()
    {
        return new SkiaStackLayout { Orientation = StackOrientation.Vertical };
    }
}

/// <summary>
/// Linux handler for Grid.
/// </summary>
public partial class GridHandler : LayoutHandler
{
    public static new IPropertyMapper<IGridLayout, GridHandler> Mapper = new PropertyMapper<IGridLayout, GridHandler>(LayoutHandler.Mapper)
    {
        [nameof(IGridLayout.ColumnSpacing)] = MapColumnSpacing,
        [nameof(IGridLayout.RowSpacing)] = MapRowSpacing,
    };

    public GridHandler() : base(Mapper)
    {
    }

    protected override SkiaLayoutView CreatePlatformView()
    {
        return new SkiaGrid();
    }

    public static void MapColumnSpacing(GridHandler handler, IGridLayout layout)
    {
        if (handler.PlatformView is SkiaGrid grid)
        {
            grid.ColumnSpacing = (float)layout.ColumnSpacing;
            grid.Invalidate();
        }
    }

    public static void MapRowSpacing(GridHandler handler, IGridLayout layout)
    {
        if (handler.PlatformView is SkiaGrid grid)
        {
            grid.RowSpacing = (float)layout.RowSpacing;
            grid.Invalidate();
        }
    }
}

/// <summary>
/// Linux handler for AbsoluteLayout.
/// </summary>
public partial class AbsoluteLayoutHandler : LayoutHandler
{
    public AbsoluteLayoutHandler() : base(Mapper)
    {
    }

    protected override SkiaLayoutView CreatePlatformView()
    {
        return new SkiaAbsoluteLayout();
    }
}

/// <summary>
/// Linux handler for ScrollView.
/// </summary>
public partial class ScrollViewHandler : ViewHandler<IScrollView, SkiaScrollView>
{
    public static IPropertyMapper<IScrollView, ScrollViewHandler> Mapper = new PropertyMapper<IScrollView, ScrollViewHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IScrollView.Content)] = MapContent,
        [nameof(IScrollView.HorizontalScrollBarVisibility)] = MapHorizontalScrollBarVisibility,
        [nameof(IScrollView.VerticalScrollBarVisibility)] = MapVerticalScrollBarVisibility,
        [nameof(IScrollView.Orientation)] = MapOrientation,
    };

    public static CommandMapper<IScrollView, ScrollViewHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
        [nameof(IScrollView.RequestScrollTo)] = MapRequestScrollTo,
    };

    public ScrollViewHandler() : base(Mapper, CommandMapper)
    {
    }

    protected override SkiaScrollView CreatePlatformView()
    {
        return new SkiaScrollView();
    }

    protected override void ConnectHandler(SkiaScrollView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Scrolled += OnScrolled;
    }

    protected override void DisconnectHandler(SkiaScrollView platformView)
    {
        platformView.Scrolled -= OnScrolled;
        base.DisconnectHandler(platformView);
    }

    private void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        VirtualView?.ScrollFinished();
    }

    public static void MapContent(ScrollViewHandler handler, IScrollView scrollView)
    {
        if (scrollView.PresentedContent?.Handler?.PlatformView is SkiaView content)
        {
            handler.PlatformView.Content = content;
        }
        else
        {
            handler.PlatformView.Content = null;
        }
    }

    public static void MapHorizontalScrollBarVisibility(ScrollViewHandler handler, IScrollView scrollView)
    {
        handler.PlatformView.HorizontalScrollBarVisibility = scrollView.HorizontalScrollBarVisibility switch
        {
            Microsoft.Maui.ScrollBarVisibility.Always => ScrollBarVisibility.Always,
            Microsoft.Maui.ScrollBarVisibility.Never => ScrollBarVisibility.Never,
            _ => ScrollBarVisibility.Auto
        };
    }

    public static void MapVerticalScrollBarVisibility(ScrollViewHandler handler, IScrollView scrollView)
    {
        handler.PlatformView.VerticalScrollBarVisibility = scrollView.VerticalScrollBarVisibility switch
        {
            Microsoft.Maui.ScrollBarVisibility.Always => ScrollBarVisibility.Always,
            Microsoft.Maui.ScrollBarVisibility.Never => ScrollBarVisibility.Never,
            _ => ScrollBarVisibility.Auto
        };
    }

    public static void MapOrientation(ScrollViewHandler handler, IScrollView scrollView)
    {
        handler.PlatformView.Orientation = scrollView.Orientation switch
        {
            Microsoft.Maui.ScrollOrientation.Horizontal => ScrollOrientation.Horizontal,
            Microsoft.Maui.ScrollOrientation.Both => ScrollOrientation.Both,
            Microsoft.Maui.ScrollOrientation.Neither => ScrollOrientation.Neither,
            _ => ScrollOrientation.Vertical
        };
    }

    public static void MapRequestScrollTo(ScrollViewHandler handler, IScrollView scrollView, object? arg)
    {
        if (arg is ScrollToRequest request)
        {
            handler.PlatformView.ScrollTo(
                (float)request.HorizontalOffset,
                (float)request.VerticalOffset,
                request.Instant == false);
        }
    }
}

/// <summary>
/// Scroll to request.
/// </summary>
public class ScrollToRequest
{
    public double HorizontalOffset { get; set; }
    public double VerticalOffset { get; set; }
    public bool Instant { get; set; }
}
