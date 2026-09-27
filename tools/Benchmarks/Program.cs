// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// OpenMaui headless benchmark suite and performance regression gate.
//
//   dotnet run --project tools/Benchmarks -c Release -- [options]
//
//   --runs N               run the whole suite N times; reported values are the per-statistic median
//                          (default 1, 3 with --update-baseline)
//   --trials N             processes per scenario within a run; timing distributions keep the
//                          fastest trial (least disturbed by other load), scalars the median (default 3)
//   --scenario a,b         only scenarios whose id starts with one of these (e.g. scroll,text-1.0x)
//   --no-gpu               skip the GPU (headless EGL) variants
//   --render-node PATH     DRM render node for the GPU variants (default /dev/dri/renderD128)
//   --json FILE            also write the results as JSON
//   --baseline FILE        baseline file (docs/perf-baseline.json)
//   --compare              compare against --baseline; exit 1 when a gated metric regresses
//   --warn-only            with --compare: report regressions but exit 0 (shared CI runners)
//   --update-baseline      write the results (median of --runs, default 3) to --baseline
//   --list                 list the scenarios
//
// Every scenario runs in its own child process (the same executable with
// --child), so JIT state, caches and the GC heap of one scenario cannot
// colour another, and startup is measured cold.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenMaui.Benchmarks;

internal static class Program
{
    /// <summary>Started on entry to Main (startup scenario's in-process clock).</summary>
    public static Stopwatch SinceMain { get; private set; } = null!;

    private const string ResultMarker = "@@BENCH-RESULT ";

