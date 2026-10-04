// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Native;

/// <summary>
/// The browser part of the WebKit GLib API that both ports share: navigation state, policy
/// decisions (navigation, new window, response), the main resource's HTTP status, zoom, user
/// agent and downloads. Resolved lazily by name: a symbol an older WebKit lacks only disables
/// what uses it (its call returns a default) instead of failing the view.
/// </summary>
public sealed unsafe partial class WebKitContentApi
{
    // WebKitLoadEvent
    public const int LoadStarted = 0;
    public const int LoadRedirected = 1;
    public const int LoadCommitted = 2;
    public const int LoadFinished = 3;

    // WebKitPolicyDecisionType
    public const int PolicyNavigationAction = 0;
    public const int PolicyNewWindowAction = 1;
    public const int PolicyResponse = 2;

    // WebKitNavigationType
    public const int NavigationLinkClicked = 0;
    public const int NavigationFormSubmitted = 1;
    public const int NavigationBackForward = 2;
    public const int NavigationReload = 3;
    public const int NavigationFormResubmitted = 4;
    public const int NavigationOther = 5;

    private readonly ConcurrentDictionary<string, IntPtr> _optional = new();

    private IntPtr Opt(string name, IntPtr? library = null) =>
        _optional.GetOrAdd(name, n => NativeLibrary.TryGetExport(library ?? _lib, n, out var p) ? p : IntPtr.Zero);

    /// <summary>True when this WebKit exports <paramref name="symbol"/>.</summary>
    public bool Has(string symbol) => Opt(symbol) != IntPtr.Zero;

    private static string? Utf8String(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);

    private IntPtr Call(string name, IntPtr a)
    {
        var f = Opt(name);
        return f == IntPtr.Zero || a == IntPtr.Zero ? IntPtr.Zero : ((delegate* unmanaged<IntPtr, IntPtr>)f)(a);
    }

    private int CallInt(string name, IntPtr a, int fallback = 0)
    {
        var f = Opt(name);
        return f == IntPtr.Zero || a == IntPtr.Zero ? fallback : ((delegate* unmanaged<IntPtr, int>)f)(a);
    }

    private void CallVoid(string name, IntPtr a)
    {
        var f = Opt(name);
        if (f != IntPtr.Zero && a != IntPtr.Zero)
            ((delegate* unmanaged<IntPtr, void>)f)(a);
    }

    #region Navigation state

    public string? GetUri(IntPtr webView) => Utf8String(Call("webkit_web_view_get_uri", webView));
    public string? GetTitle(IntPtr webView) => Utf8String(Call("webkit_web_view_get_title", webView));
    public bool CanGoBack(IntPtr webView) => CallInt("webkit_web_view_can_go_back", webView) != 0;
    public bool CanGoForward(IntPtr webView) => CallInt("webkit_web_view_can_go_forward", webView) != 0;
    public void GoBack(IntPtr webView) => CallVoid("webkit_web_view_go_back", webView);
    public void GoForward(IntPtr webView) => CallVoid("webkit_web_view_go_forward", webView);
    public void Reload(IntPtr webView) => CallVoid("webkit_web_view_reload", webView);
    public void StopLoading(IntPtr webView) => CallVoid("webkit_web_view_stop_loading", webView);
    public bool IsLoading(IntPtr webView) => CallInt("webkit_web_view_is_loading", webView) != 0;

