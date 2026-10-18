using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
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
        [typeof(HybridWebView)] = () => new LinuxHybridWebViewHandler(),
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
            LinuxHandlerMap[drawingView] = DrawingViewHandler.Create;
        if (SemanticOrderViewHandler.ToolkitSemanticOrderViewType is { } semanticOrderView)
            LinuxHandlerMap[semanticOrderView] = () => new SemanticOrderViewHandler();
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
    /// Handler types an app or library registered that cannot be used on Linux, with the reason:
    /// each is reported once and OpenMaui's own handler is used for its views from then on.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, string> s_unusableHandlers = new();

    /// <summary>The registered handler types OpenMaui could not use, with the reason (diagnostics and tests).</summary>
    internal static IReadOnlyDictionary<Type, string> UnusableHandlers => s_unusableHandlers;

    /// <summary>
    /// The handler the app (or a library) registered for <paramref name="type"/>, when it replaces
    /// OpenMaui's: MAUI resolves the registration by type (the type, else its most derived registered
    /// base class, else an interface; the last registration for a type wins), and on every MAUI platform
    /// the app's registration is the one used. Null when the registration is OpenMaui's own or MAUI's
    /// portable handler (which has no platform view here), or when the registered handler cannot be
    /// used: it must produce a <see cref="SkiaView"/> (an element that is not a view: the platform
    /// element OpenMaui's handler produces). Such a handler is reported once through
    /// <see cref="DiagnosticLog"/> and OpenMaui's handler is used instead. The handler returned is
    /// connected to <paramref name="element"/>.
    /// </summary>
    private static IElementHandler? RegisteredHandlerFor(IElement element, Type type, Func<IElementHandler> platformFactory, IMauiContext mauiContext)
    {
        var factory = mauiContext.Handlers;
        if (factory == null)
            return null;

        Type? registered;
        try
        {
            registered = factory.GetHandlerType(type);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("MauiHandlerExtensions", $"No handler registration resolved for {type.Name}", ex);
            return null;
        }

        if (registered != null)
        {
            if (IsPlatformDefault(registered, type, platformFactory) || s_unusableHandlers.ContainsKey(registered))
                return null;
            if (PortableHandlerReason(registered) is { } portable)
            {
                ReportUnusable(registered, type, portable, error: false);
                return null;
            }
        }
        else if (!HasFactoryRegistration(factory, type))
        {
            return null;
        }

        IElementHandler? handler = null;
        try
        {
            handler = factory.GetHandler(type);
            if (handler == null)
                return null;
            var handlerType = handler.GetType();
            if (s_unusableHandlers.ContainsKey(handlerType))
                return null;
            if (registered == null && PortableHandlerReason(handlerType) is { } portable)
            {
                ReportUnusable(handlerType, type, portable, error: false);
                return null;
            }

            handler.SetMauiContext(mauiContext);
            handler.SetVirtualView(element);

            var problem = PlatformViewProblem(element, handler, platformFactory);
            if (problem == null)
                return handler;
            ReportUnusable(handlerType, type, problem, error: true);
        }
        catch (Exception ex)
        {
            var root = ex;
            while (root is System.Reflection.TargetInvocationException && root.InnerException != null)
                root = root.InnerException;
            var handlerType = handler?.GetType() ?? registered;
            if (handlerType != null)
                ReportUnusable(handlerType, type, $"it threw {root.GetType().Name}: {root.Message}", error: true);
        }

        Detach(element, handler);
        return null;
    }

    /// <summary>
    /// True when <paramref name="registered"/> is what OpenMaui itself would use for <paramref name="type"/>:
    /// MAUI's portable handler, the handler UseLinux registered for it, or the platform map's handler.
    /// </summary>
    private static bool IsPlatformDefault(Type registered, Type type, Func<IElementHandler> platformFactory)
    {
        if (LinuxHandlerDefaults.IsMauiHandler(registered) || LinuxHandlerDefaults.DefaultFor(type) == registered)
            return true;
        return s_mapHandlerTypes.GetOrAdd(platformFactory, f => f().GetType()) == registered;
    }

    private static readonly ConcurrentDictionary<Func<IElementHandler>, Type> s_mapHandlerTypes = new();

    /// <summary>
    /// True when the registration MAUI resolves for <paramref name="type"/> is a factory or instance
    /// the app or a library made (<c>AddHandler&lt;T&gt;(sp =&gt; ...)</c>), whose handler type is only
    /// known once it is made; MAUI's own factory registrations (MAUI 11) are its portable defaults.
    /// </summary>
    private static bool HasFactoryRegistration(IMauiHandlersFactory factory, Type type)
    {
        IMauiHandlersCollection? collection;
        try { collection = factory.GetCollection(); }
        catch (Exception) { return false; }
        if (collection == null)
            return false;

        var registrations = new Dictionary<Type, ServiceDescriptor>();
        foreach (var descriptor in collection)
        {
            if (typeof(IElement).IsAssignableFrom(descriptor.ServiceType) && descriptor.ServiceType.IsAssignableFrom(type))
                registrations[descriptor.ServiceType] = descriptor; // the last registration for a type wins
        }
        if (registrations.Count == 0)
            return false;

        Type? best = null;
        if (registrations.ContainsKey(type))
            best = type;
        else
        {
            foreach (var interfaces in new[] { false, true })
            {
                foreach (var key in registrations.Keys)
                    if (key.IsInterface == interfaces && (best == null || best.IsAssignableFrom(key)))
                        best = key;
                if (best != null)
                    break;
            }
        }
        return best != null && registrations[best].ImplementationType == null && !LinuxHandlerDefaults.IsMauiFactory(registrations[best]);
    }

    /// <summary>
    /// Why <paramref name="handlerType"/> is MAUI's portable kind of handler, which has no platform view on
    /// Linux (its platform view type is <c>object</c> and it never makes one: no CreatePlatformView, or one
    /// that only throws), or null. A handler written for Linux makes a Skia view.
    /// </summary>
    private static string? PortableHandlerReason(Type handlerType)
    {
        if (PlatformViewType(handlerType) != typeof(object))
            return null;
        for (var t = handlerType; t != null && !LinuxHandlerDefaults.IsMauiHandler(t); t = t.BaseType)
        {
            foreach (var name in new[] { "CreatePlatformView", "CreatePlatformElement" })
            {
                var method = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, Type.EmptyTypes);
                if (method != null && !OnlyThrows(method))
                    return null;
            }
        }
        return "it is a portable handler (platform view type object) that makes no platform view on Linux";
    }

    /// <summary>True when the method's body is a single <c>throw new X()</c>.</summary>
    private static bool OnlyThrows(MethodInfo method)
    {
        try
        {
            var il = method.GetMethodBody()?.GetILAsByteArray();
            if (il == null)
                return false;
            // [nop...] newobj <ctor token> throw
            int start = 0;
            while (start < il.Length && il[start] == 0x00)
                start++;
            return il.Length - start == 6 && il[start] == 0x73 && il[start + 5] == 0x7A;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The TPlatformView of a handler's ViewHandler/ElementHandler base, or null.</summary>
    private static Type? PlatformViewType(Type handlerType)
    {
        for (var t = handlerType; t != null; t = t.BaseType)
        {
            if (!t.IsGenericType)
                continue;
            var definition = t.GetGenericTypeDefinition();
            if (definition == typeof(Microsoft.Maui.Handlers.ViewHandler<,>) || definition == typeof(Microsoft.Maui.Handlers.ElementHandler<,>))
                return t.GetGenericArguments()[1];
        }
        return null;
    }

    /// <summary>
    /// Why the connected <paramref name="handler"/>'s platform view cannot be shown, or null: a view needs
    /// a <see cref="SkiaView"/>; any other element the kind of platform element OpenMaui's handler makes.
    /// </summary>
    private static string? PlatformViewProblem(IElement element, IElementHandler handler, Func<IElementHandler> platformFactory)
    {
        var platformView = handler.PlatformView;
        if (element is IView)
            return platformView is SkiaView
                ? null
                : $"its platform view is {platformView?.GetType().FullName ?? "null"}, not a {nameof(SkiaView)}";
        if (platformView == null)
            return "it made no platform element";
        var expected = PlatformViewType(s_mapHandlerTypes.GetOrAdd(platformFactory, f => f().GetType()));
        return expected == null || expected.IsInstanceOfType(platformView)
            ? null
            : $"its platform element is {platformView.GetType().FullName}, not a {expected.Name}";
    }

    private static void ReportUnusable(Type handlerType, Type elementType, string reason, bool error)
    {
        if (!s_unusableHandlers.TryAdd(handlerType, reason))
            return;
        var message = $"The handler registered for {elementType.FullName} ({handlerType.FullName}) cannot be used on Linux: {reason}. " +
            $"OpenMaui's own handler is used for it instead. A Linux handler makes a {nameof(SkiaView)} " +
            "(derive from OpenMaui's handler for the control, e.g. Microsoft.Maui.Platform.Linux.Handlers.LabelHandler, or customise it through its Mapper).";
        if (error)
            DiagnosticLog.Error("MauiHandlerExtensions", message);
        else
            DiagnosticLog.Warn("MauiHandlerExtensions", message);
    }

    /// <summary>Undoes a failed connection of <paramref name="handler"/> to <paramref name="element"/>.</summary>
    private static void Detach(IElement element, IElementHandler? handler)
    {
        if (handler == null)
            return;
        try
        {
            if (handler.VirtualView != null && handler.PlatformView != null)
                handler.DisconnectHandler();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("MauiHandlerExtensions", $"Disconnecting the unusable {handler.GetType().Name}", ex);
        }
        try
        {
            if (ReferenceEquals(element.Handler, handler))
                element.Handler = null;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("MauiHandlerExtensions", $"Clearing the unusable {handler.GetType().Name}", ex);
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

        // OpenMaui's handler for the type: an exact entry in the platform map, else the entry for
        // its most derived mapped base type.
        Func<IElementHandler>? platformFactory = null;
        Type? mappedAs = null;
        if (LinuxHandlerMap.TryGetValue(type, out Func<IElementHandler>? factory))
        {
            platformFactory = factory;
            mappedAs = type;
        }
        else
        {
            foreach (var kvp in LinuxHandlerMap)
            {
                if (kvp.Key.IsAssignableFrom(type) && (mappedAs == null || mappedAs.IsAssignableFrom(kvp.Key)))
                {
                    mappedAs = kvp.Key;
                    platformFactory = kvp.Value;
                }
            }
        }

        if (platformFactory != null)
        {
            // As on every MAUI platform, the handler the app or a library registered for the type
            // (ConfigureMauiHandlers(h => h.AddHandler<Label, MyLabelHandler>()), or one for a
            // library's own subclass: MarketAlly.ViewEngine's WebView keeps its WebViewHandler)
            // replaces OpenMaui's, wherever the view is: in a page, a Shell, a template or a list
            // row. MAUI's portable handlers (no platform view here) and OpenMaui's own defaults
            // keep the platform map's handler.
            handler = RegisteredHandlerFor(element, type, platformFactory, mauiContext);
            if (handler != null)
            {
                DiagnosticLog.Debug("MauiHandlerExtensions", $"Using the registered handler for {type.Name}: {handler.GetType().Name}");
            }
            else
            {
                handler = platformFactory();
                DiagnosticLog.Debug("MauiHandlerExtensions", mappedAs == type
                    ? $"Using Linux handler for {type.Name}: {handler.GetType().Name}"
                    : $"Using Linux handler (via base {mappedAs!.Name}) for {type.Name}: {handler.GetType().Name}");
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
