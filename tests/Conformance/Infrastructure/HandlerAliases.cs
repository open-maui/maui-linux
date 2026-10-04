// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.DeviceTests.Stubs;
using Microsoft.Maui.Hosting;
using Linux = Microsoft.Maui.Platform.Linux.Handlers;

// MAUI's handler tests name the handler under test by its simple name
// (LabelHandler, ...) with `using Microsoft.Maui.Handlers;`. A type declared in
// the tests' own namespace (Microsoft.Maui.DeviceTests) wins name lookup over
// any using directive, so these empty subclasses make every reference in the
// MAUI files (CoreHandlerTestBase<LabelHandler, LabelStub>, LabelHandler.Mapper,
// GetNativeText(LabelHandler) ...) bind to the Linux handler. No behaviour is
// added: each alias is the Linux handler with its own mappers, and forwards
// exactly the constructors the Linux handler has.
namespace Microsoft.Maui.DeviceTests
{
	public class LabelHandler : Linux.LabelHandler, IPlatformViewHandler
	{
		public LabelHandler() { }
		public LabelHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class ButtonHandler : Linux.ButtonHandler, IPlatformViewHandler
	{
		public ButtonHandler() { }
		public ButtonHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	// MAUI's text-input tests build PropertyMapper<IEntry, IEntryHandler> and put
	// EntryHandler.MapKeyboard etc. (MAUI signature: (IEntryHandler, IEntry)) first.
	// The Linux handlers implement neither the I*Handler interfaces nor public
	// MAUI-signature Map* methods; the alias adds the interface and Map* methods
	// that run the Linux mapper's own entry for that property, so the test still
	// checks what it is about (mapping order), on the Linux mapping.
	public class EntryHandler : Linux.EntryHandler, IPlatformViewHandler, IEntryHandler
	{
		public EntryHandler() { }
		public EntryHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }

		IEntry IEntryHandler.VirtualView => VirtualView;
		object IEntryHandler.PlatformView => PlatformView;

		public static void MapKeyboard(IEntryHandler handler, IEntry entry) => LinuxMapping.Run(Linux.EntryHandler.Mapper, handler, entry, nameof(IEntry.Keyboard));
		public static void MapIsReadOnly(IEntryHandler handler, IEntry entry) => LinuxMapping.Run(Linux.EntryHandler.Mapper, handler, entry, nameof(IEntry.IsReadOnly));
		public static void MapIsPassword(IEntryHandler handler, IEntry entry) => LinuxMapping.Run(Linux.EntryHandler.Mapper, handler, entry, nameof(IEntry.IsPassword));
	}
	public class EditorHandler : Linux.EditorHandler, IPlatformViewHandler, IEditorHandler
	{
		public EditorHandler() { }
		public EditorHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }

		IEditor IEditorHandler.VirtualView => VirtualView;
		object IEditorHandler.PlatformView => PlatformView;

