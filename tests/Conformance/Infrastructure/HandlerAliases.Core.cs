// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.DeviceTests.Stubs;
using Microsoft.Maui.Hosting;
using Linux = Microsoft.Maui.Platform.Linux.Handlers;

// Core-suite part of the handler aliases (HandlerAliases.cs holds the ones both
// suites share). MAUI's Core ButtonHandler tests drive a ButtonStub through the
// plain Linux ButtonHandler; the stub registrations below are the Core stubs'.
namespace Microsoft.Maui.DeviceTests
{
	public class ButtonHandler : Linux.ButtonHandler, IPlatformViewHandler
	{
		public ButtonHandler() { }
		public ButtonHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
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
			Add<ShapeViewStub, IShapeView, ShapeViewHandler>(handlers);
		}

		static void Add<TStub, TInterface, THandler>(IMauiHandlersCollection handlers)
			where THandler : IElementHandler
		{
			handlers.AddHandler(typeof(TStub), typeof(THandler));
			handlers.AddHandler(typeof(TInterface), typeof(THandler));
		}
	}
}
