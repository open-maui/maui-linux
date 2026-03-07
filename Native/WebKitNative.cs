using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Native;

internal static class WebKitNative
{
    private delegate IntPtr WebKitWebViewNewDelegate();
    private delegate void WebKitWebViewLoadUriDelegate(IntPtr webView, string uri);
    private delegate void WebKitWebViewLoadHtmlDelegate(IntPtr webView, string content, string? baseUri);
    private delegate IntPtr WebKitWebViewGetUriDelegate(IntPtr webView);
    private delegate IntPtr WebKitWebViewGetTitleDelegate(IntPtr webView);
    private delegate void WebKitWebViewGoBackDelegate(IntPtr webView);
    private delegate void WebKitWebViewGoForwardDelegate(IntPtr webView);
    private delegate bool WebKitWebViewCanGoBackDelegate(IntPtr webView);
    private delegate bool WebKitWebViewCanGoForwardDelegate(IntPtr webView);
    private delegate void WebKitWebViewReloadDelegate(IntPtr webView);
    private delegate void WebKitWebViewStopLoadingDelegate(IntPtr webView);
    private delegate IntPtr WebKitWebViewGetSettingsDelegate(IntPtr webView);
    private delegate void WebKitSettingsSetHardwareAccelerationPolicyDelegate(IntPtr settings, int policy);
    private delegate void WebKitSettingsSetEnableJavascriptDelegate(IntPtr settings, bool enabled);
    private delegate void WebKitWebViewSetBackgroundColorDelegate(IntPtr webView, ref GdkRGBA color);

    [StructLayout(LayoutKind.Sequential)]
    public struct GdkRGBA
    {
        public double Red;
        public double Green;
        public double Blue;
        public double Alpha;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LoadChangedCallback(IntPtr webView, int loadEvent, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool ScriptDialogCallback(IntPtr webView, IntPtr dialog, IntPtr userData);

    private delegate ulong GSignalConnectDataDelegate(IntPtr instance, string signalName, Delegate callback, IntPtr userData, IntPtr destroyNotify, int connectFlags);

    // WebKitScriptDialog functions
    private delegate int WebKitScriptDialogGetDialogTypeDelegate(IntPtr dialog);
    private delegate IntPtr WebKitScriptDialogGetMessageDelegate(IntPtr dialog);
    private delegate void WebKitScriptDialogConfirmSetConfirmedDelegate(IntPtr dialog, bool confirmed);
    private delegate IntPtr WebKitScriptDialogPromptGetDefaultTextDelegate(IntPtr dialog);
    private delegate void WebKitScriptDialogPromptSetTextDelegate(IntPtr dialog, string text);

    public enum WebKitLoadEvent
    {
        Started,
        Redirected,
        Committed,
        Finished
    }

    public enum WebKitScriptDialogType
    {
        Alert = 0,
        Confirm = 1,
        Prompt = 2,
        BeforeUnloadConfirm = 3
    }

    private static IntPtr _handle;
    private static bool _initialized;

    private static readonly string[] LibraryNames = new string[4]
    {
        "libwebkit2gtk-4.1.so.0",
        "libwebkit2gtk-4.0.so.37",
        "libwebkit2gtk-4.0.so",
        "libwebkit2gtk-4.1.so"
    };

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
    private static WebKitWebViewGetSettingsDelegate? _webkitGetSettings;
    private static WebKitSettingsSetHardwareAccelerationPolicyDelegate? _webkitSetHardwareAccel;
    private static WebKitSettingsSetEnableJavascriptDelegate? _webkitSetJavascript;
    private static WebKitWebViewSetBackgroundColorDelegate? _webkitSetBackgroundColor;
    private static GSignalConnectDataDelegate? _gSignalConnectData;
    private static WebKitScriptDialogGetDialogTypeDelegate? _webkitScriptDialogGetDialogType;
    private static WebKitScriptDialogGetMessageDelegate? _webkitScriptDialogGetMessage;
    private static WebKitScriptDialogConfirmSetConfirmedDelegate? _webkitScriptDialogConfirmSetConfirmed;
    private static WebKitScriptDialogPromptGetDefaultTextDelegate? _webkitScriptDialogPromptGetDefaultText;
    private static WebKitScriptDialogPromptSetTextDelegate? _webkitScriptDialogPromptSetText;

    private static readonly Dictionary<IntPtr, LoadChangedCallback> _loadChangedCallbacks = new Dictionary<IntPtr, LoadChangedCallback>();
    private static readonly Dictionary<IntPtr, ScriptDialogCallback> _scriptDialogCallbacks = new Dictionary<IntPtr, ScriptDialogCallback>();
    private static readonly Dictionary<IntPtr, ulong> _loadChangedSignalIds = new Dictionary<IntPtr, ulong>();
    private static readonly Dictionary<IntPtr, ulong> _scriptDialogSignalIds = new Dictionary<IntPtr, ulong>();

    /// <summary>
    /// Event raised when a JavaScript dialog (alert, confirm, prompt) is requested.
    /// </summary>
    public static event Action<IntPtr, WebKitScriptDialogType, string>? ScriptDialogRequested;

    private const int RTLD_NOW = 2;
    private const int RTLD_GLOBAL = 256;

    private static IntPtr _gobjectHandle;

    [DllImport("libdl.so.2")]
    private static extern IntPtr dlopen(string? filename, int flags);

    [DllImport("libdl.so.2")]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);

