using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp.Views.Maui.Controls;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace Microsoft.Maui.Platform.Linux.Hosting;

/// <summary>
/// Extension methods for creating MAUI handlers on Linux.
/// Maps MAUI types to Linux-specific handlers with fallback to MAUI defaults.
/// </summary>
public static class MauiHandlerExtensions
{
    private static readonly Dictionary<Type, Func<IElementHandler>> LinuxHandlerMap = new Dictionary<Type, Func<IElementHandler>>
    {
        [typeof(Button)] = () => new TextButtonHandler(),
        [typeof(Label)] = () => new LabelHandler(),
        [typeof(Entry)] = () => new EntryHandler(),
        [typeof(Editor)] = () => new EditorHandler(),
        [typeof(CheckBox)] = () => new CheckBoxHandler(),
        [typeof(Switch)] = () => new SwitchHandler(),
        [typeof(Slider)] = () => new SliderHandler(),
        [typeof(Stepper)] = () => new StepperHandler(),
        [typeof(ProgressBar)] = () => new ProgressBarHandler(),
        [typeof(ActivityIndicator)] = () => new ActivityIndicatorHandler(),
        [typeof(Picker)] = () => new PickerHandler(),
        [typeof(DatePicker)] = () => new DatePickerHandler(),
        [typeof(TimePicker)] = () => new TimePickerHandler(),
        [typeof(SearchBar)] = () => new SearchBarHandler(),
        [typeof(RadioButton)] = () => new RadioButtonHandler(),
        [typeof(WebView)] = () => new LinuxWebViewHandler(),
        [typeof(Image)] = () => new ImageHandler(),
        [typeof(ImageButton)] = () => new ImageButtonHandler(),
        [typeof(BoxView)] = () => new BoxViewHandler(),
        [typeof(Frame)] = () => new FrameHandler(),
        [typeof(Border)] = () => new BorderHandler(),
        [typeof(ContentView)] = () => new ContentViewHandler(),
        [typeof(ContentPresenter)] = () => new ContentPresenterHandler(),
        [typeof(TemplatedView)] = () => new TemplatedViewHandler(),
        [typeof(ScrollView)] = () => new ScrollViewHandler(),
        [typeof(Grid)] = () => new GridHandler(),
        [typeof(StackLayout)] = () => new StackLayoutHandler(),
        [typeof(VerticalStackLayout)] = () => new StackLayoutHandler(),
        [typeof(HorizontalStackLayout)] = () => new StackLayoutHandler(),
        [typeof(AbsoluteLayout)] = () => new AbsoluteLayoutHandler(),
        [typeof(FlexLayout)] = () => new FlexLayoutHandler(),
        [typeof(Layout)] = () => new CrossPlatformLayoutHandler(),
        [typeof(CollectionView)] = () => new CollectionViewHandler(),
        [typeof(ListView)] = () => new ListViewHandler(),
        [typeof(TableView)] = () => new TableViewHandler(),
        [typeof(Page)] = () => new PageHandler(),
        [typeof(ContentPage)] = () => new ContentPageHandler(),
        [typeof(NavigationPage)] = () => new NavigationPageHandler(),
        [typeof(Shell)] = () => new ShellHandler(),
        [typeof(FlyoutPage)] = () => new FlyoutPageHandler(),
        [typeof(TabbedPage)] = () => new TabbedPageHandler(),
        [typeof(Application)] = () => new ApplicationHandler(),
        [typeof(Microsoft.Maui.Controls.Window)] = () => new WindowHandler(),
        [typeof(GraphicsView)] = () => new GraphicsViewHandler(),
        [typeof(Path)] = () => new ShapePathHandler(),
        [typeof(Microsoft.Maui.Controls.Shapes.Rectangle)] = () => new RectangleHandler(),
        [typeof(Microsoft.Maui.Controls.Shapes.Ellipse)] = () => new EllipseHandler(),
        [typeof(Microsoft.Maui.Controls.Shapes.Line)] = () => new LineHandler(),
        [typeof(Microsoft.Maui.Controls.Shapes.Polygon)] = () => new PolygonHandler(),
        [typeof(Microsoft.Maui.Controls.Shapes.Polyline)] = () => new PolylineHandler(),
        [typeof(Microsoft.Maui.Controls.Shapes.RoundRectangle)] = () => new RoundRectangleHandler(),
        [typeof(CarouselView)] = () => new CarouselViewHandler(),
        [typeof(SwipeView)] = () => new SwipeViewHandler(),
        [typeof(SwipeItemView)] = () => new SwipeItemViewHandler(),
        [typeof(RefreshView)] = () => new RefreshViewHandler(),
        [typeof(IndicatorView)] = () => new IndicatorViewHandler(),
        [typeof(MenuBar)] = () => new MenuBarHandler(),
        [typeof(MenuFlyout)] = () => new MenuFlyoutHandler(),
        [typeof(Toolbar)] = () => new ToolbarHandler(),
        [typeof(SKCanvasView)] = () => new SKCanvasViewHandler(),
        [typeof(SKGLView)] = () => new SKGLViewHandler()
    };

