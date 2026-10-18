// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>What <see cref="HybridWebViewHttpProxy"/> asks of the view it serves.</summary>
internal interface IHybridWebViewHttpProxyHost
{
    /// <summary>
    /// Has the view's network accept the proxy's certificate for <paramref name="host"/>;
    /// awaited before the proxy answers the CONNECT for that host.
    /// </summary>
    Task AllowCertificateForHostAsync(string host);

    /// <summary>Offers the request to the app; null when the app did not handle it.</summary>
    Task<HybridWebResponse?> InterceptAsync(HybridWebRequest request);
}

/// <summary>
/// The loopback HTTP proxy one HybridWebView's network goes through while the app handles
/// <c>WebResourceRequested</c>: every http and https request the page makes is offered to the
/// app first, as WebView2 does on Windows; the requests the app does not handle go out to the
/// server unchanged. https arrives as a CONNECT tunnel that the proxy terminates with its own
/// certificate, which only the view's network accepts (and only for the hosts it was allowed
/// for); the proxy then makes the request to the real server itself over TLS that is verified
/// normally. Listens on 127.0.0.1 only, on a random port, and answers only clients that send
/// its random password (HTTP proxy authentication), so no other process or user can use it.
/// WebSocket upgrades are tunnelled to the server untouched.
/// </summary>
internal sealed class HybridWebViewHttpProxy : IDisposable
{
    internal const string UserName = "openmaui";
    private const int MaxHeadBytes = 64 * 1024;
    private const string Realm = "OpenMaui HybridWebView";
    private static readonly SslApplicationProtocol[] Http11Only = { SslApplicationProtocol.Http11 };

