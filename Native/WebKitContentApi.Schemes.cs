// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Native;

/// <summary>
/// Custom URI schemes served per view, with the whole request (method, headers, body) and a
/// response that can come later and carry headers: what WKURLSchemeHandler gives MAUI's
/// HybridWebView on iOS. WebKit registers a scheme once per context (every view shares it), so
/// one native handler per scheme routes each request to the view that made it; a view without
/// its own handler falls back to the one <see cref="RegisterUriScheme"/> set (BlazorWebView's).
/// The request and response API is WebKit 2.36+ (body 2.40+); an older WebKit gives GET requests
/// without headers and responses without extra headers.
/// </summary>
public sealed unsafe partial class WebKitContentApi
{
    private const string LibSoup = "libsoup-3.0.so.0";
    private const int SoupMessageHeadersResponse = 1;

    private sealed class SchemeRoute
    {
        public SchemeHandler? Fallback;
        public readonly ConcurrentDictionary<IntPtr, Action<SchemeRequest>> PerView = new();
    }

    private readonly Dictionary<string, SchemeRoute> _schemeRoutes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _secureSchemes = new(StringComparer.OrdinalIgnoreCase);
    private IntPtr _soup;

    /// <summary>
    /// Serves <paramref name="scheme"/> for one view: every request the view makes to it reaches
    /// <paramref name="handler"/> on the UI thread, which answers now or later with
    /// <see cref="SchemeRequest.Finish"/>. With <paramref name="secure"/> the scheme is a secure
    /// context whose pages can <c>fetch</c> their own origin (CORS-enabled), as https is.
    /// Dispose the result before the view is destroyed; requests still open then are answered 503.
    /// </summary>
    public IDisposable RegisterUriSchemeHandler(IntPtr webView, string scheme, Action<SchemeRequest> handler, bool secure = true)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var route = EnsureScheme(webView, scheme);
        if (secure)
            RegisterSecureScheme(webView, scheme);
        var registration = new SchemeRegistration(this, route, webView, handler);
        route.PerView[webView] = registration.Dispatch;
        return registration;
    }

    private SchemeRoute EnsureScheme(IntPtr webView, string scheme)
    {
        lock (_schemeRoutes)
        {
            if (_schemeRoutes.TryGetValue(scheme, out var existing))
                return existing;
            var route = new SchemeRoute();
            _schemeRoutes[scheme] = route;

            UriSchemeRequestCallback cb = (request, _) => DispatchSchemeRequest(scheme, route, request);
            lock (_rooted) _rooted.Add(cb);
            using var s = new Utf8(scheme);
            _registerUriScheme(_webViewGetContext(webView), s, Marshal.GetFunctionPointerForDelegate(cb), IntPtr.Zero, IntPtr.Zero);
            return route;
        }
    }

    private void RegisterSecureScheme(IntPtr webView, string scheme)
    {
        lock (_secureSchemes)
        {
            if (!_secureSchemes.Add(scheme))
                return;
        }
        var manager = Call("webkit_web_context_get_security_manager", _webViewGetContext(webView));
        if (manager == IntPtr.Zero)
            return;
        using var s = new Utf8(scheme);
        foreach (var name in new[] { "webkit_security_manager_register_uri_scheme_as_secure", "webkit_security_manager_register_uri_scheme_as_cors_enabled" })
        {
            var f = Opt(name);
            if (f != IntPtr.Zero)
                ((delegate* unmanaged<IntPtr, IntPtr, void>)f)(manager, s);
        }
    }

    private void DispatchSchemeRequest(string scheme, SchemeRoute route, IntPtr request)
    {
        try
        {
            var view = _schemeRequestGetWebView(request);
            if (route.PerView.TryGetValue(view, out var perView))
            {
                perView(new SchemeRequest(this, request, view));
                return;
            }
            var uri = Marshal.PtrToStringUTF8(_schemeRequestGetUri(request)) ?? string.Empty;
            var response = route.Fallback?.Invoke(view, uri)
                ?? new SchemeResponse(404, "Not Found", Array.Empty<byte>(), "text/plain");
            Finish(request, response);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WebKitContentApi", $"Scheme handler for '{scheme}' threw", ex);
            Finish(request, new SchemeResponse(500, "Internal Error", Array.Empty<byte>(), "text/plain"));
        }
    }

    private sealed class SchemeRegistration : IDisposable
    {
        private readonly WebKitContentApi _api;
        private readonly SchemeRoute _route;
        private readonly IntPtr _webView;
        private readonly Action<SchemeRequest> _handler;
        private readonly HashSet<SchemeRequest> _open = new();
        private bool _disposed;

        public SchemeRegistration(WebKitContentApi api, SchemeRoute route, IntPtr webView, Action<SchemeRequest> handler)
        {
            _api = api;
            _route = route;
            _webView = webView;
            _handler = handler;
        }

        public void Dispatch(SchemeRequest request)
        {
            lock (_open)
            {
                if (_disposed)
                {
                    request.Finish(503, "Service Unavailable", null, null);
                    return;
                }
                _open.Add(request);
            }
            request.Finished += OnFinished;
            try
            {
                _handler(request);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("WebKitContentApi", $"Scheme handler for {request.Uri} threw", ex);
                request.Finish(500, "Internal Error", null, null);
            }
        }

        private void OnFinished(SchemeRequest request)
        {
            lock (_open) _open.Remove(request);
        }

        public void Dispose()
        {
            SchemeRequest[] open;
            lock (_open)
            {
                if (_disposed) return;
                _disposed = true;
                open = _open.ToArray();
                _open.Clear();
            }
            _route.PerView.TryRemove(new KeyValuePair<IntPtr, Action<SchemeRequest>>(_webView, Dispatch));
            foreach (var request in open)
                request.Finish(503, "Service Unavailable", null, null);
        }
    }

    /// <summary>
    /// One request to a custom scheme: the URI, method, headers and body WebKit sent. Answer it
    /// once with <see cref="Finish"/>, from any thread (the answer is handed to WebKit on the
    /// thread the request came in on).
    /// </summary>
    public sealed class SchemeRequest
    {
        private readonly WebKitContentApi _api;
        private readonly IntPtr _request;
        private readonly SynchronizationContext? _context;
        private readonly int _threadId;
        private int _finished;

        internal SchemeRequest(WebKitContentApi api, IntPtr request, IntPtr webView)
        {
            _api = api;
            _request = request;
            WebView = webView;
            _context = SynchronizationContext.Current;
            _threadId = Environment.CurrentManagedThreadId;
            api.ObjectRef(request); // kept until it is finished, which may be later

            Uri = Marshal.PtrToStringUTF8(api._schemeRequestGetUri(request)) ?? string.Empty;
            Method = Utf8String(api.Call("webkit_uri_scheme_request_get_http_method", request)) ?? "GET";
            Headers = api.ReadRequestHeaders(request);
            Body = api.ReadRequestBody(request);
        }

        /// <summary>The WebKitWebView* that made the request.</summary>
        public IntPtr WebView { get; }

        public string Uri { get; }

        /// <summary>The HTTP method (GET when WebKit does not report one).</summary>
        public string Method { get; }

        /// <summary>The request headers (names compared without case).</summary>
        public IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>The request body (a fetch or form POST), or null when there is none.</summary>
        public byte[]? Body { get; }

        internal event Action<SchemeRequest>? Finished;

        /// <summary>
        /// Answers the request. <paramref name="headers"/> go out with the response (Content-Type
        /// among them when <paramref name="contentType"/> is null). Only the first call counts.
        /// </summary>
        public void Finish(int statusCode, string? reasonPhrase, string? contentType, byte[]? body, IEnumerable<KeyValuePair<string, string>>? headers = null)
        {
            if (Interlocked.Exchange(ref _finished, 1) == 1)
                return;
            var headerList = headers?.ToList();
            if (contentType == null && headerList != null)
                contentType = headerList.FirstOrDefault(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value;

            void Send()
            {
                try
                {
                    _api.FinishWithHeaders(_request, statusCode, reasonPhrase ?? string.Empty, contentType, body ?? Array.Empty<byte>(), headerList);
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Error("WebKitContentApi", $"Answering {Uri} failed", ex);
                }
                finally
                {
                    _api._objectUnref(_request);
                    Finished?.Invoke(this);
                }
            }

            if (Environment.CurrentManagedThreadId != _threadId && _context != null)
                _context.Post(_ => Send(), null);
            else
                Send();
        }
    }

    private void ObjectRef(IntPtr obj)
    {
        var f = Opt("g_object_ref", _gobject);
        if (f != IntPtr.Zero && obj != IntPtr.Zero)
            ((delegate* unmanaged<IntPtr, IntPtr>)f)(obj);
    }

    private IntPtr SoupSym(string name)
    {
        if (_soup == IntPtr.Zero && !NativeLibrary.TryLoad(LibSoup, out _soup))
            return IntPtr.Zero;
        return Opt(name, _soup);
    }

    private IReadOnlyDictionary<string, string> ReadRequestHeaders(IntPtr request)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var soupHeaders = Call("webkit_uri_scheme_request_get_http_headers", request);
        var iterInit = SoupSym("soup_message_headers_iter_init");
        var iterNext = SoupSym("soup_message_headers_iter_next");
        if (soupHeaders == IntPtr.Zero || iterInit == IntPtr.Zero || iterNext == IntPtr.Zero)
            return headers;

        // SoupMessageHeadersIter is three pointers, filled in by iter_init.
        var iter = stackalloc IntPtr[4];
        ((delegate* unmanaged<IntPtr*, IntPtr, void>)iterInit)(iter, soupHeaders);
        IntPtr name, value;
        while (((delegate* unmanaged<IntPtr*, IntPtr*, IntPtr*, int>)iterNext)(iter, &name, &value) != 0)
        {
            var key = Utf8String(name);
            if (string.IsNullOrEmpty(key)) continue;
            var v = Utf8String(value) ?? string.Empty;
            // Repeated headers combine as HTTP lists do (and as WebView2 reports them).
            headers[key] = headers.TryGetValue(key, out var previous) ? previous + ", " + v : v;
        }
        return headers;
    }

    private byte[]? ReadRequestBody(IntPtr request)
    {
        var stream = Call("webkit_uri_scheme_request_get_http_body", request);
        if (stream == IntPtr.Zero)
            return null;
        try
        {
            var read = Opt("g_input_stream_read", _gio);
            if (read == IntPtr.Zero)
                return null;
            using var buffer = new MemoryStream();
            var chunk = new byte[16384];
            while (true)
            {
                IntPtr error = IntPtr.Zero;
                nint n;
                fixed (byte* p = chunk)
                    n = ((delegate* unmanaged<IntPtr, byte*, nuint, IntPtr, IntPtr*, nint>)read)(stream, p, (nuint)chunk.Length, IntPtr.Zero, &error);
                if (error != IntPtr.Zero)
                {
                    DiagnosticLog.Debug("WebKitContentApi", $"Reading a request body failed: {GErrorMessage(error)}");
                    _errorFree(error);
                    break;
                }
                if (n <= 0)
                    break;
                buffer.Write(chunk, 0, (int)n);
            }
            return buffer.ToArray();
        }
        finally
        {
            _objectUnref(stream);
        }
    }

    private void FinishWithHeaders(IntPtr request, int statusCode, string reasonPhrase, string? contentType, byte[] body, List<KeyValuePair<string, string>>? headers)
    {
        var native = Marshal.AllocHGlobal(Math.Max(1, body.Length));
        if (body.Length > 0)
            Marshal.Copy(body, 0, native, body.Length);
        var stream = _memoryInputStreamNewFromData(native, (nuint)body.Length, (IntPtr)_free);

        var response = _schemeResponseNew(stream, body.Length);
        using (var reason = new Utf8(reasonPhrase))
            _schemeResponseSetStatus(response, (uint)statusCode, reason);
        if (!string.IsNullOrEmpty(contentType))
        {
            using var type = new Utf8(contentType);
            _schemeResponseSetContentType(response, type);
        }
        if (headers is { Count: > 0 })
            SetResponseHeaders(response, headers);
        _schemeRequestFinishWithResponse(request, response);
        _objectUnref(response);
        _objectUnref(stream);
    }

    private void SetResponseHeaders(IntPtr response, List<KeyValuePair<string, string>> headers)
    {
        var setHeaders = Opt("webkit_uri_scheme_response_set_http_headers");
        var create = SoupSym("soup_message_headers_new");
        var append = SoupSym("soup_message_headers_append");
        if (setHeaders == IntPtr.Zero || create == IntPtr.Zero || append == IntPtr.Zero)
            return;
        var soupHeaders = ((delegate* unmanaged<int, IntPtr>)create)(SoupMessageHeadersResponse);
        foreach (var (key, value) in headers)
        {
            if (string.IsNullOrEmpty(key)) continue;
            using var k = new Utf8(key);
            using var v = new Utf8(value ?? string.Empty);
            ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)append)(soupHeaders, k, v);
        }
        // The response takes ownership of the headers.
        ((delegate* unmanaged<IntPtr, IntPtr, void>)setHeaders)(response, soupHeaders);
    }

    #region Script messages that can be removed

    /// <summary>
    /// Like <see cref="RegisterScriptMessageHandler"/>, but removable: disposing the result
    /// unregisters <c>window.webkit.messageHandlers.[name]</c> and disconnects the handler. Dispose
    /// it before the view is destroyed.
    /// </summary>
    public IDisposable AddScriptMessageHandler(IntPtr webView, string name, Action<string> onMessage)
    {
        ArgumentNullException.ThrowIfNull(onMessage);
        var manager = _webViewGetUserContentManager(webView);
        ObjectRef(manager);
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

        using (var n = new Utf8(name))
        {
            if (ApiFlavor == Flavor.Modern)
                _registerScriptMessageHandlerModern(manager, n, IntPtr.Zero);
            else
                _registerScriptMessageHandler41(manager, n);
        }
        var id = Connect(manager, $"script-message-received::{name}", cb, keepAlive: false);
        return new ScriptMessageRegistration(this, manager, name, id, cb);
    }

    private sealed class ScriptMessageRegistration : IDisposable
    {
        private readonly WebKitContentApi _api;
        private readonly IntPtr _manager;
        private readonly string _name;
        private readonly ulong _id;
        private Delegate? _callback;

        public ScriptMessageRegistration(WebKitContentApi api, IntPtr manager, string name, ulong id, Delegate callback)
        {
            _api = api;
            _manager = manager;
            _name = name;
            _id = id;
            _callback = callback;
        }

        public void Dispose()
        {
            if (_callback == null) return;
            _api.Disconnect(_manager, _id);
            var f = _api.Opt("webkit_user_content_manager_unregister_script_message_handler");
            if (f != IntPtr.Zero)
            {
                using var n = new Utf8(_name);
                if (_api.ApiFlavor == Flavor.Modern)
                    ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)f)(_manager, n, IntPtr.Zero);
                else
                    ((delegate* unmanaged<IntPtr, IntPtr, void>)f)(_manager, n);
            }
            _api._objectUnref(_manager);
            GC.KeepAlive(_callback);
            _callback = null;
        }
    }

    #endregion
}
