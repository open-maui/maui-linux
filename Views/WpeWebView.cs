// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Views;

/// <summary>
/// A WebView composited inside the Skia render tree: WPE WebKit renders the
/// page headlessly (GPU-backed via DMA-BUF where a DRM render node exists),
/// each finished frame arrives through WPEPlatform's <c>buffer-rendered</c>
/// signal, and this view draws it like any other content. No GTK widget, no
/// reparenting, identical behaviour on native Wayland and X11. Input is
/// translated from the platform's pointer/keyboard/scroll events to
/// <c>WPEEvent</c>s.
/// </summary>
/// <remarks>
/// Frames are copied to an <see cref="SKBitmap"/> (shared-memory import); a
/// zero-copy EGLImage path for the GPU render target is a planned follow-up.
/// The WebKit content API (custom schemes, user scripts, script messages,
/// JavaScript evaluation) is exposed through <see cref="Content"/> and the raw
/// <see cref="NativeWebView"/> pointer for bridges such as BlazorWebView.
/// </remarks>
public class WpeWebView : SkiaView
{
    private static readonly Lazy<IntPtr> s_display = new(CreateDisplay);

    private IntPtr _webView;
    private IntPtr _wpeView;
    private SKBitmap? _frame;
    private readonly Lock _frameLock = new();
    private int _requestedWidth;
    private int _requestedHeight;
    private double _requestedScale;
    private uint _pressedButtons;
    private uint _clickCount;
    private long _lastClickTicks;
    private (double X, double Y) _lastClickPos;
    private uint _lastClickButton;
    private bool _disposedNative;
    private string? _lastUri;

