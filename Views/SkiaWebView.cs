// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// WebView implementation using WebKitGTK for Linux.
/// Renders web content in a native GTK window embedded via X11.
/// </summary>
public class SkiaWebView : SkiaView
{
    #region Delegates

    private delegate void LoadChangedCallback(IntPtr webView, int loadEvent, IntPtr userData);
    private delegate IntPtr WebKitWebViewNewDelegate();
    private delegate void WebKitWebViewLoadUriDelegate(IntPtr webView, [MarshalAs(UnmanagedType.LPStr)] string uri);
    private delegate void WebKitWebViewLoadHtmlDelegate(IntPtr webView, [MarshalAs(UnmanagedType.LPStr)] string html, [MarshalAs(UnmanagedType.LPStr)] string? baseUri);
    private delegate IntPtr WebKitWebViewGetUriDelegate(IntPtr webView);
    private delegate IntPtr WebKitWebViewGetTitleDelegate(IntPtr webView);
    private delegate void WebKitWebViewGoBackDelegate(IntPtr webView);
    private delegate void WebKitWebViewGoForwardDelegate(IntPtr webView);
    private delegate bool WebKitWebViewCanGoBackDelegate(IntPtr webView);
    private delegate bool WebKitWebViewCanGoForwardDelegate(IntPtr webView);
    private delegate void WebKitWebViewReloadDelegate(IntPtr webView);
    private delegate void WebKitWebViewStopLoadingDelegate(IntPtr webView);
    private delegate double WebKitWebViewGetEstimatedLoadProgressDelegate(IntPtr webView);
    private delegate IntPtr WebKitWebViewGetSettingsDelegate(IntPtr webView);
    private delegate void WebKitSettingsSetEnableJavascriptDelegate(IntPtr settings, bool enabled);
    private delegate void WebKitSettingsSetHardwareAccelerationPolicyDelegate(IntPtr settings, int policy);
    private delegate void WebKitSettingsSetEnableWebglDelegate(IntPtr settings, bool enabled);

    #endregion

    #region X11 Structures

    private struct XWindowAttributes
    {
        public int x;
        public int y;
        public int width;
        public int height;
        public int border_width;
        public int depth;
        public IntPtr visual;
        public IntPtr root;
        public int c_class;
        public int bit_gravity;
        public int win_gravity;
        public int backing_store;
        public ulong backing_planes;
        public ulong backing_pixel;
        public int save_under;
        public IntPtr colormap;
        public int map_installed;
        public int map_state;
        public long all_event_masks;
        public long your_event_mask;
        public long do_not_propagate_mask;
        public int override_redirect;
        public IntPtr screen;
    }

    #endregion

    #region Constants

    private const string LibGtk4 = "libgtk-4.so.1";
    private const string LibGtk3 = "libgtk-3.so.0";
    private const string LibWebKit2Gtk4 = "libwebkitgtk-6.0.so.4";
    private const string LibWebKit2Gtk3 = "libwebkit2gtk-4.1.so.0";
    private const string LibGObject = "libgobject-2.0.so.0";
    private const string LibGLib = "libglib-2.0.so.0";
    private const string LibGdk4 = "libgtk-4.so.1";
    private const string LibGdk3 = "libgdk-3.so.0";
    private const string LibX11 = "libX11.so.6";
    private const int RTLD_NOW = 2;
    private const int RTLD_GLOBAL = 256;

    #endregion

    #region Static Fields

    private static bool _useGtk4;
    private static bool _gtkInitialized;
    private static string _webkitLib = LibWebKit2Gtk3;
    private static LoadChangedCallback? _loadChangedCallback;
    private static IntPtr _webkitHandle;
    private static IntPtr _mainDisplay;
    private static IntPtr _mainWindow;

    private static WebKitWebViewNewDelegate? _webkitWebViewNew;
    private static WebKitWebViewLoadUriDelegate? _webkitLoadUri;
    private static WebKitWebViewLoadHtmlDelegate? _webkitLoadHtml;
    private static WebKitWebViewGetUriDelegate? _webkitGetUri;
    private static WebKitWebViewGetTitleDelegate? _webkitGetTitle;
    private static WebKitWebViewGoBackDelegate? _webkitGoBack;
    private static WebKitWebViewGoForwardDelegate? _webkitGoForward;
    private static WebKitWebViewCanGoBackDelegate? _webkitCanGoBack;
    private static WebKitWebViewCanGoForwardDelegate? _webkitCanGoForward;
    private static WebKitWebViewReloadDelegate? _webkitReload;
    private static WebKitWebViewStopLoadingDelegate? _webkitStopLoading;
    private static WebKitWebViewGetEstimatedLoadProgressDelegate? _webkitGetProgress;
    private static WebKitWebViewGetSettingsDelegate? _webkitGetSettings;
    private static WebKitSettingsSetEnableJavascriptDelegate? _webkitSetJavascript;
    private static WebKitSettingsSetHardwareAccelerationPolicyDelegate? _webkitSetHardwareAcceleration;
    private static WebKitSettingsSetEnableWebglDelegate? _webkitSetWebgl;

    private static readonly List<SkiaWebView> _activeWebViews = new();
    private static readonly Dictionary<IntPtr, SkiaWebView> _webViewInstances = new();

    #endregion

    #region Instance Fields

    private IntPtr _gtkWindow;
    private IntPtr _webView;
    private IntPtr _gtkX11Window;
    private IntPtr _x11Container;
    private string _source = "";
    private string _html = "";
    private bool _isInitialized;
    private bool _isEmbedded;
    private bool _isProperlyReparented;
    private bool _javascriptEnabled = true;
    private double _loadProgress;
    private SKRect _lastBounds;
    private int _lastMainX;
    private int _lastMainY;
    private int _lastPosX;
    private int _lastPosY;
    private int _lastWidth;
    private int _lastHeight;

    #endregion

    #region GTK Native Imports

    [DllImport(LibGtk4, EntryPoint = "gtk_init")]
    private static extern void gtk4_init();

    [DllImport(LibGtk3, EntryPoint = "gtk_init_check")]
    private static extern bool gtk3_init_check(ref int argc, ref IntPtr argv);

    [DllImport(LibGtk4, EntryPoint = "gtk_window_new")]
    private static extern IntPtr gtk4_window_new();

    [DllImport(LibGtk3, EntryPoint = "gtk_window_new")]
    private static extern IntPtr gtk3_window_new(int type);