    static MauiHandlerExtensions()
    {
        // Third-party controls whose generic-TFM handler has no platform view;
        // mapped only when the library is part of the app.
        if (DrawingViewHandler.ToolkitDrawingViewType is { } drawingView)
            LinuxHandlerMap[drawingView] = () => new DrawingViewHandler();
    }

    /// <summary>
    /// Creates an element handler for the given element.
    /// </summary>
    public static IElementHandler ToHandler(this IElement element, IMauiContext mauiContext)
    {
        return CreateHandler(element, mauiContext)!;
    }

    /// <summary>
    /// Creates a view handler for the given view.
    /// </summary>
    public static IViewHandler? ToViewHandler(this IView view, IMauiContext mauiContext)
    {
        var handler = CreateHandler((IElement)view, mauiContext);
        return handler as IViewHandler;
    }

    /// <summary>
    /// The handler type the platform's own map resolves for a control type, or
    /// null when the map has no entry (the MAUI handler factory is used then).
    /// Exposed so tests can assert the map and the DI registrations agree.
    /// </summary>
    public static Type? GetLinuxHandlerType(Type controlType)
    {
        if (LinuxHandlerMap.TryGetValue(controlType, out var factory))
            return factory().GetType();
        return null;
    }

    /// <summary>All control types the platform map covers.</summary>
    public static IEnumerable<Type> MappedControlTypes => LinuxHandlerMap.Keys;

    /// <summary>
    /// The handler an app or library registered for <paramref name="type"/> itself, when it is more
    /// specific than the one registered for the framework type the platform map matched
    /// (<paramref name="mappedBase"/>) and is written for Linux; otherwise null.
    /// </summary>
    private static IElementHandler? LibraryHandlerFor(Type type, Type mappedBase, IMauiContext mauiContext)
    {
        try
        {
            var registered = mauiContext.Handlers.GetHandlerType(type);
            if (registered == null || registered == mauiContext.Handlers.GetHandlerType(mappedBase) || !IsLinuxHandler(registered))
                return null;
            return mauiContext.Handlers.GetHandler(type);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("MauiHandlerExtensions", $"The handler registered for {type.Name} could not be created", ex);
            return null;
        }
    }

    /// <summary>True when <paramref name="handlerType"/> derives from an OpenMaui handler.</summary>
    internal static bool IsLinuxHandler(Type handlerType)
    {
        for (var t = handlerType; t != null; t = t.BaseType)
        {
            var definition = t.IsGenericType ? t.GetGenericTypeDefinition() : t;
            if (definition.Namespace?.StartsWith("Microsoft.Maui.Platform.Linux", StringComparison.Ordinal) == true
                && definition.Assembly == typeof(MauiHandlerExtensions).Assembly)
                return true;
        }
        return false;
    }

