// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Interop;

/// <summary>
/// P/Invoke bindings for WebKitGTK library.
/// WebKitGTK provides a full-featured web browser engine for Linux.
/// </summary>
public static partial class WebKitGtk
{
    private const string WebKit2Lib = "libwebkit2gtk-4.1.so.0";
    private const string GtkLib = "libgtk-3.so.0";
    private const string GObjectLib = "libgobject-2.0.so.0";
    private const string GLibLib = "libglib-2.0.so.0";

    #region GTK Initialization

    [LibraryImport(GtkLib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool gtk_init_check(ref int argc, ref IntPtr argv);

    [LibraryImport(GtkLib)]
    public static partial void gtk_main();

    [LibraryImport(GtkLib)]
    public static partial void gtk_main_quit();

    [LibraryImport(GtkLib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool gtk_events_pending();

    [LibraryImport(GtkLib)]
    public static partial void gtk_main_iteration();

    [LibraryImport(GtkLib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool gtk_main_iteration_do([MarshalAs(UnmanagedType.Bool)] bool blocking);

    #endregion

    #region GTK Window

    [LibraryImport(GtkLib)]
    public static partial IntPtr gtk_window_new(int type);

    [LibraryImport(GtkLib)]
    public static partial void gtk_window_set_default_size(IntPtr window, int width, int height);

    [LibraryImport(GtkLib)]
    public static partial void gtk_window_set_decorated(IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool decorated);

    [LibraryImport(GtkLib)]
    public static partial void gtk_window_move(IntPtr window, int x, int y);

    [LibraryImport(GtkLib)]
    public static partial void gtk_window_resize(IntPtr window, int width, int height);

    #endregion

    #region GTK Widget

    [LibraryImport(GtkLib)]
    public static partial void gtk_widget_show_all(IntPtr widget);

    [LibraryImport(GtkLib)]
    public static partial void gtk_widget_show(IntPtr widget);

    [LibraryImport(GtkLib)]
    public static partial void gtk_widget_hide(IntPtr widget);

    [LibraryImport(GtkLib)]
    public static partial void gtk_widget_destroy(IntPtr widget);

    [LibraryImport(GtkLib)]
    public static partial void gtk_widget_set_size_request(IntPtr widget, int width, int height);

    [LibraryImport(GtkLib)]
    public static partial void gtk_widget_realize(IntPtr widget);

    [LibraryImport(GtkLib)]
    public static partial IntPtr gtk_widget_get_window(IntPtr widget);

    [LibraryImport(GtkLib)]
    public static partial void gtk_widget_set_can_focus(IntPtr widget, [MarshalAs(UnmanagedType.Bool)] bool canFocus);

    #endregion

    #region GTK Container

    [LibraryImport(GtkLib)]
    public static partial void gtk_container_add(IntPtr container, IntPtr widget);

    [LibraryImport(GtkLib)]
    public static partial void gtk_container_remove(IntPtr container, IntPtr widget);

    #endregion

    #region GTK Plug (for embedding in X11 windows)

    [LibraryImport(GtkLib)]
    public static partial IntPtr gtk_plug_new(ulong socketId);

    [LibraryImport(GtkLib)]
    public static partial ulong gtk_plug_get_id(IntPtr plug);

    #endregion

    #region WebKitWebView

    [LibraryImport(WebKit2Lib)]
    public static partial IntPtr webkit_web_view_new();

    [LibraryImport(WebKit2Lib)]
    public static partial IntPtr webkit_web_view_new_with_context(IntPtr context);

    [LibraryImport(WebKit2Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial void webkit_web_view_load_uri(IntPtr webView, string uri);

    [LibraryImport(WebKit2Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial void webkit_web_view_load_html(IntPtr webView,
        string content,
        string? baseUri);

    [LibraryImport(WebKit2Lib)]
    public static partial void webkit_web_view_reload(IntPtr webView);

    [LibraryImport(WebKit2Lib)]
    public static partial void webkit_web_view_stop_loading(IntPtr webView);

    [LibraryImport(WebKit2Lib)]
    public static partial void webkit_web_view_go_back(IntPtr webView);

    [LibraryImport(WebKit2Lib)]
    public static partial void webkit_web_view_go_forward(IntPtr webView);

    [LibraryImport(WebKit2Lib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool webkit_web_view_can_go_back(IntPtr webView);

    [LibraryImport(WebKit2Lib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool webkit_web_view_can_go_forward(IntPtr webView);

    [LibraryImport(WebKit2Lib)]
    public static partial IntPtr webkit_web_view_get_uri(IntPtr webView);

    [LibraryImport(WebKit2Lib)]
    public static partial IntPtr webkit_web_view_get_title(IntPtr webView);

    [LibraryImport(WebKit2Lib)]
    public static partial double webkit_web_view_get_estimated_load_progress(IntPtr webView);

    [LibraryImport(WebKit2Lib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool webkit_web_view_is_loading(IntPtr webView);

    [LibraryImport(WebKit2Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial void webkit_web_view_run_javascript(IntPtr webView,
        string script,
        IntPtr cancellable,
        IntPtr callback,
        IntPtr userData);

    [LibraryImport(WebKit2Lib)]
    public static partial IntPtr webkit_web_view_run_javascript_finish(IntPtr webView,
        IntPtr result,
        out IntPtr error);

    #endregion

    #region WebKitSettings

    [LibraryImport(WebKit2Lib)]
    public static partial IntPtr webkit_web_view_get_settings(IntPtr webView);

    [LibraryImport(WebKit2Lib)]
    public static partial void webkit_settings_set_enable_javascript(IntPtr settings, [MarshalAs(UnmanagedType.Bool)] bool enabled);

    [LibraryImport(WebKit2Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial void webkit_settings_set_user_agent(IntPtr settings,
        string userAgent);

    [LibraryImport(WebKit2Lib)]
    public static partial IntPtr webkit_settings_get_user_agent(IntPtr settings);

    [LibraryImport(WebKit2Lib)]
    public static partial void webkit_settings_set_enable_developer_extras(IntPtr settings, [MarshalAs(UnmanagedType.Bool)] bool enabled);

    [LibraryImport(WebKit2Lib)]
    public static partial void webkit_settings_set_javascript_can_access_clipboard(IntPtr settings, [MarshalAs(UnmanagedType.Bool)] bool enabled);

    [LibraryImport(WebKit2Lib)]
    public static partial void webkit_settings_set_enable_webgl(IntPtr settings, [MarshalAs(UnmanagedType.Bool)] bool enabled);

    [LibraryImport(WebKit2Lib)]
    public static partial void webkit_settings_set_allow_file_access_from_file_urls(IntPtr settings, [MarshalAs(UnmanagedType.Bool)] bool enabled);

    [LibraryImport(WebKit2Lib)]
    public static partial void webkit_settings_set_allow_universal_access_from_file_urls(IntPtr settings, [MarshalAs(UnmanagedType.Bool)] bool enabled);

    #endregion

    #region WebKitWebContext

    [LibraryImport(WebKit2Lib)]
    public static partial IntPtr webkit_web_context_get_default();

    [LibraryImport(WebKit2Lib)]
    public static partial IntPtr webkit_web_context_new();

    [LibraryImport(WebKit2Lib)]
    public static partial IntPtr webkit_web_context_get_cookie_manager(IntPtr context);

    #endregion

    #region WebKitCookieManager

    [LibraryImport(WebKit2Lib)]
    public static partial void webkit_cookie_manager_set_accept_policy(IntPtr cookieManager, int policy);

    [LibraryImport(WebKit2Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial void webkit_cookie_manager_set_persistent_storage(IntPtr cookieManager,
        string filename,
        int storage);

    // Cookie accept policies
    public const int WEBKIT_COOKIE_POLICY_ACCEPT_ALWAYS = 0;
    public const int WEBKIT_COOKIE_POLICY_ACCEPT_NEVER = 1;
    public const int WEBKIT_COOKIE_POLICY_ACCEPT_NO_THIRD_PARTY = 2;

    // Cookie persistent storage types
    public const int WEBKIT_COOKIE_PERSISTENT_STORAGE_TEXT = 0;
    public const int WEBKIT_COOKIE_PERSISTENT_STORAGE_SQLITE = 1;

    #endregion

    #region WebKitNavigationAction

    [LibraryImport(WebKit2Lib)]
    public static partial IntPtr webkit_navigation_action_get_request(IntPtr action);

    [LibraryImport(WebKit2Lib)]
    public static partial int webkit_navigation_action_get_navigation_type(IntPtr action);

    #endregion

    #region WebKitURIRequest

    [LibraryImport(WebKit2Lib)]
    public static partial IntPtr webkit_uri_request_get_uri(IntPtr request);

    #endregion

    #region WebKitPolicyDecision

    [LibraryImport(WebKit2Lib)]
    public static partial void webkit_policy_decision_use(IntPtr decision);

    [LibraryImport(WebKit2Lib)]
    public static partial void webkit_policy_decision_ignore(IntPtr decision);

    [LibraryImport(WebKit2Lib)]
    public static partial void webkit_policy_decision_download(IntPtr decision);

    #endregion

    #region GObject Signal Connection

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void GCallback();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LoadChangedCallback(IntPtr webView, int loadEvent, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool DecidePolicyCallback(IntPtr webView, IntPtr decision, int decisionType, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LoadFailedCallback(IntPtr webView, int loadEvent, IntPtr failingUri, IntPtr error, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void NotifyCallback(IntPtr webView, IntPtr paramSpec, IntPtr userData);

    // g_signal_connect_data has a Delegate parameter - complex marshalling, kept as DllImport
    [DllImport(GObjectLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong g_signal_connect_data(IntPtr instance,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string detailedSignal,
        Delegate handler,
        IntPtr data,
        IntPtr destroyData,
        int connectFlags);

    [LibraryImport(GObjectLib)]
    public static partial void g_signal_handler_disconnect(IntPtr instance, ulong handlerId);

    [LibraryImport(GObjectLib)]
    public static partial void g_object_unref(IntPtr obj);

    #endregion

    #region GLib Memory

    [LibraryImport(GLibLib)]
    public static partial void g_free(IntPtr mem);

    #endregion

    #region WebKit Load Events

    public const int WEBKIT_LOAD_STARTED = 0;
    public const int WEBKIT_LOAD_REDIRECTED = 1;
    public const int WEBKIT_LOAD_COMMITTED = 2;
    public const int WEBKIT_LOAD_FINISHED = 3;

    #endregion

    #region WebKit Policy Decision Types

    public const int WEBKIT_POLICY_DECISION_TYPE_NAVIGATION_ACTION = 0;
    public const int WEBKIT_POLICY_DECISION_TYPE_NEW_WINDOW_ACTION = 1;
    public const int WEBKIT_POLICY_DECISION_TYPE_RESPONSE = 2;

    #endregion

    #region Helper Methods

    /// <summary>
    /// Converts a native UTF-8 string pointer to a managed string.
    /// </summary>
    public static string? PtrToStringUtf8(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            return null;
        return Marshal.PtrToStringUTF8(ptr);
    }

    /// <summary>
    /// Processes pending GTK events without blocking.
    /// </summary>
    public static void ProcessGtkEvents()
    {
        while (gtk_events_pending())
        {
            gtk_main_iteration_do(false);
        }
    }

    #endregion
}
