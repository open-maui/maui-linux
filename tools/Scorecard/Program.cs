// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Compatibility scorecard generator.
//
//   dotnet run --project tools/Scorecard -- [--trx <file>]... [--out docs/COMPATIBILITY.md] [--run]
//
// Reads one or more TRX test result files (produced by `dotnet test --logger trx`;
// pass --trx once per file, e.g. the main suite and tests/Compat), maps
// every executed test onto the categories in tools/Scorecard/categories.json
// (the same 19 categories Microsoft's maui-labs GTK4 backend publishes), and
// writes a Markdown scorecard where each cell's coverage is COMPUTED from the
// tests that back it: an item counts as covered only when at least one mapped
// test exists and every mapped test passed. --run executes the test suite first.
// The "thirdParty" section of categories.json is reported separately as
// "N of M libraries run unmodified" (fed by tests/Compat).

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

var trxPaths = new List<string>();
string outPath = "docs/COMPATIBILITY.md";
bool run = false;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--trx": trxPaths.Add(args[++i]); break;
        case "--out": outPath = args[++i]; break;
        case "--run": run = true; break;
    }
}

string repoRoot = FindRepoRoot();
string categoriesPath = Path.Combine(repoRoot, "tools", "Scorecard", "categories.json");
if (run)
{
    var trxDir = Path.Combine(Path.GetTempPath(), "openmaui-scorecard");
    Directory.CreateDirectory(trxDir);
    var trxPath = Path.Combine(trxDir, "results.trx");
    trxPaths.Add(trxPath);
    var psi = new ProcessStartInfo("dotnet", $"test \"{Path.Combine(repoRoot, "tests", "OpenMaui.Controls.Linux.Tests.csproj")}\" --nologo -v q --logger \"trx;LogFileName={trxPath}\"")
    { RedirectStandardOutput = true, RedirectStandardError = true };
    using var p = Process.Start(psi)!;
    Console.Write(p.StandardOutput.ReadToEnd());
    p.WaitForExit();
}
if (trxPaths.Count == 0)
{
    Console.Error.WriteLine("No TRX file. Pass --trx <file> (repeatable) or --run.");
    return 2;
}
if (trxPaths.FirstOrDefault(p => !File.Exists(p)) is { } missing)
{
    Console.Error.WriteLine($"TRX file not found: {missing}");
    return 2;
}

// ---- Load tests from every TRX -------------------------------------------------
XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
var tests = new List<TestResult>();
foreach (var path in trxPaths)
{
    var doc = XDocument.Load(path);
    var definitions = doc.Descendants(ns + "UnitTest")
        .ToDictionary(
            u => (string)u.Attribute("id")!,
            u => (Class: (string?)u.Element(ns + "TestMethod")?.Attribute("className") ?? "", Name: (string)u.Attribute("name")!));
    tests.AddRange(doc.Descendants(ns + "UnitTestResult")
        .Where(r => definitions.ContainsKey((string)r.Attribute("testId")!))
        .Select(r =>
        {
            var def = definitions[(string)r.Attribute("testId")!];
            var outcome = (string)r.Attribute("outcome")!;
            // Skipped tests (NotExecuted) carry their skip reason as the message.
            var message = (string?)r.Element(ns + "Output")?.Element(ns + "ErrorInfo")?.Element(ns + "Message");
            return new TestResult(def.Class, def.Name, outcome == "Passed", outcome, message);
        }));
}

