// Third-party stub scan (docs/STUBS.md).
//
// MAUI libraries ship a platform-neutral lib/net10.0 build next to their per-platform builds.
// OpenMaui apps load the neutral one, so anything a library implements only in its Windows
// build (lib/net10.0-windows*) is a hole OpenMaui has to bridge or accept. This tool finds
// those holes by comparing the two builds of every library assembly, method by method:
//
//   stub          the neutral body is a stub (empty, returns a constant/default, or only
//                 throws) while the Windows body of the same method does real work;
//   windows-only  a method the Windows build declares on a type both builds share, which
//                 the neutral build does not have at all (compared by name).
//
// Every finding must be reviewed in tools/StubScan/baseline.json with a status:
//   covered         OpenMaui bridges it ("by" names the file/patch that does; it must exist);
//   not-applicable  irrelevant on OpenMaui ("reason" says why, e.g. WinUI plumbing that the
//                   Skia renderer replaces);
//   open            a real parity gap (listed in docs/STUBS.md).
// A finding missing from the baseline (a new library version added a stub nobody reviewed)
// fails the run. Stale baseline entries (no longer reported) are listed as a warning.
//
// Usage (repo root, after `dotnet restore tests/Compat/OpenMaui.Compat.Tests.csproj`):
//   dotnet run --project tools/StubScan -- [--assets <project.assets.json>]
//       [--baseline <baseline.json>] [--report <report.md>] [--write-baseline] [--verbose]
// Exit codes: 0 every finding reviewed; 1 unreviewed findings or an invalid baseline;
//             2 setup error (assets file or packages missing).

using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OpenMaui.StubScan;