    [DllImport(LibGtk4, EntryPoint = "gtk_window_set_default_size")]
    private static extern void gtk4_window_set_default_size(IntPtr window, int width, int height);

    [DllImport(LibGtk4, EntryPoint = "gtk_window_set_title")]
    private static extern void gtk4_window_set_title(IntPtr window, [MarshalAs(UnmanagedType.LPStr)] string title);

    [DllImport(LibGtk3, EntryPoint = "gtk_window_set_default_size")]
    private static extern void gtk3_window_set_default_size(IntPtr window, int width, int height);

    [DllImport(LibGtk3, EntryPoint = "gtk_window_set_title")]
    private static extern void gtk3_window_set_title(IntPtr window, [MarshalAs(UnmanagedType.LPStr)] string title);

    [DllImport(LibGtk4, EntryPoint = "gtk_window_set_child")]
    private static extern void gtk4_window_set_child(IntPtr window, IntPtr child);

    [DllImport(LibGtk3, EntryPoint = "gtk_container_add")]
    private static extern void gtk3_container_add(IntPtr container, IntPtr widget);

    [DllImport(LibGtk4, EntryPoint = "gtk_widget_show")]
    private static extern void gtk4_widget_show(IntPtr widget);

    [DllImport(LibGtk4, EntryPoint = "gtk_window_present")]
    private static extern void gtk4_window_present(IntPtr window);

    [DllImport(LibGtk3, EntryPoint = "gtk_widget_show_all")]
    private static extern void gtk3_widget_show_all(IntPtr widget);

    [DllImport(LibGtk4, EntryPoint = "gtk_widget_hide")]
    private static extern void gtk4_widget_hide(IntPtr widget);

    [DllImport(LibGtk3, EntryPoint = "gtk_widget_hide")]
    private static extern void gtk3_widget_hide(IntPtr widget);

    [DllImport(LibGtk4, EntryPoint = "gtk_widget_get_width")]
    private static extern int gtk4_widget_get_width(IntPtr widget);

    [DllImport(LibGtk4, EntryPoint = "gtk_widget_get_height")]
    private static extern int gtk4_widget_get_height(IntPtr widget);

    [DllImport(LibGObject)]
    private static extern void g_object_unref(IntPtr obj);

    [DllImport(LibGObject)]
    private static extern ulong g_signal_connect_data(IntPtr instance, [MarshalAs(UnmanagedType.LPStr)] string signal, IntPtr handler, IntPtr data, IntPtr destroyData, int flags);

    [DllImport(LibGtk4, EntryPoint = "gtk_native_get_surface")]
    private static extern IntPtr gtk4_native_get_surface(IntPtr native);

    [DllImport(LibGtk4, EntryPoint = "gdk_x11_surface_get_xid")]
    private static extern IntPtr gdk4_x11_surface_get_xid(IntPtr surface);

    [DllImport(LibGtk3, EntryPoint = "gtk_widget_get_window")]
    private static extern IntPtr gtk3_widget_get_window(IntPtr widget);

    [DllImport(LibGdk3, EntryPoint = "gdk_x11_window_get_xid")]
    private static extern IntPtr gdk3_x11_window_get_xid(IntPtr gdkWindow);

    [DllImport(LibGtk4, EntryPoint = "gtk_window_set_decorated")]
    private static extern void gtk4_window_set_decorated(IntPtr window, bool decorated);

    [DllImport(LibGtk3, EntryPoint = "gtk_window_set_decorated")]
    private static extern void gtk3_window_set_decorated(IntPtr window, bool decorated);

    [DllImport(LibGtk3, EntryPoint = "gtk_window_move")]
    private static extern void gtk3_window_move(IntPtr window, int x, int y);

    [DllImport(LibGtk3, EntryPoint = "gtk_window_resize")]
    private static extern void gtk3_window_resize(IntPtr window, int width, int height);

    [DllImport(LibGdk3, EntryPoint = "gdk_window_move_resize")]
    private static extern void gdk3_window_move_resize(IntPtr window, int x, int y, int width, int height);

    [DllImport(LibGdk3, EntryPoint = "gdk_window_move")]
    private static extern void gdk3_gdk_window_move(IntPtr window, int x, int y);

    [DllImport(LibGdk3, EntryPoint = "gdk_window_set_override_redirect")]
    private static extern void gdk3_window_set_override_redirect(IntPtr window, bool override_redirect);

    [DllImport(LibGdk3, EntryPoint = "gdk_window_set_transient_for")]
    private static extern void gdk3_window_set_transient_for(IntPtr window, IntPtr parent);

    [DllImport(LibGdk3, EntryPoint = "gdk_window_raise")]
    private static extern void gdk3_window_raise(IntPtr window);

    [DllImport(LibGdk3, EntryPoint = "gdk_x11_window_foreign_new_for_display")]
    private static extern IntPtr gdk3_x11_window_foreign_new_for_display(IntPtr display, IntPtr window);

    [DllImport(LibGdk3, EntryPoint = "gdk_display_get_default")]
    private static extern IntPtr gdk3_display_get_default();

    [DllImport(LibGtk3, EntryPoint = "gtk_widget_set_parent_window")]
    private static extern void gtk3_widget_set_parent_window(IntPtr widget, IntPtr parentWindow);

    [DllImport(LibGtk3, EntryPoint = "gtk_socket_new")]
    private static extern IntPtr gtk3_socket_new();

    [DllImport(LibGtk3, EntryPoint = "gtk_socket_add_id")]
    private static extern void gtk3_socket_add_id(IntPtr socket, IntPtr windowId);

    [DllImport(LibGtk3, EntryPoint = "gtk_socket_get_id")]
    private static extern IntPtr gtk3_socket_get_id(IntPtr socket);

    [DllImport(LibGtk3, EntryPoint = "gtk_plug_new")]
    private static extern IntPtr gtk3_plug_new(IntPtr socketId);

    [DllImport(LibGtk3, EntryPoint = "gtk_plug_get_id")]
    private static extern IntPtr gtk3_plug_get_id(IntPtr plug);

    [DllImport(LibGtk3, EntryPoint = "gtk_window_set_skip_taskbar_hint")]
    private static extern void gtk3_window_set_skip_taskbar_hint(IntPtr window, bool setting);

    [DllImport(LibGtk3, EntryPoint = "gtk_window_set_skip_pager_hint")]
    private static extern void gtk3_window_set_skip_pager_hint(IntPtr window, bool setting);

