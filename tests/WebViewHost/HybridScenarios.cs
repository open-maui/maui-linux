// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Views;

namespace Microsoft.Maui.Controls.Linux.Tests.WebViewHost;

/// <summary>
/// HybridWebView end to end on WPE, through <see cref="LinuxHybridWebViewHandler"/>: the page
/// served from app://0.0.0.1/ out of the HybridRoot assets with MAUI's own hybridwebview.js,
/// raw messages both ways, InvokeJavaScriptAsync, InvokeDotNet (a fetch POST to the app scheme),
/// EvaluateJavaScriptAsync and WebResourceRequested.
/// </summary>
public static partial class Program
{
    private const string HybridIndex = """
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="utf-8" />
          <title>Hybrid</title>
          <script src="_framework/hybridwebview.js"></script>
          <script>
            window.addEventListener('HybridWebViewMessageReceived', function (e) {
              window.HybridWebView.SendRawMessage('echo:' + e.detail.message);
            });
            function add(a, b) { return a + b; }
            async function later(x) { await new Promise(function (r) { setTimeout(r, 10); }); return { value: x, doubled: x * 2 }; }
            function boom() { throw new Error('kaput'); }
            window.addEventListener('load', function () { window.HybridWebView.SendRawMessage('ready ' + location.href); });
          </script>
        </head>
        <body><p id="p">hybrid</p></body>
        </html>
        """;

    /// <summary>Runs posted continuations on the main thread while <see cref="Pump"/> waits.</summary>
    private sealed class MainLoopContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

