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
        ["BackgroundColor"] = MapBackgroundColor,
        [nameof(ILayout.ClipsToBounds)] = MapClipsToBounds,
        [nameof(IPadding.Padding)] = MapPadding,
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

    protected override void ConnectHandler(SkiaLayoutView platformView)
    {
        base.ConnectHandler(platformView);

        // Explicitly map BackgroundColor since it may be set before handler creation
        // (e.g., in ItemTemplates for CollectionView)
        if (VirtualView is Microsoft.Maui.Controls.VisualElement ve && ve.BackgroundColor != null)
        {
            platformView.BackgroundColor = ve.BackgroundColor.ToSKColor();
            platformView.Invalidate();
        }

        // Add existing children (important for template-created views)
        if (VirtualView is ILayout layout && MauiContext != null)
        {
            for (int i = 0; i < layout.Count; i++)
            {
                var child = layout[i];
                if (child == null) continue;

                // Create handler for child if it doesn't exist
                if (child.Handler == null)
                {
                    child.Handler = child.ToHandler(MauiContext);
                }

                if (child.Handler?.PlatformView is SkiaView skiaChild)
                {
                    platformView.AddChild(skiaChild);
                }
            }
        }
    }

    public static void MapBackground(LayoutHandler handler, ILayout layout)
    {
        // Don't override if BackgroundColor is explicitly set
        if (layout is Microsoft.Maui.Controls.VisualElement ve && ve.BackgroundColor != null)
            return;

        var background = layout.Background;
        if (background is SolidColorBrush solidBrush && solidBrush.Color != null)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color.ToSKColor();
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapBackgroundColor(LayoutHandler handler, ILayout layout)
    {
        if (layout is Microsoft.Maui.Controls.VisualElement ve && ve.BackgroundColor != null)
        {
            handler.PlatformView.BackgroundColor = ve.BackgroundColor.ToSKColor();
            handler.PlatformView.Invalidate();
        }
    }

    public static void MapClipsToBounds(LayoutHandler handler, ILayout layout)
    {
        handler.PlatformView.ClipToBounds = layout.ClipsToBounds;
        handler.PlatformView.Invalidate();
    }

    public static void MapPadding(LayoutHandler handler, ILayout layout)
    {
        if (layout is IPadding paddable)
        {
            var padding = paddable.Padding;
            handler.PlatformView.Padding = new SKRect(
                (float)padding.Left,
                (float)padding.Top,
                (float)padding.Right,
                (float)padding.Bottom);
            handler.PlatformView.InvalidateMeasure();
            handler.PlatformView.Invalidate();
        }
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
        [nameof(IGridLayout.RowDefinitions)] = MapRowDefinitions,
        [nameof(IGridLayout.ColumnDefinitions)] = MapColumnDefinitions,
    };

    public static new CommandMapper<IGridLayout, GridHandler> GridCommandMapper = new(LayoutHandler.CommandMapper)
    {
        ["Add"] = MapGridAdd,
    };

    public GridHandler() : base(Mapper, GridCommandMapper)
    {
    }

    protected override SkiaLayoutView CreatePlatformView()
    {
        return new SkiaGrid();
    }

    protected override void ConnectHandler(SkiaLayoutView platformView)
    {
        Console.WriteLine($"[GridHandler.ConnectHandler] Called! VirtualView={VirtualView?.GetType().Name}, PlatformView={platformView?.GetType().Name}, MauiContext={(MauiContext != null ? "set" : "null")}");
        base.ConnectHandler(platformView);

        // Map definitions on connect
        if (VirtualView is IGridLayout gridLayout && platformView is SkiaGrid grid && MauiContext != null)
        {
            Console.WriteLine($"[GridHandler.ConnectHandler] Grid has {gridLayout.Count} children, RowDefs={gridLayout.RowDefinitions?.Count ?? 0}");
            UpdateRowDefinitions(grid, gridLayout);
            UpdateColumnDefinitions(grid, gridLayout);

            // Add existing children (important for template-created views)
            for (int i = 0; i < gridLayout.Count; i++)
            {
                var child = gridLayout[i];
                if (child == null) continue;

                Console.WriteLine($"[GridHandler.ConnectHandler] Child[{i}]: {child.GetType().Name}, Handler={child.Handler?.GetType().Name ?? "null"}");

                // Create handler for child if it doesn't exist
                if (child.Handler == null)
                {
                    child.Handler = child.ToHandler(MauiContext);
                    Console.WriteLine($"[GridHandler.ConnectHandler] Created handler for child[{i}]: {child.Handler?.GetType().Name ?? "failed"}");
                }

                if (child.Handler?.PlatformView is SkiaView skiaChild)
                {
                    // Get grid position from attached properties
                    int row = 0, column = 0, rowSpan = 1, columnSpan = 1;
                    if (child is Microsoft.Maui.Controls.View mauiView)
                    {
                        row = Microsoft.Maui.Controls.Grid.GetRow(mauiView);
                        column = Microsoft.Maui.Controls.Grid.GetColumn(mauiView);
                        rowSpan = Microsoft.Maui.Controls.Grid.GetRowSpan(mauiView);
                        columnSpan = Microsoft.Maui.Controls.Grid.GetColumnSpan(mauiView);
                    }
                    Console.WriteLine($"[GridHandler.ConnectHandler] Adding child[{i}] at row={row}, col={column}");
                    grid.AddChild(skiaChild, row, column, rowSpan, columnSpan);
                }
            }
            Console.WriteLine($"[GridHandler.ConnectHandler] Grid now has {grid.Children.Count} SkiaView children");
        }
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

    public static void MapRowDefinitions(GridHandler handler, IGridLayout layout)
    {
        if (handler.PlatformView is SkiaGrid grid)
        {
            UpdateRowDefinitions(grid, layout);
            grid.InvalidateMeasure();
            grid.Invalidate();
        }
    }

    public static void MapColumnDefinitions(GridHandler handler, IGridLayout layout)
    {
        if (handler.PlatformView is SkiaGrid grid)
        {
            UpdateColumnDefinitions(grid, layout);
            grid.InvalidateMeasure();
            grid.Invalidate();
        }
    }

    private static void UpdateRowDefinitions(SkiaGrid grid, IGridLayout layout)
    {
        grid.RowDefinitions.Clear();
        foreach (var rowDef in layout.RowDefinitions)
        {
            var height = rowDef.Height;
            if (height.IsAbsolute)
                grid.RowDefinitions.Add(new GridLength((float)height.Value, GridUnitType.Absolute));
            else if (height.IsAuto)
                grid.RowDefinitions.Add(GridLength.Auto);
            else // Star
                grid.RowDefinitions.Add(new GridLength((float)height.Value, GridUnitType.Star));
        }
    }

    private static void UpdateColumnDefinitions(SkiaGrid grid, IGridLayout layout)
    {
        grid.ColumnDefinitions.Clear();
        foreach (var colDef in layout.ColumnDefinitions)
        {
            var width = colDef.Width;
            if (width.IsAbsolute)
                grid.ColumnDefinitions.Add(new GridLength((float)width.Value, GridUnitType.Absolute));
            else if (width.IsAuto)
                grid.ColumnDefinitions.Add(GridLength.Auto);
            else // Star
                grid.ColumnDefinitions.Add(new GridLength((float)width.Value, GridUnitType.Star));
        }
    }

    public static void MapGridAdd(GridHandler handler, ILayout layout, object? arg)
    {
        if (arg is LayoutHandlerUpdate update && handler.PlatformView is SkiaGrid grid)
        {
            var childHandler = update.View.Handler;
            if (childHandler?.PlatformView is SkiaView skiaView)
            {
                // Get grid position from attached properties
                int row = 0, column = 0, rowSpan = 1, columnSpan = 1;

                if (update.View is Microsoft.Maui.Controls.View mauiView)
                {
                    row = Microsoft.Maui.Controls.Grid.GetRow(mauiView);
                    column = Microsoft.Maui.Controls.Grid.GetColumn(mauiView);
                    rowSpan = Microsoft.Maui.Controls.Grid.GetRowSpan(mauiView);
                    columnSpan = Microsoft.Maui.Controls.Grid.GetColumnSpan(mauiView);
                }

                grid.AddChild(skiaView, row, column, rowSpan, columnSpan);
            }
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
