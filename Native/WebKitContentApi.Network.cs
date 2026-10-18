// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Native;

/// <summary>
/// A network of its own for one view, routed through a proxy: what HybridWebView's
/// <c>WebResourceRequested</c> needs to see http and https requests. On WPE (2.0 API) the
/// network is a <c>WebKitNetworkSession</c>; on WebKitGTK 4.1 it is a <c>WebKitWebContext</c>
/// with its own <c>WebKitWebsiteDataManager</c>, where 4.1 keeps the proxy settings and the
/// allowed TLS certificates. Resolved by name: a WebKit without one of these symbols reports
/// <see cref="SupportsProxyNetworks"/> false.
/// </summary>
public sealed unsafe partial class WebKitContentApi
{
    // WebKitNetworkProxyMode
    private const int ProxyModeDefault = 0;
    private const int ProxyModeCustom = 2;

    // WebKitCredentialPersistence
    private const int CredentialPersistenceForSession = 1;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int AuthenticateCallback(IntPtr webView, IntPtr request, IntPtr userData);

    private string[] ProxyNetworkSymbols => ApiFlavor == Flavor.Modern
        ? new[]
        {
            "webkit_network_session_new", "webkit_network_session_new_ephemeral",
            "webkit_network_session_set_proxy_settings", "webkit_network_session_allow_tls_certificate_for_host",
            "webkit_network_proxy_settings_new", "webkit_network_proxy_settings_free",
            "webkit_authentication_request_is_for_proxy", "webkit_authentication_request_get_host",
            "webkit_authentication_request_get_port", "webkit_authentication_request_authenticate",
            "webkit_credential_new", "webkit_credential_free",
        }
        : new[]
        {
            "webkit_website_data_manager_new", "webkit_website_data_manager_new_ephemeral",
            "webkit_web_context_new_with_website_data_manager", "webkit_web_context_get_website_data_manager",
            "webkit_website_data_manager_set_network_proxy_settings", "webkit_web_context_allow_tls_certificate_for_host",
            "webkit_network_proxy_settings_new", "webkit_network_proxy_settings_free",
            "webkit_authentication_request_is_for_proxy", "webkit_authentication_request_get_host",
            "webkit_authentication_request_get_port", "webkit_authentication_request_authenticate",
            "webkit_credential_new", "webkit_credential_free",
            "webkit_web_view_new_with_context",
        };

    /// <summary>True when this WebKit can give a view a network of its own behind a proxy.</summary>
    internal bool SupportsProxyNetworks => ProxyNetworkSymbols.All(Has)
        && Opt("g_tls_certificate_new_from_pem", _gio) != IntPtr.Zero
        && Opt("g_signal_handler_disconnect", _gobject) != IntPtr.Zero;

