using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Native;

internal static class GtkNative
{
    public struct GtkAllocation
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool GSourceFunc(IntPtr userData);

    private const string Lib = "libgtk-3.so.0";

    public const int GTK_WINDOW_TOPLEVEL = 0;
    public const int GTK_WINDOW_POPUP = 1;

    private const string LibGdkPixbuf = "libgdk_pixbuf-2.0.so.0";

    public const int GDK_COLORSPACE_RGB = 0;

    private const string GLib = "libglib-2.0.so.0";

    private static readonly List<GSourceFunc> _idleCallbacks = new List<GSourceFunc>();

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_init(ref int argc, ref IntPtr argv);

    [DllImport("libgtk-3.so.0")]
    public static extern bool gtk_init_check(ref int argc, ref IntPtr argv);

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_window_new(int windowType);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_window_set_title(IntPtr window, string title);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_window_set_default_size(IntPtr window, int width, int height);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_window_resize(IntPtr window, int width, int height);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_window_move(IntPtr window, int x, int y);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_window_get_size(IntPtr window, out int width, out int height);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_window_get_position(IntPtr window, out int x, out int y);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_window_set_icon(IntPtr window, IntPtr pixbuf);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_window_set_icon_from_file(IntPtr window, string filename, IntPtr error);

    [DllImport("libgdk_pixbuf-2.0.so.0")]
    public static extern IntPtr gdk_pixbuf_new_from_file(string filename, IntPtr error);

    [DllImport("libgdk_pixbuf-2.0.so.0")]
    public static extern IntPtr gdk_pixbuf_new_from_data(IntPtr data, int colorspace, bool hasAlpha, int bitsPerSample, int width, int height, int rowstride, IntPtr destroyFn, IntPtr destroyFnData);

    [DllImport("libgdk_pixbuf-2.0.so.0")]
    public static extern void g_object_unref(IntPtr obj);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_widget_show_all(IntPtr widget);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_widget_show(IntPtr widget);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_widget_hide(IntPtr widget);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_widget_destroy(IntPtr widget);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_widget_queue_draw(IntPtr widget);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_widget_set_size_request(IntPtr widget, int width, int height);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_widget_get_allocation(IntPtr widget, out GtkAllocation allocation);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_main();

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_main_quit();

    [DllImport("libgtk-3.so.0")]
    public static extern bool gtk_events_pending();

    [DllImport("libgtk-3.so.0")]
    public static extern bool gtk_main_iteration_do(bool blocking);

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_overlay_new();

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_container_add(IntPtr container, IntPtr widget);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_container_remove(IntPtr container, IntPtr widget);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_overlay_add_overlay(IntPtr overlay, IntPtr widget);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_overlay_set_overlay_pass_through(IntPtr overlay, IntPtr widget, bool passThrough);

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_fixed_new();

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_fixed_put(IntPtr fixedWidget, IntPtr widget, int x, int y);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_fixed_move(IntPtr fixedWidget, IntPtr widget, int x, int y);

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_drawing_area_new();

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_widget_set_can_focus(IntPtr widget, bool canFocus);

    [DllImport("libgtk-3.so.0")]
    public static extern bool gtk_widget_grab_focus(IntPtr widget);

    [DllImport("libgtk-3.so.0")]
    public static extern bool gtk_widget_has_focus(IntPtr widget);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_widget_add_events(IntPtr widget, int events);

    [DllImport("libgtk-3.so.0")]
    public static extern ulong g_signal_connect_data(IntPtr instance, string detailedSignal, IntPtr cHandler, IntPtr data, IntPtr destroyData, int connectFlags);

    [DllImport("libgtk-3.so.0")]
    public static extern void g_signal_handler_disconnect(IntPtr instance, ulong handlerId);

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_widget_get_window(IntPtr widget);

    [DllImport("libglib-2.0.so.0", EntryPoint = "g_idle_add")]
    public static extern uint IdleAdd(GSourceFunc function, IntPtr data);

    [DllImport("libglib-2.0.so.0", EntryPoint = "g_source_remove")]
    public static extern bool SourceRemove(uint sourceId);

