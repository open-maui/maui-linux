// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenMaui.Benchmarks;

/// <summary>
/// One measured quantity. Distribution metrics (frame times) fill every
/// statistic; scalar metrics (frame counts, memory) set all of them to the
/// same value so gates can address any statistic uniformly.
/// </summary>
internal sealed class Metric
{
    public string Unit { get; set; } = "";
    public int N { get; set; }
    public double Avg { get; set; }
    public double P50 { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
    public double Max { get; set; }

    [JsonIgnore]
    public bool IsScalar => N <= 1;

    public double Get(string stat) => stat switch
    {
        "avg" => Avg,
        "p50" => P50,
        "p95" => P95,
        "p99" => P99,
        "max" => Max,
        "value" => P50,
        _ => throw new ArgumentException($"unknown statistic '{stat}' (avg, p50, p95, p99, max, value)"),
    };

    public static Metric Scalar(double value, string unit) =>
        new() { Unit = unit, N = 1, Avg = value, P50 = value, P95 = value, P99 = value, Max = value };

    public static Metric FromSamples(IReadOnlyList<double> samples, string unit)
    {
        if (samples.Count == 0) return Scalar(0, unit);
        var sorted = samples.OrderBy(x => x).ToArray();
        return new Metric
        {
            Unit = unit,
            N = sorted.Length,
            Avg = sorted.Average(),
            P50 = Percentile(sorted, 0.50),
            P95 = Percentile(sorted, 0.95),
            P99 = Percentile(sorted, 0.99),
            Max = sorted[^1],
        };
    }

    /// <summary>
    /// Statistics over <paramref name="blocks"/> consecutive blocks of the
    /// samples, combined by taking each statistic's median across blocks; max
    /// stays the overall maximum. A burst of interference from other processes
    /// (a parallel build, a compositor frame) lands in one or two blocks and
    /// is voted out, which makes repeated runs agree far better than a single
    /// distribution over all samples on a busy machine.
    /// </summary>
    public static Metric FromBlocks(IReadOnlyList<double> samples, string unit, int blocks = 5)
    {
        if (samples.Count < blocks * 10) return FromSamples(samples, unit);
        int size = samples.Count / blocks;
        var parts = new List<Metric>(blocks);
        for (int b = 0; b < blocks; b++)
            parts.Add(FromSamples(samples.Skip(b * size).Take(b == blocks - 1 ? samples.Count - b * size : size).ToList(), unit));
        var m = Median(parts);
        m.N = samples.Count;
        m.Max = samples.Max();
        return m;
    }

    /// <summary>Nearest-rank percentile (same definition as FrameStatistics).</summary>
    private static double Percentile(double[] sorted, double p)
    {
        int index = (int)Math.Ceiling(p * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    /// <summary>Per-statistic median over several runs (the baseline value).</summary>
    public static Metric Median(IReadOnlyList<Metric> runs)
    {
        static double Med(IEnumerable<double> xs)
        {
            var a = xs.OrderBy(x => x).ToArray();
            return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2;
        }
        return new Metric
        {
            Unit = runs[0].Unit,
            N = runs[0].N,
            Avg = Med(runs.Select(r => r.Avg)),
            P50 = Med(runs.Select(r => r.P50)),
            P95 = Med(runs.Select(r => r.P95)),
            P99 = Med(runs.Select(r => r.P99)),
            Max = Med(runs.Select(r => r.Max)),
        };
    }
}

/// <summary>Metrics of one suite run, keyed "scenario/target/measure".</summary>
internal sealed class SuiteResult
{
    public Machine Machine { get; set; } = new();
    public string Timestamp { get; set; } = "";
    public int Runs { get; set; } = 1;
    public SortedDictionary<string, Metric> Metrics { get; set; } = new(StringComparer.Ordinal);
    public List<string> Notes { get; set; } = new();
}

/// <summary>Machine description; the fingerprint decides whether numbers are comparable.</summary>
internal sealed class Machine
{
    public string Cpu { get; set; } = "";
    public int LogicalCores { get; set; }
    public int MemoryGb { get; set; }
    public string Gpu { get; set; } = "";
    public string Os { get; set; } = "";
    public string Kernel { get; set; } = "";
    public string Runtime { get; set; } = "";
    public string Fingerprint { get; set; } = "";
}

/// <summary>A gate: metrics matching <see cref="Metric"/> (glob with '*') must not regress.</summary>
internal sealed class Gate
{
    [JsonPropertyName("metric")] public string Pattern { get; set; } = "";
    public string Stat { get; set; } = "p95";
    /// <summary>Fail when current &gt; baseline * (1 + pct/100) ...</summary>
    public double? MaxIncreasePct { get; set; }
    /// <summary>... and the increase is also larger than this (absorbs noise on tiny values).</summary>
    public double MinAbsDelta { get; set; }
    /// <summary>Fail when current &gt; this, regardless of the baseline (e.g. 0 idle frames).</summary>
    public double? MaxAbsolute { get; set; }

    public bool Matches(string metric)
    {
        var rx = "^" + System.Text.RegularExpressions.Regex.Escape(Pattern).Replace("\\*", ".*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(metric, rx);
    }
}

internal sealed class BaselineFile
{
    public int Schema { get; set; } = 1;
    public string Description { get; set; } =
        "OpenMaui performance baseline (tools/Benchmarks). Values are the per-statistic median of Runs suite runs on Machine. " +
        "Gates apply in order; the first gate whose metric pattern matches decides. See docs/PERFORMANCE.md.";
    public Machine Machine { get; set; } = new();
    public string Timestamp { get; set; } = "";
    public int Runs { get; set; }
    public List<Gate> Gates { get; set; } = new();
    public SortedDictionary<string, Metric> Metrics { get; set; } = new(StringComparer.Ordinal);

    public static List<Gate> DefaultGates() => new()
    {
        // Rendering must stop when nothing changes.
        new Gate { Pattern = "idle/*/frames", Stat = "value", MaxAbsolute = 0 },
        new Gate { Pattern = "idle/*/loop_us", Stat = "p50", MaxIncreasePct = 25, MinAbsDelta = 20 },
        // Virtualisation: realised item views must stay proportional to what was scrolled past.
        new Gate { Pattern = "scroll-*/*/realised", Stat = "value", MaxIncreasePct = 10, MinAbsDelta = 5 },
        // GPU timings include the driver and share the GPU with the desktop
        // compositor: run-to-run p95 spread is about twice the CPU raster one.
        new Gate { Pattern = "*/gpu/frame_ms", Stat = "p95", MaxIncreasePct = 30, MinAbsDelta = 0.5 },
        new Gate { Pattern = "*/frame_ms", Stat = "p95", MaxIncreasePct = 15, MinAbsDelta = 0.25 },
        new Gate { Pattern = "resize/gpu/latency_ms", Stat = "p95", MaxIncreasePct = 30, MinAbsDelta = 0.5 },
        new Gate { Pattern = "resize/*/latency_ms", Stat = "p95", MaxIncreasePct = 15, MinAbsDelta = 0.25 },
        new Gate { Pattern = "layout-*/*/ms", Stat = "p95", MaxIncreasePct = 25, MinAbsDelta = 0.25 },
        new Gate { Pattern = "startup/*/first_frame_ms", Stat = "p50", MaxIncreasePct = 20, MinAbsDelta = 10 },
        new Gate { Pattern = "startup/*/build_ms", Stat = "p50", MaxIncreasePct = 20, MinAbsDelta = 10 },
        new Gate { Pattern = "memory/*/*_mb", Stat = "value", MaxIncreasePct = 20, MinAbsDelta = 2 },
        new Gate { Pattern = "animation/*/stalled_ticks", Stat = "value", MaxAbsolute = 0 },
    };
}

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public static readonly JsonSerializerOptions Compact = new(Options) { WriteIndented = false };
}
