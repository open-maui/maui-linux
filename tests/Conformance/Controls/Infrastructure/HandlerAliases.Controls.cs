// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Controls suite: MAUI's handler names ARE OpenMaui's handlers.
//
// The Core suite declares subclasses of the Linux handlers under MAUI's names
// (../../Infrastructure/HandlerAliases.cs); that cannot work for Controls
// views in a window. OpenMaui realizes a page's views itself
// (MauiHandlerExtensions.ToHandler: a fixed Controls-type -> Linux-handler map
// that does not consult the app's handler registrations for the built-in
// types), so a Label inside a hosted page always gets
// Microsoft.Maui.Platform.Linux.Handlers.LabelHandler, never a subclass a test
// registered, and MAUI's `CreateHandlerAndAddToWindow<LabelHandler>` would not
// find its handler. Aliasing the names to the exact Linux types makes MAUI's
// registrations (handlers.AddHandler<Label, LabelHandler>()), its casts and its
// generic constraints all mean "the handler an OpenMaui app gets".
//
// A global using alias outranks the `using Microsoft.Maui.Handlers;` imports in
// MAUI's files (aliases win over namespace imports at the same level).
//
// MAUI handler names with no Linux handler (MenuBarItemHandler,
// ...) are deliberately NOT aliased: they bind to MAUI's
// platform-neutral handler, which has no platform view, so a test that needs
// one fails visibly instead of passing on something OpenMaui does not have.

// MAUI's IPlatformViewHandler exists only on the platform TFMs. Its meaning in
// the tests ("a view handler whose PlatformView is the native view") is every
// OpenMaui view handler, so it is IViewHandler here.
global using IPlatformViewHandler = Microsoft.Maui.IViewHandler;

