// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>A request the hybrid page made to its app origin.</summary>
internal sealed record HybridWebRequest(Uri Uri, string Method, IReadOnlyDictionary<string, string> Headers, byte[]? Body);

/// <summary>The answer to a <see cref="HybridWebRequest"/>.</summary>
internal sealed record HybridWebResponse(int StatusCode, string ReasonPhrase, IReadOnlyList<KeyValuePair<string, string>> Headers, byte[] Body)
{
    public string? ContentType => Headers.FirstOrDefault(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value;

    public static HybridWebResponse Status(int code, string reason) => new(code, reason, Array.Empty<KeyValuePair<string, string>>(), Array.Empty<byte>());
}

/// <summary>What the bridge needs from the browser: running script, and the page it shows.</summary>
internal interface IHybridWebViewScriptHost
{
    /// <summary>Runs a script; completes with its result when it is a string, else null.</summary>
    Task<string?> EvaluateJavaScriptAsync(string script);

    /// <summary>The URL of the page the view shows (the main frame).</summary>
    string? CurrentUrl { get; }
}

/// <summary>
/// MAUI's HybridWebView protocol, the way MAUI's own handler speaks it on iOS and Mac Catalyst
/// (also WebKit): the page is served from <c>app://0.0.0.1/</c> out of the app's
/// <see cref="IHybridWebView.HybridRoot"/> assets; <c>_framework/hybridwebview.js</c> is MAUI's
/// own script (embedded in Microsoft.Maui); JavaScript posts <c>type|payload</c> messages to the
/// <c>webwindowinterop</c> script-message handler; .NET answers through
/// <c>window.external.receiveMessage</c>; <c>InvokeDotNet</c> is a POST to
/// <c>__hwvInvokeDotNet</c> answered with MAUI's JSON result. Every request is first offered to
/// the app (<c>HybridWebView.WebResourceRequested</c>). No native code here, so it is unit-tested
/// without a WebKit host.
/// </summary>
internal sealed class HybridWebViewBridge
{
    // Same origin as MAUI on iOS/Mac Catalyst: "https" cannot be served by a WebKit scheme handler.
    public const string AppHostScheme = "app";
    public const string AppHostAddress = "0.0.0.1";
    public const string AppOrigin = "app://0.0.0.1/";
    public static readonly Uri AppOriginUri = new(AppOrigin);

    public const string ScriptMessageHandlerName = "webwindowinterop";

    internal const string InvokeDotNetPath = "__hwvInvokeDotNet";
    internal const string HybridWebViewDotJsPath = "_framework/hybridwebview.js";
    internal const string InvokeDotNetTokenHeaderName = "X-Maui-Invoke-Token";
    internal const string InvokeDotNetTokenHeaderValue = "HybridWebView";

    private const string InvokeJavaScriptThrowsExceptionsSwitch = "HybridWebView.InvokeJavaScriptThrowsExceptions";
    private const string Tag = "HybridWebView";

    private readonly IHybridWebView _view;
    private readonly IHybridWebViewScriptHost _host;
    private readonly Func<string, Task<Stream?>> _openAsset;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string?>> _tasks = new();
    private int _lastTaskId;

    /// <param name="view">The HybridWebView the page belongs to.</param>
    /// <param name="host">Runs script in the page.</param>
    /// <param name="openAsset">Opens an app package file (default: <see cref="FileSystem.OpenAppPackageFileAsync"/>); null when it does not exist.</param>
    public HybridWebViewBridge(IHybridWebView view, IHybridWebViewScriptHost host, Func<string, Task<Stream?>>? openAsset = null)
    {
        _view = view;
        _host = host;
        _openAsset = openAsset ?? OpenAppPackageFileAsync;
    }

    private static bool IsInvokeJavaScriptThrowsExceptionsEnabled =>
        !AppContext.TryGetSwitch(InvokeJavaScriptThrowsExceptionsSwitch, out var enabled) || enabled;

    #region Requests

