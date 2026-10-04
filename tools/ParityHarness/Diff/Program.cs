// ParityHarness.Diff: compares two ParityHarness dump folders (reference vs candidate,
// normally WinUI vs OpenMaui) and reports, per page, the elements whose frames differ
// beyond a tolerance and the elements present on one side only.
//
//   dotnet run --project tools/ParityHarness/Diff -- <reference-dir> <candidate-dir> [options]
//
// Options:
//   --tol <px>            tolerance for non-text elements (default 1)
//   --text-tol <px>       tolerance for text-bearing elements (default 16)
//   --text-scope <s>      which elements get the text tolerance:
//                           self       the text element itself (Label, Button, Entry, ...)
//                           ancestors  also every element containing text (default)
//   --channel <c>         frame | native | both (default both). "frame" is the MAUI layout
//                         frame accumulated to the page, "native" the platform view's rect.
//   --pages a,b           only these pages
//   --allow-missing       elements/pages on one side only do not fail the run
//   --out <file.md>       write the markdown report here (default: stdout only)
//   --json <file.json>    also write a machine-readable summary
//   --max-rows <n>        rows per page table (default 200)
//   --images <dir>        compare the pages' screenshots too: writes a reference |
//                         candidate | difference image per page to <dir> and a visual
//                         section to the report (informational: text rasterises
//                         differently per platform, so it never fails the run)
//
// Exit code: 0 = within tolerance, 1 = differences over tolerance (or missing elements
// without --allow-missing, or a dump error), 2 = usage / input error.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParityHarness.Diff;

internal sealed record RectDto(double X, double Y, double W, double H);

internal sealed class ElementRecord
{
    public string Key { get; set; } = "";
    public string? AutomationId { get; set; }
    public string Type { get; set; } = "";
    public string? ParentKey { get; set; }
    public int Depth { get; set; }
    public bool IsVisible { get; set; }
    public bool Text { get; set; }
    public bool ContainsText { get; set; }
    public bool TextDependent { get; set; }
    public RectDto? Frame { get; set; }
    public RectDto? Native { get; set; }
}

internal sealed class SizeDto
{
    public double W { get; set; }
    public double H { get; set; }
    public override string ToString() => $"{Fmt(W)}x{Fmt(H)}";
    private static string Fmt(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}

internal sealed class PageDump
{
    public string Page { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Os { get; set; } = "";
    public SizeDto? Requested { get; set; }
    public SizeDto? PageSize { get; set; }
    public bool Settled { get; set; }
    public string? Error { get; set; }
    public string? Warning { get; set; }
    public string? Screenshot { get; set; }
    public List<ElementRecord> Elements { get; set; } = new();
}

internal sealed class Options
{
    public string Reference = "";
    public string Candidate = "";
    public double Tol = 1;
    public double TextTol = 16;
    public bool TextAncestors = true;
    public bool Frame = true;
    public bool Native = true;
    public HashSet<string>? Pages;
    public bool AllowMissing;
    public string? Out;
    public string? Json;
    public int MaxRows = 200;
    public string? Images;
}

internal sealed record Difference(
    string Key, string Type, string Channel, RectDto? Reference, RectDto? Candidate,
    double MaxDelta, double Tolerance, bool TextTolerance, string? Note);

internal sealed class PageResult
{
    public string Page = "";
    public int Compared;
    public int WithinTolerance;
    public int NonZeroWithinTolerance;
    public List<Difference> Over = new();
    public List<ElementRecord> ReferenceOnly = new();
    public List<ElementRecord> CandidateOnly = new();
    public List<string> Notes = new();
    public bool MissingPage;
    public bool DumpError;
    public bool Failed(Options o) =>
        Over.Count > 0 || DumpError || (!o.AllowMissing && (MissingPage || ReferenceOnly.Count > 0 || CandidateOnly.Count > 0));
}

public static class Program
{
    private static readonly JsonSerializerOptions JsonIn = new() { PropertyNameCaseInsensitive = true };

