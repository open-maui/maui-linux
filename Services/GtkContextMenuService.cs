using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Native;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Service for displaying native GTK context menus in MAUI applications.
/// Provides popup menu functionality with action callbacks.
/// </summary>
public static class GtkContextMenuService
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ActivateCallback(IntPtr menuItem, IntPtr userData);

    // Keep references to prevent garbage collection
    private static readonly List<ActivateCallback> _callbacks = new();
    private static readonly List<Action> _actions = new();

    public static void ShowContextMenu(List<GtkMenuItem> items)
    {
        if (items == null || items.Count == 0)
        {
            return;
        }

        _callbacks.Clear();
        _actions.Clear();

        IntPtr menu = GtkNative.gtk_menu_new();
        if (menu == IntPtr.Zero)
        {
            Console.WriteLine("[GtkContextMenuService] Failed to create GTK menu");
            return;
        }

        foreach (var item in items)
        {
            IntPtr menuItem;

            if (item.IsSeparator)
            {
                menuItem = GtkNative.gtk_separator_menu_item_new();
            }
            else
            {
                menuItem = GtkNative.gtk_menu_item_new_with_label(item.Text);
                GtkNative.gtk_widget_set_sensitive(menuItem, item.IsEnabled);

                if (item.IsEnabled && item.Action != null)
                {
                    var action = item.Action;
                    _actions.Add(action);
                    int actionIndex = _actions.Count - 1;

                    ActivateCallback callback = delegate
                    {
                        Console.WriteLine("[GtkContextMenuService] Menu item activated: " + item.Text);
                        _actions[actionIndex]?.Invoke();
                    };
                    _callbacks.Add(callback);

                    GtkNative.g_signal_connect_data(
                        menuItem,
                        "activate",
                        Marshal.GetFunctionPointerForDelegate(callback),
                        IntPtr.Zero,
                        IntPtr.Zero,
                        0);
                }
            }

            GtkNative.gtk_menu_shell_append(menu, menuItem);
            GtkNative.gtk_widget_show(menuItem);
        }

        // Apply theme-aware CSS styling
        ApplyMenuTheme(menu);

        GtkNative.gtk_widget_show(menu);

        IntPtr currentEvent = GtkNative.gtk_get_current_event();
        GtkNative.gtk_menu_popup_at_pointer(menu, currentEvent);

        if (currentEvent != IntPtr.Zero)
        {
            GtkNative.gdk_event_free(currentEvent);
        }

        Console.WriteLine($"[GtkContextMenuService] Showed GTK menu with {items.Count} items");
    }

    /// <summary>
    /// Applies theme-aware CSS styling to a GTK menu based on the app's current theme.
    /// </summary>
    private static void ApplyMenuTheme(IntPtr menu)
    {
        try
        {
            // Check the app's current theme (not the system theme)
            bool isDark = Microsoft.Maui.Controls.Application.Current?.UserAppTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark;

            // If UserAppTheme is Unspecified, fall back to RequestedTheme
            if (Microsoft.Maui.Controls.Application.Current?.UserAppTheme == Microsoft.Maui.ApplicationModel.AppTheme.Unspecified)
            {
                isDark = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark;
            }

            Console.WriteLine($"[GtkContextMenuService] ApplyMenuTheme: isDark={isDark}");

            // Create comprehensive CSS based on the theme
            string css = isDark
                ? @"
                    * {
                        background-color: #303030;
                        color: #E0E0E0;
                    }
                    menu, menuitem, .menu, .menuitem {
                        background-color: #303030;
                        color: #E0E0E0;
                    }
                    menuitem:hover, .menuitem:hover {
                        background-color: #505050;
                    }
                    separator {
                        background-color: #505050;
                    }
                "
                : @"
                    * {
                        background-color: #FFFFFF;
                        color: #212121;
                    }
                    menu, menuitem, .menu, .menuitem {
                        background-color: #FFFFFF;
                        color: #212121;
                    }
                    menuitem:hover, .menuitem:hover {
                        background-color: #E0E0E0;
                    }
                    separator {
                        background-color: #E0E0E0;
                    }
                ";

            // Create CSS provider and apply to the screen
            IntPtr cssProvider = GtkNative.gtk_css_provider_new();
            if (cssProvider != IntPtr.Zero)
            {
                GtkNative.gtk_css_provider_load_from_data(cssProvider, css, -1, IntPtr.Zero);

                IntPtr screen = GtkNative.gtk_widget_get_screen(menu);
                if (screen == IntPtr.Zero)
                {
                    screen = GtkNative.gdk_screen_get_default();
                }

                if (screen != IntPtr.Zero)
                {
                    GtkNative.gtk_style_context_add_provider_for_screen(screen, cssProvider, GtkNative.GTK_STYLE_PROVIDER_PRIORITY_USER);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GtkContextMenuService] Error applying menu theme: {ex.Message}");
        }
    }
}