    /// <summary>
    /// Answers a request to the app scheme: the app's <c>WebResourceRequested</c> first, then
    /// MAUI's script, the .NET invoker, and the HybridRoot assets (the default file for the root).
    /// </summary>
    public async Task<HybridWebResponse> HandleRequestAsync(HybridWebRequest request)
    {
        // 1. The app may answer (or replace) any request.
        if (HybridWebViewResourceInterception.TryIntercept(_view, request, out var intercepted))
        {
            DiagnosticLog.Debug(Tag, $"Request for {request.Uri} was handled by the app");
            return await intercepted.ConfigureAwait(false);
        }

        // 2. The app origin is served from the app package.
        if (AppOriginUri.IsBaseOf(request.Uri))
            return await ServeAppRequestAsync(request).ConfigureAwait(false);

        // 3. Another host on the app scheme: nothing serves it (WebKit cannot send it to the network).
        DiagnosticLog.Debug(Tag, $"Request for {request.Uri} was not handled");
        return HybridWebResponse.Status(404, "Not Found");
    }

    private async Task<HybridWebResponse> ServeAppRequestAsync(HybridWebRequest request)
    {
        var relativePath = ResolveRelativePath(request.Uri);
        if (relativePath == null)
        {
            DiagnosticLog.Debug(Tag, $"Request for {request.Uri} resolved to an invalid path");
            return HybridWebResponse.Status(404, "Not Found");
        }

        // 1.a. MAUI's own bridge script.
        if (relativePath == HybridWebViewDotJsPath && HybridScript is { } script)
            return Ok(script, "application/javascript");

        // 1.b. JavaScript calling .NET.
        if (relativePath == InvokeDotNetPath)
        {
            if (!HasExpectedHeaders(request.Headers))
            {
                DiagnosticLog.Error(Tag, "InvokeDotNet endpoint missing or invalid request header");
                return HybridWebResponse.Status(400, "Bad Request");
            }
            if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                DiagnosticLog.Error(Tag, $"InvokeDotNet endpoint only accepts POST requests. Received: {request.Method}");
                return HybridWebResponse.Status(405, "Method Not Allowed");
            }
            if (request.Body is not { Length: > 0 } body)
            {
                DiagnosticLog.Error(Tag, "InvokeDotNet request body is empty");
                return HybridWebResponse.Status(400, "Bad Request");
            }
            var result = await InvokeDotNetAsync(body).ConfigureAwait(false);
            return Ok(result, "application/json");
        }

        // 2. Static content from HybridRoot.
        string contentType;
        if (string.IsNullOrEmpty(relativePath))
        {
            relativePath = _view.DefaultFile;
            contentType = "text/html";
        }
        else if (!TryGetContentType(relativePath, out contentType))
        {
            contentType = "text/plain";
            DiagnosticLog.Warn(Tag, $"Could not determine content type for '{relativePath}'");
        }

        var assetPath = CombineAssetPath(_view.HybridRoot, relativePath);
        if (assetPath != null)
        {
            using var stream = await _openAsset(assetPath).ConfigureAwait(false);
            if (stream != null)
            {
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer).ConfigureAwait(false);
                return Ok(buffer.ToArray(), contentType);
            }
        }