    public static int Main(string[] args)
    {
        Options o;
        try
        {
            o = ParseArgs(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("usage: ParityHarness.Diff <reference-dir> <candidate-dir> [--tol px] [--text-tol px] [--text-scope self|ancestors] [--channel frame|native|both] [--pages a,b] [--allow-missing] [--out report.md] [--json report.json] [--max-rows n] [--images dir]");
            return 2;
        }

        if (!Directory.Exists(o.Reference) || !Directory.Exists(o.Candidate))
        {
            Console.Error.WriteLine($"dump folder not found: {(Directory.Exists(o.Reference) ? o.Candidate : o.Reference)}");
            return 2;
        }

        var refPages = LoadPages(o.Reference);
        var candPages = LoadPages(o.Candidate);
        if (refPages.Count == 0 || candPages.Count == 0)
        {
            Console.Error.WriteLine("no page dumps (*.json) found in " + (refPages.Count == 0 ? o.Reference : o.Candidate));
            return 2;
        }

        string refName = PlatformOf(refPages, "reference");
        string candName = PlatformOf(candPages, "candidate");

        var names = refPages.Keys.Union(candPages.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(n => o.Pages == null || o.Pages.Contains(n))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = new List<PageResult>();
        foreach (var name in names)
        {
            refPages.TryGetValue(name, out var r);
            candPages.TryGetValue(name, out var c);
            results.Add(ComparePage(name, r, c, o, refName, candName));
        }

        string report = BuildReport(results, o, refName, candName);
        if (o.Images != null)
            report += Visual.Compare(names, refPages, candPages, o, refName, candName);
        Console.WriteLine(report);
        if (o.Out != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(o.Out))!);
            File.WriteAllText(o.Out, report);
        }
        if (o.Json != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(o.Json))!);
            File.WriteAllText(o.Json, BuildJson(results, o, refName, candName));
        }

        return results.Any(p => p.Failed(o)) ? 1 : 0;
    }

