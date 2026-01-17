// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Interop;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Linux platform WebView using WebKitGTK.
/// This is a native widget overlay that renders on top of the Skia surface.
/// </summary>
public class LinuxWebView : SkiaView
{
    private IntPtr _webView;
    private IntPtr _gtkWindow;
    private bool _initialized;
    private bool _isVisible = true;
    private string? _currentUrl;
    private string? _userAgent;

    // Signal handler IDs for cleanup
    private ulong _loadChangedHandlerId;
    private ulong _decidePolicyHandlerId;
    private ulong _titleChangedHandlerId;

    // Keep delegates alive to prevent GC
    private WebKitGtk.LoadChangedCallback? _loadChangedCallback;
    private WebKitGtk.DecidePolicyCallback? _decidePolicyCallback;
    private WebKitGtk.NotifyCallback? _titleChangedCallback;

    /// <summary>
    /// Event raised when navigation starts.
    /// </summary>
    public event EventHandler<WebViewNavigatingEventArgs>? Navigating;

    /// <summary>
    /// Event raised when navigation completes.
    /// </summary>
    public event EventHandler<WebViewNavigatedEventArgs>? Navigated;

    /// <summary>
    /// Event raised when the page title changes.
    /// </summary>
    public event EventHandler<string?>? TitleChanged;

    /// <summary>
    /// Gets whether the WebView can navigate back.
    /// </summary>
    public bool CanGoBack => _webView != IntPtr.Zero && WebKitGtk.webkit_web_view_can_go_back(_webView);

    /// <summary>
    /// Gets whether the WebView can navigate forward.
    /// </summary>
    public bool CanGoForward => _webView != IntPtr.Zero && WebKitGtk.webkit_web_view_can_go_forward(_webView);

    /// <summary>
    /// Gets the current URL.
    /// </summary>
    public string? CurrentUrl
    {
        get
        {
            if (_webView == IntPtr.Zero)
                return _currentUrl;

            var uriPtr = WebKitGtk.webkit_web_view_get_uri(_webView);
            return WebKitGtk.PtrToStringUtf8(uriPtr) ?? _currentUrl;
        }
    }

    /// <summary>
    /// Gets or sets the user agent string.
    /// </summary>
    public string? UserAgent
    {
        get => _userAgent;
        set
        {
            _userAgent = value;
            if (_webView != IntPtr.Zero && value != null)
            {
                var settings = WebKitGtk.webkit_web_view_get_settings(_webView);
                WebKitGtk.webkit_settings_set_user_agent(settings, value);
            }
        }
    }

    public LinuxWebView()
    {
        // WebView will be initialized when first shown or when source is set
    }

    /// <summary>
    /// Initializes the WebKitGTK WebView.
    /// </summary>
    private void EnsureInitialized()
    {
        if (_initialized)
            return;

        try
        {
            // Initialize GTK if not already done
            int argc = 0;
            IntPtr argv = IntPtr.Zero;
            WebKitGtk.gtk_init_check(ref argc, ref argv);

            // Create a top-level window to host the WebView
            // GTK_WINDOW_TOPLEVEL = 0
            _gtkWindow = WebKitGtk.gtk_window_new(0);
            if (_gtkWindow == IntPtr.Zero)
            {
                Console.WriteLine("[LinuxWebView] Failed to create GTK window");
                return;
            }

            // Configure the window
            WebKitGtk.gtk_window_set_decorated(_gtkWindow, false);
            WebKitGtk.gtk_widget_set_can_focus(_gtkWindow, true);

            // Create the WebKit WebView
            _webView = WebKitGtk.webkit_web_view_new();
            if (_webView == IntPtr.Zero)
            {
                Console.WriteLine("[LinuxWebView] Failed to create WebKit WebView");
                WebKitGtk.gtk_widget_destroy(_gtkWindow);
                _gtkWindow = IntPtr.Zero;
                return;
            }

            // Configure settings
            var settings = WebKitGtk.webkit_web_view_get_settings(_webView);
            WebKitGtk.webkit_settings_set_enable_javascript(settings, true);
            WebKitGtk.webkit_settings_set_enable_webgl(settings, true);
            WebKitGtk.webkit_settings_set_enable_developer_extras(settings, true);
            WebKitGtk.webkit_settings_set_javascript_can_access_clipboard(settings, true);

            if (_userAgent != null)
            {
                WebKitGtk.webkit_settings_set_user_agent(settings, _userAgent);
            }

            // Connect signals
            ConnectSignals();

            // Add WebView to window
            WebKitGtk.gtk_container_add(_gtkWindow, _webView);

            _initialized = true;
            Console.WriteLine("[LinuxWebView] WebKitGTK WebView initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LinuxWebView] Initialization failed: {ex.Message}");
            Console.WriteLine($"[LinuxWebView] Make sure WebKitGTK is installed: sudo apt install libwebkit2gtk-4.1-0");
        }
    }

