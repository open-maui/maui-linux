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

	// MAUI's Core SwipeView tests drive an ISwipeView stub: the Linux handler for any ISwipeView
	// is CoreSwipeViewHandler (Linux.SwipeViewHandler is the Controls SwipeView's, which the
	// Controls suite aliases).
	public class SwipeViewHandler : Linux.CoreSwipeViewHandler, IPlatformViewHandler
	{
		public SwipeViewHandler() { }
		public SwipeViewHandler(IPropertyMapper mapper) : base(mapper) { }
		public SwipeViewHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}

	// MAUI's core RefreshViewHandler / IndicatorViewHandler tests drive IRefreshView /
	// IIndicatorView stubs: on Linux those get the core handlers (any IRefreshView /
	// IIndicatorView), not the ones typed to the Controls views.
	public class RefreshViewHandler : Linux.CoreRefreshViewHandler, IPlatformViewHandler
	{
		public RefreshViewHandler() { }
		public RefreshViewHandler(IPropertyMapper mapper) : base(mapper) { }
		public RefreshViewHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}

	public class IndicatorViewHandler : Linux.CoreIndicatorViewHandler, IPlatformViewHandler
	{
		public IndicatorViewHandler() { }
		public IndicatorViewHandler(IPropertyMapper mapper) : base(mapper) { }
		public IndicatorViewHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}

	// MAUI's core PageHandler / NavigationViewHandler tests drive a core page (IContentView)
	// and a core IStackNavigationView: the Linux core handlers, not the Controls page ones.
	public class PageHandler : Linux.CorePageHandler, IPlatformViewHandler
	{
		public PageHandler() { }
		public PageHandler(IPropertyMapper mapper) : base(mapper) { }
		public PageHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}

	public class NavigationViewHandler : Linux.CoreNavigationViewHandler, IPlatformViewHandler
	{
		public NavigationViewHandler() { }
		public NavigationViewHandler(IPropertyMapper mapper) : base(mapper) { }
		public NavigationViewHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
	}

	// MAUI's window tests use WindowHandler's static map methods (MapRequestDisplayDensity).
	public class WindowHandler : Linux.WindowHandler
	{
		public WindowHandler() { }
		public WindowHandler(IPropertyMapper mapper, CommandMapper commandMapper) : base(mapper, commandMapper) { }
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
			Add<SwipeViewStub, ISwipeView, SwipeViewHandler>(handlers);
			Add<RefreshViewStub, IRefreshView, RefreshViewHandler>(handlers);
			Add<IndicatorViewStub, IIndicatorView, IndicatorViewHandler>(handlers);
			handlers.AddHandler(typeof(PageStub), typeof(PageHandler));
			Add<NavigationViewStub, IStackNavigationView, NavigationViewHandler>(handlers);
		}

		static void Add<TStub, TInterface, THandler>(IMauiHandlersCollection handlers)
			where THandler : IElementHandler
		{
			handlers.AddHandler(typeof(TStub), typeof(THandler));
			handlers.AddHandler(typeof(TInterface), typeof(THandler));
		}
	}
}
