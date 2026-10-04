// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// <c>HybridWebView.WebResourceRequested</c> on Linux, through MAUI's own code: the handler
/// calls <see cref="IWebRequestInterceptingWebView.WebResourceRequested"/> with a
/// <see cref="WebResourceRequestedEventArgs"/>, MAUI's HybridWebView wraps it in
/// <see cref="PlatformWebViewWebResourceRequestedEventArgs"/> and
/// <see cref="WebViewWebResourceRequestedEventArgs"/> and raises its event, as on Windows and iOS.
/// The platform-neutral build of those args is empty (no URI, method or headers, and
/// <c>SetResponse</c> does nothing), so it is patched here to read the WebKit request and to
/// collect the app's response.
/// </summary>
internal static class HybridWebViewResourceInterception
{
    /// <summary>One intercepted request: what the app reads, and the response it sets.</summary>
    internal sealed class Exchange
    {
        public Exchange(HybridWebRequest request) => Request = request;

        public HybridWebRequest Request { get; }

        /// <summary>The app's response (SetResponse), once it gave one.</summary>
        public TaskCompletionSource<HybridWebResponse> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ResponseSet { get; set; }
    }

    private static readonly ConditionalWeakTable<WebResourceRequestedEventArgs, Exchange> s_byCoreArgs = new();
    private static readonly ConditionalWeakTable<PlatformWebViewWebResourceRequestedEventArgs, Exchange> s_byPlatformArgs = new();

    [ThreadStatic]
    private static Exchange? t_current;

    private static int s_state; // 0 not tried, 1 installed, 2 failed
    private static ConstructorInfo? s_coreArgsCtor;

    internal static bool IsInstalled => Volatile.Read(ref s_state) == 1;

    internal static void Install()
    {
        if (Interlocked.CompareExchange(ref s_state, 2, 0) != 0)
            return;
        try
        {
            const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
            var platform = typeof(PlatformWebViewWebResourceRequestedEventArgs);
            s_coreArgsCtor = typeof(WebResourceRequestedEventArgs).GetConstructor(Instance, Type.EmptyTypes);
            var ctor = platform.GetConstructor(Instance, new[] { typeof(WebResourceRequestedEventArgs) });
            var getUri = platform.GetMethod("GetRequestUri", Instance, Type.EmptyTypes);
            var getMethod = platform.GetMethod("GetRequestMethod", Instance, Type.EmptyTypes);
            var getHeaders = platform.GetMethod("GetRequestHeaders", Instance, Type.EmptyTypes);
            var setResponse = platform.GetMethod("SetResponse", Instance, new[] { typeof(int), typeof(string), typeof(IReadOnlyDictionary<string, string>), typeof(Stream) });
            var setResponseAsync = platform.GetMethod("SetResponseAsync", Instance, new[] { typeof(int), typeof(string), typeof(IReadOnlyDictionary<string, string>), typeof(Task<Stream?>) });
            if (s_coreArgsCtor == null || ctor == null || getUri == null || getMethod == null || getHeaders == null || setResponse == null || setResponseAsync == null)
            {
                DiagnosticLog.Warn("HybridWebView", "MAUI's web resource request arguments changed shape; WebResourceRequested is not raised");
                return;
            }

            HarmonyMethod Prefix(string name) => new(typeof(HybridWebViewResourceInterception).GetMethod(name, Static));
            var harmony = new Harmony("com.openmaui.hybridwebview.webresourcerequested");
            harmony.Patch(ctor, postfix: Prefix(nameof(Ctor_Postfix)));
            harmony.Patch(getUri, Prefix(nameof(GetRequestUri_Prefix)));
            harmony.Patch(getMethod, Prefix(nameof(GetRequestMethod_Prefix)));
            harmony.Patch(getHeaders, Prefix(nameof(GetRequestHeaders_Prefix)));
            harmony.Patch(setResponse, Prefix(nameof(SetResponse_Prefix)));
            harmony.Patch(setResponseAsync, Prefix(nameof(SetResponseAsync_Prefix)));
            Volatile.Write(ref s_state, 1);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("HybridWebView", "Patching MAUI's web resource request arguments failed; WebResourceRequested is not raised", ex);
        }
    }

