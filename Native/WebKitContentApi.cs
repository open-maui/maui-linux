// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Native;

/// <summary>
/// The part of the WebKit GLib API that is identical across the WPE
/// (libWPEWebKit-2.0) and WebKitGTK (libwebkit2gtk-4.1) ports: custom URI
/// schemes, user scripts, script-message handlers, JavaScript evaluation and
/// navigation. Resolved by symbol from a specific library so one bridge (for
/// example BlazorWebView) can drive a view from either port. Only the 2.40+
/// signatures are used (<c>evaluate_javascript</c>, <c>JSCValue</c> message
/// values); WebKitGTK 4.1 differs in the script-message-handler arity, which
/// <see cref="ApiFlavor"/> covers.
/// </summary>
public sealed unsafe class WebKitContentApi
{
    public enum Flavor
    {
        /// <summary>libWPEWebKit-2.0 / libwebkitgtk-6.0 (WebKit "2.0"/"6.0" API).</summary>
        Modern,
        /// <summary>libwebkit2gtk-4.1 (register_script_message_handler takes no world).</summary>
        WebKitGtk41,
    }

    private const string LibGio = "libgio-2.0.so.0";
    private const string LibGObject = "libgobject-2.0.so.0";
    private const string LibGLib = "libglib-2.0.so.0";

    public Flavor ApiFlavor { get; }
    public string LibraryName { get; }

    private readonly IntPtr _lib;
    private readonly IntPtr _gio;
    private readonly IntPtr _gobject;
    private readonly IntPtr _glib;

    // WebKit
    private readonly delegate* unmanaged<IntPtr, IntPtr> _webViewGetContext;
    private readonly delegate* unmanaged<IntPtr, IntPtr> _webViewGetUserContentManager;
    private readonly delegate* unmanaged<IntPtr, IntPtr, void> _webViewLoadUri;
    private readonly delegate* unmanaged<IntPtr, IntPtr, nint, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void> _evaluateJavascript;
    private readonly delegate* unmanaged<IntPtr, IntPtr, IntPtr*, IntPtr> _evaluateJavascriptFinish;
    private readonly delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void> _registerUriScheme;
    private readonly delegate* unmanaged<IntPtr, IntPtr> _schemeRequestGetUri;
    private readonly delegate* unmanaged<IntPtr, IntPtr> _schemeRequestGetWebView;
    private readonly delegate* unmanaged<IntPtr, IntPtr, long, IntPtr, void> _schemeRequestFinish;
    private readonly delegate* unmanaged<IntPtr, IntPtr, void> _schemeRequestFinishWithResponse;
    private readonly delegate* unmanaged<IntPtr, long, IntPtr> _schemeResponseNew;
    private readonly delegate* unmanaged<IntPtr, uint, IntPtr, void> _schemeResponseSetStatus;
    private readonly delegate* unmanaged<IntPtr, IntPtr, void> _schemeResponseSetContentType;
    private readonly delegate* unmanaged<IntPtr, IntPtr, IntPtr, int> _registerScriptMessageHandlerModern;
    private readonly delegate* unmanaged<IntPtr, IntPtr, int> _registerScriptMessageHandler41;
    private readonly delegate* unmanaged<IntPtr, int, int, IntPtr, IntPtr, IntPtr> _userScriptNew;
    private readonly delegate* unmanaged<IntPtr, IntPtr, void> _userContentManagerAddScript;
    private readonly delegate* unmanaged<IntPtr, void> _userScriptUnref;
    private readonly delegate* unmanaged<IntPtr, IntPtr> _javascriptResultGetJsValue; // 4.1 only
    private readonly delegate* unmanaged<IntPtr, int> _jscValueIsString;
    private readonly delegate* unmanaged<IntPtr, IntPtr> _jscValueToString;

    // GLib / GIO / GObject
    private readonly delegate* unmanaged<IntPtr, nuint, IntPtr, IntPtr> _memoryInputStreamNewFromData;
    private readonly delegate* unmanaged<IntPtr, void> _objectUnref;
    private readonly delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, int, nuint> _signalConnectData;
    private readonly delegate* unmanaged<IntPtr, void> _free;
    private readonly delegate* unmanaged<IntPtr, void> _errorFree;

