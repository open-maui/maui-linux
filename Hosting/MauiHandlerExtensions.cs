using System;
using System.Collections.Generic;
using System.Reflection;
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
        [typeof(WebView)] = () => new GtkWebViewHandler(),
        [typeof(Image)] = () => new ImageHandler(),
        [typeof(ImageButton)] = () => new ImageButtonHandler(),
        [typeof(BoxView)] = () => new BoxViewHandler(),
        [typeof(Frame)] = () => new FrameHandler(),
        [typeof(Border)] = () => new BorderHandler(),
        [typeof(ContentView)] = () => new ContentViewHandler(),
        [typeof(ScrollView)] = () => new ScrollViewHandler(),
        [typeof(Grid)] = () => new GridHandler(),
        [typeof(StackLayout)] = () => new StackLayoutHandler(),
        [typeof(VerticalStackLayout)] = () => new StackLayoutHandler(),
        [typeof(HorizontalStackLayout)] = () => new StackLayoutHandler(),
        [typeof(AbsoluteLayout)] = () => new LayoutHandler(),
        [typeof(FlexLayout)] = () => new LayoutHandler(),
        [typeof(CollectionView)] = () => new CollectionViewHandler(),
        [typeof(ListView)] = () => new CollectionViewHandler(),
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
        [typeof(SKCanvasView)] = () => new SKCanvasViewHandler(),
        [typeof(SKGLView)] = () => new SKGLViewHandler()
    };

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
                handler = bestFactory();
                DiagnosticLog.Debug("MauiHandlerExtensions", $"Using Linux handler (via base {bestMatch!.Name}) for {type.Name}: {handler.GetType().Name}");
            }
        }

        // Fall back to MAUI's default handler
        if (handler == null)
        {
            handler = mauiContext.Handlers.GetHandler(type);
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

                // Sync visual properties from MAUI view to platform view,
                // and subscribe to future changes. MAUI's ViewMapper doesn't
                // call platform-specific mappers for these on Linux.
                skiaView.IsVisible = mauiView.IsVisible;
                skiaView.Opacity = (float)mauiView.Opacity;
                skiaView.InputTransparent = mauiView.InputTransparent;

                mauiView.PropertyChanged += (s, e) =>
                {
                    switch (e.PropertyName)
                    {
                        case nameof(View.IsVisible):
                            skiaView.IsVisible = mauiView.IsVisible;
                            break;
                        case nameof(View.Opacity):
                            skiaView.Opacity = (float)mauiView.Opacity;
                            break;
                        case nameof(View.InputTransparent):
                            skiaView.InputTransparent = mauiView.InputTransparent;
                            break;
                    }
                };
            }

            // Fire VisualElement.Loaded event — MAUI platform handlers are
            // responsible for triggering this. Controls like LiveCharts'
            // MotionCanvas subscribe to PaintSurface in their Loaded handler.
            if (element is VisualElement visualElement && !visualElement.IsLoaded)
            {
                SendLoadedToElement(visualElement);
            }
        }

        return handler;
    }

    private static MethodInfo? _sendLoadedMethod;

    /// <summary>
    /// Calls the private VisualElement.SendLoaded() method via reflection.
    /// This fires the Loaded event, which is required for controls that
    /// set up event subscriptions in their Loaded handler.
    /// </summary>
    private static void SendLoadedToElement(VisualElement element)
    {
        try
        {
            _sendLoadedMethod ??= typeof(VisualElement).GetMethod(
                "SendLoaded",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);

            _sendLoadedMethod?.Invoke(element, null);
            DiagnosticLog.Error("MauiHandlerExtensions", $"SendLoaded: {element.GetType().Name}, IsLoaded={element.IsLoaded}, Window={element.Window?.GetType().Name ?? "null"}");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("MauiHandlerExtensions", $"SendLoaded failed for {element.GetType().Name}: {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}
