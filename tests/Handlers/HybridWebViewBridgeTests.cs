// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// HybridWebView's protocol on Linux without a WebKit host (the live round trips are the
/// hybrid-* scenarios of the WebViewHost): messages, InvokeJavaScriptAsync and
/// EvaluateJavaScriptAsync scripts, the app-origin server, the .NET invoker and
/// WebResourceRequested through MAUI's own event args.
/// </summary>
public class HybridWebViewBridgeTests
{
    private sealed class FakeHost : IHybridWebViewScriptHost
    {
        public List<string> Scripts { get; } = new();
        public Func<string, string?> Result { get; set; } = _ => null;
        public string? CurrentUrl { get; set; } = "app://0.0.0.1/";

        public Task<string?> EvaluateJavaScriptAsync(string script)
        {
            Scripts.Add(script);
            return Task.FromResult(Result(script));
        }
    }

    public sealed class Target
    {
        public int Add(int a, int b) => a + b;
        public async Task<string> EchoAsync(string text) { await Task.Yield(); return "echo " + text; }
        public string[] Pair(string a, string b) => new[] { a, b };
        public void Nothing() { }
        public void Fail() => throw new InvalidOperationException("nope");
    }

    private static readonly Dictionary<string, string> s_assets = new()
    {
        ["wwwroot/index.html"] = "<html>index</html>",
        ["wwwroot/css/site.css"] = "p{}",
        ["wwwroot/app.js"] = "var x;",
        ["other/start.html"] = "<html>start</html>",
    };

    private static Task<Stream?> OpenAsset(string path) =>
        Task.FromResult<Stream?>(s_assets.TryGetValue(path, out var text) ? new MemoryStream(Encoding.UTF8.GetBytes(text)) : null);

    private static (HybridWebView View, FakeHost Host, HybridWebViewBridge Bridge) Create()
    {
        var view = new HybridWebView();
        var host = new FakeHost();
        return (view, host, new HybridWebViewBridge(view, host, OpenAsset));
    }

