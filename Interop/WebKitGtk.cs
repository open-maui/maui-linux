// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Interop;

/// <summary>
/// P/Invoke bindings for WebKitGTK library.
/// WebKitGTK provides a full-featured web browser engine for Linux.
/// </summary>
public static class WebKitGtk
{
    private const string WebKit2Lib = "libwebkit2gtk-4.1.so.0";
    private const string GtkLib = "libgtk-3.so.0";
    private const string GObjectLib = "libgobject-2.0.so.0";
    private const string GLibLib = "libglib-2.0.so.0";

    #region GTK Initialization

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool gtk_init_check(ref int argc, ref IntPtr argv);

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gtk_main();

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gtk_main_quit();

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool gtk_events_pending();

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gtk_main_iteration();

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool gtk_main_iteration_do(bool blocking);

    #endregion

    #region GTK Window

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr gtk_window_new(int type);

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gtk_window_set_default_size(IntPtr window, int width, int height);

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gtk_window_set_decorated(IntPtr window, bool decorated);

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gtk_window_move(IntPtr window, int x, int y);

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gtk_window_resize(IntPtr window, int width, int height);

    #endregion

    #region GTK Widget

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gtk_widget_show_all(IntPtr widget);

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gtk_widget_show(IntPtr widget);

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gtk_widget_hide(IntPtr widget);

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gtk_widget_destroy(IntPtr widget);

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gtk_widget_set_size_request(IntPtr widget, int width, int height);

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gtk_widget_realize(IntPtr widget);

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr gtk_widget_get_window(IntPtr widget);

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gtk_widget_set_can_focus(IntPtr widget, bool canFocus);

    #endregion

    #region GTK Container

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gtk_container_add(IntPtr container, IntPtr widget);

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gtk_container_remove(IntPtr container, IntPtr widget);

    #endregion

    #region GTK Plug (for embedding in X11 windows)

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr gtk_plug_new(ulong socketId);

    [DllImport(GtkLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong gtk_plug_get_id(IntPtr plug);

    #endregion

    #region WebKitWebView

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr webkit_web_view_new();

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr webkit_web_view_new_with_context(IntPtr context);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_web_view_load_uri(IntPtr webView, [MarshalAs(UnmanagedType.LPUTF8Str)] string uri);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_web_view_load_html(IntPtr webView,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string content,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? baseUri);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_web_view_reload(IntPtr webView);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_web_view_stop_loading(IntPtr webView);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_web_view_go_back(IntPtr webView);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_web_view_go_forward(IntPtr webView);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool webkit_web_view_can_go_back(IntPtr webView);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool webkit_web_view_can_go_forward(IntPtr webView);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr webkit_web_view_get_uri(IntPtr webView);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr webkit_web_view_get_title(IntPtr webView);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern double webkit_web_view_get_estimated_load_progress(IntPtr webView);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool webkit_web_view_is_loading(IntPtr webView);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_web_view_run_javascript(IntPtr webView,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string script,
        IntPtr cancellable,
        IntPtr callback,
        IntPtr userData);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr webkit_web_view_run_javascript_finish(IntPtr webView,
        IntPtr result,
        out IntPtr error);

    #endregion

    #region WebKitSettings

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr webkit_web_view_get_settings(IntPtr webView);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_settings_set_enable_javascript(IntPtr settings, bool enabled);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_settings_set_user_agent(IntPtr settings,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string userAgent);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr webkit_settings_get_user_agent(IntPtr settings);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_settings_set_enable_developer_extras(IntPtr settings, bool enabled);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_settings_set_javascript_can_access_clipboard(IntPtr settings, bool enabled);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_settings_set_enable_webgl(IntPtr settings, bool enabled);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_settings_set_allow_file_access_from_file_urls(IntPtr settings, bool enabled);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_settings_set_allow_universal_access_from_file_urls(IntPtr settings, bool enabled);

    #endregion

    #region WebKitWebContext

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr webkit_web_context_get_default();

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr webkit_web_context_new();

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr webkit_web_context_get_cookie_manager(IntPtr context);

    #endregion

    #region WebKitCookieManager

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_cookie_manager_set_accept_policy(IntPtr cookieManager, int policy);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_cookie_manager_set_persistent_storage(IntPtr cookieManager,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string filename,
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

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr webkit_navigation_action_get_request(IntPtr action);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int webkit_navigation_action_get_navigation_type(IntPtr action);

    #endregion

    #region WebKitURIRequest

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr webkit_uri_request_get_uri(IntPtr request);

    #endregion

    #region WebKitPolicyDecision

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_policy_decision_use(IntPtr decision);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_policy_decision_ignore(IntPtr decision);

    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void webkit_policy_decision_download(IntPtr decision);

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

    [DllImport(GObjectLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong g_signal_connect_data(IntPtr instance,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string detailedSignal,
        Delegate handler,
        IntPtr data,
        IntPtr destroyData,
        int connectFlags);

    [DllImport(GObjectLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void g_signal_handler_disconnect(IntPtr instance, ulong handlerId);

    [DllImport(GObjectLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void g_object_unref(IntPtr obj);

    #endregion

    #region GLib Memory

    [DllImport(GLibLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void g_free(IntPtr mem);

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