    public static uint IdleAdd(Func<bool> callback)
    {
        GSourceFunc gSourceFunc = (IntPtr _) => callback();
        _idleCallbacks.Add(gSourceFunc);
        return IdleAdd(gSourceFunc, IntPtr.Zero);
    }

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_menu_new();

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_menu_item_new_with_label(string label);

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_separator_menu_item_new();

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_menu_shell_append(IntPtr menuShell, IntPtr child);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_menu_popup_at_pointer(IntPtr menu, IntPtr triggerEvent);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_widget_set_sensitive(IntPtr widget, bool sensitive);

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_get_current_event();

    [DllImport("libgdk-3.so.0")]
    public static extern void gdk_event_free(IntPtr eventPtr);

    // Message Dialog support
    public const int GTK_DIALOG_MODAL = 1;
    public const int GTK_DIALOG_DESTROY_WITH_PARENT = 2;

    public const int GTK_MESSAGE_INFO = 0;
    public const int GTK_MESSAGE_WARNING = 1;
    public const int GTK_MESSAGE_QUESTION = 2;
    public const int GTK_MESSAGE_ERROR = 3;
    public const int GTK_MESSAGE_OTHER = 4;

    public const int GTK_BUTTONS_NONE = 0;
    public const int GTK_BUTTONS_OK = 1;
    public const int GTK_BUTTONS_CLOSE = 2;
    public const int GTK_BUTTONS_CANCEL = 3;
    public const int GTK_BUTTONS_YES_NO = 4;
    public const int GTK_BUTTONS_OK_CANCEL = 5;

    public const int GTK_RESPONSE_NONE = -1;
    public const int GTK_RESPONSE_REJECT = -2;
    public const int GTK_RESPONSE_ACCEPT = -3;
    public const int GTK_RESPONSE_DELETE_EVENT = -4;
    public const int GTK_RESPONSE_OK = -5;
    public const int GTK_RESPONSE_CANCEL = -6;
    public const int GTK_RESPONSE_CLOSE = -7;
    public const int GTK_RESPONSE_YES = -8;
    public const int GTK_RESPONSE_NO = -9;

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_message_dialog_new(
        IntPtr parent,
        int flags,
        int type,
        int buttons,
        string message,
        IntPtr args);

    [DllImport("libgtk-3.so.0")]
    public static extern int gtk_dialog_run(IntPtr dialog);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_window_set_transient_for(IntPtr window, IntPtr parent);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_window_set_modal(IntPtr window, bool modal);

    // CSS styling for dialogs
    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_css_provider_new();

    [DllImport("libgtk-3.so.0")]
    public static extern bool gtk_css_provider_load_from_data(IntPtr provider, string data, int length, IntPtr error);

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_widget_get_style_context(IntPtr widget);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_style_context_add_provider(IntPtr context, IntPtr provider, uint priority);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_style_context_add_provider_for_screen(IntPtr screen, IntPtr provider, uint priority);

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_widget_get_screen(IntPtr widget);

    [DllImport("libgdk-3.so.0")]
    public static extern IntPtr gdk_screen_get_default();

    public const uint GTK_STYLE_PROVIDER_PRIORITY_APPLICATION = 600;
    public const uint GTK_STYLE_PROVIDER_PRIORITY_USER = 800;

    // Dialog with custom content (for prompt dialogs)
    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_dialog_new_with_buttons(
        string title,
        IntPtr parent,
        int flags,
        string firstButtonText,
        int firstButtonResponse,
        string secondButtonText,
        int secondButtonResponse,
        IntPtr terminator);

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_dialog_get_content_area(IntPtr dialog);

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_box_new(int orientation, int spacing);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_box_pack_start(IntPtr box, IntPtr child, bool expand, bool fill, uint padding);

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_label_new(string text);

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_entry_new();

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_entry_set_text(IntPtr entry, string text);

    [DllImport("libgtk-3.so.0")]
    public static extern IntPtr gtk_entry_get_text(IntPtr entry);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_widget_set_margin_start(IntPtr widget, int margin);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_widget_set_margin_end(IntPtr widget, int margin);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_widget_set_margin_top(IntPtr widget, int margin);

    [DllImport("libgtk-3.so.0")]
    public static extern void gtk_widget_set_margin_bottom(IntPtr widget, int margin);

    public const int GTK_ORIENTATION_HORIZONTAL = 0;
    public const int GTK_ORIENTATION_VERTICAL = 1;
}
