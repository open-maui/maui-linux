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
public partial class LayoutHandler : LinuxViewHandler<ILayout, SkiaLayoutView>, ILayoutHandler
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

    /// <summary>MAUI's <c>MapClipsToBounds</c> signature; maps the property on a Linux <see cref="LayoutHandler"/>.</summary>
    public static void MapClipsToBounds(ILayoutHandler handler, ILayout layout)
    {
        if (handler is LayoutHandler linuxHandler)
            MapClipsToBoundsCore(linuxHandler, layout);
    }

    private static void MapClipsToBoundsCore(LayoutHandler handler, ILayout layout)
    {
        if (handler.PlatformView == null) return;
        handler.PlatformView.ClipToBounds = layout.ClipsToBounds;
    }

    /// <summary>MAUI's <c>MapBackground</c> signature; maps the property on a Linux <see cref="LayoutHandler"/>.</summary>
    public static void MapBackground(ILayoutHandler handler, ILayout layout)
    {
        if (handler is LayoutHandler linuxHandler)
            MapBackgroundCore(linuxHandler, layout);
    }

    private static void MapBackgroundCore(LayoutHandler handler, ILayout layout)
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

    /// <summary>MAUI's <c>MapAdd</c> signature; runs the command on a Linux <see cref="LayoutHandler"/>.</summary>
    public static void MapAdd(ILayoutHandler handler, ILayout layout, object? arg)
    {
        if (handler is LayoutHandler linuxHandler)
            MapAddCore(linuxHandler, layout, arg);
        else if (arg is Microsoft.Maui.Handlers.LayoutHandlerUpdate update)
            handler.Add(update.View);
    }

    private static void MapAddCore(LayoutHandler handler, ILayout layout, object? arg)
    {
        // Use MAUI's LayoutHandlerUpdate type (Microsoft.Maui.Handlers namespace)
        if (arg is Microsoft.Maui.Handlers.LayoutHandlerUpdate update)
            handler.AddChildAt(update.Index, update.View);
    }

    /// <summary>MAUI's <c>MapRemove</c> signature; runs the command on a Linux <see cref="LayoutHandler"/>.</summary>
    public static void MapRemove(ILayoutHandler handler, ILayout layout, object? arg)
    {
        if (handler is LayoutHandler linuxHandler)
            MapRemoveCore(linuxHandler, layout, arg);
        else if (arg is Microsoft.Maui.Handlers.LayoutHandlerUpdate update)
            handler.Remove(update.View);
    }

    private static void MapRemoveCore(LayoutHandler handler, ILayout layout, object? arg)
    {
        if (handler.PlatformView == null) return;

        if (arg is not Microsoft.Maui.Handlers.LayoutHandlerUpdate update)
            return;

        // Controls.Layout sends the index the child had before it was removed.
        var index = update.Index;
        if (index >= 0 && index < handler.PlatformView.Children.Count)
        {
            handler.PlatformView.RemoveChildAt(index);
            handler.PlatformView.InvalidateMeasure();
            handler.PlatformView.Invalidate();
        }
        else if (update.View is { } view)
        {
            handler.Remove(view);
        }
    }

    /// <summary>MAUI's <c>MapClear</c> signature; runs the command on a Linux <see cref="LayoutHandler"/>.</summary>
    public static void MapClear(ILayoutHandler handler, ILayout layout, object? arg)
    {
        if (handler is LayoutHandler linuxHandler)
            MapClearCore(linuxHandler, layout, arg);
        else
            handler.Clear();
    }

    private static void MapClearCore(LayoutHandler handler, ILayout layout, object? arg)
    {
        handler.Clear();
    }

    /// <summary>MAUI's <c>MapInsert</c> signature; runs the command on a Linux <see cref="LayoutHandler"/>.</summary>
    public static void MapInsert(ILayoutHandler handler, ILayout layout, object? arg)
    {
        if (handler is LayoutHandler linuxHandler)
            MapInsertCore(linuxHandler, layout, arg);
        else if (arg is Microsoft.Maui.Handlers.LayoutHandlerUpdate update)
            handler.Insert(update.Index, update.View);
    }

    private static void MapInsertCore(LayoutHandler handler, ILayout layout, object? arg)
    {
        MapAddCore(handler, layout, arg);
    }

    /// <summary>
    /// A child's ZIndex changed (MAUI's ViewHandler sends this to the parent layout): the child
    /// takes its new place in the drawing and hit-testing order, as MAUI's layout handlers do.
    /// </summary>
    public static void MapUpdateZIndex(LayoutHandler handler, ILayout layout, object? arg)
    {
        if (arg is IView child)
            handler.UpdateZIndex(child);
        else
            handler.PlatformView?.Invalidate();
    }

    /// <summary>
    /// ILayout.Update: the child at the index was replaced (Layout[i] = view;
    /// BindableLayout swaps its rows this way when its ItemTemplate changes).
    /// The platform child there is replaced by the new view's; before, only a
    /// re-layout ran and the old rows stayed on screen.
    /// </summary>
    public static void MapUpdate(LayoutHandler handler, ILayout layout, object? arg)
    {
        if (arg is Microsoft.Maui.Handlers.LayoutHandlerUpdate update)
            handler.Update(update.Index, update.View);
        else
            handler.PlatformView?.InvalidateMeasure();
    }

    // ILayoutHandler: the operations MAUI's layout handlers expose as methods. Controls.Layout
    // drives them through the commands above (handler.Invoke("Add", LayoutHandlerUpdate)); MAUI
    // code that holds an ILayoutHandler calls them directly.

    /// <summary>Adds the child's platform view at the child's index in the layout.</summary>
    public void Add(IView child)
    {
        ArgumentNullException.ThrowIfNull(child);
        AddChildAt(VirtualView?.IndexOf(child) ?? -1, child);
    }

    /// <summary>Inserts the child's platform view at <paramref name="index"/>.</summary>
    public void Insert(int index, IView child)
    {
        ArgumentNullException.ThrowIfNull(child);
        AddChildAt(index, child);
    }

    /// <summary>Removes the child's platform view.</summary>
    public void Remove(IView child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (PlatformView is null || child.Handler?.PlatformView is not SkiaView childView)
            return;
        var children = PlatformView.Children;
        var index = -1;
        for (var i = 0; i < children.Count; i++)
        {
            if (ReferenceEquals(children[i], childView))
            {
                index = i;
                break;
            }
        }
        if (index < 0)
            return;
        PlatformView.RemoveChildAt(index);
        PlatformView.InvalidateMeasure();
        PlatformView.Invalidate();
    }

    /// <summary>Removes every platform child.</summary>
    public void Clear() => PlatformView?.ClearChildren();

    /// <summary>The child at <paramref name="index"/> was replaced by <paramref name="child"/>.</summary>
    public void Update(int index, IView child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (PlatformView == null)
            return;
        if (index >= 0 && index < PlatformView.Children.Count)
        {
            var current = PlatformView.Children[index];
            if (!ReferenceEquals(current, child.Handler?.PlatformView))
            {
                PlatformView.RemoveChildAt(index);
                AddChildAt(index, child);
            }
        }
        PlatformView.InvalidateMeasure();
    }

    /// <summary>The child's ZIndex changed: it takes its new place in the drawing and hit-testing order.</summary>
    public void UpdateZIndex(IView child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Handler?.PlatformView is SkiaView childView)
            childView.ZIndex = child.ZIndex;
        PlatformView?.Invalidate();
    }

    ILayout ILayoutHandler.VirtualView => VirtualView;

    object ILayoutHandler.PlatformView => PlatformView;

    // Realizes the child (creates its handler when it has none) and puts its platform view at
    // the index (appended when the index is out of range).
    private void AddChildAt(int index, IView child)
    {
        if (PlatformView == null)
            return;

        if (child.Handler == null && MauiContext != null)
            child.Handler = child.ToViewHandler(MauiContext);

        if (child.Handler?.PlatformView is SkiaView skiaView)
        {
            if (index >= 0 && index < PlatformView.Children.Count)
                PlatformView.InsertChild(index, skiaView);
            else
                PlatformView.AddChild(skiaView);
            PlatformView.InvalidateMeasure();
            PlatformView.Invalidate();
        }
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