        DiagnosticLog.Debug(Tag, $"Request for {request.Uri} could not be fulfilled");
        return HybridWebResponse.Status(404, "Not Found");
    }

    private static HybridWebResponse Ok(byte[] body, string contentType) => new(200, "OK", new[]
    {
        new KeyValuePair<string, string>("Content-Type", contentType),
        // As MAUI does on iOS: a cached script would not run again.
        new KeyValuePair<string, string>("Cache-Control", "no-cache, max-age=0, must-revalidate, no-store"),
        new KeyValuePair<string, string>("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture)),
    }, body);

    /// <summary>The path below the app origin, without query or fragment; null when it would leave the root.</summary>
    internal static string? ResolveRelativePath(Uri requestUri)
    {
        var url = requestUri.GetLeftPart(UriPartial.Path);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !AppOriginUri.IsBaseOf(uri))
            return null;
        var relativePath = Uri.UnescapeDataString(AppOriginUri.MakeRelativeUri(uri).ToString());
        return IsValidRelativePath(relativePath) ? relativePath : null;
    }

    private static bool IsValidRelativePath(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return true;
        if (Path.IsPathRooted(relativePath))
            return false;
        foreach (var segment in relativePath.Split('\\', '/'))
        {
            if (segment == "..")
                return false;
        }
        return true;
    }

    /// <summary>HybridRoot plus the relative path, as MAUI's FileSystemUtils.Combine (null when it escapes the root).</summary>
    internal static string? CombineAssetPath(string? root, string? relativePath)
    {
        if (relativePath == null || !IsValidRelativePath(relativePath))
            return null;
        return Path.Combine(root ?? string.Empty, relativePath).Replace('\\', '/');
    }

    internal static bool HasExpectedHeaders(IReadOnlyDictionary<string, string> headers)
    {
        var expectedOrigin = AppOrigin.TrimEnd('/');
        bool token = false, origin = false, referer = false;
        foreach (var (name, value) in headers)
        {
            var urlValue = value?.TrimEnd('/');
            token |= string.Equals(name, InvokeDotNetTokenHeaderName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(value, InvokeDotNetTokenHeaderValue, StringComparison.OrdinalIgnoreCase);
            origin |= string.Equals(name, "Origin", StringComparison.OrdinalIgnoreCase)
                && string.Equals(urlValue, expectedOrigin, StringComparison.OrdinalIgnoreCase);
            referer |= string.Equals(name, "Referer", StringComparison.OrdinalIgnoreCase)
                && string.Equals(urlValue, expectedOrigin, StringComparison.OrdinalIgnoreCase);
        }
        return token && (origin || referer);
    }

    private static async Task<Stream?> OpenAppPackageFileAsync(string path)
    {
        try
        {
            if (!await FileSystem.AppPackageFileExistsAsync(path).ConfigureAwait(false))
                return null;
            return await FileSystem.OpenAppPackageFileAsync(path).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug(Tag, $"App package file '{path}' could not be opened: {ex.Message}");
            return null;
        }
    }

    private static readonly Lazy<byte[]?> s_hybridScript = new(() =>
    {
        // MAUI's own script, from the Microsoft.Maui the app runs against.
        using var stream = typeof(HybridWebViewHandler).Assembly.GetManifestResourceStream(HybridWebViewDotJsPath);
        if (stream == null)
        {
            DiagnosticLog.Error(Tag, $"Microsoft.Maui has no '{HybridWebViewDotJsPath}' resource");
            return null;
        }
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    });

    internal static byte[]? HybridScript => s_hybridScript.Value;

    // MAUI's content-type table (ASP.NET's FileExtensionContentTypeProvider), so a file gets the
    // same type as on Windows; OpenMaui's MIME table when MAUI's is not reachable.
    private static readonly Lazy<Func<string, string?>?> s_mauiContentType = new(() =>
    {
        try
        {
            var provider = typeof(HybridWebViewHandler).GetField("ContentTypeProvider", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            var tryGet = provider?.GetType().GetMethod("TryGetContentType", new[] { typeof(string), typeof(string).MakeByRefType() });
            if (provider == null || tryGet == null)
                return null;
            return path =>
            {
                var args = new object?[] { path, null };
                return tryGet.Invoke(provider, args) is true ? args[1] as string : null;
            };
        }
        catch
        {
            return null;
        }
    });

    internal static bool TryGetContentType(string path, out string contentType)
    {
        var type = s_mauiContentType.Value?.Invoke(path);
        if (type == null)
        {
            var extension = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(extension))
                type = MimeTypes.FromExtension(extension);
            if (type == "application/octet-stream")
                type = null;
        }
        contentType = type ?? string.Empty;
        return type != null;
    }

    #endregion

    #region JavaScript to .NET

    /// <summary>
    /// A <c>type|payload</c> message from the page (MAUI's script posts them): a raw message, or
    /// the result of an InvokeJavaScriptAsync call. Ignored when the page is not the app's.
    /// </summary>
    public void OnScriptMessage(string message)
    {
        if (!Uri.TryCreate(_host.CurrentUrl, UriKind.Absolute, out var source) || !AppOriginUri.IsBaseOf(source))
        {
            DiagnosticLog.Debug(Tag, "Ignoring web message from an unrecognized source.");
            return;
        }
        MessageReceived(message);
    }

    /// <summary>MAUI's HybridWebViewHandler.MessageReceived.</summary>
    internal void MessageReceived(string rawMessage)
    {
        if (string.IsNullOrEmpty(rawMessage))
            throw new ArgumentException("The raw message cannot be null or empty.", nameof(rawMessage));
        var indexOfPipe = rawMessage.IndexOf('|', StringComparison.Ordinal);
        if (indexOfPipe == -1)
            throw new ArgumentException("The raw message must contain a pipe character ('|').", nameof(rawMessage));

        var messageType = rawMessage.Substring(0, indexOfPipe);
        var messageContent = rawMessage.Substring(indexOfPipe + 1);

        switch (messageType)
        {
            case "__InvokeJavaScriptFailed":
            case "__InvokeJavaScriptCompleted":
                {
                    var indexOfPipeInContent = messageContent.IndexOf('|', StringComparison.Ordinal);
                    if (indexOfPipeInContent == -1)
                        throw new ArgumentException($"The '{messageType}' message content must contain a pipe character ('|').", nameof(rawMessage));
                    var taskId = messageContent.Substring(0, indexOfPipeInContent);
                    var result = messageContent.Substring(indexOfPipeInContent + 1);

                    if (messageType == "__InvokeJavaScriptFailed")
                    {
                        if (IsInvokeJavaScriptThrowsExceptionsEnabled)
                            SetTaskFailed(taskId, CreateInvokeJavaScriptException(result));
                    }
                    else
                    {
                        SetTaskCompleted(taskId, result);
                    }
                }
                break;
            case "__RawMessage":
                // The script URL-encodes the payload (HybridWebView.ts sendRawMessage).
                _view.RawMessageReceived(Uri.UnescapeDataString(messageContent));
                break;
            default:
                throw new ArgumentException($"The message type '{messageType}' is not recognized.", nameof(rawMessage));
        }
    }

    private sealed class JSInvokeError
    {
        public string? Name { get; set; }
        public string? Message { get; set; }
        public string? StackTrace { get; set; }
    }

    private static Exception CreateInvokeJavaScriptException(string result)
    {
        if (string.IsNullOrWhiteSpace(result))
            return NewInvokeJavaScriptException(Array.Empty<object?>());
        var jsError = JsonSerializer.Deserialize<JSInvokeError>(result);
        var jsException = NewInvokeJavaScriptException(new object?[] { jsError?.Message, jsError?.Name, jsError?.StackTrace });
        return NewInvokeJavaScriptException(new object?[] { $"InvokeJavaScript threw an exception: {jsException.Message}", jsException });
    }

    // MAUI's own (internal) HybridWebViewInvokeJavaScriptException, so the app sees the same type
    // as on the other platforms.
    private static readonly Type? s_invokeJavaScriptExceptionType =
        typeof(HybridWebViewHandler).Assembly.GetType("Microsoft.Maui.Handlers.HybridWebViewInvokeJavaScriptException");

    private static Exception NewInvokeJavaScriptException(object?[] args)
    {
        if (s_invokeJavaScriptExceptionType != null)
        {
            try
            {
                var types = args.Length switch
                {
                    0 => Type.EmptyTypes,
                    2 => new[] { typeof(string), typeof(Exception) },
                    _ => new[] { typeof(string), typeof(string), typeof(string) },
                };
                var ctor = s_invokeJavaScriptExceptionType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, types);
                if (ctor?.Invoke(args) is Exception created)
                    return created;
            }
            catch
            {
            }
        }
        return args.Length switch
        {
            0 => new InvalidOperationException("InvokeJavaScript failed."),
            2 => new InvalidOperationException(args[0] as string, args[1] as Exception),
            _ => new InvalidOperationException(args[0] as string),
        };
    }

    private sealed class JSInvokeMethodData
    {
        public string? MethodName { get; set; }
        public string[]? ParamValues { get; set; }
    }

    private sealed class DotNetInvokeResult
    {
        public object? Result { get; set; }
        public bool IsJson { get; set; }
        public bool IsError { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorType { get; set; }
        public string? ErrorStackTrace { get; set; }
    }

    /// <summary>
    /// MAUI's HybridWebViewHandler.InvokeDotNetAsync: calls the named public method of the
    /// InvokeJavaScriptTarget with the JSON parameters, and answers MAUI's JSON result (an error
    /// result when it throws).
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "HybridWebView needs dynamic JSON, as on every platform (MauiHybridWebViewSupported).")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "HybridWebView needs dynamic JSON, as on every platform (MauiHybridWebViewSupported).")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "InvokeJavaScriptType is annotated with DynamicallyAccessedMembers.All by SetInvokeJavaScriptTarget.")]
    internal async Task<byte[]> InvokeDotNetAsync(byte[] body)
    {
        try
        {
            var invokeTarget = _view.InvokeJavaScriptTarget ?? throw new InvalidOperationException($"The {nameof(IHybridWebView)}.{nameof(IHybridWebView.InvokeJavaScriptTarget)} property must have a value in order to invoke a .NET method from JavaScript.");
            var invokeTargetType = _view.InvokeJavaScriptType ?? throw new InvalidOperationException($"The {nameof(IHybridWebView)}.{nameof(IHybridWebView.InvokeJavaScriptType)} property must have a value in order to invoke a .NET method from JavaScript.");

            var invokeData = JsonSerializer.Deserialize<JSInvokeMethodData>(body);
            if (invokeData?.MethodName is null)
                throw new InvalidOperationException("The invoke data did not provide a method name.");

            var invokeResultRaw = await InvokeDotNetMethodAsync(invokeTargetType, invokeTarget, invokeData).ConfigureAwait(false);
            var invokeResult = CreateInvokeResult(invokeResultRaw);
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(invokeResult));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error(Tag, $"An error occurred while invoking a .NET method from JavaScript: {ex.Message}", ex);
            var errorResult = new DotNetInvokeResult
            {
                IsError = true,
                ErrorMessage = ex.Message,
                ErrorType = ex.GetType().Name,
                ErrorStackTrace = ex.StackTrace,
            };
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(errorResult));
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "HybridWebView needs dynamic JSON.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "HybridWebView needs dynamic JSON.")]
    private static DotNetInvokeResult CreateInvokeResult(object? result)
    {
        if (result is null)
            return new DotNetInvokeResult();
        var resultType = result.GetType();
        if (resultType.IsArray || resultType.IsClass)
            return new DotNetInvokeResult { Result = JsonSerializer.Serialize(result), IsJson = true };
        return new DotNetInvokeResult { Result = result };
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "HybridWebView needs dynamic JSON.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "HybridWebView needs dynamic JSON.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Task<T>.Result of the invoked method's own return type.")]
    private static async Task<object?> InvokeDotNetMethodAsync(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type targetType,
        object jsInvokeTarget,
        JSInvokeMethodData invokeData)
    {
        var requestMethodName = invokeData.MethodName!;
        var requestParams = invokeData.ParamValues;

        var dotnetMethod = targetType.GetMethod(requestMethodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod)
            ?? throw new InvalidOperationException($"The method {requestMethodName} couldn't be found on the {nameof(jsInvokeTarget)} of type {jsInvokeTarget.GetType().FullName}.");
        var dotnetParams = dotnetMethod.GetParameters();
        if (requestParams is not null && dotnetParams.Length != requestParams.Length)
            throw new InvalidOperationException($"The number of parameters on {nameof(jsInvokeTarget)}'s method {requestMethodName} ({dotnetParams.Length}) doesn't match the number of values passed from JavaScript code ({requestParams.Length}).");

        object?[]? invokeParamValues = null;
        if (requestParams is not null)
        {
            invokeParamValues = new object?[requestParams.Length];
            for (var i = 0; i < requestParams.Length; i++)
                invokeParamValues[i] = JsonSerializer.Deserialize(requestParams[i], dotnetParams[i].ParameterType);
        }

        object? dotnetReturnValue;
        try
        {
            dotnetReturnValue = dotnetMethod.Invoke(jsInvokeTarget, invokeParamValues);
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
            throw;
        }

        if (dotnetReturnValue is null)
            return null;
        if (dotnetReturnValue is Task task)
        {
            await task.ConfigureAwait(false);
            if (dotnetMethod.ReturnType.IsGenericType)
                return dotnetMethod.ReturnType.GetProperty(nameof(Task<object>.Result))?.GetValue(task);
            return null;
        }
        return dotnetReturnValue;
    }

    #endregion

    #region .NET to JavaScript

    /// <summary>
    /// MAUI's HybridWebView.EvaluateJavaScriptAsync on WebKit: the script runs in <c>eval</c>, its
    /// value comes back JSON-encoded, then unquoted; an error or undefined gives null.
    /// </summary>
    public async Task<string?> EvaluateJavaScriptAsync(string script)
    {
        var wrapped = "try{JSON.stringify(eval('" + EscapeJsString(script) + "'))}catch(e){'null'};";
        var result = await _host.EvaluateJavaScriptAsync(wrapped).ConfigureAwait(false);
        if (result == "null")
            return null;
        return result?.Trim('"');
    }

    /// <summary>
    /// MAUI's InvokeJavaScriptAsync: calls <c>window.HybridWebView.__InvokeJavaScript</c> with a
    /// task id, and completes when the page reports that task's result.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Serialized with the caller's JsonTypeInfo.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Serialized with the caller's JsonTypeInfo.")]
    public async Task<object?> InvokeJavaScriptAsync(HybridWebViewInvokeJavaScriptRequest request)
    {
        var (taskId, callback) = CreateTask();
        try
        {
            var paramsValues = request.ParamValues == null
                ? string.Empty
                : string.Join(", ", request.ParamValues.Select((v, i) => v == null ? "null" : JsonSerializer.Serialize(v, request.ParamJsonTypeInfos![i]!)));

            await EvaluateJavaScriptAsync($"window.HybridWebView.__InvokeJavaScript({taskId}, {request.MethodName}, [{paramsValues}])").ConfigureAwait(false);

            var stringResult = await callback.Task.ConfigureAwait(false);
            if (stringResult is null || stringResult == "null" || stringResult == "undefined")
                return null;
            if (request.ReturnTypeJsonTypeInfo is null)
                return null;
            return JsonSerializer.Deserialize(stringResult, request.ReturnTypeJsonTypeInfo);
        }
        finally
        {
            _tasks.TryRemove(taskId, out _);
        }
    }

    /// <summary>The script that delivers a raw message to the page (MAUI's MauiHybridWebView.SendRawMessage on WebKit).</summary>
    public static string BuildSendRawMessageScript(string rawMessage) =>
        $"window.external.receiveMessage({JsonSerializer.Serialize(rawMessage ?? string.Empty)})";

    public void SendRawMessage(string rawMessage)
    {
        _ = _host.EvaluateJavaScriptAsync(BuildSendRawMessageScript(rawMessage)).ContinueWith(
            t => DiagnosticLog.Error(Tag, "SendRawMessage failed", t.Exception!.GetBaseException()),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>Fails every InvokeJavaScriptAsync still waiting (the view went away).</summary>
    public void CancelPendingTasks()
    {
        foreach (var id in _tasks.Keys.ToArray())
        {
            if (_tasks.TryRemove(id, out var tcs))
                tcs.TrySetCanceled();
        }
    }

    private (string TaskId, TaskCompletionSource<string?> Callback) CreateTask()
    {
        var taskId = Interlocked.Increment(ref _lastTaskId).ToString("0", CultureInfo.InvariantCulture);
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _tasks[taskId] = tcs;
        return (taskId, tcs);
    }

    private void SetTaskCompleted(string taskId, string result)
    {
        if (_tasks.TryRemove(taskId, out var tcs))
            tcs.TrySetResult(result);
    }

    private void SetTaskFailed(string taskId, Exception exception)
    {
        if (_tasks.TryRemove(taskId, out var tcs))
            tcs.TrySetException(exception);
    }

    /// <summary>MAUI's WebViewHelper.EscapeJsString: a single-quoted string literal for eval().</summary>
    internal static string EscapeJsString(string js) => js
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("'", "\\'", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\u2028", "\\u2028", StringComparison.Ordinal)
        .Replace("\u2029", "\\u2029", StringComparison.Ordinal);

    #endregion
}
