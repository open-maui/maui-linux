// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.FileProviders;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Views;

namespace Microsoft.Maui.Platform.Linux.Blazor.Handlers;

/// <summary>
/// Blazor's <see cref="WebViewManager"/> over a <see cref="WpeWebView"/>. Serves
/// the app through the <c>app://localhost/</c> custom scheme (host page, static
/// assets, <c>_framework/blazor.webview.js</c>), bridges JavaScript messages
/// through a WebKit script-message handler, and pushes .NET messages with
/// <c>evaluate_javascript</c>. Uses only the WebKit content API shared by the
/// WPE and WebKitGTK ports.
/// </summary>
internal sealed class LinuxWebViewManager : WebViewManager
{
    public const string AppScheme = "app";

    // WebKit rejects "0.0.0.0" as a restricted host for custom schemes ("Not
    // allowed to use restricted network port"); localhost is what WebKit-based
    // MAUI platforms use. Only relative URLs are visible to app code.
    public const string AppHostAddress = "localhost";
    public static readonly Uri AppOriginUri = new($"{AppScheme}://{AppHostAddress}/");

    private const string MessageHandlerName = "webwindowinterop";

    // Custom schemes are per WebKit context (shared by every view), so requests
    // are routed to the manager that owns the originating view.
    private static readonly ConcurrentDictionary<IntPtr, LinuxWebViewManager> s_managersByView = new();

    private const string InitScript = """
        window.__receiveMessageCallbacks = [];
        window.__dispatchMessageCallback = function (message) {
            window.__receiveMessageCallbacks.forEach(function (callback) { callback(message); });
        };
        window.external = {
            sendMessage: function (message) {
                window.webkit.messageHandlers.webwindowinterop.postMessage(message);
            },
            receiveMessage: function (callback) {
                window.__receiveMessageCallbacks.push(callback);
            }
        };
        // MAUI host pages load blazor.webview.js with autostart="false" so the
        // platform starts Blazor once the message bridge exists (that is us).
        document.addEventListener('DOMContentLoaded', function () {
            if (window.Blazor && !window.__openmauiBlazorStarted) {
                window.__openmauiBlazorStarted = true;
                window.Blazor.start();
            }
        });
        """;

    private readonly WpeWebView _view;
    private readonly IntPtr _nativeView;
    private readonly WebKitContentApi _api;

    public LinuxWebViewManager(
        WpeWebView view,
        IServiceProvider services,
        AspNetCore.Components.Dispatcher dispatcher,
        IFileProvider fileProvider,
        JSComponentConfigurationStore jsComponents,
        string hostPageRelativePath)
        : base(services, dispatcher, AppOriginUri, fileProvider, jsComponents, hostPageRelativePath)
    {
        _view = view;
        _nativeView = view.NativeWebView;
        _api = view.Content;

        s_managersByView[_nativeView] = this;
        _api.RegisterUriScheme(_nativeView, AppScheme, HandleSchemeRequest);
        _api.AddUserScript(_nativeView, InitScript);
        _api.RegisterScriptMessageHandler(_nativeView, MessageHandlerName, message =>
        {
            DiagnosticLog.Debug("LinuxWebViewManager", $"JS->NET {(message.Length > 80 ? message[..80] + "..." : message)}");
            MessageReceived(AppOriginUri, message);
        });
    }

    private static WebKitContentApi.SchemeResponse HandleSchemeRequest(IntPtr webView, string uri)
    {
        if (!s_managersByView.TryGetValue(webView, out var manager))
            return new WebKitContentApi.SchemeResponse(404, "Not Found", Array.Empty<byte>(), "text/plain");
        return manager.Serve(uri);
    }

    private const string FrameworkScriptPath = "/_framework/blazor.webview.js";
    private static readonly Lazy<byte[]?> s_frameworkScript = new(() =>
    {
        using var stream = typeof(LinuxWebViewManager).Assembly.GetManifestResourceStream("blazor.webview.js");
        if (stream == null) return null;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    });

    private WebKitContentApi.SchemeResponse Serve(string uri)
    {
        // The Blazor bootstrap script is a static web asset on other platforms;
        // here it is embedded from the exact package version we run against.
        Uri.TryCreate(uri, UriKind.Absolute, out var requested);
        if (requested != null
            && string.Equals(requested.AbsolutePath, FrameworkScriptPath, StringComparison.OrdinalIgnoreCase)
            && s_frameworkScript.Value is { } script)
        {
            DiagnosticLog.Debug("LinuxWebViewManager", $"200 {uri} (embedded)");
            return new WebKitContentApi.SchemeResponse(200, "OK", script, "application/javascript");
        }

        // Navigations (no file extension) fall back to the host page so deep
        // links into the Blazor router work; asset requests must resolve exactly.
        bool allowFallbackOnHostPage = requested == null || !Path.HasExtension(requested.AbsolutePath);

        bool isModulesManifest = requested != null
            && string.Equals(requested.AbsolutePath, "/_framework/blazor.modules.json", StringComparison.OrdinalIgnoreCase);

        // TryGetResponseContent returns true with a 404 status for unknown files.
        if (TryGetResponseContent(uri, allowFallbackOnHostPage, out int statusCode, out string statusMessage, out var content, out var headers)
            && !(isModulesManifest && statusCode != 200))
        {
            DiagnosticLog.Debug("LinuxWebViewManager", $"{statusCode} {uri}");
            using (content)
            {
                using var buffer = new MemoryStream();
                content.CopyTo(buffer);
                var contentType = headers.TryGetValue("Content-Type", out var ct) ? ct : "application/octet-stream";
                return new WebKitContentApi.SchemeResponse(statusCode, statusMessage, buffer.ToArray(), contentType);
            }
        }
        content?.Dispose();

        // JS initializers manifest: generated by the static web asset pipeline
        // on other platforms; an app without one gets an empty list.
        if (isModulesManifest)
        {
            DiagnosticLog.Debug("LinuxWebViewManager", $"200 {uri} (empty manifest)");
            return new WebKitContentApi.SchemeResponse(200, "OK", "[]"u8.ToArray(), "application/json");
        }

        DiagnosticLog.Debug("LinuxWebViewManager", $"404 for {uri}");
        return new WebKitContentApi.SchemeResponse(404, "Not Found", Array.Empty<byte>(), "text/plain");
    }

    protected override void NavigateCore(Uri absoluteUri)
    {
        _view.Navigate(absoluteUri.ToString());
    }

    protected override void SendMessage(string message)
    {
        // JSON-encode to a JavaScript string literal (quotes, newlines, unicode).
        DiagnosticLog.Debug("LinuxWebViewManager", $"NET->JS {(message.Length > 80 ? message[..80] + "..." : message)}");
        var literal = JsonSerializer.Serialize(message);
        _api.RunJavaScript(_nativeView, $"window.__dispatchMessageCallback({literal});");
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        s_managersByView.TryRemove(_nativeView, out _);
        await base.DisposeAsyncCore();
    }
}