    private WebKitContentApi(string libraryName, Flavor flavor)
    {
        LibraryName = libraryName;
        ApiFlavor = flavor;
        _lib = NativeLibrary.Load(libraryName);
        _gio = NativeLibrary.Load(LibGio);
        _gobject = NativeLibrary.Load(LibGObject);
        _glib = NativeLibrary.Load(LibGLib);

        _webViewGetContext = (delegate* unmanaged<IntPtr, IntPtr>)Sym(_lib, "webkit_web_view_get_context");
        _webViewGetUserContentManager = (delegate* unmanaged<IntPtr, IntPtr>)Sym(_lib, "webkit_web_view_get_user_content_manager");
        _webViewLoadUri = (delegate* unmanaged<IntPtr, IntPtr, void>)Sym(_lib, "webkit_web_view_load_uri");
        _evaluateJavascript = (delegate* unmanaged<IntPtr, IntPtr, nint, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)Sym(_lib, "webkit_web_view_evaluate_javascript");
        _evaluateJavascriptFinish = (delegate* unmanaged<IntPtr, IntPtr, IntPtr*, IntPtr>)Sym(_lib, "webkit_web_view_evaluate_javascript_finish");
        _registerUriScheme = (delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)Sym(_lib, "webkit_web_context_register_uri_scheme");
        _schemeRequestGetUri = (delegate* unmanaged<IntPtr, IntPtr>)Sym(_lib, "webkit_uri_scheme_request_get_uri");
        _schemeRequestGetWebView = (delegate* unmanaged<IntPtr, IntPtr>)Sym(_lib, "webkit_uri_scheme_request_get_web_view");
        _schemeRequestFinish = (delegate* unmanaged<IntPtr, IntPtr, long, IntPtr, void>)Sym(_lib, "webkit_uri_scheme_request_finish");
        _schemeRequestFinishWithResponse = (delegate* unmanaged<IntPtr, IntPtr, void>)Sym(_lib, "webkit_uri_scheme_request_finish_with_response");
        _schemeResponseNew = (delegate* unmanaged<IntPtr, long, IntPtr>)Sym(_lib, "webkit_uri_scheme_response_new");
        _schemeResponseSetStatus = (delegate* unmanaged<IntPtr, uint, IntPtr, void>)Sym(_lib, "webkit_uri_scheme_response_set_status");
        _schemeResponseSetContentType = (delegate* unmanaged<IntPtr, IntPtr, void>)Sym(_lib, "webkit_uri_scheme_response_set_content_type");
        if (flavor == Flavor.Modern)
            _registerScriptMessageHandlerModern = (delegate* unmanaged<IntPtr, IntPtr, IntPtr, int>)Sym(_lib, "webkit_user_content_manager_register_script_message_handler");
        else
        {
            _registerScriptMessageHandler41 = (delegate* unmanaged<IntPtr, IntPtr, int>)Sym(_lib, "webkit_user_content_manager_register_script_message_handler");
            _javascriptResultGetJsValue = (delegate* unmanaged<IntPtr, IntPtr>)Sym(_lib, "webkit_javascript_result_get_js_value");
        }
        _userScriptNew = (delegate* unmanaged<IntPtr, int, int, IntPtr, IntPtr, IntPtr>)Sym(_lib, "webkit_user_script_new");
        _userContentManagerAddScript = (delegate* unmanaged<IntPtr, IntPtr, void>)Sym(_lib, "webkit_user_content_manager_add_script");
        _userScriptUnref = (delegate* unmanaged<IntPtr, void>)Sym(_lib, "webkit_user_script_unref");
        _jscValueIsString = (delegate* unmanaged<IntPtr, int>)SymJsc("jsc_value_is_string");
        _jscValueToString = (delegate* unmanaged<IntPtr, IntPtr>)SymJsc("jsc_value_to_string");

        _memoryInputStreamNewFromData = (delegate* unmanaged<IntPtr, nuint, IntPtr, IntPtr>)Sym(_gio, "g_memory_input_stream_new_from_data");
        _objectUnref = (delegate* unmanaged<IntPtr, void>)Sym(_gobject, "g_object_unref");
        _signalConnectData = (delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, int, nuint>)Sym(_gobject, "g_signal_connect_data");
        _free = (delegate* unmanaged<IntPtr, void>)Sym(_glib, "g_free");
        _errorFree = (delegate* unmanaged<IntPtr, void>)Sym(_glib, "g_error_free");
    }

    private static IntPtr Sym(IntPtr lib, string name)
    {
        if (!NativeLibrary.TryGetExport(lib, name, out var p) || p == IntPtr.Zero)
            throw new EntryPointNotFoundException($"WebKit symbol '{name}' not found");
        return p;
    }

