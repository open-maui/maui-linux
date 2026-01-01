using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Handlers;

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
        [typeof(WebView)] = () => new WebViewHandler(),
        [typeof(Image)] = () => new ImageHandler(),
        [typeof(ImageButton)] = () => new ImageButtonHandler(),
        [typeof(BoxView)] = () => new BoxViewHandler(),
        [typeof(Frame)] = () => new FrameHandler(),
        [typeof(Border)] = () => new BorderHandler(),
        [typeof(ContentView)] = () => new BorderHandler(),
        [typeof(ScrollView)] = () => new ScrollViewHandler(),
        [typeof(Grid)] = () => new GridHandler(),
        [typeof(StackLayout)] = () => new StackLayoutHandler(),
        [typeof(VerticalStackLayout)] = () => new StackLayoutHandler(),
        [typeof(HorizontalStackLayout)] = () => new StackLayoutHandler(),
        [typeof(AbsoluteLayout)] = () => new LayoutHandler(),
        [typeof(FlexLayout)] = () => new FlexLayoutHandler(),
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
        [typeof(GraphicsView)] = () => new GraphicsViewHandler()
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
            Console.WriteLine($"[ToHandler] Using Linux handler for {type.Name}: {handler.GetType().Name}");
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
                Console.WriteLine($"[ToHandler] Using Linux handler (via base {bestMatch!.Name}) for {type.Name}: {handler.GetType().Name}");
            }
        }

        // Fall back to MAUI's default handler
        if (handler == null)
        {
            handler = mauiContext.Handlers.GetHandler(type);
            Console.WriteLine($"[ToHandler] Using MAUI handler for {type.Name}: {handler?.GetType().Name ?? "null"}");
        }

        if (handler != null)
        {
            handler.SetMauiContext(mauiContext);
            handler.SetVirtualView(element);
        }

        return handler;
    }
}