    private static Options ParseArgs(string[] args)
    {
        var o = new Options();
        var positional = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{a} needs a value");
            double Num() => double.TryParse(Next(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0
                ? v : throw new ArgumentException($"{a} needs a non-negative number");
            switch (a)
            {
                case "--tol": o.Tol = Num(); break;
                case "--text-tol": o.TextTol = Num(); break;
                case "--text-scope":
                    o.TextAncestors = Next() switch
                    {
                        "self" => false,
                        "ancestors" => true,
                        var s => throw new ArgumentException($"--text-scope: unknown value '{s}'"),
                    };
                    break;
                case "--channel":
                    switch (Next())
                    {
                        case "frame": o.Frame = true; o.Native = false; break;
                        case "native": o.Frame = false; o.Native = true; break;
                        case "both": o.Frame = o.Native = true; break;
                        default: throw new ArgumentException("--channel: frame, native or both");
                    }
                    break;
                case "--pages":
                    o.Pages = new HashSet<string>(Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
                    break;
                case "--allow-missing": o.AllowMissing = true; break;
                case "--out": o.Out = Next(); break;
                case "--json": o.Json = Next(); break;
                case "--max-rows": o.MaxRows = (int)Num(); break;
                case "--images": o.Images = Next(); break;
                default:
                    if (a.StartsWith("--", StringComparison.Ordinal))
                        throw new ArgumentException($"unknown option {a}");
                    positional.Add(a);
                    break;
            }
        }
        if (positional.Count != 2)
            throw new ArgumentException("expected <reference-dir> <candidate-dir>");
        o.Reference = positional[0];
        o.Candidate = positional[1];
        return o;
    }

    private static Dictionary<string, PageDump> LoadPages(string dir)
    {
        var pages = new Dictionary<string, PageDump>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            if (Path.GetFileName(file).Equals("index.json", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                var dump = JsonSerializer.Deserialize<PageDump>(File.ReadAllText(file), JsonIn);
                if (dump != null)
                    pages[string.IsNullOrEmpty(dump.Page) ? Path.GetFileNameWithoutExtension(file) : dump.Page] = dump;
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"skipping {file}: {ex.Message}");
            }
        }
        return pages;
    }

    private static string PlatformOf(Dictionary<string, PageDump> pages, string fallback)
        => pages.Values.Select(p => p.Platform).FirstOrDefault(p => !string.IsNullOrEmpty(p)) ?? fallback;

    private static PageResult ComparePage(string name, PageDump? r, PageDump? c, Options o, string refName, string candName)
    {
        var result = new PageResult { Page = name };
        if (r == null || c == null)
        {
            result.MissingPage = true;
            result.Notes.Add($"page dumped by {(r == null ? candName : refName)} only");
            return result;
        }

        foreach (var (dump, who) in new[] { (r, refName), (c, candName) })
        {
            if (dump.Error != null)
            {
                result.DumpError = true;
                result.Notes.Add($"{who} dump error: {dump.Error}");
            }
            if (dump.Warning != null)
                result.Notes.Add($"{who} warning: {dump.Warning}");
            if (!dump.Settled)
                result.Notes.Add($"{who} layout did not settle");
        }
        if (r.PageSize != null && c.PageSize != null && (Math.Abs(r.PageSize.W - c.PageSize.W) > 0.5 || Math.Abs(r.PageSize.H - c.PageSize.H) > 0.5))
            result.Notes.Add($"page size differs: {refName} {r.PageSize}, {candName} {c.PageSize}");

        var refByKey = r.Elements.GroupBy(e => e.Key).ToDictionary(g => g.Key, g => g.First());
        var candByKey = c.Elements.GroupBy(e => e.Key).ToDictionary(g => g.Key, g => g.First());

        foreach (var re in r.Elements)
        {
            if (!candByKey.TryGetValue(re.Key, out var ce))
            {
                result.ReferenceOnly.Add(re);
                continue;
            }

            result.Compared++;
            bool textual = re.Text || ce.Text || re.TextDependent || ce.TextDependent || (o.TextAncestors && (re.ContainsText || ce.ContainsText));
            double tol = textual ? Math.Max(o.TextTol, o.Tol) : o.Tol;
            bool over = false, nonZero = false;

            if (re.IsVisible != ce.IsVisible)
            {
                result.Over.Add(new Difference(re.Key, re.Type, "visible", null, null, double.NaN, tol, textual,
                    $"IsVisible {refName}={re.IsVisible}, {candName}={ce.IsVisible}"));
                over = true;
            }

            if (re.Type != ce.Type)
                result.Notes.Add($"{re.Key}: type {refName}={re.Type}, {candName}={ce.Type}");

            if (o.Frame)
                Check("frame", re.Frame, ce.Frame);
            if (o.Native)
                Check("native", re.Native, ce.Native);

            if (!over) result.WithinTolerance++;
            if (!over && nonZero) result.NonZeroWithinTolerance++;

            void Check(string channel, RectDto? a, RectDto? b)
            {
                if (a == null && b == null) return;
                if (a == null || b == null)
                {
                    // A platform that cannot report this channel for the element: noted, not failed.
                    result.Notes.Add($"{re.Key}: no {channel} rect on {(a == null ? refName : candName)}");
                    return;
                }
                double d = MaxDelta(a, b);
                if (d > tol)
                {
                    result.Over.Add(new Difference(re.Key, re.Type, channel, a, b, d, tol, textual, null));
                    over = true;
                }
                else if (d > 0)
                {
                    nonZero = true;
                }
            }
        }

        foreach (var ce in c.Elements)
            if (!refByKey.ContainsKey(ce.Key))
                result.CandidateOnly.Add(ce);

        return result;
    }

    private static double MaxDelta(RectDto a, RectDto b)
        => new[] { Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y), Math.Abs(a.W - b.W), Math.Abs(a.H - b.H) }.Max();

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string R(RectDto? r) => r == null ? "-" : $"{F(r.X)}, {F(r.Y)} {F(r.W)}x{F(r.H)}";

    private static string Delta(RectDto? a, RectDto? b)
    {
        if (a == null || b == null) return "-";
        static string S(double v) => (v > 0 ? "+" : "") + F(v);
        var parts = new List<string>();
        if (b.X != a.X) parts.Add("x" + S(b.X - a.X));
        if (b.Y != a.Y) parts.Add("y" + S(b.Y - a.Y));
        if (b.W != a.W) parts.Add("w" + S(b.W - a.W));
        if (b.H != a.H) parts.Add("h" + S(b.H - a.H));
        return string.Join(" ", parts);
    }

    private static string Cell(string s) => s.Replace("|", "\\|");