    private static HybridWebRequest Get(string url, Dictionary<string, string>? headers = null) =>
        new(new Uri(url), "GET", headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), null);

    private static HybridWebRequest Invoke(string json, string method = "POST", bool token = true, string origin = "app://0.0.0.1")
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Origin"] = origin };
        if (token)
            headers["X-Maui-Invoke-Token"] = "HybridWebView";
        return new(new Uri("app://0.0.0.1/__hwvInvokeDotNet"), method, headers, Encoding.UTF8.GetBytes(json));
    }

    private static string Text(HybridWebResponse response) => Encoding.UTF8.GetString(response.Body);

    private static JsonTypeInfo<T> TypeInfo<T>() => (JsonTypeInfo<T>)JsonSerializerOptions.Default.GetTypeInfo(typeof(T));

    // ---- messages ---------------------------------------------------------

    [Fact]
    public void Raw_messages_are_url_decoded_and_raised_on_the_view()
    {
        var (view, _, bridge) = Create();
        string? received = null;
        view.RawMessageReceived += (_, e) => received = e.Message;

        bridge.OnScriptMessage("__RawMessage|" + Uri.EscapeDataString("a|b \"c\"\né"));

        received.Should().Be("a|b \"c\"\né");
    }

    [Fact]
    public void Messages_from_a_page_outside_the_app_origin_are_ignored()
    {
        var (view, host, bridge) = Create();
        var count = 0;
        view.RawMessageReceived += (_, _) => count++;

        host.CurrentUrl = "https://example.com/";
        bridge.OnScriptMessage("__RawMessage|x");
        host.CurrentUrl = null;
        bridge.OnScriptMessage("__RawMessage|x");

        count.Should().Be(0);
    }

    [Fact]
    public void Malformed_messages_throw_as_MAUI_does()
    {
        var (_, _, bridge) = Create();
        bridge.Invoking(b => b.MessageReceived("no pipe")).Should().Throw<ArgumentException>();
        bridge.Invoking(b => b.MessageReceived("__Unknown|x")).Should().Throw<ArgumentException>();
        bridge.Invoking(b => b.MessageReceived("__InvokeJavaScriptCompleted|nopipe")).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SendRawMessage_calls_the_script_receiver_with_a_JSON_string()
    {
        var (_, host, bridge) = Create();
        bridge.SendRawMessage("say \"hi\"\n</script>");
        host.Scripts.Should().ContainSingle().Which.Should().Be(
            "window.external.receiveMessage(" + JsonSerializer.Serialize("say \"hi\"\n</script>") + ")");
    }

    // ---- .NET to JavaScript ------------------------------------------------

    [Fact]
    public async Task EvaluateJavaScriptAsync_wraps_the_script_and_unquotes_the_JSON_result()
    {
        var (_, host, bridge) = Create();
        host.Result = _ => "\"it's\"";

        var result = await bridge.EvaluateJavaScriptAsync("document.title + 'x'\n");

        result.Should().Be("it's");
        host.Scripts.Single().Should().Be("try{JSON.stringify(eval('document.title + \\'x\\'\\n'))}catch(e){'null'};");

        host.Result = _ => "null";
        (await bridge.EvaluateJavaScriptAsync("undefined")).Should().BeNull();
        host.Result = _ => null;
        (await bridge.EvaluateJavaScriptAsync("x")).Should().BeNull();
    }

    [Fact]
    public async Task InvokeJavaScriptAsync_calls_the_page_and_completes_with_its_typed_result()
    {
        var (_, host, bridge) = Create();
        var request = new HybridWebViewInvokeJavaScriptRequest("add", TypeInfo<int>(), new object?[] { 2, null }, new JsonTypeInfo?[] { TypeInfo<int>(), TypeInfo<int>() });

        var pending = bridge.InvokeJavaScriptAsync(request);
        host.Scripts.Should().ContainSingle().Which.Should().Contain("window.HybridWebView.__InvokeJavaScript(1, add, [2, null])");
        pending.IsCompleted.Should().BeFalse();

        bridge.MessageReceived("__InvokeJavaScriptCompleted|1|42");
        (await pending).Should().Be(42);
    }

    [Fact]
    public async Task InvokeJavaScriptAsync_without_a_return_type_or_with_a_null_result_gives_null()
    {
        var (_, _, bridge) = Create();
        var untyped = bridge.InvokeJavaScriptAsync(new HybridWebViewInvokeJavaScriptRequest("f", null, null, null));
        bridge.MessageReceived("__InvokeJavaScriptCompleted|1|{\"a\":1}");
        (await untyped).Should().BeNull();

        var undefinedResult = bridge.InvokeJavaScriptAsync(new HybridWebViewInvokeJavaScriptRequest("g", TypeInfo<int>(), null, null));
        bridge.MessageReceived("__InvokeJavaScriptCompleted|2|undefined");
        (await undefinedResult).Should().BeNull();
    }

    [Fact]
    public async Task A_failed_JavaScript_call_throws_MAUIs_exception_with_the_script_error()
    {
        var (_, _, bridge) = Create();
        var pending = bridge.InvokeJavaScriptAsync(new HybridWebViewInvokeJavaScriptRequest("boom", TypeInfo<int>(), null, null));

        bridge.MessageReceived("__InvokeJavaScriptFailed|1|" + JsonSerializer.Serialize(new { Name = "TypeError", Message = "kaput", StackTrace = "at boom" }));

        var ex = (await pending.Invoking(p => p).Should().ThrowAsync<Exception>()).Which;
        ex.GetType().FullName.Should().Be("Microsoft.Maui.Handlers.HybridWebViewInvokeJavaScriptException");
        ex.Message.Should().Be("InvokeJavaScript threw an exception: kaput");
        ex.InnerException!.Data["JavaScriptErrorName"].Should().Be("TypeError");
        ex.InnerException.StackTrace.Should().Be("at boom");
    }

    // ---- the app origin ----------------------------------------------------

    [Fact]
    public async Task The_root_serves_the_default_file_from_HybridRoot()
    {
        var (_, _, bridge) = Create();
        var response = await bridge.HandleRequestAsync(Get("app://0.0.0.1/"));
        response.StatusCode.Should().Be(200);
        response.ContentType.Should().Be("text/html");
        Text(response).Should().Be("<html>index</html>");
        response.Headers.Should().Contain(new KeyValuePair<string, string>("Cache-Control", "no-cache, max-age=0, must-revalidate, no-store"));

        var custom = new HybridWebView { HybridRoot = "other", DefaultFile = "start.html" };
        var other = await new HybridWebViewBridge(custom, new FakeHost(), OpenAsset).HandleRequestAsync(Get("app://0.0.0.1/?q=1#top"));
        Text(other).Should().Be("<html>start</html>");
    }

    [Theory]
    [InlineData("app://0.0.0.1/css/site.css", "text/css", "p{}")]
    [InlineData("app://0.0.0.1/app.js?v=3", "text/javascript", "var x;")]
    public async Task Assets_are_served_with_MAUIs_content_type(string url, string contentType, string body)
    {
        var (_, _, bridge) = Create();
        var response = await bridge.HandleRequestAsync(Get(url));
        response.StatusCode.Should().Be(200);
        response.ContentType.Should().Be(contentType);
        Text(response).Should().Be(body);
    }

    [Theory]
    [InlineData("app://0.0.0.1/missing.html")]
    [InlineData("app://0.0.0.1/%2e%2e/secret.txt")]
    [InlineData("app://0.0.0.1/css/..%2F..%2Fsecret.txt")]
    [InlineData("app://localhost/index.html")]
    public async Task Missing_outside_and_foreign_paths_are_not_found(string url)
    {
        var (_, _, bridge) = Create();
        (await bridge.HandleRequestAsync(Get(url))).StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task The_bridge_script_is_MAUIs_own()
    {
        var (_, _, bridge) = Create();
        var response = await bridge.HandleRequestAsync(Get("app://0.0.0.1/_framework/hybridwebview.js"));

        using var expected = typeof(HybridWebViewHandler).Assembly.GetManifestResourceStream("_framework/hybridwebview.js")!;
        using var buffer = new MemoryStream();
        expected.CopyTo(buffer);
        response.StatusCode.Should().Be(200);
        response.ContentType.Should().Be("application/javascript");
        response.Body.Should().Equal(buffer.ToArray());
        Text(response).Should().Contain("window.webkit.messageHandlers.webwindowinterop");
    }

    // ---- JavaScript to .NET ------------------------------------------------

    [Fact]
    public async Task InvokeDotNet_calls_the_target_and_answers_MAUIs_JSON()
    {
        var (view, _, bridge) = Create();
        view.SetInvokeJavaScriptTarget(new Target());

        var add = await bridge.HandleRequestAsync(Invoke("{\"MethodName\":\"Add\",\"ParamValues\":[\"2\",\"3\"]}"));
        add.StatusCode.Should().Be(200);
        add.ContentType.Should().Be("application/json");
        Text(add).Should().Be("{\"Result\":5,\"IsJson\":false,\"IsError\":false,\"ErrorMessage\":null,\"ErrorType\":null,\"ErrorStackTrace\":null}");

        var echo = JsonDocument.Parse(Text(await bridge.HandleRequestAsync(Invoke("{\"MethodName\":\"EchoAsync\",\"ParamValues\":[\"\\\"hi\\\"\"]}")))).RootElement;
        echo.GetProperty("Result").GetString().Should().Be("\"echo hi\"");
        echo.GetProperty("IsJson").GetBoolean().Should().BeTrue(); // a string is a class: JSON, as MAUI does

        var pair = JsonDocument.Parse(Text(await bridge.HandleRequestAsync(Invoke("{\"MethodName\":\"Pair\",\"ParamValues\":[\"\\\"a\\\"\",\"\\\"b\\\"\"]}")))).RootElement;
        pair.GetProperty("Result").GetString().Should().Be("[\"a\",\"b\"]");

        var nothing = JsonDocument.Parse(Text(await bridge.HandleRequestAsync(Invoke("{\"MethodName\":\"Nothing\"}")))).RootElement;
        nothing.GetProperty("Result").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task InvokeDotNet_failures_answer_an_error_result()
    {
        var (view, _, bridge) = Create();
        var noTarget = JsonDocument.Parse(Text(await bridge.HandleRequestAsync(Invoke("{\"MethodName\":\"Add\"}")))).RootElement;
        noTarget.GetProperty("IsError").GetBoolean().Should().BeTrue();

        view.SetInvokeJavaScriptTarget(new Target());
        var fail = JsonDocument.Parse(Text(await bridge.HandleRequestAsync(Invoke("{\"MethodName\":\"Fail\"}")))).RootElement;
        fail.GetProperty("IsError").GetBoolean().Should().BeTrue();
        fail.GetProperty("ErrorMessage").GetString().Should().Be("nope");
        fail.GetProperty("ErrorType").GetString().Should().Be("InvalidOperationException");

        var unknown = JsonDocument.Parse(Text(await bridge.HandleRequestAsync(Invoke("{\"MethodName\":\"Missing\"}")))).RootElement;
        unknown.GetProperty("ErrorMessage").GetString().Should().Contain("Missing");
    }

    [Fact]
    public async Task InvokeDotNet_only_answers_MAUIs_script()
    {
        var (view, _, bridge) = Create();
        view.SetInvokeJavaScriptTarget(new Target());
        const string body = "{\"MethodName\":\"Add\",\"ParamValues\":[\"1\",\"1\"]}";

        (await bridge.HandleRequestAsync(Invoke(body, token: false))).StatusCode.Should().Be(400);
        (await bridge.HandleRequestAsync(Invoke(body, origin: "https://evil.example"))).StatusCode.Should().Be(400);
        (await bridge.HandleRequestAsync(Invoke(body, method: "GET"))).StatusCode.Should().Be(405);
        (await bridge.HandleRequestAsync(Invoke(""))).StatusCode.Should().Be(400);
    }

    // ---- WebResourceRequested ----------------------------------------------

    [Fact]
    public async Task WebResourceRequested_sees_the_request_and_answers_with_SetResponse()
    {
        var (view, _, bridge) = Create();
        WebViewWebResourceRequestedEventArgs? seen = null;
        view.WebResourceRequested += (_, e) =>
        {
            seen = e;
            e.Handled = true;
            e.SetResponse(202, "Accepted", "application/json", new MemoryStream(Encoding.UTF8.GetBytes("{\"v\":1}")));
        };

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Accept"] = "application/json" };
        var response = await bridge.HandleRequestAsync(Get("app://0.0.0.1/api/data?x=1&y=two%20words", headers));

        seen.Should().NotBeNull();
        seen!.PlatformArgs.Should().NotBeNull("MAUI's own HybridWebView built the args");
        seen.Uri.Should().Be(new Uri("app://0.0.0.1/api/data?x=1&y=two%20words"));
        seen.Method.Should().Be("GET");
        seen.Headers["accept"].Should().Be("application/json");
        seen.QueryParameters.Should().Contain(new KeyValuePair<string, string>("x", "1")).And.Contain(new KeyValuePair<string, string>("y", "two words"));
        response.StatusCode.Should().Be(202);
        response.ReasonPhrase.Should().Be("Accepted");
        response.ContentType.Should().Be("application/json");
        Text(response).Should().Be("{\"v\":1}");
    }

    [Fact]
    public async Task WebResourceRequested_can_answer_later_with_a_content_task()
    {
        var (view, _, bridge) = Create();
        var content = new TaskCompletionSource<Stream?>();
        view.WebResourceRequested += (_, e) =>
        {
            e.Handled = true;
            e.SetResponse(200, "OK", new Dictionary<string, string> { ["Content-Type"] = "text/plain", ["X-Custom"] = "yes" }, content.Task);
        };

        var pending = bridge.HandleRequestAsync(Get("app://0.0.0.1/later"));
        pending.IsCompleted.Should().BeFalse();
        content.SetResult(new MemoryStream(Encoding.UTF8.GetBytes("done")));

        var response = await pending;
        Text(response).Should().Be("done");
        response.Headers.Should().Contain(new KeyValuePair<string, string>("X-Custom", "yes"));
    }

    [Fact]
    public async Task An_unhandled_request_is_served_as_usual()
    {
        var (view, _, bridge) = Create();
        var raised = 0;
        view.WebResourceRequested += (_, e) =>
        {
            raised++;
            e.SetResponse(500, "ignored"); // not Handled: MAUI serves the request itself
        };

        var response = await bridge.HandleRequestAsync(Get("app://0.0.0.1/"));
        raised.Should().Be(1);
        response.StatusCode.Should().Be(200);
        Text(response).Should().Be("<html>index</html>");
    }

    [Fact]
    public async Task Handled_without_a_response_is_not_found()
    {
        var (view, _, bridge) = Create();
        view.WebResourceRequested += (_, e) => e.Handled = true;
        (await bridge.HandleRequestAsync(Get("app://0.0.0.1/"))).StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Interception_keeps_working_after_the_JIT_optimizes_the_callers()
    {
        // The patched MAUI members are tiny; inlined into a re-jitted caller they would lose the
        // patch. Many requests, with pauses for tiered compilation, all still intercepted.
        var (view, _, bridge) = Create();
        view.WebResourceRequested += (_, e) =>
        {
            e.Handled = true;
            e.SetResponse(200, "OK", "text/plain", new MemoryStream(Encoding.UTF8.GetBytes(e.Uri.AbsolutePath)));
        };
        for (var round = 0; round < 4; round++)
        {
            for (var i = 0; i < 60; i++)
            {
                var response = await bridge.HandleRequestAsync(Get($"app://0.0.0.1/r{round}/{i}"));
                Text(response).Should().Be($"/r{round}/{i}");
            }
            await Task.Delay(150);
        }
    }

    // ---- handler -----------------------------------------------------------

    [Fact]
    public void The_handler_maps_MAUIs_HybridWebView_commands()
    {
        ICommandMapper mapper = LinuxHybridWebViewHandler.CommandMapper;
        foreach (var command in new[] { nameof(IHybridWebView.EvaluateJavaScriptAsync), nameof(IHybridWebView.InvokeJavaScriptAsync), nameof(IHybridWebView.SendRawMessage) })
            mapper.GetCommand(command).Should().NotBeNull(command);
        new LinuxHybridWebViewHandler().Should().BeAssignableTo<IHybridWebViewHandler>();
        LinuxHybridWebViewHandler.AppOriginUri.Should().Be(new Uri("app://0.0.0.1/"));
    }

    [Fact]
    public void HybridWebView_resolves_to_the_Linux_handler_in_both_registries()
    {
        MauiHandlerExtensions.GetLinuxHandlerType(typeof(HybridWebView)).Should().Be(typeof(LinuxHybridWebViewHandler));
        var builder = MauiApp.CreateBuilder(useDefaults: false);
        builder.UseMauiApp<Application>();
        builder.UseLinux();
        builder.Build().Services.GetRequiredService<IMauiHandlersFactory>()
            .GetHandlerType(typeof(HybridWebView)).Should().Be(typeof(LinuxHybridWebViewHandler));
    }
}
