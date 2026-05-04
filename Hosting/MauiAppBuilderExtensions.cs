// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Copyright (c) 2025 MarketAlly LLC

using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Handlers;

namespace OpenMaui.Platform.Linux.Hosting;

/// <summary>
/// Extension methods for configuring OpenMaui Linux platform in a MAUI application.
/// This enables full XAML support by registering Linux-specific handlers.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Configures the application to use OpenMaui Linux platform with full XAML support.
    /// </summary>
    /// <param name="builder">The MAUI app builder.</param>
    /// <returns>The configured MAUI app builder.</returns>
    /// <example>
    /// <code>
    /// var builder = MauiApp.CreateBuilder();
    /// builder
    ///     .UseMauiApp&lt;App&gt;()
    ///     .UseOpenMauiLinux();  // Enable Linux support with XAML
    /// </code>
    /// </example>
    public static MauiAppBuilder UseOpenMauiLinux(this MauiAppBuilder builder)
    {
        builder.ConfigureMauiHandlers(handlers =>
        {
            // Register all Linux platform handlers
            // These map MAUI virtual views to our Skia platform views

            // Basic Controls
            handlers.AddHandler<Button, ButtonHandler>();
            handlers.AddHandler<Label, LabelHandler>();
            handlers.AddHandler<Entry, EntryHandler>();
            handlers.AddHandler<Editor, EditorHandler>();
            handlers.AddHandler<CheckBox, CheckBoxHandler>();
            handlers.AddHandler<Switch, SwitchHandler>();
            handlers.AddHandler<RadioButton, RadioButtonHandler>();

            // Selection Controls
            handlers.AddHandler<Slider, SliderHandler>();
            handlers.AddHandler<Stepper, StepperHandler>();
            handlers.AddHandler<Picker, PickerHandler>();
            handlers.AddHandler<DatePicker, DatePickerHandler>();
            handlers.AddHandler<TimePicker, TimePickerHandler>();

            // Display Controls
            handlers.AddHandler<Image, ImageHandler>();
            handlers.AddHandler<ImageButton, ImageButtonHandler>();
            handlers.AddHandler<ActivityIndicator, ActivityIndicatorHandler>();
            handlers.AddHandler<ProgressBar, ProgressBarHandler>();

            // Layout Controls
            handlers.AddHandler<Border, BorderHandler>();

            // Collection Controls
            handlers.AddHandler<CollectionView, CollectionViewHandler>();

            // Navigation Controls
            handlers.AddHandler<NavigationPage, NavigationPageHandler>();
            handlers.AddHandler<TabbedPage, TabbedPageHandler>();
            handlers.AddHandler<FlyoutPage, FlyoutPageHandler>();
            handlers.AddHandler<Shell, ShellHandler>();

            // Page Controls
            handlers.AddHandler<Page, PageHandler>();
            handlers.AddHandler<ContentPage, PageHandler>();

            // Graphics
            handlers.AddHandler<GraphicsView, GraphicsViewHandler>();

            // Search
            handlers.AddHandler<SearchBar, SearchBarHandler>();

            // Web
            handlers.AddHandler<WebView, WebViewHandler>();

            // Window
            handlers.AddHandler<Window, WindowHandler>();
        });

        // Register Linux-specific services
        builder.Services.AddSingleton<ILinuxPlatformServices, LinuxPlatformServices>();

        return builder;
    }

    /// <summary>
    /// Configures the application to use OpenMaui Linux with custom handler configuration.
    /// </summary>
    /// <param name="builder">The MAUI app builder.</param>
    /// <param name="configureHandlers">Action to configure additional handlers.</param>
    /// <returns>The configured MAUI app builder.</returns>
    public static MauiAppBuilder UseOpenMauiLinux(
        this MauiAppBuilder builder,
        Action<IMauiHandlersCollection>? configureHandlers)
    {
        builder.UseOpenMauiLinux();

        if (configureHandlers != null)
        {
            builder.ConfigureMauiHandlers(configureHandlers);
        }

        return builder;
    }
}

/// <summary>
/// Interface for Linux platform services.
/// </summary>
public interface ILinuxPlatformServices
{
    /// <summary>
    /// Gets the display server type (X11 or Wayland).
    /// </summary>
    DisplayServerType DisplayServer { get; }

    /// <summary>
    /// Gets the current DPI scale factor.
    /// </summary>
    float ScaleFactor { get; }

    /// <summary>
    /// Gets whether high contrast mode is enabled.
    /// </summary>
    bool IsHighContrastEnabled { get; }
}

/// <summary>
/// Display server types supported by OpenMaui.
/// </summary>
public enum DisplayServerType
{
    /// <summary>X11 display server.</summary>
    X11,
    /// <summary>Wayland display server.</summary>
    Wayland,
    /// <summary>Auto-detected display server.</summary>
    Auto
}

/// <summary>
/// Implementation of Linux platform services.
/// </summary>
internal class LinuxPlatformServices : ILinuxPlatformServices
{
    public DisplayServerType DisplayServer => DetectDisplayServer();
    public float ScaleFactor => DetectScaleFactor();
    public bool IsHighContrastEnabled => DetectHighContrast();

    private static DisplayServerType DetectDisplayServer()
    {
        var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        if (!string.IsNullOrEmpty(waylandDisplay))
            return DisplayServerType.Wayland;

        var display = Environment.GetEnvironmentVariable("DISPLAY");
        if (!string.IsNullOrEmpty(display))
            return DisplayServerType.X11;

        return DisplayServerType.Auto;
    }

    private static float DetectScaleFactor()
    {
        // Try GDK_SCALE first
        var gdkScale = Environment.GetEnvironmentVariable("GDK_SCALE");
        if (float.TryParse(gdkScale, out var scale))
            return scale;

        // Default to 1.0
        return 1.0f;
    }

    private static bool DetectHighContrast()
    {
        var highContrast = Environment.GetEnvironmentVariable("GTK_THEME");
        return highContrast?.Contains("HighContrast", StringComparison.OrdinalIgnoreCase) ?? false;
    }
}