    [DllImport("libdl.so.2")]
    private static extern int dlclose(IntPtr handle);

    [DllImport("libdl.so.2")]
    private static extern IntPtr dlerror();

    [DllImport("libgobject-2.0.so.0")]
    private static extern void g_signal_handler_disconnect(IntPtr instance, ulong handlerId);

    public static bool Initialize()
    {
        if (_initialized)
        {
            return _handle != IntPtr.Zero;
        }
        _initialized = true;

        string[] libraryNames = LibraryNames;
        foreach (string text in libraryNames)
        {
            _handle = dlopen(text, 258);
            if (_handle != IntPtr.Zero)
            {
                DiagnosticLog.Debug("WebKitNative", "Loaded " + text);
                break;
            }
        }

        if (_handle == IntPtr.Zero)
        {
            DiagnosticLog.Warn("WebKitNative", "Failed to load WebKitGTK library");
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
        _webkitGetSettings = LoadFunction<WebKitWebViewGetSettingsDelegate>("webkit_web_view_get_settings");
        _webkitSetHardwareAccel = LoadFunction<WebKitSettingsSetHardwareAccelerationPolicyDelegate>("webkit_settings_set_hardware_acceleration_policy");
        _webkitSetJavascript = LoadFunction<WebKitSettingsSetEnableJavascriptDelegate>("webkit_settings_set_enable_javascript");
        _webkitSetBackgroundColor = LoadFunction<WebKitWebViewSetBackgroundColorDelegate>("webkit_web_view_set_background_color");
        _webkitScriptDialogGetDialogType = LoadFunction<WebKitScriptDialogGetDialogTypeDelegate>("webkit_script_dialog_get_dialog_type");
        _webkitScriptDialogGetMessage = LoadFunction<WebKitScriptDialogGetMessageDelegate>("webkit_script_dialog_get_message");
        _webkitScriptDialogConfirmSetConfirmed = LoadFunction<WebKitScriptDialogConfirmSetConfirmedDelegate>("webkit_script_dialog_confirm_set_confirmed");
        _webkitScriptDialogPromptGetDefaultText = LoadFunction<WebKitScriptDialogPromptGetDefaultTextDelegate>("webkit_script_dialog_prompt_get_default_text");
        _webkitScriptDialogPromptSetText = LoadFunction<WebKitScriptDialogPromptSetTextDelegate>("webkit_script_dialog_prompt_set_text");

        _gobjectHandle = dlopen("libgobject-2.0.so.0", 258);
        if (_gobjectHandle != IntPtr.Zero)
        {
            IntPtr intPtr = dlsym(_gobjectHandle, "g_signal_connect_data");
            if (intPtr != IntPtr.Zero)
            {
                _gSignalConnectData = Marshal.GetDelegateForFunctionPointer<GSignalConnectDataDelegate>(intPtr);
                DiagnosticLog.Debug("WebKitNative", "Loaded g_signal_connect_data");
            }
        }

        return _webkitWebViewNew != null;
    }

    private static T? LoadFunction<T>(string name) where T : Delegate
    {
        if (_handle == IntPtr.Zero)
        {
            return null;
        }
        IntPtr intPtr = dlsym(_handle, name);
        if (intPtr == IntPtr.Zero)
        {
            return null;
        }
        return Marshal.GetDelegateForFunctionPointer<T>(intPtr);
    }

    public static IntPtr WebViewNew()
    {
        if (!Initialize() || _webkitWebViewNew == null)
        {
            return IntPtr.Zero;
        }
        return _webkitWebViewNew();
    }

    public static void LoadUri(IntPtr webView, string uri)
    {
        _webkitLoadUri?.Invoke(webView, uri);
    }

    public static void LoadHtml(IntPtr webView, string content, string? baseUri = null)
    {
        _webkitLoadHtml?.Invoke(webView, content, baseUri);
    }

    public static string? GetUri(IntPtr webView)
    {
        IntPtr intPtr = _webkitGetUri?.Invoke(webView) ?? IntPtr.Zero;
        if (intPtr == IntPtr.Zero)
        {
            return null;
        }
        return Marshal.PtrToStringUTF8(intPtr);
    }

    public static string? GetTitle(IntPtr webView)
    {
        IntPtr intPtr = _webkitGetTitle?.Invoke(webView) ?? IntPtr.Zero;
        if (intPtr == IntPtr.Zero)
        {
            return null;
        }
        return Marshal.PtrToStringUTF8(intPtr);
    }

    public static void GoBack(IntPtr webView)
    {
        _webkitGoBack?.Invoke(webView);
    }

    public static void GoForward(IntPtr webView)
    {
        _webkitGoForward?.Invoke(webView);
    }

    public static bool CanGoBack(IntPtr webView)
    {
        return _webkitCanGoBack?.Invoke(webView) ?? false;
    }

    public static bool CanGoForward(IntPtr webView)
    {
        return _webkitCanGoForward?.Invoke(webView) ?? false;
    }

    public static void Reload(IntPtr webView)
    {
        _webkitReload?.Invoke(webView);
    }

    public static void StopLoading(IntPtr webView)
    {
        _webkitStopLoading?.Invoke(webView);
    }

    public static void ConfigureSettings(IntPtr webView, bool disableHardwareAccel = true)
    {
        if (_webkitGetSettings != null)
        {
            IntPtr intPtr = _webkitGetSettings(webView);
            if (intPtr != IntPtr.Zero && disableHardwareAccel && _webkitSetHardwareAccel != null)
            {
                _webkitSetHardwareAccel(intPtr, 2);
            }
        }
    }

    public static void SetJavascriptEnabled(IntPtr webView, bool enabled)
    {
        if (_webkitGetSettings != null && _webkitSetJavascript != null)
        {
            IntPtr intPtr = _webkitGetSettings(webView);
            if (intPtr != IntPtr.Zero)
            {
                _webkitSetJavascript(intPtr, enabled);
            }
        }
    }

    public static void SetBackgroundColor(IntPtr webView, double r, double g, double b, double a = 1.0)
    {
        if (_webkitSetBackgroundColor != null && webView != IntPtr.Zero)
        {
            var color = new GdkRGBA { Red = r, Green = g, Blue = b, Alpha = a };
            _webkitSetBackgroundColor(webView, ref color);
        }
    }

    public static ulong ConnectLoadChanged(IntPtr webView, LoadChangedCallback callback)
    {
        if (_gSignalConnectData == null || webView == IntPtr.Zero)
        {
            DiagnosticLog.Warn("WebKitNative", "Cannot connect load-changed: signal connect not available");
            return 0uL;
        }
        _loadChangedCallbacks[webView] = callback;
        ulong signalId = _gSignalConnectData(webView, "load-changed", callback, IntPtr.Zero, IntPtr.Zero, 0);
        _loadChangedSignalIds[webView] = signalId;
        return signalId;
    }

    public static void DisconnectLoadChanged(IntPtr webView)
    {
        if (_loadChangedSignalIds.TryGetValue(webView, out ulong signalId) && signalId != 0)
        {
            g_signal_handler_disconnect(webView, signalId);
            _loadChangedSignalIds.Remove(webView);
        }
        _loadChangedCallbacks.Remove(webView);
    }

    /// <summary>
    /// Connects to the script-dialog signal to intercept JavaScript alert/confirm/prompt dialogs.
    /// Returns true from the callback to prevent the default WebKitGTK dialog.
    /// </summary>
    public static ulong ConnectScriptDialog(IntPtr webView, ScriptDialogCallback callback)
    {
        if (_gSignalConnectData == null || webView == IntPtr.Zero)
        {
            DiagnosticLog.Warn("WebKitNative", "Cannot connect script-dialog: signal connect not available");
            return 0uL;
        }
        _scriptDialogCallbacks[webView] = callback;
        ulong signalId = _gSignalConnectData(webView, "script-dialog", callback, IntPtr.Zero, IntPtr.Zero, 0);
        _scriptDialogSignalIds[webView] = signalId;
        return signalId;
    }

    public static void DisconnectScriptDialog(IntPtr webView)
    {
        if (_scriptDialogSignalIds.TryGetValue(webView, out ulong signalId) && signalId != 0)
        {
            g_signal_handler_disconnect(webView, signalId);
            _scriptDialogSignalIds.Remove(webView);
        }
        _scriptDialogCallbacks.Remove(webView);
    }

    /// <summary>
    /// Gets the type of a script dialog.
    /// </summary>
    public static WebKitScriptDialogType GetScriptDialogType(IntPtr dialog)
    {
        if (_webkitScriptDialogGetDialogType == null || dialog == IntPtr.Zero)
            return WebKitScriptDialogType.Alert;
        return (WebKitScriptDialogType)_webkitScriptDialogGetDialogType(dialog);
    }

    /// <summary>
    /// Gets the message from a script dialog.
    /// </summary>
    public static string? GetScriptDialogMessage(IntPtr dialog)
    {
        if (_webkitScriptDialogGetMessage == null || dialog == IntPtr.Zero)
            return null;
        IntPtr msgPtr = _webkitScriptDialogGetMessage(dialog);
        return msgPtr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(msgPtr);
    }

    /// <summary>
    /// Sets the confirmed state for a confirm dialog.
    /// </summary>
    public static void SetScriptDialogConfirmed(IntPtr dialog, bool confirmed)
    {
        _webkitScriptDialogConfirmSetConfirmed?.Invoke(dialog, confirmed);
    }

    /// <summary>
    /// Gets the default text for a prompt dialog.
    /// </summary>
    public static string? GetScriptDialogPromptDefaultText(IntPtr dialog)
    {
        if (_webkitScriptDialogPromptGetDefaultText == null || dialog == IntPtr.Zero)
            return null;
        IntPtr textPtr = _webkitScriptDialogPromptGetDefaultText(dialog);
        return textPtr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(textPtr);
    }

    /// <summary>
    /// Sets the text response for a prompt dialog.
    /// </summary>
    public static void SetScriptDialogPromptText(IntPtr dialog, string text)
    {
        _webkitScriptDialogPromptSetText?.Invoke(dialog, text);
    }

    /// <summary>
    /// Cleans up native library handles. Call on application shutdown.
    /// </summary>
    public static void Cleanup()
    {
        _loadChangedCallbacks.Clear();
        _scriptDialogCallbacks.Clear();
        _loadChangedSignalIds.Clear();
        _scriptDialogSignalIds.Clear();

        if (_gobjectHandle != IntPtr.Zero)
        {
            dlclose(_gobjectHandle);
            _gobjectHandle = IntPtr.Zero;
        }

        if (_handle != IntPtr.Zero)
        {
            dlclose(_handle);
            _handle = IntPtr.Zero;
        }

        _initialized = false;
    }
}