// ---- Map onto categories -------------------------------------------------------
var catalog = JsonSerializer.Deserialize<Catalog>(File.ReadAllText(categoriesPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
var sb = new StringBuilder();
sb.AppendLine("# OpenMaui Linux compatibility scorecard");
sb.AppendLine();
sb.AppendLine($"Generated {DateTime.UtcNow:yyyy-MM-dd} by `tools/Scorecard` from {tests.Count} executed tests ({tests.Count(t => t.Passed)} passed) in {trxPaths.Count} result file(s). " +
              "Coverage is computed, not asserted: an item is covered only when at least one mapped test exists and every mapped test passed. " +
              "The categories mirror the table Microsoft publishes for its maui-labs GTK4 backend so the two can be compared row for row.");
sb.AppendLine();
sb.AppendLine("## Implementation parity");
sb.AppendLine();
sb.AppendLine("| Category | Coverage | Items | Tests | Notes |");
sb.AppendLine("|----------|----------|-------|-------|-------|");

var detail = new StringBuilder();
int totalItems = 0, coveredItems = 0;
var unmapped = new HashSet<TestResult>(tests);

foreach (var cat in catalog.Categories)
{
    int items = cat.Items.Count, covered = 0, catTests = 0;
    detail.AppendLine($"### {cat.Name}");
    detail.AppendLine();
    detail.AppendLine("| Item | Status | Tests | Backing tests |");
    detail.AppendLine("|------|--------|-------|---------------|");
    foreach (var item in cat.Items)
    {
        var matched = tests.Where(t => item.Tests.Any(sel => Matches(sel, t))).Distinct().ToList();
        foreach (var m in matched) unmapped.Remove(m);
        bool ok = matched.Count > 0 && matched.All(t => t.Passed);
        string status = matched.Count == 0 ? (item.NotApplicable != null ? "N/A" : "Untested")
                      : ok ? "Covered" : "Failing";
        if (ok || item.NotApplicable != null) covered++;
        catTests += matched.Count;
        var classes = matched.Select(t => t.Class.Split('.').Last()).Distinct().OrderBy(c => c).ToList();
        string backing = matched.Count == 0 ? (item.NotApplicable ?? "-") : string.Join(", ", classes.Select(c => $"`{c}`"));
        detail.AppendLine($"| {item.Name} | {status} | {matched.Count} | {backing} |");
    }
    detail.AppendLine();
    totalItems += items; coveredItems += covered;
    string pct = items == 0 ? "-" : $"{100.0 * covered / items:0}%";
    sb.AppendLine($"| {cat.Name} | {pct} | {covered}/{items} | {catTests} | {cat.Notes} |");
}

sb.AppendLine();
sb.AppendLine($"**Overall: {coveredItems}/{totalItems} items covered ({100.0 * coveredItems / Math.Max(1, totalItems):0}%).**");
sb.AppendLine();

// ---- Third-party libraries (separate table, own percentage) --------------------
if (catalog.ThirdParty is { } third)
{
    int evaluated = 0, running = 0;
    var rows = new StringBuilder();
    foreach (var lib in third.Items)
    {
        var matched = tests.Where(t => lib.Tests.Any(sel => Matches(sel, t))).Distinct().ToList();
        foreach (var m in matched) unmapped.Remove(m);
        string status, detailText = lib.Notes ?? "";
        if (lib.NotEvaluated != null)
        {
            status = "Not evaluated";
            detailText = lib.NotEvaluated;
        }
        else
        {
            evaluated++;
            var failed = matched.Where(t => !t.Passed && t.Outcome != "NotExecuted").ToList();
            var skipped = matched.Where(t => t.Outcome == "NotExecuted").ToList();
            if (matched.Count == 0) status = "Untested";
            else if (failed.Count > 0) status = $"Failing ({failed.Count} of {matched.Count})";
            else if (skipped.Count > 0)
            {
                status = "Incompatible";
                var reason = skipped.Select(t => t.Message).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
                if (reason != null) detailText = reason.Trim();
            }
            else { status = "Runs unmodified"; running++; }
        }
        var classes = matched.Select(t => t.Class.Split('.').Last()).Distinct().OrderBy(c => c).ToList();
        string backing = classes.Count == 0 ? "-" : string.Join(", ", classes.Select(c => $"`{c}`"));
        rows.AppendLine($"| {lib.Name} | {lib.Version ?? "-"} | {status} | {matched.Count(t => t.Passed)}/{matched.Count} | {backing} | {detailText.Replace("|", "\\|")} |");
    }

    sb.AppendLine($"## {third.Name}");
    sb.AppendLine();
    sb.AppendLine(third.Notes);
    sb.AppendLine();
    sb.AppendLine($"**{running} of {evaluated} libraries run unmodified ({100.0 * running / Math.Max(1, evaluated):0}%).** " +
                  "Libraries marked Not evaluated are listed with the reason and are not counted.");
    sb.AppendLine();
    sb.AppendLine("| Library | Version | Status | Tests passed | Backing tests | Notes |");
    sb.AppendLine("|---------|---------|--------|--------------|---------------|-------|");
    sb.Append(rows);
    sb.AppendLine();
}

sb.AppendLine("## Detail");
sb.AppendLine();
sb.Append(detail);
sb.AppendLine("## Tests not mapped to any category");
sb.AppendLine();
sb.AppendLine($"{unmapped.Count} tests are not attributed to a category above (infrastructure, diagnostics, platform-only features such as tray, printing, IME, drag payloads). They still run in the suite.");
sb.AppendLine();
var unmappedClasses = unmapped.Select(t => t.Class.Split('.').Last()).Distinct().OrderBy(c => c).ToList();
sb.AppendLine(string.Join(", ", unmappedClasses.Select(c => $"`{c}`")));
sb.AppendLine();

var outFull = Path.IsPathRooted(outPath) ? outPath : Path.Combine(repoRoot, outPath);
File.WriteAllText(outFull, sb.ToString());
Console.WriteLine($"Scorecard written to {outFull}: {coveredItems}/{totalItems} items covered.");
return 0;

static bool Matches(string selector, TestResult t)
{
    // "Class" matches a class simple name; "Class.Method" a method; "Class/regex" methods by regex.
    string cls = t.Class.Split('.').Last();
    if (selector.Contains('/'))
    {
        var parts = selector.Split('/', 2);
        return cls == parts[0] && Regex.IsMatch(t.Name, parts[1]);
    }
    if (selector.Contains('.'))
    {
        var parts = selector.Split('.', 2);
        return cls == parts[0] && t.Name.StartsWith(parts[1], StringComparison.Ordinal);
    }
    return cls == selector;
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && !File.Exists(Path.Combine(dir.FullName, "OpenMaui.Controls.Linux.csproj")))
        dir = dir.Parent;
    return dir?.FullName ?? Directory.GetCurrentDirectory();
}

record TestResult(string Class, string Name, bool Passed, string Outcome = "Passed", string? Message = null);
record Catalog(List<Category> Categories, ThirdPartySection? ThirdParty);
record ThirdPartySection(string Name, string Notes, List<Library> Items);
record Library(string Name, string? Version, List<string> Tests, string? Notes, string? NotEvaluated);
record Category(string Name, string Notes, List<Item> Items);
record Item(string Name, List<string> Tests, string? NotApplicable);
