// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Text;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Authentication;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux implementation of <see cref="IWebAuthenticator"/>. The authorization URL opens
/// in the system browser (xdg-open through the Essentials <see cref="ILauncher"/> path),
/// and the provider's redirect comes back one of two ways:
/// <list type="bullet">
/// <item>A custom URI scheme (<c>myapp://callback</c>), as on Windows. The scheme is
/// registered with the desktop for the app (<see cref="UriSchemeRegistration"/>), and the
/// process the desktop starts for the callback hands the URI to the running instance
/// and exits, as Windows redirects the protocol activation to the instance that owns the
/// callback route. The request then completes with MAUI's
/// <see cref="WebAuthenticatorResult"/> parsing.</item>
/// <item>A loopback URL (<c>http://localhost:&lt;port&gt;/...</c> or
/// <c>http://127.0.0.1:&lt;port&gt;/...</c>), a Linux addition: a minimal
/// <see cref="HttpListener"/> waits for the redirect.</item>
/// </list>
/// </summary>
public sealed class WebAuthenticatorService : IWebAuthenticator
{
    /// <summary>Default time to wait for a loopback callback.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    private static readonly Lock s_schemeGate = new();
    private static SchemeRequest? s_activeSchemeRequest;

    private readonly Func<Uri, Task<bool>> _openBrowser;
    private readonly Lock _lock = new();
    private TaskCompletionSource<WebAuthenticatorResult>? _pending;

    /// <summary>
    /// Creates an authenticator that launches URLs through <see cref="LauncherService"/>.
    /// </summary>
    public WebAuthenticatorService()
        : this(uri => new LauncherService().OpenAsync(uri))
    {
    }

    /// <summary>
    /// Creates an authenticator with a custom browser launcher (tests, embedded
    /// web views).
    /// </summary>
    public WebAuthenticatorService(Func<Uri, Task<bool>> openBrowser)
    {
        _openBrowser = openBrowser ?? throw new ArgumentNullException(nameof(openBrowser));
    }

    /// <summary>
    /// Maximum time to wait for a loopback callback before failing with
    /// <see cref="TaskCanceledException"/>. A custom-scheme request waits, as on
    /// Windows, until the callback arrives or its cancellation token is canceled.
    /// </summary>
    public TimeSpan Timeout { get; set; } = DefaultTimeout;

    /// <summary>
    /// Whether <see cref="AuthenticateAsync(WebAuthenticatorOptions, CancellationToken)"/>
    /// registers an unregistered callback scheme for the app
    /// (<see cref="UriSchemeRegistration.Register"/>) before opening the browser. On by
    /// default; when off, an unregistered scheme throws <see cref="InvalidOperationException"/>,
    /// as on Windows when an unpackaged app has not called
    /// <c>ActivationRegistrationManager.RegisterForProtocolActivation</c>.
    /// </summary>
    public bool RegisterCallbackScheme { get; set; } = true;

    /// <summary>Replaces bringing the app window to the front before a callback completes (tests).</summary>
    internal static Action? BringToForegroundOverride { get; set; }

    /// <inheritdoc />
    public Task<WebAuthenticatorResult> AuthenticateAsync(WebAuthenticatorOptions webAuthenticatorOptions)
        => AuthenticateAsync(webAuthenticatorOptions, CancellationToken.None);

