// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Single-instance routing of custom-scheme callbacks, the Linux counterpart of the
/// AppInstance key MAUI registers on Windows ("Microsoft.Maui.WebAuthenticator:&lt;scheme&gt;").
/// The instance waiting for a callback listens on a Unix socket named after the app and
/// the scheme, in the user's runtime directory (<c>$XDG_RUNTIME_DIR/openmaui</c>, or the
/// app's shared <c>$XDG_RUNTIME_DIR/app/$FLATPAK_ID</c> in a Flatpak). The process the
/// desktop starts for <c>myapp://callback?...</c> finds that socket, sends the URI, and
/// exits, as Windows redirects the activation and terminates the transient process.
/// </summary>
internal static class WebAuthenticatorCallbackRoute
{
    private const int MaxUriBytes = 64 * 1024;

    private static readonly Lock s_gate = new();
    private static RouteListener? s_listener;
    private static bool s_exitHookInstalled;

    /// <summary>Overrides the socket directory (tests).</summary>
    internal static string? DirectoryOverride { get; set; }

    /// <summary>Overrides the app identity the route is keyed on (tests).</summary>
    internal static string? AppKeyOverride { get; set; }

    /// <summary>How long the transient process waits for the owner to take the URI.</summary>
    internal static TimeSpan ForwardTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>The scheme the current process listens for, else null.</summary>
    internal static string? OwnedScheme
    {
        get
        {
            lock (s_gate)
                return s_listener?.Scheme;
        }
    }

    internal static string RouteDirectory()
    {
        if (DirectoryOverride != null)
            return DirectoryOverride;
        var runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        var flatpak = Environment.GetEnvironmentVariable("FLATPAK_ID");
        if (!string.IsNullOrEmpty(runtime) && !string.IsNullOrEmpty(flatpak))
        {
            // Shared by every instance of the same Flatpak app.
            var appDir = Path.Combine(runtime, "app", flatpak);
            if (Directory.Exists(appDir))
                return appDir;
        }
        if (!string.IsNullOrEmpty(runtime))
            return Path.Combine(runtime, "openmaui");
        return Path.Combine(Path.GetTempPath(), $"openmaui-{Environment.UserName}");
    }

    /// <summary>The app identity routes are keyed on: the Flatpak id, else the handler command.</summary>
    internal static string AppKey()
    {
        if (AppKeyOverride != null)
            return AppKeyOverride;
        var flatpak = Environment.GetEnvironmentVariable("FLATPAK_ID");
        if (!string.IsNullOrEmpty(flatpak))
            return flatpak;
        return string.Join('\0', XdgAssociationEnvironment.AppHandlerCommand());
    }

