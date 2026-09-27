// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace OpenMaui.Benchmarks;

internal static class Report
{
    private static string F(double v) => Math.Abs(v) >= 100 ? v.ToString("0.0", CultureInfo.InvariantCulture)
        : Math.Abs(v) >= 10 ? v.ToString("0.00", CultureInfo.InvariantCulture)
        : v.ToString("0.000", CultureInfo.InvariantCulture);

    public static void PrintTable(SuiteResult suite)
    {
        Console.WriteLine($"{"metric",-42} {"unit",-7} {"n",5} {"avg",9} {"p50",9} {"p95",9} {"p99",9} {"max",9}");
        Console.WriteLine(new string('-', 105));
        foreach (var (key, m) in suite.Metrics)
        {
            if (m.IsScalar)
                Console.WriteLine($"{key,-42} {m.Unit,-7} {"",5} {F(m.P50),9}");
            else
                Console.WriteLine($"{key,-42} {m.Unit,-7} {m.N,5} {F(m.Avg),9} {F(m.P50),9} {F(m.P95),9} {F(m.P99),9} {F(m.Max),9}");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Spread of each distribution metric's p50 and p95 across runs:
    /// (max - min) / median, the "repeated runs vary by X %" figure.
    /// </summary>
    public static void PrintVariance(List<SortedDictionary<string, Metric>> runs)
    {
        Console.WriteLine($"Run-to-run spread over {runs.Count} runs ((max - min) / median):");
        Console.WriteLine($"{"metric",-42} {"p50 spread",11} {"p95 spread",11}   p50 per run");
        var p50Spreads = new List<double>();
        var p95Spreads = new List<double>();
        foreach (var key in runs[0].Keys)
        {
            var ms = runs.Where(r => r.ContainsKey(key)).Select(r => r[key]).ToList();
            if (ms.Count != runs.Count) continue;
            double s50 = Spread(ms.Select(m => m.P50).ToList());
            double s95 = Spread(ms.Select(m => m.P95).ToList());
            if (ms[0].Unit is "ms" or "us" or "MB") { p50Spreads.Add(s50); if (!ms[0].IsScalar) p95Spreads.Add(s95); }
            Console.WriteLine($"{key,-42} {s50,10:0.0}% {(ms[0].IsScalar ? "" : s95.ToString("0.0", CultureInfo.InvariantCulture) + "%"),11}   {string.Join(" / ", ms.Select(m => F(m.P50)))}");
        }
        if (p50Spreads.Count > 0)
        {
            p50Spreads.Sort();
            Console.WriteLine($"Timing/memory metrics: median p50 spread {p50Spreads[p50Spreads.Count / 2]:0.0}%, worst {p50Spreads[^1]:0.0}%" +
                              (p95Spreads.Count > 0 ? $"; median p95 spread {p95Spreads.OrderBy(x => x).ElementAt(p95Spreads.Count / 2):0.0}%, worst {p95Spreads.Max():0.0}%" : ""));
        }
        Console.WriteLine();

        static double Spread(List<double> xs)
        {
            var sorted = xs.OrderBy(x => x).ToList();
            double median = sorted[sorted.Count / 2];
            if (median == 0) return sorted[^1] == sorted[0] ? 0 : 100;
            return 100.0 * (sorted[^1] - sorted[0]) / Math.Abs(median);
        }
    }

    /// <summary>Prints the gate table; returns true when any gated metric regressed.</summary>
    public static bool Compare(BaselineFile baseline, SuiteResult current)
    {
        Console.WriteLine($"Comparing against baseline from {baseline.Timestamp} (median of {baseline.Runs} runs)");
        if (baseline.Machine.Fingerprint != current.Machine.Fingerprint)
        {
            Console.WriteLine("WARNING: machine fingerprint differs from the baseline's; timings are not directly comparable.");
            Console.WriteLine($"  baseline: {baseline.Machine.Cpu}, {baseline.Machine.LogicalCores} threads, {baseline.Machine.MemoryGb} GB, GPU {baseline.Machine.Gpu} [{baseline.Machine.Fingerprint}]");
            Console.WriteLine($"  current:  {current.Machine.Cpu}, {current.Machine.LogicalCores} threads, {current.Machine.MemoryGb} GB, GPU {current.Machine.Gpu} [{current.Machine.Fingerprint}]");
            Console.WriteLine("  Record a baseline on this machine with --update-baseline to gate reliably.");
        }
        else if (current.Machine.Gpu != "none" && baseline.Machine.Gpu != current.Machine.Gpu)
        {
            Console.WriteLine($"WARNING: GPU differs from the baseline's ({baseline.Machine.Gpu} vs {current.Machine.Gpu}); the */gpu/* timings are not directly comparable.");
        }
        Console.WriteLine();
        Console.WriteLine($"{"metric",-42} {"stat",-5} {"baseline",10} {"current",10} {"change",9} {"limit",-24} result");
        Console.WriteLine(new string('-', 115));

        bool regressed = false;
        foreach (var (key, baseMetric) in baseline.Metrics)
        {
            var gate = baseline.Gates.FirstOrDefault(g => g.Matches(key));
            if (gate == null) continue;
            if (!current.Metrics.TryGetValue(key, out var cur))
            {
                Console.WriteLine($"{key,-42} {gate.Stat,-5} {F(baseMetric.Get(gate.Stat)),10} {"-",10} {"",9} {"",-24} MISSING (not run)");
                continue;
            }
            double b = baseMetric.Get(gate.Stat), c = cur.Get(gate.Stat);
            double pct = b != 0 ? 100.0 * (c - b) / Math.Abs(b) : (c == 0 ? 0 : double.PositiveInfinity);
            bool fail = false;
            string limit;
            if (gate.MaxAbsolute is double abs)
            {
                limit = $"<= {F(abs)}";
                fail = c > abs;
            }
            else
            {
                double maxPct = gate.MaxIncreasePct ?? 15;
                double allowed = Math.Max(b * (1 + maxPct / 100.0), b + gate.MinAbsDelta);
                limit = $"+{maxPct:0}% (min +{F(gate.MinAbsDelta)}) = {F(allowed)}";
                fail = c > allowed;
            }
            regressed |= fail;
            string change = double.IsInfinity(pct) ? "new" : $"{pct:+0.0;-0.0;0.0}%";
            string status = fail ? "REGRESSED" : pct <= -10 ? "ok (improved)" : "ok";
            Console.WriteLine($"{key,-42} {gate.Stat,-5} {F(b),10} {F(c),10} {change,9} {limit,-24} {status}");
        }

        var added = current.Metrics.Keys.Where(k => !baseline.Metrics.ContainsKey(k)).ToList();
        if (added.Count > 0)
            Console.WriteLine($"Not in the baseline (ungated until --update-baseline): {string.Join(", ", added)}");
        Console.WriteLine();
        return regressed;
    }
}
