// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Diagnostics;

/// <summary>
/// <c>OPENMAUI_TRACE_INVALIDATE=1</c>: once a second, how many frames were
/// drawn (and how many whole) and which views asked to be repainted, by type,
/// with their MAUI view. For finding what keeps an idle window repainting.
/// <c>OPENMAUI_TRACE_INVALIDATE_STACK=&lt;type&gt;</c> (a platform or MAUI view type name, as the
/// trace prints them) also prints the call stack of the first requests from views of that type
/// after the app has run 15 seconds (past start-up, where every view asks once): what keeps asking.
/// </summary>
internal static class InvalidationTrace
{
    internal static readonly bool Enabled = Environment.GetEnvironmentVariable("OPENMAUI_TRACE_INVALIDATE") == "1";

    private static readonly Dictionary<string, int> s_requests = new();
    private static int s_frames;
    private static int s_fullFrames;
    private static DateTime s_windowStart = DateTime.UtcNow;

    private static readonly string? s_stackType = Environment.GetEnvironmentVariable("OPENMAUI_TRACE_INVALIDATE_STACK");
    private static int s_stacksPrinted;
    private static readonly DateTime s_started = DateTime.UtcNow;

    internal static void Record(SkiaView view)
    {
        var key = view.MauiView is { } maui ? $"{view.GetType().Name}/{maui.GetType().Name}" : view.GetType().Name;
        if (!string.IsNullOrEmpty(s_stackType) && s_stacksPrinted < 5
            && (DateTime.UtcNow - s_started).TotalSeconds > 15
            && (view.GetType().Name == s_stackType || view.MauiView?.GetType().Name == s_stackType))
        {
            s_stacksPrinted++;
            Console.Error.WriteLine($"[InvalidationTrace] {key} asked for a repaint:\n{Environment.StackTrace}");
        }
        lock (s_requests)
            s_requests[key] = s_requests.TryGetValue(key, out var n) ? n + 1 : 1;
    }

    internal static void Frame(bool full)
    {
        s_frames++;
        if (full) s_fullFrames++;
        var now = DateTime.UtcNow;
        if (now - s_windowStart < TimeSpan.FromSeconds(1))
            return;
        string top;
        lock (s_requests)
        {
            top = string.Join(", ", s_requests.OrderByDescending(p => p.Value).Take(8).Select(p => $"{p.Key} x{p.Value}"));
            s_requests.Clear();
        }
        // Written directly: the trace is opted into by its variable, and DiagnosticLog.Info is
        // off in a release package, which left the variable doing nothing there.
        Console.Error.WriteLine($"[InvalidationTrace] {s_frames} frames ({s_fullFrames} whole) in {(now - s_windowStart).TotalSeconds:0.0}s; repaint requests: {top}");
        s_frames = 0;
        s_fullFrames = 0;
        s_windowStart = now;
    }
}
