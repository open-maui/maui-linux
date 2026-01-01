using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Handlers;

namespace Microsoft.Maui.Platform.Linux.Hosting;

public static class MauiHandlerExtensions
{
	private static readonly Dictionary<Type, Func<IElementHandler>> LinuxHandlerMap = new Dictionary<Type, Func<IElementHandler>>
	{
		[typeof(Button)] = () => (IElementHandler)(object)new TextButtonHandler(),
		[typeof(Label)] = () => (IElementHandler)(object)new LabelHandler(),
		[typeof(Entry)] = () => (IElementHandler)(object)new EntryHandler(),
		[typeof(Editor)] = () => (IElementHandler)(object)new EditorHandler(),
		[typeof(CheckBox)] = () => (IElementHandler)(object)new CheckBoxHandler(),
		[typeof(Switch)] = () => (IElementHandler)(object)new SwitchHandler(),
		[typeof(Slider)] = () => (IElementHandler)(object)new SliderHandler(),
		[typeof(Stepper)] = () => (IElementHandler)(object)new StepperHandler(),
		[typeof(ProgressBar)] = () => (IElementHandler)(object)new ProgressBarHandler(),
		[typeof(ActivityIndicator)] = () => (IElementHandler)(object)new ActivityIndicatorHandler(),
		[typeof(Picker)] = () => (IElementHandler)(object)new PickerHandler(),
		[typeof(DatePicker)] = () => (IElementHandler)(object)new DatePickerHandler(),
		[typeof(TimePicker)] = () => (IElementHandler)(object)new TimePickerHandler(),
		[typeof(SearchBar)] = () => (IElementHandler)(object)new SearchBarHandler(),
		[typeof(RadioButton)] = () => (IElementHandler)(object)new RadioButtonHandler(),
		[typeof(WebView)] = () => (IElementHandler)(object)new GtkWebViewHandler(),
		[typeof(Image)] = () => (IElementHandler)(object)new ImageHandler(),
		[typeof(ImageButton)] = () => (IElementHandler)(object)new ImageButtonHandler(),
		[typeof(BoxView)] = () => (IElementHandler)(object)new BoxViewHandler(),
		[typeof(Frame)] = () => (IElementHandler)(object)new FrameHandler(),
		[typeof(Border)] = () => (IElementHandler)(object)new BorderHandler(),
		[typeof(ContentView)] = () => (IElementHandler)(object)new BorderHandler(),
		[typeof(ScrollView)] = () => (IElementHandler)(object)new ScrollViewHandler(),
		[typeof(Grid)] = () => (IElementHandler)(object)new GridHandler(),
		[typeof(StackLayout)] = () => (IElementHandler)(object)new StackLayoutHandler(),
		[typeof(VerticalStackLayout)] = () => (IElementHandler)(object)new StackLayoutHandler(),
		[typeof(HorizontalStackLayout)] = () => (IElementHandler)(object)new StackLayoutHandler(),
		[typeof(AbsoluteLayout)] = () => (IElementHandler)(object)new LayoutHandler(),
		[typeof(FlexLayout)] = () => (IElementHandler)(object)new LayoutHandler(),
		[typeof(CollectionView)] = () => (IElementHandler)(object)new CollectionViewHandler(),
		[typeof(ListView)] = () => (IElementHandler)(object)new CollectionViewHandler(),
		[typeof(Page)] = () => (IElementHandler)(object)new PageHandler(),
		[typeof(ContentPage)] = () => (IElementHandler)(object)new ContentPageHandler(),
		[typeof(NavigationPage)] = () => (IElementHandler)(object)new NavigationPageHandler(),
		[typeof(Shell)] = () => (IElementHandler)(object)new ShellHandler(),
		[typeof(FlyoutPage)] = () => (IElementHandler)(object)new FlyoutPageHandler(),
		[typeof(TabbedPage)] = () => (IElementHandler)(object)new TabbedPageHandler(),
		[typeof(Application)] = () => (IElementHandler)(object)new ApplicationHandler(),
		[typeof(Window)] = () => (IElementHandler)(object)new WindowHandler(),
		[typeof(GraphicsView)] = () => (IElementHandler)(object)new GraphicsViewHandler()
	};

	public static IElementHandler ToHandler(this IElement element, IMauiContext mauiContext)
	{
		return CreateHandler(element, mauiContext);
	}

	public static IViewHandler? ToViewHandler(this IView view, IMauiContext mauiContext)
	{
		IElementHandler? obj = CreateHandler((IElement)(object)view, mauiContext);
		return (IViewHandler?)(object)((obj is IViewHandler) ? obj : null);
	}

	private static IElementHandler? CreateHandler(IElement element, IMauiContext mauiContext)
	{
		Type type = ((object)element).GetType();
		IElementHandler val = null;
		if (LinuxHandlerMap.TryGetValue(type, out Func<IElementHandler> value))
		{
			val = value();
			Console.WriteLine("[ToHandler] Using Linux handler for " + type.Name + ": " + ((object)val).GetType().Name);
		}
		else
		{
			Type type2 = null;
			Func<IElementHandler> func = null;
			foreach (KeyValuePair<Type, Func<IElementHandler>> item in LinuxHandlerMap)
			{
				if (item.Key.IsAssignableFrom(type) && (type2 == null || type2.IsAssignableFrom(item.Key)))
				{
					type2 = item.Key;
					func = item.Value;
				}
			}
			if (func != null)
			{
				val = func();
				Console.WriteLine($"[ToHandler] Using Linux handler (via base {type2.Name}) for {type.Name}: {((object)val).GetType().Name}");
			}
		}
		if (val == null)
		{
			val = mauiContext.Handlers.GetHandler(type);
			Console.WriteLine("[ToHandler] Using MAUI handler for " + type.Name + ": " + (((object)val)?.GetType().Name ?? "null"));
		}
		if (val != null)
		{
			val.SetMauiContext(mauiContext);
			val.SetVirtualView(element);
		}
		return val;
	}
}