    /// <summary>
    /// A new network: persistent in <paramref name="dataDirectory"/> and
    /// <paramref name="cacheDirectory"/>, or ephemeral when they are null. WPE: a
    /// WebKitNetworkSession (the view's "network-session"); WebKitGTK: a WebKitWebContext with
    /// its own data manager (the view's "web-context"). Owned by the caller (g_object_unref).
    /// </summary>
    internal IntPtr CreateProxyNetwork(string? dataDirectory, string? cacheDirectory)
    {
        bool persistent = dataDirectory != null && cacheDirectory != null;
        using var data = new Utf8(dataDirectory);
        using var cache = new Utf8(cacheDirectory);
        if (ApiFlavor == Flavor.Modern)
        {
            return persistent
                ? ((delegate* unmanaged<IntPtr, IntPtr, IntPtr>)Opt("webkit_network_session_new"))(data, cache)
                : ((delegate* unmanaged<IntPtr>)Opt("webkit_network_session_new_ephemeral"))();
        }

        IntPtr manager;
        if (persistent)
        {
            using var dataName = new Utf8("base-data-directory");
            using var cacheName = new Utf8("base-cache-directory");
            manager = ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr>)Opt("webkit_website_data_manager_new"))(dataName, data, cacheName, cache, IntPtr.Zero);
        }
        else
        {
            manager = ((delegate* unmanaged<IntPtr>)Opt("webkit_website_data_manager_new_ephemeral"))();
        }
        if (manager == IntPtr.Zero)
            return IntPtr.Zero;
        var context = ((delegate* unmanaged<IntPtr, IntPtr>)Opt("webkit_web_context_new_with_website_data_manager"))(manager);
        _objectUnref(manager);
        return context;
    }

    /// <summary>
    /// Routes the network through <paramref name="proxyUri"/> (http and https alike, no
    /// exceptions), or back to the system's proxy settings when it is null.
    /// </summary>
    internal void SetNetworkProxy(IntPtr network, string? proxyUri)
    {
        if (network == IntPtr.Zero)
            return;
        IntPtr settings = IntPtr.Zero;
        if (proxyUri != null)
        {
            using var uri = new Utf8(proxyUri);
            settings = ((delegate* unmanaged<IntPtr, IntPtr, IntPtr>)Opt("webkit_network_proxy_settings_new"))(uri, IntPtr.Zero);
        }
        int mode = proxyUri != null ? ProxyModeCustom : ProxyModeDefault;
        try
        {
            if (ApiFlavor == Flavor.Modern)
            {
                ((delegate* unmanaged<IntPtr, int, IntPtr, void>)Opt("webkit_network_session_set_proxy_settings"))(network, mode, settings);
            }
            else
            {
                var manager = ((delegate* unmanaged<IntPtr, IntPtr>)Opt("webkit_web_context_get_website_data_manager"))(network);
                if (manager != IntPtr.Zero)
                    ((delegate* unmanaged<IntPtr, int, IntPtr, void>)Opt("webkit_website_data_manager_set_network_proxy_settings"))(manager, mode, settings);
            }
        }
        finally
        {
            if (settings != IntPtr.Zero)
                ((delegate* unmanaged<IntPtr, void>)Opt("webkit_network_proxy_settings_free"))(settings);
        }
    }

    /// <summary>A GTlsCertificate (owned by the caller) for a PEM certificate; zero on failure.</summary>
    internal IntPtr CreateTlsCertificate(string pem)
    {
        using var text = new Utf8(pem);
        IntPtr error = IntPtr.Zero;
        var certificate = ((delegate* unmanaged<IntPtr, nint, IntPtr*, IntPtr>)Opt("g_tls_certificate_new_from_pem", _gio))(text, -1, &error);
        if (error != IntPtr.Zero)
        {
            DiagnosticLog.Warn("WebKitContentApi", "g_tls_certificate_new_from_pem failed");
            _errorFree(error);
        }
        return certificate;
    }

    /// <summary>
    /// Has the network accept <paramref name="certificate"/> for <paramref name="host"/> despite
    /// its TLS errors. Only that exact certificate is accepted, and only on this network.
    /// </summary>
    internal void AllowTlsCertificateForHost(IntPtr network, IntPtr certificate, string host)
    {
        if (network == IntPtr.Zero || certificate == IntPtr.Zero)
            return;
        using var h = new Utf8(host);
        var name = ApiFlavor == Flavor.Modern
            ? "webkit_network_session_allow_tls_certificate_for_host"
            : "webkit_web_context_allow_tls_certificate_for_host";
        ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)Opt(name))(network, certificate, h);
    }

    /// <summary>
    /// Answers the view's proxy authentication challenges (the "authenticate" signal with
    /// <c>is_for_proxy</c>) with the credentials <paramref name="credentials"/> gives for the
    /// challenging proxy's host and port; anything else is left to WebKit. Dispose the result
    /// while the view is alive to disconnect; <see cref="ProxyAuthentication.Abandon"/> when the
    /// view is already gone.
    /// </summary>
    internal ProxyAuthentication AnswerProxyAuthentication(IntPtr webView, Func<string, int, (string User, string Password)?> credentials)
    {
        var registration = new ProxyAuthentication(this, webView);
        AuthenticateCallback callback = (view, request, _) =>
        {
            try
            {
                if (registration.Abandoned || CallInt("webkit_authentication_request_is_for_proxy", request) == 0)
                    return 0;
                var host = Utf8String(Call("webkit_authentication_request_get_host", request)) ?? string.Empty;
                var port = (int)((delegate* unmanaged<IntPtr, uint>)Opt("webkit_authentication_request_get_port"))(request);
                if (credentials(host, port) is not { } answer)
                    return 0;
                using var user = new Utf8(answer.User);
                using var password = new Utf8(answer.Password);
                var credential = ((delegate* unmanaged<IntPtr, IntPtr, int, IntPtr>)Opt("webkit_credential_new"))(user, password, CredentialPersistenceForSession);
                ((delegate* unmanaged<IntPtr, IntPtr, void>)Opt("webkit_authentication_request_authenticate"))(request, credential);
                ((delegate* unmanaged<IntPtr, void>)Opt("webkit_credential_free"))(credential);
                return 1;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("WebKitContentApi", "authenticate handler failed", ex);
                return 0;
            }
        };
        registration.Callback = callback;
        lock (_rooted) _rooted.Add(callback);
        using var signal = new Utf8("authenticate");
        registration.HandlerId = _signalConnectData(webView, signal, Marshal.GetFunctionPointerForDelegate(callback), IntPtr.Zero, IntPtr.Zero, 0);
        return registration;
    }

    /// <summary>The "authenticate" handler <see cref="AnswerProxyAuthentication"/> connected.</summary>
    internal sealed class ProxyAuthentication : IDisposable
    {
        private readonly WebKitContentApi _api;
        private readonly IntPtr _webView;

        internal ProxyAuthentication(WebKitContentApi api, IntPtr webView)
        {
            _api = api;
            _webView = webView;
        }

        internal object? Callback { get; set; }
        internal nuint HandlerId { get; set; }
        internal bool Abandoned { get; private set; }

        /// <summary>The view is gone (its handlers went with it): only stop answering.</summary>
        public void Abandon()
        {
            // The callback stays rooted: a destroyed view's handler is already gone, but one
            // whose destruction is still pending could fire once more.
            Abandoned = true;
        }

        /// <summary>Disconnects the handler; the view must still be alive.</summary>
        public void Dispose()
        {
            if (Abandoned)
                return;
            Abandoned = true;
            if (HandlerId != 0 && _webView != IntPtr.Zero)
                ((delegate* unmanaged<IntPtr, nuint, void>)_api.Opt("g_signal_handler_disconnect", _api._gobject))(_webView, HandlerId);
            if (Callback != null)
                lock (_api._rooted) _api._rooted.Remove(Callback);
        }
    }
}
