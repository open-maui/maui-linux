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

        GtkNative.gtk_widget_show(menu);

        IntPtr currentEvent = GtkNative.gtk_get_current_event();
        GtkNative.gtk_menu_popup_at_pointer(menu, currentEvent);

        if (currentEvent != IntPtr.Zero)
        {
            GtkNative.gdk_event_free(currentEvent);
        }

        Console.WriteLine($"[GtkContextMenuService] Showed GTK menu with {items.Count} items");
    }
}
