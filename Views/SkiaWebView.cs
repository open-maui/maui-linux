using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaWebView : SkiaView
{
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

	private const string LibGtk4 = "libgtk-4.so.1";

	private const string LibGtk3 = "libgtk-3.so.0";

	private const string LibWebKit2Gtk4 = "libwebkitgtk-6.0.so.4";

	private const string LibWebKit2Gtk3 = "libwebkit2gtk-4.1.so.0";

	private const string LibGObject = "libgobject-2.0.so.0";

	private const string LibGLib = "libglib-2.0.so.0";

	private static bool _useGtk4;

	private static bool _gtkInitialized;

	private static string _webkitLib = "libwebkit2gtk-4.1.so.0";

	private static LoadChangedCallback? _loadChangedCallback;

	private const string LibGdk4 = "libgtk-4.so.1";

	private const string LibGdk3 = "libgdk-3.so.0";

	private const string LibX11 = "libX11.so.6";

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

	private const int RTLD_NOW = 2;

	private const int RTLD_GLOBAL = 256;

	private static IntPtr _webkitHandle;

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

	private static IntPtr _mainDisplay;

	private static IntPtr _mainWindow;

	private static readonly List<SkiaWebView> _activeWebViews = new List<SkiaWebView>();

	private static readonly Dictionary<IntPtr, SkiaWebView> _webViewInstances = new Dictionary<IntPtr, SkiaWebView>();

	private int _lastPosX;

	private int _lastPosY;

	private int _lastWidth;

	private int _lastHeight;

	public string Source
	{
		get
		{
			return _source;
		}
		set
		{
			if (!(_source != value))
			{
				return;
			}
			_source = value;
			if (!string.IsNullOrEmpty(value))
			{
				if (!_isInitialized)
				{
					Initialize();
				}
				if (_isInitialized)
				{
					LoadUrl(value);
				}
			}
			Invalidate();
		}
	}

	public string Html
	{
		get
		{
			return _html;
		}
		set
		{
			if (!(_html != value))
			{
				return;
			}
			_html = value;
			if (!string.IsNullOrEmpty(value))
			{
				if (!_isInitialized)
				{
					Initialize();
				}
				if (_isInitialized)
				{
					LoadHtml(value);
				}
			}
			Invalidate();
		}
	}

	public bool CanGoBack
	{
		get
		{
			if (_webView != IntPtr.Zero)
			{
				return _webkitCanGoBack?.Invoke(_webView) ?? false;
			}
			return false;
		}
	}

	public bool CanGoForward
	{
		get
		{
			if (_webView != IntPtr.Zero)
			{
				return _webkitCanGoForward?.Invoke(_webView) ?? false;
			}
			return false;
		}
	}

	public string? CurrentUrl
	{
		get
		{
			if (_webView == IntPtr.Zero || _webkitGetUri == null)
			{
				return null;
			}
			IntPtr intPtr = _webkitGetUri(_webView);
			if (intPtr == IntPtr.Zero)
			{
				return null;
			}
			return Marshal.PtrToStringAnsi(intPtr);
		}
	}

	public string? Title
	{
		get
		{
			if (_webView == IntPtr.Zero || _webkitGetTitle == null)
			{
				return null;
			}
			IntPtr intPtr = _webkitGetTitle(_webView);
			if (intPtr == IntPtr.Zero)
			{
				return null;
			}
			return Marshal.PtrToStringAnsi(intPtr);
		}
	}

	public bool JavaScriptEnabled
	{
		get
		{
			return _javascriptEnabled;
		}
		set
		{
			_javascriptEnabled = value;
			UpdateJavaScriptSetting();
		}
	}

	public double LoadProgress => _loadProgress;

	public static bool IsSupported => InitializeWebKit();

	public event EventHandler<WebNavigatingEventArgs>? Navigating;

	public event EventHandler<WebNavigatedEventArgs>? Navigated;

	public event EventHandler<string>? TitleChanged;

	public event EventHandler<double>? LoadProgressChanged;

	[DllImport("libgtk-4.so.1", EntryPoint = "gtk_init")]
	private static extern void gtk4_init();

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_init_check")]
	private static extern bool gtk3_init_check(ref int argc, ref IntPtr argv);

	[DllImport("libgtk-4.so.1", EntryPoint = "gtk_window_new")]
	private static extern IntPtr gtk4_window_new();

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_window_new")]
	private static extern IntPtr gtk3_window_new(int type);

	[DllImport("libgtk-4.so.1", EntryPoint = "gtk_window_set_default_size")]
	private static extern void gtk4_window_set_default_size(IntPtr window, int width, int height);

	[DllImport("libgtk-4.so.1", EntryPoint = "gtk_window_set_title")]
	private static extern void gtk4_window_set_title(IntPtr window, [MarshalAs(UnmanagedType.LPStr)] string title);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_window_set_default_size")]
	private static extern void gtk3_window_set_default_size(IntPtr window, int width, int height);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_window_set_title")]
	private static extern void gtk3_window_set_title(IntPtr window, [MarshalAs(UnmanagedType.LPStr)] string title);

	[DllImport("libgtk-4.so.1", EntryPoint = "gtk_window_set_child")]
	private static extern void gtk4_window_set_child(IntPtr window, IntPtr child);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_container_add")]
	private static extern void gtk3_container_add(IntPtr container, IntPtr widget);

	[DllImport("libgtk-4.so.1", EntryPoint = "gtk_widget_show")]
	private static extern void gtk4_widget_show(IntPtr widget);

	[DllImport("libgtk-4.so.1", EntryPoint = "gtk_window_present")]
	private static extern void gtk4_window_present(IntPtr window);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_widget_show_all")]
	private static extern void gtk3_widget_show_all(IntPtr widget);

	[DllImport("libgtk-4.so.1", EntryPoint = "gtk_widget_hide")]
	private static extern void gtk4_widget_hide(IntPtr widget);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_widget_hide")]
	private static extern void gtk3_widget_hide(IntPtr widget);

	[DllImport("libgtk-4.so.1", EntryPoint = "gtk_widget_get_width")]
	private static extern int gtk4_widget_get_width(IntPtr widget);

	[DllImport("libgtk-4.so.1", EntryPoint = "gtk_widget_get_height")]
	private static extern int gtk4_widget_get_height(IntPtr widget);

	[DllImport("libgobject-2.0.so.0")]
	private static extern void g_object_unref(IntPtr obj);

	[DllImport("libgobject-2.0.so.0")]
	private static extern ulong g_signal_connect_data(IntPtr instance, [MarshalAs(UnmanagedType.LPStr)] string signal, IntPtr handler, IntPtr data, IntPtr destroyData, int flags);

	[DllImport("libgtk-4.so.1", EntryPoint = "gtk_native_get_surface")]
	private static extern IntPtr gtk4_native_get_surface(IntPtr native);

	[DllImport("libgtk-4.so.1", EntryPoint = "gdk_x11_surface_get_xid")]
	private static extern IntPtr gdk4_x11_surface_get_xid(IntPtr surface);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_widget_get_window")]
	private static extern IntPtr gtk3_widget_get_window(IntPtr widget);

	[DllImport("libgdk-3.so.0", EntryPoint = "gdk_x11_window_get_xid")]
	private static extern IntPtr gdk3_x11_window_get_xid(IntPtr gdkWindow);

	[DllImport("libX11.so.6")]
	private static extern int XReparentWindow(IntPtr display, IntPtr window, IntPtr parent, int x, int y);

	[DllImport("libX11.so.6")]
	private static extern int XMoveResizeWindow(IntPtr display, IntPtr window, int x, int y, uint width, uint height);

	[DllImport("libX11.so.6")]
	private static extern int XMapWindow(IntPtr display, IntPtr window);

	[DllImport("libX11.so.6")]
	private static extern int XUnmapWindow(IntPtr display, IntPtr window);

	[DllImport("libX11.so.6")]
	private static extern int XFlush(IntPtr display);

	[DllImport("libX11.so.6")]
	private static extern int XRaiseWindow(IntPtr display, IntPtr window);

	[DllImport("libX11.so.6")]
	private static extern IntPtr XCreateSimpleWindow(IntPtr display, IntPtr parent, int x, int y, uint width, uint height, uint borderWidth, ulong border, ulong background);

	[DllImport("libX11.so.6")]
	private static extern int XDestroyWindow(IntPtr display, IntPtr window);

	[DllImport("libX11.so.6")]
	private static extern int XSelectInput(IntPtr display, IntPtr window, long eventMask);

	[DllImport("libX11.so.6")]
	private static extern int XSync(IntPtr display, bool discard);

	[DllImport("libX11.so.6")]
	private static extern bool XQueryTree(IntPtr display, IntPtr window, ref IntPtr root, ref IntPtr parent, ref IntPtr children, ref uint nchildren);

	[DllImport("libX11.so.6")]
	private static extern int XFree(IntPtr data);

	[DllImport("libgtk-4.so.1", EntryPoint = "gtk_window_set_decorated")]
	private static extern void gtk4_window_set_decorated(IntPtr window, bool decorated);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_window_set_decorated")]
	private static extern void gtk3_window_set_decorated(IntPtr window, bool decorated);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_window_move")]
	private static extern void gtk3_window_move(IntPtr window, int x, int y);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_window_resize")]
	private static extern void gtk3_window_resize(IntPtr window, int width, int height);

	[DllImport("libgdk-3.so.0", EntryPoint = "gdk_window_move_resize")]
	private static extern void gdk3_window_move_resize(IntPtr window, int x, int y, int width, int height);

	[DllImport("libgdk-3.so.0", EntryPoint = "gdk_window_move")]
	private static extern void gdk3_gdk_window_move(IntPtr window, int x, int y);

	[DllImport("libgdk-3.so.0", EntryPoint = "gdk_window_set_override_redirect")]
	private static extern void gdk3_window_set_override_redirect(IntPtr window, bool override_redirect);

	[DllImport("libgdk-3.so.0", EntryPoint = "gdk_window_set_transient_for")]
	private static extern void gdk3_window_set_transient_for(IntPtr window, IntPtr parent);

	[DllImport("libgdk-3.so.0", EntryPoint = "gdk_window_raise")]
	private static extern void gdk3_window_raise(IntPtr window);

	[DllImport("libgdk-3.so.0", EntryPoint = "gdk_x11_window_foreign_new_for_display")]
	private static extern IntPtr gdk3_x11_window_foreign_new_for_display(IntPtr display, IntPtr window);

	[DllImport("libgdk-3.so.0", EntryPoint = "gdk_display_get_default")]
	private static extern IntPtr gdk3_display_get_default();

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_widget_set_parent_window")]
	private static extern void gtk3_widget_set_parent_window(IntPtr widget, IntPtr parentWindow);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_socket_new")]
	private static extern IntPtr gtk3_socket_new();

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_socket_add_id")]
	private static extern void gtk3_socket_add_id(IntPtr socket, IntPtr windowId);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_socket_get_id")]
	private static extern IntPtr gtk3_socket_get_id(IntPtr socket);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_plug_new")]
	private static extern IntPtr gtk3_plug_new(IntPtr socketId);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_plug_get_id")]
	private static extern IntPtr gtk3_plug_get_id(IntPtr plug);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_window_set_skip_taskbar_hint")]
	private static extern void gtk3_window_set_skip_taskbar_hint(IntPtr window, bool setting);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_window_set_skip_pager_hint")]
	private static extern void gtk3_window_set_skip_pager_hint(IntPtr window, bool setting);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_window_set_type_hint")]
	private static extern void gtk3_window_set_type_hint(IntPtr window, int hint);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_window_present")]
	private static extern void gtk3_window_present(IntPtr window);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_widget_queue_draw")]
	private static extern void gtk3_widget_queue_draw(IntPtr widget);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_window_set_keep_above")]
	private static extern void gtk3_window_set_keep_above(IntPtr window, bool setting);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_widget_set_hexpand")]
	private static extern void gtk3_widget_set_hexpand(IntPtr widget, bool expand);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_widget_set_vexpand")]
	private static extern void gtk3_widget_set_vexpand(IntPtr widget, bool expand);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_widget_set_size_request")]
	private static extern void gtk3_widget_set_size_request(IntPtr widget, int width, int height);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_widget_realize")]
	private static extern void gtk3_widget_realize(IntPtr widget);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_widget_map")]
	private static extern void gtk3_widget_map(IntPtr widget);

	[DllImport("libglib-2.0.so.0")]
	private static extern bool g_main_context_iteration(IntPtr context, bool mayBlock);

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_events_pending")]
	private static extern bool gtk3_events_pending();

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_main_iteration")]
	private static extern void gtk3_main_iteration();

	[DllImport("libgtk-3.so.0", EntryPoint = "gtk_main_iteration_do")]
	private static extern bool gtk3_main_iteration_do(bool blocking);

	[DllImport("libdl.so.2")]
	private static extern IntPtr dlopen([MarshalAs(UnmanagedType.LPStr)] string? filename, int flags);

	[DllImport("libdl.so.2")]
	private static extern IntPtr dlsym(IntPtr handle, [MarshalAs(UnmanagedType.LPStr)] string symbol);

	[DllImport("libdl.so.2")]
	private static extern IntPtr dlerror();

	public static void SetMainWindow(IntPtr display, IntPtr window)
	{
		_mainDisplay = display;
		_mainWindow = window;
		Console.WriteLine($"[WebView] Main window set: display={display}, window={window}");
	}

	public SkiaWebView()
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		base.RequestedWidth = 400.0;
		base.RequestedHeight = 300.0;
		base.BackgroundColor = SKColors.White;
	}

	private static bool InitializeWebKit()
	{
		if (_webkitHandle != IntPtr.Zero)
		{
			return true;
		}
		_webkitHandle = dlopen("libwebkit2gtk-4.1.so.0", 258);
		if (_webkitHandle != IntPtr.Zero)
		{
			_useGtk4 = false;
			_webkitLib = "libwebkit2gtk-4.1.so.0";
		}
		else
		{
			_webkitHandle = dlopen("libwebkit2gtk-4.0.so.37", 258);
			if (_webkitHandle != IntPtr.Zero)
			{
				_useGtk4 = false;
				_webkitLib = "libwebkit2gtk-4.0.so.37";
			}
			else
			{
				_webkitHandle = dlopen("libwebkitgtk-6.0.so.4", 258);
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
		Console.WriteLine("[WebView] Using " + _webkitLib);
		return _webkitWebViewNew != null;
	}

	private static T? LoadFunction<T>(string name) where T : Delegate
	{
		IntPtr intPtr = dlsym(_webkitHandle, name);
		if (intPtr == IntPtr.Zero)
		{
			return null;
		}
		return Marshal.GetDelegateForFunctionPointer<T>(intPtr);
	}

	private void Initialize()
	{
		if (_isInitialized || !InitializeWebKit())
		{
			return;
		}
		try
		{
			if (!_gtkInitialized)
			{
				Environment.SetEnvironmentVariable("GDK_BACKEND", "x11");
				Environment.SetEnvironmentVariable("LIBGL_ALWAYS_SOFTWARE", "1");
				Environment.SetEnvironmentVariable("WEBKIT_DISABLE_COMPOSITING_MODE", "1");
				Console.WriteLine("[WebView] Using X11 backend with software rendering for proper positioning");
				Environment.GetEnvironmentVariable("DISPLAY");
				string environmentVariable = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
				Console.WriteLine("[WebView] XDG_RUNTIME_DIR: " + Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"));
				Console.WriteLine("[WebView] Forcing X11: GDK_BACKEND=x11, WAYLAND_DISPLAY=" + environmentVariable + ", XDG_SESSION_TYPE=x11");
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
				IntPtr value = gdk3_display_get_default();
				Console.WriteLine($"[WebView] GDK display: {value}");
			}
			_webView = _webkitWebViewNew();
			if (_webView == IntPtr.Zero)
			{
				Console.WriteLine("[WebView] Failed to create WebKit view");
				return;
			}
			_webViewInstances[_webView] = this;
			_loadChangedCallback = OnLoadChanged;
			IntPtr functionPointerForDelegate = Marshal.GetFunctionPointerForDelegate(_loadChangedCallback);
			g_signal_connect_data(_webView, "load-changed", functionPointerForDelegate, IntPtr.Zero, IntPtr.Zero, 0);
			Console.WriteLine("[WebView] Connected to load-changed signal");
			int num = Math.Max(800, (int)base.RequestedWidth);
			int num2 = Math.Max(600, (int)base.RequestedHeight);
			if (_useGtk4)
			{
				_gtkWindow = gtk4_window_new();
				gtk4_window_set_title(_gtkWindow, "OpenMaui WebView");
				gtk4_window_set_default_size(_gtkWindow, num, num2);
				gtk4_window_set_child(_gtkWindow, _webView);
				Console.WriteLine($"[WebView] GTK4 window created: {num}x{num2}");
			}
			else
			{
				_gtkWindow = gtk3_window_new(0);
				gtk3_window_set_default_size(_gtkWindow, num, num2);
				gtk3_window_set_title(_gtkWindow, "WebViewDemo");
				gtk3_widget_set_hexpand(_webView, expand: true);
				gtk3_widget_set_vexpand(_webView, expand: true);
				gtk3_widget_set_size_request(_webView, num, num2);
				gtk3_container_add(_gtkWindow, _webView);
				Console.WriteLine($"[WebView] GTK3 TOPLEVEL window created: {num}x{num2}");
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
			Console.WriteLine("[WebView] Initialization failed: " + ex.Message);
		}
	}

	public void LoadUrl(string url)
	{
		if (!_isInitialized)
		{
			Initialize();
		}
		if (_webView != IntPtr.Zero && _webkitLoadUri != null)
		{
			this.Navigating?.Invoke(this, new WebNavigatingEventArgs(url));
			_webkitLoadUri(_webView, url);
			Console.WriteLine("[WebView] URL loaded: " + url);
			ShowNativeWindow();
		}
	}

	public void LoadHtml(string html, string? baseUrl = null)
	{
		Console.WriteLine($"[WebView] LoadHtml called, html length: {html?.Length ?? 0}");
		if (!_isInitialized)
		{
			Initialize();
		}
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
		if (_webView != IntPtr.Zero)
		{
			_webkitReload?.Invoke(_webView);
		}
	}

	public void Stop()
	{
		if (_webView != IntPtr.Zero)
		{
			_webkitStopLoading?.Invoke(_webView);
		}
	}

	private void UpdateJavaScriptSetting()
	{
		if (_webView != IntPtr.Zero && _webkitGetSettings != null && _webkitSetJavascript != null)
		{
			IntPtr intPtr = _webkitGetSettings(_webView);
			if (intPtr != IntPtr.Zero)
			{
				_webkitSetJavascript(intPtr, _javascriptEnabled);
			}
		}
	}

	private void ConfigureWebKitSettings()
	{
		if (_webView == IntPtr.Zero)
		{
			return;
		}
		try
		{
			if (_webkitGetSettings == null)
			{
				return;
			}
			IntPtr intPtr = _webkitGetSettings(_webView);
			if (intPtr == IntPtr.Zero)
			{
				Console.WriteLine("[WebView] Could not get WebKit settings");
				return;
			}
			if (_webkitSetHardwareAcceleration != null)
			{
				_webkitSetHardwareAcceleration(intPtr, 2);
				Console.WriteLine("[WebView] Set hardware acceleration to NEVER (software rendering)");
			}
			else
			{
				Console.WriteLine("[WebView] Warning: Could not set hardware acceleration policy");
			}
			if (_webkitSetWebgl != null)
			{
				_webkitSetWebgl(intPtr, enabled: false);
				Console.WriteLine("[WebView] Disabled WebGL");
			}
			Console.WriteLine("[WebView] WebKit settings configured successfully");
		}
		catch (Exception ex)
		{
			Console.WriteLine("[WebView] Failed to configure settings: " + ex.Message);
		}
	}

	private static void OnLoadChanged(IntPtr webView, int loadEvent, IntPtr userData)
	{
		string[] array = new string[4] { "STARTED", "REDIRECTED", "COMMITTED", "FINISHED" };
		string text = ((loadEvent >= 0 && loadEvent < array.Length) ? array[loadEvent] : loadEvent.ToString());
		Console.WriteLine("[WebView] Load event: " + text);
		if (!_webViewInstances.TryGetValue(webView, out SkiaWebView value))
		{
			return;
		}
		string url = value.Source ?? "";
		if (_webkitGetUri != null)
		{
			IntPtr intPtr = _webkitGetUri(webView);
			if (intPtr != IntPtr.Zero)
			{
				url = Marshal.PtrToStringAnsi(intPtr) ?? "";
			}
		}
		switch (loadEvent)
		{
		case 0:
			value.Navigating?.Invoke(value, new WebNavigatingEventArgs(url));
			break;
		case 3:
			value.Navigated?.Invoke(value, new WebNavigatedEventArgs(url, success: true));
			break;
		}
	}

	public void ProcessEvents()
	{
		if (!_isInitialized)
		{
			return;
		}
		g_main_context_iteration(IntPtr.Zero, mayBlock: false);
		if (_webView != IntPtr.Zero && _webkitGetProgress != null)
		{
			double num = _webkitGetProgress(_webView);
			if (Math.Abs(num - _loadProgress) > 0.01)
			{
				_loadProgress = num;
				this.LoadProgressChanged?.Invoke(this, num);
			}
		}
	}

	private bool CreateX11Container()
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
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
			SKRect bounds = base.Bounds;
			int num = (int)((SKRect)(ref bounds)).Left;
			bounds = base.Bounds;
			int num2 = (int)((SKRect)(ref bounds)).Top;
			bounds = base.Bounds;
			uint num3 = Math.Max(100u, (uint)((SKRect)(ref bounds)).Width);
			bounds = base.Bounds;
			uint num4 = Math.Max(100u, (uint)((SKRect)(ref bounds)).Height);
			if (num3 < 100)
			{
				num3 = 780u;
			}
			if (num4 < 100)
			{
				num4 = 300u;
			}
			Console.WriteLine($"[WebView] Creating X11 container at ({num}, {num2}), size ({num3}x{num4})");
			_x11Container = XCreateSimpleWindow(_mainDisplay, _mainWindow, num, num2, num3, num4, 0u, 0uL, 16777215uL);
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
			Console.WriteLine("[WebView] Error creating X11 container: " + ex.Message);
			return false;
		}
	}

	public void ShowNativeWindow()
	{
		if (!_isInitialized)
		{
			Initialize();
		}
		if (_gtkWindow == IntPtr.Zero)
		{
			return;
		}
		Console.WriteLine("[WebView] Showing native GTK window...");
		if (!_useGtk4)
		{
			gtk3_window_set_decorated(_gtkWindow, decorated: false);
			gtk3_window_set_skip_taskbar_hint(_gtkWindow, setting: true);
			gtk3_window_set_skip_pager_hint(_gtkWindow, setting: true);
			gtk3_window_set_keep_above(_gtkWindow, setting: true);
			gtk3_window_set_type_hint(_gtkWindow, 5);
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
				gtk3_main_iteration_do(blocking: false);
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
		IntPtr intPtr = gtk3_widget_get_window(_gtkWindow);
		if (intPtr != IntPtr.Zero)
		{
			_gtkX11Window = gdk3_x11_window_get_xid(intPtr);
			Console.WriteLine($"[WebView] GTK X11 window: {_gtkX11Window}");
		}
		PositionUsingGtk();
	}

	private void PositionUsingGtk()
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		if (_gtkWindow == IntPtr.Zero || _mainDisplay == IntPtr.Zero)
		{
			return;
		}
		int destX = 0;
		int destY = 0;
		try
		{
			IntPtr dest = XDefaultRootWindow(_mainDisplay);
			XTranslateCoordinates(_mainDisplay, _mainWindow, dest, 0, 0, out destX, out destY, out var _);
		}
		catch
		{
			destX = 0;
			destY = 0;
		}
		int num = destX;
		SKRect bounds = base.Bounds;
		int num2 = num + (int)((SKRect)(ref bounds)).Left;
		int num3 = destY;
		bounds = base.Bounds;
		int num4 = num3 + (int)((SKRect)(ref bounds)).Top;
		bounds = base.Bounds;
		int num5 = Math.Max(100, (int)((SKRect)(ref bounds)).Width);
		bounds = base.Bounds;
		int num6 = Math.Max(100, (int)((SKRect)(ref bounds)).Height);
		DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(53, 6);
		defaultInterpolatedStringHandler.AppendLiteral("[WebView] Position: screen=(");
		defaultInterpolatedStringHandler.AppendFormatted(num2);
		defaultInterpolatedStringHandler.AppendLiteral(", ");
		defaultInterpolatedStringHandler.AppendFormatted(num4);
		defaultInterpolatedStringHandler.AppendLiteral("), size (");
		defaultInterpolatedStringHandler.AppendFormatted(num5);
		defaultInterpolatedStringHandler.AppendLiteral("x");
		defaultInterpolatedStringHandler.AppendFormatted(num6);
		defaultInterpolatedStringHandler.AppendLiteral("), bounds=(");
		bounds = base.Bounds;
		defaultInterpolatedStringHandler.AppendFormatted(((SKRect)(ref bounds)).Left);
		defaultInterpolatedStringHandler.AppendLiteral(",");
		bounds = base.Bounds;
		defaultInterpolatedStringHandler.AppendFormatted(((SKRect)(ref bounds)).Top);
		defaultInterpolatedStringHandler.AppendLiteral(")");
		Console.WriteLine(defaultInterpolatedStringHandler.ToStringAndClear());
		if (!_useGtk4)
		{
			gtk3_window_move(_gtkWindow, num2, num4);
			gtk3_window_resize(_gtkWindow, num5, num6);
			while (gtk3_events_pending())
			{
				gtk3_main_iteration_do(blocking: false);
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
			gtk4_window_set_default_size(_gtkWindow, num5, num6);
		}
	}

	private void PositionWithX11()
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		if (_gtkX11Window != IntPtr.Zero && _mainDisplay != IntPtr.Zero)
		{
			int destX = 0;
			int destY = 0;
			try
			{
				IntPtr dest = XDefaultRootWindow(_mainDisplay);
				XTranslateCoordinates(_mainDisplay, _mainWindow, dest, 0, 0, out destX, out destY, out var _);
			}
			catch
			{
			}
			int num = destX;
			SKRect bounds = base.Bounds;
			int x = num + (int)((SKRect)(ref bounds)).Left;
			int num2 = destY;
			bounds = base.Bounds;
			int y = num2 + (int)((SKRect)(ref bounds)).Top;
			bounds = base.Bounds;
			float val;
			if (!(((SKRect)(ref bounds)).Width > 10f))
			{
				val = 780f;
			}
			else
			{
				bounds = base.Bounds;
				val = ((SKRect)(ref bounds)).Width;
			}
			uint width = (uint)Math.Max(100f, val);
			bounds = base.Bounds;
			float val2;
			if (!(((SKRect)(ref bounds)).Height > 10f))
			{
				val2 = 300f;
			}
			else
			{
				bounds = base.Bounds;
				val2 = ((SKRect)(ref bounds)).Height;
			}
			uint height = (uint)Math.Max(100f, val2);
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
	}

	[DllImport("libX11.so.6")]
	private static extern IntPtr XInternAtom(IntPtr display, string atomName, bool onlyIfExists);

	[DllImport("libX11.so.6")]
	private static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type, int format, int mode, IntPtr data, int nelements);

	[DllImport("libX11.so.6")]
	private static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type, int format, int mode, IntPtr[] data, int nelements);

	private void SetWindowAlwaysOnTop(IntPtr window)
	{
		try
		{
			IntPtr property = XInternAtom(_mainDisplay, "_NET_WM_STATE", onlyIfExists: false);
			IntPtr intPtr = XInternAtom(_mainDisplay, "_NET_WM_STATE_ABOVE", onlyIfExists: false);
			IntPtr type = XInternAtom(_mainDisplay, "ATOM", onlyIfExists: false);
			IntPtr[] data = new IntPtr[1] { intPtr };
			XChangeProperty(_mainDisplay, window, property, type, 32, 0, data, 1);
		}
		catch
		{
		}
	}

	private void EnableOverlayMode()
	{
		if (_gtkWindow == IntPtr.Zero || _useGtk4)
		{
			return;
		}
		try
		{
			gtk3_window_set_type_hint(_gtkWindow, 5);
			gtk3_window_set_skip_taskbar_hint(_gtkWindow, setting: true);
			gtk3_window_set_skip_pager_hint(_gtkWindow, setting: true);
			gtk3_window_set_keep_above(_gtkWindow, setting: true);
			gtk3_window_set_decorated(_gtkWindow, decorated: false);
			Console.WriteLine("[WebView] Overlay mode enabled with UTILITY hint");
		}
		catch (Exception ex)
		{
			Console.WriteLine("[WebView] Failed to enable overlay mode: " + ex.Message);
		}
	}

	private void SetupEmbedding()
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		if (_mainDisplay == IntPtr.Zero || _mainWindow == IntPtr.Zero)
		{
			Console.WriteLine("[WebView] Cannot setup embedding - main window not set");
			return;
		}
		GetWindowPosition(_mainDisplay, _mainWindow, out var x, out var y);
		int num = x;
		SKRect bounds = base.Bounds;
		int num2 = num + (int)((SKRect)(ref bounds)).Left;
		int num3 = y;
		bounds = base.Bounds;
		int num4 = num3 + (int)((SKRect)(ref bounds)).Top;
		bounds = base.Bounds;
		int num5 = Math.Max(100, (int)((SKRect)(ref bounds)).Width);
		bounds = base.Bounds;
		int num6 = Math.Max(100, (int)((SKRect)(ref bounds)).Height);
		Console.WriteLine($"[WebView] Initial position: ({num2}, {num4}), size ({num5}x{num6})");
		if (!_useGtk4)
		{
			gtk3_window_move(_gtkWindow, num2, num4);
			gtk3_window_resize(_gtkWindow, num5, num6);
		}
		else
		{
			gtk4_window_set_default_size(_gtkWindow, num5, num6);
		}
		_lastBounds = base.Bounds;
	}

	private void PositionAtScreenCoordinates()
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		if (_gtkWindow == IntPtr.Zero || _mainDisplay == IntPtr.Zero)
		{
			return;
		}
		int destX = 0;
		int destY = 0;
		try
		{
			IntPtr dest = XDefaultRootWindow(_mainDisplay);
			XTranslateCoordinates(_mainDisplay, _mainWindow, dest, 0, 0, out destX, out destY, out var _);
		}
		catch
		{
		}
		int num = 0;
		int num2 = 0;
		int num3 = destX;
		SKRect bounds = base.Bounds;
		int num4 = num3 + (int)((SKRect)(ref bounds)).Left - num;
		int num5 = destY;
		bounds = base.Bounds;
		int num6 = num5 + (int)((SKRect)(ref bounds)).Top - num2;
		bounds = base.Bounds;
		int num7 = Math.Max(100, (int)((SKRect)(ref bounds)).Width);
		bounds = base.Bounds;
		int num8 = Math.Max(100, (int)((SKRect)(ref bounds)).Height);
		if (Math.Abs(num4 - _lastPosX) > 2 || Math.Abs(num6 - _lastPosY) > 2 || Math.Abs(num7 - _lastWidth) > 2 || Math.Abs(num8 - _lastHeight) > 2)
		{
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(57, 8);
			defaultInterpolatedStringHandler.AppendLiteral("[WebView] Move to (");
			defaultInterpolatedStringHandler.AppendFormatted(num4);
			defaultInterpolatedStringHandler.AppendLiteral(", ");
			defaultInterpolatedStringHandler.AppendFormatted(num6);
			defaultInterpolatedStringHandler.AppendLiteral("), size (");
			defaultInterpolatedStringHandler.AppendFormatted(num7);
			defaultInterpolatedStringHandler.AppendLiteral("x");
			defaultInterpolatedStringHandler.AppendFormatted(num8);
			defaultInterpolatedStringHandler.AppendLiteral("), mainWin=(");
			defaultInterpolatedStringHandler.AppendFormatted(destX);
			defaultInterpolatedStringHandler.AppendLiteral(",");
			defaultInterpolatedStringHandler.AppendFormatted(destY);
			defaultInterpolatedStringHandler.AppendLiteral("), bounds=(");
			bounds = base.Bounds;
			defaultInterpolatedStringHandler.AppendFormatted(((SKRect)(ref bounds)).Left);
			defaultInterpolatedStringHandler.AppendLiteral(",");
			bounds = base.Bounds;
			defaultInterpolatedStringHandler.AppendFormatted(((SKRect)(ref bounds)).Top);
			defaultInterpolatedStringHandler.AppendLiteral(")");
			Console.WriteLine(defaultInterpolatedStringHandler.ToStringAndClear());
			_lastPosX = num4;
			_lastPosY = num6;
			_lastWidth = num7;
			_lastHeight = num8;
			_lastBounds = base.Bounds;
		}
		if (!_useGtk4)
		{
			gtk3_window_move(_gtkWindow, num4, num6);
			gtk3_window_resize(_gtkWindow, num7, num8);
			IntPtr intPtr = gtk3_widget_get_window(_gtkWindow);
			if (intPtr != IntPtr.Zero)
			{
				IntPtr intPtr2 = gdk3_x11_window_get_xid(intPtr);
				if (intPtr2 != IntPtr.Zero)
				{
					XRaiseWindow(_mainDisplay, intPtr2);
					XFlush(_mainDisplay);
				}
			}
			while (gtk3_events_pending())
			{
				gtk3_main_iteration_do(blocking: false);
			}
		}
		else
		{
			gtk4_window_set_default_size(_gtkWindow, num7, num8);
		}
	}

	[DllImport("libX11.so.6")]
	private static extern int XMapRaised(IntPtr display, IntPtr window);

	private IntPtr GetGtkX11Window()
	{
		if (_gtkWindow == IntPtr.Zero)
		{
			return IntPtr.Zero;
		}
		for (int i = 0; i < 50; i++)
		{
			g_main_context_iteration(IntPtr.Zero, mayBlock: false);
		}
		IntPtr result = IntPtr.Zero;
		if (_useGtk4)
		{
			IntPtr intPtr = gtk4_native_get_surface(_gtkWindow);
			if (intPtr != IntPtr.Zero)
			{
				try
				{
					result = gdk4_x11_surface_get_xid(intPtr);
				}
				catch
				{
				}
			}
		}
		else
		{
			IntPtr intPtr2 = gtk3_widget_get_window(_gtkWindow);
			if (intPtr2 != IntPtr.Zero)
			{
				try
				{
					result = gdk3_x11_window_get_xid(intPtr2);
				}
				catch
				{
				}
			}
		}
		return result;
	}

	[DllImport("libX11.so.6")]
	private static extern int XGetWindowAttributes(IntPtr display, IntPtr window, out XWindowAttributes attributes);

	[DllImport("libX11.so.6")]
	private static extern bool XTranslateCoordinates(IntPtr display, IntPtr src, IntPtr dest, int srcX, int srcY, out int destX, out int destY, out IntPtr child);

	[DllImport("libX11.so.6")]
	private static extern IntPtr XDefaultRootWindow(IntPtr display);

	private void GetWindowPosition(IntPtr display, IntPtr window, out int x, out int y)
	{
		x = 0;
		y = 0;
		try
		{
			IntPtr dest = XDefaultRootWindow(display);
			if (XTranslateCoordinates(display, window, dest, 0, 0, out x, out y, out var _))
			{
				Console.WriteLine($"[WebView] Main window at screen ({x}, {y})");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("[WebView] Failed to get window position: " + ex.Message);
		}
	}

	public void UpdateEmbeddedPosition()
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		if (_mainDisplay == IntPtr.Zero)
		{
			return;
		}
		SKRect bounds = base.Bounds;
		if (((SKRect)(ref bounds)).Width < 10f)
		{
			return;
		}
		bounds = base.Bounds;
		if (((SKRect)(ref bounds)).Height < 10f)
		{
			return;
		}
		bounds = base.Bounds;
		if (!(Math.Abs(((SKRect)(ref bounds)).Left - ((SKRect)(ref _lastBounds)).Left) > 1f))
		{
			bounds = base.Bounds;
			if (!(Math.Abs(((SKRect)(ref bounds)).Top - ((SKRect)(ref _lastBounds)).Top) > 1f))
			{
				bounds = base.Bounds;
				if (!(Math.Abs(((SKRect)(ref bounds)).Width - ((SKRect)(ref _lastBounds)).Width) > 1f))
				{
					bounds = base.Bounds;
					if (!(Math.Abs(((SKRect)(ref bounds)).Height - ((SKRect)(ref _lastBounds)).Height) > 1f))
					{
						return;
					}
				}
			}
		}
		_lastBounds = base.Bounds;
		bounds = base.Bounds;
		int num = (int)((SKRect)(ref bounds)).Left;
		bounds = base.Bounds;
		int num2 = (int)((SKRect)(ref bounds)).Top;
		bounds = base.Bounds;
		uint num3 = (uint)Math.Max(10f, ((SKRect)(ref bounds)).Width);
		bounds = base.Bounds;
		uint num4 = (uint)Math.Max(10f, ((SKRect)(ref bounds)).Height);
		if (_isProperlyReparented && _gtkX11Window != IntPtr.Zero)
		{
			Console.WriteLine($"[WebView] UpdateEmbedded (reparented): ({num}, {num2}), size ({num3}x{num4})");
			XMoveResizeWindow(_mainDisplay, _gtkX11Window, num, num2, num3, num4);
			XFlush(_mainDisplay);
		}
		else if (_x11Container != IntPtr.Zero)
		{
			Console.WriteLine($"[WebView] UpdateEmbedded (container): ({num}, {num2}), size ({num3}x{num4})");
			XMoveResizeWindow(_mainDisplay, _x11Container, num, num2, num3, num4);
			if (_gtkX11Window != IntPtr.Zero && _isProperlyReparented)
			{
				XMoveResizeWindow(_mainDisplay, _gtkX11Window, 0, 0, num3, num4);
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
		}
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Expected O, but got Unknown
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Expected O, but got Unknown
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Expected O, but got Unknown
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_023d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0242: Unknown result type (might be due to invalid IL or missing references)
		//IL_0249: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_025a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Expected O, but got Unknown
		//IL_0346: Unknown result type (might be due to invalid IL or missing references)
		//IL_034b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0352: Unknown result type (might be due to invalid IL or missing references)
		//IL_035c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0363: Unknown result type (might be due to invalid IL or missing references)
		//IL_0370: Expected O, but got Unknown
		//IL_040e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0413: Unknown result type (might be due to invalid IL or missing references)
		//IL_0423: Unknown result type (might be due to invalid IL or missing references)
		//IL_042d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0436: Expected O, but got Unknown
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0437: Unknown result type (might be due to invalid IL or missing references)
		//IL_043e: Unknown result type (might be due to invalid IL or missing references)
		//IL_044a: Expected O, but got Unknown
		//IL_047a: Unknown result type (might be due to invalid IL or missing references)
		//IL_047f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0481: Unknown result type (might be due to invalid IL or missing references)
		//IL_0486: Unknown result type (might be due to invalid IL or missing references)
		//IL_0493: Unknown result type (might be due to invalid IL or missing references)
		//IL_049d: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a6: Expected O, but got Unknown
		//IL_04a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ba: Expected O, but got Unknown
		base.OnDraw(canvas, bounds);
		base.Bounds = bounds;
		if (_isInitialized)
		{
			while (gtk3_events_pending())
			{
				gtk3_main_iteration_do(blocking: false);
			}
			if (_gtkWindow != IntPtr.Zero && _mainDisplay != IntPtr.Zero)
			{
				bool flag = Math.Abs(((SKRect)(ref bounds)).Left - ((SKRect)(ref _lastBounds)).Left) > 1f || Math.Abs(((SKRect)(ref bounds)).Top - ((SKRect)(ref _lastBounds)).Top) > 1f || Math.Abs(((SKRect)(ref bounds)).Width - ((SKRect)(ref _lastBounds)).Width) > 1f || Math.Abs(((SKRect)(ref bounds)).Height - ((SKRect)(ref _lastBounds)).Height) > 1f;
				if (!flag && ((SKRect)(ref _lastBounds)).Width < 150f && ((SKRect)(ref bounds)).Width > 150f)
				{
					flag = true;
				}
				if (flag && ((SKRect)(ref bounds)).Width > 50f && ((SKRect)(ref bounds)).Height > 50f)
				{
					PositionUsingGtk();
					_lastBounds = bounds;
				}
			}
		}
		if (_isInitialized && _gtkWindow != IntPtr.Zero)
		{
			return;
		}
		SKPaint val = new SKPaint
		{
			Color = base.BackgroundColor,
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRect(bounds, val);
			SKPaint val2 = new SKPaint
			{
				Color = new SKColor((byte)200, (byte)200, (byte)200),
				Style = (SKPaintStyle)1,
				StrokeWidth = 1f
			};
			try
			{
				canvas.DrawRect(bounds, val2);
				float midX = ((SKRect)(ref bounds)).MidX;
				float midY = ((SKRect)(ref bounds)).MidY;
				SKPaint val3 = new SKPaint
				{
					Color = new SKColor((byte)100, (byte)100, (byte)100),
					Style = (SKPaintStyle)1,
					StrokeWidth = 2f,
					IsAntialias = true
				};
				try
				{
					canvas.DrawCircle(midX, midY - 20f, 25f, val3);
					canvas.DrawLine(midX - 25f, midY - 20f, midX + 25f, midY - 20f, val3);
					canvas.DrawArc(new SKRect(midX - 15f, midY - 45f, midX + 15f, midY + 5f), 0f, 180f, false, val3);
					SKPaint val4 = new SKPaint
					{
						Color = new SKColor((byte)80, (byte)80, (byte)80),
						IsAntialias = true,
						TextSize = 14f
					};
					try
					{
						string text;
						if (!IsSupported)
						{
							text = "WebKitGTK not installed";
						}
						else if (_isInitialized)
						{
							text = (string.IsNullOrEmpty(_source) ? "No URL loaded" : ("Loading: " + _source));
							if (_loadProgress > 0.0 && _loadProgress < 1.0)
							{
								text = $"Loading: {(int)(_loadProgress * 100.0)}%";
							}
						}
						else
						{
							text = "WebView (click to open)";
						}
						float num = val4.MeasureText(text);
						canvas.DrawText(text, midX - num / 2f, midY + 30f, val4);
						if (!IsSupported)
						{
							SKPaint val5 = new SKPaint
							{
								Color = new SKColor((byte)120, (byte)120, (byte)120),
								IsAntialias = true,
								TextSize = 11f
							};
							try
							{
								string text2 = "Install: sudo apt install libwebkit2gtk-4.1-0";
								float num2 = val5.MeasureText(text2);
								canvas.DrawText(text2, midX - num2 / 2f, midY + 50f, val5);
							}
							finally
							{
								((IDisposable)val5)?.Dispose();
							}
						}
						if (!(_loadProgress > 0.0) || !(_loadProgress < 1.0))
						{
							return;
						}
						SKRect val6 = default(SKRect);
						((SKRect)(ref val6))._002Ector(((SKRect)(ref bounds)).Left + 20f, ((SKRect)(ref bounds)).Bottom - 30f, ((SKRect)(ref bounds)).Right - 20f, ((SKRect)(ref bounds)).Bottom - 20f);
						SKPaint val7 = new SKPaint
						{
							Color = new SKColor((byte)230, (byte)230, (byte)230),
							Style = (SKPaintStyle)0
						};
						try
						{
							canvas.DrawRoundRect(new SKRoundRect(val6, 5f), val7);
							float num3 = ((SKRect)(ref val6)).Width * (float)_loadProgress;
							SKRect val8 = new SKRect(((SKRect)(ref val6)).Left, ((SKRect)(ref val6)).Top, ((SKRect)(ref val6)).Left + num3, ((SKRect)(ref val6)).Bottom);
							SKPaint val9 = new SKPaint
							{
								Color = new SKColor((byte)33, (byte)150, (byte)243),
								Style = (SKPaintStyle)0
							};
							try
							{
								canvas.DrawRoundRect(new SKRoundRect(val8, 5f), val9);
							}
							finally
							{
								((IDisposable)val9)?.Dispose();
							}
						}
						finally
						{
							((IDisposable)val7)?.Dispose();
						}
					}
					finally
					{
						((IDisposable)val4)?.Dispose();
					}
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
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

	public static void ProcessGtkEvents()
	{
		bool flag;
		lock (_activeWebViews)
		{
			flag = _activeWebViews.Count > 0;
		}
		if (flag && _gtkInitialized)
		{
			while (g_main_context_iteration(IntPtr.Zero, mayBlock: false))
			{
			}
		}
	}
}