internal static class Program
{
    private static int Main(string[] args)
    {
        string root = FindRepoRoot();
        string assetsPath = Path.Combine(root, "tests", "Compat", "obj", "project.assets.json");
        string baselinePath = Path.Combine(root, "tools", "StubScan", "baseline.json");
        string? reportPath = null;
        bool writeBaseline = false, verbose = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--assets": assetsPath = Path.GetFullPath(args[++i]); break;
                case "--baseline": baselinePath = Path.GetFullPath(args[++i]); break;
                case "--report": reportPath = Path.GetFullPath(args[++i]); break;
                case "--write-baseline": writeBaseline = true; break;
                case "--verbose": verbose = true; break;
                case "-h":
                case "--help":
                    Console.WriteLine("stubscan [--assets <project.assets.json>] [--baseline <baseline.json>] [--report <report.md>] [--write-baseline] [--verbose]");
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown argument '{args[i]}'.");
                    return 2;
            }
        }

        if (!File.Exists(baselinePath))
        {
            Console.Error.WriteLine($"Baseline not found: {baselinePath}");
            return 2;
        }
        var baseline = Baseline.Load(baselinePath);

        if (!File.Exists(assetsPath))
        {
            Console.Error.WriteLine($"Assets file not found: {assetsPath}. Run `dotnet restore tests/Compat/OpenMaui.Compat.Tests.csproj` first.");
            return 2;
        }

        List<PackageRef> packages;
        try
        {
            packages = PackageRef.Resolve(assetsPath, baseline.PackagePatterns);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        if (packages.Count == 0)
        {
            Console.Error.WriteLine($"No package in {assetsPath} matches the baseline's package patterns.");
            return 2;
        }

        // ---- Scan --------------------------------------------------------------------------
        var findings = new List<Finding>();
        foreach (var pkg in packages)
        {
            foreach (var (neutral, windows) in pkg.AssemblyPairs())
            {
                var found = Scanner.Compare(pkg, neutral, windows);
                findings.AddRange(found);
                if (verbose)
                    Console.WriteLine($"  {pkg.Id} {pkg.Version}: {Path.GetFileName(neutral)} -> {found.Count} finding(s)");
            }
        }
        findings = findings.GroupBy(f => f.Id).Select(g => g.First()).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();

        // ---- Compare with the baseline ------------------------------------------------------
        var errors = baseline.Validate(root);
        var unreviewed = findings.Where(f => !baseline.Entries.ContainsKey(f.Id)).ToList();
        var reported = findings.Select(f => f.Id).ToHashSet(StringComparer.Ordinal);
        var stale = baseline.Entries.Keys.Where(k => !reported.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();

        if (writeBaseline)
        {
            baseline.Write(baselinePath, findings, packages);
            Console.WriteLine($"Baseline written: {baselinePath} ({unreviewed.Count} new entr{(unreviewed.Count == 1 ? "y" : "ies")} marked \"unreviewed\", {stale.Count} stale entr{(stale.Count == 1 ? "y" : "ies")} removed).");
            Console.WriteLine("Review every \"unreviewed\" entry (covered / not-applicable / open) before committing.");
            return 0;
        }

        // ---- Report ---------------------------------------------------------------------------
        var md = new StringBuilder();
        md.AppendLine("# Third-party stub scan").AppendLine();
        md.AppendLine("| Package | Version | Assembly findings | covered | not-applicable | open | unreviewed |");
        md.AppendLine("|---|---|---:|---:|---:|---:|---:|");
        Console.WriteLine("Stub scan: neutral lib/net10.0 vs lib/net10.0-windows*");
        Console.WriteLine($"{"package",-42} {"version",-10} {"total",6} {"covered",8} {"n/a",5} {"open",5} {"new",5}");
        foreach (var pkg in packages)
        {
            var mine = findings.Where(f => f.Package == pkg.Id).ToList();
            int Count(string s) => mine.Count(f => baseline.Entries.TryGetValue(f.Id, out var e) && e.Status == s);
            int fresh = mine.Count(f => !baseline.Entries.ContainsKey(f.Id));
            Console.WriteLine($"{pkg.Id,-42} {pkg.Version,-10} {mine.Count,6} {Count("covered"),8} {Count("not-applicable"),5} {Count("open"),5} {fresh,5}");
            md.AppendLine($"| {pkg.Id} | {pkg.Version} | {mine.Count} | {Count("covered")} | {Count("not-applicable")} | {Count("open")} | {fresh} |");
        }

        // Open gaps, grouped by package and reason (one reason usually spans a type's members).
        var open = findings.Where(f => baseline.Entries.TryGetValue(f.Id, out var e) && e.Status == "open").ToList();
        var groups = open
            .GroupBy(f => (f.Package, Reason: baseline.Entries[f.Id].Reason ?? ""))
            .OrderBy(g => g.Key.Package, StringComparer.Ordinal).ThenByDescending(g => g.Count())
            .ToList();
        Console.WriteLine();
        Console.WriteLine($"Open parity gaps: {open.Count} finding(s) in {groups.Count} group(s); see docs/STUBS.md");
        md.AppendLine().AppendLine($"## Open ({open.Count})").AppendLine();
        foreach (var g in groups)
        {
            Console.WriteLine($"  OPEN  {g.Key.Package}: {g.Key.Reason} ({g.Count()})");
            md.AppendLine($"### {g.Key.Package}: {g.Key.Reason} ({g.Count()})").AppendLine();
            foreach (var f in g)
            {
                if (verbose)
                    Console.WriteLine($"          {f.Id}");
                md.AppendLine($"- `{f.Id}`");
            }
            md.AppendLine();
        }
        if (!verbose && open.Count > 0)
            Console.WriteLine("  (--verbose lists every member; --report writes them all to a Markdown file)");

        if (stale.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"warning: {stale.Count} baseline entr{(stale.Count == 1 ? "y is" : "ies are")} no longer reported (fixed upstream or renamed); run with --write-baseline to drop them:");
            md.AppendLine().AppendLine($"## Stale baseline entries ({stale.Count})").AppendLine();
            foreach (var s in stale)
            {
                Console.WriteLine($"  STALE {s}");
                md.AppendLine($"- `{s}`");
            }
        }

        if (unreviewed.Count > 0)
        {
            Console.WriteLine();
            Console.Error.WriteLine($"error: {unreviewed.Count} finding(s) not in {Path.GetFileName(baselinePath)} (a library update introduced stubs nobody reviewed):");
            md.AppendLine().AppendLine($"## Unreviewed ({unreviewed.Count})").AppendLine();
            foreach (var f in unreviewed)
            {
                Console.Error.WriteLine($"  NEW   {f.Id}   [{f.Detail}]");
                md.AppendLine($"- `{f.Id}` ({f.Detail})");
            }
            Console.Error.WriteLine("Review each one (grep Syncfusion/, Handlers/, Services/ for a bridge), then add it to the baseline (--write-baseline adds them as \"unreviewed\").");
        }

        if (errors.Count > 0)
        {
            Console.WriteLine();
            Console.Error.WriteLine($"error: the baseline has {errors.Count} invalid entr{(errors.Count == 1 ? "y" : "ies")}:");
            md.AppendLine().AppendLine($"## Invalid baseline entries ({errors.Count})").AppendLine();
            foreach (var err in errors)
            {
                Console.Error.WriteLine($"  {err}");
                md.AppendLine($"- {err}");
            }
        }

        if (reportPath != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            File.WriteAllText(reportPath, md.ToString());
        }

        bool ok = unreviewed.Count == 0 && errors.Count == 0;
        Console.WriteLine();
        Console.WriteLine(ok
            ? $"OK: {findings.Count} finding(s), all reviewed ({open.Count} open)."
            : "FAILED: see the errors above.");
        return ok ? 0 : 1;
    }

    private static string FindRepoRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "OpenMaui.Controls.Linux.csproj")))
                    return dir.FullName;
            }
        }
        return Directory.GetCurrentDirectory();
    }
}