    private void ConnectSignals()
    {
        // Keep callbacks alive
        _loadChangedCallback = OnLoadChanged;
        _decidePolicyCallback = OnDecidePolicy;
        _titleChangedCallback = OnTitleChanged;

        // Connect load-changed signal
        _loadChangedHandlerId = WebKitGtk.g_signal_connect_data(
            _webView, "load-changed", _loadChangedCallback, IntPtr.Zero, IntPtr.Zero, 0);

        // Connect decide-policy signal for navigation control
        _decidePolicyHandlerId = WebKitGtk.g_signal_connect_data(
            _webView, "decide-policy", _decidePolicyCallback, IntPtr.Zero, IntPtr.Zero, 0);

        // Connect notify::title for title changes
        _titleChangedHandlerId = WebKitGtk.g_signal_connect_data(
            _webView, "notify::title", _titleChangedCallback, IntPtr.Zero, IntPtr.Zero, 0);
    }

    private void OnLoadChanged(IntPtr webView, int loadEvent, IntPtr userData)
    {
        var url = CurrentUrl ?? "";

        switch (loadEvent)
        {
            case WebKitGtk.WEBKIT_LOAD_STARTED:
            case WebKitGtk.WEBKIT_LOAD_REDIRECTED:
                Navigating?.Invoke(this, new WebViewNavigatingEventArgs(url));
                break;

            case WebKitGtk.WEBKIT_LOAD_FINISHED:
                Navigated?.Invoke(this, new WebViewNavigatedEventArgs(url, true));
                break;

            case WebKitGtk.WEBKIT_LOAD_COMMITTED:
                // Page content has started loading
                break;
        }
    }

    private bool OnDecidePolicy(IntPtr webView, IntPtr decision, int decisionType, IntPtr userData)
    {
        if (decisionType == WebKitGtk.WEBKIT_POLICY_DECISION_TYPE_NAVIGATION_ACTION)
        {
            var action = WebKitGtk.webkit_navigation_action_get_request(decision);
            var uriPtr = WebKitGtk.webkit_uri_request_get_uri(action);
            var url = WebKitGtk.PtrToStringUtf8(uriPtr) ?? "";

            var args = new WebViewNavigatingEventArgs(url);
            Navigating?.Invoke(this, args);

            if (args.Cancel)
            {
                WebKitGtk.webkit_policy_decision_ignore(decision);
                return true;
            }
        }

        WebKitGtk.webkit_policy_decision_use(decision);
        return true;
    }

    private void OnTitleChanged(IntPtr webView, IntPtr paramSpec, IntPtr userData)
    {
        var titlePtr = WebKitGtk.webkit_web_view_get_title(_webView);
        var title = WebKitGtk.PtrToStringUtf8(titlePtr);
        TitleChanged?.Invoke(this, title);
    }

    /// <summary>
    /// Navigates to the specified URL.
    /// </summary>
    public void LoadUrl(string url)
    {
        EnsureInitialized();
        if (_webView == IntPtr.Zero)
            return;

        _currentUrl = url;
        WebKitGtk.webkit_web_view_load_uri(_webView, url);
        UpdateWindowPosition();
        ShowWebView();
    }

    /// <summary>
    /// Loads HTML content.
    /// </summary>
    public void LoadHtml(string html, string? baseUrl = null)
    {
        EnsureInitialized();
        if (_webView == IntPtr.Zero)
            return;

        WebKitGtk.webkit_web_view_load_html(_webView, html, baseUrl);
        UpdateWindowPosition();
        ShowWebView();
    }

    /// <summary>
    /// Navigates back in history.
    /// </summary>
    public void GoBack()
    {
        if (_webView != IntPtr.Zero && CanGoBack)
        {
            WebKitGtk.webkit_web_view_go_back(_webView);
        }
    }

    /// <summary>
    /// Navigates forward in history.
    /// </summary>
    public void GoForward()
    {
        if (_webView != IntPtr.Zero && CanGoForward)
        {
            WebKitGtk.webkit_web_view_go_forward(_webView);
        }
    }

    /// <summary>
    /// Reloads the current page.
    /// </summary>
    public void Reload()
    {
        if (_webView != IntPtr.Zero)
        {
            WebKitGtk.webkit_web_view_reload(_webView);
        }
    }

    /// <summary>
    /// Stops loading the current page.
    /// </summary>
    public void Stop()
    {
        if (_webView != IntPtr.Zero)
        {
            WebKitGtk.webkit_web_view_stop_loading(_webView);
        }
    }

