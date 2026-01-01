// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// WebView implementation using WebKitGTK for Linux.
/// Renders web content in a native GTK window and composites to Skia.
/// </summary>
public class SkiaWebView : SkiaView
{
    #region Native Interop - GTK

    private const string LibGtk4 = "libgtk-4.so.1";
    private const string LibGtk3 = "libgtk-3.so.0";
    private const string LibWebKit2Gtk4 = "libwebkitgtk-6.0.so.4";
    private const string LibWebKit2Gtk3 = "libwebkit2gtk-4.1.so.0";
    private const string LibGObject = "libgobject-2.0.so.0";
    private const string LibGLib = "libglib-2.0.so.0";

    private static bool _useGtk4;
    private static bool _gtkInitialized;
    private static string _webkitLib = LibWebKit2Gtk3;

    // GTK functions
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

    [DllImport(LibGtk3, EntryPoint = "gtk_window_set_default_size")]
    private static extern void gtk3_window_set_default_size(IntPtr window, int width, int height);

    [DllImport(LibGtk4, EntryPoint = "gtk_window_set_child")]
    private static extern void gtk4_window_set_child(IntPtr window, IntPtr child);

    [DllImport(LibGtk3, EntryPoint = "gtk_container_add")]
    private static extern void gtk3_container_add(IntPtr container, IntPtr widget);

    [DllImport(LibGtk4, EntryPoint = "gtk_widget_show")]
    private static extern void gtk4_widget_show(IntPtr widget);

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

    // GObject
    [DllImport(LibGObject, EntryPoint = "g_object_unref")]
    private static extern void g_object_unref(IntPtr obj);

    [DllImport(LibGObject, EntryPoint = "g_signal_connect_data")]
    private static extern ulong g_signal_connect_data(IntPtr instance,
        [MarshalAs(UnmanagedType.LPStr)] string signal,
        IntPtr handler, IntPtr data, IntPtr destroyData, int flags);

    // GLib main loop (for event processing)
    [DllImport(LibGLib, EntryPoint = "g_main_context_iteration")]
    private static extern bool g_main_context_iteration(IntPtr context, bool mayBlock);

    #endregion

    #region WebKit Functions

    // We'll load these dynamically based on available version
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

    [DllImport("libdl.so.2")]
    private static extern IntPtr dlopen([MarshalAs(UnmanagedType.LPStr)] string? filename, int flags);

    [DllImport("libdl.so.2")]
    private static extern IntPtr dlsym(IntPtr handle, [MarshalAs(UnmanagedType.LPStr)] string symbol);

    [DllImport("libdl.so.2")]
    private static extern IntPtr dlerror();

    private const int RTLD_NOW = 2;
    private const int RTLD_GLOBAL = 0x100;

    private static IntPtr _webkitHandle;

    #endregion

    #region Fields