        public void Drain()
        {
            while (_queue.TryDequeue(out var item))
                item.Callback(item.State);
        }
    }

    private static MainLoopContext? s_mainLoopContext;

    private static void DrainMainLoopContext() => s_mainLoopContext?.Drain();

    public sealed class HybridTarget
    {
        public int Add(int a, int b) => a + b;

        public async Task<string> EchoAsync(string text)
        {
            await Task.Delay(5);
            return "echo " + text;
        }

        public string[] Pair(string a, string b) => new[] { a, b };

        public void Fail() => throw new InvalidOperationException("dotnet failed");
    }

    /// <summary>A HybridWebView whose page has loaded and said "ready"; the messages it sends are collected.</summary>
    private sealed class HybridFixture : IDisposable
    {
        private readonly string _rootDirectory;

        public HybridFixture()
        {
            if (s_mainLoopContext == null)
            {
                s_mainLoopContext = new MainLoopContext();
                SynchronizationContext.SetSynchronizationContext(s_mainLoopContext);
            }
            EssentialsPatches.Apply(); // FileSystem.OpenAppPackageFileAsync reads next to the app, as in an app

            RootName = "hybrid_test_" + Guid.NewGuid().ToString("N")[..8];
            _rootDirectory = Path.Combine(AppContext.BaseDirectory, RootName);
            Directory.CreateDirectory(Path.Combine(_rootDirectory, "css"));
            File.WriteAllText(Path.Combine(_rootDirectory, "index.html"), HybridIndex);
            File.WriteAllText(Path.Combine(_rootDirectory, "css", "site.css"), "p { color: red; }");

            View = new HybridWebView { HybridRoot = RootName };
            View.RawMessageReceived += (_, e) => Messages.Enqueue(e.Message ?? string.Empty);
            View.WebViewInitializing += (_, _) => Initializing++;
            View.WebViewInitialized += (_, _) => Initialized++;

            Handler = new LinuxHybridWebViewHandler();
            Handler.SetMauiContext(new MauiContext(new ServiceCollection().BuildServiceProvider()));
            View.Handler = Handler;
            Browser = Handler.Browser ?? throw new InvalidOperationException("the handler has no browser");
            ((Microsoft.Maui.Platform.SkiaView)Handler.PlatformView!).Bounds = new Microsoft.Maui.Graphics.Rect(0, 0, 400, 300);

            var ready = WaitForMessage(m => m.StartsWith("ready ", StringComparison.Ordinal));
            Check(ready != null, $"the page did not load (messages: {string.Join(" | ", Messages)}; url {Browser.Url})");
            ReadyMessage = ready!;
        }

        public string RootName { get; }
        public HybridWebView View { get; }
        public LinuxHybridWebViewHandler Handler { get; }
        public ILinuxWebView Browser { get; }
        public string ReadyMessage { get; }
        public int Initializing { get; private set; }
        public int Initialized { get; private set; }
        public ConcurrentQueue<string> Messages { get; } = new();

        public string? WaitForMessage(Func<string, bool> match, int maxMs = 8000)
        {
            string? found = null;
            Pump(() => (found = Messages.FirstOrDefault(match)) != null, maxMs);
            return found;
        }

        public void Dispose()
        {
            View.Handler = null;
            try { Directory.Delete(_rootDirectory, recursive: true); } catch { }
        }
    }

    private static JsonTypeInfo<T> TypeInfo<T>() => (JsonTypeInfo<T>)JsonSerializerOptions.Default.GetTypeInfo(typeof(T));

    private static void HybridServesAppAndRawMessages()
    {
        using var hybrid = new HybridFixture();
        Check(hybrid.ReadyMessage == "ready app://0.0.0.1/", $"the page should load from the app origin, said '{hybrid.ReadyMessage}'");
        Check(hybrid.Initializing == 1 && hybrid.Initialized == 1, $"WebViewInitializing/WebViewInitialized were raised {hybrid.Initializing}/{hybrid.Initialized} times");
        Pump(() => hybrid.Browser.Title == "Hybrid", 3000);
        Check(hybrid.Browser.Title == "Hybrid", $"title was '{hybrid.Browser.Title}'");

        // .NET -> JS -> .NET, with characters that need escaping.
        const string text = "ping \"quoted\" line\nbreak é 日本";
        hybrid.View.SendRawMessage(text);
        var echo = hybrid.WaitForMessage(m => m.StartsWith("echo:", StringComparison.Ordinal));
        Check(echo == "echo:" + text, $"raw message round trip returned '{echo}'");

        // Static assets from HybridRoot, with their content type; unknown files are 404.
        var css = Await(hybrid.View.EvaluateJavaScriptAsync(
            "fetch('/css/site.css').then(function (r) { return r.text().then(function (t) { window.HybridWebView.SendRawMessage('css:' + r.status + ':' + r.headers.get('content-type') + ':' + t); }); }); 'started'"));
        Check(css == "started", $"EvaluateJavaScriptAsync returned '{css}'");
        var cssMessage = hybrid.WaitForMessage(m => m.StartsWith("css:", StringComparison.Ordinal));
        Check(cssMessage == "css:200:text/css:p { color: red; }", $"css asset fetch said '{cssMessage}'");

        Await(hybrid.View.EvaluateJavaScriptAsync(
            "fetch('/missing.js').then(function (r) { window.HybridWebView.SendRawMessage('missing:' + r.status); }); 0"));
        var missing = hybrid.WaitForMessage(m => m.StartsWith("missing:", StringComparison.Ordinal));
        Check(missing == "missing:404", $"a missing asset said '{missing}'");
    }

    private static void HybridInvokeJavaScript()
    {
        using var hybrid = new HybridFixture();

        var sum = Await(hybrid.View.InvokeJavaScriptAsync<int>("add", TypeInfo<int>(), new object?[] { 2, 40 }, new JsonTypeInfo?[] { TypeInfo<int>(), TypeInfo<int>() }));
        Check(sum == 42, $"add(2, 40) returned {sum}");

        var later = Await(hybrid.View.InvokeJavaScriptAsync<Dictionary<string, int>>("later", TypeInfo<Dictionary<string, int>>(), new object?[] { 21 }, new JsonTypeInfo?[] { TypeInfo<int>() }));
        Check(later != null && later["value"] == 21 && later["doubled"] == 42, $"later(21) returned {JsonSerializer.Serialize(later)}");

        Exception? failure = null;
        try
        {
            Await(hybrid.View.InvokeJavaScriptAsync<int>("boom", TypeInfo<int>()));
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        Check(failure != null && failure.Message.Contains("kaput", StringComparison.Ordinal), $"boom() should fail with its message, got {failure?.GetType().Name}: {failure?.Message}");
        Check(failure!.GetType().Name == "HybridWebViewInvokeJavaScriptException", $"boom() should throw MAUI's exception type, got {failure.GetType().FullName}");

        var three = Await(hybrid.View.EvaluateJavaScriptAsync("1 + 2"));
        Check(three == "3", $"EvaluateJavaScriptAsync('1 + 2') returned '{three}'");
        var str = Await(hybrid.View.EvaluateJavaScriptAsync("document.getElementById('p').textContent"));
        Check(str == "hybrid", $"EvaluateJavaScriptAsync of a string returned '{str}'");
        var none = Await(hybrid.View.EvaluateJavaScriptAsync("undefinedFunction()"));
        Check(none == null, $"a failing script should give null, gave '{none}'");
    }

    private static void HybridInvokeDotNet()
    {
        using var hybrid = new HybridFixture();
        hybrid.View.SetInvokeJavaScriptTarget(new HybridTarget());

        Await(hybrid.View.EvaluateJavaScriptAsync("""
            (async function () {
              var r = [];
              r.push('add=' + await window.HybridWebView.InvokeDotNet('Add', [2, 3]));
              r.push('echo=' + await window.HybridWebView.InvokeDotNet('EchoAsync', 'hi'));
              r.push('pair=' + JSON.stringify(await window.HybridWebView.InvokeDotNet('Pair', ['a', 'b'])));
              try { await window.HybridWebView.InvokeDotNet('Fail'); r.push('fail=none'); }
              catch (e) { r.push('fail=' + e.message + '/' + e.dotNetErrorType); }
              window.HybridWebView.SendRawMessage('dotnet:' + r.join(';'));
            })(); 0
            """));
        var result = hybrid.WaitForMessage(m => m.StartsWith("dotnet:", StringComparison.Ordinal));
        Check(result == "dotnet:add=5;echo=echo hi;pair=[\"a\",\"b\"];fail=dotnet failed/InvalidOperationException", $"InvokeDotNet said '{result}'");

        // The invoker only answers MAUI's own script (token header, app origin, POST).
        Await(hybrid.View.EvaluateJavaScriptAsync(
            "fetch('/__hwvInvokeDotNet', { method: 'POST', body: '{}' }).then(function (r) { window.HybridWebView.SendRawMessage('forged:' + r.status); }); 0"));
        var forged = hybrid.WaitForMessage(m => m.StartsWith("forged:", StringComparison.Ordinal));
        Check(forged == "forged:400", $"a request without MAUI's token said '{forged}'");
    }

    private static void HybridWebResourceRequested()
    {
        using var hybrid = new HybridFixture();
        string? seenMethod = null, seenQuery = null, seenAccept = null;
        hybrid.View.WebResourceRequested += (_, e) =>
        {
            if (e.Uri.AbsolutePath == "/api/data")
            {
                seenMethod = e.Method;
                seenQuery = e.QueryParameters.TryGetValue("x", out var x) ? x : null;
                seenAccept = e.Headers.TryGetValue("Accept", out var accept) ? accept : null;
                e.Handled = true;
                e.SetResponse(200, "OK", "application/json", new MemoryStream(Encoding.UTF8.GetBytes("{\"v\":42}")));
            }
            else if (e.Uri.AbsolutePath == "/api/later")
            {
                e.Handled = true;
                e.SetResponse(201, "Created", new Dictionary<string, string> { ["Content-Type"] = "text/plain", ["X-Custom"] = "yes" },
                    Task.Run<Stream?>(async () => { await Task.Delay(20); return new MemoryStream(Encoding.UTF8.GetBytes("made")); }));
            }
        };

        Await(hybrid.View.EvaluateJavaScriptAsync("""
            (async function () {
              var a = await fetch('/api/data?x=1', { headers: { 'Accept': 'application/json' } });
              var j = await a.json();
              var b = await fetch('/api/later');
              var t = await b.text();
              window.HybridWebView.SendRawMessage('intercepted:' + j.v + ':' + b.status + ':' + b.headers.get('x-custom') + ':' + t);
            })(); 0
            """));
        var result = hybrid.WaitForMessage(m => m.StartsWith("intercepted:", StringComparison.Ordinal));
        Check(result == "intercepted:42:201:yes:made", $"intercepted requests said '{result}'");
        Check(seenMethod == "GET", $"Method was '{seenMethod}'");
        Check(seenQuery == "1", $"QueryParameters[x] was '{seenQuery}'");
        Check(seenAccept == "application/json", $"Headers[Accept] was '{seenAccept}'");
    }
    private static void SchemeFallbackAndPerViewRouting()
    {
        // BlazorWebView's process-wide handler and a HybridWebView's per-view one share "app".
        using var shared = NewView();
        using var own = NewView();
        var api = shared.Content;
        var fallbackRegistered = api.RegisterUriScheme(shared.NativeWebView, "app", (_, uri) =>
            new Microsoft.Maui.Platform.Linux.Native.WebKitContentApi.SchemeResponse(200, "OK", Encoding.UTF8.GetBytes("<title>fallback</title>"), "text/html"));
        Check(fallbackRegistered, "the first process-wide handler should be taken");
        var registration = api.RegisterUriSchemeHandler(own.NativeWebView, "app", request =>
            request.Finish(200, "OK", "text/html", Encoding.UTF8.GetBytes($"<title>own {request.Method}</title>")));

        shared.Navigate("app://localhost/a");
        own.Navigate("app://0.0.0.1/b");
        Pump(() => shared.Title == "fallback" && own.Title == "own GET");
        Check(shared.Title == "fallback", $"the shared view showed '{shared.Title}'");
        Check(own.Title == "own GET", $"the view with its own handler showed '{own.Title}'");

        registration.Dispose();
        own.Navigate("app://0.0.0.1/c");
        Pump(() => own.Title == "fallback");
        Check(own.Title == "fallback", $"after its handler went, the view showed '{own.Title}'");
    }
}