internal sealed record Finding(string Package, string Assembly, string Kind, string Member, string Detail)
{
    /// <summary>Stable id: no package version in it, so a version bump keeps reviewed entries.</summary>
    public string Id => $"{Assembly}|{Kind}|{Member}";
}

internal sealed class PackageRef
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string Directory { get; init; }

    public static List<PackageRef> Resolve(string assetsPath, IReadOnlyList<string> patterns)
    {
        var doc = JsonNode.Parse(File.ReadAllText(assetsPath))!;
        var folders = doc["packageFolders"]!.AsObject().Select(kv => kv.Key).ToList();
        var regexes = patterns.Select(p => new Regex("^" + Regex.Escape(p).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase)).ToList();
        var result = new List<PackageRef>();
        foreach (var (key, value) in doc["libraries"]!.AsObject())
        {
            if ((string?)value!["type"] != "package")
                continue;
            var parts = key.Split('/');
            if (!regexes.Any(r => r.IsMatch(parts[0])))
                continue;
            var relPath = (string?)value["path"] ?? $"{parts[0].ToLowerInvariant()}/{parts[1].ToLowerInvariant()}";
            var dir = folders.Select(f => Path.Combine(f, relPath)).FirstOrDefault(System.IO.Directory.Exists)
                ?? throw new DirectoryNotFoundException($"Package {key} is in the assets file but not in any package folder ({string.Join(", ", folders)}). Restore first.");
            result.Add(new PackageRef { Id = parts[0], Version = parts[1], Directory = dir });
        }
        return result.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>(neutral, windows) assembly pairs: lib/net10.0/X.dll with lib/net10.0-windows*/X.dll.</summary>
    public IEnumerable<(string Neutral, string Windows)> AssemblyPairs()
    {
        var lib = Path.Combine(Directory, "lib");
        var neutralDir = Path.Combine(lib, "net10.0");
        if (!System.IO.Directory.Exists(neutralDir))
            yield break;
        // The lowest Windows SDK flavour is the one every library ships (windows10.0.19041).
        var windowsDir = System.IO.Directory.Exists(lib)
            ? System.IO.Directory.GetDirectories(lib, "net10.0-windows*").OrderBy(d => d, StringComparer.Ordinal).FirstOrDefault()
            : null;
        if (windowsDir == null)
            yield break;
        foreach (var neutral in System.IO.Directory.GetFiles(neutralDir, "*.dll").OrderBy(f => f, StringComparer.Ordinal))
        {
            var windows = Path.Combine(windowsDir, Path.GetFileName(neutral));
            if (File.Exists(windows))
                yield return (neutral, windows);
        }
    }
}

internal sealed record MethodInfoLite(string Type, string Name, string Signature, int IlSize, StubKind Stub, bool IsPublicSurface);

internal enum StubKind { None, Empty, Default, Throw }

internal static class Scanner
{
    /// <summary>A Windows body this small does nothing worth reporting (returns a field, forwards a constant).</summary>
    private const int MinWorkIlSize = 8;

