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

            // Sync layout alignment from virtual view to platform view.
            // Most handlers don't map HorizontalLayoutAlignment/VerticalLayoutAlignment,
            // so the SkiaView defaults to Fill. This ensures all views respect alignment.
            if (element is IView view && handler is IViewHandler viewHandler &&
                viewHandler.PlatformView is SkiaView skiaView)
            {
                skiaView.HorizontalOptions = view.HorizontalLayoutAlignment switch
                {
                    Primitives.LayoutAlignment.Start => LayoutOptions.Start,
                    Primitives.LayoutAlignment.Center => LayoutOptions.Center,
                    Primitives.LayoutAlignment.End => LayoutOptions.End,
                    _ => LayoutOptions.Fill,
                };
                skiaView.VerticalOptions = view.VerticalLayoutAlignment switch
                {
                    Primitives.LayoutAlignment.Start => LayoutOptions.Start,
                    Primitives.LayoutAlignment.Center => LayoutOptions.Center,
                    Primitives.LayoutAlignment.End => LayoutOptions.End,
                    _ => LayoutOptions.Fill,
                };
            }
        }

        return handler;
    }
}
