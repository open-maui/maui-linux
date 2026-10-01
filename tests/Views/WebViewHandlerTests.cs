// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Views;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// End-to-end WebView behaviour on the WPE engine, which renders headlessly
/// so these run without a display: HTML and URL sources, navigation events,
/// JavaScript evaluation with real results, history, cookies, reload, frames
/// (raster copy and GPU zero-copy), and hardware keycodes in key events.
/// </summary>
/// <remarks>
/// WebKit binds itself to the process main thread (WTF::initializeMainThread
/// aborts anywhere else) and xunit runs tests on pool threads, so each
/// scenario runs in the tests/WebViewHost program, on its main thread, and
/// the test asserts on that process's exit code and output. The host is
/// built alongside the test project. Scenarios are skipped when WPE WebKit
/// is not installed on the machine (exit code 3).
/// </remarks>
[Collection("GLibMainLoop")]
public class WebViewHandlerTests
{
    private static readonly string s_hostDll = Path.Combine(AppContext.BaseDirectory, "WebViewHost.dll");

    public static TheoryData<string> Scenarios => new()
    {
        "html-source-navigation-events",
        "javascript-evaluation",
        "javascript-mutation-and-eval",
        "javascript-non-string-and-errors",
        "navigation-decision-cancel",
        "history-can-go-back",
        "user-agent-round-trip",
        "frames-delivered-after-load",
        "cookies-round-trip",
        "reload-and-stop",
        "keyboard-event-code",
        "frames-delivered-gpu",
        "scale-change",
        "browser-status-and-failure",
        "browser-response-policy",
        "browser-download-intercept",
        "browser-download-save-dialog",
        "browser-new-window-in-place",
        "browser-url-changed",
        "browser-zoom-round-trip",
        "browser-capture",
        "browser-save-as-pdf",
        "browser-zoom-gestures",
    };

    /// <summary>Host exit code for "this machine cannot run the scenario" (see WebViewHost Program).</summary>
    private const int NotSupportedExitCode = 4;

    /// <summary>
    /// Scenarios that need more than WPE (a GPU EGL context) and may report
    /// "not supported" on machines without one; that counts as a pass with a
    /// note, and only for these.
    /// </summary>
    private static readonly HashSet<string> s_mayBeUnsupported = new(StringComparer.Ordinal)
    {
        "frames-delivered-gpu",
    };

    private readonly ITestOutputHelper _output;

    public WebViewHandlerTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Host_program_is_built_next_to_the_tests()
    {
        File.Exists(s_hostDll).Should().BeTrue($"the WebView scenarios run out of process; expected {s_hostDll}");
    }

    [Fact]
    public void Host_lists_every_scenario_the_tests_know()
    {
        var (code, stdout, _) = RunHost("--list");
        code.Should().Be(0);
        var listed = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var scenario in Scenarios)
            listed.Should().Contain((string)scenario[0]);
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Scenario_passes_on_the_process_main_thread(string scenario)
    {
        if (!WpeWebView.IsSupported)
            return; // WPE not installed: nothing to verify here.

        var (code, stdout, stderr) = RunHost(scenario);
        code.Should().NotBe(3, "WPE is available in this process, so it must be in the host too");
        if (code == NotSupportedExitCode && s_mayBeUnsupported.Contains(scenario))
        {
            _output.WriteLine($"NOTE: '{scenario}' is not supported on this machine: {stderr.Trim()}");
            return;
        }
        if (!string.IsNullOrWhiteSpace(stdout))
            _output.WriteLine(stdout.Trim());
        code.Should().Be(0, $"scenario '{scenario}' failed:\n{stderr}\n{stdout}");
        stdout.Should().Contain($"ok {scenario}");
    }

    private static (int ExitCode, string StdOut, string StdErr) RunHost(string argument)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        psi.ArgumentList.Add(s_hostDll);
        psi.ArgumentList.Add(argument);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60_000))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"WebViewHost {argument} did not exit within 60 s.\n{stderr.Result}");
        }
        return (process.ExitCode, stdout.Result, stderr.Result);
    }
}