    public static List<Finding> Compare(PackageRef pkg, string neutralPath, string windowsPath)
    {
        var asm = Path.GetFileNameWithoutExtension(neutralPath);
        var neutral = Read(neutralPath);
        var windows = Read(windowsPath);
        var findings = new List<Finding>();

        var neutralByKey = neutral.ToDictionary(m => Key(m), StringComparer.Ordinal);
        foreach (var w in windows)
        {
            if (!neutralByKey.TryGetValue(Key(w), out var n))
                continue;
            if (n.Stub != StubKind.None && w.Stub == StubKind.None && w.IlSize >= MinWorkIlSize)
            {
                var what = n.Stub switch { StubKind.Throw => "throws", StubKind.Empty => "empty", _ => "returns default" };
                findings.Add(new Finding(pkg.Id, asm, "stub", $"{w.Type}::{w.Name}({w.Signature})",
                    $"neutral {what}, {n.IlSize} B IL; windows {w.IlSize} B IL"));
            }
        }

        var neutralTypes = neutral.Select(m => m.Type).ToHashSet(StringComparer.Ordinal);
        var neutralNames = neutral.Select(m => m.Type + "::" + m.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var g in windows
                     .Where(m => m.IlSize >= MinWorkIlSize && neutralTypes.Contains(m.Type) && !neutralNames.Contains(m.Type + "::" + m.Name))
                     .GroupBy(m => m.Type + "::" + m.Name))
        {
            var sample = g.First();
            findings.Add(new Finding(pkg.Id, asm, "windows-only", g.Key,
                $"{(g.Any(m => m.IsPublicSurface) ? "public/protected" : "internal/private")}, {g.Count()} overload(s), {g.Max(m => m.IlSize)} B IL"));
        }
        return findings;
    }

    private static string Key(MethodInfoLite m) => $"{m.Type}::{m.Name}({m.Signature})";

    public static List<MethodInfoLite> Read(string path)
    {
        var result = new List<MethodInfoLite>();
        using var fs = File.OpenRead(path);
        using var pe = new PEReader(fs);
        var md = pe.GetMetadataReader();
        var provider = new NameProvider(md);
        foreach (var th in md.TypeDefinitions)
        {
            var t = md.GetTypeDefinition(th);
            var typeName = NameProvider.TypeDefName(md, th);
            if (typeName.Contains('<'))
                continue; // compiler-generated (closures, state machines, anonymous types)
            if (typeName.StartsWith("Microsoft.Maui.Controls.Generated.", StringComparison.Ordinal))
                continue; // source-generated binding interceptors: per-build hashed names, no platform code
            bool typeVisible = IsTypeVisible(md, t);
            foreach (var mh in t.GetMethods())
            {
                var m = md.GetMethodDefinition(mh);
                var name = md.GetString(m.Name);
                if (name.Contains('<'))
                    continue; // local functions, lambdas
                MethodSignature<string> sig;
                try { sig = m.DecodeSignature(provider, null); }
                catch (BadImageFormatException) { continue; }
                var signature = string.Join(",", sig.ParameterTypes);
                if (m.GetGenericParameters().Count > 0)
                    name += "`" + m.GetGenericParameters().Count;

                int size = 0;
                var stub = StubKind.None;
                if (m.RelativeVirtualAddress != 0)
                {
                    var il = pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes() ?? Array.Empty<byte>();
                    size = il.Length;
                    stub = Classify(il);
                }
                else
                {
                    size = -1; // abstract / extern: no body to compare
                    stub = StubKind.None;
                }
                var access = m.Attributes & MethodAttributes.MemberAccessMask;
                bool surface = typeVisible && (access == MethodAttributes.Public || access == MethodAttributes.Family || access == MethodAttributes.FamORAssem);
                if (size < 0)
                    continue;
                result.Add(new MethodInfoLite(typeName, name, signature, size, stub, surface));
            }
        }
        return result;
    }

    private static bool IsTypeVisible(MetadataReader md, TypeDefinition t)
    {
        while (true)
        {
            var vis = t.Attributes & TypeAttributes.VisibilityMask;
            if (vis == TypeAttributes.NotPublic) return false;
            if (vis == TypeAttributes.Public) return true;
            if (vis is not (TypeAttributes.NestedPublic or TypeAttributes.NestedFamily or TypeAttributes.NestedFamORAssem)) return false;
            t = md.GetTypeDefinition(t.GetDeclaringType());
        }
    }