    /// <summary>The route's socket path for <paramref name="scheme"/> (scheme compared case-insensitively).</summary>
    internal static string SocketPath(string scheme)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(AppKey() + "\n" + scheme.ToLowerInvariant()));
        return Path.Combine(RouteDirectory(), "webauth-" + Convert.ToHexStringLower(hash.AsSpan(0, 12)) + ".sock");
    }

    /// <summary>
    /// Makes this process the owner of the route for <paramref name="scheme"/>. A route
    /// for another scheme is replaced, as Windows' FindOrRegisterForKey replaces the
    /// framework key; the route stays registered after the request, until the process exits.
    /// </summary>
    /// <exception cref="InvalidOperationException">Another running instance owns the route.</exception>
    internal static void Register(string scheme, Func<Uri, bool> handler)
    {
        scheme = scheme.ToLowerInvariant();
        lock (s_gate)
        {
            if (s_listener != null && s_listener.Scheme == scheme)
            {
                s_listener.Handler = handler;
                return;
            }

            var path = SocketPath(scheme);
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);
            if (DirectoryOverride == null && !dir.Contains("/app/", StringComparison.Ordinal))
            {
                try { File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }

            if (File.Exists(path))
            {
                if (TryConnect(path, out var probe))
                {
                    probe!.Dispose();
                    throw new InvalidOperationException("Another app instance owns the WebAuthenticator callback route.");
                }
                // A socket left behind by an instance that crashed.
                try { File.Delete(path); } catch (IOException) { }
            }

            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                socket.Bind(new UnixDomainSocketEndPoint(path));
                socket.Listen(4);
            }
            catch (SocketException ex)
            {
                socket.Dispose();
                throw new InvalidOperationException("Another app instance owns the WebAuthenticator callback route.", ex);
            }

            s_listener?.Dispose();
            s_listener = new RouteListener(scheme, path, socket, handler);
            s_listener.Start();
            if (!s_exitHookInstalled)
            {
                s_exitHookInstalled = true;
                AppDomain.CurrentDomain.ProcessExit += (_, _) => Unregister();
            }
            DiagnosticLog.Debug("WebAuthenticator", $"Listening for '{scheme}' callbacks on {path}");
        }
    }

    /// <summary>Closes the route and removes its socket (process exit, tests).</summary>
    internal static void Unregister()
    {
        lock (s_gate)
        {
            s_listener?.Dispose();
            s_listener = null;
        }
    }

    /// <summary>
    /// The transient-process side: when another instance owns the route for
    /// <paramref name="callbackUri"/>'s scheme, sends it the URI and returns true (the
    /// caller then exits). False when no instance is listening.
    /// </summary>
    internal static bool TryForward(Uri callbackUri)
    {
        string path;
        try
        {
            path = SocketPath(callbackUri.Scheme);
        }
        catch (Exception)
        {
            return false;
        }

        lock (s_gate)
        {
            // This process owns the route itself: nothing to forward to.
            if (s_listener != null && s_listener.Path == path)
                return false;
        }

        if (!File.Exists(path) || !TryConnect(path, out var socket))
            return false;

        using (socket)
        {
            try
            {
                socket!.SendTimeout = (int)ForwardTimeout.TotalMilliseconds;
                socket.ReceiveTimeout = (int)ForwardTimeout.TotalMilliseconds;
                var bytes = Encoding.UTF8.GetBytes(callbackUri.OriginalString + "\n");
                socket.Send(bytes);
                socket.Shutdown(SocketShutdown.Send);
                var ack = new byte[2];
                var read = socket.Receive(ack);
                DiagnosticLog.Debug("WebAuthenticator", $"Forwarded callback to the running instance (accepted={read > 0 && ack[0] == (byte)'1'})");
            }
            catch (SocketException ex)
            {
                DiagnosticLog.Debug("WebAuthenticator", $"Unable to redirect the callback activation: {ex.Message}");
            }
        }
        return true;
    }

    private static bool TryConnect(string path, out Socket? socket)
    {
        socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            socket.Connect(new UnixDomainSocketEndPoint(path));
            return true;
        }
        catch (SocketException)
        {
            socket.Dispose();
            socket = null;
            return false;
        }
    }

    private sealed class RouteListener : IDisposable
    {
        private readonly Socket _socket;
        private readonly CancellationTokenSource _cts = new();

        public RouteListener(string scheme, string path, Socket socket, Func<Uri, bool> handler)
        {
            Scheme = scheme;
            Path = path;
            _socket = socket;
            Handler = handler;
        }

        public string Scheme { get; }

        public string Path { get; }

        public Func<Uri, bool> Handler { get; set; }

        public void Start() => _ = Task.Run(AcceptLoopAsync);

        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                Socket client;
                try
                {
                    client = await _socket.AcceptAsync(_cts.Token).ConfigureAwait(false);
                }
                catch (Exception) when (_cts.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Debug("WebAuthenticator", $"Callback route accept failed: {ex.Message}");
                    return;
                }
                _ = Task.Run(() => ServeAsync(client));
            }
        }

        private async Task ServeAsync(Socket client)
        {
            using (client)
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                    timeout.CancelAfter(ForwardTimeout);
                    var buffer = new MemoryStream();
                    var chunk = new byte[4096];
                    while (buffer.Length <= MaxUriBytes)
                    {
                        var read = await client.ReceiveAsync(chunk, SocketFlags.None, timeout.Token).ConfigureAwait(false);
                        if (read == 0)
                            break;
                        buffer.Write(chunk, 0, read);
                        if (Array.IndexOf(chunk, (byte)'\n', 0, read) >= 0)
                            break;
                    }
                    var text = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)Math.Min(buffer.Length, MaxUriBytes)).TrimEnd('\n', '\r');
                    var handled = Uri.TryCreate(text, UriKind.Absolute, out var uri) && Handler(uri);
                    if (!handled)
                        DiagnosticLog.Debug("WebAuthenticator", "A forwarded callback matched no pending authentication");
                    await client.SendAsync(Encoding.ASCII.GetBytes(handled ? "1\n" : "0\n"), SocketFlags.None, timeout.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Debug("WebAuthenticator", $"Callback route read failed: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _socket.Dispose(); } catch { }
            try { File.Delete(Path); } catch { }
        }
    }
}