    /// <summary>
    /// Offers <paramref name="request"/> to the app. True when the app handled it (set
    /// <c>Handled</c>); <paramref name="response"/> then completes with the response it set.
    /// </summary>
    internal static bool TryIntercept(IHybridWebView view, HybridWebRequest request, out Task<HybridWebResponse> response)
    {
        response = Task.FromResult(HybridWebResponse.Status(404, "Not Found"));
        Install();
        if (!IsInstalled || s_coreArgsCtor == null)
            return false;

        var exchange = new Exchange(request);
        var args = (WebResourceRequestedEventArgs)s_coreArgsCtor.Invoke(null);
        s_byCoreArgs.AddOrUpdate(args, exchange);

        bool handled;
        var previous = t_current;
        t_current = exchange;
        try
        {
            handled = view.WebResourceRequested(args);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("HybridWebView", $"WebResourceRequested for {request.Uri} threw", ex);
            return false;
        }
        finally
        {
            t_current = previous;
        }

        if (!handled)
            return false;
        if (!exchange.ResponseSet)
        {
            // Handled without a response: nothing will answer the page (on iOS the request would
            // never complete); answer Not Found rather than leave it waiting.
            DiagnosticLog.Warn("HybridWebView", $"WebResourceRequested handled {request.Uri} without calling SetResponse");
            exchange.Response.TrySetResult(HybridWebResponse.Status(404, "Not Found"));
        }
        response = exchange.Response.Task;
        return true;
    }

    private static Exchange? Find(PlatformWebViewWebResourceRequestedEventArgs instance) =>
        s_byPlatformArgs.TryGetValue(instance, out var exchange) ? exchange : t_current;

    private static void Ctor_Postfix(PlatformWebViewWebResourceRequestedEventArgs __instance, WebResourceRequestedEventArgs args)
    {
        if (args != null && s_byCoreArgs.TryGetValue(args, out var exchange))
            s_byPlatformArgs.AddOrUpdate(__instance, exchange);
    }

    private static bool GetRequestUri_Prefix(PlatformWebViewWebResourceRequestedEventArgs __instance, ref string? __result)
    {
        if (Find(__instance) is not { } exchange)
            return true;
        __result = exchange.Request.Uri.ToString();
        return false;
    }

    private static bool GetRequestMethod_Prefix(PlatformWebViewWebResourceRequestedEventArgs __instance, ref string? __result)
    {
        if (Find(__instance) is not { } exchange)
            return true;
        __result = exchange.Request.Method;
        return false;
    }

    private static bool GetRequestHeaders_Prefix(PlatformWebViewWebResourceRequestedEventArgs __instance, ref IReadOnlyDictionary<string, string>? __result)
    {
        if (Find(__instance) is not { } exchange)
            return true;
        __result = new Dictionary<string, string>(exchange.Request.Headers, StringComparer.OrdinalIgnoreCase);
        return false;
    }

    private static bool SetResponse_Prefix(PlatformWebViewWebResourceRequestedEventArgs __instance, int code, string reason, IReadOnlyDictionary<string, string>? headers, Stream? content)
    {
        if (Find(__instance) is not { } exchange)
            return true;
        exchange.ResponseSet = true;
        try
        {
            exchange.Response.TrySetResult(CreateResponse(code, reason, headers, ReadAll(content)));
        }
        catch (Exception ex)
        {
            exchange.Response.TrySetResult(Failed(exchange, ex));
        }
        return false;
    }

    private static bool SetResponseAsync_Prefix(PlatformWebViewWebResourceRequestedEventArgs __instance, int code, string reason, IReadOnlyDictionary<string, string>? headers, Task<Stream?> contentTask, ref Task __result)
    {
        if (Find(__instance) is not { } exchange)
            return true;
        exchange.ResponseSet = true;
        __result = CompleteAsync(exchange, code, reason, headers, contentTask);
        return false;
    }

    private static async Task CompleteAsync(Exchange exchange, int code, string reason, IReadOnlyDictionary<string, string>? headers, Task<Stream?> contentTask)
    {
        try
        {
            var content = await contentTask.ConfigureAwait(false);
            exchange.Response.TrySetResult(CreateResponse(code, reason, headers, ReadAll(content)));
        }
        catch (Exception ex)
        {
            exchange.Response.TrySetResult(Failed(exchange, ex));
        }
    }

    private static HybridWebResponse Failed(Exchange exchange, Exception ex)
    {
        DiagnosticLog.Error("HybridWebView", $"The response the app set for {exchange.Request.Uri} could not be read", ex);
        return HybridWebResponse.Status(500, "Internal Server Error");
    }

    private static HybridWebResponse CreateResponse(int code, string reason, IReadOnlyDictionary<string, string>? headers, byte[] body) =>
        new(code, reason ?? string.Empty, headers?.ToList() ?? new List<KeyValuePair<string, string>>(), body);

    private static byte[] ReadAll(Stream? content)
    {
        // Read from where the stream is, and left open: the app owns it (as on iOS and Windows).
        if (content == null)
            return Array.Empty<byte>();
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        return buffer.ToArray();
    }
}
