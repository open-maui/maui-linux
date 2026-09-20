// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Text;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Authentication;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux implementation of <see cref="IWebAuthenticator"/> using the OAuth
/// "loopback redirect" flow: the authorization URL is opened in the system
/// browser (xdg-open through the Essentials <see cref="ILauncher"/> path) and a
/// minimal <see cref="HttpListener"/> on the loopback interface waits for the
/// provider to redirect to the callback URL. Only <c>http://localhost:&lt;port&gt;/...</c>
/// and <c>http://127.0.0.1:&lt;port&gt;/...</c> callbacks are supported; custom
/// URI schemes (<c>myapp://callback</c>) require a desktop-file handler and
/// D-Bus activation that the platform does not provide, so they throw
/// <see cref="NotSupportedException"/> with guidance.
/// </summary>
public sealed class WebAuthenticatorService : IWebAuthenticator
{
    /// <summary>Default time to wait for the browser round-trip.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

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
    /// Maximum time to wait for the callback before failing with
    /// <see cref="TaskCanceledException"/>.
    /// </summary>
    public TimeSpan Timeout { get; set; } = DefaultTimeout;

    /// <inheritdoc />
    public Task<WebAuthenticatorResult> AuthenticateAsync(WebAuthenticatorOptions webAuthenticatorOptions)
        => AuthenticateAsync(webAuthenticatorOptions, CancellationToken.None);

    /// <inheritdoc />
    public async Task<WebAuthenticatorResult> AuthenticateAsync(WebAuthenticatorOptions webAuthenticatorOptions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(webAuthenticatorOptions);
        var url = webAuthenticatorOptions.Url ?? throw new ArgumentException("Url is required.", nameof(webAuthenticatorOptions));
        var callback = webAuthenticatorOptions.CallbackUrl ?? throw new ArgumentException("CallbackUrl is required.", nameof(webAuthenticatorOptions));

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
    /// Validates that <paramref name="callback"/> is an http(s) loopback URL.
    /// </summary>
    internal static void EnsureLoopbackCallback(Uri callback)
    {
        bool isHttp = callback.Scheme == Uri.UriSchemeHttp || callback.Scheme == Uri.UriSchemeHttps;
        if (!isHttp)
        {
            throw new NotSupportedException(
                $"Callback scheme '{callback.Scheme}' is not supported on Linux. Register a loopback " +
                "callback such as http://localhost:PORT/callback with your identity provider; custom " +
                "URI schemes need a desktop-file handler that OpenMaui does not install.");
        }
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
