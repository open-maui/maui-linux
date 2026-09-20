// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.ExceptionServices;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Views;
using SkiaSharp;

namespace Microsoft.Maui.Controls.Linux.Tests.WebViewHost;

/// <summary>
/// Runs one named WebView scenario on the process main thread against the
/// WPE engine (headless, no display needed). Exit code 0 = passed, 1 = a
/// check failed (message on stderr), 2 = unknown scenario, 3 = WPE not
/// installed. <c>--list</c> prints the scenario names.
/// </summary>
public static class Program
{
    private static readonly Dictionary<string, Action> s_scenarios = new(StringComparer.Ordinal)
    {
        ["html-source-navigation-events"] = HtmlSourceNavigationEvents,
        ["javascript-evaluation"] = JavaScriptEvaluation,
        ["javascript-mutation-and-eval"] = JavaScriptMutationAndEval,
        ["javascript-non-string-and-errors"] = JavaScriptNonStringAndErrors,
        ["navigation-decision-cancel"] = NavigationDecisionCancel,
        ["history-can-go-back"] = HistoryCanGoBack,
        ["user-agent-round-trip"] = UserAgentRoundTrip,
        ["frames-delivered-after-load"] = FramesDeliveredAfterLoad,
        ["cookies-round-trip"] = CookiesRoundTrip,
        ["reload-and-stop"] = ReloadAndStop,
    };

    public static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--list")
        {
            foreach (var name in s_scenarios.Keys)
                Console.WriteLine(name);
            return 0;
        }

        if (args.Length != 1 || !s_scenarios.TryGetValue(args[0], out var scenario))
        {
            Console.Error.WriteLine($"usage: WebViewHost <scenario>|--list (unknown: {string.Join(' ', args)})");
            return 2;
        }

        if (!WpeWebView.IsSupported)
        {
            Console.Error.WriteLine("WPE WebKit (libWPEWebKit-2.0) is not installed");
            return 3;
        }