    /// <summary>
    /// Stub shapes (after leading nops): `ret`; `ldnull|ldc.*|ldarg.N; ret` (a constant or an
    /// argument straight back); `ldloca.s; initobj; ldloc; ret` (default(T) for a struct);
    /// and a short body that ends in `throw` (new NotImplementedException / PlatformNotSupported).
    /// </summary>
    public static StubKind Classify(byte[] il)
    {
        int start = 0;
        while (start < il.Length && il[start] == 0x00) start++; // nop
        int len = il.Length - start;
        if (len <= 0) return StubKind.Empty;
        if (len == 1 && il[start] == 0x2A) return StubKind.Empty;
        if (len <= 3 && il[^1] == 0x2A) return StubKind.Default;
        // ldc.i8 / ldc.r4 / ldc.r8 constant + ret
        if ((il[start] == 0x21 && len == 10) || (il[start] == 0x22 && len == 6) || (il[start] == 0x23 && len == 10))
            if (il[^1] == 0x2A) return StubKind.Default;
        // ldloca.s N; initobj T; ldloc.N; ret
        if (len >= 10 && len <= 12 && il[start] == 0x12 && il[start + 2] == 0xFE && il[start + 3] == 0x15 && il[^1] == 0x2A)
            return StubKind.Default;
        // A straight-line body that only builds an exception and throws it.
        if (len <= 24 && il[^1] == 0x7A && Opcodes(il, start).All(op => ThrowOnlyOpcodes.Contains(op)))
            return StubKind.Throw;
        return StubKind.None;
    }

    // ldarg.0-3, ldarg.s, ldnull, ldc.i4.*, ldstr, newobj, call, callvirt, ldsfld, ldfld, throw, nop
    private static readonly HashSet<int> ThrowOnlyOpcodes = new()
    {
        0x00, 0x02, 0x03, 0x04, 0x05, 0x0E, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F,
        0x20, 0x28, 0x6F, 0x72, 0x73, 0x7A, 0x7B, 0x7E, 0x8C,
    };

    /// <summary>Opcodes of a method body (two-byte opcodes as 0xFE00 | second byte).</summary>
    private static IEnumerable<int> Opcodes(byte[] il, int start)
    {
        int i = start;
        while (i < il.Length)
        {
            int op = il[i++];
            int operand;
            if (op == 0xFE)
            {
                if (i >= il.Length) yield break;
                int op2 = il[i++];
                op = 0xFE00 | op2;
                operand = op2 switch
                {
                    0x06 or 0x07 or 0x15 or 0x16 or 0x1C => 4,
                    >= 0x09 and <= 0x0E => 2,
                    0x12 or 0x19 => 1,
                    _ => 0,
                };
            }
            else
            {
                operand = op switch
                {
                    >= 0x0E and <= 0x13 => 1,
                    0x1F => 1,
                    0x20 or 0x22 => 4,
                    0x21 or 0x23 => 8,
                    0x27 or 0x28 or 0x29 => 4,
                    >= 0x2B and <= 0x37 => 1,
                    >= 0x38 and <= 0x44 => 4,
                    0x45 => i + 4 <= il.Length ? 4 + 4 * BitConverter.ToInt32(il, i) : 0,
                    0x6F or 0x70 or 0x71 or 0x72 or 0x73 or 0x74 or 0x75 or 0x79 => 4,
                    >= 0x7B and <= 0x81 => 4,
                    0x8C or 0x8D or 0x8F or 0xA3 or 0xA4 or 0xA5 or 0xC2 or 0xC6 or 0xD0 or 0xDD => 4,
                    0xDE => 1,
                    _ => 0,
                };
            }
            yield return op;
            i += operand;
        }
    }
}

/// <summary>Readable, assembly-independent type names for signatures (tokens differ between builds).</summary>
internal sealed class NameProvider : ISignatureTypeProvider<string, object?>
{
    private readonly MetadataReader _md;
    public NameProvider(MetadataReader md) => _md = md;

