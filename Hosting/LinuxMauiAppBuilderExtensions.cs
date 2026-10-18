// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.Accessibility;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Platform.Linux;
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
        // Mopups popups as window layers, when the app uses Mopups.
        MopupsBridge.Register(builder.Services);
        // VisualElement.Focus()/Unfocus() through the window's focus.
        Microsoft.Maui.Platform.Linux.Handlers.FocusCommands.Register();
        Microsoft.Maui.Platform.Linux.Handlers.LinuxViewMappers.Register();
        if (Microsoft.Maui.Platform.Linux.Diagnostics.PageInvariants.Enabled)
            Microsoft.Maui.Platform.Linux.Diagnostics.PageInvariants.Install();
        Microsoft.Maui.Platform.Linux.Handlers.LoadedEventPatches.Install();
        // CommunityToolkit.Maui's platform features (touch behaviors, alerts, badge, speech,
        // DrawingView image export), when the app uses the toolkit.
        CommunityToolkitPatches.Install();
        // VisualTreeElementExtensions' platform lookups (GetVisualTreeElements(point), a platform
        // view's element) on the Skia tree.
        Microsoft.Maui.Platform.Linux.Handlers.VisualTreeElementPatches.Install();
        // MAUI's window overlays (Window.AddOverlay, the visual diagnostics adorners) and the
        // view-geometry helpers they use, on the Skia tree.
        Microsoft.Maui.Platform.Linux.Handlers.WindowOverlayPatches.Install();
        // MAUI's effects pipeline: an Element resolves the PlatformEffect for a RoutingEffect
        // through the EffectsFactory service, which only ConfigureEffects registers; without
        // it the first effect added to any view threw. Registered empty here, so an app or a
        // library's own ConfigureEffects only adds to it.
        builder.ConfigureEffects(_ => { });
        // MAUI's image-source services, with the Linux load method: the Image and ImageButton
        // handlers resolve the service for a source's type through IImageSourceServiceProvider,
        // so an app or library adds its own source type with
        // ConfigureImageSources(s => s.AddService<TSource, TService>()) as on any platform.
        builder.ConfigureImageSources(LinuxImageSourceServices.Register);

        var options = new LinuxApplicationOptions();
        configure?.Invoke(options);

        // Register dispatcher provider
        builder.Services.TryAddSingleton<IDispatcherProvider>(LinuxDispatcherProvider.Instance);

        // Animation ticker and manager for MAUI's ViewExtensions (FadeTo, ...).
        // Replacing MAUI's own: UseMauiApp registers a PlatformTicker first, which
        // never runs on the platform-neutral build, and a TryAdd here lost to it,
        // so anything animating through the app's services got no frames
        // (Syncfusion schedules chart and list updates as one-frame animations).
        builder.Services.RemoveAll<Microsoft.Maui.Animations.ITicker>();
        builder.Services.RemoveAll<Microsoft.Maui.Animations.IAnimationManager>();
        builder.Services.AddSingleton<Microsoft.Maui.Animations.ITicker, LinuxTicker>();
        builder.Services.AddSingleton<Microsoft.Maui.Animations.IAnimationManager, LinuxAnimationManager>();

        // VisualElement.CaptureAsync / Window.CaptureAsync: MAUI built for a platform it has no
        // screenshot code for resolves IScreenshot from the handler's services and captures
        // through its IViewScreenshot (null results without one). The Screenshot facade's
        // instance (ScreenshotService, installed by EssentialsPatches) is both.
        builder.Services.TryAddSingleton<Microsoft.Maui.Media.IScreenshot>(_ => Microsoft.Maui.Media.Screenshot.Default);

        // Named font sizes (FontSize="Large" etc.): MAUI's XAML converter resolves
        // them through DependencyService and throws when no platform provides it.
        Microsoft.Maui.Controls.DependencyService.Register<Microsoft.Maui.Controls.Internals.IFontNamedSizeService, LinuxFontNamedSizeService>();

        // Register device services
        builder.Services.TryAddSingleton<IDeviceInfo>(DeviceInfoService.Instance);
        builder.Services.TryAddSingleton<IDeviceDisplay>(DeviceDisplayService.Instance);
        builder.Services.TryAddSingleton<IAppInfo>(AppInfoService.Instance);
        builder.Services.TryAddSingleton<IConnectivity>(ConnectivityService.Instance);

        // Register platform services. Each resolves to the instance behind the static facade
        // (Preferences.Default, ...), which EssentialsPatches installed: an injected
        // IPreferences and Preferences.Set must share one store (two PreferencesService
        // instances each cached the same file and overwrote each other's changes), and an
        // injected IVersionTracking must be MAUI's, which the facade uses.
        builder.Services.TryAddSingleton<ILauncher>(_ => Launcher.Default);
        builder.Services.TryAddSingleton<IPreferences>(_ => Preferences.Default);
        builder.Services.TryAddSingleton<IFilePicker>(_ => FilePicker.Default);
        builder.Services.TryAddSingleton<IClipboard>(_ => Clipboard.Default);
        builder.Services.TryAddSingleton<IShare>(_ => Share.Default);
        builder.Services.TryAddSingleton<ISecureStorage>(_ => SecureStorage.Default);
        builder.Services.TryAddSingleton<IVersionTracking>(_ => VersionTracking.Default);
        builder.Services.TryAddSingleton<IAppActions>(_ => AppActions.Current);
        builder.Services.TryAddSingleton<IBrowser>(_ => Microsoft.Maui.ApplicationModel.Browser.Default);
        builder.Services.TryAddSingleton<IEmail>(_ => Email.Default);

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

        // Fonts: MauiApp.CreateBuilder() already TryAdd'ed MAUI's portable FontRegistrar/FontManager
        // (their EmbeddedFontLoader is a no-op on the generic TFM), so Replace rather than TryAdd.
        builder.Services.Replace(ServiceDescriptor.Singleton<IFontRegistrar>(_ => LinuxFontRegistrar.Instance));
        builder.Services.Replace(ServiceDescriptor.Singleton<IFontManager>(sp => new LinuxFontManager(sp.GetRequiredService<IFontRegistrar>())));

        // Essentials gaps
        builder.Services.TryAddSingleton<IFileSystem>(_ => FileSystem.Current);
        builder.Services.TryAddSingleton<ISemanticScreenReader>(_ => SemanticScreenReader.Default);
        builder.Services.TryAddSingleton<IWebAuthenticator>(_ => WebAuthenticator.Default);
        builder.Services.TryAddSingleton<IPasskeys>(_ => Passkeys.Default);

        // Motion/environment sensors: the facades' MAUI implementations, reading the kernel's IIO
        // devices (IsSupported only with the hardware; Start throws FeatureNotSupportedException without).
        builder.Services.TryAddSingleton<IAccelerometer>(_ => Accelerometer.Default);
        builder.Services.TryAddSingleton<IBarometer>(_ => Barometer.Default);
        builder.Services.TryAddSingleton<ICompass>(_ => Compass.Default);
        builder.Services.TryAddSingleton<IGyroscope>(_ => Gyroscope.Default);
        builder.Services.TryAddSingleton<IMagnetometer>(_ => Magnetometer.Default);
        builder.Services.TryAddSingleton<IOrientationSensor>(_ => OrientationSensor.Default);

        // Register additional Linux-specific services
        builder.Services.TryAddSingleton<FolderPickerService>();
        builder.Services.TryAddSingleton<NotificationService>();
        builder.Services.TryAddSingleton<SystemTrayService>();
        builder.Services.TryAddSingleton(_ => MonitorService.Instance);
        builder.Services.TryAddSingleton<DragDropService>();

        // Page.DisplayAlert / DisplayPromptAsync / DisplayActionSheet -> Skia dialogs
        // (MAUI 10 AlertManager resolves these keyed delegates).
        LinuxAlertManager.Register(builder.Services);

        // Navigation.PushModalAsync / PopModalAsync -> full-window layers of the window's
        // context, through MAUI's modal-platform extension point (an app's own
        // IModalNavigationPlatformFactory registration takes precedence).
        LinuxModalNavigationPlatform.Register(builder.Services);

        // Register GTK host service
        builder.Services.TryAddSingleton(_ => GtkHostService.Instance);

        // Register type converters for XAML support
        RegisterTypeConverters();

        // Register Linux-specific handlers, as the platform's defaults (LinuxHandlerDefaults):
        // a handler the app or a library registered for the same type, before or after
        // UseLinux(), is the one used, as on MAUI's own platforms. The CommunityToolkit
        // registrations below replace the toolkit's portable handlers (no platform view).
        builder.ConfigureMauiHandlers(handlers =>
        {
            // Application handler
            LinuxHandlerDefaults.Register(handlers, typeof(IApplication), typeof(ApplicationHandler));

            // Shapes
            LinuxHandlerDefaults.Register(handlers, typeof(Microsoft.Maui.Controls.Shapes.Ellipse), typeof(EllipseHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Microsoft.Maui.Controls.Shapes.Line), typeof(LineHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Microsoft.Maui.Controls.Shapes.Rectangle), typeof(RectangleHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Microsoft.Maui.Controls.Shapes.Polygon), typeof(PolygonHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Microsoft.Maui.Controls.Shapes.Polyline), typeof(PolylineHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Microsoft.Maui.Controls.Shapes.Path), typeof(ShapePathHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Microsoft.Maui.Controls.Shapes.RoundRectangle), typeof(RoundRectangleHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(MenuBar), typeof(MenuBarHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(MenuFlyout), typeof(MenuFlyoutHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Toolbar), typeof(ToolbarHandler));

            // Core controls
            LinuxHandlerDefaults.Register(handlers, typeof(BoxView), typeof(BoxViewHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Button), typeof(TextButtonHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Label), typeof(LabelHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Entry), typeof(EntryHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Editor), typeof(EditorHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(CheckBox), typeof(CheckBoxHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Switch), typeof(SwitchHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Slider), typeof(SliderHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Stepper), typeof(StepperHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(RadioButton), typeof(RadioButtonHandler));

            // Layout controls
            LinuxHandlerDefaults.Register(handlers, typeof(Grid), typeof(GridHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(StackLayout), typeof(StackLayoutHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(VerticalStackLayout), typeof(StackLayoutHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(HorizontalStackLayout), typeof(StackLayoutHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(AbsoluteLayout), typeof(AbsoluteLayoutHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(FlexLayout), typeof(FlexLayoutHandler));
            // Any other Layout subclass (third-party or app-defined): its own ILayoutManager.
            LinuxHandlerDefaults.Register(handlers, typeof(Layout), typeof(CrossPlatformLayoutHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(ScrollView), typeof(ScrollViewHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Frame), typeof(FrameHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Border), typeof(BorderHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(ContentView), typeof(ContentViewHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(ContentPresenter), typeof(ContentPresenterHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(TemplatedView), typeof(TemplatedViewHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(RefreshView), typeof(RefreshViewHandler));
            // A library's own IRefreshView (not a Controls RefreshView): MAUI's RefreshViewHandler on Linux.
            LinuxHandlerDefaults.Register(handlers, typeof(IRefreshView), typeof(CoreRefreshViewHandler));

            // Picker controls
            LinuxHandlerDefaults.Register(handlers, typeof(Picker), typeof(PickerHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(DatePicker), typeof(DatePickerHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(TimePicker), typeof(TimePickerHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(SearchBar), typeof(SearchBarHandler));

            // Progress & Activity
            LinuxHandlerDefaults.Register(handlers, typeof(ProgressBar), typeof(ProgressBarHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(ActivityIndicator), typeof(ActivityIndicatorHandler));

            // Image & Graphics
            LinuxHandlerDefaults.Register(handlers, typeof(Image), typeof(ImageHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(ImageButton), typeof(ImageButtonHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(GraphicsView), typeof(GraphicsViewHandler));

            // SkiaSharp native views (LiveCharts, Microcharts, custom drawings): replaces the
            // portable handlers UseSkiaSharp() registers, which have no platform view here.
            handlers.AddHandler(typeof(SkiaSharp.Views.Maui.Controls.SKCanvasView), typeof(SKCanvasViewHandler));
            handlers.AddHandler(typeof(SkiaSharp.Views.Maui.Controls.SKGLView), typeof(SKGLViewHandler));

            // CommunityToolkit.Maui DrawingView (only when the toolkit is in the app)
            if (DrawingViewHandler.ToolkitDrawingViewType is { } drawingView)
                handlers.AddHandler(drawingView, DrawingViewHandler.HandlerType);
            // CommunityToolkit.Maui SemanticOrderView (its ViewOrder orders the AT-SPI tree)
            if (SemanticOrderViewHandler.ToolkitSemanticOrderViewType is { } semanticOrderView)
                handlers.AddHandler(semanticOrderView, typeof(SemanticOrderViewHandler));

            // Web: one handler on either engine, WPE WebKit composited in the Skia tree when
            // installed (works in native Wayland/X11 mode), else the GTK-hosted WebKitGTK view.
            LinuxHandlerDefaults.Register(handlers, typeof(WebView), typeof(LinuxWebViewHandler));
            // HybridWebView on the same engines, unless the app turned the feature off
            // ($(MauiHybridWebViewSupported)=false), as MAUI registers its own.
            if (IsHybridWebViewSupported)
                LinuxHandlerDefaults.Register(handlers, typeof(HybridWebView), typeof(LinuxHybridWebViewHandler));

            // Collection Views
            LinuxHandlerDefaults.Register(handlers, typeof(CollectionView), typeof(CollectionViewHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(ListView), typeof(ListViewHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(TableView), typeof(TableViewHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(CarouselView), typeof(CarouselViewHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(IndicatorView), typeof(IndicatorViewHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(IIndicatorView), typeof(CoreIndicatorViewHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(SwipeView), typeof(SwipeViewHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Microsoft.Maui.Controls.SwipeItem), typeof(SwipeItemMenuItemHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(SwipeItemView), typeof(SwipeItemViewHandler));
            // A library's own swipe view and swipe items (MAUI's interfaces) get the Linux handlers.
            LinuxHandlerDefaults.Register(handlers, typeof(ISwipeView), typeof(CoreSwipeViewHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(ISwipeItemMenuItem), typeof(SwipeItemMenuItemHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(ISwipeItemView), typeof(SwipeItemViewHandler));

            // Pages & Navigation
            LinuxHandlerDefaults.Register(handlers, typeof(Page), typeof(PageHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(ContentPage), typeof(ContentPageHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(NavigationPage), typeof(NavigationPageHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Shell), typeof(ShellHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(FlyoutPage), typeof(FlyoutPageHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(TabbedPage), typeof(TabbedPageHandler));

            // Application & Window
            LinuxHandlerDefaults.Register(handlers, typeof(Application), typeof(ApplicationHandler));
            LinuxHandlerDefaults.Register(handlers, typeof(Microsoft.Maui.Controls.Window), typeof(WindowHandler));
        });

        // Store options for later use
        builder.Services.AddSingleton(options);
    }

    // MAUI's RuntimeFeature.IsHybridWebViewSupported (on unless $(MauiHybridWebViewSupported) is false).
    private static bool IsHybridWebViewSupported =>
        !AppContext.TryGetSwitch("Microsoft.Maui.RuntimeFeature.IsHybridWebViewSupported", out var supported) || supported;

    private static void RegisterTypeConverters()
    {
        TypeDescriptor.AddAttributes(typeof(SKColor), new TypeConverterAttribute(typeof(SKColorTypeConverter)));
        TypeDescriptor.AddAttributes(typeof(SKRect), new TypeConverterAttribute(typeof(SKRectTypeConverter)));
        TypeDescriptor.AddAttributes(typeof(SKSize), new TypeConverterAttribute(typeof(SKSizeTypeConverter)));
        TypeDescriptor.AddAttributes(typeof(SKPoint), new TypeConverterAttribute(typeof(SKPointTypeConverter)));
    }
}
