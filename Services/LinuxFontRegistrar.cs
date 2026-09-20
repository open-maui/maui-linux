// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux implementation of <see cref="IFontRegistrar"/>. Backs
/// <c>builder.ConfigureFonts(fonts =&gt; fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"))</c>.
///
/// MAUI's portable <c>FontRegistrar</c> delegates to an <c>EmbeddedFontLoader</c>
/// that is a no-op on the generic TFM, so every registered alias silently
/// resolved to <c>null</c> and labels fell back to fontconfig. This registrar
/// resolves the font file itself (absolute path, next to the executable,
/// <c>Resources/Fonts</c>, or an embedded resource extracted to the XDG cache)
/// and loads it with <see cref="SKTypeface.FromFile(string, int)"/>. Rendering
/// code asks <see cref="TryGetTypeface(string, SKFontStyle)"/> before falling
/// back to fontconfig so registered aliases and their family names win.
/// </summary>
public sealed class LinuxFontRegistrar : IFontRegistrar
{
    private static readonly Lazy<LinuxFontRegistrar> _instance = new(() => new LinuxFontRegistrar());

    /// <summary>
    /// Process-wide registrar. The DI singleton resolves to this same object so
    /// <see cref="Rendering.ResourceCache"/> (constructed outside DI by the
    /// rendering engine) and <c>FontImageSource</c> rendering see the fonts
    /// registered through <c>ConfigureFonts</c>.
    /// </summary>
    public static LinuxFontRegistrar Instance => _instance.Value;

    private static readonly string[] FontExtensions = { ".ttf", ".otf", ".ttc", ".woff2", ".woff" };

