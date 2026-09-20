// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// Opt-in per-window frame timing, enabled with <c>OPENMAUI_RENDER_STATS=1</c>.
/// Measures each rendered frame from region selection through presentation
/// (draw + flush + submit) and prints a summary line every reporting interval:
/// frames rendered, effective FPS, and average / p50 / p95 / p99 / max frame
/// time in milliseconds. Frames the engine skips (nothing dirty) are not
/// counted, so FPS reflects rendered frames, not loop iterations.
/// </summary>
public sealed class FrameStatistics
{
    public const string EnvironmentVariable = "OPENMAUI_RENDER_STATS";

    private static readonly bool s_enabled = IsEnabledByEnvironment();

    private readonly string _label;
    private readonly List<double> _samples = new(512);
    private readonly Stopwatch _interval = Stopwatch.StartNew();
    private long _frameStartTicks;
    private long _totalFrames;

    /// <summary>Reporting interval; 2 seconds is short enough to watch interactively.</summary>
    public static TimeSpan ReportInterval { get; set; } = TimeSpan.FromSeconds(2);

    public static bool Enabled => s_enabled;

    public FrameStatistics(string label)
    {
        _label = label;
    }

    private static bool IsEnabledByEnvironment()
    {
        var v = Environment.GetEnvironmentVariable(EnvironmentVariable);
        return !string.IsNullOrEmpty(v) && v != "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    public void BeginFrame()
    {
        _frameStartTicks = Stopwatch.GetTimestamp();
    }

    public void EndFrame()
    {
        double ms = Stopwatch.GetElapsedTime(_frameStartTicks).TotalMilliseconds;
        _samples.Add(ms);
        _totalFrames++;

        if (_interval.Elapsed >= ReportInterval)
            Report();
    }

    private void Report()
    {
        double seconds = _interval.Elapsed.TotalSeconds;
        int n = _samples.Count;
        if (n > 0)
        {
            _samples.Sort();
            double sum = 0;
            foreach (var s in _samples) sum += s;
            Console.WriteLine(
                $"[RenderStats] {_label}: {n} frames in {seconds:F1}s ({n / seconds:F1} fps rendered) " +
                $"frame ms avg {sum / n:F2} p50 {Percentile(0.50):F2} p95 {Percentile(0.95):F2} p99 {Percentile(0.99):F2} max {_samples[n - 1]:F2} " +
                $"(total {_totalFrames})");
        }
        _samples.Clear();
        _interval.Restart();
    }

    private double Percentile(double p)
    {
        int n = _samples.Count;
        if (n == 0) return 0;
        int index = (int)Math.Ceiling(p * n) - 1;
        return _samples[Math.Clamp(index, 0, n - 1)];
    }
}