    public void LoadHtml(IntPtr webView, string html, string? baseUri)
    {
        var f = Opt("webkit_web_view_load_html");
        if (f == IntPtr.Zero || webView == IntPtr.Zero) return;
        using var h = new Utf8(html);
        using var b = new Utf8(baseUri);
        ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)f)(webView, h, b);
    }

    /// <summary>The main resource's HTTP status (0 before a response, or for non-HTTP loads).</summary>
    public int GetMainResourceStatus(IntPtr webView)
    {
        var resource = Call("webkit_web_view_get_main_resource", webView);
        var response = Call("webkit_web_resource_get_response", resource);
        return GetResponseStatus(response);
    }

    #endregion

    #region Settings and zoom

    public IntPtr GetSettings(IntPtr webView) => Call("webkit_web_view_get_settings", webView);

    public string? GetUserAgent(IntPtr webView) => Utf8String(Call("webkit_settings_get_user_agent", GetSettings(webView)));

    /// <summary>Sets the user agent; null or empty restores WebKit's own.</summary>
    public void SetUserAgent(IntPtr webView, string? userAgent)
    {
        var settings = GetSettings(webView);
        var f = Opt("webkit_settings_set_user_agent");
        if (f == IntPtr.Zero || settings == IntPtr.Zero) return;
        using var ua = new Utf8(string.IsNullOrEmpty(userAgent) ? null : userAgent);
        ((delegate* unmanaged<IntPtr, IntPtr, void>)f)(settings, ua);
    }

    /// <summary>Turns WebKit's Web Inspector (developer extras) on or off for the view.</summary>
    public void SetDeveloperExtrasEnabled(IntPtr webView, bool enabled)
    {
        var settings = GetSettings(webView);
        var f = Opt("webkit_settings_set_enable_developer_extras");
        if (f != IntPtr.Zero && settings != IntPtr.Zero)
            ((delegate* unmanaged<IntPtr, int, void>)f)(settings, enabled ? 1 : 0);
    }

    public double GetZoomLevel(IntPtr webView)
    {
        var f = Opt("webkit_web_view_get_zoom_level");
        return f == IntPtr.Zero || webView == IntPtr.Zero ? 1.0 : ((delegate* unmanaged<IntPtr, double>)f)(webView);
    }

    public void SetZoomLevel(IntPtr webView, double zoom)
    {
        var f = Opt("webkit_web_view_set_zoom_level");
        if (f != IntPtr.Zero && webView != IntPtr.Zero)
            ((delegate* unmanaged<IntPtr, double, void>)f)(webView, zoom);
    }

    #endregion

    #region Policy decisions

    /// <summary>The URI a navigation or new-window decision is about.</summary>
    public string? GetNavigationDecisionUri(IntPtr decision)
    {
        var action = Call("webkit_navigation_policy_decision_get_navigation_action", decision);
        var request = Call("webkit_navigation_action_get_request", action);
        return Utf8String(Call("webkit_uri_request_get_uri", request));
    }

    /// <summary>What started a navigation (a <c>Navigation*</c> constant).</summary>
    public int GetNavigationDecisionType(IntPtr decision)
    {
        var action = Call("webkit_navigation_policy_decision_get_navigation_action", decision);
        return CallInt("webkit_navigation_action_get_navigation_type", action, NavigationOther);
    }

    /// <summary>The navigation action of a WebView "create" signal: the URI the new window would load.</summary>
    public string? GetNavigationActionUri(IntPtr action)
    {
        var request = Call("webkit_navigation_action_get_request", action);
        return Utf8String(Call("webkit_uri_request_get_uri", request));
    }

    public IntPtr GetResponse(IntPtr responseDecision) => Call("webkit_response_policy_decision_get_response", responseDecision);
    public string? GetResponseUri(IntPtr response) => Utf8String(Call("webkit_uri_response_get_uri", response));
    public string? GetResponseMimeType(IntPtr response) => Utf8String(Call("webkit_uri_response_get_mime_type", response));
    public int GetResponseStatus(IntPtr response) => CallInt("webkit_uri_response_get_status_code", response);
    public string? GetResponseSuggestedFilename(IntPtr response) => Utf8String(Call("webkit_uri_response_get_suggested_filename", response));

    /// <summary>True when WebKit can show the response's MIME type itself.</summary>
    public bool IsResponseMimeTypeSupported(IntPtr responseDecision) =>
        CallInt("webkit_response_policy_decision_is_mime_type_supported", responseDecision, 1) != 0;

    /// <summary>True when the response is the main frame's document (not a subresource or iframe).</summary>
    public bool IsMainFrameResponse(IntPtr responseDecision) =>
        !Has("webkit_response_policy_decision_is_main_frame_main_resource")
        || CallInt("webkit_response_policy_decision_is_main_frame_main_resource", responseDecision, 1) != 0;

    public void PolicyUse(IntPtr decision) => CallVoid("webkit_policy_decision_use", decision);
    public void PolicyIgnore(IntPtr decision) => CallVoid("webkit_policy_decision_ignore", decision);
    public void PolicyDownload(IntPtr decision) => CallVoid("webkit_policy_decision_download", decision);

    #endregion

    #region Downloads

    /// <summary>The object that announces downloads: the network session (newer API) or the web context (4.1).</summary>
    public IntPtr GetDownloadSource(IntPtr webView)
    {
        var session = Call("webkit_web_view_get_network_session", webView);
        return session != IntPtr.Zero ? session : _webViewGetContext(webView);
    }

    public string? GetDownloadUri(IntPtr download)
    {
        var request = Call("webkit_download_get_request", download);
        return Utf8String(Call("webkit_uri_request_get_uri", request));
    }

    public IntPtr GetDownloadWebView(IntPtr download) => Call("webkit_download_get_web_view", download);

    public void CancelDownload(IntPtr download) => CallVoid("webkit_download_cancel", download);

    /// <summary>Sets where a download is written: a path in the newer API, a file URI in 4.1.</summary>
    public void SetDownloadDestination(IntPtr download, string path)
    {
        var f = Opt("webkit_download_set_destination");
        if (f == IntPtr.Zero || download == IntPtr.Zero) return;
        var destination = ApiFlavor == Flavor.WebKitGtk41 ? new Uri(path).AbsoluteUri : path;
        using var d = new Utf8(destination);
        ((delegate* unmanaged<IntPtr, IntPtr, void>)f)(download, d);
    }

    public string? GetDownloadDestination(IntPtr download)
    {
        var value = Utf8String(Call("webkit_download_get_destination", download));
        if (value != null && value.StartsWith("file://", StringComparison.Ordinal) && Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return uri.LocalPath;
        return value;
    }

    #endregion

    #region Signals

    /// <summary>
    /// Connects <paramref name="handler"/> (a delegate with the signal's C signature) to
    /// <paramref name="signal"/> on <paramref name="instance"/>. With <paramref name="keepAlive"/>
    /// the delegate is kept for the process's lifetime; otherwise the caller keeps it alive until
    /// it disconnects (a per-view handler does, so a closed view's delegates can be collected).
    /// </summary>
    public ulong Connect(IntPtr instance, string signal, Delegate handler, bool keepAlive = true)
    {
        if (instance == IntPtr.Zero) return 0;
        if (keepAlive)
            lock (_rooted) _rooted.Add(handler);
        using var s = new Utf8(signal);
        return _signalConnectData(instance, s, Marshal.GetFunctionPointerForDelegate(handler), IntPtr.Zero, IntPtr.Zero, 0);
    }

    /// <summary>Disconnects a handler <see cref="Connect"/> returned.</summary>
    public void Disconnect(IntPtr instance, ulong handlerId)
    {
        if (instance == IntPtr.Zero || handlerId == 0) return;
        var f = Opt("g_signal_handler_disconnect", _gobject);
        if (f != IntPtr.Zero)
            ((delegate* unmanaged<IntPtr, nuint, void>)f)(instance, (nuint)handlerId);
    }

    /// <summary>The message of a GError (null for none).</summary>
    public static string? GErrorMessage(IntPtr error) =>
        error == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(error, 8));

    #endregion
}
