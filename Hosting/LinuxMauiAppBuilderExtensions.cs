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

    /// <summary>
    /// Cross-platform stub entry point used by <c>UseX11()</c> / <c>UseWayland()</c> to
    /// pre-set the display server before the main registration runs. <paramref name="serverName"/>
    /// is the case-insensitive enum name (e.g. "X11", "Wayland").
    /// </summary>
    public static void RegisterWithDisplayServer(MauiAppBuilder builder, string serverName)
    {
        var server = Enum.TryParse<DisplayServerType>(serverName, ignoreCase: true, out var parsed)
            ? parsed
            : DisplayServerType.Auto;
        LinuxMauiAppBuilderExtensionsInternal.RegisterLinuxServices(
            builder,
            options => options.DisplayServer = server);
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

    /// <summary>
    /// Adds Linux platform support and forces the X11/XWayland backend, even on a Wayland
    /// session. Use this as a drop-in replacement for <see cref="UseLinux(MauiAppBuilder)"/>
    /// — call one or the other, not both. Optional <paramref name="configure"/> runs after
    /// the display-server preset so additional option tweaks still apply.
    /// </summary>
    public static MauiAppBuilder UseX11(this MauiAppBuilder builder, Action<LinuxApplicationOptions>? configure = null)
    {
        RegisterLinuxServices(builder, options =>
        {
            options.DisplayServer = DisplayServerType.X11;
            configure?.Invoke(options);
        });
        return builder;
    }

    /// <summary>
    /// Adds Linux platform support and forces the native Wayland backend. Falls back to
    /// X11/XWayland automatically if the Wayland connection cannot be opened (e.g.
    /// libwayland-client missing, or running under a pure X11 session). Use as a drop-in
    /// replacement for <see cref="UseLinux(MauiAppBuilder)"/>.
    /// </summary>
    public static MauiAppBuilder UseWayland(this MauiAppBuilder builder, Action<LinuxApplicationOptions>? configure = null)
    {
        RegisterLinuxServices(builder, options =>
        {
            options.DisplayServer = DisplayServerType.Wayland;
            configure?.Invoke(options);
        });
        return builder;
    }

    internal static void RegisterLinuxServices(MauiAppBuilder builder, Action<LinuxApplicationOptions>? configure)
    {
        // Patch MAUI Essentials stubs before any services use them
        EssentialsPatches.Apply();

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

        // Register theming services. SystemThemeService has a private constructor and
        // a single canonical instance — DI returns the same object as
        // SystemThemeService.Instance so consumers don't see a divergent second copy.
        // (HighContrastService is constructed directly by SkiaView.Accessibility — see
        // the static field there — so we don't register it here.)
        builder.Services.TryAddSingleton(_ => SystemThemeService.Instance);

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

            // Shapes
            handlers.AddHandler<Microsoft.Maui.Controls.Shapes.Ellipse, EllipseHandler>();
            handlers.AddHandler<Microsoft.Maui.Controls.Shapes.Line, LineHandler>();
            handlers.AddHandler<Microsoft.Maui.Controls.Shapes.Rectangle, RectangleHandler>();
            handlers.AddHandler<Microsoft.Maui.Controls.Shapes.Polygon, PolygonHandler>();
            handlers.AddHandler<Microsoft.Maui.Controls.Shapes.Polyline, PolylineHandler>();
            handlers.AddHandler<Microsoft.Maui.Controls.Shapes.Path, PathHandler>();

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

            // SkiaSharp native views (LiveCharts, Microcharts, custom drawings)
            handlers.AddHandler<SkiaSharp.Views.Maui.Controls.SKCanvasView, SKCanvasViewHandler>();
            handlers.AddHandler<SkiaSharp.Views.Maui.Controls.SKGLView, SKGLViewHandler>();

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
