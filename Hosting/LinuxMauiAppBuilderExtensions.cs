// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

/// <summary>
/// Called by OpenMaui.Hosting stub via reflection.
/// This is the well-known entry point that the cross-platform UseLinux() resolves to.
/// </summary>
public static class LinuxPlatformRegistrar
{
    public static void Register(MauiAppBuilder builder)
    {
        LinuxMauiAppBuilderExtensionsInternal.RegisterLinuxServices(builder, null);
    }
}

/// <summary>
/// Direct extension methods for Linux-only projects that reference OpenMaui.Controls.Linux directly.
/// For cross-platform projects, use OpenMaui.Hosting's UseLinux() instead.
/// </summary>
public static class LinuxMauiAppBuilderExtensionsInternal
{
    /// <summary>
    /// Adds Linux platform support with configuration options.
    /// For cross-platform projects, prefer the parameterless UseLinux() from OpenMaui.Hosting.
    /// </summary>
    public static MauiAppBuilder UseLinux(this MauiAppBuilder builder, Action<LinuxApplicationOptions> configure)
    {
        RegisterLinuxServices(builder, configure);
        return builder;
    }

    internal static void RegisterLinuxServices(MauiAppBuilder builder, Action<LinuxApplicationOptions>? configure)
    {
        var options = new LinuxApplicationOptions();
        configure?.Invoke(options);

        // Register dispatcher provider
        builder.Services.TryAddSingleton<IDispatcherProvider>(LinuxDispatcherProvider.Instance);

        // Register device services
        builder.Services.TryAddSingleton<IDeviceInfo>(DeviceInfoService.Instance);
        builder.Services.TryAddSingleton<IDeviceDisplay>(DeviceDisplayService.Instance);
        builder.Services.TryAddSingleton<IAppInfo>(AppInfoService.Instance);
        builder.Services.TryAddSingleton<IConnectivity>(ConnectivityService.Instance);

        // Register platform services
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

        // Register theming and accessibility services
        builder.Services.TryAddSingleton<SystemThemeService>();
        builder.Services.TryAddSingleton<HighContrastService>();

        // Register accessibility service
        builder.Services.TryAddSingleton<IAccessibilityService>(_ => AccessibilityServiceFactory.Instance);

        // Register input method service
        builder.Services.TryAddSingleton<IInputMethodService>(_ => InputMethodServiceFactory.Instance);

        // Register font fallback manager
        builder.Services.TryAddSingleton(_ => FontFallbackManager.Instance);

        // Register additional Linux-specific services
        builder.Services.TryAddSingleton<FolderPickerService>();
        builder.Services.TryAddSingleton<NotificationService>();
        builder.Services.TryAddSingleton<SystemTrayService>();
        builder.Services.TryAddSingleton(_ => MonitorService.Instance);
        builder.Services.TryAddSingleton<DragDropService>();

        // Register GTK host service
        builder.Services.TryAddSingleton(_ => GtkHostService.Instance);

        // Register type converters for XAML support
        RegisterTypeConverters();

        // Register Linux-specific handlers
        builder.ConfigureMauiHandlers(handlers =>
        {
            // Application handler
            handlers.AddHandler<IApplication, ApplicationHandler>();

            // Core controls
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

            // Layout controls
            handlers.AddHandler<Grid, GridHandler>();
            handlers.AddHandler<StackLayout, StackLayoutHandler>();
            handlers.AddHandler<VerticalStackLayout, StackLayoutHandler>();
            handlers.AddHandler<HorizontalStackLayout, StackLayoutHandler>();
            handlers.AddHandler<AbsoluteLayout, LayoutHandler>();
            handlers.AddHandler<FlexLayout, FlexLayoutHandler>();
            handlers.AddHandler<ScrollView, ScrollViewHandler>();
            handlers.AddHandler<Frame, FrameHandler>();
            handlers.AddHandler<Border, BorderHandler>();
            handlers.AddHandler<ContentView, BorderHandler>();
            handlers.AddHandler<RefreshView, RefreshViewHandler>();

            // Picker controls
            handlers.AddHandler<Picker, PickerHandler>();
            handlers.AddHandler<DatePicker, DatePickerHandler>();
            handlers.AddHandler<TimePicker, TimePickerHandler>();
            handlers.AddHandler<SearchBar, SearchBarHandler>();

            // Progress & Activity
            handlers.AddHandler<ProgressBar, ProgressBarHandler>();
            handlers.AddHandler<ActivityIndicator, ActivityIndicatorHandler>();

            // Image & Graphics
            handlers.AddHandler<Image, ImageHandler>();
            handlers.AddHandler<ImageButton, ImageButtonHandler>();
            handlers.AddHandler<GraphicsView, GraphicsViewHandler>();

            // Web - use GtkWebViewHandler
            handlers.AddHandler<WebView, GtkWebViewHandler>();

            // Collection Views
            handlers.AddHandler<CollectionView, CollectionViewHandler>();
            handlers.AddHandler<ListView, CollectionViewHandler>();
            handlers.AddHandler<CarouselView, CarouselViewHandler>();
            handlers.AddHandler<IndicatorView, IndicatorViewHandler>();
            handlers.AddHandler<SwipeView, SwipeViewHandler>();

            // Pages & Navigation
            handlers.AddHandler<Page, PageHandler>();
            handlers.AddHandler<ContentPage, ContentPageHandler>();
            handlers.AddHandler<NavigationPage, NavigationPageHandler>();
            handlers.AddHandler<Shell, ShellHandler>();
            handlers.AddHandler<FlyoutPage, FlyoutPageHandler>();
            handlers.AddHandler<TabbedPage, TabbedPageHandler>();

            // Application & Window
            handlers.AddHandler<Application, ApplicationHandler>();
            handlers.AddHandler<Microsoft.Maui.Controls.Window, WindowHandler>();
        });

        // Store options for later use
        builder.Services.AddSingleton(options);
    }

    private static void RegisterTypeConverters()
    {
        TypeDescriptor.AddAttributes(typeof(SKColor), new TypeConverterAttribute(typeof(SKColorTypeConverter)));
        TypeDescriptor.AddAttributes(typeof(SKRect), new TypeConverterAttribute(typeof(SKRectTypeConverter)));
        TypeDescriptor.AddAttributes(typeof(SKSize), new TypeConverterAttribute(typeof(SKSizeTypeConverter)));
        TypeDescriptor.AddAttributes(typeof(SKPoint), new TypeConverterAttribute(typeof(SKPointTypeConverter)));
    }
}
