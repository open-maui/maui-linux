using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Platform.Linux.Converters;
using Microsoft.Maui.Platform.Linux.Dispatching;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Storage;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Hosting;

public static class LinuxMauiAppBuilderExtensions
{
	public static MauiAppBuilder UseLinux(this MauiAppBuilder builder)
	{
		return builder.UseLinux(null);
	}

	public static MauiAppBuilder UseLinux(this MauiAppBuilder builder, Action<LinuxApplicationOptions>? configure)
	{
		LinuxApplicationOptions linuxApplicationOptions = new LinuxApplicationOptions();
		configure?.Invoke(linuxApplicationOptions);
		builder.Services.TryAddSingleton<IDispatcherProvider>((IDispatcherProvider)(object)LinuxDispatcherProvider.Instance);
		builder.Services.TryAddSingleton<IDeviceInfo>((IDeviceInfo)(object)DeviceInfoService.Instance);
		builder.Services.TryAddSingleton<IDeviceDisplay>((IDeviceDisplay)(object)DeviceDisplayService.Instance);
		builder.Services.TryAddSingleton<IAppInfo>((IAppInfo)(object)AppInfoService.Instance);
		builder.Services.TryAddSingleton<IConnectivity>((IConnectivity)(object)ConnectivityService.Instance);
		builder.Services.TryAddSingleton<ILauncher, LauncherService>();
		builder.Services.TryAddSingleton<IPreferences, PreferencesService>();
		builder.Services.TryAddSingleton<IFilePicker, FilePickerService>();
		builder.Services.TryAddSingleton<IClipboard, ClipboardService>();
		builder.Services.TryAddSingleton<IShare, ShareService>();
		builder.Services.TryAddSingleton<ISecureStorage, SecureStorageService>();
		builder.Services.TryAddSingleton<IVersionTracking, VersionTrackingService>();
		builder.Services.TryAddSingleton<IAppActions, AppActionsService>();
		builder.Services.TryAddSingleton<IBrowser, BrowserService>();
		builder.Services.TryAddSingleton<IEmail, EmailService>();
		builder.Services.TryAddSingleton((IServiceProvider _) => GtkHostService.Instance);
		RegisterTypeConverters();
		HandlerMauiAppBuilderExtensions.ConfigureMauiHandlers(builder, (Action<IMauiHandlersCollection>)delegate(IMauiHandlersCollection handlers)
		{
			handlers.AddHandler<IApplication, ApplicationHandler>();
			handlers.AddHandler<BoxView, BoxViewHandler>();
			handlers.AddHandler<Button, TextButtonHandler>();
			handlers.AddHandler<Label, LabelHandler>();
			handlers.AddHandler<Entry, EntryHandler>();
			handlers.AddHandler<Editor, EditorHandler>();
			handlers.AddHandler<CheckBox, CheckBoxHandler>();
			handlers.AddHandler<Switch, SwitchHandler>();
			handlers.AddHandler<Slider, SliderHandler>();
			handlers.AddHandler<Stepper, StepperHandler>();
			handlers.AddHandler<RadioButton, RadioButtonHandler>();
			handlers.AddHandler<Grid, GridHandler>();
			handlers.AddHandler<StackLayout, StackLayoutHandler>();
			handlers.AddHandler<VerticalStackLayout, StackLayoutHandler>();
			handlers.AddHandler<HorizontalStackLayout, StackLayoutHandler>();
			handlers.AddHandler<AbsoluteLayout, LayoutHandler>();
			handlers.AddHandler<FlexLayout, LayoutHandler>();
			handlers.AddHandler<ScrollView, ScrollViewHandler>();
			handlers.AddHandler<Frame, FrameHandler>();
			handlers.AddHandler<Border, BorderHandler>();
			handlers.AddHandler<ContentView, BorderHandler>();
			handlers.AddHandler<Picker, PickerHandler>();
			handlers.AddHandler<DatePicker, DatePickerHandler>();
			handlers.AddHandler<TimePicker, TimePickerHandler>();
			handlers.AddHandler<SearchBar, SearchBarHandler>();
			handlers.AddHandler<ProgressBar, ProgressBarHandler>();
			handlers.AddHandler<ActivityIndicator, ActivityIndicatorHandler>();
			handlers.AddHandler<Image, ImageHandler>();
			handlers.AddHandler<ImageButton, ImageButtonHandler>();
			handlers.AddHandler<GraphicsView, GraphicsViewHandler>();
			handlers.AddHandler<WebView, GtkWebViewHandler>();
			handlers.AddHandler<CollectionView, CollectionViewHandler>();
			handlers.AddHandler<ListView, CollectionViewHandler>();
			handlers.AddHandler<Page, PageHandler>();
			handlers.AddHandler<ContentPage, ContentPageHandler>();
			handlers.AddHandler<NavigationPage, NavigationPageHandler>();
			handlers.AddHandler<Shell, ShellHandler>();
			handlers.AddHandler<FlyoutPage, FlyoutPageHandler>();
			handlers.AddHandler<TabbedPage, TabbedPageHandler>();
			handlers.AddHandler<Application, ApplicationHandler>();
			handlers.AddHandler<Window, WindowHandler>();
		});
		builder.Services.AddSingleton(linuxApplicationOptions);
		return builder;
	}

	private static void RegisterTypeConverters()
	{
		TypeDescriptor.AddAttributes(typeof(SKColor), new TypeConverterAttribute(typeof(SKColorTypeConverter)));
		TypeDescriptor.AddAttributes(typeof(SKRect), new TypeConverterAttribute(typeof(SKRectTypeConverter)));
		TypeDescriptor.AddAttributes(typeof(SKSize), new TypeConverterAttribute(typeof(SKSizeTypeConverter)));
		TypeDescriptor.AddAttributes(typeof(SKPoint), new TypeConverterAttribute(typeof(SKPointTypeConverter)));
	}
}