    private IntPtr SymJsc(string name)
    {
        // WPE bundles JavaScriptCore in libWPEWebKit; WebKitGTK ships it separately.
        if (NativeLibrary.TryGetExport(_lib, name, out var p) && p != IntPtr.Zero)
            return p;
        foreach (var jsc in new[] { "libjavascriptcoregtk-4.1.so.0", "libjavascriptcoregtk-6.0.so.1" })
        {
            if (NativeLibrary.TryLoad(jsc, out var h) && NativeLibrary.TryGetExport(h, name, out p) && p != IntPtr.Zero)
                return p;
        }
        throw new EntryPointNotFoundException($"JavaScriptCore symbol '{name}' not found");
    }

    private static readonly Dictionary<string, WebKitContentApi> s_cache = new();

    /// <summary>Resolves (and caches) the API table for a WebKit library.</summary>
    public static WebKitContentApi For(string libraryName, Flavor flavor)
    {
        lock (s_cache)
        {
            if (!s_cache.TryGetValue(libraryName, out var api))
            {
                api = new WebKitContentApi(libraryName, flavor);
                s_cache[libraryName] = api;
            }
            return api;
        }
    }

    #region Navigation

    public IntPtr GetContext(IntPtr webView) => _webViewGetContext(webView);
    public IntPtr GetUserContentManager(IntPtr webView) => _webViewGetUserContentManager(webView);

    public void LoadUri(IntPtr webView, string uri)
    {
        using var u = new Utf8(uri);
        _webViewLoadUri(webView, u);
    }

    #endregion

    #region Custom URI schemes

    /// <summary>A response to a custom-scheme request.</summary>
    public readonly record struct SchemeResponse(int StatusCode, string StatusMessage, byte[] Body, string ContentType);

    /// <param name="webView">The WebKitWebView* the request came from (schemes are per context, shared by all views).</param>
    public delegate SchemeResponse SchemeHandler(IntPtr webView, string uri);

    private readonly List<object> _rooted = new();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void UriSchemeRequestCallback(IntPtr request, IntPtr userData);

    private readonly HashSet<string> _registeredSchemes = new();

    /// <summary>
    /// Registers <paramref name="scheme"/> on the view's WebKitWebContext. Must be
    /// called before the first load of that scheme. Contexts are shared, so the
    /// registration happens once per process per scheme (WebKit rejects a
    /// second one); the handler receives the originating view for routing.
    /// Returns false when the scheme was already registered (the earlier
    /// handler stays in place).
    /// </summary>
    public bool RegisterUriScheme(IntPtr webView, string scheme, SchemeHandler handler)
    {
        lock (_registeredSchemes)
        {
            if (!_registeredSchemes.Add(scheme))
                return false;
        }
        var context = _webViewGetContext(webView);
        UriSchemeRequestCallback cb = (request, _) =>
        {
            try
            {
                var uri = Marshal.PtrToStringUTF8(_schemeRequestGetUri(request)) ?? string.Empty;
                var response = handler(_schemeRequestGetWebView(request), uri);
                Finish(request, response);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("WebKitContentApi", $"Scheme handler for '{scheme}' threw", ex);
                Finish(request, new SchemeResponse(500, "Internal Error", Array.Empty<byte>(), "text/plain"));
            }
        };
        _rooted.Add(cb);
        using var s = new Utf8(scheme);
        _registerUriScheme(context, s, Marshal.GetFunctionPointerForDelegate(cb), IntPtr.Zero, IntPtr.Zero);
        return true;
    }

    private void Finish(IntPtr request, SchemeResponse response)
    {
        // Copy the body into GLib-owned memory; the stream frees it with g_free.
        var body = response.Body ?? Array.Empty<byte>();
        var native = Marshal.AllocHGlobal(Math.Max(1, body.Length));
        if (body.Length > 0)
            Marshal.Copy(body, 0, native, body.Length);
        var stream = _memoryInputStreamNewFromData(native, (nuint)body.Length, (IntPtr)_free);

        var webkitResponse = _schemeResponseNew(stream, body.Length);
        using (var reason = new Utf8(response.StatusMessage))
            _schemeResponseSetStatus(webkitResponse, (uint)response.StatusCode, reason);
        using (var type = new Utf8(response.ContentType))
            _schemeResponseSetContentType(webkitResponse, type);
        _schemeRequestFinishWithResponse(request, webkitResponse);
        _objectUnref(webkitResponse);
        _objectUnref(stream);
    }

    #endregion

    #region User scripts and script messages