    private static readonly HashSet<string> HopByHop = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization", "Proxy-Connection",
        "TE", "Trailer", "Transfer-Encoding", "Upgrade",
    };

    private readonly IHybridWebViewHttpProxyHost _host;
    private readonly X509Certificate2 _certificate;
    private readonly HttpMessageInvoker _upstream;
    private readonly bool _ownsUpstream;
    private readonly Func<string, int, bool, CancellationToken, Task<Stream>> _connectTunnel;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly byte[] _expectedCredentials;
    private readonly ConcurrentDictionary<string, Lazy<Task>> _allowedHosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Stream, byte> _connections = new();
    private int _disposed;

    /// <param name="host">The view: certificate allowances and the app's answers.</param>
    /// <param name="certificate">The proxy's TLS certificate, with its private key (<see cref="CreateCertificate"/>).</param>
    /// <param name="upstream">Sends the requests the app does not handle (default: a handler that verifies TLS normally).</param>
    /// <param name="connectTunnel">Opens a WebSocket tunnel to host, port, over TLS or not (default: TCP, TLS verified normally).</param>
    public HybridWebViewHttpProxy(
        IHybridWebViewHttpProxyHost host,
        X509Certificate2 certificate,
        HttpMessageInvoker? upstream = null,
        Func<string, int, bool, CancellationToken, Task<Stream>>? connectTunnel = null)
    {
        _host = host;
        _certificate = certificate;
        _ownsUpstream = upstream == null;
        _upstream = upstream ?? new HttpMessageInvoker(CreateUpstreamHandler());
        _connectTunnel = connectTunnel ?? ConnectTunnelAsync;
        Password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _expectedCredentials = Encoding.ASCII.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes($"{UserName}:{Password}")));
        _listener = new TcpListener(IPAddress.Loopback, 0);
    }

    /// <summary>The random password clients must send (Basic proxy authentication, user <see cref="UserName"/>).</summary>
    public string Password { get; }

    /// <summary>The port the proxy listens on (127.0.0.1), once started.</summary>
    public int Port { get; private set; }

    /// <summary>The proxy's address for WebKit's proxy settings (no credentials: they go through the authenticate signal).</summary>
    public string ProxyUri => $"http://127.0.0.1:{Port}/";

    /// <summary>A self-signed server certificate with an in-memory key, for one proxy's lifetime.</summary>
    public static X509Certificate2 CreateCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=OpenMaui HybridWebView interception", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
        var now = DateTimeOffset.UtcNow;
        using var created = request.CreateSelfSigned(now.AddDays(-1), now.AddYears(1));
        // Round-trip through PKCS#12 so SslStream gets a certificate whose key it can always use.
        var pfx = created.Export(X509ContentType.Pkcs12);
        try
        {
            return X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.EphemeralKeySet);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pfx);
        }
    }

    /// <summary>The handler for requests the app leaves alone: TLS verified as for any client, nothing added or changed.</summary>
    internal static SocketsHttpHandler CreateUpstreamHandler() => new()
    {
        AllowAutoRedirect = false,           // the page sees redirects, as without the proxy
        UseCookies = false,                  // cookies are WebKit's, sent as the page's headers
        AutomaticDecompression = DecompressionMethods.None,
        PreAuthenticate = false,
        Credentials = null,
        ActivityHeadersPropagator = null,    // no traceparent headers
        ConnectTimeout = TimeSpan.FromSeconds(30),
        PooledConnectionIdleTimeout = TimeSpan.FromSeconds(60),
    };

    public void Start()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = AcceptLoopAsync();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _stop.Cancel();
        try { _listener.Stop(); } catch { }
        foreach (var connection in _connections.Keys)
        {
            try { connection.Dispose(); } catch { }
        }
        if (_ownsUpstream)
            _upstream.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
            }
            catch (Exception) when (_stop.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Warn("HybridWebView", $"Interception proxy stopped accepting: {ex.Message}");
                return;
            }
            _ = ServeClientAsync(client);
        }
    }

    private async Task ServeClientAsync(TcpClient client)
    {
        var token = _stop.Token;
        using var owned = client;
        client.NoDelay = true;
        var stream = client.GetStream();
        _connections.TryAdd(stream, 0);
        try
        {
            var connection = new ProxyConnection(stream);
            var first = await connection.ReadHeadAsync(token).ConfigureAwait(false);
            if (first == null)
                return;
            if (!IsAuthorized(first))
            {
                await connection.WriteAsync(
                    "HTTP/1.1 407 Proxy Authentication Required\r\n" +
                    $"Proxy-Authenticate: Basic realm=\"{Realm}\"\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", token).ConfigureAwait(false);
                return;
            }

            if (first.Method == "CONNECT")
            {
                await ServeTunnelAsync(connection, first, token).ConfigureAwait(false);
                return;
            }

            // Plain http: absolute-form requests, any number on one authenticated connection.
            for (var head = first; head != null; head = await connection.ReadHeadAsync(token).ConfigureAwait(false))
            {
                // libsoup sends a plain WebSocket handshake to the proxy as "GET ws://host/path".
                if (Uri.TryCreate(head.Target, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeWs && head.IsUpgrade)
                    uri = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttp, Port = uri.Port }.Uri;
                if (uri == null || uri.Scheme != Uri.UriSchemeHttp)
                {
                    await connection.WriteAsync("HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", token).ConfigureAwait(false);
                    return;
                }
                if (head.IsUpgrade)
                {
                    await TunnelUpgradeAsync(connection, head, uri, tls: false, token).ConfigureAwait(false);
                    return;
                }
                if (!await ServeRequestAsync(connection, head, uri, token).ConfigureAwait(false))
                    return;
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException or AuthenticationException)
        {
            // The page or the server went away; WebKit reports the failed load.
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("HybridWebView", "Interception proxy connection failed", ex);
        }
        finally
        {
            _connections.TryRemove(stream, out _);
        }
    }

    private bool IsAuthorized(RequestHead head)
    {
        var value = head.Get("Proxy-Authorization");
        if (value == null || !value.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;
        var presented = Encoding.ASCII.GetBytes(value.Substring(6).Trim());
        return CryptographicOperations.FixedTimeEquals(presented, _expectedCredentials);
    }

    /// <summary>https: terminate the CONNECT tunnel with the proxy's certificate, then serve the requests inside it.</summary>
    private async Task ServeTunnelAsync(ProxyConnection connection, RequestHead connect, CancellationToken token)
    {
        if (!TrySplitAuthority(connect.Target, out var host, out var port))
        {
            await connection.WriteAsync("HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", token).ConfigureAwait(false);
            return;
        }

        // The view's network must accept the certificate for this host before its TLS handshake.
        var allowed = _allowedHosts.GetOrAdd(host, h => new Lazy<Task>(() => _host.AllowCertificateForHostAsync(h)));
        try
        {
            await allowed.Value.ConfigureAwait(false);
        }
        catch
        {
            _allowedHosts.TryRemove(new KeyValuePair<string, Lazy<Task>>(host, allowed));
            throw;
        }
        await connection.WriteAsync("HTTP/1.1 200 Connection established\r\n\r\n", token).ConfigureAwait(false);

        // libsoup also tunnels plain ws:// through CONNECT: TLS starts with a handshake record (0x16).
        var firstByte = await connection.PeekByteAsync(token).ConfigureAwait(false);
        if (firstByte < 0)
            return;
        if (firstByte != 0x16)
        {
            await ServeTunnelRequestsAsync(connection, Uri.UriSchemeHttp, host, port, token).ConfigureAwait(false);
            return;
        }

        using var tls = new SslStream(connection, leaveInnerStreamOpen: true);
        await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = _certificate,
            ApplicationProtocols = new List<SslApplicationProtocol>(Http11Only),
            ClientCertificateRequired = false,
        }, token).ConfigureAwait(false);

        await ServeTunnelRequestsAsync(new ProxyConnection(tls), Uri.UriSchemeHttps, host, port, token).ConfigureAwait(false);
    }

    /// <summary>The origin-form requests inside a tunnel to <paramref name="host"/>:<paramref name="port"/>.</summary>
    private async Task ServeTunnelRequestsAsync(ProxyConnection inner, string scheme, string host, int port, CancellationToken token)
    {
        bool defaultPort = port == (scheme == Uri.UriSchemeHttps ? 443 : 80);
        var authority = defaultPort ? FormatHost(host) : $"{FormatHost(host)}:{port}";
        for (var head = await inner.ReadHeadAsync(token).ConfigureAwait(false); head != null; head = await inner.ReadHeadAsync(token).ConfigureAwait(false))
        {
            if (!head.Target.StartsWith('/') || !Uri.TryCreate($"{scheme}://{authority}{head.Target}", UriKind.Absolute, out var uri))
            {
                await inner.WriteAsync("HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", token).ConfigureAwait(false);
                return;
            }
            if (head.IsUpgrade)
            {
                await TunnelUpgradeAsync(inner, head, uri, tls: scheme == Uri.UriSchemeHttps, token).ConfigureAwait(false);
                return;
            }
            if (!await ServeRequestAsync(inner, head, uri, token).ConfigureAwait(false))
                return;
        }
    }

    /// <summary>One request: the app's answer, else the server's. False when the connection must close.</summary>
    private async Task<bool> ServeRequestAsync(ProxyConnection connection, RequestHead head, Uri uri, CancellationToken token)
    {
        if (string.Equals(head.Get("Expect"), "100-continue", StringComparison.OrdinalIgnoreCase))
            await connection.WriteAsync("HTTP/1.1 100 Continue\r\n\r\n", token).ConfigureAwait(false);
        var body = await connection.ReadBodyAsync(head, token).ConfigureAwait(false);
        if (body == null)
        {
            await connection.WriteAsync("HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", token).ConfigureAwait(false);
            return false;
        }
        bool keepAlive = head.KeepAlive;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in head.Headers)
        {
            if (HopByHop.Contains(name))
                continue;
            headers[name] = headers.TryGetValue(name, out var existing) ? $"{existing}, {value}" : value;
        }
        var request = new HybridWebRequest(uri, head.Method, headers, body.Length > 0 ? body : null);

        HybridWebResponse? answer;
        try
        {
            answer = await _host.InterceptAsync(request).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("HybridWebView", $"WebResourceRequested for {uri} failed", ex);
            answer = null;
        }

        if (answer != null)
        {
            await WriteAppResponseAsync(connection, answer, head.Method == "HEAD", keepAlive, token).ConfigureAwait(false);
            return keepAlive;
        }
        return await ForwardAsync(connection, head, uri, body, keepAlive, token).ConfigureAwait(false);
    }

    private static async Task WriteAppResponseAsync(ProxyConnection connection, HybridWebResponse response, bool isHead, bool keepAlive, CancellationToken token)
    {
        var text = new StringBuilder();
        text.Append("HTTP/1.1 ").Append(response.StatusCode).Append(' ').Append(Clean(response.ReasonPhrase)).Append("\r\n");
        foreach (var (name, value) in response.Headers)
        {
            if (HopByHop.Contains(name) || name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) || !IsSafeHeader(name, value))
                continue;
            text.Append(name).Append(": ").Append(value).Append("\r\n");
        }
        text.Append("Content-Length: ").Append(response.Body.Length).Append("\r\n");
        text.Append(keepAlive ? "Connection: keep-alive\r\n\r\n" : "Connection: close\r\n\r\n");
        await connection.WriteAsync(text.ToString(), token).ConfigureAwait(false);
        if (!isHead && response.Body.Length > 0)
            await connection.WriteAsync(response.Body, token).ConfigureAwait(false);
        await connection.FlushAsync(token).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the request to the server and relays its response. A server that cannot be reached
    /// (including one whose TLS certificate does not verify) closes the page's connection
    /// without a response, so WebKit reports the load as failed.
    /// </summary>
    private async Task<bool> ForwardAsync(ProxyConnection connection, RequestHead head, Uri uri, byte[] body, bool keepAlive, CancellationToken token)
    {
        using var message = new HttpRequestMessage(new HttpMethod(head.Method), uri);
        if (body.Length > 0 || head.Get("Content-Length") != null || head.Get("Transfer-Encoding") != null)
            message.Content = new ByteArrayContent(body);
        foreach (var (name, value) in head.Headers)
        {
            if (HopByHop.Contains(name) || name.Equals("Host", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) || name.Equals("Expect", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!message.Headers.TryAddWithoutValidation(name, value))
                message.Content?.Headers.TryAddWithoutValidation(name, value);
        }

        HttpResponseMessage response;
        try
        {
            response = await _upstream.SendAsync(message, token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or AuthenticationException or TaskCanceledException)
        {
            DiagnosticLog.Debug("HybridWebView", $"{head.Method} {uri} failed: {ex.Message}");
            return false;
        }

        using (response)
        {
            int status = (int)response.StatusCode;
            bool noBody = head.Method == "HEAD" || status is >= 100 and < 200 or 204 or 304;
            long? length = response.Content.Headers.ContentLength;
            bool chunked = !noBody && length == null;

            var text = new StringBuilder();
            text.Append("HTTP/1.1 ").Append(status).Append(' ').Append(Clean(response.ReasonPhrase ?? string.Empty)).Append("\r\n");
            foreach (var (name, values) in response.Headers.NonValidated)
                AppendHeader(text, name, values);
            foreach (var (name, values) in response.Content.Headers.NonValidated)
                AppendHeader(text, name, values);
            if (chunked)
                text.Append("Transfer-Encoding: chunked\r\n");
            text.Append(keepAlive ? "Connection: keep-alive\r\n\r\n" : "Connection: close\r\n\r\n");
            await connection.WriteAsync(text.ToString(), token).ConfigureAwait(false);

            if (!noBody)
            {
                await using var content = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                var buffer = new byte[64 * 1024];
                long copied = 0;
                int read;
                while ((read = await content.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    if (chunked)
                    {
                        await connection.WriteAsync($"{read:X}\r\n", token).ConfigureAwait(false);
                        await connection.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                        await connection.WriteAsync("\r\n", token).ConfigureAwait(false);
                    }
                    else
                    {
                        await connection.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    }
                    copied += read;
                    await connection.FlushAsync(token).ConfigureAwait(false);
                }
                if (chunked)
                    await connection.WriteAsync("0\r\n\r\n", token).ConfigureAwait(false);
                else if (length is { } expected && copied != expected)
                    return false; // the server's body ended early: never let the page take it as complete
            }
            await connection.FlushAsync(token).ConfigureAwait(false);
        }
        return keepAlive;
    }

    private static void AppendHeader(StringBuilder text, string name, HeaderStringValues values)
    {
        if (HopByHop.Contains(name))
            return;
        foreach (var value in values)
        {
            if (IsSafeHeader(name, value))
                text.Append(name).Append(": ").Append(value).Append("\r\n");
        }
    }

    /// <summary>WebSocket (or any Upgrade): pass the request to the server and the bytes both ways.</summary>
    private async Task TunnelUpgradeAsync(ProxyConnection client, RequestHead head, Uri uri, bool tls, CancellationToken token)
    {
        Stream server;
        try
        {
            server = await _connectTunnel(uri.IdnHost, uri.Port, tls, token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or AuthenticationException or OperationCanceledException)
        {
            DiagnosticLog.Debug("HybridWebView", $"Upgrade to {uri} failed: {ex.Message}");
            return;
        }

        await using (server)
        {
            var text = new StringBuilder();
            text.Append(head.Method).Append(' ').Append(uri.PathAndQuery).Append(" HTTP/1.1\r\n");
            foreach (var (name, value) in head.Headers)
            {
                if (name.StartsWith("Proxy-", StringComparison.OrdinalIgnoreCase))
                    continue;
                text.Append(name).Append(": ").Append(value).Append("\r\n");
            }
            text.Append("\r\n");
            await server.WriteAsync(Encoding.Latin1.GetBytes(text.ToString()), token).ConfigureAwait(false);
            await server.FlushAsync(token).ConfigureAwait(false);

            using var both = CancellationTokenSource.CreateLinkedTokenSource(token);
            var up = PumpAsync(client, server, both.Token);
            var down = PumpAsync(server, client, both.Token);
            await Task.WhenAny(up, down).ConfigureAwait(false);
            both.Cancel();
            try { await Task.WhenAll(up, down).ConfigureAwait(false); } catch { }
        }
    }

    private static async Task PumpAsync(Stream from, Stream to, CancellationToken token)
    {
        var buffer = new byte[16 * 1024];
        try
        {
            int read;
            while ((read = await from.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                await to.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                await to.FlushAsync(token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException or SocketException)
        {
        }
    }

    private static async Task<Stream> ConnectTunnelAsync(string host, int port, bool tls, CancellationToken token)
    {
        var tcp = new TcpClient { NoDelay = true };
        try
        {
            await tcp.ConnectAsync(host, port, token).ConfigureAwait(false);
            Stream stream = tcp.GetStream();
            if (!tls)
                return new OwningStream(stream, tcp);
            var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
            // Default validation: the server's certificate must verify, as WebKit would require.
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host,
                ApplicationProtocols = new List<SslApplicationProtocol>(Http11Only),
            }, token).ConfigureAwait(false);
            return new OwningStream(ssl, tcp);
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    internal static bool TrySplitAuthority(string authority, out string host, out int port)
    {
        host = string.Empty;
        port = 0;
        int colon = authority.LastIndexOf(':');
        if (colon <= 0 || colon == authority.Length - 1 || authority.IndexOf(']') > colon)
            return false;
        if (!int.TryParse(authority.AsSpan(colon + 1), System.Globalization.NumberStyles.None, null, out port) || port is <= 0 or > 65535)
            return false;
        host = authority.Substring(0, colon);
        if (host.StartsWith('[') && host.EndsWith(']'))
            host = host.Substring(1, host.Length - 2);
        return host.Length > 0 && Uri.CheckHostName(host) != UriHostNameType.Unknown;
    }

    private static string FormatHost(string host) => host.Contains(':') ? $"[{host}]" : host;

    private static string Clean(string text) => text.Replace("\r", string.Empty).Replace("\n", string.Empty);

    private static bool IsSafeHeader(string name, string value) =>
        name.Length > 0 && name.IndexOfAny(new[] { '\r', '\n', ':', ' ' }) < 0 && value.IndexOfAny(new[] { '\r', '\n' }) < 0;

    /// <summary>A request line and its headers.</summary>
    internal sealed class RequestHead
    {
        public RequestHead(string method, string target, string version, List<KeyValuePair<string, string>> headers)
        {
            Method = method;
            Target = target;
            Version = version;
            Headers = headers;
        }

        public string Method { get; }
        public string Target { get; }
        public string Version { get; }
        public List<KeyValuePair<string, string>> Headers { get; }

        public string? Get(string name) => Headers.FirstOrDefault(h => h.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

        public bool IsUpgrade => Get("Upgrade") != null
            && (Get("Connection") ?? string.Empty).Split(',').Any(t => t.Trim().Equals("Upgrade", StringComparison.OrdinalIgnoreCase));

        public bool KeepAlive
        {
            get
            {
                var connection = Get("Connection") ?? Get("Proxy-Connection") ?? string.Empty;
                var tokens = connection.Split(',').Select(t => t.Trim());
                return Version == "HTTP/1.1"
                    ? !tokens.Any(t => t.Equals("close", StringComparison.OrdinalIgnoreCase))
                    : tokens.Any(t => t.Equals("keep-alive", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    /// <summary>A buffered HTTP/1.1 connection: request heads and bodies in, raw bytes out.</summary>
    internal sealed class ProxyConnection : Stream
    {
        private readonly Stream _inner;
        private readonly byte[] _buffer = new byte[16 * 1024];
        private int _start;
        private int _end;

        public ProxyConnection(Stream inner) => _inner = inner;

        /// <summary>The next request head; null at the end of the connection.</summary>
        public async Task<RequestHead?> ReadHeadAsync(CancellationToken token)
        {
            string? requestLine;
            do
            {
                requestLine = await ReadLineAsync(token).ConfigureAwait(false);
                if (requestLine == null)
                    return null;
            }
            while (requestLine.Length == 0); // tolerate blank lines between requests

            var parts = requestLine.Split(' ');
            if (parts.Length != 3 || !parts[2].StartsWith("HTTP/1.", StringComparison.Ordinal))
                throw new IOException("Malformed request line");

            var headers = new List<KeyValuePair<string, string>>();
            int total = requestLine.Length;
            while (true)
            {
                var line = await ReadLineAsync(token).ConfigureAwait(false) ?? throw new IOException("Connection closed in the request head");
                if (line.Length == 0)
                    break;
                total += line.Length;
                if (total > MaxHeadBytes)
                    throw new IOException("Request head too large");
                int colon = line.IndexOf(':');
                if (colon <= 0)
                    throw new IOException("Malformed header");
                headers.Add(new(line.Substring(0, colon).Trim(), line.Substring(colon + 1).Trim()));
            }
            return new RequestHead(parts[0], parts[1], parts[2], headers);
        }

        /// <summary>The request body (Content-Length or chunked); null when it is malformed.</summary>
        public async Task<byte[]?> ReadBodyAsync(RequestHead head, CancellationToken token)
        {
            var encoding = head.Get("Transfer-Encoding");
            if (encoding != null)
            {
                if (!encoding.Trim().Equals("chunked", StringComparison.OrdinalIgnoreCase))
                    return null;
                using var body = new MemoryStream();
                while (true)
                {
                    var sizeLine = await ReadLineAsync(token).ConfigureAwait(false);
                    if (sizeLine == null)
                        return null;
                    var size = sizeLine.Split(';')[0].Trim();
                    if (!int.TryParse(size, System.Globalization.NumberStyles.AllowHexSpecifier, null, out var count) || count < 0)
                        return null;
                    if (count == 0)
                    {
                        // Trailers, up to the blank line.
                        string? trailer;
                        while ((trailer = await ReadLineAsync(token).ConfigureAwait(false)) is { Length: > 0 }) { }
                        return body.ToArray();
                    }
                    var chunk = await ReadExactlyAsync(count, token).ConfigureAwait(false);
                    if (chunk == null)
                        return null;
                    body.Write(chunk);
                    if (await ReadLineAsync(token).ConfigureAwait(false) != string.Empty)
                        return null;
                }
            }

            var length = head.Get("Content-Length");
            if (length == null)
                return Array.Empty<byte>();
            if (!long.TryParse(length, System.Globalization.NumberStyles.None, null, out var bytes) || bytes > int.MaxValue)
                return null;
            return await ReadExactlyAsync((int)bytes, token).ConfigureAwait(false);
        }

        private async Task<byte[]?> ReadExactlyAsync(int count, CancellationToken token)
        {
            var result = new byte[count];
            int filled = 0;
            while (filled < count)
            {
                int read = await ReadAsync(result.AsMemory(filled), token).ConfigureAwait(false);
                if (read == 0)
                    return null;
                filled += read;
            }
            return result;
        }

        private async Task<string?> ReadLineAsync(CancellationToken token)
        {
            var line = new MemoryStream();
            while (true)
            {
                if (_start == _end)
                {
                    _start = 0;
                    _end = await _inner.ReadAsync(_buffer, token).ConfigureAwait(false);
                    if (_end == 0)
                        return line.Length == 0 ? null : throw new IOException("Connection closed mid-line");
                }
                int newline = Array.IndexOf(_buffer, (byte)'\n', _start, _end - _start);
                int stop = newline < 0 ? _end : newline;
                line.Write(_buffer, _start, stop - _start);
                if (line.Length > MaxHeadBytes)
                    throw new IOException("Line too long");
                if (newline < 0)
                {
                    _start = _end;
                    continue;
                }
                _start = newline + 1;
                var text = Encoding.Latin1.GetString(line.GetBuffer(), 0, (int)line.Length);
                return text.EndsWith('\r') ? text[..^1] : text;
            }
        }

        /// <summary>The next byte without consuming it; -1 at the end of the connection.</summary>
        public async Task<int> PeekByteAsync(CancellationToken token)
        {
            if (_start == _end)
            {
                _start = 0;
                _end = await _inner.ReadAsync(_buffer, token).ConfigureAwait(false);
                if (_end == 0)
                    return -1;
            }
            return _buffer[_start];
        }

        public Task WriteAsync(string text, CancellationToken token) =>
            _inner.WriteAsync(Encoding.Latin1.GetBytes(text), token).AsTask();

        public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
        {
            if (_start < _end)
            {
                int count = Math.Min(destination.Length, _end - _start);
                _buffer.AsMemory(_start, count).CopyTo(destination);
                _start += count;
                return count;
            }
            return await _inner.ReadAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default) => _inner.WriteAsync(source, cancellationToken);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.WriteAsync(buffer, offset, count, cancellationToken);

        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

        public override void Flush() => _inner.Flush();

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    /// <summary>A stream that also disposes the TCP client under it.</summary>
    private sealed class OwningStream : Stream
    {
        private readonly Stream _inner;
        private readonly IDisposable _owner;

        public OwningStream(Stream inner, IDisposable owner)
        {
            _inner = inner;
            _owner = owner;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default) => _inner.ReadAsync(destination, cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default) => _inner.WriteAsync(source, cancellationToken);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
        public override void Flush() => _inner.Flush();
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _owner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