		public static void MapKeyboard(IEditorHandler handler, IEditor editor) => LinuxMapping.Run(Linux.EditorHandler.Mapper, handler, editor, nameof(IEditor.Keyboard));
	}
	static class LinuxMapping
	{
		public static void Run(IPropertyMapper mapper, IElementHandler handler, IElement view, string property) =>
			mapper.UpdateProperty(handler, view, property);
	}
	public class CheckBoxHandler : Linux.CheckBoxHandler, IPlatformViewHandler
	{
		public CheckBoxHandler() { }
		public CheckBoxHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class SwitchHandler : Linux.SwitchHandler, IPlatformViewHandler
	{
		public SwitchHandler() { }
		public SwitchHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class SliderHandler : Linux.SliderHandler, IPlatformViewHandler
	{
		public SliderHandler() { }
		public SliderHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class StepperHandler : Linux.StepperHandler, IPlatformViewHandler
	{
		public StepperHandler() { }
		public StepperHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class ProgressBarHandler : Linux.ProgressBarHandler, IPlatformViewHandler
	{
		public ProgressBarHandler() { }
		public ProgressBarHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class ActivityIndicatorHandler : Linux.ActivityIndicatorHandler, IPlatformViewHandler
	{
		public ActivityIndicatorHandler() { }
		public ActivityIndicatorHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	// MAUI's ImageHandlerTests<TImageHandler, ...> require IImageHandler, which the
	// Linux image handlers do not implement (they load through a private
	// ImageSourceServiceResultManager, not MAUI's ImageSourcePartLoader). The alias
	// implements the interface by forwarding VirtualView/PlatformView; SourceLoader
	// has no Linux counterpart and throws, so a test that needs it fails visibly.
	public class ImageHandler : Linux.ImageHandler, IPlatformViewHandler, IImageHandler
	{
		public ImageHandler() { }
		public ImageHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }

		IImage IImageHandler.VirtualView => VirtualView;
		object IImageHandler.PlatformView => PlatformView;
		ImageSourcePartLoader IImageHandler.SourceLoader => throw ImageAliasGap.NoSourceLoader();
	}
	public class ImageButtonHandler : Linux.ImageButtonHandler, IPlatformViewHandler, IImageHandler
	{
		public ImageButtonHandler() { }
		public ImageButtonHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }

		IImage IImageHandler.VirtualView => VirtualView;
		object IImageHandler.PlatformView => PlatformView;
		ImageSourcePartLoader IImageHandler.SourceLoader => throw ImageAliasGap.NoSourceLoader();
	}
	static class ImageAliasGap
	{
		public static System.Exception NoSourceLoader() => new System.NotSupportedException(
			"Linux image handlers do not implement IImageHandler.SourceLoader (MAUI's ImageSourcePartLoader).");
	}
	public class DatePickerHandler : Linux.DatePickerHandler, IPlatformViewHandler
	{
		public DatePickerHandler() { }
		public DatePickerHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class TimePickerHandler : Linux.TimePickerHandler, IPlatformViewHandler
	{
		public TimePickerHandler() { }
		public TimePickerHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class PickerHandler : Linux.PickerHandler, IPlatformViewHandler
	{
		public PickerHandler() { }
		public PickerHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class SearchBarHandler : Linux.SearchBarHandler, IPlatformViewHandler, ISearchBarHandler
	{
		public SearchBarHandler() { }
		public SearchBarHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }

		ISearchBar ISearchBarHandler.VirtualView => VirtualView;
		object ISearchBarHandler.PlatformView => PlatformView;
		// MAUI exposes the search field as an editor (QueryEditor); the Skia search bar
		// draws its own field and has no child editor view.
		object ISearchBarHandler.QueryEditor => null;

		public static void MapKeyboard(ISearchBarHandler handler, ISearchBar searchBar) => LinuxMapping.Run(Linux.SearchBarHandler.Mapper, handler, searchBar, nameof(ISearchBar.Keyboard));
	}
	public class RadioButtonHandler : Linux.RadioButtonHandler, IPlatformViewHandler
	{
		public RadioButtonHandler() { }
		public RadioButtonHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class BorderHandler : Linux.BorderHandler, IPlatformViewHandler
	{
		public BorderHandler() { }
		public BorderHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class ScrollViewHandler : Linux.ScrollViewHandler, IPlatformViewHandler
	{
		public ScrollViewHandler() { }
		public ScrollViewHandler(IPropertyMapper mapper) : base(mapper) { }
		public ScrollViewHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	// MAUI's LayoutHandler implements ILayoutHandler (Add/Insert/Remove/Clear/Update/
	// UpdateZIndex as methods); the Linux one only handles the same operations as
	// commands (handler.Invoke("Add", LayoutHandlerUpdate)), which is how
	// Controls.Layout drives it. The alias implements ILayoutHandler by sending
	// exactly the command Controls.Layout would send for each call.
	public class LayoutHandler : Linux.LayoutHandler, IPlatformViewHandler, ILayoutHandler
	{
		public LayoutHandler() { }
		public LayoutHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }

		ILayout ILayoutHandler.VirtualView => VirtualView;
		object ILayoutHandler.PlatformView => PlatformView;

		public void Add(IView view) =>
			Invoke(nameof(ILayoutHandler.Add), new LayoutHandlerUpdate(VirtualView.IndexOf(view), view));

		public void Insert(int index, IView view) =>
			Invoke(nameof(ILayoutHandler.Insert), new LayoutHandlerUpdate(index, view));

		public void Update(int index, IView view) =>
			Invoke(nameof(ILayoutHandler.Update), new LayoutHandlerUpdate(index, view));

		// Controls.Layout sends the index the child had before it was removed;
		// the stub layout has already dropped it, so recover it from the
		// platform children (which mirror the layout's order).
		public void Remove(IView view)
		{
			var platform = view.Handler?.PlatformView as Microsoft.Maui.Platform.SkiaView;
			var index = platform is null ? -1 : System.Linq.Enumerable.ToList(PlatformView.Children).IndexOf(platform);
			Invoke(nameof(ILayoutHandler.Remove), new LayoutHandlerUpdate(index, view));
		}

		public void Clear() => Invoke(nameof(ILayoutHandler.Clear), null);

		public void UpdateZIndex(IView view) => Invoke(nameof(ILayoutHandler.UpdateZIndex), view);
	}
	public class GraphicsViewHandler : Linux.GraphicsViewHandler, IPlatformViewHandler
	{
		public GraphicsViewHandler() { }
		public GraphicsViewHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class ContentViewHandler : Linux.ContentViewHandler, IPlatformViewHandler
	{
		public ContentViewHandler() { }
		public ContentViewHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}

	public static class HandlerAliases
	{
		/// <summary>
		/// Stub (and core interface) to Linux handler registrations, so views
		/// realized through the handler factory (layout children, border
		/// content, AttachAndRun) get the same handlers as the view under test.
		/// </summary>
		public static void Register(IMauiHandlersCollection handlers)
		{
			Add<LabelStub, ILabel, LabelHandler>(handlers);
			Add<ButtonStub, IButton, ButtonHandler>(handlers);
			Add<EntryStub, IEntry, EntryHandler>(handlers);
			Add<EditorStub, IEditor, EditorHandler>(handlers);
			Add<CheckBoxStub, ICheckBox, CheckBoxHandler>(handlers);
			Add<SwitchStub, ISwitch, SwitchHandler>(handlers);
			Add<SliderStub, ISlider, SliderHandler>(handlers);
			Add<StepperStub, IStepper, StepperHandler>(handlers);
			Add<ProgressBarStub, IProgress, ProgressBarHandler>(handlers);
			Add<ActivityIndicatorStub, IActivityIndicator, ActivityIndicatorHandler>(handlers);
			Add<ImageStub, IImage, ImageHandler>(handlers);
			Add<ImageButtonStub, IImageButton, ImageButtonHandler>(handlers);
			Add<DatePickerStub, IDatePicker, DatePickerHandler>(handlers);
			Add<TimePickerStub, ITimePicker, TimePickerHandler>(handlers);
			Add<PickerStub, IPicker, PickerHandler>(handlers);
			Add<SearchBarStub, ISearchBar, SearchBarHandler>(handlers);
			Add<RadioButtonStub, IRadioButton, RadioButtonHandler>(handlers);
			Add<BorderStub, IBorderView, BorderHandler>(handlers);
			Add<ScrollViewStub, IScrollView, ScrollViewHandler>(handlers);
			Add<LayoutStub, ILayout, LayoutHandler>(handlers);
			Add<ContentViewStub, IContentView, ContentViewHandler>(handlers);
			Add<GraphicsViewStub, IGraphicsView, GraphicsViewHandler>(handlers);
		}

		static void Add<TStub, TInterface, THandler>(IMauiHandlersCollection handlers)
			where THandler : IElementHandler
		{
			handlers.AddHandler(typeof(TStub), typeof(THandler));
			handlers.AddHandler(typeof(TInterface), typeof(THandler));
		}
	}
}