global using ActivityIndicatorHandler = Microsoft.Maui.Platform.Linux.Handlers.ActivityIndicatorHandler;
global using ApplicationHandler = Microsoft.Maui.Platform.Linux.Handlers.ApplicationHandler;
global using BorderHandler = Microsoft.Maui.Platform.Linux.Handlers.BorderHandler;
global using BoxViewHandler = Microsoft.Maui.Platform.Linux.Handlers.BoxViewHandler;
// An app's Controls Button gets TextButtonHandler (UseLinux and the platform map).
global using ButtonHandler = Microsoft.Maui.Platform.Linux.Handlers.TextButtonHandler;
global using CarouselViewHandler = Microsoft.Maui.Platform.Linux.Handlers.CarouselViewHandler;
global using CheckBoxHandler = Microsoft.Maui.Platform.Linux.Handlers.CheckBoxHandler;
global using CollectionViewHandler = Microsoft.Maui.Platform.Linux.Handlers.CollectionViewHandler;
global using ContentViewHandler = Microsoft.Maui.Platform.Linux.Handlers.ContentViewHandler;
global using DatePickerHandler = Microsoft.Maui.Platform.Linux.Handlers.DatePickerHandler;
global using EditorHandler = Microsoft.Maui.Platform.Linux.Handlers.EditorHandler;
global using EllipseHandler = Microsoft.Maui.Platform.Linux.Handlers.EllipseHandler;
global using EntryHandler = Microsoft.Maui.Platform.Linux.Handlers.EntryHandler;
// MAUI's FlyoutPage handler is FlyoutViewHandler.
global using FlyoutViewHandler = Microsoft.Maui.Platform.Linux.Handlers.FlyoutPageHandler;
global using FrameHandler = Microsoft.Maui.Platform.Linux.Handlers.FrameHandler;
global using GraphicsViewHandler = Microsoft.Maui.Platform.Linux.Handlers.GraphicsViewHandler;
global using ImageButtonHandler = Microsoft.Maui.Platform.Linux.Handlers.ImageButtonHandler;
global using ImageHandler = Microsoft.Maui.Platform.Linux.Handlers.ImageHandler;
global using IndicatorViewHandler = Microsoft.Maui.Platform.Linux.Handlers.IndicatorViewHandler;
global using LabelHandler = Microsoft.Maui.Platform.Linux.Handlers.LabelHandler;
global using LayoutHandler = Microsoft.Maui.Platform.Linux.Handlers.LayoutHandler;
global using LineHandler = Microsoft.Maui.Platform.Linux.Handlers.LineHandler;
global using MenuBarHandler = Microsoft.Maui.Platform.Linux.Handlers.MenuBarHandler;
global using MenuFlyoutHandler = Microsoft.Maui.Platform.Linux.Handlers.MenuFlyoutHandler;
// MAUI's NavigationPage handler is NavigationViewHandler.
global using NavigationViewHandler = Microsoft.Maui.Platform.Linux.Handlers.NavigationPageHandler;
// MAUI's PageHandler is the ContentPage handler (registered for ContentPage by
// MAUI's ConfigureTestBuilder); OpenMaui's is ContentPageHandler.
global using PageHandler = Microsoft.Maui.Platform.Linux.Handlers.ContentPageHandler;
global using PathHandler = Microsoft.Maui.Platform.Linux.Handlers.ShapePathHandler;
global using PickerHandler = Microsoft.Maui.Platform.Linux.Handlers.PickerHandler;
global using PolygonHandler = Microsoft.Maui.Platform.Linux.Handlers.PolygonHandler;
global using PolylineHandler = Microsoft.Maui.Platform.Linux.Handlers.PolylineHandler;
global using ProgressBarHandler = Microsoft.Maui.Platform.Linux.Handlers.ProgressBarHandler;
global using RadioButtonHandler = Microsoft.Maui.Platform.Linux.Handlers.RadioButtonHandler;
global using RectangleHandler = Microsoft.Maui.Platform.Linux.Handlers.RectangleHandler;
global using RefreshViewHandler = Microsoft.Maui.Platform.Linux.Handlers.RefreshViewHandler;
global using RoundRectangleHandler = Microsoft.Maui.Platform.Linux.Handlers.RoundRectangleHandler;
global using ScrollViewHandler = Microsoft.Maui.Platform.Linux.Handlers.ScrollViewHandler;
global using SearchBarHandler = Microsoft.Maui.Platform.Linux.Handlers.SearchBarHandler;
global using ShapeViewHandler = Microsoft.Maui.Platform.Linux.Handlers.ShapeViewHandler;
global using ShellHandler = Microsoft.Maui.Platform.Linux.Handlers.ShellHandler;
global using SliderHandler = Microsoft.Maui.Platform.Linux.Handlers.SliderHandler;
global using StepperHandler = Microsoft.Maui.Platform.Linux.Handlers.StepperHandler;
global using SwipeViewHandler = Microsoft.Maui.Platform.Linux.Handlers.SwipeViewHandler;
global using SwitchHandler = Microsoft.Maui.Platform.Linux.Handlers.SwitchHandler;
// MAUI's TabbedPage handler is TabbedViewHandler.
global using TabbedViewHandler = Microsoft.Maui.Platform.Linux.Handlers.TabbedPageHandler;
global using TemplatedViewHandler = Microsoft.Maui.Platform.Linux.Handlers.TemplatedViewHandler;
global using TimePickerHandler = Microsoft.Maui.Platform.Linux.Handlers.TimePickerHandler;
global using ToolbarHandler = Microsoft.Maui.Platform.Linux.Handlers.ToolbarHandler;
global using WindowHandler = Microsoft.Maui.Platform.Linux.Handlers.WindowHandler;
// MAUI's per-platform WindowHandlerStub (Stubs/WindowHandlerStub.*.cs) is the
// platform's WindowHandler.
global using WindowHandlerStub = Microsoft.Maui.Platform.Linux.Handlers.WindowHandler;

// WebView: an app's WebView gets LinuxWebViewHandler (UseLinux).
global using WebViewHandler = Microsoft.Maui.Platform.Linux.Handlers.LinuxWebViewHandler;

// MAUI's compatibility renderers (Microsoft.Maui.Controls.Handlers.Compatibility,
// platform TFMs only), registered by the shared test-case lists for Frame,
// ListView and TableView: on Linux the handler for each of those elements.
global using FrameRenderer = Microsoft.Maui.Platform.Linux.Handlers.FrameHandler;
global using ListViewRenderer = Microsoft.Maui.Platform.Linux.Handlers.ListViewHandler;
global using TableViewRenderer = Microsoft.Maui.Platform.Linux.Handlers.TableViewHandler;
global using NavigationRenderer = Microsoft.Maui.Platform.Linux.Handlers.NavigationPageHandler;
global using TabbedRenderer = Microsoft.Maui.Platform.Linux.Handlers.TabbedPageHandler;
global using PhoneFlyoutPageRenderer = Microsoft.Maui.Platform.Linux.Handlers.FlyoutPageHandler;
