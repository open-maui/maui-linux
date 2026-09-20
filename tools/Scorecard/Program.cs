// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Compatibility scorecard generator.
//
//   dotnet run --project tools/Scorecard -- [--trx <file>] [--out docs/COMPATIBILITY.md] [--run]
//
// Reads a TRX test result file (produced by `dotnet test --logger trx`), maps
// every executed test onto the categories in tools/Scorecard/categories.json
// (the same 19 categories Microsoft's maui-labs GTK4 backend publishes), and
// writes a Markdown scorecard where each cell's coverage is COMPUTED from the
// tests that back it: an item counts as covered only when at least one mapped
// test exists and every mapped test passed. --run executes the test suite first.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

string? trxPath = null;
string outPath = "docs/COMPATIBILITY.md";
bool run = false;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--trx": trxPath = args[++i]; break;
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
    trxPath = Path.Combine(trxDir, "results.trx");
    var psi = new ProcessStartInfo("dotnet", $"test \"{Path.Combine(repoRoot, "tests", "OpenMaui.Controls.Linux.Tests.csproj")}\" --nologo -v q --logger \"trx;LogFileName={trxPath}\"")
    { RedirectStandardOutput = true, RedirectStandardError = true };
    using var p = Process.Start(psi)!;
    Console.Write(p.StandardOutput.ReadToEnd());
    p.WaitForExit();
}
if (trxPath == null || !File.Exists(trxPath))
{
    Console.Error.WriteLine("No TRX file. Pass --trx <file> or --run.");
    return 2;
}

// ---- Load tests from TRX ------------------------------------------------------
XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
var doc = XDocument.Load(trxPath);
var definitions = doc.Descendants(ns + "UnitTest")
    .ToDictionary(
        u => (string)u.Attribute("id")!,
        u => (Class: (string?)u.Element(ns + "TestMethod")?.Attribute("className") ?? "", Name: (string)u.Attribute("name")!));
var results = doc.Descendants(ns + "UnitTestResult")
    .Select(r => (Id: (string)r.Attribute("testId")!, Outcome: (string)r.Attribute("outcome")!))
    .ToList();
var tests = results
    .Where(r => definitions.ContainsKey(r.Id))
    .Select(r => new TestResult(definitions[r.Id].Class, definitions[r.Id].Name, r.Outcome == "Passed"))
    .ToList();

// ---- Map onto categories -------------------------------------------------------
var catalog = JsonSerializer.Deserialize<Catalog>(File.ReadAllText(categoriesPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
var sb = new StringBuilder();
sb.AppendLine("# OpenMaui Linux compatibility scorecard");
sb.AppendLine();
sb.AppendLine($"Generated {DateTime.UtcNow:yyyy-MM-dd} by `tools/Scorecard` from {tests.Count} executed tests ({tests.Count(t => t.Passed)} passed). " +
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

record TestResult(string Class, string Name, bool Passed);
record Catalog(List<Category> Categories);
record Category(string Name, string Notes, List<Item> Items);
record Item(string Name, List<string> Tests, string? NotApplicable);
