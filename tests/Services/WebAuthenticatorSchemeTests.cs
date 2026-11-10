// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// Custom-scheme WebAuthenticator callbacks (myapp://callback), as MAUI 11 handles them on
/// Windows: the scheme is registered with the desktop for the app, the callback process
/// hands the URI to the running instance over the callback route, and the pending request
/// completes with MAUI's result parsing. Every test works in temporary XDG and runtime
/// directories; the user's associations are never read or written.
/// </summary>
[Collection(WebAuthenticatorRouteCollection.Name)]
public sealed class WebAuthenticatorSchemeTests : IDisposable
{
    private const string App = "/opt/omtest/MyApp";
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"om-webauth-{Guid.NewGuid():N}");
    private readonly XdgAssociationEnvironment _env;

    public WebAuthenticatorSchemeTests()
    {
        Directory.CreateDirectory(_root);
        _env = new XdgAssociationEnvironment(
            Path.Combine(_root, "config"),
            new[] { Path.Combine(_root, "etc-xdg") },
            Path.Combine(_root, "data"),
            new[] { Path.Combine(_root, "usr-share") },
            new[] { "kde" },
            new[] { App },
            "myapp.desktop",
            "MyApp");
        UriSchemeRegistration.EnvironmentOverride = _env;
        // Short socket directory: Unix socket paths are limited to 108 bytes.
        WebAuthenticatorCallbackRoute.DirectoryOverride = Path.Combine(_root, "run");
        WebAuthenticatorCallbackRoute.AppKeyOverride = "omtest-" + Guid.NewGuid().ToString("N");
        WebAuthenticatorService.BringToForegroundOverride = () => { };
    }

    public void Dispose()
    {
        WebAuthenticatorCallbackRoute.Unregister();
        WebAuthenticatorCallbackRoute.DirectoryOverride = null;
        WebAuthenticatorCallbackRoute.AppKeyOverride = null;
        WebAuthenticatorService.BringToForegroundOverride = null;
        UriSchemeRegistration.EnvironmentOverride = null;
        Directory.Delete(_root, recursive: true);
    }

    private string MimeApps => Path.Combine(_env.ConfigHome, "mimeapps.list");

    private string HandlerFile(string scheme) => Path.Combine(_env.DataHome, "applications", $"myapp-{scheme}-handler.desktop");

    // ---- registration ----

    [Fact]
    public void Register_writes_a_hidden_handler_entry_and_makes_it_the_default()
    {
        UriSchemeRegistration.IsRegistered("omtest").Should().BeFalse();

        UriSchemeRegistration.Register("OMTest");

        var entry = File.ReadAllText(HandlerFile("omtest"));
        entry.Should().Contain("Exec=/opt/omtest/MyApp %u\n")
            .And.Contain("MimeType=x-scheme-handler/omtest;\n")
            .And.Contain("NoDisplay=true\n")
            .And.Contain("StartupNotify=false\n");
        var list = File.ReadAllText(MimeApps);
        list.Should().Contain("[Default Applications]\nx-scheme-handler/omtest=myapp-omtest-handler.desktop;");
        list.Should().Contain("[Added Associations]\nx-scheme-handler/omtest=myapp-omtest-handler.desktop;");
        UriSchemeRegistration.IsRegistered("omtest").Should().BeTrue();
    }

    [Fact]
    public void Register_keeps_the_users_other_associations_and_Unregister_removes_only_its_own()
    {
        Directory.CreateDirectory(_env.ConfigHome);
        File.WriteAllText(MimeApps,
            "[Default Applications]\ntext/html=firefox.desktop;\n\n[Added Associations]\nx-scheme-handler/omtest=other.desktop;\n");

        UriSchemeRegistration.Register("omtest");
        File.ReadAllText(MimeApps).Should().Contain("text/html=firefox.desktop;")
            .And.Contain("x-scheme-handler/omtest=myapp-omtest-handler.desktop;other.desktop;");

        UriSchemeRegistration.Unregister("omtest").Should().BeTrue();

        File.Exists(HandlerFile("omtest")).Should().BeFalse();
        var list = File.ReadAllText(MimeApps);
        list.Should().Contain("text/html=firefox.desktop;").And.Contain("x-scheme-handler/omtest=other.desktop;");
        list.Should().NotContain("myapp-omtest-handler.desktop");
        UriSchemeRegistration.Unregister("omtest").Should().BeFalse();
    }

    [Fact]
    public void A_scheme_another_application_handles_is_not_registered_for_this_app()
    {
        WriteApp(Path.Combine(_root, "usr-share", "applications"), "other.desktop", "/usr/bin/other %u", "x-scheme-handler/omtest;");
        Directory.CreateDirectory(_env.ConfigHome);
        File.WriteAllText(MimeApps, "[Default Applications]\nx-scheme-handler/omtest=other.desktop;\n");

        UriSchemeRegistration.IsRegistered("omtest").Should().BeFalse();
    }

    [Fact]
    public void A_packaged_desktop_file_that_runs_the_app_counts_through_mimeinfo_cache()
    {
        var apps = Path.Combine(_root, "usr-share", "applications");
        WriteApp(apps, "com.example.MyApp.desktop", "\"/opt/omtest/MyApp\" %U", "x-scheme-handler/omtest;");
        File.WriteAllText(Path.Combine(apps, "mimeinfo.cache"), "[MIME Cache]\nx-scheme-handler/omtest=com.example.MyApp.desktop;\n");

        UriSchemeRegistration.IsRegistered("omtest").Should().BeTrue();
    }

    [Fact]
    public void Desktop_specific_lists_and_removed_associations_follow_the_XDG_order()
    {
        var apps = Path.Combine(_env.DataHome, "applications");
        WriteApp(apps, "mine.desktop", App + " %u", "x-scheme-handler/omtest;");
        WriteApp(apps, "theirs.desktop", "/usr/bin/theirs %u", "x-scheme-handler/omtest;");
        Directory.CreateDirectory(_env.ConfigHome);
        File.WriteAllText(MimeApps, "[Default Applications]\nx-scheme-handler/omtest=mine.desktop;\n");
        File.WriteAllText(Path.Combine(_env.ConfigHome, "kde-mimeapps.list"), "[Default Applications]\nx-scheme-handler/omtest=theirs.desktop;\n");

        UriSchemeRegistration.ResolveHandler(_env, "omtest")!.Value.DesktopId.Should().Be("theirs.desktop");

        File.WriteAllText(Path.Combine(_env.ConfigHome, "kde-mimeapps.list"), "[Removed Associations]\nx-scheme-handler/omtest=theirs.desktop;\n[Default Applications]\nx-scheme-handler/omtest=theirs.desktop;\n");
        UriSchemeRegistration.ResolveHandler(_env, "omtest")!.Value.DesktopId.Should().Be("mine.desktop");
    }

    [Fact]
    public void Inside_a_Flatpak_the_exported_desktop_file_declares_the_scheme_and_Register_refuses()
    {
        var desktop = Path.Combine(_root, "com.example.MyApp.desktop");
        File.WriteAllText(desktop, "[Desktop Entry]\nType=Application\nExec=MyApp %u\nMimeType=text/plain;x-scheme-handler/omtest;\n");
        UriSchemeRegistration.EnvironmentOverride = _env with { FlatpakDesktopFile = desktop };

        UriSchemeRegistration.IsRegistered("omtest").Should().BeTrue();
        UriSchemeRegistration.IsRegistered("other").Should().BeFalse();
        var act = () => UriSchemeRegistration.Register("other");
        act.Should().Throw<InvalidOperationException>().WithMessage("*x-scheme-handler/other;*");
    }

    [Theory]
    [InlineData("/opt/My App/run", "\"/opt/My App/run\"")]
    [InlineData("/opt/app", "/opt/app")]
    [InlineData("/opt/100%/app", "/opt/100%%/app")]
    [InlineData("/opt/a$b", "\"/opt/a\\\\$b\"")]
    public void Exec_arguments_are_quoted_per_the_desktop_entry_spec_and_split_back(string arg, string quoted)
    {
        DesktopExec.Quote(arg).Should().Be(quoted);
        DesktopExec.Split(DesktopExec.Quote(arg) + " %u").Should().Equal(arg, "%u");
    }

    [Theory]
    [InlineData("/opt/omtest/MyApp %u", true)]
    [InlineData("\"/opt/omtest/MyApp\" %U", true)]
    [InlineData("/opt/other/MyApp %u", false)]
    [InlineData("MyApp %u", true)]
    [InlineData("/usr/bin/flatpak run com.example.MyApp @@u %u @@", true)]
    [InlineData("", true)]
    public void Ownership_of_a_handler_command_is_decided_as_Windows_decides_a_registry_command(string exec, bool owned)
        => UriSchemeRegistration.IsCommandOwnedByApp(exec, new[] { App }).Should().Be(owned);

    [Fact]
    public void A_framework_dependent_launch_matches_dotnet_and_the_app_dll()
    {
        var command = new[] { "/usr/lib64/dotnet/dotnet", "/opt/omtest/MyApp.dll" };
        UriSchemeRegistration.IsCommandOwnedByApp("/usr/lib64/dotnet/dotnet /opt/omtest/MyApp.dll %u", command).Should().BeTrue();
        UriSchemeRegistration.IsCommandOwnedByApp("/usr/lib64/dotnet/dotnet /opt/omtest/Other.dll %u", command).Should().BeFalse();
    }

    [Fact]
    public void Gio_resolves_the_registered_handler()
    {
        var gio = FindOnPath("gio");
        if (gio == null)
            return;

        // GIO skips entries whose program does not exist.
        var exe = Path.Combine(_root, "MyApp");
        File.WriteAllText(exe, "#!/bin/sh\n");
        File.SetUnixFileMode(exe, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        UriSchemeRegistration.EnvironmentOverride = _env with { AppCommand = new[] { exe } };
        UriSchemeRegistration.Register("omtestgio");

        var psi = new ProcessStartInfo(gio, "mime x-scheme-handler/omtestgio") { RedirectStandardOutput = true, RedirectStandardError = true };
        psi.Environment["XDG_DATA_HOME"] = _env.DataHome;
        psi.Environment["XDG_CONFIG_HOME"] = _env.ConfigHome;
        psi.Environment["XDG_DATA_DIRS"] = Path.Combine(_root, "usr-share");
        psi.Environment["XDG_CONFIG_DIRS"] = Path.Combine(_root, "etc-xdg");
        using var process = Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(10000);

        output.Should().Contain("myapp-omtestgio-handler.desktop");
    }

    // ---- authentication ----

    [Fact]
    public async Task An_unregistered_scheme_is_registered_and_the_forwarded_callback_completes_the_request()
    {
        var callback = new Uri("omtest://callback");
        Uri? opened = null;
        var auth = new WebAuthenticatorService(url =>
        {
            opened = url;
            // The desktop starts a second process for the redirect; it sends the URI over the route.
            _ = Task.Run(() => SendOverRoute("omtest", "omtest://callback?code=abc123&state=xyz#access_token=tok"));
            return Task.FromResult(true);
        });

        var result = await auth.AuthenticateAsync(new WebAuthenticatorOptions
        {
            Url = new Uri("https://issuer.example/authorize?client_id=1"),
            CallbackUrl = callback,
        }).WaitAsync(TimeSpan.FromSeconds(15));

        opened.Should().Be(new Uri("https://issuer.example/authorize?client_id=1"));
        result.Get("code").Should().Be("abc123");
        result.Get("state").Should().Be("xyz");
        result.AccessToken.Should().Be("tok");
        result.CallbackUri!.Scheme.Should().Be("omtest");
        UriSchemeRegistration.IsRegistered("omtest").Should().BeTrue();
        // The route stays registered after the request, as Windows keeps its key.
        WebAuthenticatorCallbackRoute.OwnedScheme.Should().Be("omtest");
    }

    [Fact]
    public async Task A_callback_for_another_route_is_refused_and_the_request_keeps_waiting()
    {
        UriSchemeRegistration.Register("omtest");
        var foregrounded = 0;
        WebAuthenticatorService.BringToForegroundOverride = () => Interlocked.Increment(ref foregrounded);
        string? wrongAck = null;
        var auth = new WebAuthenticatorService(url =>
        {
            _ = Task.Run(() =>
            {
                wrongAck = SendOverRoute("omtest", "omtest://callback/other?code=nope");
                SendOverRoute("omtest", "omtest://callback/done?code=yes");
            });
            return Task.FromResult(true);
        });

        var result = await auth.AuthenticateAsync(new WebAuthenticatorOptions
        {
            Url = new Uri("https://issuer.example/authorize"),
            CallbackUrl = new Uri("omtest://callback/done"),
        }).WaitAsync(TimeSpan.FromSeconds(15));

        wrongAck.Should().Be("0");
        result.Get("code").Should().Be("yes");
        foregrounded.Should().Be(1);
    }

    [Fact]
    public async Task The_response_decoder_is_used_as_on_Windows()
    {
        UriSchemeRegistration.Register("omtest");
        var auth = new WebAuthenticatorService(url =>
        {
            _ = Task.Run(() => SendOverRoute("omtest", "omtest://cb?raw=1"));
            return Task.FromResult(true);
        });

        var result = await auth.AuthenticateAsync(new WebAuthenticatorOptions
        {
            Url = new Uri("https://issuer.example/authorize"),
            CallbackUrl = new Uri("omtest://cb"),
            ResponseDecoder = new FixedDecoder(),
        }).WaitAsync(TimeSpan.FromSeconds(15));

        result.Get("decoded").Should().Be("yes");
    }

    [Fact]
    public async Task Without_automatic_registration_an_unregistered_scheme_throws_as_on_Windows()
    {
        var auth = new WebAuthenticatorService(_ => Task.FromResult(true)) { RegisterCallbackScheme = false };

        var act = () => auth.AuthenticateAsync(new WebAuthenticatorOptions { Url = new Uri("https://issuer.example/a"), CallbackUrl = new Uri("omtest://cb") });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("The URI scheme 'omtest' is not registered for this application.*");
        File.Exists(HandlerFile("omtest")).Should().BeFalse();
    }

    [Fact]
    public async Task Cancellation_ends_the_request_with_TaskCanceledException()
    {
        UriSchemeRegistration.Register("omtest");
        using var cts = new CancellationTokenSource();
        var auth = new WebAuthenticatorService(_ =>
        {
            cts.CancelAfter(50);
            return Task.FromResult(true);
        });

        var act = () => auth.AuthenticateAsync(new WebAuthenticatorOptions { Url = new Uri("https://issuer.example/a"), CallbackUrl = new Uri("omtest://cb") }, cts.Token);

        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task A_second_request_while_one_is_pending_throws()
    {
        UriSchemeRegistration.Register("omtest");
        using var cts = new CancellationTokenSource();
        var auth = new WebAuthenticatorService(_ => Task.FromResult(true));
        var first = auth.AuthenticateAsync(new WebAuthenticatorOptions { Url = new Uri("https://issuer.example/a"), CallbackUrl = new Uri("omtest://cb") }, cts.Token);

        var second = () => new WebAuthenticatorService(_ => Task.FromResult(true))
            .AuthenticateAsync(new WebAuthenticatorOptions { Url = new Uri("https://issuer.example/a"), CallbackUrl = new Uri("omtest://cb") });

        await second.Should().ThrowAsync<InvalidOperationException>().WithMessage("Another WebAuthenticator operation is already in progress.");
        cts.Cancel();
        await FluentActions.Awaiting(() => first).Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task A_browser_that_does_not_open_fails_the_request()
    {
        UriSchemeRegistration.Register("omtest");
        var auth = new WebAuthenticatorService(_ => Task.FromResult(false));

        var act = () => auth.AuthenticateAsync(new WebAuthenticatorOptions { Url = new Uri("https://issuer.example/a"), CallbackUrl = new Uri("omtest://cb") });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Failed to launch the browser for authentication.");
    }

    [Fact]
    public async Task A_route_another_live_instance_owns_fails_the_request()
    {
        UriSchemeRegistration.Register("omtest");
        using var other = ListenAs("omtest", out _);
        var auth = new WebAuthenticatorService(_ => Task.FromResult(true));

        var act = () => auth.AuthenticateAsync(new WebAuthenticatorOptions { Url = new Uri("https://issuer.example/a"), CallbackUrl = new Uri("omtest://cb") });

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Unable to register the WebAuthenticator callback route.");
        thrown.Which.InnerException!.Message.Should().Be("Another app instance owns the WebAuthenticator callback route.");
    }

    [Fact]
    public void A_stale_socket_left_by_a_crashed_instance_is_replaced()
    {
        // A crashed owner leaves its socket file behind with nobody accepting on it.
        var path = WebAuthenticatorCallbackRoute.SocketPath("omtest");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);

        WebAuthenticatorCallbackRoute.Register("omtest", _ => true);

        WebAuthenticatorCallbackRoute.OwnedScheme.Should().Be("omtest");
    }

    [Fact]
    public void The_callback_process_forwards_the_uri_to_the_route_owner_and_reports_redirected()
    {
        using var owner = ListenAs("omtest", out var path);
        var received = Task.Run(() =>
        {
            using var client = owner.Accept();
            var buffer = new byte[1024];
            var n = client.Receive(buffer);
            client.Send("1\n"u8.ToArray());
            return Encoding.UTF8.GetString(buffer, 0, n);
        });

        var redirected = WebAuthenticatorService.TryRedirectCallbackActivation(new[] { "--verbose", "omtest://callback?code=1" });

        redirected.Should().BeTrue();
        received.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        received.Result.Should().Be("omtest://callback?code=1\n");
    }

    [Theory]
    [InlineData("omtest://callback?code=1")]
    [InlineData("https://example.com/")]
    [InlineData("/home/user/file.txt")]
    public void Without_a_route_owner_the_app_starts_normally(string arg)
        => WebAuthenticatorService.TryRedirectCallbackActivation(new[] { arg }).Should().BeFalse();

    [Theory]
    [InlineData("myapp://callback", "myapp://callback?code=1", true)]
    [InlineData("myapp://callback", "MYAPP://callback", true)]
    [InlineData("myapp://callback", "myapp://other", false)]
    [InlineData("myapp://callback/path", "myapp://callback/other", false)]
    [InlineData("myapp://callback/", "myapp://callback/any/thing", true)]
    [InlineData("myapp:/", "myapp://host/x", true)]
    [InlineData("myapp://callback", "other://callback", false)]
    public void Callback_matching_follows_MAUIs_WebUtils(string expected, string actual, bool match)
        => WebAuthenticatorService.CanHandleCallback(new Uri(expected), new Uri(actual)).Should().Be(match);

    private static string SendOverRoute(string scheme, string uri)
    {
        var path = WebAuthenticatorCallbackRoute.SocketPath(scheme);
        for (var i = 0; i < 100 && !File.Exists(path); i++)
            Thread.Sleep(20);
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Connect(new UnixDomainSocketEndPoint(path));
        socket.Send(Encoding.UTF8.GetBytes(uri + "\n"));
        socket.Shutdown(SocketShutdown.Send);
        var ack = new byte[2];
        var n = socket.Receive(ack);
        return Encoding.ASCII.GetString(ack, 0, Math.Max(0, n)).Trim();
    }

    private static Socket ListenAs(string scheme, out string path)
    {
        path = WebAuthenticatorCallbackRoute.SocketPath(scheme);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(path));
        socket.Listen(1);
        return socket;
    }

    private static void WriteApp(string dir, string id, string exec, string mime)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, id), $"[Desktop Entry]\nType=Application\nName=X\nExec={exec}\nMimeType={mime}\n");
    }

    private static string? FindOnPath(string name)
        => (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => Path.Combine(d, name)).FirstOrDefault(File.Exists);

    private sealed class FixedDecoder : IWebAuthenticatorResponseDecoder
    {
        public IDictionary<string, string>? DecodeResponse(Uri uri) => new Dictionary<string, string> { ["decoded"] = "yes" };
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WebAuthenticatorRouteCollection
{
    public const string Name = "WebAuthenticator callback route";
}
