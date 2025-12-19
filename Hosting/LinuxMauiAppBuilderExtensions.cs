// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Platform.Linux.Hosting;

/// <summary>
/// Extension methods for configuring MAUI applications for Linux.
/// </summary>
public static class LinuxMauiAppBuilderExtensions
{
    /// <summary>
    /// Configures the MAUI application to run on Linux.
    /// </summary>
    public static MauiAppBuilder UseLinux(this MauiAppBuilder builder)
    {
        return builder.UseLinux(configure: null);
    }

    /// <summary>
    /// Configures the MAUI application to run on Linux with options.
    /// </summary>
    public static MauiAppBuilder UseLinux(this MauiAppBuilder builder, Action<LinuxApplicationOptions>? configure)
    {
        var options = new LinuxApplicationOptions();
        configure?.Invoke(options);

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

        // Register Linux-specific handlers
        builder.ConfigureMauiHandlers(handlers =>
        {
            // Phase 1 - MVP controls
            handlers.AddHandler<IButton, ButtonHandler>();
            handlers.AddHandler<ILabel, LabelHandler>();
            handlers.AddHandler<IEntry, EntryHandler>();
            handlers.AddHandler<ICheckBox, CheckBoxHandler>();
            handlers.AddHandler<ILayout, LayoutHandler>();
            handlers.AddHandler<IStackLayout, StackLayoutHandler>();
            handlers.AddHandler<IGridLayout, GridHandler>();

            // Phase 2 - Input controls
            handlers.AddHandler<ISlider, SliderHandler>();
            handlers.AddHandler<ISwitch, SwitchHandler>();
            handlers.AddHandler<IProgress, ProgressBarHandler>();
            handlers.AddHandler<IActivityIndicator, ActivityIndicatorHandler>();
            handlers.AddHandler<ISearchBar, SearchBarHandler>();

            // Phase 2 - Image & Graphics
            handlers.AddHandler<IImage, ImageHandler>();
            handlers.AddHandler<IImageButton, ImageButtonHandler>();
            handlers.AddHandler<IGraphicsView, GraphicsViewHandler>();

            // Phase 3 - Collection Views
            handlers.AddHandler<CollectionView, CollectionViewHandler>();

            // Phase 4 - Pages & Navigation
            handlers.AddHandler<Page, PageHandler>();
            handlers.AddHandler<ContentPage, ContentPageHandler>();
            handlers.AddHandler<NavigationPage, NavigationPageHandler>();

            // Phase 5 - Advanced Controls
            handlers.AddHandler<IPicker, PickerHandler>();
            handlers.AddHandler<IDatePicker, DatePickerHandler>();
            handlers.AddHandler<ITimePicker, TimePickerHandler>();
            handlers.AddHandler<IEditor, EditorHandler>();

            // Phase 7 - Additional Controls
            handlers.AddHandler<IStepper, StepperHandler>();
            handlers.AddHandler<IRadioButton, RadioButtonHandler>();
            handlers.AddHandler<IBorderView, BorderHandler>();

            // Window handler
            handlers.AddHandler<IWindow, WindowHandler>();
        });

        // Store options for later use
        builder.Services.AddSingleton(options);

        return builder;
    }
}

/// <summary>
/// Handler registration extensions.
/// </summary>
public static class HandlerMappingExtensions
{
    /// <summary>
    /// Adds a handler for the specified view type.
    /// </summary>
    public static IMauiHandlersCollection AddHandler<TView, THandler>(
        this IMauiHandlersCollection handlers)
        where TView : class
        where THandler : class
    {
        handlers.AddHandler(typeof(TView), typeof(THandler));
        return handlers;
    }
}