        try
        {
            scenario();
            Console.WriteLine($"ok {args[0]}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL {args[0]}: {ex.Message}");
            return 1;
        }
    }

    // ---- scenarios ------------------------------------------------------

    private static void HtmlSourceNavigationEvents()
    {
        string? started = null; (string Url, bool Success)? completed = null;
        using var view = new WpeWebView();
        view.NavigationStarted += (_, u) => started = u;
        view.NavigationCompleted += (_, e) => completed = e;
        view.Bounds = new Microsoft.Maui.Graphics.Rect(0, 0, 400, 300);
        view.LoadHtml("<html><head><title>Hello WPE</title></head><body>hi</body></html>", "app://localhost/");

        Pump(() => completed != null);
        Check(completed != null, "NavigationCompleted was not raised");
        Check(completed!.Value.Success, "navigation did not succeed");
        Check(started != null, "NavigationStarted was not raised");
        Check(view.Title == "Hello WPE", $"title was '{view.Title}'");
    }

    private static void JavaScriptEvaluation()
    {
        using var view = Load("<html><body><div id='x'>forty-two</div></body></html>", out var loaded);
        Pump(loaded);

        var text = Await(view.EvaluateJavaScriptAsync("document.getElementById('x').textContent + '!'"));
        Check(text == "forty-two!", $"textContent evaluation returned '{text}'");

        var arith = Await(view.EvaluateJavaScriptAsync("String(6 * 7)"));
        Check(arith == "42", $"arithmetic evaluation returned '{arith}'");
    }

    private static void JavaScriptMutationAndEval()
    {
        using var view = Load("<html><body><p id='p'>before</p></body></html>", out var loaded);
        Pump(loaded);

        view.Eval("document.getElementById('p').textContent = 'after'");
        var read = Await(view.EvaluateJavaScriptAsync("document.getElementById('p').textContent"));
        Check(read == "after", $"Eval did not mutate the document (read '{read}')");
    }

    private static void JavaScriptNonStringAndErrors()
    {
        using var view = Load("<html><body></body></html>", out var loaded);
        Pump(loaded);

        var obj = Await(view.EvaluateJavaScriptAsync("({a:1})"));
        Check(obj == null, $"object result should be null, was '{obj}'");

        var err = Await(view.EvaluateJavaScriptAsync("throw new Error('nope')"));
        Check(err == null, $"thrown error should yield null, was '{err}'");
    }

    private static void NavigationDecisionCancel()
    {
        using var view = Load("<html><body>first</body></html>", out var loaded);
        Pump(loaded);

        bool asked = false;
        view.NavigationDecision += (_, e) => { asked = true; e.Cancel = e.Url.Contains("blocked"); };
        int completions = 0;
        view.NavigationCompleted += (_, _) => completions++;

        view.Navigate("app://localhost/blocked");
        Pump(() => asked, 3000);
        Check(asked, "NavigationDecision was not raised");
        Pump(() => false, 300);
        Check(completions == 0, $"cancelled navigation still completed {completions} time(s)");
    }

    private static void HistoryCanGoBack()
    {
        using var server = new LocalServer(("/one", "<html><body>one</body></html>"), ("/two", "<html><body>two</body></html>"));
        using var view = new WpeWebView();
        view.Bounds = new Microsoft.Maui.Graphics.Rect(0, 0, 400, 300);
        int completions = 0;
        view.NavigationCompleted += (_, e) => { if (e.Success) completions++; };

        view.Navigate(server.Url("/one"));
        Pump(() => completions >= 1);
        Check(completions >= 1, "first navigation did not complete");
        Check(!view.CanGoBack, "CanGoBack should be false after the first load");

        view.Navigate(server.Url("/two"));
        Pump(() => completions >= 2);
        Check(completions >= 2, "second navigation did not complete");
        Check(view.CanGoBack, "CanGoBack should be true after two loads");
        Check(!view.CanGoForward, "CanGoForward should be false at the newest entry");
        Check(view.CurrentUri != null && view.CurrentUri.EndsWith("/two"), $"Url should report the current page, was '{view.CurrentUri}'");

        view.GoBack();
        Pump(() => completions >= 3);
        Check(completions >= 3, "GoBack did not complete");
        Check(view.CanGoForward, "CanGoForward should be true after GoBack");
        Check(view.CurrentUri != null && view.CurrentUri.EndsWith("/one"), $"GoBack should land on /one, was '{view.CurrentUri}'");
    }

    private static void UserAgentRoundTrip()
    {
        using var view = new WpeWebView();
        view.UserAgent = "OpenMauiTest/1.0";
        Check(view.UserAgent == "OpenMauiTest/1.0", $"UserAgent read back '{view.UserAgent}'");
    }

    private static void FramesDeliveredAfterLoad()
    {
        using var view = Load("<html><body style='background:#ff0000'></body></html>", out var loaded);
        Pump(loaded);
        // The compositor delivers the first buffer shortly after load.
        Pump(() => false, 300);

        using var bitmap = new SKBitmap(new SKImageInfo(200, 150, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            view.Bounds = new Microsoft.Maui.Graphics.Rect(0, 0, 200, 150);
            view.Draw(canvas);
        }
        var c = bitmap.GetPixel(100, 75);
        Check(c.Red > 200 && c.Green < 60 && c.Blue < 60, $"expected a red frame, centre pixel was {c}");
    }

    private static void CookiesRoundTrip()
    {
        // Cookies need an http(s) origin; app:// and data: loads have no jar.
        using var server = new LocalServer(("/", "<html><body>cookies</body></html>"));
        using var view = new WpeWebView();
        view.Bounds = new Microsoft.Maui.Graphics.Rect(0, 0, 400, 300);
        bool loaded = false;
        view.NavigationCompleted += (_, e) => loaded = e.Success;
        view.Navigate(server.Url("/"));
        Pump(() => loaded);
        Check(loaded, "page did not load");

        view.Eval("document.cookie = 'flavour=oatmeal'");
        var read = Await(view.EvaluateJavaScriptAsync("document.cookie"));
        Check(read != null && read.Contains("flavour=oatmeal"), $"cookie did not round-trip (read '{read}')");

        // A second request from the same origin carries the cookie back to the server.
        view.Navigate(server.Url("/"));
        Pump(() => server.RequestCount >= 2);
        Check(server.LastCookieHeader?.Contains("flavour=oatmeal") == true, $"server saw Cookie header '{server.LastCookieHeader}'");
    }

    private static void ReloadAndStop()
    {
        using var server = new LocalServer(("/", "<html><body><p id='p'>original</p></body></html>"));
        using var view = new WpeWebView();
        view.Bounds = new Microsoft.Maui.Graphics.Rect(0, 0, 400, 300);
        int completions = 0;
        view.NavigationCompleted += (_, e) => { if (e.Success) completions++; };
        view.Navigate(server.Url("/"));
        Pump(() => completions >= 1);
        Check(completions >= 1, "page did not load");

        view.Eval("document.getElementById('p').textContent = 'changed'");
        Check(Await(view.EvaluateJavaScriptAsync("document.getElementById('p').textContent")) == "changed", "mutation before reload");

        view.Reload();
        Pump(() => completions >= 2);
        Check(completions >= 2, "Reload did not complete");
        Check(server.RequestCount >= 2, "Reload did not hit the server again");
        var after = Await(view.EvaluateJavaScriptAsync("document.getElementById('p').textContent"));
        Check(after == "original", $"Reload should restore the document (read '{after}')");

        // StopLoading on an idle view must not throw or fire a completion.
        int before = completions;
        view.StopLoading();
        Pump(() => false, 100);
        Check(completions == before, "StopLoading on an idle view raised NavigationCompleted");
    }

    /// <summary>Tiny HTTP server so http-only behaviour (cookies, history, reload) is exercised for real.</summary>
    private sealed class LocalServer : IDisposable
    {
        private readonly System.Net.HttpListener _listener = new();
        private readonly Dictionary<string, string> _pages;
        private readonly int _port;
        private int _requests;

        public LocalServer(params (string Path, string Html)[] pages)
        {
            _pages = pages.ToDictionary(p => p.Path, p => p.Html, StringComparer.Ordinal);
            _port = FreePort();
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();
            _ = Task.Run(ServeAsync);
        }

        public int RequestCount => Volatile.Read(ref _requests);
        public string? LastCookieHeader { get; private set; }
        public string Url(string path) => $"http://127.0.0.1:{_port}{path}";

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                System.Net.HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch { return; }

                LastCookieHeader = ctx.Request.Headers["Cookie"];
                Interlocked.Increment(ref _requests);
                var html = _pages.TryGetValue(ctx.Request.Url?.AbsolutePath ?? "/", out var page) ? page : null;
                ctx.Response.StatusCode = html == null ? 404 : 200;
                ctx.Response.ContentType = "text/html; charset=utf-8";
                ctx.Response.Headers["Cache-Control"] = "no-store";
                var bytes = System.Text.Encoding.UTF8.GetBytes(html ?? "<html><body>not found</body></html>");
                ctx.Response.ContentLength64 = bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
            }
        }

        private static int FreePort()
        {
            var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            probe.Start();
            int port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        public void Dispose()
        {
            try { _listener.Stop(); _listener.Close(); } catch { }
        }
    }

    // ---- helpers --------------------------------------------------------

    private static WpeWebView Load(string html, out Func<bool> loaded)
    {
        var view = new WpeWebView();
        bool done = false;
        view.NavigationCompleted += (_, e) => done = true;
        view.Bounds = new Microsoft.Maui.Graphics.Rect(0, 0, 400, 300);
        view.LoadHtml(html, "app://localhost/");
        loaded = () => done;
        return view;
    }

    private static void Pump(Func<bool> until, int maxMs = 8000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxMs);
        while (!until() && DateTime.UtcNow < deadline)
        {
            GLibNative.ProcessPendingEvents(50);
            Thread.Sleep(5);
        }
    }

    private static T Await<T>(Task<T> task)
    {
        Pump(() => task.IsCompleted);
        Check(task.IsCompleted, "asynchronous operation did not complete in time");
        if (task.IsFaulted)
            ExceptionDispatchInfo.Capture(task.Exception!.GetBaseException()).Throw();
        return task.Result;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
