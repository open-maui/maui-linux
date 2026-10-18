// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Handlers;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// The loopback proxy behind HybridWebView's http and https WebResourceRequested, driven over
/// real loopback sockets as WebKit drives it (no WebKit, no network; the servers the app leaves
/// requests to are a fake handler). The live round trip with WebKit is the WebViewHost scenario
/// hybrid-web-resource-requested-http.
/// </summary>
public class HybridWebViewHttpProxyTests : IDisposable
{
    private sealed class FakeHost : IHybridWebViewHttpProxyHost
    {
        public List<string> Allowed { get; } = new();
        public List<HybridWebRequest> Offered { get; } = new();
        public Func<HybridWebRequest, HybridWebResponse?> Answer { get; set; } = _ => null;

        public Task AllowCertificateForHostAsync(string host)
        {
            lock (Allowed) Allowed.Add(host);
            return Task.CompletedTask;
        }

        public Task<HybridWebResponse?> InterceptAsync(HybridWebRequest request)
        {
            lock (Offered) Offered.Add(request);
            return Task.FromResult(Answer(request));
        }
    }

    private sealed class FakeUpstream : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Seen { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("upstream") };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (Seen) Seen.Add((request, body));
            return Respond(request);
        }
    }

    private static readonly X509Certificate2 s_certificate = HybridWebViewHttpProxy.CreateCertificate();

    private readonly FakeHost _host = new();
    private readonly FakeUpstream _upstream = new();
    private readonly HybridWebViewHttpProxy _proxy;

    public HybridWebViewHttpProxyTests()
    {
        _proxy = new HybridWebViewHttpProxy(_host, s_certificate, new HttpMessageInvoker(_upstream));
        _proxy.Start();
    }

    public void Dispose() => _proxy.Dispose();

    private string Credentials => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{HybridWebViewHttpProxy.UserName}:{_proxy.Password}"));

    private async Task<Stream> ConnectAsync()
    {
        var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _proxy.Port);
        return client.GetStream();
    }

    private static async Task SendAsync(Stream stream, string text)
    {
        await stream.WriteAsync(Encoding.Latin1.GetBytes(text));
        await stream.FlushAsync();
    }

    /// <summary>Reads one response: head, then a Content-Length or chunked body.</summary>
    private static async Task<(int Status, Dictionary<string, string> Headers, string Body)> ReadResponseAsync(Stream stream)
    {
        var line = await ReadLineAsync(stream);
        var status = int.Parse(line.Split(' ')[1]);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var header = await ReadLineAsync(stream); header.Length > 0; header = await ReadLineAsync(stream))
        {
            var colon = header.IndexOf(':');
            headers[header[..colon].Trim()] = header[(colon + 1)..].Trim();
        }
        var body = new MemoryStream();
        if (headers.TryGetValue("Content-Length", out var length))
        {
            var buffer = new byte[int.Parse(length)];
            await stream.ReadExactlyAsync(buffer);
            body.Write(buffer);
        }
        else if (headers.TryGetValue("Transfer-Encoding", out var encoding) && encoding == "chunked")
        {
            while (true)
            {
                var size = Convert.ToInt32(await ReadLineAsync(stream), 16);
                if (size == 0)
                {
                    await ReadLineAsync(stream);
                    break;
                }
                var buffer = new byte[size];
                await stream.ReadExactlyAsync(buffer);
                body.Write(buffer);
                await ReadLineAsync(stream);
            }
        }
        return (status, headers, Encoding.UTF8.GetString(body.ToArray()));
    }

    private static async Task<string> ReadLineAsync(Stream stream)
    {
        var bytes = new List<byte>();
        var one = new byte[1];
        while (await stream.ReadAsync(one) == 1)
        {
            if (one[0] == '\n')
                break;
            bytes.Add(one[0]);
        }
        return Encoding.Latin1.GetString(bytes.ToArray()).TrimEnd('\r');
    }

    [Fact]
    public void Listens_on_loopback_only_with_a_random_password()
    {
        _proxy.ProxyUri.Should().StartWith("http://127.0.0.1:");
        _proxy.Port.Should().BeGreaterThan(0);
        _proxy.Password.Length.Should().BeGreaterThanOrEqualTo(40, "32 random bytes, base64");
        _proxy.ProxyUri.Should().NotContain(_proxy.Password, "the password goes through WebKit's authenticate signal, not the proxy URI");

        using var other = new HybridWebViewHttpProxy(new FakeHost(), s_certificate);
        other.Password.Should().NotBe(_proxy.Password);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Basic b3Blbm1hdWk6d3Jvbmc=")] // openmaui:wrong
    [InlineData("Bearer anything")]
    public async Task A_client_without_the_password_gets_407_and_nothing_else(string? authorization)
    {
        await using var stream = await ConnectAsync();
        var header = authorization == null ? string.Empty : $"Proxy-Authorization: {authorization}\r\n";
        await SendAsync(stream, $"GET http://example.test/ HTTP/1.1\r\nHost: example.test\r\n{header}\r\n");

        var response = await ReadResponseAsync(stream);

        response.Status.Should().Be(407);
        response.Headers["Proxy-Authenticate"].Should().StartWith("Basic realm=");
        _host.Offered.Should().BeEmpty("an unauthenticated request never reaches the app");
        _upstream.Seen.Should().BeEmpty("nor the network");
    }

    [Fact]
    public async Task An_http_request_the_app_answers_gets_the_app_response()
    {
        _host.Answer = request => new HybridWebResponse(201, "Created",
            new List<KeyValuePair<string, string>> { new("Content-Type", "text/plain"), new("X-From", "app") },
            Encoding.UTF8.GetBytes("made by the app"));

        await using var stream = await ConnectAsync();
        await SendAsync(stream,
            "POST http://example.test/api?x=1 HTTP/1.1\r\nHost: example.test\r\n" +
            $"Proxy-Authorization: Basic {Credentials}\r\nProxy-Connection: keep-alive\r\n" +
            "Content-Type: text/plain\r\nX-Page: 7\r\nContent-Length: 5\r\n\r\nhello");

        var response = await ReadResponseAsync(stream);

        response.Status.Should().Be(201);
        response.Headers["X-From"].Should().Be("app");
        response.Body.Should().Be("made by the app");
        var offered = _host.Offered.Should().ContainSingle().Subject;
        offered.Method.Should().Be("POST");
        offered.Uri.Should().Be(new Uri("http://example.test/api?x=1"));
        offered.Headers["X-Page"].Should().Be("7");
        offered.Headers.Keys.Should().NotContain(new[] { "Proxy-Authorization", "Proxy-Connection" }, "proxy headers are WebKit's to the proxy, not the page's");
        Encoding.UTF8.GetString(offered.Body!).Should().Be("hello");
        _upstream.Seen.Should().BeEmpty("a request the app answers never goes out");
    }

    [Fact]
    public async Task An_http_request_the_app_leaves_alone_goes_out_unchanged_and_its_response_comes_back()
    {
        _upstream.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("no such page"))) };

        await using var stream = await ConnectAsync();
        await SendAsync(stream,
            "GET http://example.test/missing HTTP/1.1\r\nHost: example.test\r\n" +
            $"Proxy-Authorization: Basic {Credentials}\r\nCookie: sid=42\r\nAccept: text/html\r\n\r\n");

        var response = await ReadResponseAsync(stream);

        response.Status.Should().Be(404);
        response.Body.Should().Be("no such page");
        _host.Offered.Should().ContainSingle("the app is asked first");
        var (sent, _) = _upstream.Seen.Should().ContainSingle().Subject;
        sent.RequestUri.Should().Be(new Uri("http://example.test/missing"));
        sent.Method.Should().Be(HttpMethod.Get);
        sent.Headers.GetValues("Cookie").Should().Equal("sid=42");
        sent.Headers.Contains("Proxy-Authorization").Should().BeFalse("the proxy's password never leaves the machine");
    }

    [Fact]
    public async Task Requests_on_one_connection_are_served_in_turn()
    {
        _host.Answer = request => request.Uri.AbsolutePath == "/one"
            ? new HybridWebResponse(200, "OK", new List<KeyValuePair<string, string>>(), Encoding.UTF8.GetBytes("first"))
            : null;

        await using var stream = await ConnectAsync();
        await SendAsync(stream, $"GET http://example.test/one HTTP/1.1\r\nHost: example.test\r\nProxy-Authorization: Basic {Credentials}\r\n\r\n");
        (await ReadResponseAsync(stream)).Body.Should().Be("first");
        await SendAsync(stream, "GET http://example.test/two HTTP/1.1\r\nHost: example.test\r\n\r\n");
        (await ReadResponseAsync(stream)).Body.Should().Be("upstream");
    }

    [Fact]
    public async Task An_https_request_is_tunnelled_after_the_certificate_is_allowed_for_its_host_and_offered_with_its_https_uri()
    {
        _host.Answer = request => new HybridWebResponse(200, "OK",
            new List<KeyValuePair<string, string>> { new("Content-Type", "text/plain") }, Encoding.UTF8.GetBytes("secure answer"));

        await using var stream = await ConnectAsync();
        await SendAsync(stream, $"CONNECT secure.test:8443 HTTP/1.1\r\nHost: secure.test:8443\r\nProxy-Authorization: Basic {Credentials}\r\n\r\n");
        (await ReadLineAsync(stream)).Should().Be("HTTP/1.1 200 Connection established");
        (await ReadLineAsync(stream)).Should().BeEmpty();
        _host.Allowed.Should().Equal(new[] { "secure.test" }, "the view's network must accept the certificate before the handshake");

        X509Certificate? presented = null;
        await using var tls = new SslStream(stream, false, (_, certificate, _, _) =>
        {
            presented = certificate;
            return true;
        });
        await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = "secure.test",
            ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http2, SslApplicationProtocol.Http11 },
        });
        tls.NegotiatedApplicationProtocol.Should().Be(SslApplicationProtocol.Http11, "the proxy speaks HTTP/1.1 only");
        presented!.GetCertHashString().Should().Be(s_certificate.GetCertHashString());

        await SendAsync(tls, "GET /path?q=1 HTTP/1.1\r\nHost: secure.test:8443\r\n\r\n");
        var response = await ReadResponseAsync(tls);

        response.Status.Should().Be(200);
        response.Body.Should().Be("secure answer");
        _host.Offered.Should().ContainSingle().Which.Uri.Should().Be(new Uri("https://secure.test:8443/path?q=1"));
    }

    [Fact]
    public async Task An_unreachable_server_closes_the_connection_without_a_response()
    {
        _upstream.Respond = _ => throw new HttpRequestException("The SSL connection could not be established");

        await using var stream = await ConnectAsync();
        await SendAsync(stream, $"GET http://bad.test/ HTTP/1.1\r\nHost: bad.test\r\nProxy-Authorization: Basic {Credentials}\r\n\r\n");

        var rest = new byte[64];
        (await stream.ReadAsync(rest)).Should().Be(0, "WebKit then reports the load as failed, as it would without the proxy");
    }

    [Fact]
    public void The_upstream_handler_verifies_tls_and_adds_nothing()
    {
        using var handler = HybridWebViewHttpProxy.CreateUpstreamHandler();
        handler.SslOptions.RemoteCertificateValidationCallback.Should().BeNull("the servers' certificates are verified normally");
        handler.AllowAutoRedirect.Should().BeFalse();
        handler.UseCookies.Should().BeFalse();
        handler.AutomaticDecompression.Should().Be(DecompressionMethods.None);
        handler.ActivityHeadersPropagator.Should().BeNull();
    }

    [Fact]
    public void The_certificate_is_a_server_certificate_with_its_key()
    {
        s_certificate.HasPrivateKey.Should().BeTrue();
        s_certificate.Extensions.OfType<X509BasicConstraintsExtension>().Single().CertificateAuthority.Should().BeFalse("it can sign nothing");
        s_certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single().EnhancedKeyUsages[0]!.Value.Should().Be("1.3.6.1.5.5.7.3.1");
    }

    [Theory]
    [InlineData("example.test:443", true, "example.test", 443)]
    [InlineData("[::1]:8443", true, "::1", 8443)]
    [InlineData("127.0.0.1:80", true, "127.0.0.1", 80)]
    [InlineData("example.test", false, "", 0)]
    [InlineData("example.test:0", false, "", 0)]
    [InlineData("example.test:99999", false, "", 0)]
    [InlineData(":443", false, "", 0)]
    public void Connect_targets_are_split_strictly(string authority, bool ok, string host, int port)
    {
        HybridWebViewHttpProxy.TrySplitAuthority(authority, out var h, out var p).Should().Be(ok);
        if (ok)
        {
            h.Should().Be(host);
            p.Should().Be(port);
        }
    }

    [Fact]
    public void Interception_follows_the_apps_subscription()
    {
        var view = new HybridWebView();
        HybridWebViewHttpInterception.HasSubscribers(view).Should().BeFalse();
        EventHandler<WebViewWebResourceRequestedEventArgs> handler = (_, _) => { };
        view.WebResourceRequested += handler;
        HybridWebViewHttpInterception.HasSubscribers(view).Should().BeTrue();
        view.WebResourceRequested -= handler;
        HybridWebViewHttpInterception.HasSubscribers(view).Should().BeFalse();
        HybridWebViewHttpInterception.HasSubscribers(null).Should().BeFalse();
    }
}