    private static int Main(string[] args)
    {
        SinceMain = Stopwatch.StartNew();
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"benchmarks: {ex}");
            return 2;
        }
    }

    private static int Run(string[] args)
    {
        int runs = 0, trials = 3;
        string? filter = null, jsonPath = null, baselinePath = null, child = null;
        string target = "raster";
        string renderNode = "/dev/dri/renderD128";
        bool noGpu = false, compare = false, update = false, warnOnly = false, list = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--runs": runs = int.Parse(args[++i]); break;
                case "--trials": trials = Math.Max(1, int.Parse(args[++i])); break;
                case "--scenario": filter = args[++i]; break;
                case "--no-gpu": noGpu = true; break;
                case "--render-node": renderNode = args[++i]; break;
                case "--json": jsonPath = args[++i]; break;
                case "--baseline": baselinePath = args[++i]; break;
                case "--compare": compare = true; break;
                case "--warn-only": warnOnly = true; break;
                case "--update-baseline": update = true; break;
                case "--list": list = true; break;
                case "--child": child = args[++i]; break;
                case "--target": target = args[++i]; break;
                default: throw new ArgumentException($"unknown option {args[i]} (see the header of tools/Benchmarks/Program.cs)");
            }
        }

        if (child != null)
            return RunChild(child, target, renderNode);

        if (list)
        {
            foreach (var s in Scenarios.All)
                Console.WriteLine($"{s.Id,-12} {(s.SupportsGpu ? "raster+gpu" : "raster    ")}  {s.Description}");
            return 0;
        }

        if ((compare || update) && baselinePath == null)
            throw new ArgumentException("--compare and --update-baseline need --baseline FILE");
        if (runs <= 0) runs = update ? 3 : 1;
        baselinePath = baselinePath != null ? Path.GetFullPath(baselinePath) : null;
        jsonPath = jsonPath != null ? Path.GetFullPath(jsonPath) : null;

        var selected = Scenarios.All
            .Where(s => filter == null || filter.Split(',').Any(f => s.Id.StartsWith(f.Trim(), StringComparison.Ordinal)))
            .ToArray();
        if (selected.Length == 0)
            throw new ArgumentException($"no scenario matches '{filter}'");

        // GPU probe (in this process; the measuring happens in children).
        string? gpu = null, gpuError = "disabled with --no-gpu";
        if (!noGpu)
        {
            using var gl = OffscreenGl.TryCreate(renderNode, out gpuError);
            if (gl != null) gpu = $"{gl.Renderer} ({gl.Platform})";
        }
        var machine = MachineInfo.Describe(gpu);
        Console.WriteLine($"Machine: {machine.Cpu}, {machine.LogicalCores} threads, {machine.MemoryGb} GB; GPU: {gpu ?? "none (" + gpuError + ")"}");
        Console.WriteLine($"         {machine.Os}, kernel {machine.Kernel}, {machine.Runtime}; fingerprint {machine.Fingerprint}");
        Console.WriteLine($"Runs: {runs} x {trials} trials; scenarios: {string.Join(", ", selected.Select(s => s.Id))}");
        Console.WriteLine();

        var perRun = new List<SortedDictionary<string, Metric>>();
        var notes = new List<string>();
        for (int r = 0; r < runs; r++)
        {
            var metrics = new SortedDictionary<string, Metric>(StringComparer.Ordinal);
            foreach (var scenario in selected)
            {
                var targets = scenario.SupportsGpu && gpu != null ? new[] { "raster", "gpu" } : new[] { "raster" };
                foreach (var t in targets)
                {
                    var sw = Stopwatch.StartNew();
                    Console.Write($"[run {r + 1}/{runs}] {scenario.Id}/{t} ... ");
                    var result = scenario.Id == "startup" ? RunStartup(renderNode) : RunTrials(scenario.Id, t, renderNode, trials);
                    foreach (var (k, v) in result)
                        metrics[$"{scenario.Id}/{t}/{k}"] = v;
                    Console.WriteLine($"{sw.Elapsed.TotalSeconds:0.0}s");
                }
            }
            perRun.Add(metrics);
        }
        if (gpu == null)
            notes.Add($"GPU variants skipped: {gpuError}");

        var suite = new SuiteResult
        {
            Machine = machine,
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Runs = runs,
            Notes = notes,
        };
        foreach (var key in perRun[0].Keys)
        {
            var values = perRun.Where(m => m.ContainsKey(key)).Select(m => m[key]).ToList();
            suite.Metrics[key] = Round(runs == 1 ? values[0] : Metric.Median(values));
        }

        Console.WriteLine();
        Report.PrintTable(suite);
        if (runs > 1)
            Report.PrintVariance(perRun);
        foreach (var n in notes) Console.WriteLine($"Note: {n}");

        if (jsonPath != null)
        {
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(suite, Json.Options) + "\n");
            Console.WriteLine($"JSON written to {jsonPath}");
        }

        int exit = 0;
        if (compare)
        {
            var baseline = JsonSerializer.Deserialize<BaselineFile>(File.ReadAllText(baselinePath!), Json.Options)
                ?? throw new InvalidDataException($"{baselinePath} is empty");
            bool regressed = Report.Compare(baseline, suite);
            if (regressed)
            {
                Console.WriteLine(warnOnly ? "Performance regression detected (warn-only: not failing)." : "Performance regression detected.");
                if (!warnOnly) exit = 1;
            }
            else
            {
                Console.WriteLine("No gated metric regressed.");
            }
        }

        if (update)
        {
            var gates = BaselineFile.DefaultGates();
            if (File.Exists(baselinePath!))
            {
                var existing = JsonSerializer.Deserialize<BaselineFile>(File.ReadAllText(baselinePath!), Json.Options);
                if (existing?.Gates is { Count: > 0 } g) gates = g; // keep hand-tuned thresholds
            }
            var file = new BaselineFile
            {
                Machine = machine,
                Timestamp = suite.Timestamp,
                Runs = runs,
                Gates = gates,
                Metrics = suite.Metrics,
            };
            File.WriteAllText(baselinePath!, JsonSerializer.Serialize(file, Json.Options) + "\n");
            Console.WriteLine($"Baseline written to {baselinePath} ({suite.Metrics.Count} metrics, median of {runs} runs).");
        }
        return exit;
    }

    private static Metric Round(Metric m)
    {
        static double R(double v) => Math.Round(v, Math.Abs(v) >= 100 ? 1 : 4);
        return new Metric { Unit = m.Unit, N = m.N, Avg = R(m.Avg), P50 = R(m.P50), P95 = R(m.P95), P99 = R(m.P99), Max = R(m.Max) };
    }

    /// <summary>
    /// Runs a scenario in <paramref name="trials"/> fresh processes. Other load
    /// on the machine only ever makes a trial slower, so for timing
    /// distributions the trial with the lowest p50 is the best estimate of the
    /// code's own cost (its whole distribution is kept, so percentiles stay
    /// consistent); counts and memory take the median across trials.
    /// </summary>
    private static Dictionary<string, Metric> RunTrials(string scenario, string target, string renderNode, int trials)
    {
        var all = new List<Dictionary<string, Metric>>();
        for (int i = 0; i < trials; i++)
            all.Add(RunChildProcess(scenario, target, renderNode));
        var result = new Dictionary<string, Metric>();
        foreach (var key in all[0].Keys)
        {
            var values = all.Where(d => d.ContainsKey(key)).Select(d => d[key]).ToList();
            bool timing = !values[0].IsScalar && values[0].Unit is "ms" or "us";
            result[key] = timing ? values.MinBy(m => m.P50)! : Metric.Median(values);
        }
        return result;
    }

    /// <summary>Startup: 2 discarded + 10 measured cold processes, aggregated into distributions.</summary>
    private static Dictionary<string, Metric> RunStartup(string renderNode)
    {
        const int Discard = 2, Measured = 10;
        var samples = new Dictionary<string, List<double>>();
        string unit = "ms";
        for (int i = 0; i < Discard + Measured; i++)
        {
            var one = RunChildProcess("startup", "raster", renderNode);
            if (i < Discard) continue;
            foreach (var (k, v) in one)
            {
                if (!samples.TryGetValue(k, out var list)) samples[k] = list = new List<double>();
                list.Add(v.P50);
                unit = v.Unit;
            }
        }
        return samples.ToDictionary(kv => kv.Key, kv => Metric.FromSamples(kv.Value, unit));
    }

    private static Dictionary<string, Metric> RunChildProcess(string scenario, string target, string renderNode)
    {
        var psi = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // Re-launch this executable (apphost), or `dotnet <dll>` when started that way.
        var self = Environment.ProcessPath!;
        if (Path.GetFileNameWithoutExtension(self) == "dotnet")
        {
            psi.FileName = self;
            psi.ArgumentList.Add(typeof(Program).Assembly.Location);
        }
        else
        {
            psi.FileName = self;
        }
        foreach (var a in new[] { "--child", scenario, "--target", target, "--render-node", renderNode })
            psi.ArgumentList.Add(a);
        // No desktop: the suite must not depend on (or open) a display.
        psi.Environment.Remove("WAYLAND_DISPLAY");
        psi.Environment.Remove("DISPLAY");
        psi.Environment["OPENMAUI_REDUCE_MOTION"] = "0"; // animations on regardless of desktop settings
        psi.Environment.Remove("OPENMAUI_RENDER_STATS");

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("could not start child process");
        var stderr = p.StandardError.ReadToEndAsync();
        string? resultLine = null;
        var stdout = new StringBuilder();
        string? line;
        while ((line = p.StandardOutput.ReadLine()) != null)
        {
            if (line.StartsWith(ResultMarker, StringComparison.Ordinal)) resultLine = line[ResultMarker.Length..];
            else stdout.AppendLine(line);
        }
        if (!p.WaitForExit(TimeSpan.FromMinutes(5)))
        {
            p.Kill(true);
            throw new TimeoutException($"{scenario}/{target} did not finish in 5 minutes");
        }
        if (p.ExitCode != 0 || resultLine == null)
            throw new InvalidOperationException($"{scenario}/{target} failed (exit {p.ExitCode}):\n{stdout}{stderr.Result}");
        return JsonSerializer.Deserialize<Dictionary<string, Metric>>(resultLine, Json.Options)!;
    }

    private static int RunChild(string id, string target, string renderNode)
    {
        var scenario = Scenarios.All.FirstOrDefault(s => s.Id == id)
            ?? throw new ArgumentException($"unknown scenario {id}");
        OffscreenGl? gl = null;
        var kind = TargetKind.Raster;
        if (target == "gpu")
        {
            gl = OffscreenGl.TryCreate(renderNode, out var error)
                ?? throw new InvalidOperationException($"GPU target unavailable: {error}");
            kind = TargetKind.Gpu;
        }
        var metrics = scenario.Run(kind, gl);
        Console.Out.Flush();
        Console.WriteLine(ResultMarker + JsonSerializer.Serialize(metrics, Json.Compact));
        Console.Out.Flush();
        // Skip teardown of native/global state (GL context, GLib); the process ends here.
        Environment.Exit(0);
        return 0;
    }
}

