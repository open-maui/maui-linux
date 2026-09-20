// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Native;

/// <summary>
/// WPE WebKit 2.54+ (WPEPlatform API) bindings for the composited WebView:
/// headless display, view, rendered-buffer import, input events, and the
/// WebKitWebView surface the view drives. Only the WPEPlatform API is bound;
/// the deprecated libwpe/FDO backend is not used. Availability is probed with
/// <see cref="IsAvailable"/> so a machine without WPE falls back cleanly.
/// </summary>
internal static partial class WpeNative
{
    public const string LibWpeWebKit = "libWPEWebKit-2.0.so.1";
    private const string LibGObject = "libgobject-2.0.so.0";
    private const string LibGLib = "libglib-2.0.so.0";
    private const string LibGio = "libgio-2.0.so.0";

    private static readonly Lazy<bool> s_available = new(() =>
    {
        try
        {
            return NativeLibrary.TryLoad(LibWpeWebKit, out var h) && h != IntPtr.Zero;
        }
        catch
        {
            return false;
        }
    });

    /// <summary>True when libWPEWebKit-2.0 can be loaded on this machine.</summary>
    public static bool IsAvailable => s_available.Value;

    // WPEEventType
    public const int WPE_EVENT_POINTER_DOWN = 1;
    public const int WPE_EVENT_POINTER_UP = 2;
    public const int WPE_EVENT_POINTER_MOVE = 3;
    public const int WPE_EVENT_POINTER_ENTER = 4;
    public const int WPE_EVENT_POINTER_LEAVE = 5;
    public const int WPE_EVENT_SCROLL = 6;
    public const int WPE_EVENT_KEYBOARD_KEY_DOWN = 7;
    public const int WPE_EVENT_KEYBOARD_KEY_UP = 8;

    // WPEInputSource
    public const int WPE_INPUT_SOURCE_MOUSE = 0;
    public const int WPE_INPUT_SOURCE_KEYBOARD = 2;

    // WPEModifiers
    public const uint WPE_MODIFIER_KEYBOARD_CONTROL = 1u << 0;
    public const uint WPE_MODIFIER_KEYBOARD_SHIFT = 1u << 1;
    public const uint WPE_MODIFIER_KEYBOARD_ALT = 1u << 2;
    public const uint WPE_MODIFIER_KEYBOARD_META = 1u << 3;
    public const uint WPE_MODIFIER_KEYBOARD_CAPS_LOCK = 1u << 4;
    public const uint WPE_MODIFIER_POINTER_BUTTON1 = 1u << 8;
    public const uint WPE_MODIFIER_POINTER_BUTTON2 = 1u << 9;
    public const uint WPE_MODIFIER_POINTER_BUTTON3 = 1u << 10;

    // Buttons
    public const uint WPE_BUTTON_PRIMARY = 1;
    public const uint WPE_BUTTON_MIDDLE = 2;
    public const uint WPE_BUTTON_SECONDARY = 3;

    // WebKitLoadEvent
    public const int WEBKIT_LOAD_STARTED = 0;
    public const int WEBKIT_LOAD_REDIRECTED = 1;
    public const int WEBKIT_LOAD_COMMITTED = 2;
    public const int WEBKIT_LOAD_FINISHED = 3;

    // WebKitPolicyDecisionType
    public const int WEBKIT_POLICY_DECISION_TYPE_NAVIGATION_ACTION = 0;
    public const int WEBKIT_POLICY_DECISION_TYPE_NEW_WINDOW_ACTION = 1;
    public const int WEBKIT_POLICY_DECISION_TYPE_RESPONSE = 2;

    #region WPEPlatform: display / view / buffers

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr wpe_display_headless_new();

