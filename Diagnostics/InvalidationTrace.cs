// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Diagnostics;

/// <summary>
/// <c>OPENMAUI_TRACE_INVALIDATE=1</c>: once a second, how many frames were
/// drawn (and how many whole) and which views asked to be repainted, by type,
/// with their MAUI view. For finding what keeps an idle window repainting.
/// </summary>
internal static class InvalidationTrace
{
    internal static readonly bool Enabled = Environment.GetEnvironmentVariable("OPENMAUI_TRACE_INVALIDATE") == "1";

    private static readonly Dictionary<string, int> s_requests = new();
    private static int s_frames;
    private static int s_fullFrames;
    private static DateTime s_windowStart = DateTime.UtcNow;

    internal static void Record(SkiaView view)
    {
        var key = view.MauiView is { } maui ? $"{view.GetType().Name}/{maui.GetType().Name}" : view.GetType().Name;
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
        DiagnosticLog.Info("InvalidationTrace", $"{s_frames} frames ({s_fullFrames} whole) in {(now - s_windowStart).TotalSeconds:0.0}s; repaint requests: {top}");
        s_frames = 0;
        s_fullFrames = 0;
        s_windowStart = now;
    }
}
