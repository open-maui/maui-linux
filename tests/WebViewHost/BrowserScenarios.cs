// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Views;
using SkiaSharp;

namespace Microsoft.Maui.Controls.Linux.Tests.WebViewHost;

/// <summary>ILinuxWebView on WPE: what a library (MarketAlly.ViewEngine) needs beyond MAUI's WebView.</summary>
public static partial class Program
{
    /// <summary>Serves fixed responses: status, content type, body and headers per path.</summary>
    private sealed class ResponseServer : IDisposable
    {
        public sealed record Reply(int Status, string ContentType, byte[] Body, Dictionary<string, string>? Headers = null);

        private readonly System.Net.HttpListener _listener = new();
        private readonly Dictionary<string, Reply> _replies;
        private readonly int _port;

        public ResponseServer(params (string Path, Reply Reply)[] replies)
        {
            _replies = replies.ToDictionary(r => r.Path, r => r.Reply, StringComparer.Ordinal);
            var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            probe.Start();
            _port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();
            _ = Task.Run(ServeAsync);
        }

        public string Url(string path) => $"http://127.0.0.1:{_port}{path}";

        public static Reply Html(string html, int status = 200) => new(status, "text/html; charset=utf-8", System.Text.Encoding.UTF8.GetBytes(html));

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                System.Net.HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch { return; }
                var reply = _replies.TryGetValue(ctx.Request.Url?.AbsolutePath ?? "/", out var r) ? r : Html("<html><body>not found</body></html>", 404);
                ctx.Response.StatusCode = reply.Status;
                ctx.Response.ContentType = reply.ContentType;
                ctx.Response.Headers["Cache-Control"] = "no-store";
                if (reply.Headers != null)
                    foreach (var (name, value) in reply.Headers)
                        ctx.Response.Headers[name] = value;
                ctx.Response.ContentLength64 = reply.Body.Length;
                try
                {
                    await ctx.Response.OutputStream.WriteAsync(reply.Body);
                    ctx.Response.Close();
                }
                catch
                {
                    // The client went away (a cancelled download).
                }
            }
        }

        public void Dispose()
        {
            try { _listener.Stop(); _listener.Close(); } catch { }
        }
    }

    private static WpeWebView NewView(int width = 400, int height = 300)
    {
        var view = new WpeWebView();
        view.Bounds = new Microsoft.Maui.Graphics.Rect(0, 0, width, height);
        return view;
    }

    private static byte[] SmallPdf()
    {
        using var stream = new MemoryStream();
        using (var document = SKDocument.CreatePdf(stream))
        {
            document.BeginPage(100, 100).Clear(SKColors.White);
            document.EndPage();
        }
        return stream.ToArray();
    }

    private static void BrowserStatusAndFailure()
    {
        using var server = new ResponseServer(("/ok", ResponseServer.Html("<html><body>ok</body></html>")),
            ("/missing", ResponseServer.Html("<html><body>gone</body></html>", 404)));
        using var view = NewView();
        ILinuxWebView browser = view;
        var finished = new List<LinuxWebNavigationFinishedEventArgs>();
        browser.NavigationFinished += (_, e) => finished.Add(e);

        browser.Navigate(server.Url("/ok"));
        Pump(() => finished.Count >= 1);
        Check(finished.Count >= 1 && finished[0].Success && finished[0].HttpStatusCode == 200, $"200 page: {Describe(finished)}");

        browser.Navigate(server.Url("/missing"));
        Pump(() => finished.Count >= 2);
        Check(finished.Count >= 2 && finished[1].Success && finished[1].HttpStatusCode == 404, $"a 404 page loads and reports 404: {Describe(finished)}");

        // Nothing listens on port 9 (discard) on loopback here: the connection is refused.
        browser.Navigate("http://127.0.0.1:9/");
        Pump(() => finished.Count >= 3);
        Check(finished.Count >= 3 && !finished[2].Success && !string.IsNullOrEmpty(finished[2].Error), $"a refused connection fails with an error: {Describe(finished)}");
        Pump(() => false, 300);
        Check(finished.Count == 3, $"a failed load is reported once: {Describe(finished)}");
    }

    private static string Describe(List<LinuxWebNavigationFinishedEventArgs> list) =>
        string.Join("; ", list.Select(e => $"{e.Url} success={e.Success} status={e.HttpStatusCode} error={e.Error}"));

    private static void BrowserResponsePolicy()
    {
        using var server = new ResponseServer(("/doc.pdf", new ResponseServer.Reply(200, "application/pdf", SmallPdf())));
        using var view = NewView();
        ILinuxWebView browser = view;
        LinuxWebResponseEventArgs? seen = null;
        browser.ResponseReceived += (_, e) =>
        {
            if (!e.Url.EndsWith("/doc.pdf")) return;
            seen = e;
            e.Action = LinuxWebResponseAction.Ignore; // the app shows PDFs itself
        };
        var downloads = 0;
        browser.DownloadStarting += (_, _) => downloads++;

        browser.Navigate(server.Url("/doc.pdf"));
        Pump(() => seen != null);
        Check(seen != null, "ResponseReceived was not raised for the PDF");
        Check(seen!.IsMainFrame, "the PDF is the main frame's document");
        Check(seen.MimeType == "application/pdf", $"MIME type was '{seen.MimeType}'");
        Check(seen.HttpStatusCode == 200, $"status was {seen.HttpStatusCode}");
        Pump(() => false, 500);
        Check(downloads == 0, "an ignored response is not downloaded");
    }

    private static void BrowserDownloadIntercept()
    {
        var payload = System.Text.Encoding.UTF8.GetBytes("citation export");
        using var server = new ResponseServer(("/export.bin", new ResponseServer.Reply(200, "application/octet-stream", payload,
            new Dictionary<string, string> { ["Content-Disposition"] = "attachment; filename=\"export.bin\"" })));
        using var view = NewView();
        ILinuxWebView browser = view;
        var destination = Path.Combine(Path.GetTempPath(), $"openmaui-download-{Guid.NewGuid():N}.bin");
        string? started = null;
        browser.DownloadStarting += (_, e) =>
        {
            started = e.Url;
            e.DestinationPath = destination;
        };
        LinuxWebDownloadFinishedEventArgs? finished = null;
        browser.DownloadFinished += (_, e) => finished = e;

        browser.Navigate(server.Url("/export.bin"));
        Pump(() => finished != null, 10000);
        try
        {
            Check(started != null && started.EndsWith("/export.bin"), $"DownloadStarting was not raised (url '{started}')");
            Check(finished != null && finished.Success, $"DownloadFinished did not report success ({finished?.Error})");
            Check(finished!.Path == destination, $"DownloadFinished reported '{finished.Path}'");
            Check(File.Exists(destination), "the download was not written where the handler chose");
            Check(File.ReadAllBytes(destination).SequenceEqual(payload), "the download's content differs");
        }
        finally
        {
            File.Delete(destination);
        }
    }

    private static void BrowserNewWindowInPlace()
    {
        using var server = new ResponseServer(
            ("/one", ResponseServer.Html("<html><body><a id='a' href='/two' target='_blank'>open</a></body></html>")),
            ("/two", ResponseServer.Html("<html><body>two</body></html>")));
        using var view = NewView();
        ILinuxWebView browser = view;
        var loaded = new List<string>();
        browser.NavigationFinished += (_, e) => { if (e.Success) loaded.Add(e.Url); };
        LinuxWebNavigationStartingEventArgs? newWindow = null;
        browser.NavigationStarting += (_, e) => { if (e.IsNewWindow) newWindow = e; };

        browser.Navigate(server.Url("/one"));
        Pump(() => loaded.Count >= 1);
        _ = browser.EvaluateJavaScriptAsync("document.getElementById('a').click(); 'clicked'");
        Pump(() => loaded.Any(u => u.EndsWith("/two")));
        Check(newWindow != null && newWindow.Url.EndsWith("/two"), "the target=_blank click was not reported as a new window");
        Check(browser.Url?.EndsWith("/two") == true, $"the new window's page did not load in place (url '{browser.Url}')");
    }

    private static void BrowserUrlChanged()
    {
        using var server = new ResponseServer(("/page", ResponseServer.Html("<html><body>spa</body></html>")));
        using var view = NewView();
        ILinuxWebView browser = view;
        bool loaded = false;
        browser.NavigationFinished += (_, _) => loaded = true;
        var urls = new List<string>();
        browser.UrlChanged += (_, url) => urls.Add(url);

        browser.Navigate(server.Url("/page"));
        Pump(() => loaded);
        Await(browser.EvaluateJavaScriptAsync("history.pushState({}, '', '/page/section'); 'pushed'"));
        Pump(() => urls.Any(u => u.EndsWith("/page/section")), 3000);
        Check(urls.Any(u => u.EndsWith("/page/section")), $"UrlChanged did not report the pushed URL: {string.Join(", ", urls)}");
    }

    private static void BrowserZoomRoundTrip()
    {
        using var view = NewView();
        ILinuxWebView browser = view;
        browser.ZoomLevel = 1.5;
        Check(Math.Abs(browser.ZoomLevel - 1.5) < 0.001, $"zoom was {browser.ZoomLevel}");
        browser.ZoomLevel = 1.0;
        Check(Math.Abs(browser.ZoomLevel - 1.0) < 0.001, $"zoom was {browser.ZoomLevel}");
    }

    private const string TallPage = "<html><body style='margin:0'>"
        + "<div style='height:1000px;background:#ff0000'></div>"
        + "<div style='height:1000px;background:#00ff00'></div>"
        + "<div style='height:1000px;background:#0000ff'></div>"
        + "</body></html>";

    private static void BrowserCapture()
    {
        using var view = NewView(400, 300);
        ILinuxWebView browser = view;
        bool loaded = false;
        browser.NavigationFinished += (_, _) => loaded = true;
        browser.LoadHtml(TallPage, "app://localhost/");
        Pump(() => loaded);
        Pump(() => false, 400); // the first frame

        using var visible = Await(browser.CaptureAsync());
        Check(visible != null, "the visible capture returned nothing");
        Check(visible!.Height < 1000, $"the visible capture is the view, was {visible.Width}x{visible.Height}");
        var top = visible.GetPixel(visible.Width / 2, visible.Height / 2);
        Check(top.Red > 200 && top.Green < 60, $"the view shows the red band, was {top}");

        using var whole = Await(browser.CaptureAsync(fullDocument: true));
        Check(whole != null, "the whole-document capture returned nothing");
        Check(whole!.Height >= 3000 * whole.Width / 400 - 2, $"the whole document is 3000 CSS px tall, captured {whole.Width}x{whole.Height}");
        var bottom = whole.GetPixel(whole.Width / 2, whole.Height - 20);
        Check(bottom.Blue > 200 && bottom.Red < 60, $"the bottom of the document is the blue band, was {bottom}");
    }

    private static void BrowserSaveAsPdf()
    {
        using var view = NewView(400, 300);
        ILinuxWebView browser = view;
        bool loaded = false;
        browser.NavigationFinished += (_, _) => loaded = true;
        browser.LoadHtml(TallPage, "app://localhost/");
        Pump(() => loaded);
        Pump(() => false, 400);

        var path = Path.Combine(Path.GetTempPath(), $"openmaui-page-{Guid.NewGuid():N}.pdf");
        try
        {
            Check(Await(browser.SaveAsPdfAsync(path)), "SaveAsPdfAsync returned false");
            var bytes = File.ReadAllBytes(path);
            Check(bytes.Length > 100 && System.Text.Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-", "the file is not a PDF");
            int pages = System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.Latin1.GetString(bytes), @"/Type\s*/Page\b").Count;
            Check(pages >= 2, $"a 3000 px page on A4 is more than one page, found {pages}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void BrowserZoomGestures()
    {
        using var view = NewView();
        ILinuxWebView browser = view;
        bool loaded = false;
        browser.NavigationFinished += (_, _) => loaded = true;
        browser.LoadHtml("<html><body>zoom</body></html>", "app://localhost/");
        Pump(() => loaded);
        Check(browser.ZoomGesturesEnabled, "zoom gestures are on by default");

        const string wheelIn = "window.dispatchEvent(new WheelEvent('wheel', {deltaY: -100, ctrlKey: true, cancelable: true})); 'ok'";
        Await(browser.EvaluateJavaScriptAsync(wheelIn));
        Pump(() => browser.ZoomLevel > 1.05, 3000);
        Check(Math.Abs(browser.ZoomLevel - 1.1) < 0.001, $"Ctrl+wheel up zooms in one step, zoom is {browser.ZoomLevel}");

        Await(browser.EvaluateJavaScriptAsync("window.dispatchEvent(new KeyboardEvent('keydown', {key: '0', ctrlKey: true})); 'ok'"));
        Pump(() => Math.Abs(browser.ZoomLevel - 1.0) < 0.001, 3000);
        Check(Math.Abs(browser.ZoomLevel - 1.0) < 0.001, $"Ctrl+0 resets the zoom, zoom is {browser.ZoomLevel}");

        browser.ZoomGesturesEnabled = false;
        Await(browser.EvaluateJavaScriptAsync(wheelIn));
        Pump(() => false, 400);
        Check(Math.Abs(browser.ZoomLevel - 1.0) < 0.001, $"disabled, Ctrl+wheel does not zoom, zoom is {browser.ZoomLevel}");
    }
}