    public static string TypeDefName(MetadataReader md, TypeDefinitionHandle h)
    {
        var t = md.GetTypeDefinition(h);
        var name = md.GetString(t.Name);
        var declaring = t.GetDeclaringType();
        if (!declaring.IsNil)
            return TypeDefName(md, declaring) + "+" + name;
        var ns = md.GetString(t.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    private static string TypeRefName(MetadataReader md, TypeReferenceHandle h)
    {
        var t = md.GetTypeReference(h);
        var name = md.GetString(t.Name);
        if (t.ResolutionScope.Kind == HandleKind.TypeReference)
            return TypeRefName(md, (TypeReferenceHandle)t.ResolutionScope) + "+" + name;
        var ns = md.GetString(t.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => TypeDefName(reader, handle);
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => TypeRefName(reader, handle);
    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    public string GetSZArrayType(string elementType) => elementType + "[]";
    public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', shape.Rank - 1) + "]";
    public string GetByReferenceType(string elementType) => elementType + "&";
    public string GetPointerType(string elementType) => elementType + "*";
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType + "<" + string.Join(",", typeArguments) + ">";
    public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index;
    public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index;
    public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetPinnedType(string elementType) => elementType;
}

internal sealed class BaselineEntry
{
    public required string Id { get; init; }
    public required string Status { get; set; }
    public string? By { get; set; }
    public string? Reason { get; set; }
}

internal sealed class Baseline
{
    public List<string> PackagePatterns { get; } = new();
    public Dictionary<string, BaselineEntry> Entries { get; } = new(StringComparer.Ordinal);
    private readonly List<string> _loadErrors = new();
    private JsonObject _doc = new();

    public static Baseline Load(string path)
    {
        var b = new Baseline();
        b._doc = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })!.AsObject();
        foreach (var p in b._doc["packages"]?.AsArray() ?? new JsonArray())
            b.PackagePatterns.Add((string)p!);
        foreach (var node in b._doc["findings"]?.AsArray() ?? new JsonArray())
        {
            var id = (string?)node!["id"];
            if (string.IsNullOrEmpty(id)) { b._loadErrors.Add("entry without an id"); continue; }
            var entry = new BaselineEntry
            {
                Id = id,
                Status = (string?)node["status"] ?? "",
                By = (string?)node["by"],
                Reason = (string?)node["reason"],
            };
            if (!b.Entries.TryAdd(id, entry))
                b._loadErrors.Add($"duplicate entry: {id}");
        }
        return b;
    }

    public List<string> Validate(string repoRoot)
    {
        var errors = new List<string>(_loadErrors);
        foreach (var e in Entries.Values)
        {
            if (e.Status == "unreviewed")
            {
                errors.Add($"{e.Id}: status \"unreviewed\" (set covered / not-applicable / open)");
                continue;
            }
            if (Array.IndexOf(StatusesList, e.Status) < 0)
            {
                errors.Add($"{e.Id}: unknown status \"{e.Status}\"");
                continue;
            }
            if (e.Status == "covered")
            {
                if (string.IsNullOrWhiteSpace(e.By))
                    errors.Add($"{e.Id}: covered needs \"by\" (the OpenMaui file or patch that bridges it)");
                else
                    foreach (var file in e.By.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        if (!File.Exists(Path.Combine(repoRoot, file)))
                            errors.Add($"{e.Id}: \"by\" names {file}, which does not exist");
            }
            if (e.Status == "not-applicable" && string.IsNullOrWhiteSpace(e.Reason))
                errors.Add($"{e.Id}: not-applicable needs a \"reason\"");
        }
        return errors;
    }

    private static readonly string[] StatusesList = { "covered", "not-applicable", "open" };

    public void Write(string path, List<Finding> findings, List<PackageRef> packages)
    {
        var arr = new JsonArray();
        foreach (var f in findings)
        {
            var o = new JsonObject { ["id"] = f.Id };
            if (Entries.TryGetValue(f.Id, out var e))
            {
                o["status"] = e.Status;
                if (!string.IsNullOrEmpty(e.By)) o["by"] = e.By;
                if (!string.IsNullOrEmpty(e.Reason)) o["reason"] = e.Reason;
            }
            else
            {
                o["status"] = "unreviewed";
                o["reason"] = f.Detail;
            }
            arr.Add(o);
        }
        _doc["scanned"] = new JsonObject(packages.Select(p => KeyValuePair.Create(p.Id, (JsonNode?)JsonValue.Create(p.Version))));
        _doc["findings"] = arr;
        File.WriteAllText(path, _doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
    }
}
