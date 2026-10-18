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
	// MAUI's text-input tests build PropertyMapper<IEntry, IEntryHandler> and put
	// EntryHandler.MapKeyboard etc. first: the Linux handlers implement IEntryHandler /
	// IEditorHandler and have MAUI's interface-typed Map* methods.
	public class EntryHandler : Linux.EntryHandler, IPlatformViewHandler
	{
		public EntryHandler() { }
		public EntryHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class EditorHandler : Linux.EditorHandler, IPlatformViewHandler
	{
		public EditorHandler() { }
		public EditorHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
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
	// MAUI's ImageHandlerTests<TImageHandler, ...> require IImageHandler, which the Linux
	// image handlers implement (SourceLoader loads through the Linux image pipeline).
	public class ImageHandler : Linux.ImageHandler, IPlatformViewHandler
	{
		public ImageHandler() { }
		public ImageHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class ImageButtonHandler : Linux.ImageButtonHandler, IPlatformViewHandler
	{
		public ImageButtonHandler() { }
		public ImageButtonHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
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
	// ISearchBarHandler.QueryEditor is null: the Skia search bar draws its own field.
	public class SearchBarHandler : Linux.SearchBarHandler, IPlatformViewHandler
	{
		public SearchBarHandler() { }
		public SearchBarHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
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
	// The Linux LayoutHandler implements ILayoutHandler (Add/Insert/Remove/Clear/Update/
	// UpdateZIndex), as MAUI's does.
	public class LayoutHandler : Linux.LayoutHandler, IPlatformViewHandler
	{
		public LayoutHandler() { }
		public LayoutHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class GraphicsViewHandler : Linux.GraphicsViewHandler, IPlatformViewHandler
	{
		public GraphicsViewHandler() { }
		public GraphicsViewHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class ShapeViewHandler : Linux.ShapeViewHandler, IPlatformViewHandler
	{
		public ShapeViewHandler() { }
		public ShapeViewHandler(IPropertyMapper mapper) : base(mapper) { }
		public ShapeViewHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
	public class ContentViewHandler : Linux.ContentViewHandler, IPlatformViewHandler
	{
		public ContentViewHandler() { }
		public ContentViewHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}
}