    private static IElementHandler? CreateHandler(IElement element, IMauiContext mauiContext)
    {
        Type type = element.GetType();
        IElementHandler? handler = null;

        // First, try exact type match
        if (LinuxHandlerMap.TryGetValue(type, out Func<IElementHandler>? factory))
        {
            handler = factory();
            DiagnosticLog.Debug("MauiHandlerExtensions", $"Using Linux handler for {type.Name}: {handler.GetType().Name}");
        }
        else
        {
            // Try to find a base type match
            Type? bestMatch = null;
            Func<IElementHandler>? bestFactory = null;

            foreach (var kvp in LinuxHandlerMap)
            {
                if (kvp.Key.IsAssignableFrom(type) && (bestMatch == null || bestMatch.IsAssignableFrom(kvp.Key)))
                {
                    bestMatch = kvp.Key;
                    bestFactory = kvp.Value;
                }
            }

            if (bestFactory != null)
            {
                // A library's own control (a subclass of a framework one) keeps the handler the
                // library registered for it, when that handler is written for Linux (derives from
                // an OpenMaui handler): MarketAlly.ViewEngine's WebView keeps its WebViewHandler,
                // a LinuxWebViewHandler. A handler on MAUI's platform-neutral base cannot draw
                // here and is still replaced by the platform's own.
                var registered = LibraryHandlerFor(type, bestMatch!, mauiContext);
                if (registered != null)
                {
                    handler = registered;
                    DiagnosticLog.Debug("MauiHandlerExtensions", $"Using the library's Linux handler for {type.Name}: {handler.GetType().Name}");
                }
                else
                {
                    handler = bestFactory();
                    DiagnosticLog.Debug("MauiHandlerExtensions", $"Using Linux handler (via base {bestMatch!.Name}) for {type.Name}: {handler.GetType().Name}");
                }
            }
        }

        // Fall back to MAUI's default handler
        if (handler == null)
        {
            handler = mauiContext.Handlers.GetHandler(type);
            // A library view that implements IContentView without deriving from ContentView
            // (Syncfusion's SfScheduler and charts, among others) resolves to MAUI's own
            // ContentViewHandler, whose platform view throws on plain net10.0. OpenMaui's
            // content handler lays such a view out through its ICrossPlatformLayout.
            if (handler?.GetType() == typeof(Microsoft.Maui.Handlers.ContentViewHandler))
                handler = new Microsoft.Maui.Platform.Linux.Handlers.ContentViewHandler();
            // Likewise a core IShapeView (MAUI's ShapeViewHandler draws nothing on plain net10.0).
            else if (handler?.GetType() == typeof(Microsoft.Maui.Handlers.ShapeViewHandler))
                handler = new Microsoft.Maui.Platform.Linux.Handlers.ShapeViewHandler();
            // And a core ISwipeView or swipe item view given MAUI's own (platform-neutral) handler.
            else if (handler?.GetType() == typeof(Microsoft.Maui.Handlers.SwipeViewHandler))
                handler = new CoreSwipeViewHandler();
            else if (handler?.GetType() == typeof(Microsoft.Maui.Handlers.SwipeItemViewHandler))
                handler = new SwipeItemViewHandler();
            // Likewise a core IRefreshView / IIndicatorView given MAUI's own handler.
            else if (handler?.GetType() == typeof(Microsoft.Maui.Handlers.RefreshViewHandler))
                handler = new CoreRefreshViewHandler();
            else if (handler?.GetType() == typeof(Microsoft.Maui.Handlers.IndicatorViewHandler))
                handler = new CoreIndicatorViewHandler();
            // MAUI's core PageHandler / NavigationViewHandler (no platform view on plain net10.0).
            else if (handler?.GetType() == typeof(Microsoft.Maui.Handlers.PageHandler))
                handler = new CorePageHandler();
            else if (handler?.GetType() == typeof(Microsoft.Maui.Handlers.NavigationViewHandler))
                handler = new CoreNavigationViewHandler();
            DiagnosticLog.Debug("MauiHandlerExtensions", $"Using MAUI handler for {type.Name}: {handler?.GetType().Name ?? "null"}");
        }

        if (handler != null)
        {
            handler.SetMauiContext(mauiContext);
            handler.SetVirtualView(element);

            // Set MauiView back-reference so layout views can read alignment
            // directly from the MAUI virtual view (authoritative source).
            if (element is View mauiView && handler is IViewHandler viewHandler &&
                viewHandler.PlatformView is SkiaView skiaView)
            {
                skiaView.MauiView = mauiView;
                VisualStateBridge.Attach(mauiView, skiaView);

                // Opacity, Visibility, InputTransparent, semantics and the other base view
                // properties reach the platform view through ViewHandler.ViewMapper
                // (LinuxViewMappers), as on the other platforms.
            }

            // NOTE: Loaded event is fired from SkiaView.Arrange after the
            // first successful layout, when the element has its final size.
        }

        return handler;
    }
}