    /// <inheritdoc />
    public async Task<WebAuthenticatorResult> AuthenticateAsync(WebAuthenticatorOptions webAuthenticatorOptions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(webAuthenticatorOptions);
        var url = webAuthenticatorOptions.Url ?? throw new ArgumentNullException(nameof(webAuthenticatorOptions.Url));
        var callback = webAuthenticatorOptions.CallbackUrl ?? throw new ArgumentNullException(nameof(webAuthenticatorOptions.CallbackUrl));
        if (!url.IsAbsoluteUri)
            throw new ArgumentException("The authentication URL must be absolute.", nameof(webAuthenticatorOptions.Url));
        if (!callback.IsAbsoluteUri)
            throw new ArgumentException("The callback URL must be absolute.", nameof(webAuthenticatorOptions.CallbackUrl));

        if (callback.Scheme == Uri.UriSchemeHttp || callback.Scheme == Uri.UriSchemeHttps)
            return await AuthenticateLoopbackAsync(webAuthenticatorOptions, url, callback, cancellationToken).ConfigureAwait(false);
        return await AuthenticateCustomSchemeAsync(webAuthenticatorOptions, url, callback, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WebAuthenticatorResult> AuthenticateCustomSchemeAsync(WebAuthenticatorOptions options, Uri url, Uri callback, CancellationToken cancellationToken)
    {
        EnsureCallbackSchemeRegistered(callback.Scheme, RegisterCallbackScheme);
        cancellationToken.ThrowIfCancellationRequested();

        var request = new SchemeRequest(callback, options.ResponseDecoder, cancellationToken);
        lock (s_schemeGate)
        {
            if (s_activeSchemeRequest != null)
                throw new InvalidOperationException("Another WebAuthenticator operation is already in progress.");
            s_activeSchemeRequest = request;
        }

        var registration = cancellationToken.Register(static state =>
        {
            var r = (SchemeRequest)state!;
            if (TryReserve(r))
                r.Completion.TrySetCanceled(r.CancellationToken);
        }, request);

        try
        {
            if (!request.Completion.Task.IsCompleted)
            {
                try
                {
                    WebAuthenticatorCallbackRoute.Register(callback.Scheme, TryHandleCallback);
                }
                catch (Exception ex)
                {
                    TryFail(request, new InvalidOperationException("Unable to register the WebAuthenticator callback route.", ex));
                }
            }

            if (!request.Completion.Task.IsCompleted)
            {
                DiagnosticLog.Debug("WebAuthenticator", $"Opening the browser and waiting for a '{callback.Scheme}' callback");
                try
                {
                    if (!await _openBrowser(url).ConfigureAwait(false))
                        TryFail(request, new InvalidOperationException("Failed to launch the browser for authentication."));
                }
                catch (Exception ex)
                {
                    TryFail(request, new InvalidOperationException("Failed to launch the browser for authentication.", ex));
                }
            }

            return await request.Completion.Task.ConfigureAwait(false);
        }
        finally
        {
            // The route stays registered after the request, as Windows keeps its key.
            registration.Dispose();
            lock (s_schemeGate)
            {
                if (ReferenceEquals(s_activeSchemeRequest, request))
                    s_activeSchemeRequest = null;
            }
        }
    }

    /// <summary>
    /// Validates the custom callback scheme against the desktop's registration (Windows
    /// checks the registry, or the manifest of a packaged app), registering it for the
    /// app when <paramref name="register"/> is set and the app is not sandboxed.
    /// </summary>
    internal static void EnsureCallbackSchemeRegistered(string scheme, bool register)
    {
        if (UriSchemeRegistration.IsRegistered(scheme))
            return;

        if (UriSchemeRegistration.Environment.FlatpakDesktopFile != null)
        {
            throw new InvalidOperationException(
                $"You need to declare MimeType=x-scheme-handler/{scheme.ToLowerInvariant()}; in the desktop file " +
                "the Flatpak exports for this application.");
        }
        if (!register)
        {
            throw new InvalidOperationException(
                $"The URI scheme '{scheme}' is not registered for this application. " +
                "Call UriSchemeRegistration.Register, or install a desktop file that handles x-scheme-handler/" +
                scheme.ToLowerInvariant() + ".");
        }
        UriSchemeRegistration.Register(scheme);
    }

    /// <summary>
    /// Completes the pending custom-scheme request with <paramref name="callbackUri"/> when it
    /// matches the request's callback route (scheme; host and port when the callback has a
    /// host; path unless it is "/"), as MAUI's <c>WebUtils.CanHandleCallback</c> does.
    /// Duplicate callbacks for a request already completing count as handled.
    /// </summary>
    internal static bool TryHandleCallback(Uri? callbackUri)
    {
        SchemeRequest? request;
        lock (s_schemeGate)
        {
            request = s_activeSchemeRequest;
            if (request == null || callbackUri == null || !callbackUri.IsAbsoluteUri)
                return false;
            if (!CanHandleCallback(request.CallbackUrl, callbackUri))
                return false;
            if (request.CompletionClaimed)
                return true;
            request.CompletionClaimed = true;
        }

        try
        {
            BringToForeground();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("WebAuthenticator", $"Pre-completion callback failed ({ex.GetType().Name})");
        }

        try
        {
            request.Completion.TrySetResult(new WebAuthenticatorResult(callbackUri, request.ResponseDecoder));
        }
        catch (Exception ex)
        {
            // Decoder exceptions go to the caller.
            request.Completion.TrySetException(ex);
        }
        return true;
    }

    /// <summary>
    /// The transient-process side of a custom-scheme callback, run by
    /// <c>LinuxApplication.Run</c> before any window exists: when an argument is a URI that
    /// this process's own pending request accepts, completes it; when another instance of
    /// the app owns the callback route for the URI's scheme, forwards the URI there and
    /// returns true, and the caller exits (Windows redirects the activation and terminates
    /// the process). False lets the app start normally.
    /// </summary>
    internal static bool TryRedirectCallbackActivation(IReadOnlyList<string>? args)
    {
        if (args == null)
            return false;
        foreach (var arg in args)
        {
            if (string.IsNullOrEmpty(arg) || arg.StartsWith('-') ||
                !Uri.TryCreate(arg, UriKind.Absolute, out var uri) ||
                uri.IsFile || uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            {
                continue;
            }
            if (TryHandleCallback(uri))
                return false;
            if (WebAuthenticatorCallbackRoute.TryForward(uri))
                return true;
        }
        return false;
    }

    /// <summary>MAUI's WebUtils.CanHandleCallback (internal to Essentials).</summary>
    internal static bool CanHandleCallback(Uri expectedUrl, Uri callbackUrl)
    {
        if (expectedUrl is null || callbackUrl is null || !expectedUrl.IsAbsoluteUri || !callbackUrl.IsAbsoluteUri)
            return false;
        if (!callbackUrl.Scheme.Equals(expectedUrl.Scheme, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrEmpty(expectedUrl.Host))
        {
            if (!callbackUrl.Host.Equals(expectedUrl.Host, StringComparison.OrdinalIgnoreCase))
                return false;
            if (callbackUrl.Port != expectedUrl.Port)
                return false;
        }
        if (!string.IsNullOrEmpty(expectedUrl.AbsolutePath) && expectedUrl.AbsolutePath != "/" &&
            !callbackUrl.AbsolutePath.Equals(expectedUrl.AbsolutePath, StringComparison.Ordinal))
            return false;
        return true;
    }

    private static bool TryReserve(SchemeRequest request)
    {
        lock (s_schemeGate)
        {
            if (!ReferenceEquals(s_activeSchemeRequest, request) || request.CompletionClaimed)
                return false;
            request.CompletionClaimed = true;
            return true;
        }
    }

    private static void TryFail(SchemeRequest request, Exception exception)
    {
        if (TryReserve(request))
            request.Completion.TrySetException(exception);
    }

    /// <summary>
    /// Brings the app window forward before the result is delivered, as Windows does
    /// (best effort: an X11 window asks the window manager through _NET_ACTIVE_WINDOW;
    /// a Wayland compositor gives focus only with an activation token, so the window
    /// stays where it is there).
    /// </summary>
    private static void BringToForeground()
    {
        if (BringToForegroundOverride is { } overrideAction)
        {
            overrideAction();
            return;
        }
        var window = LinuxApplication.Current?.MainWindow as Microsoft.Maui.Platform.Linux.Window.X11Window;
        if (window == null)
            return;
        var dispatcher = Microsoft.Maui.Platform.Linux.Dispatching.LinuxDispatcher.Main;
        if (dispatcher == null)
            return;
        dispatcher.Dispatch(() =>
        {
            try { window.RequestActivation(); }
            catch (Exception ex) { DiagnosticLog.Debug("WebAuthenticator", $"Unable to bring the window to the front: {ex.Message}"); }
        });
    }

    private sealed class SchemeRequest
    {
        public SchemeRequest(Uri callbackUrl, IWebAuthenticatorResponseDecoder? decoder, CancellationToken cancellationToken)
        {
            CallbackUrl = callbackUrl;
            ResponseDecoder = decoder;
            CancellationToken = cancellationToken;
        }

        public Uri CallbackUrl { get; }

        public IWebAuthenticatorResponseDecoder? ResponseDecoder { get; }

        public CancellationToken CancellationToken { get; }

        public TaskCompletionSource<WebAuthenticatorResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Read and written only under s_schemeGate.
        public bool CompletionClaimed;
    }

    private async Task<WebAuthenticatorResult> AuthenticateLoopbackAsync(WebAuthenticatorOptions webAuthenticatorOptions, Uri url, Uri callback, CancellationToken cancellationToken)
    {
        EnsureLoopbackCallback(callback);

        lock (_lock)
        {
            if (_pending != null && !_pending.Task.IsCompleted)
                throw new InvalidOperationException("Another web authentication is already in progress.");
            _pending = new TaskCompletionSource<WebAuthenticatorResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        var pending = _pending;

        using var listener = new HttpListener();
        listener.Prefixes.Add(BuildPrefix(callback));

        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            pending.TrySetException(ex);
            throw new InvalidOperationException($"Could not listen on '{callback}' for the OAuth callback: {ex.Message}", ex);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Timeout);
        using var registration = cts.Token.Register(() => pending.TrySetCanceled(cts.Token));

        var serverTask = ServeAsync(listener, callback, webAuthenticatorOptions.ResponseDecoder, pending, cts.Token);

        DiagnosticLog.Debug("WebAuthenticator", $"Opening '{url}' and waiting for callback on '{callback}'");
        bool opened;
        try
        {
            opened = await _openBrowser(url).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            pending.TrySetException(ex);
            opened = false;
        }
        if (!opened)
        {
            pending.TrySetException(new InvalidOperationException($"Could not open the system browser for '{url}'."));
        }

        try
        {
            return await pending.Task.ConfigureAwait(false);
        }
        finally
        {
            try { listener.Stop(); } catch { }
            try { await serverTask.ConfigureAwait(false); } catch { }
        }
    }

    /// <summary>
    /// Validates that an http(s) <paramref name="callback"/> is a plain-http loopback URL.
    /// </summary>
    internal static void EnsureLoopbackCallback(Uri callback)
    {
        if (callback.Scheme == Uri.UriSchemeHttps)
        {
            throw new NotSupportedException(
                "HTTPS loopback callbacks are not supported (no certificate is available for the local listener). " +
                "Use http://localhost:PORT/... instead.");
        }
        if (!callback.IsLoopback)
        {
            throw new NotSupportedException(
                $"Callback host '{callback.Host}' is not a loopback address. Use http://localhost:PORT/... or http://127.0.0.1:PORT/....");
        }
    }

    /// <summary>
    /// Builds the <see cref="HttpListener"/> prefix for <paramref name="callback"/>:
    /// scheme, host and port with a trailing slash so any path is accepted.
    /// </summary>
    internal static string BuildPrefix(Uri callback)
        => $"{callback.Scheme}://{callback.Host}:{callback.Port}/";

    private static async Task ServeAsync(HttpListener listener, Uri callback, IWebAuthenticatorResponseDecoder? decoder,
        TaskCompletionSource<WebAuthenticatorResult> pending, CancellationToken token)
    {
        var expectedPath = callback.AbsolutePath.TrimEnd('/');
        try
        {
            while (!pending.Task.IsCompleted && !token.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception) when (pending.Task.IsCompleted || token.IsCancellationRequested || !listener.IsListening)
                {
                    return;
                }

                var request = context.Request;
                var requestPath = (request.Url?.AbsolutePath ?? "/").TrimEnd('/');
                if (!string.Equals(requestPath, expectedPath, StringComparison.Ordinal))
                {
                    await WriteResponseAsync(context.Response, 404, "Not the expected callback path.").ConfigureAwait(false);
                    continue;
                }

                var received = request.Url!;
                // Rebuild the URI on the registered callback so the authority the
                // provider used (localhost vs 127.0.0.1) does not leak into results.
                var resultUri = new UriBuilder(callback) { Query = received.Query.TrimStart('?'), Fragment = received.Fragment.TrimStart('#') }.Uri;

                try
                {
                    var result = new WebAuthenticatorResult(resultUri, decoder);
                    await WriteResponseAsync(context.Response, 200,
                        "<html><body style=\"font-family:sans-serif\"><h2>Authentication complete</h2><p>You can close this window and return to the application.</p></body></html>")
                        .ConfigureAwait(false);
                    pending.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    await WriteResponseAsync(context.Response, 500, "Failed to parse the callback.").ConfigureAwait(false);
                    pending.TrySetException(ex);
                }
                return;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("WebAuthenticator", $"Listener loop ended: {ex.Message}");
            pending.TrySetException(ex);
        }
    }

    private static async Task WriteResponseAsync(HttpListenerResponse response, int status, string body)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            response.StatusCode = status;
            response.ContentType = body.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ? "text/html; charset=utf-8" : "text/plain; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("WebAuthenticator", $"Failed to write callback response: {ex.Message}");
        }
        finally
        {
            try { response.Close(); } catch { }
        }
    }
}