    [DllImport(LibGtk3, EntryPoint = "gtk_window_set_type_hint")]
    private static extern void gtk3_window_set_type_hint(IntPtr window, int hint);

    [DllImport(LibGtk3, EntryPoint = "gtk_window_present")]
    private static extern void gtk3_window_present(IntPtr window);

    [DllImport(LibGtk3, EntryPoint = "gtk_widget_queue_draw")]
    private static extern void gtk3_widget_queue_draw(IntPtr widget);

    [DllImport(LibGtk3, EntryPoint = "gtk_window_set_keep_above")]
    private static extern void gtk3_window_set_keep_above(IntPtr window, bool setting);

    [DllImport(LibGtk3, EntryPoint = "gtk_widget_set_hexpand")]
    private static extern void gtk3_widget_set_hexpand(IntPtr widget, bool expand);

    [DllImport(LibGtk3, EntryPoint = "gtk_widget_set_vexpand")]
    private static extern void gtk3_widget_set_vexpand(IntPtr widget, bool expand);

    [DllImport(LibGtk3, EntryPoint = "gtk_widget_set_size_request")]
    private static extern void gtk3_widget_set_size_request(IntPtr widget, int width, int height);

    [DllImport(LibGtk3, EntryPoint = "gtk_widget_realize")]
    private static extern void gtk3_widget_realize(IntPtr widget);

    [DllImport(LibGtk3, EntryPoint = "gtk_widget_map")]
    private static extern void gtk3_widget_map(IntPtr widget);

    [DllImport(LibGLib)]
    private static extern bool g_main_context_iteration(IntPtr context, bool mayBlock);

    [DllImport(LibGtk3, EntryPoint = "gtk_events_pending")]
    private static extern bool gtk3_events_pending();

    [DllImport(LibGtk3, EntryPoint = "gtk_main_iteration")]
    private static extern void gtk3_main_iteration();

    [DllImport(LibGtk3, EntryPoint = "gtk_main_iteration_do")]
    private static extern bool gtk3_main_iteration_do(bool blocking);

    #endregion

    #region X11 Native Imports

    [DllImport(LibX11)]
    private static extern int XReparentWindow(IntPtr display, IntPtr window, IntPtr parent, int x, int y);

    [DllImport(LibX11)]
    private static extern int XMoveResizeWindow(IntPtr display, IntPtr window, int x, int y, uint width, uint height);

    [DllImport(LibX11)]
    private static extern int XMapWindow(IntPtr display, IntPtr window);

    [DllImport(LibX11)]
    private static extern int XUnmapWindow(IntPtr display, IntPtr window);

    [DllImport(LibX11)]
    private static extern int XFlush(IntPtr display);

    [DllImport(LibX11)]
    private static extern int XRaiseWindow(IntPtr display, IntPtr window);

    [DllImport(LibX11)]
    private static extern IntPtr XCreateSimpleWindow(IntPtr display, IntPtr parent, int x, int y, uint width, uint height, uint borderWidth, ulong border, ulong background);

    [DllImport(LibX11)]
    private static extern int XDestroyWindow(IntPtr display, IntPtr window);

    [DllImport(LibX11)]
    private static extern int XSelectInput(IntPtr display, IntPtr window, long eventMask);

    [DllImport(LibX11)]
    private static extern int XSync(IntPtr display, bool discard);

    [DllImport(LibX11)]
    private static extern bool XQueryTree(IntPtr display, IntPtr window, ref IntPtr root, ref IntPtr parent, ref IntPtr children, ref uint nchildren);

    [DllImport(LibX11)]
    private static extern int XFree(IntPtr data);

    [DllImport(LibX11)]
    private static extern int XMapRaised(IntPtr display, IntPtr window);

    [DllImport(LibX11)]
    private static extern int XGetWindowAttributes(IntPtr display, IntPtr window, out XWindowAttributes attributes);

    [DllImport(LibX11)]
    private static extern bool XTranslateCoordinates(IntPtr display, IntPtr src, IntPtr dest, int srcX, int srcY, out int destX, out int destY, out IntPtr child);