    private static string BuildReport(List<PageResult> results, Options o, string refName, string candName)
    {
        var sb = new StringBuilder();
        int failed = results.Count(p => p.Failed(o));
        sb.AppendLine("# Layout parity report");
        sb.AppendLine();
        sb.AppendLine($"Reference: **{refName}** (`{o.Reference}`)  ");
        sb.AppendLine($"Candidate: **{candName}** (`{o.Candidate}`)  ");
        sb.AppendLine($"Tolerance: {F(o.Tol)} for layout, {F(o.TextTol)} for text-bearing elements ({(o.TextAncestors ? "text elements and their containers" : "text elements only")}); channels: {(o.Frame && o.Native ? "frame + native" : o.Frame ? "frame" : "native")}  ");
        sb.AppendLine($"Result: **{(failed == 0 ? "PASS" : $"FAIL ({failed} of {results.Count} pages)")}**");
        sb.AppendLine();
        sb.AppendLine($"| Page | Compared | Over tolerance | Within tol. (non-zero) | Reference only ({refName}) | Candidate only ({candName}) | Status |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---|");
        foreach (var p in results)
        {
            sb.AppendLine($"| [{p.Page}](#{p.Page.ToLowerInvariant()}) | {p.Compared} | {p.Over.Count} | {p.WithinTolerance} ({p.NonZeroWithinTolerance}) | {p.ReferenceOnly.Count} | {p.CandidateOnly.Count} | {(p.Failed(o) ? "FAIL" : "ok")} |");
        }
        sb.AppendLine();

        foreach (var p in results)
        {
            sb.AppendLine($"## {p.Page}");
            sb.AppendLine();
            if (p.Over.Count == 0 && p.ReferenceOnly.Count == 0 && p.CandidateOnly.Count == 0 && p.Notes.Count == 0)
            {
                sb.AppendLine($"All {p.Compared} elements within tolerance.");
                sb.AppendLine();
                continue;
            }

            foreach (var n in p.Notes.Distinct())
                sb.AppendLine($"- {Cell(n)}");
            if (p.Notes.Count > 0) sb.AppendLine();

            if (p.Over.Count > 0)
            {
                sb.AppendLine("Over tolerance (rects are x, y WxH relative to the page; delta is candidate minus reference):");
                sb.AppendLine();
                sb.AppendLine($"| Element | Type | Channel | Reference ({refName}) | Candidate ({candName}) | Delta | Tol |");
                sb.AppendLine("|---|---|---|---|---|---|---:|");
                foreach (var d in p.Over.Take(o.MaxRows))
                {
                    string delta = d.Note ?? Delta(d.Reference, d.Candidate);
                    sb.AppendLine($"| `{Cell(d.Key)}` | {d.Type} | {d.Channel} | {R(d.Reference)} | {R(d.Candidate)} | {Cell(delta)} | {F(d.Tolerance)}{(d.TextTolerance ? " (text)" : "")} |");
                }
                if (p.Over.Count > o.MaxRows)
                    sb.AppendLine($"| ... {p.Over.Count - o.MaxRows} more | | | | | | |");
                sb.AppendLine();
            }

            void Only(List<ElementRecord> list, string who)
            {
                if (list.Count == 0) return;
                sb.AppendLine($"Only in {who}:");
                sb.AppendLine();
                sb.AppendLine("| Element | Type | Frame | Native |");
                sb.AppendLine("|---|---|---|---|");
                foreach (var e in list.Take(o.MaxRows))
                    sb.AppendLine($"| `{Cell(e.Key)}` | {e.Type} | {R(e.Frame)} | {R(e.Native)} |");
                if (list.Count > o.MaxRows)
                    sb.AppendLine($"| ... {list.Count - o.MaxRows} more | | | |");
                sb.AppendLine();
            }
            Only(p.ReferenceOnly, $"reference ({refName})");
            Only(p.CandidateOnly, $"candidate ({candName})");
        }
        return sb.ToString();
    }

    private static string BuildJson(List<PageResult> results, Options o, string refName, string candName)
    {
        var doc = new
        {
            reference = refName,
            candidate = candName,
            tolerance = o.Tol,
            textTolerance = o.TextTol,
            passed = !results.Any(p => p.Failed(o)),
            pages = results.Select(p => new
            {
                page = p.Page,
                failed = p.Failed(o),
                compared = p.Compared,
                over = p.Over.Select(d => new { key = d.Key, type = d.Type, channel = d.Channel, reference = d.Reference, candidate = d.Candidate, maxDelta = double.IsNaN(d.MaxDelta) ? (double?)null : d.MaxDelta, tolerance = d.Tolerance, note = d.Note }),
                referenceOnly = p.ReferenceOnly.Select(e => e.Key),
                candidateOnly = p.CandidateOnly.Select(e => e.Key),
                notes = p.Notes.Distinct(),
            }),
        };
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
    }
}