    private readonly Lock _lock = new();
    private readonly List<RegisteredFont> _fonts = new();
    private readonly Dictionary<string, RegisteredFont> _byAlias = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RegisteredFont> _byFileName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _lookupCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] _searchDirectories;

    /// <summary>
    /// Creates a registrar that probes the application's output directory.
    /// </summary>
    public LinuxFontRegistrar()
        : this(null)
    {
    }

    /// <summary>
    /// Creates a registrar with explicit search directories (tests, custom hosts).
    /// When <paramref name="searchDirectories"/> is null the default set is used:
    /// <c>AppContext.BaseDirectory</c>, its <c>Resources/Fonts</c> and <c>Fonts</c>
    /// sub-folders, the entry assembly directory and the current directory.
    /// </summary>
    public LinuxFontRegistrar(IEnumerable<string>? searchDirectories)
    {
        _searchDirectories = (searchDirectories ?? DefaultSearchDirectories())
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Registered fonts in registration order (alias, file name, resolved path).
    /// </summary>
    public IReadOnlyList<RegisteredFont> RegisteredFonts
    {
        get { lock (_lock) { return _fonts.ToArray(); } }
    }

    /// <inheritdoc />
    public void Register(string filename, string? alias, Assembly assembly)
        => RegisterCore(filename, alias, assembly);

    /// <inheritdoc />
    public void Register(string filename, string? alias)
        => RegisterCore(filename, alias, null);

    /// <summary>
    /// Returns the resolved on-disk path of the font registered under
    /// <paramref name="font"/> (alias, file name, or family name), or
    /// <c>null</c> when nothing matches. Mirrors MAUI's contract where the
    /// return value is the platform font identifier.
    /// </summary>
    public string? GetFont(string font)
    {
        if (string.IsNullOrEmpty(font)) return null;

        lock (_lock)
        {
            if (_lookupCache.TryGetValue(font, out var cached))
                return cached;

            var entry = FindEntry(font);
            var path = entry?.EnsureLoaded() == null ? null : entry.ResolvedPath;
            _lookupCache[font] = path;
            return path;
        }
    }

    /// <summary>
    /// Resolves a registered typeface for <paramref name="fontFamily"/>: first
    /// by alias / file name, then by the family name embedded in any registered
    /// face. Among the faces of that family the closest match to
    /// <paramref name="style"/> (weight, width, slant) wins, with the alias'
    /// own face preferred on ties. Returns <c>null</c> when nothing is
    /// registered for the name so callers can fall back to fontconfig.
    /// The returned typeface is owned by the registrar; do not dispose it.
    /// </summary>
    public SKTypeface? TryGetTypeface(string? fontFamily, SKFontStyle? style = null)
    {
        if (string.IsNullOrWhiteSpace(fontFamily)) return null;

        var name = NormalizeFamilyName(fontFamily);
        style ??= SKFontStyle.Normal;

        lock (_lock)
        {
            if (_fonts.Count == 0) return null;

            var direct = FindEntry(name);
            var directFace = direct?.EnsureLoaded();

            // Candidate faces: everything that shares a family name with the
            // alias face (or, without an alias hit, everything whose family
            // name equals the requested name).
            string? familyName = directFace?.FamilyName;
            IEnumerable<RegisteredFont> candidates = familyName != null
                ? _fonts.Where(f => string.Equals(f.EnsureLoaded()?.FamilyName, familyName, StringComparison.OrdinalIgnoreCase))
                : _fonts.Where(f => string.Equals(f.EnsureLoaded()?.FamilyName, name, StringComparison.OrdinalIgnoreCase));

            RegisteredFont? best = null;
            int bestScore = int.MaxValue;
            foreach (var candidate in candidates)
            {
                var face = candidate.Typeface;
                if (face == null) continue;
                int score = StyleDistance(face.FontStyle, style);
                if (score < bestScore || (score == bestScore && candidate == direct))
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best?.Typeface ?? directFace;
        }
    }

    /// <summary>
    /// True when <paramref name="fontFamily"/> matches a registered alias,
    /// file name or loaded family name.
    /// </summary>
    public bool IsRegistered(string? fontFamily)
        => TryGetTypeface(fontFamily) != null;

    /// <summary>
    /// Removes every registration and disposes the loaded typefaces. Intended
    /// for tests; rendering caches that handed out these faces must be cleared
    /// as well.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var font in _fonts)
                font.Dispose();
            _fonts.Clear();
            _byAlias.Clear();
            _byFileName.Clear();
            _lookupCache.Clear();
        }
    }

    private void RegisterCore(string filename, string? alias, Assembly? assembly)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentException("Font file name is required.", nameof(filename));

        lock (_lock)
        {
            var entry = new RegisteredFont(this, filename, alias, assembly);
            _fonts.Add(entry);
            _byFileName[filename] = entry;
            var baseName = Path.GetFileName(filename);
            _byFileName[baseName] = entry;
            _byFileName[Path.GetFileNameWithoutExtension(baseName)] = entry;
            if (!string.IsNullOrWhiteSpace(alias))
                _byAlias[alias] = entry;
            _lookupCache.Clear();
            DiagnosticLog.Debug("LinuxFontRegistrar", $"Registered font '{filename}' as '{alias ?? baseName}'");
        }
    }

    private RegisteredFont? FindEntry(string name)
    {
        if (_byAlias.TryGetValue(name, out var entry)) return entry;
        if (_byFileName.TryGetValue(name, out entry)) return entry;

        // Family name of an already-loaded face ("Open Sans").
        foreach (var font in _fonts)
        {
            var face = font.EnsureLoaded();
            if (face != null && string.Equals(face.FamilyName, name, StringComparison.OrdinalIgnoreCase))
                return font;
        }
        return null;
    }

    private static string NormalizeFamilyName(string fontFamily)
    {
        var name = fontFamily.Trim();
        // Android-style "file.ttf#Alias" — prefer the alias part.
        var hash = name.IndexOf('#');
        if (hash >= 0 && hash < name.Length - 1)
            name = name[(hash + 1)..];
        return name;
    }

    private static int StyleDistance(SKFontStyle actual, SKFontStyle requested)
    {
        int score = Math.Abs(actual.Weight - requested.Weight);
        score += Math.Abs(actual.Width - requested.Width) * 10;
        if (actual.Slant != requested.Slant)
        {
            // Italic and oblique are interchangeable substitutes; upright is not.
            bool bothSlanted = actual.Slant != SKFontStyleSlant.Upright && requested.Slant != SKFontStyleSlant.Upright;
            score += bothSlanted ? 50 : 1000;
        }
        return score;
    }

    private static IEnumerable<string> DefaultSearchDirectories()
    {
        var baseDir = AppContext.BaseDirectory;
        yield return baseDir;
        yield return Path.Combine(baseDir, "Resources", "Fonts");
        yield return Path.Combine(baseDir, "Fonts");
        yield return Path.Combine(baseDir, "fonts");

        string? entryDir = null;
        try
        {
            var entry = Assembly.GetEntryAssembly();
            if (entry != null && !string.IsNullOrEmpty(entry.Location))
                entryDir = Path.GetDirectoryName(entry.Location);
        }
        catch
        {
            // Single-file / trimmed hosts may not expose a location.
        }
        if (!string.IsNullOrEmpty(entryDir))
        {
            yield return entryDir;
            yield return Path.Combine(entryDir, "Resources", "Fonts");
        }

        string? cwd = null;
        try { cwd = Environment.CurrentDirectory; } catch { }
        if (!string.IsNullOrEmpty(cwd))
        {
            yield return cwd;
            yield return Path.Combine(cwd, "Resources", "Fonts");
        }
    }

    /// <summary>
    /// Probes the search directories for <paramref name="filename"/>, trying the
    /// name as given, its base name, and the base name with each known font
    /// extension when none was supplied.
    /// </summary>
    internal string? ResolveFilePath(string filename)
    {
        if (Path.IsPathRooted(filename))
            return File.Exists(filename) ? filename : null;

        var baseName = Path.GetFileName(filename);
        var hasExtension = FontExtensions.Contains(Path.GetExtension(baseName), StringComparer.OrdinalIgnoreCase);

        foreach (var dir in _searchDirectories)
        {
            foreach (var candidate in Candidates(dir, filename, baseName, hasExtension))
            {
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        return null;
    }

    private static IEnumerable<string> Candidates(string dir, string filename, string baseName, bool hasExtension)
    {
        yield return Path.Combine(dir, filename);
        if (!string.Equals(filename, baseName, StringComparison.Ordinal))
            yield return Path.Combine(dir, baseName);
        if (!hasExtension)
        {
            foreach (var ext in FontExtensions)
                yield return Path.Combine(dir, baseName + ext);
        }
    }

    /// <summary>
    /// Locates an embedded manifest resource whose name ends with
    /// <paramref name="filename"/> in <paramref name="assembly"/> and extracts
    /// it to the XDG cache directory so it can be loaded from disk.
    /// </summary>
    internal static string? ExtractEmbeddedFont(Assembly assembly, string filename)
    {
        var baseName = Path.GetFileName(filename);
        string? resourceName = null;
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (name.Equals(filename, StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("." + baseName, StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("/" + baseName, StringComparison.OrdinalIgnoreCase)
                || name.Equals(baseName, StringComparison.OrdinalIgnoreCase))
            {
                resourceName = name;
                break;
            }
        }
        if (resourceName == null) return null;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return null;

        var cacheRoot = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (string.IsNullOrEmpty(cacheRoot))
            cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
        var dir = Path.Combine(cacheRoot, "openmaui", "fonts", assembly.GetName().Name ?? "app");
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, baseName);

        if (!File.Exists(target) || new FileInfo(target).Length != stream.Length)
        {
            using var file = File.Create(target);
            stream.CopyTo(file);
        }
        return target;
    }

    /// <summary>
    /// A single <c>AddFont</c> registration and its lazily loaded typeface.
    /// </summary>
    public sealed class RegisteredFont : IDisposable
    {
        private readonly LinuxFontRegistrar _owner;
        private bool _loadAttempted;

        internal RegisteredFont(LinuxFontRegistrar owner, string fileName, string? alias, Assembly? assembly)
        {
            _owner = owner;
            FileName = fileName;
            Alias = alias;
            Assembly = assembly;
        }

        /// <summary>File name as passed to <c>AddFont</c>.</summary>
        public string FileName { get; }

        /// <summary>Alias as passed to <c>AddFont</c>, or null.</summary>
        public string? Alias { get; }

        /// <summary>Assembly holding the embedded resource, or null for a file-system font.</summary>
        public Assembly? Assembly { get; }

        /// <summary>Absolute path the font was loaded from, or null when unresolved.</summary>
        public string? ResolvedPath { get; private set; }

        /// <summary>Loaded typeface, or null when the file could not be found or parsed.</summary>
        public SKTypeface? Typeface { get; private set; }

        internal SKTypeface? EnsureLoaded()
        {
            if (_loadAttempted) return Typeface;
            _loadAttempted = true;

            try
            {
                string? path = Assembly != null ? ExtractEmbeddedFont(Assembly, FileName) : null;
                path ??= _owner.ResolveFilePath(FileName);
                if (path == null && Assembly == null)
                {
                    // AddFont without an assembly still commonly ships the font
                    // as an embedded resource of the entry assembly.
                    var entry = System.Reflection.Assembly.GetEntryAssembly();
                    if (entry != null)
                        path = ExtractEmbeddedFont(entry, FileName);
                }

                if (path == null)
                {
                    DiagnosticLog.Warn("LinuxFontRegistrar", $"Font '{FileName}' (alias '{Alias}') not found in any search directory");
                    return null;
                }

                var typeface = SKTypeface.FromFile(path, 0);
                if (typeface == null)
                {
                    DiagnosticLog.Warn("LinuxFontRegistrar", $"SKTypeface.FromFile failed for '{path}'");
                    return null;
                }

                ResolvedPath = path;
                Typeface = typeface;
                DiagnosticLog.Debug("LinuxFontRegistrar", $"Loaded '{Alias ?? FileName}' from '{path}' (family '{typeface.FamilyName}')");
                return typeface;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("LinuxFontRegistrar", $"Failed to load font '{FileName}': {ex.Message}", ex);
                return null;
            }
        }

        public void Dispose()
        {
            Typeface?.Dispose();
            Typeface = null;
        }
    }
}