internal static class MachineInfo
{
    public static Machine Describe(string? gpu)
    {
        var m = new Machine
        {
            Cpu = ReadField("/proc/cpuinfo", "model name") ?? RuntimeInformation.ProcessArchitecture.ToString(),
            LogicalCores = Environment.ProcessorCount,
            MemoryGb = (int)Math.Round((ParseKb(ReadField("/proc/meminfo", "MemTotal")) ?? 0) / 1024.0 / 1024.0),
            Gpu = gpu ?? "none",
            Os = ReadOsRelease() ?? RuntimeInformation.OSDescription,
            Kernel = TryRead("/proc/sys/kernel/osrelease")?.Trim() ?? "",
            Runtime = RuntimeInformation.FrameworkDescription,
        };
        // CPU and memory only: an OS or runtime update should show up in the
        // numbers, not silence the comparison. The GPU is compared separately
        // (it is absent under --no-gpu, which only drops the GPU variants).
        var basis = $"{m.Cpu}|{m.LogicalCores}|{m.MemoryGb}";
        m.Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(basis)))[..12].ToLowerInvariant();
        return m;
    }

    private static string? TryRead(string path)
    {
        try { return File.ReadAllText(path); } catch { return null; }
    }

    private static string? ReadField(string path, string key)
    {
        var text = TryRead(path);
        if (text == null) return null;
        foreach (var line in text.Split('\n'))
        {
            int colon = line.IndexOf(':');
            if (colon > 0 && line[..colon].Trim() == key)
                return line[(colon + 1)..].Trim();
        }
        return null;
    }

    private static double? ParseKb(string? value)
    {
        if (value == null) return null;
        var digits = new string(value.TakeWhile(c => char.IsDigit(c)).ToArray());
        return double.TryParse(digits, out var kb) ? kb : null;
    }

    private static string? ReadOsRelease()
    {
        var text = TryRead("/etc/os-release");
        var line = text?.Split('\n').FirstOrDefault(l => l.StartsWith("PRETTY_NAME=", StringComparison.Ordinal));
        return line?["PRETTY_NAME=".Length..].Trim('"');
    }
}
