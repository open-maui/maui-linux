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
        [nameof(IPadding.Padding)] = MapPadding,
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

    protected override void ConnectHandler(SkiaLayoutView platformView)
    {
        base.ConnectHandler(platformView);

        // Create handlers for all children and add them to the platform view
        if (VirtualView == null || MauiContext == null) return;

        // Explicitly map BackgroundColor since it may be set before handler creation
        if (VirtualView is Microsoft.Maui.Controls.VisualElement ve && ve.BackgroundColor != null)
        {
            platformView.BackgroundColor = ve.BackgroundColor.ToSKColor();
        }

        for (int i = 0; i < VirtualView.Count; i++)
        {
            var child = VirtualView[i];
            if (child == null) continue;

            // Create handler for child if it doesn't exist
            if (child.Handler == null)
            {
                child.Handler = child.ToHandler(MauiContext);
            }

            // Add child's platform view to our layout
            if (child.Handler?.PlatformView is SkiaView skiaChild)
            {
                platformView.AddChild(skiaChild);
            }
        }
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

    public static void MapPadding(LayoutHandler handler, ILayout layout)
    {
        if (handler.PlatformView == null) return;

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

    protected override void ConnectHandler(SkiaLayoutView platformView)
    {
        // Set orientation first
        if (platformView is SkiaStackLayout stackLayout && VirtualView is IStackLayout stackView)
        {
            // Determine orientation based on view type
            if (VirtualView is Microsoft.Maui.Controls.HorizontalStackLayout)
            {
                stackLayout.Orientation = StackOrientation.Horizontal;
            }
            else if (VirtualView is Microsoft.Maui.Controls.VerticalStackLayout ||
                     VirtualView is Microsoft.Maui.Controls.StackLayout)
            {
                stackLayout.Orientation = StackOrientation.Vertical;
            }

            stackLayout.Spacing = (float)stackView.Spacing;
        }

        // Let base handle children
        base.ConnectHandler(platformView);
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
        [nameof(IGridLayout.RowDefinitions)] = MapRowDefinitions,
        [nameof(IGridLayout.ColumnDefinitions)] = MapColumnDefinitions,
    };

    public GridHandler() : base(Mapper)
    {
    }

    protected override SkiaLayoutView CreatePlatformView()
    {
        return new SkiaGrid();
    }

    protected override void ConnectHandler(SkiaLayoutView platformView)
    {
        try
        {
            // Don't call base - we handle children specially for Grid
            if (VirtualView is not IGridLayout gridLayout || MauiContext == null || platformView is not SkiaGrid grid) return;

            Console.WriteLine($"[GridHandler] ConnectHandler: {gridLayout.Count} children, {gridLayout.RowDefinitions.Count} rows, {gridLayout.ColumnDefinitions.Count} cols");

            // Explicitly map BackgroundColor since it may be set before handler creation
            if (VirtualView is Microsoft.Maui.Controls.VisualElement ve && ve.BackgroundColor != null)
            {
                platformView.BackgroundColor = ve.BackgroundColor.ToSKColor();
            }

            // Explicitly map Padding since it may be set before handler creation
            if (VirtualView is IPadding paddable)
            {
                var padding = paddable.Padding;
                platformView.Padding = new SKRect(
                    (float)padding.Left,
                    (float)padding.Top,
                    (float)padding.Right,
                    (float)padding.Bottom);
                Console.WriteLine($"[GridHandler] Applied Padding: L={padding.Left}, T={padding.Top}, R={padding.Right}, B={padding.Bottom}");
            }

            // Map row/column definitions first
            MapRowDefinitions(this, gridLayout);
            MapColumnDefinitions(this, gridLayout);

            // Add each child with its row/column position
            for (int i = 0; i < gridLayout.Count; i++)
            {
                var child = gridLayout[i];
                if (child == null) continue;

                Console.WriteLine($"[GridHandler] Processing child {i}: {child.GetType().Name}");

                // Create handler for child if it doesn't exist
                if (child.Handler == null)
                {
                    child.Handler = child.ToHandler(MauiContext);
                }

                // Get grid position from attached properties
                int row = 0, column = 0, rowSpan = 1, columnSpan = 1;
                if (child is Microsoft.Maui.Controls.View mauiView)
                {
                    row = Microsoft.Maui.Controls.Grid.GetRow(mauiView);
                    column = Microsoft.Maui.Controls.Grid.GetColumn(mauiView);
                    rowSpan = Microsoft.Maui.Controls.Grid.GetRowSpan(mauiView);
                    columnSpan = Microsoft.Maui.Controls.Grid.GetColumnSpan(mauiView);
                }

                Console.WriteLine($"[GridHandler] Child {i} at row={row}, col={column}, handler={child.Handler?.GetType().Name}");

                // Add child's platform view to our grid
                if (child.Handler?.PlatformView is SkiaView skiaChild)
                {
                    grid.AddChild(skiaChild, row, column, rowSpan, columnSpan);
                    Console.WriteLine($"[GridHandler] Added child {i} to grid");
                }
            }
            Console.WriteLine($"[GridHandler] ConnectHandler complete");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GridHandler] EXCEPTION in ConnectHandler: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[GridHandler] Stack trace: {ex.StackTrace}");
            throw;
        }
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

    public static void MapRowDefinitions(GridHandler handler, IGridLayout layout)
    {
        if (handler.PlatformView is not SkiaGrid grid) return;

        grid.RowDefinitions.Clear();
        foreach (var rowDef in layout.RowDefinitions)
        {
            var height = rowDef.Height;
            if (height.IsAbsolute)
                grid.RowDefinitions.Add(new Microsoft.Maui.Platform.GridLength((float)height.Value, Microsoft.Maui.Platform.GridUnitType.Absolute));
            else if (height.IsAuto)
                grid.RowDefinitions.Add(Microsoft.Maui.Platform.GridLength.Auto);
            else // Star
                grid.RowDefinitions.Add(new Microsoft.Maui.Platform.GridLength((float)height.Value, Microsoft.Maui.Platform.GridUnitType.Star));
        }
    }

    public static void MapColumnDefinitions(GridHandler handler, IGridLayout layout)
    {
        if (handler.PlatformView is not SkiaGrid grid) return;

        grid.ColumnDefinitions.Clear();
        foreach (var colDef in layout.ColumnDefinitions)
        {
            var width = colDef.Width;
            if (width.IsAbsolute)
                grid.ColumnDefinitions.Add(new Microsoft.Maui.Platform.GridLength((float)width.Value, Microsoft.Maui.Platform.GridUnitType.Absolute));
            else if (width.IsAuto)
                grid.ColumnDefinitions.Add(Microsoft.Maui.Platform.GridLength.Auto);
            else // Star
                grid.ColumnDefinitions.Add(new Microsoft.Maui.Platform.GridLength((float)width.Value, Microsoft.Maui.Platform.GridUnitType.Star));
        }
    }
}
