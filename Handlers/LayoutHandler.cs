// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Layout on Linux using Skia rendering. Every MAUI layout is
/// measured and arranged by its own <c>ILayoutManager</c> (Grid, the stack
/// layouts, FlexLayout, AbsoluteLayout and custom layouts alike) on a
/// <see cref="SkiaCrossPlatformLayout"/>, as on the other platforms; the
/// handler manages the platform children, padding, clipping and background.
/// </summary>
public partial class LayoutHandler : LinuxViewHandler<ILayout, SkiaLayoutView>
{
    public static IPropertyMapper<ILayout, LayoutHandler> Mapper = new PropertyMapper<ILayout, LayoutHandler>(ViewHandler.ViewMapper)
    {
        [nameof(ILayout.ClipsToBounds)] = MapClipsToBounds,
        [nameof(IView.Background)] = MapBackground,
        ["BackgroundColor"] = MapBackgroundColor,
        [nameof(IPadding.Padding)] = MapPadding,
        ["WidthRequest"] = MapWidthRequest,
        ["HeightRequest"] = MapHeightRequest,
    };

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

    public LayoutHandler(IPropertyMapper? mapper = null, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaLayoutView CreatePlatformView()
    {
        return new SkiaCrossPlatformLayout();
    }

    protected override void ConnectHandler(SkiaLayoutView platformView)
    {
        base.ConnectHandler(platformView);

        // Create handlers for all children and add them to the platform view
        if (VirtualView == null || MauiContext == null) return;

        // Wire MauiView so the layout reads BackgroundColor live from the MAUI Layout.
        // CRITICAL: do NOT echo `platformView.BackgroundColor = ve.BackgroundColor`
        // when MauiView is wired — that setter routes back to ve.SetValue() at
        // LocalValue specificity, which is higher than Binding specificity, and
        // permanently clobbers AppThemeBinding so theme toggles stop firing
        // PropertyChanged after the first one.
        if (VirtualView is View view)
            platformView.MauiView = view;

        if (VirtualView is Microsoft.Maui.Controls.VisualElement ve)
        {
            if (platformView.MauiView is null && ve.BackgroundColor != null)
                platformView.BackgroundColor = ve.BackgroundColor;
            if (ve.WidthRequest >= 0)
                platformView.WidthRequest = ve.WidthRequest;
            if (ve.HeightRequest >= 0)
                platformView.HeightRequest = ve.HeightRequest;
        }

        for (int i = 0; i < VirtualView.Count; i++)
        {
            var child = VirtualView[i];
            if (child == null) continue;

            try
            {
                // Create handler for child if it doesn't exist
                if (child.Handler == null)
                {
                    child.Handler = child.ToViewHandler(MauiContext);
                }

                // Add child's platform view to our layout
                if (child.Handler?.PlatformView is SkiaView skiaChild)
                {
                    platformView.AddChild(skiaChild);
                }
            }
            catch (Exception ex)
            {
                // Skip unsupported child views (e.g. third-party controls without Linux handlers)
                DiagnosticLog.Error("LayoutHandler", $"Skipping child {i} ({child.GetType().Name}): {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Detaches the platform children, as MAUI's layout handlers do (Android
    /// RemoveAllViews, iOS removing the subviews). The child views keep their own
    /// handlers, so a new layout handler for the same layout can add them again
    /// (a platform child that still had this view as its parent could not be added).
    /// </summary>
    protected override void DisconnectHandler(SkiaLayoutView platformView)
    {
        platformView.ClearChildren();
        base.DisconnectHandler(platformView);
    }

    public static void MapClipsToBounds(LayoutHandler handler, ILayout layout)
    {
        if (handler.PlatformView == null) return;
        handler.PlatformView.ClipToBounds = layout.ClipsToBounds;
    }

    public static void MapBackground(LayoutHandler handler, ILayout layout)
    {
        if (handler.PlatformView is null) return;

        // When MauiView is wired the platform view reads Background/BackgroundColor
        // live and OnMauiViewPropertyChanged refreshes the cache on bind updates.
        // Echoing back via the public setter routes through ve.SetValue() at
        // LocalValue specificity and clobbers AppThemeBindings on the property.
        if (handler.PlatformView.MauiView != null)
        {
            handler.PlatformView.Invalidate();
            return;
        }

        if (layout.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
        else if (layout is Microsoft.Maui.Controls.VisualElement ve && ve.BackgroundColor is not null)
        {
            handler.PlatformView.BackgroundColor = ve.BackgroundColor;
        }
    }

    public static void MapBackgroundColor(LayoutHandler handler, ILayout layout)
    {
        if (handler.PlatformView is null) return;

        // See note in MapBackground.
        if (handler.PlatformView.MauiView != null)
        {
            handler.PlatformView.Invalidate();
            return;
        }

        if (layout is Microsoft.Maui.Controls.VisualElement ve && ve.BackgroundColor is not null)
        {
            handler.PlatformView.BackgroundColor = ve.BackgroundColor;
        }
    }

    public static void MapAdd(LayoutHandler handler, ILayout layout, object? arg)
    {
        if (handler.PlatformView == null) return;

        // Use MAUI's LayoutHandlerUpdate type (Microsoft.Maui.Handlers namespace)
        if (arg is not Microsoft.Maui.Handlers.LayoutHandlerUpdate update)
            return;

        var index = update.Index;
        var child = update.View;

        // Create handler for child if needed
        if (child is IView view && child.Handler == null && handler.MauiContext != null)
        {
            child.Handler = view.ToViewHandler(handler.MauiContext);
        }

        if (child?.Handler?.PlatformView is SkiaView skiaView)
        {
            if (index >= 0 && index < handler.PlatformView.Children.Count)
                handler.PlatformView.InsertChild(index, skiaView);
            else
                handler.PlatformView.AddChild(skiaView);
            handler.PlatformView.InvalidateMeasure();
            handler.PlatformView.Invalidate();
        }
    }

    public static void MapRemove(LayoutHandler handler, ILayout layout, object? arg)
    {
        if (handler.PlatformView == null) return;

        if (arg is not Microsoft.Maui.Handlers.LayoutHandlerUpdate update)
            return;

        var index = update.Index;
        if (index >= 0 && index < handler.PlatformView.Children.Count)
        {
            handler.PlatformView.RemoveChildAt(index);
            handler.PlatformView.InvalidateMeasure();
            handler.PlatformView.Invalidate();
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

    /// <summary>
    /// ILayout.Update: the child at the index was replaced (Layout[i] = view;
    /// BindableLayout swaps its rows this way when its ItemTemplate changes).
    /// The platform child there is replaced by the new view's; before, only a
    /// re-layout ran and the old rows stayed on screen.
    /// </summary>
    /// <summary>
    /// A child's ZIndex changed (MAUI's ViewHandler sends this to the parent layout): the child
    /// takes its new place in the drawing and hit-testing order, as MAUI's layout handlers do.
    /// </summary>
    public static void MapUpdateZIndex(LayoutHandler handler, ILayout layout, object? arg)
    {
        if (arg is IView child && child.Handler?.PlatformView is SkiaView childView)
            childView.ZIndex = child.ZIndex;
        handler.PlatformView?.Invalidate();
    }

    public static void MapUpdate(LayoutHandler handler, ILayout layout, object? arg)
    {
        if (handler.PlatformView == null)
            return;
        if (arg is Microsoft.Maui.Handlers.LayoutHandlerUpdate update
            && update.Index >= 0 && update.Index < handler.PlatformView.Children.Count)
        {
            var current = handler.PlatformView.Children[update.Index];
            if (!ReferenceEquals(current, update.View.Handler?.PlatformView))
            {
                handler.PlatformView.RemoveChildAt(update.Index);
                MapAdd(handler, layout, arg);
            }
        }
        handler.PlatformView.InvalidateMeasure();
    }

    public static void MapPadding(LayoutHandler handler, ILayout layout)
    {
        if (handler.PlatformView == null) return;

        if (layout is IPadding paddable)
        {
            handler.PlatformView.Padding = paddable.Padding;
            handler.PlatformView.InvalidateMeasure();
            handler.PlatformView.Invalidate();
        }
    }

    public static void MapWidthRequest(LayoutHandler handler, ILayout layout)
    {
        if (handler.PlatformView is null) return;
        if (layout is Microsoft.Maui.Controls.VisualElement ve && ve.WidthRequest >= 0)
        {
            handler.PlatformView.WidthRequest = ve.WidthRequest;
            handler.PlatformView.InvalidateMeasure();
        }
    }

    public static void MapHeightRequest(LayoutHandler handler, ILayout layout)
    {
        if (handler.PlatformView is null) return;
        if (layout is Microsoft.Maui.Controls.VisualElement ve && ve.HeightRequest >= 0)
        {
            handler.PlatformView.HeightRequest = ve.HeightRequest;
            handler.PlatformView.InvalidateMeasure();
        }
    }
}

// NOTE: Layout add/remove/insert commands use Microsoft.Maui.Handlers.LayoutHandlerUpdate
// from the MAUI framework. Do NOT define a local LayoutHandlerUpdate class here.

/// <summary>
/// Handler for StackLayout, VerticalStackLayout and HorizontalStackLayout on
/// Linux: MAUI's stack layout managers place the children (a legacy
/// StackLayout follows its Orientation), so spacing, alignment, margins and
/// collapsed children behave as on every platform.
/// </summary>
public partial class StackLayoutHandler : LayoutHandler
{
    public static new IPropertyMapper<IStackLayout, StackLayoutHandler> Mapper = new PropertyMapper<IStackLayout, StackLayoutHandler>(LayoutHandler.Mapper)
    {
        [nameof(IStackLayout.Spacing)] = MapSpacing,
        [nameof(Microsoft.Maui.Controls.StackLayout.Orientation)] = MapOrientation,
    };

    public StackLayoutHandler() : base(Mapper)
    {
    }

    protected override SkiaLayoutView CreatePlatformView()
    {
        return new SkiaCrossPlatformLayout();
    }

    /// <summary>
    /// A legacy StackLayout's Orientation (Syncfusion's SfChipGroup lays its chips out in a
    /// horizontal one); the layout's manager follows it, so the platform view is re-laid out.
    /// </summary>
    public static void MapOrientation(StackLayoutHandler handler, IStackLayout layout) =>
        handler.PlatformView?.InvalidateMeasure();

    /// <summary>The spacing is read by the layout's manager; the platform view is re-laid out.</summary>
    public static void MapSpacing(StackLayoutHandler handler, IStackLayout layout) =>
        handler.PlatformView?.InvalidateMeasure();
}

/// <summary>
/// Handler for Grid on Linux: MAUI's GridLayoutManager sizes the rows and
/// columns and places the children (spans, Auto and Star sizing, spacing and
/// alignment), as on every platform. Rows, columns and the children's
/// attached Row/Column/spans are read from the Grid each time it is measured.
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
        return new SkiaCrossPlatformLayout();
    }

    /// <summary>Read by the grid's layout manager; the platform view is re-laid out.</summary>
    public static void MapRowSpacing(GridHandler handler, IGridLayout layout) =>
        handler.PlatformView?.InvalidateMeasure();

    /// <summary>Read by the grid's layout manager; the platform view is re-laid out.</summary>
    public static void MapColumnSpacing(GridHandler handler, IGridLayout layout) =>
        handler.PlatformView?.InvalidateMeasure();

    /// <summary>Read by the grid's layout manager; the platform view is re-laid out.</summary>
    public static void MapRowDefinitions(GridHandler handler, IGridLayout layout) =>
        handler.PlatformView?.InvalidateMeasure();

    /// <summary>Read by the grid's layout manager; the platform view is re-laid out.</summary>
    public static void MapColumnDefinitions(GridHandler handler, IGridLayout layout) =>
        handler.PlatformView?.InvalidateMeasure();
}

/// <summary>
/// Fallback for any <c>Layout</c> subclass without a dedicated handler (a
/// third-party or app-defined layout with its own <c>ILayoutManager</c>):
/// children are managed like every layout, measure and arrange are the
/// layout's own. Before this, such layouts resolved to MAUI's portable
/// LayoutHandler (which throws) or to a stack layout that ignored their manager.
/// </summary>
public class CrossPlatformLayoutHandler : LayoutHandler
{
    protected override SkiaLayoutView CreatePlatformView() => new SkiaCrossPlatformLayout();
}