    /// <summary>
    /// Evaluates JavaScript and returns the result.
    /// </summary>
    public Task<string?> EvaluateJavaScriptAsync(string script)
    {
        var tcs = new TaskCompletionSource<string?>();

        if (_webView == IntPtr.Zero)
        {
            tcs.SetResult(null);
            return tcs.Task;
        }

        // For now, use fire-and-forget JavaScript execution
        // Full async result handling requires GAsyncReadyCallback marshaling
        WebKitGtk.webkit_web_view_run_javascript(_webView, script, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        tcs.SetResult(null); // Return null for now, full implementation needs async callback

        return tcs.Task;
    }

    /// <summary>
    /// Evaluates JavaScript without waiting for result.
    /// </summary>
    public void Eval(string script)
    {
        if (_webView != IntPtr.Zero)
        {
            WebKitGtk.webkit_web_view_run_javascript(_webView, script, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private void ShowWebView()
    {
        if (_gtkWindow != IntPtr.Zero && _isVisible)
        {
            WebKitGtk.gtk_widget_show_all(_gtkWindow);
        }
    }

    private void HideWebView()
    {
        if (_gtkWindow != IntPtr.Zero)
        {
            WebKitGtk.gtk_widget_hide(_gtkWindow);
        }
    }

    private void UpdateWindowPosition()
    {
        if (_gtkWindow == IntPtr.Zero)
            return;

        // Get the screen position of this view's bounds
        var bounds = Bounds;
        var screenX = (int)bounds.Left;
        var screenY = (int)bounds.Top;
        var width = (int)bounds.Width;
        var height = (int)bounds.Height;

        if (width > 0 && height > 0)
        {
            WebKitGtk.gtk_window_move(_gtkWindow, screenX, screenY);
            WebKitGtk.gtk_window_resize(_gtkWindow, width, height);
        }
    }

    protected override void OnBoundsChanged()
    {
        base.OnBoundsChanged();
        UpdateWindowPosition();
    }

    protected override void OnVisibilityChanged()
    {
        base.OnVisibilityChanged();
        _isVisible = IsVisible;

        if (_isVisible)
        {
            ShowWebView();
        }
        else
        {
            HideWebView();
        }
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Draw a placeholder rectangle where the WebView will be overlaid
        using var paint = new SKPaint
        {
            Color = SkiaTheme.MenuBackgroundSK,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(bounds, paint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = SkiaTheme.BorderMediumSK,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawRect(bounds, borderPaint);

        // Draw "WebView" label if not yet initialized
        if (!_initialized)
        {
            using var textPaint = new SKPaint
            {
                Color = SkiaTheme.TextPlaceholderSK,
                TextSize = 14,
                IsAntialias = true
            };
            var text = "WebView (WebKitGTK)";
            var textBounds = new SKRect();
            textPaint.MeasureText(text, ref textBounds);
            var x = bounds.MidX - textBounds.MidX;
            var y = bounds.MidY - textBounds.MidY;
            canvas.DrawText(text, x, y, textPaint);
        }

        // Process GTK events to keep WebView responsive
        WebKitGtk.ProcessGtkEvents();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Disconnect signals
            if (_webView != IntPtr.Zero)
            {
                if (_loadChangedHandlerId != 0)
                    WebKitGtk.g_signal_handler_disconnect(_webView, _loadChangedHandlerId);
                if (_decidePolicyHandlerId != 0)
                    WebKitGtk.g_signal_handler_disconnect(_webView, _decidePolicyHandlerId);
                if (_titleChangedHandlerId != 0)
                    WebKitGtk.g_signal_handler_disconnect(_webView, _titleChangedHandlerId);
            }

            // Destroy widgets
            if (_gtkWindow != IntPtr.Zero)
            {
                WebKitGtk.gtk_widget_destroy(_gtkWindow);
                _gtkWindow = IntPtr.Zero;
                _webView = IntPtr.Zero; // WebView is destroyed with window
            }

            _loadChangedCallback = null;
            _decidePolicyCallback = null;
            _titleChangedCallback = null;
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// Event args for WebView navigation starting.
/// </summary>
public class WebViewNavigatingEventArgs : EventArgs
{
    public string Url { get; }
    public bool Cancel { get; set; }

    public WebViewNavigatingEventArgs(string url)
    {
        Url = url;
    }
}

/// <summary>
/// Event args for WebView navigation completed.
/// </summary>
public class WebViewNavigatedEventArgs : EventArgs
{
    public string Url { get; }
    public bool Success { get; }

    public WebViewNavigatedEventArgs(string url, bool success)
    {
        Url = url;
        Success = success;
    }
}