    [DllImport(LibX11)]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport(LibX11)]
    private static extern IntPtr XInternAtom(IntPtr display, string atomName, bool onlyIfExists);

    [DllImport(LibX11)]
    private static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type, int format, int mode, IntPtr data, int nelements);

    [DllImport(LibX11)]
    private static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type, int format, int mode, IntPtr[] data, int nelements);

    [DllImport("libdl.so.2")]
    private static extern IntPtr dlopen([MarshalAs(UnmanagedType.LPStr)] string? filename, int flags);

    [DllImport("libdl.so.2")]
    private static extern IntPtr dlsym(IntPtr handle, [MarshalAs(UnmanagedType.LPStr)] string symbol);

    [DllImport("libdl.so.2")]
    private static extern IntPtr dlerror();

    #endregion

    #region Properties

    public string Source
    {
        get => _source;
        set
        {
            if (_source == value) return;
            _source = value;
            if (!string.IsNullOrEmpty(value))
            {
                if (!_isInitialized) Initialize();
                if (_isInitialized) LoadUrl(value);
            }
            Invalidate();
        }
    }

    public string Html
    {
        get => _html;
        set
        {
            if (_html == value) return;
            _html = value;
            if (!string.IsNullOrEmpty(value))
            {
                if (!_isInitialized) Initialize();
                if (_isInitialized) LoadHtml(value);
            }
            Invalidate();
        }
    }

    public bool CanGoBack => _webView != IntPtr.Zero && (_webkitCanGoBack?.Invoke(_webView) ?? false);

    public bool CanGoForward => _webView != IntPtr.Zero && (_webkitCanGoForward?.Invoke(_webView) ?? false);

    public string? CurrentUrl
    {
        get
        {
            if (_webView == IntPtr.Zero || _webkitGetUri == null) return null;
            var ptr = _webkitGetUri(_webView);
            return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) : null;
        }
    }

    public string? Title
    {
        get
        {
            if (_webView == IntPtr.Zero || _webkitGetTitle == null) return null;
            var ptr = _webkitGetTitle(_webView);
            return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) : null;
        }
    }

    public bool JavaScriptEnabled
    {
        get => _javascriptEnabled;
        set
        {
            _javascriptEnabled = value;
            UpdateJavaScriptSetting();
        }
    }

    public double LoadProgress => _loadProgress;

    public static bool IsSupported => InitializeWebKit();

    #endregion

    #region Events

    public event EventHandler<WebNavigatingEventArgs>? Navigating;
    public event EventHandler<WebNavigatedEventArgs>? Navigated;
    public event EventHandler<string>? TitleChanged;
    public event EventHandler<double>? LoadProgressChanged;

    #endregion

    #region Static Methods

    public static void SetMainWindow(IntPtr display, IntPtr window)
    {
        _mainDisplay = display;
        _mainWindow = window;
        Console.WriteLine($"[WebView] Main window set: display={display}, window={window}");
    }

    public static void ProcessGtkEvents()
    {
        bool hasActive;
        lock (_activeWebViews)
        {
            hasActive = _activeWebViews.Count > 0;
        }
        if (hasActive && _gtkInitialized)
        {
            while (g_main_context_iteration(IntPtr.Zero, mayBlock: false)) { }
        }
    }

    private static bool InitializeWebKit()
    {
        if (_webkitHandle != IntPtr.Zero) return true;

        _webkitHandle = dlopen("libwebkit2gtk-4.1.so.0", RTLD_NOW | RTLD_GLOBAL);
        if (_webkitHandle != IntPtr.Zero)
        {
            _useGtk4 = false;
            _webkitLib = "libwebkit2gtk-4.1.so.0";
        }
        else
        {
            _webkitHandle = dlopen("libwebkit2gtk-4.0.so.37", RTLD_NOW | RTLD_GLOBAL);
            if (_webkitHandle != IntPtr.Zero)
            {
                _useGtk4 = false;
                _webkitLib = "libwebkit2gtk-4.0.so.37";
            }
            else
            {
                _webkitHandle = dlopen("libwebkitgtk-6.0.so.4", RTLD_NOW | RTLD_GLOBAL);
                if (_webkitHandle != IntPtr.Zero)
                {
                    _useGtk4 = true;
                    _webkitLib = "libwebkitgtk-6.0.so.4";
                    Console.WriteLine("[WebView] Warning: Using GTK4 WebKitGTK - embedding may be limited");
                }
            }
        }

        if (_webkitHandle == IntPtr.Zero)
        {
            Console.WriteLine("[WebView] WebKitGTK not found. Install with: sudo apt install libwebkit2gtk-4.1-0");
            return false;
        }

        _webkitWebViewNew = LoadFunction<WebKitWebViewNewDelegate>("webkit_web_view_new");
        _webkitLoadUri = LoadFunction<WebKitWebViewLoadUriDelegate>("webkit_web_view_load_uri");
        _webkitLoadHtml = LoadFunction<WebKitWebViewLoadHtmlDelegate>("webkit_web_view_load_html");
        _webkitGetUri = LoadFunction<WebKitWebViewGetUriDelegate>("webkit_web_view_get_uri");
        _webkitGetTitle = LoadFunction<WebKitWebViewGetTitleDelegate>("webkit_web_view_get_title");
        _webkitGoBack = LoadFunction<WebKitWebViewGoBackDelegate>("webkit_web_view_go_back");
        _webkitGoForward = LoadFunction<WebKitWebViewGoForwardDelegate>("webkit_web_view_go_forward");
        _webkitCanGoBack = LoadFunction<WebKitWebViewCanGoBackDelegate>("webkit_web_view_can_go_back");
        _webkitCanGoForward = LoadFunction<WebKitWebViewCanGoForwardDelegate>("webkit_web_view_can_go_forward");
        _webkitReload = LoadFunction<WebKitWebViewReloadDelegate>("webkit_web_view_reload");
        _webkitStopLoading = LoadFunction<WebKitWebViewStopLoadingDelegate>("webkit_web_view_stop_loading");
        _webkitGetProgress = LoadFunction<WebKitWebViewGetEstimatedLoadProgressDelegate>("webkit_web_view_get_estimated_load_progress");
        _webkitGetSettings = LoadFunction<WebKitWebViewGetSettingsDelegate>("webkit_web_view_get_settings");
        _webkitSetJavascript = LoadFunction<WebKitSettingsSetEnableJavascriptDelegate>("webkit_settings_set_enable_javascript");
        _webkitSetHardwareAcceleration = LoadFunction<WebKitSettingsSetHardwareAccelerationPolicyDelegate>("webkit_settings_set_hardware_acceleration_policy");
        _webkitSetWebgl = LoadFunction<WebKitSettingsSetEnableWebglDelegate>("webkit_settings_set_enable_webgl");

        Console.WriteLine($"[WebView] Using {_webkitLib}");
        return _webkitWebViewNew != null;
    }

    private static T? LoadFunction<T>(string name) where T : Delegate
    {
        var ptr = dlsym(_webkitHandle, name);
        return ptr == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    private static void OnLoadChanged(IntPtr webView, int loadEvent, IntPtr userData)
    {
        string[] events = { "STARTED", "REDIRECTED", "COMMITTED", "FINISHED" };
        string eventName = loadEvent >= 0 && loadEvent < events.Length ? events[loadEvent] : loadEvent.ToString();
        Console.WriteLine($"[WebView] Load event: {eventName}");

        if (!_webViewInstances.TryGetValue(webView, out var instance)) return;

        string url = instance.Source ?? "";
        if (_webkitGetUri != null)
        {
            var ptr = _webkitGetUri(webView);
            if (ptr != IntPtr.Zero)
            {
                url = Marshal.PtrToStringAnsi(ptr) ?? "";
            }
        }

        switch (loadEvent)
        {
            case 0: // STARTED
                instance.Navigating?.Invoke(instance, new WebNavigatingEventArgs(url));
                break;
            case 3: // FINISHED
                instance.Navigated?.Invoke(instance, new WebNavigatedEventArgs(url, true));
                break;
        }
    }

    #endregion

    #region Constructor

    public SkiaWebView()
    {
        RequestedWidth = 400.0;
        RequestedHeight = 300.0;
        BackgroundColor = SKColors.White;
    }

    #endregion

    #region Initialization

    private void Initialize()
    {
        if (_isInitialized || !InitializeWebKit()) return;

        try
        {
            if (!_gtkInitialized)
            {
                Environment.SetEnvironmentVariable("GDK_BACKEND", "x11");
                Environment.SetEnvironmentVariable("LIBGL_ALWAYS_SOFTWARE", "1");
                Environment.SetEnvironmentVariable("WEBKIT_DISABLE_COMPOSITING_MODE", "1");
                Console.WriteLine("[WebView] Using X11 backend with software rendering for proper positioning");

                var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
                Console.WriteLine($"[WebView] XDG_RUNTIME_DIR: {Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")}");
                Console.WriteLine($"[WebView] Forcing X11: GDK_BACKEND=x11, WAYLAND_DISPLAY={waylandDisplay}, XDG_SESSION_TYPE=x11");

                if (_useGtk4)
                {
                    gtk4_init();
                }
                else
                {
                    int argc = 0;
                    IntPtr argv = IntPtr.Zero;
                    if (!gtk3_init_check(ref argc, ref argv))
                    {
                        Console.WriteLine("[WebView] gtk3_init_check failed!");
                    }
                }
                _gtkInitialized = true;

                var gdkDisplay = gdk3_display_get_default();
                Console.WriteLine($"[WebView] GDK display: {gdkDisplay}");
            }

            _webView = _webkitWebViewNew!();
            if (_webView == IntPtr.Zero)
            {
                Console.WriteLine("[WebView] Failed to create WebKit view");
                return;
            }

            _webViewInstances[_webView] = this;
            _loadChangedCallback = OnLoadChanged;
            var callbackPtr = Marshal.GetFunctionPointerForDelegate(_loadChangedCallback);
            g_signal_connect_data(_webView, "load-changed", callbackPtr, IntPtr.Zero, IntPtr.Zero, 0);
            Console.WriteLine("[WebView] Connected to load-changed signal");

            int width = Math.Max(800, (int)RequestedWidth);
            int height = Math.Max(600, (int)RequestedHeight);

            if (_useGtk4)
            {
                _gtkWindow = gtk4_window_new();
                gtk4_window_set_title(_gtkWindow, "OpenMaui WebView");
                gtk4_window_set_default_size(_gtkWindow, width, height);
                gtk4_window_set_child(_gtkWindow, _webView);
                Console.WriteLine($"[WebView] GTK4 window created: {width}x{height}");
            }
            else
            {
                _gtkWindow = gtk3_window_new(0);
                gtk3_window_set_default_size(_gtkWindow, width, height);
                gtk3_window_set_title(_gtkWindow, "WebViewDemo");
                gtk3_widget_set_hexpand(_webView, true);
                gtk3_widget_set_vexpand(_webView, true);
                gtk3_widget_set_size_request(_webView, width, height);
                gtk3_container_add(_gtkWindow, _webView);
                Console.WriteLine($"[WebView] GTK3 TOPLEVEL window created: {width}x{height}");
            }

            ConfigureWebKitSettings();
            UpdateJavaScriptSetting();
            _isInitialized = true;

            lock (_activeWebViews)
            {
                if (!_activeWebViews.Contains(this))
                {
                    _activeWebViews.Add(this);
                }
            }

            if (!string.IsNullOrEmpty(_source))
            {
                LoadUrl(_source);
            }
            else if (!string.IsNullOrEmpty(_html))
            {
                LoadHtml(_html);
            }

            Console.WriteLine("[WebView] Initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebView] Initialization failed: {ex.Message}");
        }
    }

    private void ConfigureWebKitSettings()
    {
        if (_webView == IntPtr.Zero) return;

        try
        {
            if (_webkitGetSettings == null) return;

            var settings = _webkitGetSettings(_webView);
            if (settings == IntPtr.Zero)
            {
                Console.WriteLine("[WebView] Could not get WebKit settings");
                return;
            }

            if (_webkitSetHardwareAcceleration != null)
            {
                _webkitSetHardwareAcceleration(settings, 2); // NEVER
                Console.WriteLine("[WebView] Set hardware acceleration to NEVER (software rendering)");
            }
            else
            {
                Console.WriteLine("[WebView] Warning: Could not set hardware acceleration policy");
            }

            if (_webkitSetWebgl != null)
            {
                _webkitSetWebgl(settings, false);
                Console.WriteLine("[WebView] Disabled WebGL");
            }

            Console.WriteLine("[WebView] WebKit settings configured successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebView] Failed to configure settings: {ex.Message}");
        }
    }

    private void UpdateJavaScriptSetting()
    {
        if (_webView == IntPtr.Zero || _webkitGetSettings == null || _webkitSetJavascript == null) return;

        var settings = _webkitGetSettings(_webView);
        if (settings != IntPtr.Zero)
        {
            _webkitSetJavascript(settings, _javascriptEnabled);
        }
    }

    #endregion

    #region Navigation

    public void LoadUrl(string url)
    {
        if (!_isInitialized) Initialize();
        if (_webView != IntPtr.Zero && _webkitLoadUri != null)
        {
            Navigating?.Invoke(this, new WebNavigatingEventArgs(url));
            _webkitLoadUri(_webView, url);
            Console.WriteLine($"[WebView] URL loaded: {url}");
            ShowNativeWindow();
        }
    }

    public void LoadHtml(string html, string? baseUrl = null)
    {
        Console.WriteLine($"[WebView] LoadHtml called, html length: {html?.Length ?? 0}");
        if (!_isInitialized) Initialize();
        if (_webView == IntPtr.Zero || _webkitLoadHtml == null)
        {
            Console.WriteLine("[WebView] Cannot load HTML - not initialized or no webkit function");
            return;
        }
        Console.WriteLine("[WebView] Calling webkit_web_view_load_html...");
        _webkitLoadHtml(_webView, html, baseUrl);
        Console.WriteLine("[WebView] HTML loaded to WebKit");
        ShowNativeWindow();
    }

    public void GoBack()
    {
        if (_webView != IntPtr.Zero && CanGoBack)
        {
            _webkitGoBack?.Invoke(_webView);
        }
    }

    public void GoForward()
    {
        if (_webView != IntPtr.Zero && CanGoForward)
        {
            _webkitGoForward?.Invoke(_webView);
        }
    }

    public void Reload()
    {
        _webkitReload?.Invoke(_webView);
    }

    public void Stop()
    {
        _webkitStopLoading?.Invoke(_webView);
    }

    #endregion

    #region Event Processing

    public void ProcessEvents()
    {
        if (!_isInitialized) return;

        g_main_context_iteration(IntPtr.Zero, mayBlock: false);

        if (_webView != IntPtr.Zero && _webkitGetProgress != null)
        {
            double progress = _webkitGetProgress(_webView);
            if (Math.Abs(progress - _loadProgress) > 0.01)
            {
                _loadProgress = progress;
                LoadProgressChanged?.Invoke(this, progress);
            }
        }
    }

    #endregion

    #region Window Management

    private bool CreateX11Container()
    {
        if (_mainDisplay == IntPtr.Zero || _mainWindow == IntPtr.Zero)
        {
            Console.WriteLine("[WebView] Cannot create X11 container - main window not set");
            return false;
        }

        if (_x11Container != IntPtr.Zero)
        {
            Console.WriteLine("[WebView] X11 container already exists");
            return true;
        }

        try
        {
            int x = (int)Bounds.Left;
            int y = (int)Bounds.Top;
            uint width = Math.Max(100u, (uint)Bounds.Width);
            uint height = Math.Max(100u, (uint)Bounds.Height);

            if (width < 100) width = 780;
            if (height < 100) height = 300;

            Console.WriteLine($"[WebView] Creating X11 container at ({x}, {y}), size ({width}x{height})");

            _x11Container = XCreateSimpleWindow(_mainDisplay, _mainWindow, x, y, width, height, 0, 0, 0xFFFFFF);
            if (_x11Container == IntPtr.Zero)
            {
                Console.WriteLine("[WebView] Failed to create X11 container window");
                return false;
            }

            Console.WriteLine($"[WebView] Created X11 container: {_x11Container.ToInt64()}");
            XMapWindow(_mainDisplay, _x11Container);
            XFlush(_mainDisplay);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebView] Error creating X11 container: {ex.Message}");
            return false;
        }
    }

    public void ShowNativeWindow()
    {
        if (!_isInitialized) Initialize();
        if (_gtkWindow == IntPtr.Zero) return;

        Console.WriteLine("[WebView] Showing native GTK window...");

        if (!_useGtk4)
        {
            gtk3_window_set_decorated(_gtkWindow, false);
            gtk3_window_set_skip_taskbar_hint(_gtkWindow, true);
            gtk3_window_set_skip_pager_hint(_gtkWindow, true);
            gtk3_window_set_keep_above(_gtkWindow, true);
            gtk3_window_set_type_hint(_gtkWindow, 5); // UTILITY
        }

        if (_useGtk4)
        {
            gtk4_widget_show(_gtkWindow);
            gtk4_window_present(_gtkWindow);
        }
        else
        {
            gtk3_widget_show_all(_gtkWindow);
        }

        for (int i = 0; i < 100; i++)
        {
            while (gtk3_events_pending())
            {
                gtk3_main_iteration_do(false);
            }
        }

        TryReparentIntoMainWindow();
        _isEmbedded = true;
        Console.WriteLine("[WebView] Native window shown");
    }

    private void TryReparentIntoMainWindow()
    {
        if (_mainDisplay == IntPtr.Zero || _mainWindow == IntPtr.Zero)
        {
            Console.WriteLine("[WebView] Cannot setup - main window not set");
            return;
        }

        var gdkWindow = gtk3_widget_get_window(_gtkWindow);
        if (gdkWindow != IntPtr.Zero)
        {
            _gtkX11Window = gdk3_x11_window_get_xid(gdkWindow);
            Console.WriteLine($"[WebView] GTK X11 window: {_gtkX11Window}");
        }

        PositionUsingGtk();
    }

    private void PositionUsingGtk()
    {
        if (_gtkWindow == IntPtr.Zero || _mainDisplay == IntPtr.Zero) return;

        int destX = 0, destY = 0;
        try
        {
            var root = XDefaultRootWindow(_mainDisplay);
            XTranslateCoordinates(_mainDisplay, _mainWindow, root, 0, 0, out destX, out destY, out _);
        }
        catch
        {
            destX = 0;
            destY = 0;
        }

        int screenX = destX + (int)Bounds.Left;
        int screenY = destY + (int)Bounds.Top;
        int width = Math.Max(100, (int)Bounds.Width);
        int height = Math.Max(100, (int)Bounds.Height);

        Console.WriteLine($"[WebView] Position: screen=({screenX}, {screenY}), size ({width}x{height}), bounds=({Bounds.Left},{Bounds.Top})");

        if (!_useGtk4)
        {
            gtk3_window_move(_gtkWindow, screenX, screenY);
            gtk3_window_resize(_gtkWindow, width, height);

            while (gtk3_events_pending())
            {
                gtk3_main_iteration_do(false);
            }

            if (_gtkX11Window != IntPtr.Zero)
            {
                XRaiseWindow(_mainDisplay, _gtkX11Window);
                SetWindowAlwaysOnTop(_gtkX11Window);
                XFlush(_mainDisplay);
            }
        }
        else
        {
            gtk4_window_set_default_size(_gtkWindow, width, height);
        }
    }

    private void PositionWithX11()
    {
        if (_gtkX11Window == IntPtr.Zero || _mainDisplay == IntPtr.Zero) return;

        int destX = 0, destY = 0;
        try
        {
            var root = XDefaultRootWindow(_mainDisplay);
            XTranslateCoordinates(_mainDisplay, _mainWindow, root, 0, 0, out destX, out destY, out _);
        }
        catch { }

        int x = destX + (int)Bounds.Left;
        int y = destY + (int)Bounds.Top;
        uint width = (uint)Math.Max(100f, Bounds.Width > 10f ? Bounds.Width : 780f);
        uint height = (uint)Math.Max(100f, Bounds.Height > 10f ? Bounds.Height : 300f);

        XMoveResizeWindow(_mainDisplay, _gtkX11Window, x, y, width, height);
        XRaiseWindow(_mainDisplay, _gtkX11Window);
        SetWindowAlwaysOnTop(_gtkX11Window);
        XFlush(_mainDisplay);

        gtk3_widget_queue_draw(_webView);

        for (int i = 0; i < 5; i++)
        {
            g_main_context_iteration(IntPtr.Zero, mayBlock: false);
        }
    }

    private void SetWindowAlwaysOnTop(IntPtr window)
    {
        try
        {
            var wmState = XInternAtom(_mainDisplay, "_NET_WM_STATE", false);
            var wmStateAbove = XInternAtom(_mainDisplay, "_NET_WM_STATE_ABOVE", false);
            var atomType = XInternAtom(_mainDisplay, "ATOM", false);
            IntPtr[] data = { wmStateAbove };
            XChangeProperty(_mainDisplay, window, wmState, atomType, 32, 0, data, 1);
        }
        catch { }
    }

    private void EnableOverlayMode()
    {
        if (_gtkWindow == IntPtr.Zero || _useGtk4) return;

        try
        {
            gtk3_window_set_type_hint(_gtkWindow, 5); // UTILITY
            gtk3_window_set_skip_taskbar_hint(_gtkWindow, true);
            gtk3_window_set_skip_pager_hint(_gtkWindow, true);
            gtk3_window_set_keep_above(_gtkWindow, true);
            gtk3_window_set_decorated(_gtkWindow, false);
            Console.WriteLine("[WebView] Overlay mode enabled with UTILITY hint");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebView] Failed to enable overlay mode: {ex.Message}");
        }
    }

    private void SetupEmbedding()
    {
        if (_mainDisplay == IntPtr.Zero || _mainWindow == IntPtr.Zero)
        {
            Console.WriteLine("[WebView] Cannot setup embedding - main window not set");
            return;
        }

        GetWindowPosition(_mainDisplay, _mainWindow, out int x, out int y);
        int screenX = x + (int)Bounds.Left;
        int screenY = y + (int)Bounds.Top;
        int width = Math.Max(100, (int)Bounds.Width);
        int height = Math.Max(100, (int)Bounds.Height);

        Console.WriteLine($"[WebView] Initial position: ({screenX}, {screenY}), size ({width}x{height})");

        if (!_useGtk4)
        {
            gtk3_window_move(_gtkWindow, screenX, screenY);
            gtk3_window_resize(_gtkWindow, width, height);
        }
        else
        {
            gtk4_window_set_default_size(_gtkWindow, width, height);
        }

        _lastBounds = Bounds;
    }

    private void PositionAtScreenCoordinates()
    {
        if (_gtkWindow == IntPtr.Zero || _mainDisplay == IntPtr.Zero) return;

        int destX = 0, destY = 0;
        try
        {
            var root = XDefaultRootWindow(_mainDisplay);
            XTranslateCoordinates(_mainDisplay, _mainWindow, root, 0, 0, out destX, out destY, out _);
        }
        catch { }

        int offsetX = 0, offsetY = 0;
        int screenX = destX + (int)Bounds.Left - offsetX;
        int screenY = destY + (int)Bounds.Top - offsetY;
        int width = Math.Max(100, (int)Bounds.Width);
        int height = Math.Max(100, (int)Bounds.Height);

        if (Math.Abs(screenX - _lastPosX) > 2 || Math.Abs(screenY - _lastPosY) > 2 ||
            Math.Abs(width - _lastWidth) > 2 || Math.Abs(height - _lastHeight) > 2)
        {
            Console.WriteLine($"[WebView] Move to ({screenX}, {screenY}), size ({width}x{height}), mainWin=({destX},{destY}), bounds=({Bounds.Left},{Bounds.Top})");
            _lastPosX = screenX;
            _lastPosY = screenY;
            _lastWidth = width;
            _lastHeight = height;
            _lastBounds = Bounds;
        }

        if (!_useGtk4)
        {
            gtk3_window_move(_gtkWindow, screenX, screenY);
            gtk3_window_resize(_gtkWindow, width, height);

            var gdkWindow = gtk3_widget_get_window(_gtkWindow);
            if (gdkWindow != IntPtr.Zero)
            {
                var xid = gdk3_x11_window_get_xid(gdkWindow);
                if (xid != IntPtr.Zero)
                {
                    XRaiseWindow(_mainDisplay, xid);
                    XFlush(_mainDisplay);
                }
            }

            while (gtk3_events_pending())
            {
                gtk3_main_iteration_do(false);
            }
        }
        else
        {
            gtk4_window_set_default_size(_gtkWindow, width, height);
        }
    }

    private IntPtr GetGtkX11Window()
    {
        if (_gtkWindow == IntPtr.Zero) return IntPtr.Zero;

        for (int i = 0; i < 50; i++)
        {
            g_main_context_iteration(IntPtr.Zero, mayBlock: false);
        }

        if (_useGtk4)
        {
            var surface = gtk4_native_get_surface(_gtkWindow);
            if (surface != IntPtr.Zero)
            {
                try { return gdk4_x11_surface_get_xid(surface); }
                catch { }
            }
        }
        else
        {
            var gdkWindow = gtk3_widget_get_window(_gtkWindow);
            if (gdkWindow != IntPtr.Zero)
            {
                try { return gdk3_x11_window_get_xid(gdkWindow); }
                catch { }
            }
        }

        return IntPtr.Zero;
    }

    private void GetWindowPosition(IntPtr display, IntPtr window, out int x, out int y)
    {
        x = 0;
        y = 0;
        try
        {
            var root = XDefaultRootWindow(display);
            if (XTranslateCoordinates(display, window, root, 0, 0, out x, out y, out _))
            {
                Console.WriteLine($"[WebView] Main window at screen ({x}, {y})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebView] Failed to get window position: {ex.Message}");
        }
    }

    public void UpdateEmbeddedPosition()
    {
        if (_mainDisplay == IntPtr.Zero) return;
        if (Bounds.Width < 10f || Bounds.Height < 10f) return;

        bool boundsChanged = Math.Abs(Bounds.Left - _lastBounds.Left) > 1f ||
                             Math.Abs(Bounds.Top - _lastBounds.Top) > 1f ||
                             Math.Abs(Bounds.Width - _lastBounds.Width) > 1f ||
                             Math.Abs(Bounds.Height - _lastBounds.Height) > 1f;

        if (!boundsChanged) return;

        _lastBounds = Bounds;
        int x = (int)Bounds.Left;
        int y = (int)Bounds.Top;
        uint width = (uint)Math.Max(10f, Bounds.Width);
        uint height = (uint)Math.Max(10f, Bounds.Height);

        if (_isProperlyReparented && _gtkX11Window != IntPtr.Zero)
        {
            Console.WriteLine($"[WebView] UpdateEmbedded (reparented): ({x}, {y}), size ({width}x{height})");
            XMoveResizeWindow(_mainDisplay, _gtkX11Window, x, y, width, height);
            XFlush(_mainDisplay);
        }
        else if (_x11Container != IntPtr.Zero)
        {
            Console.WriteLine($"[WebView] UpdateEmbedded (container): ({x}, {y}), size ({width}x{height})");
            XMoveResizeWindow(_mainDisplay, _x11Container, x, y, width, height);
            if (_gtkX11Window != IntPtr.Zero && _isProperlyReparented)
            {
                XMoveResizeWindow(_mainDisplay, _gtkX11Window, 0, 0, width, height);
            }
            else if (_gtkWindow != IntPtr.Zero)
            {
                PositionAtScreenCoordinates();
            }
            XFlush(_mainDisplay);
        }
        else if (_gtkWindow != IntPtr.Zero)
        {
            PositionAtScreenCoordinates();
        }
    }

    public void HideNativeWindow()
    {
        if (_gtkWindow == IntPtr.Zero) return;

        if (_useGtk4)
        {
            gtk4_widget_hide(_gtkWindow);
        }
        else
        {
            gtk3_widget_hide(_gtkWindow);
        }
    }

    #endregion

    #region Rendering

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        base.OnDraw(canvas, bounds);
        Bounds = bounds;

        if (_isInitialized)
        {
            while (gtk3_events_pending())
            {
                gtk3_main_iteration_do(false);
            }

            if (_gtkWindow != IntPtr.Zero && _mainDisplay != IntPtr.Zero)
            {
                bool needsUpdate = Math.Abs(bounds.Left - _lastBounds.Left) > 1f ||
                                   Math.Abs(bounds.Top - _lastBounds.Top) > 1f ||
                                   Math.Abs(bounds.Width - _lastBounds.Width) > 1f ||
                                   Math.Abs(bounds.Height - _lastBounds.Height) > 1f;

                if (!needsUpdate && _lastBounds.Width < 150f && bounds.Width > 150f)
                {
                    needsUpdate = true;
                }

                if (needsUpdate && bounds.Width > 50f && bounds.Height > 50f)
                {
                    PositionUsingGtk();
                    _lastBounds = bounds;
                }
            }
        }

        if (_isInitialized && _gtkWindow != IntPtr.Zero) return;

        // Draw placeholder when not initialized
        using var bgPaint = new SKPaint { Color = BackgroundColor, Style = SKPaintStyle.Fill };
        canvas.DrawRect(bounds, bgPaint);

        using var borderPaint = new SKPaint
        {
            Color = new SKColor(200, 200, 200),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawRect(bounds, borderPaint);

        float midX = bounds.MidX;
        float midY = bounds.MidY;

        using var iconPaint = new SKPaint
        {
            Color = new SKColor(100, 100, 100),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };
        canvas.DrawCircle(midX, midY - 20, 25, iconPaint);
        canvas.DrawLine(midX - 25, midY - 20, midX + 25, midY - 20, iconPaint);
        canvas.DrawArc(new SKRect(midX - 15, midY - 45, midX + 15, midY + 5), 0, 180, false, iconPaint);

        using var textPaint = new SKPaint
        {
            Color = new SKColor(80, 80, 80),
            IsAntialias = true,
            TextSize = 14
        };

        string statusText;
        if (!IsSupported)
        {
            statusText = "WebKitGTK not installed";
        }
        else if (_isInitialized)
        {
            statusText = string.IsNullOrEmpty(_source) ? "No URL loaded" : $"Loading: {_source}";
            if (_loadProgress > 0 && _loadProgress < 1)
            {
                statusText = $"Loading: {(int)(_loadProgress * 100)}%";
            }
        }
        else
        {
            statusText = "WebView (click to open)";
        }

        float textWidth = textPaint.MeasureText(statusText);
        canvas.DrawText(statusText, midX - textWidth / 2, midY + 30, textPaint);

        if (!IsSupported)
        {
            using var hintPaint = new SKPaint
            {
                Color = new SKColor(120, 120, 120),
                IsAntialias = true,
                TextSize = 11
            };
            string hint = "Install: sudo apt install libwebkit2gtk-4.1-0";
            float hintWidth = hintPaint.MeasureText(hint);
            canvas.DrawText(hint, midX - hintWidth / 2, midY + 50, hintPaint);
        }

        if (_loadProgress > 0 && _loadProgress < 1)
        {
            var progressRect = new SKRect(bounds.Left + 20, bounds.Bottom - 30, bounds.Right - 20, bounds.Bottom - 20);
            using var progressBgPaint = new SKPaint { Color = new SKColor(230, 230, 230), Style = SKPaintStyle.Fill };
            canvas.DrawRoundRect(new SKRoundRect(progressRect, 5), progressBgPaint);

            float filledWidth = progressRect.Width * (float)_loadProgress;
            var filledRect = new SKRect(progressRect.Left, progressRect.Top, progressRect.Left + filledWidth, progressRect.Bottom);
            using var progressPaint = new SKPaint { Color = new SKColor(33, 150, 243), Style = SKPaintStyle.Fill };
            canvas.DrawRoundRect(new SKRoundRect(filledRect, 5), progressPaint);
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!_isInitialized && IsSupported)
        {
            Initialize();
            ShowNativeWindow();
        }
        else if (_isInitialized)
        {
            ShowNativeWindow();
        }
    }

    #endregion

    #region Cleanup

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_activeWebViews)
            {
                _activeWebViews.Remove(this);
            }

            if (_webView != IntPtr.Zero)
            {
                _webViewInstances.Remove(_webView);
            }

            if (_gtkWindow != IntPtr.Zero)
            {
                if (_useGtk4)
                {
                    gtk4_widget_hide(_gtkWindow);
                }
                else
                {
                    gtk3_widget_hide(_gtkWindow);
                }
                g_object_unref(_gtkWindow);
                _gtkWindow = IntPtr.Zero;
            }

            if (_x11Container != IntPtr.Zero && _mainDisplay != IntPtr.Zero)
            {
                XDestroyWindow(_mainDisplay, _x11Container);
                _x11Container = IntPtr.Zero;
            }

            _webView = IntPtr.Zero;
            _gtkX11Window = IntPtr.Zero;
            _isInitialized = false;
        }

        base.Dispose(disposing);
    }

    #endregion
}

#region Event Args

public class WebNavigatingEventArgs : EventArgs
{
    public string Url { get; }
    public bool Cancel { get; set; }

    public WebNavigatingEventArgs(string url)
    {
        Url = url;
    }
}

public class WebNavigatedEventArgs : EventArgs
{
    public string Url { get; }
    public bool Success { get; }
    public string? Error { get; }

    public WebNavigatedEventArgs(string url, bool success, string? error = null)
    {
        Url = url;
        Success = success;
        Error = error;
    }
}

#endregion