    // Rooted native callbacks (a collected delegate behind a live signal is a crash).
    private readonly BufferRenderedDelegate _onBufferRendered;
    private readonly LoadChangedDelegate _onLoadChanged;
    private readonly DecidePolicyDelegate _onDecidePolicy;
    private readonly ContextMenuDelegate _onContextMenu;
    private readonly ScriptDialogDelegate _onScriptDialog;
    private readonly FileChooserDelegate _onFileChooser;
    private readonly PermissionRequestDelegate _onPermissionRequest;
    private readonly ShowNotificationDelegate _onShowNotification;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int PermissionRequestDelegate(IntPtr webView, IntPtr request, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ShowNotificationDelegate(IntPtr webView, IntPtr notification, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DownloadStartedDelegate(IntPtr session, IntPtr download, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DecideDestinationDelegate(IntPtr download, IntPtr suggestedFilename, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DownloadFinishedDelegate(IntPtr download, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DownloadFailedDelegate(IntPtr download, IntPtr error, IntPtr userData);
    private (float X, float Y) _lastSecondaryPress;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ScriptDialogDelegate(IntPtr webView, IntPtr dialog, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FileChooserDelegate(IntPtr webView, IntPtr request, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetCursorFromNameDelegate(IntPtr view, IntPtr name);

    // Live views by WPEView*, for callbacks that arrive without user data.
    private static readonly Dictionary<IntPtr, WpeWebView> s_viewsByWpeView = new();
    private long _clipboardChangeCount;
    private string? _lastPushedClipboardText;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ContextMenuDelegate(IntPtr webView, IntPtr contextMenu, IntPtr hitTestResult, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void BufferRenderedDelegate(IntPtr view, IntPtr buffer, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void LoadChangedDelegate(IntPtr webView, int loadEvent, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DecidePolicyDelegate(IntPtr webView, IntPtr decision, int decisionType, IntPtr userData);

    /// <summary>Raised when a navigation starts (uri).</summary>
    public event EventHandler<string>? NavigationStarted;

    /// <summary>Raised when a navigation finishes (uri, success).</summary>
    public event EventHandler<(string Url, bool Success)>? NavigationCompleted;

    /// <summary>
    /// Raised before a navigation is followed; set <see cref="NavigationDecisionEventArgs.Cancel"/>
    /// to block it (used for MAUI's cancellable Navigating event and for
    /// BlazorWebView's external-link policy).
    /// </summary>
    public event EventHandler<NavigationDecisionEventArgs>? NavigationDecision;

    public sealed class NavigationDecisionEventArgs : EventArgs
    {
        public string Url { get; }
        public bool Cancel { get; set; }
        public NavigationDecisionEventArgs(string url) => Url = url;
    }

    /// <summary>The WebKitWebView* (WPE port).</summary>
    public IntPtr NativeWebView => _webView;

    /// <summary>Shared WebKit content API bound to libWPEWebKit.</summary>
    public WebKitContentApi Content { get; }

    /// <summary>True when WPE WebKit is installed and this view can be used.</summary>
    public static bool IsSupported => WpeNative.IsAvailable;

    public WpeWebView()
    {
        if (!WpeNative.IsAvailable)
            throw new InvalidOperationException("WPE WebKit (libWPEWebKit-2.0) is not installed");

        Content = WebKitContentApi.For(WpeNative.LibWpeWebKit, WebKitContentApi.Flavor.Modern);
        IsFocusable = true;

        var display = s_display.Value;
        if (display == IntPtr.Zero)
            throw new InvalidOperationException("WPE headless display could not be created");

        _webView = WpeNative.g_object_new(WpeNative.webkit_web_view_get_type(), "display", display, IntPtr.Zero);
        if (_webView == IntPtr.Zero)
            throw new InvalidOperationException("WebKitWebView creation failed");

        _wpeView = WpeNative.webkit_web_view_get_wpe_view(_webView);

        _onBufferRendered = OnBufferRendered;
        _onLoadChanged = OnLoadChanged;
        _onDecidePolicy = OnDecidePolicy;
        _onContextMenu = OnContextMenu;
        _onScriptDialog = OnScriptDialog;
        _onFileChooser = OnFileChooser;
        _onPermissionRequest = OnPermissionRequest;
        _onShowNotification = OnShowNotification;
        WpeNative.g_signal_connect_data(_webView, "permission-request", Marshal.GetFunctionPointerForDelegate(_onPermissionRequest), IntPtr.Zero, IntPtr.Zero, 0);
        WpeNative.g_signal_connect_data(_webView, "show-notification", Marshal.GetFunctionPointerForDelegate(_onShowNotification), IntPtr.Zero, IntPtr.Zero, 0);
        InstallDownloadHandler(_webView);
        lock (s_viewsByWpeView) s_viewsByWpeView[_wpeView] = this;
        InstallCursorHook(_wpeView);
        WpeNative.g_signal_connect_data(_webView, "script-dialog", Marshal.GetFunctionPointerForDelegate(_onScriptDialog), IntPtr.Zero, IntPtr.Zero, 0);
        WpeNative.g_signal_connect_data(_webView, "run-file-chooser", Marshal.GetFunctionPointerForDelegate(_onFileChooser), IntPtr.Zero, IntPtr.Zero, 0);
        WpeNative.g_signal_connect_data(_wpeView, "buffer-rendered", Marshal.GetFunctionPointerForDelegate(_onBufferRendered), IntPtr.Zero, IntPtr.Zero, 0);
        WpeNative.g_signal_connect_data(_webView, "load-changed", Marshal.GetFunctionPointerForDelegate(_onLoadChanged), IntPtr.Zero, IntPtr.Zero, 0);
        WpeNative.g_signal_connect_data(_webView, "decide-policy", Marshal.GetFunctionPointerForDelegate(_onDecidePolicy), IntPtr.Zero, IntPtr.Zero, 0);
        WpeNative.g_signal_connect_data(_webView, "context-menu", Marshal.GetFunctionPointerForDelegate(_onContextMenu), IntPtr.Zero, IntPtr.Zero, 0);

        // Opaque white like every other platform's WebView; pages set their own.
        var white = new WpeNative.WebKitColor { Red = 1, Green = 1, Blue = 1, Alpha = 1 };
        WpeNative.webkit_web_view_set_background_color(_webView, ref white);

        var settings = WpeNative.webkit_web_view_get_settings(_webView);
        if (settings != IntPtr.Zero)
            WpeNative.webkit_settings_set_enable_webgl(settings, 1);

        EnableSpellChecking(_webView);

        DiagnosticLog.Debug("WpeWebView", "Created WPE WebKit view");
    }

    #region Spell checking

    private static bool s_spellCheckingConfigured;

    /// <summary>
    /// WPE ships with spell checking off. Turn it on for the (shared) context
    /// with the user's locale language so editable fields get red underlines
    /// and "Spelling Suggestions" in the context menu (needs enchant and a
    /// dictionary such as hunspell-en-US on the host; harmless without).
    /// </summary>
    private static void EnableSpellChecking(IntPtr webView)
    {
        if (s_spellCheckingConfigured) return;
        s_spellCheckingConfigured = true;
        try
        {
            var context = WpeNative.webkit_web_view_get_context(webView);
            if (context == IntPtr.Zero) return;

            // LANG-style tag: "en_US.UTF-8" -> "en_US"; fall back to en_US.
            var culture = System.Globalization.CultureInfo.CurrentUICulture.Name.Replace('-', '_');
            if (string.IsNullOrEmpty(culture)) culture = "en_US";
            var languages = new[] { culture, culture.Split('_')[0], "en_US" }.Distinct().ToArray();

            // NULL-terminated char** for set_spell_checking_languages.
            var block = Marshal.AllocHGlobal(IntPtr.Size * (languages.Length + 1));
            var strings = new IntPtr[languages.Length];
            try
            {
                for (int i = 0; i < languages.Length; i++)
                {
                    strings[i] = Marshal.StringToCoTaskMemUTF8(languages[i]);
                    Marshal.WriteIntPtr(block, i * IntPtr.Size, strings[i]);
                }
                Marshal.WriteIntPtr(block, languages.Length * IntPtr.Size, IntPtr.Zero);
                WpeNative.webkit_web_context_set_spell_checking_languages(context, block);
            }
            finally
            {
                foreach (var p in strings) if (p != IntPtr.Zero) Marshal.FreeCoTaskMem(p);
                Marshal.FreeHGlobal(block);
            }
            WpeNative.webkit_web_context_set_spell_checking_enabled(context, 1);
            DiagnosticLog.Debug("WpeWebView", $"Spell checking enabled: {string.Join(",", languages)}");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("WpeWebView", $"Spell checking not enabled: {ex.Message}");
        }
    }

    #endregion

    #region Display

    /// <summary>
    /// One headless display per process. WPE infers a DRM device from libdrm
    /// and warns when several GPUs exist (and can then pick one whose gbm map
    /// fails), so prefer an explicit render node: WPE_DRM_DEVICE if set, else
    /// the first /dev/dri/renderD*. Falls back to WPE's own choice.
    /// </summary>
    private static IntPtr CreateDisplay()
    {
        IntPtr display = IntPtr.Zero;
        var device = Environment.GetEnvironmentVariable("WPE_DRM_DEVICE");
        if (string.IsNullOrEmpty(device))
        {
            try
            {
                device = Directory.Exists("/dev/dri")
                    ? Directory.GetFiles("/dev/dri", "renderD*").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault()
                    : null;
            }
            catch { device = null; }
        }

        if (!string.IsNullOrEmpty(device))
        {
            display = WpeNative.wpe_display_headless_new_for_device(device, out var err);
            if (display == IntPtr.Zero)
                DiagnosticLog.Debug("WpeWebView", $"Headless display for {device} failed: {WpeNative.ConsumeError(err, "unknown")}");
            else
                DiagnosticLog.Debug("WpeWebView", $"WPE headless display on {device}");
        }

        if (display == IntPtr.Zero)
            display = WpeNative.wpe_display_headless_new();
        if (display == IntPtr.Zero)
            return IntPtr.Zero;

        if (WpeNative.wpe_display_connect(display, out var connectErr) == 0)
        {
            DiagnosticLog.Error("WpeWebView", $"wpe_display_connect failed: {WpeNative.ConsumeError(connectErr, "unknown")}");
            WpeNative.g_object_unref(display);
            return IntPtr.Zero;
        }
        return display;
    }

    #endregion

    #region Frames

    private void OnBufferRendered(IntPtr view, IntPtr buffer, IntPtr userData)
    {
        try
        {
            int w = WpeNative.wpe_buffer_get_width(buffer);
            int h = WpeNative.wpe_buffer_get_height(buffer);
            var bytes = WpeNative.wpe_buffer_import_to_pixels(buffer, out var err);
            if (bytes == IntPtr.Zero)
            {
                DiagnosticLog.Debug("WpeWebView", $"import_to_pixels failed: {WpeNative.ConsumeError(err, "unknown")}");
                return;
            }

            var data = WpeNative.g_bytes_get_data(bytes, out var size);
            long needed = (long)w * h * 4;
            if (data != IntPtr.Zero && (long)size >= needed && w > 0 && h > 0)
            {
                lock (_frameLock)
                {
                    if (_frame == null || _frame.Width != w || _frame.Height != h)
                    {
                        _frame?.Dispose();
                        _frame = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
                    }
                    unsafe
                    {
                        Buffer.MemoryCopy((void*)data, (void*)_frame.GetPixels(), needed, needed);
                    }
                }
            }
            // The GBytes is owned and cached by the WPEBuffer (transfer none);
            // unreffing it frees pixels WPE still holds.
            Invalidate();
            PullWpeClipboardToSystem();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WpeWebView", "buffer-rendered handler failed", ex);
        }
        finally
        {
            // Mandatory, including on failure: an unreleased buffer stalls the view.
            WpeNative.wpe_view_buffer_released(view, buffer);
        }
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        lock (_frameLock)
        {
            if (_frame == null)
            {
                using var paint = new SKPaint { Color = SKColors.White };
                canvas.DrawRect(bounds, paint);
                return;
            }
            // The frame is logical x scale physical pixels; the canvas is scaled
            // to DpiScale, so drawing into the logical bounds lands 1:1.
            canvas.DrawBitmap(_frame, bounds, new SKSamplingOptions(SKFilterMode.Linear));
        }
    }

    private static float Scale => LinuxApplication.Current?.DpiScale ?? 1f;

    protected override void OnBoundsChanged()
    {
        base.OnBoundsChanged();
        SyncSize();
    }

    /// <summary>
    /// The WPE view is sized in logical (CSS) pixels with the device scale set
    /// on its toplevel, so WebKit lays the page out at the same CSS size as
    /// every other platform and renders the buffer at logical x scale physical
    /// pixels. Event coordinates are logical as well.
    /// </summary>
    private void SyncSize()
    {
        if (_wpeView == IntPtr.Zero) return;
        int w = Math.Max(1, (int)Math.Round(Bounds.Width));
        int h = Math.Max(1, (int)Math.Round(Bounds.Height));
        double scale = Scale;
        var toplevel = WpeNative.wpe_view_get_toplevel(_wpeView);

        if (scale != _requestedScale && toplevel != IntPtr.Zero)
        {
            _requestedScale = scale;
            WpeNative.wpe_toplevel_scale_changed(toplevel, scale);
        }
        if (w == _requestedWidth && h == _requestedHeight) return;
        _requestedWidth = w;
        _requestedHeight = h;
        if (toplevel != IntPtr.Zero)
            WpeNative.wpe_toplevel_resized(toplevel, w, h);
        WpeNative.wpe_view_resized(_wpeView, w, h);
    }

    #endregion

    #region Navigation

    public void Navigate(string url)
    {
        if (_webView == IntPtr.Zero) return;
        SyncSize();
        DiagnosticLog.Debug("WpeWebView", $"load_uri {url}");
        WpeNative.webkit_web_view_load_uri(_webView, url);
    }

    public void LoadHtml(string html, string? baseUrl)
    {
        if (_webView == IntPtr.Zero) return;
        SyncSize();
        WpeNative.webkit_web_view_load_html(_webView, html, baseUrl);
    }

    public void GoBack() { if (_webView != IntPtr.Zero) WpeNative.webkit_web_view_go_back(_webView); }
    public void GoForward() { if (_webView != IntPtr.Zero) WpeNative.webkit_web_view_go_forward(_webView); }
    public void Reload() { if (_webView != IntPtr.Zero) WpeNative.webkit_web_view_reload(_webView); }
    public void StopLoading() { if (_webView != IntPtr.Zero) WpeNative.webkit_web_view_stop_loading(_webView); }
    public bool CanGoBack => _webView != IntPtr.Zero && WpeNative.webkit_web_view_can_go_back(_webView) != 0;
    public bool CanGoForward => _webView != IntPtr.Zero && WpeNative.webkit_web_view_can_go_forward(_webView) != 0;
    public string? CurrentUri => _webView == IntPtr.Zero ? null : WpeNative.PtrToString(WpeNative.webkit_web_view_get_uri(_webView));
    public string? Title => _webView == IntPtr.Zero ? null : WpeNative.PtrToString(WpeNative.webkit_web_view_get_title(_webView));

    public string? UserAgent
    {
        get
        {
            var settings = _webView == IntPtr.Zero ? IntPtr.Zero : WpeNative.webkit_web_view_get_settings(_webView);
            return settings == IntPtr.Zero ? null : WpeNative.PtrToString(WpeNative.webkit_settings_get_user_agent(settings));
        }
        set
        {
            var settings = _webView == IntPtr.Zero ? IntPtr.Zero : WpeNative.webkit_web_view_get_settings(_webView);
            if (settings != IntPtr.Zero)
                WpeNative.webkit_settings_set_user_agent(settings, value);
        }
    }

    public void Eval(string script)
    {
        if (_webView != IntPtr.Zero) Content.RunJavaScript(_webView, script);
    }

    public Task<string?> EvaluateJavaScriptAsync(string script)
        => _webView == IntPtr.Zero ? Task.FromResult<string?>(null) : Content.EvaluateJavaScriptAsync(_webView, script);

    private void OnLoadChanged(IntPtr webView, int loadEvent, IntPtr userData)
    {
        try
        {
            var uri = CurrentUri ?? _lastUri ?? string.Empty;
            DiagnosticLog.Debug("WpeWebView", $"load-changed {loadEvent} {uri}");
            switch (loadEvent)
            {
                case WpeNative.WEBKIT_LOAD_STARTED:
                    _lastUri = uri;
                    NavigationStarted?.Invoke(this, uri);
                    break;
                case WpeNative.WEBKIT_LOAD_COMMITTED:
                    _lastUri = uri;
                    break;
                case WpeNative.WEBKIT_LOAD_FINISHED:
                    NavigationCompleted?.Invoke(this, (uri, true));
                    break;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WpeWebView", "load-changed handler failed", ex);
        }
    }

    private int OnDecidePolicy(IntPtr webView, IntPtr decision, int decisionType, IntPtr userData)
    {
        try
        {
            if (decisionType != WpeNative.WEBKIT_POLICY_DECISION_TYPE_NAVIGATION_ACTION)
                return 0; // default handling for new-window and response decisions

            var handler = NavigationDecision;
            if (handler == null)
                return 0;

            var action = WpeNative.webkit_navigation_policy_decision_get_navigation_action(decision);
            var request = action == IntPtr.Zero ? IntPtr.Zero : WpeNative.webkit_navigation_action_get_request(action);
            var uri = request == IntPtr.Zero ? null : WpeNative.PtrToString(WpeNative.webkit_uri_request_get_uri(request));
            if (uri == null)
                return 0;

            var args = new NavigationDecisionEventArgs(uri);
            handler(this, args);
            if (args.Cancel)
            {
                WpeNative.webkit_policy_decision_ignore(decision);
                return 1;
            }
            return 0;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WpeWebView", "decide-policy handler failed", ex);
            return 0;
        }
    }

    #endregion

    #region Script dialogs and file chooser

    /// <summary>
    /// alert/confirm/prompt: WPE hands the dialog to the embedder. Shown through
    /// the platform's own dialog (asynchronously: the dialog is ref'd, the
    /// signal returns handled, and WebKit is answered when the user does).
    /// </summary>
    private int OnScriptDialog(IntPtr webView, IntPtr dialog, IntPtr userData)
    {
        try
        {
            int type = WpeNative.webkit_script_dialog_get_dialog_type(dialog);
            string message = WpeNative.PtrToString(WpeNative.webkit_script_dialog_get_message(dialog)) ?? string.Empty;
            string? defaultText = type == WpeNative.WEBKIT_SCRIPT_DIALOG_PROMPT
                ? WpeNative.PtrToString(WpeNative.webkit_script_dialog_prompt_get_default_text(dialog))
                : null;
            WpeNative.webkit_script_dialog_ref(dialog);
            _ = ShowScriptDialogAsync(dialog, type, message, defaultText);
            return 1;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WpeWebView", "script-dialog handler failed", ex);
            return 0;
        }
    }

    private async Task ShowScriptDialogAsync(IntPtr dialog, int type, string message, string? defaultText)
    {
        bool confirmed = false;
        string? promptText = null;
        try
        {
            switch (type)
            {
                case WpeNative.WEBKIT_SCRIPT_DIALOG_PROMPT:
                    promptText = await LinuxDialogService.ShowPromptAsync("Prompt", message, "OK", "Cancel", defaultText ?? string.Empty);
                    confirmed = promptText != null;
                    break;
                case WpeNative.WEBKIT_SCRIPT_DIALOG_CONFIRM:
                    confirmed = await LinuxDialogService.ShowAlertAsync("Confirm", message, "OK", "Cancel");
                    break;
                case WpeNative.WEBKIT_SCRIPT_DIALOG_BEFORE_UNLOAD_CONFIRM:
                    confirmed = await LinuxDialogService.ShowAlertAsync("Leave page?", message, "Leave", "Stay");
                    break;
                default:
                    confirmed = await LinuxDialogService.ShowAlertAsync("Alert", message, "OK", null);
                    break;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WpeWebView", "Script dialog failed", ex);
        }
        finally
        {
            try
            {
                switch (type)
                {
                    case WpeNative.WEBKIT_SCRIPT_DIALOG_CONFIRM:
                    case WpeNative.WEBKIT_SCRIPT_DIALOG_BEFORE_UNLOAD_CONFIRM:
                        WpeNative.webkit_script_dialog_confirm_set_confirmed(dialog, confirmed ? 1 : 0);
                        break;
                    case WpeNative.WEBKIT_SCRIPT_DIALOG_PROMPT:
                        WpeNative.webkit_script_dialog_prompt_set_text(dialog, confirmed ? promptText : null);
                        break;
                }
                WpeNative.webkit_script_dialog_close(dialog);
            }
            finally
            {
                WpeNative.webkit_script_dialog_unref(dialog);
            }
        }
    }

    /// <summary>&lt;input type="file"&gt;: routed to the platform file picker.</summary>
    private int OnFileChooser(IntPtr webView, IntPtr request, IntPtr userData)
    {
        try
        {
            bool multiple = WpeNative.webkit_file_chooser_request_get_select_multiple(request) != 0;
            WpeNative.g_object_ref(request);
            _ = PickFilesAsync(request, multiple);
            return 1;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WpeWebView", "run-file-chooser handler failed", ex);
            return 0;
        }
    }

    private static async Task PickFilesAsync(IntPtr request, bool multiple)
    {
        var files = new List<string>();
        try
        {
            var picker = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services
                .GetService(typeof(Microsoft.Maui.Storage.IFilePicker)) as Microsoft.Maui.Storage.IFilePicker
                ?? new FilePickerService();
            if (multiple)
            {
                foreach (var f in await picker.PickMultipleAsync())
                    if (!string.IsNullOrEmpty(f.FullPath)) files.Add(f.FullPath);
            }
            else
            {
                var f = await picker.PickAsync();
                if (f != null && !string.IsNullOrEmpty(f.FullPath)) files.Add(f.FullPath);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WpeWebView", "File chooser failed", ex);
        }

        try
        {
            if (files.Count == 0)
            {
                WpeNative.webkit_file_chooser_request_cancel(request);
                return;
            }
            // NULL-terminated char** for select_files.
            var block = Marshal.AllocHGlobal(IntPtr.Size * (files.Count + 1));
            var strings = new IntPtr[files.Count];
            try
            {
                for (int i = 0; i < files.Count; i++)
                {
                    strings[i] = Marshal.StringToCoTaskMemUTF8(files[i]);
                    Marshal.WriteIntPtr(block, i * IntPtr.Size, strings[i]);
                }
                Marshal.WriteIntPtr(block, files.Count * IntPtr.Size, IntPtr.Zero);
                WpeNative.webkit_file_chooser_request_select_files(request, block);
            }
            finally
            {
                foreach (var p in strings) if (p != IntPtr.Zero) Marshal.FreeCoTaskMem(p);
                Marshal.FreeHGlobal(block);
            }
        }
        finally
        {
            WpeNative.g_object_unref(request);
        }
    }

    #endregion

    #region Permissions, notifications, downloads

    private static NotificationService? s_notifications;
    private static NotificationService Notifications => s_notifications ??= new NotificationService(
        Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Title
        ?? Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "MAUI Application"));

    /// <summary>
    /// Geolocation, camera/microphone/screen and web-notification permissions:
    /// asked through the platform's confirm dialog, answered asynchronously.
    /// Anything else is denied (the safe default).
    /// </summary>
    private int OnPermissionRequest(IntPtr webView, IntPtr request, IntPtr userData)
    {
        try
        {
            string? what = null;
            if (WpeNative.g_type_check_instance_is_a(request, WpeNative.webkit_geolocation_permission_request_get_type()) != 0)
                what = "your location";
            else if (WpeNative.g_type_check_instance_is_a(request, WpeNative.webkit_notification_permission_request_get_type()) != 0)
                what = "show notifications";
            else if (WpeNative.g_type_check_instance_is_a(request, WpeNative.webkit_user_media_permission_request_get_type()) != 0)
            {
                var parts = new List<string>();
                if (WpeNative.webkit_user_media_permission_is_for_video_device(request) != 0) parts.Add("your camera");
                if (WpeNative.webkit_user_media_permission_is_for_audio_device(request) != 0) parts.Add("your microphone");
                if (WpeNative.webkit_user_media_permission_is_for_display_device(request) != 0) parts.Add("your screen");
                what = parts.Count > 0 ? string.Join(" and ", parts) : "media devices";
            }

            if (what == null)
            {
                WpeNative.webkit_permission_request_deny(request);
                return 1;
            }

            var origin = Uri.TryCreate(CurrentUri, UriKind.Absolute, out var u) ? u.Host : "This page";
            WpeNative.g_object_ref(request);
            _ = AskPermissionAsync(request, $"{origin} wants to {(what.StartsWith("show") ? what : "use " + what)}.");
            return 1;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WpeWebView", "permission-request handler failed", ex);
            return 0;
        }
    }

    private static async Task AskPermissionAsync(IntPtr request, string message)
    {
        bool allow = false;
        try { allow = await LinuxDialogService.ShowAlertAsync("Permission", message, "Allow", "Deny"); }
        catch (Exception ex) { DiagnosticLog.Error("WpeWebView", "Permission dialog failed", ex); }
        finally
        {
            if (allow) WpeNative.webkit_permission_request_allow(request);
            else WpeNative.webkit_permission_request_deny(request);
            WpeNative.g_object_unref(request);
        }
    }

    /// <summary>Web Notifications API: routed to the platform notification service.</summary>
    private int OnShowNotification(IntPtr webView, IntPtr notification, IntPtr userData)
    {
        try
        {
            var title = WpeNative.PtrToString(WpeNative.webkit_notification_get_title(notification)) ?? Title ?? "Notification";
            var body = WpeNative.PtrToString(WpeNative.webkit_notification_get_body(notification)) ?? string.Empty;
            _ = Notifications.ShowAsync(title, body);
            return 1;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WpeWebView", "show-notification handler failed", ex);
            return 0;
        }
    }

    // Downloads are announced on the (shared) network session: one handler per session.
    private static readonly HashSet<IntPtr> s_sessionsWithDownloadHandler = new();
    private static DownloadStartedDelegate? s_onDownloadStarted;
    private static readonly List<object> s_downloadCallbacks = new();

    private static void InstallDownloadHandler(IntPtr webView)
    {
        var session = WpeNative.webkit_web_view_get_network_session(webView);
        if (session == IntPtr.Zero) return;
        lock (s_sessionsWithDownloadHandler)
        {
            if (!s_sessionsWithDownloadHandler.Add(session)) return;
            s_onDownloadStarted ??= OnDownloadStarted;
            WpeNative.g_signal_connect_data(session, "download-started", Marshal.GetFunctionPointerForDelegate(s_onDownloadStarted), IntPtr.Zero, IntPtr.Zero, 0);
        }
    }

    /// <summary>
    /// Saves into the user's Downloads folder under the suggested name (made
    /// unique), and reports completion or failure as a notification.
    /// </summary>
    private static void OnDownloadStarted(IntPtr session, IntPtr download, IntPtr userData)
    {
        try
        {
            DecideDestinationDelegate decide = (dl, suggestedPtr, ud) =>
            {
                try
                {
                    var suggested = WpeNative.PtrToString(suggestedPtr);
                    if (string.IsNullOrWhiteSpace(suggested)) suggested = "download";
                    suggested = Path.GetFileName(suggested);
                    var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                    Directory.CreateDirectory(dir);
                    var path = Path.Combine(dir, suggested);
                    var stem = Path.GetFileNameWithoutExtension(suggested);
                    var ext = Path.GetExtension(suggested);
                    for (int n = 1; File.Exists(path); n++)
                        path = Path.Combine(dir, $"{stem} ({n}){ext}");
                    WpeNative.webkit_download_set_destination(dl, path);
                    DiagnosticLog.Debug("WpeWebView", $"Download -> {path}");
                    return 1;
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Error("WpeWebView", "decide-destination failed", ex);
                    return 0;
                }
            };
            DownloadFinishedDelegate finished = (dl, ud) =>
            {
                var dest = WpeNative.PtrToString(WpeNative.webkit_download_get_destination(dl));
                _ = Notifications.ShowAsync("Download complete", dest != null ? Path.GetFileName(dest) : "File saved to Downloads");
            };
            DownloadFailedDelegate failed = (dl, error, ud) =>
            {
                var message = error == IntPtr.Zero ? "unknown error" : (Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(error, 8)) ?? "unknown error");
                var dest = WpeNative.PtrToString(WpeNative.webkit_download_get_destination(dl));
                _ = Notifications.ShowAsync("Download failed", $"{(dest != null ? Path.GetFileName(dest) : "Download")}: {message}");
            };
            lock (s_downloadCallbacks) { s_downloadCallbacks.Add(decide); s_downloadCallbacks.Add(finished); s_downloadCallbacks.Add(failed); }
            WpeNative.g_signal_connect_data(download, "decide-destination", Marshal.GetFunctionPointerForDelegate(decide), IntPtr.Zero, IntPtr.Zero, 0);
            WpeNative.g_signal_connect_data(download, "finished", Marshal.GetFunctionPointerForDelegate(finished), IntPtr.Zero, IntPtr.Zero, 0);
            WpeNative.g_signal_connect_data(download, "failed", Marshal.GetFunctionPointerForDelegate(failed), IntPtr.Zero, IntPtr.Zero, 0);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WpeWebView", "download-started handler failed", ex);
        }
    }

    #endregion

    #region Cursor

    // WPEViewClass.set_cursor_from_name is a class vfunc WebKit calls when the
    // page wants a different pointer (links, text fields). The headless view
    // has no display to apply it to and there is no signal, so the class
    // slot is redirected to us once; offsets from the 2.54 headers
    // (GObjectClass = 136 bytes, then buffers_changed, render_buffer,
    // lock_pointer, unlock_pointer, set_cursor_from_name at 168).
    private const int SetCursorFromNameVfuncOffset = 168;
    private static SetCursorFromNameDelegate? s_cursorHook;
    private static IntPtr s_originalSetCursorFromName;
    private static bool s_cursorHookInstalled;

    private static void InstallCursorHook(IntPtr wpeView)
    {
        if (s_cursorHookInstalled || wpeView == IntPtr.Zero) return;
        s_cursorHookInstalled = true;
        try
        {
            var klass = Marshal.ReadIntPtr(wpeView); // GTypeInstance.g_class
            s_originalSetCursorFromName = Marshal.ReadIntPtr(klass, SetCursorFromNameVfuncOffset);
            s_cursorHook = OnSetCursorFromName;
            Marshal.WriteIntPtr(klass, SetCursorFromNameVfuncOffset, Marshal.GetFunctionPointerForDelegate(s_cursorHook));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("WpeWebView", $"Cursor hook not installed: {ex.Message}");
        }
    }

    private static void OnSetCursorFromName(IntPtr view, IntPtr namePtr)
    {
        try
        {
            if (s_originalSetCursorFromName != IntPtr.Zero)
                Marshal.GetDelegateForFunctionPointer<SetCursorFromNameDelegate>(s_originalSetCursorFromName)(view, namePtr);

            WpeWebView? owner;
            lock (s_viewsByWpeView) s_viewsByWpeView.TryGetValue(view, out owner);
            if (owner == null) return;

            var name = Marshal.PtrToStringUTF8(namePtr) ?? "default";
            var cursor = name switch
            {
                "pointer" or "hand" or "grab" or "grabbing" => Window.CursorType.Hand,
                "text" or "vertical-text" => Window.CursorType.Text,
                _ => Window.CursorType.Arrow,
            };
            if (owner.CursorType == cursor) return;
            owner.CursorType = cursor;
            // WindowContext re-applies CursorType on the next pointer move; apply
            // now too so a hover that stops moving still shows the right cursor.
            LinuxApplication.Current?.FocusedContext?.DisplayWindow?.SetCursor(cursor);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("WpeWebView", $"Cursor update failed: {ex.Message}");
        }
    }

    #endregion

    #region Context menu

    /// <summary>
    /// WPE has no toolkit, so it hands over the menu model and expects the
    /// embedder to show it. The items are presented through the platform's own
    /// context menu (Skia-drawn in native mode, GTK in GTK mode) at the last
    /// secondary-button press, and a chosen item activates WebKit's GAction.
    /// </summary>
    private int OnContextMenu(IntPtr webView, IntPtr contextMenu, IntPtr hitTestResult, IntPtr userData)
    {
        try
        {
            var entries = new List<(string? Title, IntPtr Action, IntPtr Target, bool Enabled)>();
            for (var node = WpeNative.webkit_context_menu_get_items(contextMenu); node != IntPtr.Zero; node = Marshal.ReadIntPtr(node, IntPtr.Size))
            {
                var item = Marshal.ReadIntPtr(node); // GList.data
                if (item == IntPtr.Zero) continue;
                if (WpeNative.webkit_context_menu_item_is_separator(item) != 0)
                {
                    entries.Add((null, IntPtr.Zero, IntPtr.Zero, false));
                    continue;
                }
                var submenu = WpeNative.webkit_context_menu_item_get_submenu(item);
                if (submenu != IntPtr.Zero)
                {
                    // The platform menu is flat: show the submenu as a disabled
                    // header followed by its (indented) items.
                    var header = WpeNative.PtrToString(WpeNative.webkit_context_menu_item_get_title(item))?.Replace("_", string.Empty);
                    var children = new List<(string? Title, IntPtr Action, IntPtr Target, bool Enabled)>();
                    for (var sub = WpeNative.webkit_context_menu_get_items(submenu); sub != IntPtr.Zero; sub = Marshal.ReadIntPtr(sub, IntPtr.Size))
                    {
                        var child = Marshal.ReadIntPtr(sub);
                        if (child == IntPtr.Zero || WpeNative.webkit_context_menu_item_is_separator(child) != 0) continue;
                        var childTitle = WpeNative.PtrToString(WpeNative.webkit_context_menu_item_get_title(child));
                        var childAction = WpeNative.webkit_context_menu_item_get_gaction(child);
                        if (string.IsNullOrEmpty(childTitle) || childAction == IntPtr.Zero) continue;
                        var childTarget = WpeNative.webkit_context_menu_item_get_gaction_target(child);
                        children.Add(("    " + childTitle.Replace("_", string.Empty), WpeNative.g_object_ref(childAction),
                            childTarget == IntPtr.Zero ? IntPtr.Zero : WpeNative.g_variant_ref(childTarget),
                            WpeNative.g_action_get_enabled(childAction) != 0));
                    }
                    if (children.Count == 0) continue;
                    if (!string.IsNullOrEmpty(header))
                        entries.Add((header, IntPtr.Zero, IntPtr.Zero, false));
                    entries.AddRange(children);
                    continue;
                }
                var title = WpeNative.PtrToString(WpeNative.webkit_context_menu_item_get_title(item));
                if (string.IsNullOrEmpty(title)) continue;
                var action = WpeNative.webkit_context_menu_item_get_gaction(item);
                if (action == IntPtr.Zero) continue;
                var target = WpeNative.webkit_context_menu_item_get_gaction_target(item);
                entries.Add((title.Replace("_", string.Empty), WpeNative.g_object_ref(action),
                    target == IntPtr.Zero ? IntPtr.Zero : WpeNative.g_variant_ref(target),
                    WpeNative.g_action_get_enabled(action) != 0));
            }

            // Drop leading/trailing/double separators.
            while (entries.Count > 0 && entries[0].Title == null) entries.RemoveAt(0);
            while (entries.Count > 0 && entries[^1].Title == null) entries.RemoveAt(entries.Count - 1);
            if (entries.Count == 0)
                return 1;

            var refs = entries.Where(e => e.Action != IntPtr.Zero).ToList();
            void ReleaseAll()
            {
                foreach (var e in refs)
                {
                    WpeNative.g_object_unref(e.Action);
                    if (e.Target != IntPtr.Zero) WpeNative.g_variant_unref(e.Target);
                }
                refs.Clear();
            }
            Action? Activate((string? Title, IntPtr Action, IntPtr Target, bool Enabled) e) => e.Action == IntPtr.Zero ? null : () =>
            {
                try { WpeNative.g_action_activate(e.Action, e.Target); ScheduleClipboardPull(); }
                catch (Exception ex) { DiagnosticLog.Error("WpeWebView", $"Context menu action '{e.Title}' failed", ex); }
                finally { ReleaseAll(); }
            };

            float x = _lastSecondaryPress.X, y = _lastSecondaryPress.Y;
            if (LinuxApplication.IsGtkMode)
            {
                var gtkItems = entries.Select(e => e.Title == null ? GtkMenuItem.Separator : new GtkMenuItem(e.Title, Activate(e), e.Enabled)).ToList();
                GtkContextMenuService.ShowContextMenu(gtkItems);
            }
            else
            {
                bool isDarkTheme = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark;
                var items = entries.Select(e => e.Title == null ? ContextMenuItem.Separator : new ContextMenuItem(e.Title, Activate(e), e.Enabled)).ToList();
                LinuxDialogService.ShowContextMenu(new SkiaContextMenu(x, y, items, isDarkTheme));
            }
            return 1;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WpeWebView", "context-menu handler failed", ex);
            return 1;
        }
    }

    #endregion

    #region Clipboard bridge

    private const string TextFormat = "text/plain;charset=utf-8";

    private static IntPtr Clipboard => s_display.IsValueCreated && s_display.Value != IntPtr.Zero
        ? WpeNative.wpe_display_get_clipboard(s_display.Value)
        : IntPtr.Zero;

    /// <summary>
    /// System clipboard -> WPE, before WebKit could read it (focus-in, secondary
    /// click, Ctrl+V). Skipped when the text is what we last pushed or what
    /// WebKit itself last wrote, so richer WebKit content is not clobbered.
    /// </summary>
    private void PushSystemClipboardToWpe()
    {
        var clipboard = Clipboard;
        if (clipboard == IntPtr.Zero) return;
        string? text;
        try { text = SystemClipboard.GetText(); }
        catch { return; }
        if (string.IsNullOrEmpty(text) || text == _lastPushedClipboardText) return;
        if (text == ReadWpeClipboardText(clipboard)) { _lastPushedClipboardText = text; return; }

        var content = WpeNative.wpe_clipboard_content_new();
        WpeNative.wpe_clipboard_content_set_text(content, text);
        WpeNative.wpe_clipboard_set_content(clipboard, content);
        WpeNative.wpe_clipboard_content_unref(content);
        _lastPushedClipboardText = text;
        _clipboardChangeCount = WpeNative.wpe_clipboard_get_change_count(clipboard);
    }

    /// <summary>WPE -> system clipboard whenever WebKit changed its clipboard (Copy/Cut).</summary>
    private void PullWpeClipboardToSystem()
    {
        var clipboard = Clipboard;
        if (clipboard == IntPtr.Zero) return;
        long count = WpeNative.wpe_clipboard_get_change_count(clipboard);
        if (count == _clipboardChangeCount) return;
        _clipboardChangeCount = count;

        var text = ReadWpeClipboardText(clipboard);
        if (string.IsNullOrEmpty(text) || text == _lastPushedClipboardText) return;
        try
        {
            SystemClipboard.SetText(text);
            _lastPushedClipboardText = text;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("WpeWebView", $"Clipboard sync to system failed: {ex.Message}");
        }
    }

    private static string? ReadWpeClipboardText(IntPtr clipboard)
    {
        var p = WpeNative.wpe_clipboard_read_text(clipboard, TextFormat, out var size);
        if (p == IntPtr.Zero) return null;
        var text = Marshal.PtrToStringUTF8(p, checked((int)size));
        WpeNative.g_free(p);
        return text;
    }

    /// <summary>WebKit writes the clipboard asynchronously; check shortly after a copy trigger.</summary>
    private void ScheduleClipboardPull()
    {
        GLibNative.TimeoutAdd(80, () => { PullWpeClipboardToSystem(); return false; });
    }

    #endregion

    #region Input

    private static uint Now => (uint)Environment.TickCount64;

    // WPE event coordinates are in view (logical) pixels.
    private (double X, double Y) ToViewPixels(float absX, float absY)
        => (absX - Bounds.Left, absY - Bounds.Top);

    private static uint ToWpeButton(PointerButton button) => button switch
    {
        PointerButton.Right => WpeNative.WPE_BUTTON_SECONDARY,
        PointerButton.Middle => WpeNative.WPE_BUTTON_MIDDLE,
        _ => WpeNative.WPE_BUTTON_PRIMARY,
    };

    private static uint ToWpeModifiers(KeyModifiers m)
    {
        uint r = 0;
        if ((m & KeyModifiers.Control) != 0) r |= WpeNative.WPE_MODIFIER_KEYBOARD_CONTROL;
        if ((m & KeyModifiers.Shift) != 0) r |= WpeNative.WPE_MODIFIER_KEYBOARD_SHIFT;
        if ((m & KeyModifiers.Alt) != 0) r |= WpeNative.WPE_MODIFIER_KEYBOARD_ALT;
        if ((m & KeyModifiers.Super) != 0) r |= WpeNative.WPE_MODIFIER_KEYBOARD_META;
        if ((m & KeyModifiers.CapsLock) != 0) r |= WpeNative.WPE_MODIFIER_KEYBOARD_CAPS_LOCK;
        return r;
    }

    private void Send(IntPtr evt)
    {
        if (evt == IntPtr.Zero || _wpeView == IntPtr.Zero) return;
        WpeNative.wpe_view_event(_wpeView, evt);
        WpeNative.wpe_event_unref(evt);
    }

    public override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        var (x, y) = ToViewPixels(e.X, e.Y);
        Send(WpeNative.wpe_event_pointer_move_new(WpeNative.WPE_EVENT_POINTER_ENTER, _wpeView, WpeNative.WPE_INPUT_SOURCE_MOUSE, Now, _pressedButtons, x, y, 0, 0));
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        var (x, y) = ToViewPixels(e.X, e.Y);
        Send(WpeNative.wpe_event_pointer_move_new(WpeNative.WPE_EVENT_POINTER_LEAVE, _wpeView, WpeNative.WPE_INPUT_SOURCE_MOUSE, Now, _pressedButtons, x, y, 0, 0));
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var (x, y) = ToViewPixels(e.X, e.Y);
        Send(WpeNative.wpe_event_pointer_move_new(WpeNative.WPE_EVENT_POINTER_MOVE, _wpeView, WpeNative.WPE_INPUT_SOURCE_MOUSE, Now, _pressedButtons, x, y, 0, 0));
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Button == PointerButton.Right)
        {
            _lastSecondaryPress = (e.X, e.Y);
            PushSystemClipboardToWpe(); // so WebKit enables Paste in the menu it builds
        }
        var (x, y) = ToViewPixels(e.X, e.Y);
        uint button = ToWpeButton(e.Button);
        _pressedButtons |= button switch
        {
            WpeNative.WPE_BUTTON_MIDDLE => WpeNative.WPE_MODIFIER_POINTER_BUTTON2,
            WpeNative.WPE_BUTTON_SECONDARY => WpeNative.WPE_MODIFIER_POINTER_BUTTON3,
            _ => WpeNative.WPE_MODIFIER_POINTER_BUTTON1,
        };

        // Multi-click detection (word / paragraph selection): same button within
        // 400 ms and 5 px counts up, capped at 3 like GTK.
        long now = Environment.TickCount64;
        bool sameSpot = Math.Abs(x - _lastClickPos.X) <= 5 && Math.Abs(y - _lastClickPos.Y) <= 5;
        if (button == _lastClickButton && sameSpot && now - _lastClickTicks <= 400 && _clickCount < 3)
            _clickCount++;
        else
            _clickCount = 1;
        _lastClickTicks = now;
        _lastClickPos = (x, y);
        _lastClickButton = button;

        Send(WpeNative.wpe_event_pointer_button_new(WpeNative.WPE_EVENT_POINTER_DOWN, _wpeView, WpeNative.WPE_INPUT_SOURCE_MOUSE, Now, _pressedButtons, button, x, y, _clickCount));
        e.Handled = true;
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        var (x, y) = ToViewPixels(e.X, e.Y);
        uint button = ToWpeButton(e.Button);
        _pressedButtons &= ~(button switch
        {
            WpeNative.WPE_BUTTON_MIDDLE => WpeNative.WPE_MODIFIER_POINTER_BUTTON2,
            WpeNative.WPE_BUTTON_SECONDARY => WpeNative.WPE_MODIFIER_POINTER_BUTTON3,
            _ => WpeNative.WPE_MODIFIER_POINTER_BUTTON1,
        });
        // pressCount must be 0 on release (WPE asserts otherwise and returns no event).
        Send(WpeNative.wpe_event_pointer_button_new(WpeNative.WPE_EVENT_POINTER_UP, _wpeView, WpeNative.WPE_INPUT_SOURCE_MOUSE, Now, _pressedButtons, button, x, y, 0));
        e.Handled = true;
    }

    public override void OnScroll(ScrollEventArgs e)
    {
        var (x, y) = ToViewPixels(e.X, e.Y);
        // Platform deltas are wheel steps (small integers) on X11 and pixel-ish
        // axis values on Wayland; feed WebKit precise pixel deltas either way.
        double dx = Math.Abs(e.DeltaX) <= 3 ? e.DeltaX * 40 : e.DeltaX;
        double dy = Math.Abs(e.DeltaY) <= 3 ? e.DeltaY * 40 : e.DeltaY;
        Send(WpeNative.wpe_event_scroll_new(_wpeView, WpeNative.WPE_INPUT_SOURCE_MOUSE, Now, ToWpeModifiers(e.Modifiers) | _pressedButtons, -dx, -dy, 1, 0, x, y));
        e.Handled = true;
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        uint keyval = Input.KeyMapping.ToKeysym(e.Key, (e.Modifiers & KeyModifiers.Shift) != 0);
        bool printable = Input.KeyMapping.IsPrintable(e.Key);
        // Printable keys arrive again as TextInput (with IME composition applied);
        // only send them here when a modifier turns them into a shortcut.
        bool shortcut = (e.Modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Super)) != 0;
        if (keyval == 0 || (printable && !shortcut)) return;
        bool ctrl = (e.Modifiers & KeyModifiers.Control) != 0;
        if (ctrl && e.Key == Key.V)
            PushSystemClipboardToWpe();
        Send(WpeNative.wpe_event_keyboard_new(WpeNative.WPE_EVENT_KEYBOARD_KEY_DOWN, _wpeView, WpeNative.WPE_INPUT_SOURCE_KEYBOARD, Now, ToWpeModifiers(e.Modifiers), 0, keyval));
        if (ctrl && (e.Key == Key.C || e.Key == Key.X))
            ScheduleClipboardPull();
        e.Handled = true;
    }

    public override void OnKeyUp(KeyEventArgs e)
    {
        uint keyval = Input.KeyMapping.ToKeysym(e.Key, (e.Modifiers & KeyModifiers.Shift) != 0);
        bool printable = Input.KeyMapping.IsPrintable(e.Key);
        bool shortcut = (e.Modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Super)) != 0;
        if (keyval == 0 || (printable && !shortcut)) return;
        Send(WpeNative.wpe_event_keyboard_new(WpeNative.WPE_EVENT_KEYBOARD_KEY_UP, _wpeView, WpeNative.WPE_INPUT_SOURCE_KEYBOARD, Now, ToWpeModifiers(e.Modifiers), 0, keyval));
        e.Handled = true;
    }

    public override void OnTextInput(TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;
        foreach (var rune in e.Text.EnumerateRunes())
        {
            // X keysyms: Latin-1 maps directly, everything else is 0x01000000 | codepoint.
            uint keyval = rune.Value < 0x100 ? (uint)rune.Value : 0x01000000u | (uint)rune.Value;
            Send(WpeNative.wpe_event_keyboard_new(WpeNative.WPE_EVENT_KEYBOARD_KEY_DOWN, _wpeView, WpeNative.WPE_INPUT_SOURCE_KEYBOARD, Now, 0, 0, keyval));
            Send(WpeNative.wpe_event_keyboard_new(WpeNative.WPE_EVENT_KEYBOARD_KEY_UP, _wpeView, WpeNative.WPE_INPUT_SOURCE_KEYBOARD, Now, 0, 0, keyval));
        }
        e.Handled = true;
    }

    public override void OnFocusGained()
    {
        base.OnFocusGained();
        PushSystemClipboardToWpe();
        if (_wpeView != IntPtr.Zero) WpeNative.wpe_view_focus_in(_wpeView);
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();
        if (_wpeView != IntPtr.Zero) WpeNative.wpe_view_focus_out(_wpeView);
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (!_disposedNative)
        {
            _disposedNative = true;
            if (_webView != IntPtr.Zero)
            {
                lock (s_viewsByWpeView) s_viewsByWpeView.Remove(_wpeView);
                WpeNative.g_object_unref(_webView);
                _webView = IntPtr.Zero;
                _wpeView = IntPtr.Zero;
            }
            lock (_frameLock)
            {
                _frame?.Dispose();
                _frame = null;
            }
        }
        base.Dispose(disposing);
    }
}