    public const int InjectAllFrames = 0;
    public const int InjectTopFrame = 1;
    public const int InjectAtDocumentStart = 0;
    public const int InjectAtDocumentEnd = 1;

    /// <summary>Adds a user script to the view's content manager.</summary>
    public void AddUserScript(IntPtr webView, string source, int injectedFrames = InjectTopFrame, int injectionTime = InjectAtDocumentStart)
    {
        var manager = _webViewGetUserContentManager(webView);
        using var src = new Utf8(source);
        var script = _userScriptNew(src, injectedFrames, injectionTime, IntPtr.Zero, IntPtr.Zero);
        _userContentManagerAddScript(manager, script);
        _userScriptUnref(script);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ScriptMessageCallback(IntPtr manager, IntPtr value, IntPtr userData);

    /// <summary>
    /// Registers <c>window.webkit.messageHandlers.[name].postMessage(string)</c>
    /// and routes string messages to <paramref name="onMessage"/>.
    /// </summary>
    public void RegisterScriptMessageHandler(IntPtr webView, string name, Action<string> onMessage)
    {
        var manager = _webViewGetUserContentManager(webView);
        ScriptMessageCallback cb = (_, value, _) =>
        {
            try
            {
                var jsValue = ApiFlavor == Flavor.Modern ? value : _javascriptResultGetJsValue(value);
                if (jsValue == IntPtr.Zero || _jscValueIsString(jsValue) == 0) return;
                var str = _jscValueToString(jsValue);
                var message = Marshal.PtrToStringUTF8(str) ?? string.Empty;
                _free(str);
                onMessage(message);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("WebKitContentApi", $"Script message handler '{name}' threw", ex);
            }
        };
        _rooted.Add(cb);

        using var n = new Utf8(name);
        if (ApiFlavor == Flavor.Modern)
            _registerScriptMessageHandlerModern(manager, n, IntPtr.Zero);
        else
            _registerScriptMessageHandler41(manager, n);

        using var signal = new Utf8($"script-message-received::{name}");
        _signalConnectData(manager, signal, Marshal.GetFunctionPointerForDelegate(cb), IntPtr.Zero, IntPtr.Zero, 0);
    }

    #endregion

    #region JavaScript evaluation

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void AsyncReadyCallback(IntPtr source, IntPtr result, IntPtr userData);

    /// <summary>Runs <paramref name="script"/> and completes with its string result (null for non-strings or errors).</summary>
    public Task<string?> EvaluateJavaScriptAsync(IntPtr webView, string script)
    {
        var tcs = new TaskCompletionSource<string?>();
        AsyncReadyCallback? cb = null;
        cb = (source, result, _) =>
        {
            try
            {
                IntPtr error = IntPtr.Zero;
                var jsValue = _evaluateJavascriptFinish(source, result, &error);
                if (error != IntPtr.Zero)
                {
                    var message = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(error, 8));
                    _errorFree(error);
                    DiagnosticLog.Debug("WebKitContentApi", $"evaluate_javascript failed: {message}");
                    tcs.TrySetResult(null);
                    return;
                }
                if (jsValue != IntPtr.Zero && _jscValueIsString(jsValue) != 0)
                {
                    var str = _jscValueToString(jsValue);
                    tcs.TrySetResult(Marshal.PtrToStringUTF8(str));
                    _free(str);
                }
                else
                {
                    tcs.TrySetResult(null);
                }
                if (jsValue != IntPtr.Zero) _objectUnref(jsValue);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally
            {
                lock (_rooted) _rooted.Remove(cb!);
            }
        };
        lock (_rooted) _rooted.Add(cb);

        using var s = new Utf8(script);
        _evaluateJavascript(webView, s, -1, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, Marshal.GetFunctionPointerForDelegate(cb), IntPtr.Zero);
        return tcs.Task;
    }

    /// <summary>Fire-and-forget script execution.</summary>
    public void RunJavaScript(IntPtr webView, string script)
    {
        using var s = new Utf8(script);
        _evaluateJavascript(webView, s, -1, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
    }

    #endregion

    /// <summary>Scoped UTF-8 string for the function-pointer calls.</summary>
    private readonly struct Utf8 : IDisposable
    {
        private readonly IntPtr _ptr;
        public Utf8(string? s) => _ptr = s == null ? IntPtr.Zero : Marshal.StringToCoTaskMemUTF8(s);
        public static implicit operator IntPtr(Utf8 u) => u._ptr;
        public void Dispose() { if (_ptr != IntPtr.Zero) Marshal.FreeCoTaskMem(_ptr); }
    }
}
