using System;
using Microsoft.Maui.Platform.Linux.Native;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Service for applying theme-aware CSS to GTK widgets.
/// </summary>
public static class GtkThemeService
{
    private static IntPtr _currentCssProvider = IntPtr.Zero;
    private static bool _initialized = false;

    /// <summary>
    /// Initializes the GTK theme service and applies initial CSS.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        ApplyTheme();
    }

    /// <summary>
    /// Applies GTK CSS based on the app's current theme.
    /// Call this whenever the app theme changes.
    /// </summary>
    public static void ApplyTheme()
    {
        try
        {
            // Check the app's current theme
            bool isDark = Microsoft.Maui.Controls.Application.Current?.UserAppTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark;

            // If UserAppTheme is Unspecified, fall back to RequestedTheme
            if (Microsoft.Maui.Controls.Application.Current?.UserAppTheme == Microsoft.Maui.ApplicationModel.AppTheme.Unspecified)
            {
                isDark = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark;
            }

            DiagnosticLog.Debug("GtkThemeService", $"ApplyTheme: isDark={isDark}");

            // Create comprehensive CSS based on the theme
            string css = isDark ? GetDarkCss() : GetLightCss();

            // Get the default screen
            IntPtr screen = GtkNative.gdk_screen_get_default();
            if (screen == IntPtr.Zero)
            {
                DiagnosticLog.Error("GtkThemeService", "Failed to get default screen");
                return;
            }

            // Create new CSS provider
            IntPtr newProvider = GtkNative.gtk_css_provider_new();
            if (newProvider == IntPtr.Zero)
            {
                DiagnosticLog.Error("GtkThemeService", "Failed to create CSS provider");
                return;
            }

            // Load CSS data
            if (!GtkNative.gtk_css_provider_load_from_data(newProvider, css, -1, IntPtr.Zero))
            {
                DiagnosticLog.Error("GtkThemeService", "Failed to load CSS data");
                return;
            }

            // Apply to screen (this affects all GTK widgets)
            GtkNative.gtk_style_context_add_provider_for_screen(screen, newProvider, GtkNative.GTK_STYLE_PROVIDER_PRIORITY_USER);

            // Store reference to current provider
            _currentCssProvider = newProvider;

            DiagnosticLog.Debug("GtkThemeService", "CSS applied successfully");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("GtkThemeService", $"Error applying theme: {ex.Message}");
        }
    }

    private static string GetDarkCss()
    {
        return @"
            /* Dark theme - base */
            * {
                background-color: #303030;
                color: #E0E0E0;
            }

            /* Windows and dialogs */
            window, dialog, .background {
                background-color: #303030;
                color: #E0E0E0;
            }

            /* Message dialogs - all parts same color */
            messagedialog,
            messagedialog.background,
            messagedialog .background,
            messagedialog box,
            messagedialog grid,
            messagedialog .dialog-vbox,
            messagedialog .message-area,
            messagedialog .dialog-action-area,
            messagedialog .dialog-action-box,
            messagedialog actionbar,
            messagedialog buttonbox,
            messagedialog box.vertical,
            messagedialog box.horizontal,
            messagedialog .linked,
            messagedialog .linked button,
            dialog box,
            dialog .dialog-vbox,
            dialog .dialog-action-area {
                background-color: #303030;
                background-image: none;
                border: none;
                border-style: none;
                box-shadow: none;
            }

            messagedialog separator,
            dialog separator {
                background-color: transparent;
                background-image: none;
                min-height: 0;
                min-width: 0;
                border: none;
            }

            messagedialog .dialog-action-area,
            dialog .dialog-action-area {
                background-color: #303030;
                background-image: none;
                border: none;
                border-top-style: none;
                margin: 0;
                padding: 8px;
            }

            messagedialog .dialog-action-area button,
            dialog .dialog-action-area button {
                background-color: #505050;
                background-image: none;
            }

            /* Header bars and title bars */
            headerbar, .titlebar {
                background-color: #252525;
                background-image: none;
                color: #E0E0E0;
                border: none;
                box-shadow: none;
            }

            headerbar *, .titlebar * {
                background-color: transparent;
                background-image: none;
            }

            headerbar button, .titlebar button {
                background-color: #353535;
                background-image: none;
                color: #E0E0E0;
                border-color: #353535;
                border-radius: 6px;
            }

            headerbar button:hover, .titlebar button:hover {
                background-color: #454545;
                border-color: #454545;
            }

            /* Window control buttons */
            windowcontrols button,
            button.titlebutton,
            .titlebutton {
                background-color: #252525;
                background-image: none;
                border-color: #252525;
                border-radius: 50%;
                min-width: 24px;
                min-height: 24px;
                padding: 4px;
            }

            windowcontrols button *,
            button.titlebutton *,
            .titlebutton * {
                background-color: transparent;
                background-image: none;
            }

            windowcontrols button:hover,
            button.titlebutton:hover,
            .titlebutton:hover {
                background-color: #404040;
                border-color: #404040;
            }

            /* Dialog action areas */
            .dialog-action-area,
            .dialog-action-box,
            actionbar {
                background-color: #303030;
                background-image: none;
                border: none;
            }

            /* Labels */
            label {
                background-color: transparent;
                color: #E0E0E0;
            }

            /* Buttons */
            button {
                background-image: none;
                background-color: #505050;
                color: #E0E0E0;
                border-color: #505050;
                border-radius: 6px;
            }

            button * {
                background-color: transparent;
                background-image: none;
            }

            button:hover {
                background-color: #606060;
                border-color: #606060;
            }

            button:hover * {
                background-color: transparent;
            }

            button:active {
                background-color: #404040;
                border-color: #404040;
            }

            /* Entries and text inputs */
            entry, textview, text {
                background-color: #404040;
                color: #E0E0E0;
                border-color: #505050;
                border-radius: 4px;
            }

            /* Menus and context menus */
            menu, .menu, .popup {
                background-color: #303030;
                color: #E0E0E0;
                border: none;
                border-radius: 8px;
                padding: 4px;
            }

            menuitem {
                background-color: transparent;
                color: #E0E0E0;
                border: none;
                border-radius: 6px;
                padding: 6px 12px;
                margin: 2px 4px;
            }

            menuitem * {
                background-color: transparent;
                background-image: none;
            }

            menuitem:hover {
                background-color: #505050;
            }

            menuitem:hover * {
                background-color: transparent;
            }

            /* Popover */
            popover, popover.background {
                background-color: #303030;
                border: none;
                border-radius: 8px;
            }

            /* Separators */
            separator {
                background-color: #505050;
            }

            /* Scrollbars */
            scrollbar {
                background-color: #252525;
            }

            scrollbar slider {
                background-color: #505050;
                border-radius: 4px;
            }

            /* Focus styling */
            *:focus {
                outline-color: #606060;
            }
        ";
    }

    private static string GetLightCss()
    {
        return @"
            /* Light theme - base */
            * {
                background-color: #FFFFFF;
                color: #212121;
            }

            /* Windows and dialogs */
            window, dialog, .background {
                background-color: #FFFFFF;
                color: #212121;
            }

            /* Message dialogs - all parts same color */
            messagedialog,
            messagedialog.background,
            messagedialog .background,
            messagedialog box,
            messagedialog grid,
            messagedialog .dialog-vbox,
            messagedialog .message-area,
            messagedialog .dialog-action-area,
            messagedialog .dialog-action-box,
            messagedialog actionbar,
            messagedialog buttonbox,
            messagedialog box.vertical,
            messagedialog box.horizontal,
            messagedialog .linked,
            messagedialog .linked button,
            dialog box,
            dialog .dialog-vbox,
            dialog .dialog-action-area {
                background-color: #FFFFFF;
                background-image: none;
                border: none;
                border-style: none;
                box-shadow: none;
            }

            messagedialog separator,
            dialog separator {
                background-color: transparent;
                background-image: none;
                min-height: 0;
                min-width: 0;
                border: none;
            }

            messagedialog .dialog-action-area,
            dialog .dialog-action-area {
                background-color: #FFFFFF;
                background-image: none;
                border: none;
                border-top-style: none;
                margin: 0;
                padding: 8px;
            }

            messagedialog .dialog-action-area button,
            dialog .dialog-action-area button {
                background-color: #F5F5F5;
                background-image: none;
            }

            /* Header bars and title bars */
            headerbar, .titlebar {
                background-color: #F5F5F5;
                background-image: none;
                color: #212121;
                border: none;
                box-shadow: none;
            }

            headerbar *, .titlebar * {
                background-color: transparent;
                background-image: none;
            }

            headerbar button, .titlebar button {
                background-color: #EBEBEB;
                background-image: none;
                color: #212121;
                border-color: #EBEBEB;
                border-radius: 6px;
            }

            headerbar button:hover, .titlebar button:hover {
                background-color: #DDDDDD;
                border-color: #DDDDDD;
            }

            /* Window control buttons */
            windowcontrols button,
            button.titlebutton,
            .titlebutton {
                background-color: #F5F5F5;
                background-image: none;
                border-color: #F5F5F5;
                border-radius: 50%;
                min-width: 24px;
                min-height: 24px;
                padding: 4px;
            }

            windowcontrols button *,
            button.titlebutton *,
            .titlebutton * {
                background-color: transparent;
                background-image: none;
            }

            windowcontrols button:hover,
            button.titlebutton:hover,
            .titlebutton:hover {
                background-color: #E0E0E0;
                border-color: #E0E0E0;
            }

            /* Dialog action areas */
            .dialog-action-area,
            .dialog-action-box,
            actionbar {
                background-color: #FFFFFF;
                background-image: none;
                border: none;
            }

            /* Labels */
            label {
                background-color: transparent;
                color: #212121;
            }

            /* Buttons */
            button {
                background-image: none;
                background-color: #F5F5F5;
                color: #212121;
                border-color: #E0E0E0;
                border-radius: 6px;
            }

            button * {
                background-color: transparent;
                background-image: none;
            }

            button:hover {
                background-color: #E0E0E0;
                border-color: #D0D0D0;
            }

            button:hover * {
                background-color: transparent;
            }

            button:active {
                background-color: #D0D0D0;
                border-color: #C0C0C0;
            }

            /* Entries and text inputs */
            entry, textview, text {
                background-color: #FFFFFF;
                color: #212121;
                border-color: #E0E0E0;
                border-radius: 4px;
            }

            /* Menus and context menus */
            menu, .menu, .popup {
                background-color: #FFFFFF;
                color: #212121;
                border: none;
                border-radius: 8px;
                padding: 4px;
            }

            menuitem {
                background-color: transparent;
                color: #212121;
                border: none;
                border-radius: 6px;
                padding: 6px 12px;
                margin: 2px 4px;
            }

            menuitem * {
                background-color: transparent;
                background-image: none;
            }

            menuitem:hover {
                background-color: #E8E8E8;
            }

            menuitem:hover * {
                background-color: transparent;
            }

            /* Popover */
            popover, popover.background {
                background-color: #FFFFFF;
                border: none;
                border-radius: 8px;
            }

            /* Separators */
            separator {
                background-color: #E0E0E0;
            }

            /* Scrollbars */
            scrollbar {
                background-color: #F5F5F5;
            }

            scrollbar slider {
                background-color: #C0C0C0;
                border-radius: 4px;
            }

            /* Focus styling */
            *:focus {
                outline-color: #C0C0C0;
            }
        ";
    }
}