    private IntPtr _gtkWindow;
    private IntPtr _webView;
    private string _source = "";
    private string _html = "";
    private bool _isInitialized;
    private bool _javascriptEnabled = true;
    private double _loadProgress;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the URL to navigate to.
    /// </summary>
    public string Source
    {
        get => _source;
        set
        {
            if (_source != value)
            {
                _source = value;
                if (_isInitialized && !string.IsNullOrEmpty(value))
                {
                    LoadUrl(value);
                }
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the HTML content to display.
    /// </summary>
    public string Html
    {
        get => _html;
        set
        {
            if (_html != value)
            {
                _html = value;
                if (_isInitialized && !string.IsNullOrEmpty(value))
                {
                    LoadHtml(value);
                }
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets whether the WebView can navigate back.
    /// </summary>
    public bool CanGoBack => _webView != IntPtr.Zero && _webkitCanGoBack?.Invoke(_webView) == true;

    /// <summary>
    /// Gets whether the WebView can navigate forward.
    /// </summary>
    public bool CanGoForward => _webView != IntPtr.Zero && _webkitCanGoForward?.Invoke(_webView) == true;

    /// <summary>
    /// Gets the current URL.
    /// </summary>
    public string? CurrentUrl
    {
        get
        {
            if (_webView == IntPtr.Zero || _webkitGetUri == null) return null;
            var ptr = _webkitGetUri(_webView);
            return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) : null;
        }
    }

    /// <summary>
    /// Gets the current page title.
    /// </summary>
    public string? Title
    {
        get
        {
            if (_webView == IntPtr.Zero || _webkitGetTitle == null) return null;
            var ptr = _webkitGetTitle(_webView);
            return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) : null;
        }
    }

    /// <summary>
    /// Gets or sets whether JavaScript is enabled.
    /// </summary>
    public bool JavaScriptEnabled
    {
        get => _javascriptEnabled;
        set
        {
            _javascriptEnabled = value;
            UpdateJavaScriptSetting();
        }
    }

    /// <summary>
    /// Gets the load progress (0.0 to 1.0).
    /// </summary>
    public double LoadProgress => _loadProgress;

    /// <summary>
    /// Gets whether WebKit is available on this system.
    /// </summary>
    public static bool IsSupported => InitializeWebKit();

    #endregion

    #region Events

    public event EventHandler<WebNavigatingEventArgs>? Navigating;
    public event EventHandler<WebNavigatedEventArgs>? Navigated;
    public event EventHandler<string>? TitleChanged;
    public event EventHandler<double>? LoadProgressChanged;

    #endregion

    #region Constructor

    public SkiaWebView()
    {
        RequestedWidth = 400;
        RequestedHeight = 300;
        BackgroundColor = SKColors.White;
    }

    #endregion

    #region Initialization

    private static bool InitializeWebKit()
    {
        if (_webkitHandle != IntPtr.Zero) return true;

        // Try WebKitGTK 6.0 (GTK4) first
        _webkitHandle = dlopen(LibWebKit2Gtk4, RTLD_NOW | RTLD_GLOBAL);
        if (_webkitHandle != IntPtr.Zero)
        {
            _useGtk4 = true;
            _webkitLib = LibWebKit2Gtk4;
        }
        else
        {
            // Fall back to WebKitGTK 4.1 (GTK3)
            _webkitHandle = dlopen(LibWebKit2Gtk3, RTLD_NOW | RTLD_GLOBAL);
            if (_webkitHandle != IntPtr.Zero)
            {
                _useGtk4 = false;
                _webkitLib = LibWebKit2Gtk3;
            }
            else
            {
                // Try older WebKitGTK 4.0
                _webkitHandle = dlopen("libwebkit2gtk-4.0.so.37", RTLD_NOW | RTLD_GLOBAL);
                if (_webkitHandle != IntPtr.Zero)
                {
                    _useGtk4 = false;
                    _webkitLib = "libwebkit2gtk-4.0.so.37";
                }
            }
        }

        if (_webkitHandle == IntPtr.Zero)
        {
            Console.WriteLine("[WebView] WebKitGTK not found. Install with: sudo apt install libwebkit2gtk-4.1-0");
            return false;
        }

        // Load function pointers
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

        Console.WriteLine($"[WebView] Using {_webkitLib}");
        return _webkitWebViewNew != null;
    }

    private static T? LoadFunction<T>(string name) where T : Delegate
    {
        var ptr = dlsym(_webkitHandle, name);
        if (ptr == IntPtr.Zero) return null;
        return Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    private void Initialize()
    {
        if (_isInitialized) return;
        if (!InitializeWebKit()) return;

        try
        {
            // Initialize GTK if needed
            if (!_gtkInitialized)
            {
                if (_useGtk4)
                {
                    gtk4_init();
                }
                else
                {
                    int argc = 0;
                    IntPtr argv = IntPtr.Zero;
                    gtk3_init_check(ref argc, ref argv);
                }
                _gtkInitialized = true;
            }

            // Create WebKit view
            _webView = _webkitWebViewNew!();
            if (_webView == IntPtr.Zero)
            {
                Console.WriteLine("[WebView] Failed to create WebKit view");
                return;
            }

            // Create GTK window to host the WebView
            if (_useGtk4)
            {
                _gtkWindow = gtk4_window_new();
                gtk4_window_set_default_size(_gtkWindow, (int)RequestedWidth, (int)RequestedHeight);
                gtk4_window_set_child(_gtkWindow, _webView);
            }
            else
            {
                _gtkWindow = gtk3_window_new(0); // GTK_WINDOW_TOPLEVEL
                gtk3_window_set_default_size(_gtkWindow, (int)RequestedWidth, (int)RequestedHeight);
                gtk3_container_add(_gtkWindow, _webView);
            }

            UpdateJavaScriptSetting();
            _isInitialized = true;

            // Load initial content
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

    #endregion

    #region Navigation

    public void LoadUrl(string url)
    {
        if (!_isInitialized) Initialize();
        if (_webView == IntPtr.Zero || _webkitLoadUri == null) return;

        Navigating?.Invoke(this, new WebNavigatingEventArgs(url));
        _webkitLoadUri(_webView, url);
    }

    public void LoadHtml(string html, string? baseUrl = null)
    {
        if (!_isInitialized) Initialize();
        if (_webView == IntPtr.Zero || _webkitLoadHtml == null) return;

        _webkitLoadHtml(_webView, html, baseUrl);
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
        if (_webView == IntPtr.Zero || _webkitGetSettings == null || _webkitSetJavascript == null) return;

        var settings = _webkitGetSettings(_webView);
        if (settings != IntPtr.Zero)
        {
            _webkitSetJavascript(settings, _javascriptEnabled);
        }
    }

    #endregion

    #region Event Processing

    /// <summary>
    /// Process pending GTK events. Call this from your main loop.
    /// </summary>
    public void ProcessEvents()
    {
        if (!_isInitialized) return;

        // Process GTK events
        g_main_context_iteration(IntPtr.Zero, false);

        // Update progress
        if (_webView != IntPtr.Zero && _webkitGetProgress != null)
        {
            var progress = _webkitGetProgress(_webView);
            if (Math.Abs(progress - _loadProgress) > 0.01)
            {
                _loadProgress = progress;
                LoadProgressChanged?.Invoke(this, progress);
            }
        }
    }

    /// <summary>
    /// Show the native WebView window (for testing/debugging).
    /// </summary>
    public void ShowNativeWindow()
    {
        if (!_isInitialized) Initialize();
        if (_gtkWindow == IntPtr.Zero) return;

        if (_useGtk4)
        {
            gtk4_widget_show(_gtkWindow);
        }
        else
        {
            gtk3_widget_show_all(_gtkWindow);
        }
    }

    /// <summary>
    /// Hide the native WebView window.
    /// </summary>
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

        // Draw placeholder/loading state
        using var bgPaint = new SKPaint { Color = BackgroundColor, Style = SKPaintStyle.Fill };
        canvas.DrawRect(bounds, bgPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = new SKColor(200, 200, 200),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawRect(bounds, borderPaint);

        // Draw web icon and status
        var centerX = bounds.MidX;
        var centerY = bounds.MidY;

        // Globe icon
        using var iconPaint = new SKPaint
        {
            Color = new SKColor(100, 100, 100),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };
        canvas.DrawCircle(centerX, centerY - 20, 25, iconPaint);
        canvas.DrawLine(centerX - 25, centerY - 20, centerX + 25, centerY - 20, iconPaint);
        canvas.DrawArc(new SKRect(centerX - 15, centerY - 45, centerX + 15, centerY + 5), 0, 180, false, iconPaint);

        // Status text
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

        var textWidth = textPaint.MeasureText(statusText);
        canvas.DrawText(statusText, centerX - textWidth / 2, centerY + 30, textPaint);

        // Draw install hint if not supported
        if (!IsSupported)
        {
            using var hintPaint = new SKPaint
            {
                Color = new SKColor(120, 120, 120),
                IsAntialias = true,
                TextSize = 11
            };
            var hint = "Install: sudo apt install libwebkit2gtk-4.1-0";
            var hintWidth = hintPaint.MeasureText(hint);
            canvas.DrawText(hint, centerX - hintWidth / 2, centerY + 50, hintPaint);
        }

        // Progress bar
        if (_loadProgress > 0 && _loadProgress < 1)
        {
            var progressRect = new SKRect(bounds.Left + 20, bounds.Bottom - 30, bounds.Right - 20, bounds.Bottom - 20);
            using var progressBgPaint = new SKPaint { Color = new SKColor(230, 230, 230), Style = SKPaintStyle.Fill };
            canvas.DrawRoundRect(new SKRoundRect(progressRect, 5), progressBgPaint);

            var filledWidth = progressRect.Width * (float)_loadProgress;
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
            _webView = IntPtr.Zero;
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