    [LibraryImport(LibWpeWebKit, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr wpe_display_headless_new_for_device(string name, out IntPtr error);

    [LibraryImport(LibWpeWebKit)]
    public static partial int wpe_display_connect(IntPtr display, out IntPtr error);

    [LibraryImport(LibWpeWebKit)]
    public static partial void wpe_view_resized(IntPtr view, int width, int height);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr wpe_view_get_toplevel(IntPtr view);

    [LibraryImport(LibWpeWebKit)]
    public static partial void wpe_toplevel_resized(IntPtr toplevel, int width, int height);

    [LibraryImport(LibWpeWebKit)]
    public static partial void wpe_toplevel_scale_changed(IntPtr toplevel, double scale);

    [LibraryImport(LibWpeWebKit)]
    public static partial double wpe_view_get_scale(IntPtr view);

    [LibraryImport(LibWpeWebKit)]
    public static partial void wpe_view_buffer_released(IntPtr view, IntPtr buffer);

    [LibraryImport(LibWpeWebKit)]
    public static partial void wpe_view_event(IntPtr view, IntPtr evt);

    [LibraryImport(LibWpeWebKit)]
    public static partial void wpe_view_focus_in(IntPtr view);

    [LibraryImport(LibWpeWebKit)]
    public static partial void wpe_view_focus_out(IntPtr view);

    [LibraryImport(LibWpeWebKit)]
    public static partial int wpe_buffer_get_width(IntPtr buffer);

    [LibraryImport(LibWpeWebKit)]
    public static partial int wpe_buffer_get_height(IntPtr buffer);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr wpe_buffer_import_to_pixels(IntPtr buffer, out IntPtr error);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr wpe_buffer_import_to_egl_image(IntPtr buffer, out IntPtr error);

    // Clipboard: the headless platform's WPEClipboard is an in-process store;
    // WpeWebView bridges it to the system clipboard.
    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr wpe_display_get_clipboard(IntPtr display);

    [LibraryImport(LibWpeWebKit)]
    public static partial long wpe_clipboard_get_change_count(IntPtr clipboard);

    [LibraryImport(LibWpeWebKit, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr wpe_clipboard_read_text(IntPtr clipboard, string format, out nuint size);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr wpe_clipboard_content_new();

    [LibraryImport(LibWpeWebKit, StringMarshalling = StringMarshalling.Utf8)]
    public static partial void wpe_clipboard_content_set_text(IntPtr content, string text);

    [LibraryImport(LibWpeWebKit)]
    public static partial void wpe_clipboard_content_unref(IntPtr content);

    [LibraryImport(LibWpeWebKit)]
    public static partial void wpe_clipboard_set_content(IntPtr clipboard, IntPtr content);

    #endregion

    #region WPEPlatform: events

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr wpe_event_pointer_button_new(int type, IntPtr view, int source, uint time, uint modifiers, uint button, double x, double y, uint pressCount);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr wpe_event_pointer_move_new(int type, IntPtr view, int source, uint time, uint modifiers, double x, double y, double deltaX, double deltaY);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr wpe_event_scroll_new(IntPtr view, int source, uint time, uint modifiers, double deltaX, double deltaY, int preciseDeltas, int isStop, double x, double y);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr wpe_event_keyboard_new(int type, IntPtr view, int source, uint time, uint modifiers, uint keycode, uint keyval);

    [LibraryImport(LibWpeWebKit)]
    public static partial void wpe_event_unref(IntPtr evt);

    #endregion

    #region WebKitWebView (WPE port)

    [LibraryImport(LibWpeWebKit)]
    public static partial nuint webkit_web_view_get_type();

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr webkit_web_view_get_wpe_view(IntPtr webView);

    [LibraryImport(LibWpeWebKit, StringMarshalling = StringMarshalling.Utf8)]
    public static partial void webkit_web_view_load_uri(IntPtr webView, string uri);

    [LibraryImport(LibWpeWebKit, StringMarshalling = StringMarshalling.Utf8)]
    public static partial void webkit_web_view_load_html(IntPtr webView, string content, string? baseUri);

    [LibraryImport(LibWpeWebKit)]
    public static partial void webkit_web_view_reload(IntPtr webView);

    [LibraryImport(LibWpeWebKit)]
    public static partial void webkit_web_view_stop_loading(IntPtr webView);

    [LibraryImport(LibWpeWebKit)]
    public static partial void webkit_web_view_go_back(IntPtr webView);

    [LibraryImport(LibWpeWebKit)]
    public static partial void webkit_web_view_go_forward(IntPtr webView);

    [LibraryImport(LibWpeWebKit)]
    public static partial int webkit_web_view_can_go_back(IntPtr webView);

    [LibraryImport(LibWpeWebKit)]
    public static partial int webkit_web_view_can_go_forward(IntPtr webView);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr webkit_web_view_get_uri(IntPtr webView);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr webkit_web_view_get_title(IntPtr webView);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr webkit_web_view_get_settings(IntPtr webView);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr webkit_web_view_get_context(IntPtr webView);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr webkit_web_view_get_user_content_manager(IntPtr webView);

    [LibraryImport(LibWpeWebKit)]
    public static partial void webkit_web_view_set_background_color(IntPtr webView, ref WebKitColor color);

    [LibraryImport(LibWpeWebKit, StringMarshalling = StringMarshalling.Utf8)]
    public static partial void webkit_settings_set_user_agent(IntPtr settings, string? userAgent);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr webkit_settings_get_user_agent(IntPtr settings);

    [LibraryImport(LibWpeWebKit)]
    public static partial void webkit_settings_set_enable_developer_extras(IntPtr settings, int enabled);

    [LibraryImport(LibWpeWebKit)]
    public static partial void webkit_settings_set_enable_webgl(IntPtr settings, int enabled);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr webkit_navigation_policy_decision_get_navigation_action(IntPtr decision);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr webkit_navigation_action_get_request(IntPtr action);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr webkit_uri_request_get_uri(IntPtr request);

    [LibraryImport(LibWpeWebKit)]
    public static partial void webkit_policy_decision_use(IntPtr decision);

    [LibraryImport(LibWpeWebKit)]
    public static partial void webkit_policy_decision_ignore(IntPtr decision);

    // Context menus: WPE emits the model and leaves display to the embedder.
    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr webkit_context_menu_get_items(IntPtr menu);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr webkit_context_menu_item_get_title(IntPtr item);

    [LibraryImport(LibWpeWebKit)]
    public static partial int webkit_context_menu_item_is_separator(IntPtr item);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr webkit_context_menu_item_get_gaction(IntPtr item);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr webkit_context_menu_item_get_gaction_target(IntPtr item);

    [LibraryImport(LibWpeWebKit)]
    public static partial IntPtr webkit_context_menu_item_get_submenu(IntPtr item);

    [LibraryImport(LibGio)]
    public static partial void g_action_activate(IntPtr action, IntPtr parameter);

    [LibraryImport(LibGio)]
    public static partial int g_action_get_enabled(IntPtr action);

    [LibraryImport(LibGObject)]
    public static partial IntPtr g_object_ref(IntPtr obj);

    [LibraryImport(LibGLib)]
    public static partial IntPtr g_variant_ref(IntPtr variant);

    [LibraryImport(LibGLib)]
    public static partial void g_variant_unref(IntPtr variant);

    [StructLayout(LayoutKind.Sequential)]
    public struct WebKitColor
    {
        public double Red, Green, Blue, Alpha;
    }

    #endregion

    #region GObject / GLib

    [LibraryImport(LibGObject, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr g_object_new(nuint objectType, string firstProperty, IntPtr value, IntPtr terminator);

    [LibraryImport(LibGObject)]
    public static partial void g_object_unref(IntPtr obj);

    [LibraryImport(LibGObject)]
    public static partial IntPtr g_object_ref_sink(IntPtr obj);

    [LibraryImport(LibGObject, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nuint g_signal_connect_data(IntPtr instance, string detailedSignal, IntPtr handler, IntPtr data, IntPtr destroyData, int connectFlags);

    [LibraryImport(LibGObject)]
    public static partial void g_signal_handler_disconnect(IntPtr instance, nuint handlerId);

    [LibraryImport(LibGLib)]
    public static partial IntPtr g_bytes_get_data(IntPtr bytes, out nuint size);

    [LibraryImport(LibGLib)]
    public static partial void g_bytes_unref(IntPtr bytes);

    [LibraryImport(LibGLib)]
    public static partial void g_error_free(IntPtr error);

    [LibraryImport(LibGLib)]
    public static partial void g_free(IntPtr mem);

    /// <summary>Reads GError.message (offset: GQuark domain (4) + gint code (4) + pointer) and frees the error.</summary>
    public static string ConsumeError(IntPtr error, string fallback)
    {
        if (error == IntPtr.Zero) return fallback;
        var messagePtr = Marshal.ReadIntPtr(error, 8);
        var message = messagePtr == IntPtr.Zero ? fallback : (Marshal.PtrToStringUTF8(messagePtr) ?? fallback);
        g_error_free(error);
        return message;
    }

    public static string? PtrToString(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);

    #endregion
}
