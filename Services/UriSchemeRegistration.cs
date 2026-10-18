// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Maui.Platform.Linux.Services.Portal;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Registers the running app as the desktop's handler for a custom URI scheme
/// (<c>myapp://</c>), the Linux counterpart of Windows'
/// <c>ActivationRegistrationManager.RegisterForProtocolActivation</c> for an
/// unpackaged app. The registration is an XDG <c>x-scheme-handler/&lt;scheme&gt;</c>
/// association: a hidden desktop entry for the app
/// (<c>$XDG_DATA_HOME/applications/&lt;app&gt;-&lt;scheme&gt;-handler.desktop</c>,
/// <c>Exec=&lt;app&gt; %u</c>) made the scheme's default application in
/// <c>$XDG_CONFIG_HOME/mimeapps.list</c>, the file <c>xdg-mime default</c> writes.
/// xdg-open, GIO and KDE then start the app with the URI as its argument;
/// <see cref="WebAuthenticatorService"/> hands that URI to the running instance.
/// </summary>
/// <remarks>
/// A Flatpak cannot change the host's associations: it declares the scheme in its
/// exported desktop file (<c>MimeType=x-scheme-handler/myapp;</c>), as a packaged
/// Windows app declares it in its manifest. <see cref="IsRegistered"/> reads that
/// declaration there, and <see cref="Register"/> throws.
/// </remarks>
public static class UriSchemeRegistration
{
    private static readonly AsyncLocal<XdgAssociationEnvironment?> s_environmentOverride = new();

    /// <summary>The XDG environment for the current test's flow; null uses the process's.</summary>
    internal static XdgAssociationEnvironment? EnvironmentOverride
    {
        get => s_environmentOverride.Value;
        set => s_environmentOverride.Value = value;
    }

    internal static XdgAssociationEnvironment Environment => EnvironmentOverride ?? XdgAssociationEnvironment.FromProcess();

    /// <summary>
    /// True when the desktop opens <paramref name="scheme"/> URIs with this app: the
    /// scheme's default handler (or, without a default, its first associated
    /// application) runs this app's executable. Inside a Flatpak: the app's own
    /// desktop file declares the scheme.
    /// </summary>
    public static bool IsRegistered(string scheme)
    {
        var normalized = NormalizeScheme(scheme);
        var env = Environment;
        if (env.FlatpakDesktopFile != null)
            return DeclaresScheme(env.FlatpakDesktopFile, normalized);

        var handler = ResolveHandler(env, normalized);
        return handler != null && IsCommandOwnedByApp(handler.Value.Exec, env.AppCommand);
    }

    /// <summary>
    /// Makes this app the desktop's handler for <paramref name="scheme"/>: writes the
    /// app's handler desktop entry and sets it as the scheme's default in the user's
    /// <c>mimeapps.list</c>. The registration stays until <see cref="Unregister"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Inside a Flatpak (declare the scheme in the
    /// app's desktop file instead), or when the files cannot be written.</exception>
    public static void Register(string scheme)
    {
        var normalized = NormalizeScheme(scheme);
        var env = Environment;
        if (env.FlatpakDesktopFile != null)
        {
            throw new InvalidOperationException(
                $"A Flatpak app cannot register the URI scheme '{normalized}' at run time. Declare " +
                $"MimeType=x-scheme-handler/{normalized}; in the app's desktop file.");
        }
        if (env.AppCommand.Count == 0)
            throw new InvalidOperationException("The app's executable path is unknown, so no handler can be registered.");

        try
        {
            var applications = Path.Combine(env.DataHome, "applications");
            Directory.CreateDirectory(applications);
            var desktopId = HandlerDesktopId(env, normalized);
            var path = Path.Combine(applications, desktopId);
            var content = HandlerDesktopEntry(env, normalized);
            if (!File.Exists(path) || File.ReadAllText(path) != content)
                File.WriteAllText(path, content);

            Directory.CreateDirectory(env.ConfigHome);
            var mimeapps = Path.Combine(env.ConfigHome, "mimeapps.list");
            var key = "x-scheme-handler/" + normalized;
            var lines = File.Exists(mimeapps) ? File.ReadAllLines(mimeapps).ToList() : new List<string>();
            SetEntry(lines, "[Default Applications]", key, desktopId);
            AddToEntry(lines, "[Added Associations]", key, desktopId);
            File.WriteAllLines(mimeapps, lines);
            DiagnosticLog.Debug("UriSchemeRegistration", $"Registered {desktopId} for {key}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Could not register the URI scheme '{normalized}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Removes what <see cref="Register"/> wrote for <paramref name="scheme"/>: the
    /// handler desktop entry and its <c>mimeapps.list</c> lines. Associations the
    /// user or a package made are left alone. Returns false when there was nothing to remove.
    /// </summary>
    public static bool Unregister(string scheme)
    {
        var normalized = NormalizeScheme(scheme);
        var env = Environment;
        if (env.FlatpakDesktopFile != null)
            return false;

        var removed = false;
        var desktopId = HandlerDesktopId(env, normalized);
        try
        {
            var path = Path.Combine(env.DataHome, "applications", desktopId);
            if (File.Exists(path))
            {
                File.Delete(path);
                removed = true;
            }

            var mimeapps = Path.Combine(env.ConfigHome, "mimeapps.list");
            if (File.Exists(mimeapps))
            {
                var lines = File.ReadAllLines(mimeapps).ToList();
                var key = "x-scheme-handler/" + normalized;
                if (RemoveFromEntries(lines, key, desktopId))
                {
                    File.WriteAllLines(mimeapps, lines);
                    removed = true;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Debug("UriSchemeRegistration", $"Unregister '{normalized}' failed: {ex.Message}");
        }
        return removed;
    }

    /// <summary>Lower-cases and validates an RFC 3986 scheme name.</summary>
    internal static string NormalizeScheme(string scheme)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        var s = scheme.Trim().TrimEnd(':').ToLowerInvariant();
        if (!Uri.CheckSchemeName(s))
            throw new ArgumentException($"'{scheme}' is not a valid URI scheme.", nameof(scheme));
        return s;
    }

    /// <summary>File name of the handler entry: the app's desktop id, the scheme, "-handler".</summary>
    internal static string HandlerDesktopId(XdgAssociationEnvironment env, string scheme)
    {
        var baseId = env.AppDesktopId.EndsWith(".desktop", StringComparison.Ordinal)
            ? env.AppDesktopId[..^".desktop".Length]
            : env.AppDesktopId;
        var safeScheme = new string(scheme.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_').ToArray());
        return $"{baseId}-{safeScheme}-handler.desktop";
    }

    internal static string HandlerDesktopEntry(XdgAssociationEnvironment env, string scheme)
    {
        var exec = string.Join(' ', env.AppCommand.Select(DesktopExec.Quote)) + " %u";
        var sb = new StringBuilder();
        sb.Append("[Desktop Entry]\n");
        sb.Append("Type=Application\n");
        sb.Append("Name=").Append(env.AppName).Append('\n');
        sb.Append("Exec=").Append(exec).Append('\n');
        sb.Append("NoDisplay=true\n");
        sb.Append("Terminal=false\n");
        // The process that receives the URI forwards it to the running instance and exits
        // without a window; startup notification would leave a busy cursor behind.
        sb.Append("StartupNotify=false\n");
        sb.Append("MimeType=x-scheme-handler/").Append(scheme).Append(";\n");
        return sb.ToString();
    }

    /// <summary>
    /// The application xdg-open would start for <paramref name="scheme"/>: the first
    /// installed default in the mimeapps.list files (desktop-specific lists first),
    /// else the first installed application the mimeinfo.cache files associate.
    /// </summary>
    internal static (string DesktopId, string Path, string Exec)? ResolveHandler(XdgAssociationEnvironment env, string scheme)
    {
        var key = "x-scheme-handler/" + scheme;
        var removed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var list in env.MimeAppsLists())
        {
            var sections = ReadIni(list);
            if (sections == null)
                continue;
            if (sections.TryGetValue("Removed Associations", out var rem) && rem.TryGetValue(key, out var remValue))
                removed.UnionWith(SplitList(remValue));
            if (sections.TryGetValue("Default Applications", out var defaults) && defaults.TryGetValue(key, out var value))
            {
                foreach (var id in SplitList(value))
                {
                    if (removed.Contains(id))
                        continue;
                    var found = FindDesktopFile(env, id);
                    if (found != null && ReadDesktopEntry(found) is { } entry)
                        return (id, found, entry.Exec);
                }
            }
        }

        foreach (var dir in env.ApplicationDirs())
        {
            var sections = ReadIni(Path.Combine(dir, "mimeinfo.cache"));
            if (sections == null || !sections.TryGetValue("MIME Cache", out var cache) || !cache.TryGetValue(key, out var value))
                continue;
            foreach (var id in SplitList(value))
            {
                if (removed.Contains(id))
                    continue;
                var found = FindDesktopFile(env, id);
                if (found != null && ReadDesktopEntry(found) is { } entry)
                    return (id, found, entry.Exec);
            }
        }
        return null;
    }

    /// <summary>
    /// Whether a desktop entry's Exec runs this app. As Windows does for a registry
    /// command, an Exec whose program cannot be resolved to an absolute path (a bare
    /// name found on PATH, a wrapper) counts as the app's: a false "not registered"
    /// would be worse than a false "registered".
    /// </summary>
    internal static bool IsCommandOwnedByApp(string exec, IReadOnlyList<string> appCommand)
    {
        if (string.IsNullOrWhiteSpace(exec) || appCommand.Count == 0)
            return true;
        var tokens = DesktopExec.Split(exec);
        if (tokens.Count == 0 || !Path.IsPathFullyQualified(tokens[0]))
            return true;
        // "flatpak run", "env VAR=..." and similar launchers: ambiguous.
        var program = Path.GetFileName(tokens[0]);
        if (program is "env" or "flatpak" or "sh" or "bash")
            return true;
        for (var i = 0; i < appCommand.Count; i++)
        {
            if (i >= tokens.Count || tokens[i].StartsWith('%'))
                return false;
            if (!SamePath(tokens[i], appCommand[i]))
                return false;
        }
        return true;
    }

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return true;
        }
    }

    internal static bool DeclaresScheme(string desktopFile, string scheme)
    {
        var entry = ReadDesktopEntry(desktopFile);
        if (entry == null)
            return false;
        return SplitList(entry.Value.MimeType).Any(m => string.Equals(m, "x-scheme-handler/" + scheme, StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindDesktopFile(XdgAssociationEnvironment env, string desktopId)
    {
        foreach (var dir in env.ApplicationDirs())
        {
            var direct = Path.Combine(dir, desktopId);
            if (File.Exists(direct))
                return direct;
            // Desktop file ids map "-" to subdirectories (kde4-foo.desktop -> kde4/foo.desktop).
            var dash = desktopId.IndexOf('-');
            if (dash > 0)
            {
                var nested = Path.Combine(dir, desktopId[..dash], desktopId[(dash + 1)..]);
                if (File.Exists(nested))
                    return nested;
            }
        }
        return null;
    }

    private static (string Exec, string MimeType)? ReadDesktopEntry(string path)
    {
        var sections = ReadIni(path);
        if (sections == null || !sections.TryGetValue("Desktop Entry", out var entry))
            return null;
        if (entry.TryGetValue("Hidden", out var hidden) && hidden.Equals("true", StringComparison.OrdinalIgnoreCase))
            return null;
        entry.TryGetValue("Exec", out var exec);
        entry.TryGetValue("MimeType", out var mime);
        return (exec ?? string.Empty, mime ?? string.Empty);
    }

    private static IEnumerable<string> SplitList(string value)
        => value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Dictionary<string, Dictionary<string, string>>? ReadIni(string path)
    {
        string[] lines;
        try
        {
            if (!File.Exists(path))
                return null;
            lines = File.ReadAllLines(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        Dictionary<string, string>? current = null;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;
            if (line[0] == '[' && line[^1] == ']')
            {
                var name = line[1..^1];
                if (!result.TryGetValue(name, out current))
                    result[name] = current = new Dictionary<string, string>(StringComparer.Ordinal);
                continue;
            }
            var eq = line.IndexOf('=');
            if (current == null || eq <= 0)
                continue;
            var key = line[..eq].Trim();
            // First occurrence wins, as in GLib's key file.
            current.TryAdd(key, line[(eq + 1)..].Trim());
        }
        return result;
    }

    private static int FindSection(List<string> lines, string header)
        => lines.FindIndex(l => l.Trim() == header);

    private static int SectionEnd(List<string> lines, int start)
    {
        var i = start + 1;
        while (i < lines.Count && !(lines[i].TrimStart().StartsWith('[')))
            i++;
        // Keep trailing blank lines outside the section.
        while (i > start + 1 && string.IsNullOrWhiteSpace(lines[i - 1]))
            i--;
        return i;
    }

    private static int FindKey(List<string> lines, int start, int end, string key)
    {
        for (var i = start + 1; i < end; i++)
        {
            var line = lines[i].Trim();
            var eq = line.IndexOf('=');
            if (eq > 0 && line[..eq].Trim() == key)
                return i;
        }
        return -1;
    }

    private static void SetEntry(List<string> lines, string header, string key, string desktopId)
    {
        var start = FindSection(lines, header);
        if (start < 0)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                lines.Add(string.Empty);
            lines.Add(header);
            lines.Add($"{key}={desktopId};");
            return;
        }
        var end = SectionEnd(lines, start);
        var at = FindKey(lines, start, end, key);
        if (at >= 0)
            lines[at] = $"{key}={desktopId};";
        else
            lines.Insert(end, $"{key}={desktopId};");
    }

    private static void AddToEntry(List<string> lines, string header, string key, string desktopId)
    {
        var start = FindSection(lines, header);
        if (start < 0)
        {
            SetEntry(lines, header, key, desktopId);
            return;
        }
        var end = SectionEnd(lines, start);
        var at = FindKey(lines, start, end, key);
        if (at < 0)
        {
            lines.Insert(end, $"{key}={desktopId};");
            return;
        }
        var line = lines[at];
        var values = SplitList(line[(line.IndexOf('=') + 1)..]).Where(v => v != desktopId).ToList();
        values.Insert(0, desktopId);
        lines[at] = $"{key}={string.Join(';', values)};";
    }

    private static bool RemoveFromEntries(List<string> lines, string key, string desktopId)
    {
        var changed = false;
        foreach (var header in new[] { "[Default Applications]", "[Added Associations]" })
        {
            var start = FindSection(lines, header);
            if (start < 0)
                continue;
            var end = SectionEnd(lines, start);
            var at = FindKey(lines, start, end, key);
            if (at < 0)
                continue;
            var line = lines[at];
            var values = SplitList(line[(line.IndexOf('=') + 1)..]).ToList();
            if (!values.Remove(desktopId))
                continue;
            changed = true;
            if (values.Count == 0)
                lines.RemoveAt(at);
            else
                lines[at] = $"{key}={string.Join(';', values)};";
        }
        return changed;
    }
}

/// <summary>
/// The XDG base directories and app identity <see cref="UriSchemeRegistration"/> works
/// with. Tests build one over temporary directories so the user's associations are
/// never read or written.
/// </summary>
internal sealed record XdgAssociationEnvironment(
    string ConfigHome,
    IReadOnlyList<string> ConfigDirs,
    string DataHome,
    IReadOnlyList<string> DataDirs,
    IReadOnlyList<string> CurrentDesktops,
    IReadOnlyList<string> AppCommand,
    string AppDesktopId,
    string AppName,
    string? FlatpakDesktopFile = null)
{
    internal static XdgAssociationEnvironment FromProcess()
    {
        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        static string Env(string name, string fallback)
            => System.Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : fallback;
        static string[] List(string value) => value.Split(':', StringSplitOptions.RemoveEmptyEntries);

        string? flatpakDesktop = null;
        var flatpakId = System.Environment.GetEnvironmentVariable("FLATPAK_ID");
        if (!string.IsNullOrEmpty(flatpakId) && DesktopPortal.IsSandboxed)
            flatpakDesktop = Path.Combine("/app/share/applications", flatpakId + ".desktop");

        var command = AppHandlerCommand();
        var name = command.Count > 0 ? Path.GetFileNameWithoutExtension(command[^1]) : "MauiApp";
        return new XdgAssociationEnvironment(
            Env("XDG_CONFIG_HOME", Path.Combine(home, ".config")),
            List(Env("XDG_CONFIG_DIRS", "/etc/xdg")),
            Env("XDG_DATA_HOME", Path.Combine(home, ".local", "share")),
            List(Env("XDG_DATA_DIRS", "/usr/local/share:/usr/share")),
            List(Env("XDG_CURRENT_DESKTOP", string.Empty)).Select(d => d.ToLowerInvariant()).ToArray(),
            command,
            LauncherBadgeService.DesktopFileId(),
            name,
            flatpakDesktop);
    }

    /// <summary>
    /// The command that starts this app: the AppImage file (its mount point changes on
    /// every run), the apphost, or <c>dotnet app.dll</c> for a framework-dependent launch.
    /// </summary>
    internal static IReadOnlyList<string> AppHandlerCommand()
    {
        if (AppInfoService.IsRunningFromAppImage() && System.Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 } appImage)
            return new[] { appImage };
        var processPath = System.Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath))
            return Array.Empty<string>();
        if (Path.GetFileNameWithoutExtension(processPath) == "dotnet")
        {
            var entry = System.Reflection.Assembly.GetEntryAssembly()?.Location;
            return string.IsNullOrEmpty(entry) ? new[] { processPath } : new[] { processPath, entry };
        }
        return new[] { processPath };
    }

    /// <summary>The mimeapps.list files in the XDG precedence order (desktop-specific first in each directory).</summary>
    internal IEnumerable<string> MimeAppsLists()
    {
        IEnumerable<string> In(string dir)
        {
            foreach (var desktop in CurrentDesktops)
                yield return Path.Combine(dir, desktop + "-mimeapps.list");
            yield return Path.Combine(dir, "mimeapps.list");
        }

        foreach (var file in In(ConfigHome))
            yield return file;
        foreach (var dir in ConfigDirs)
            foreach (var file in In(dir))
                yield return file;
        foreach (var dir in ApplicationDirs())
            foreach (var file in In(dir))
                yield return file;
    }

    /// <summary>The applications directories, user first.</summary>
    internal IEnumerable<string> ApplicationDirs()
    {
        yield return Path.Combine(DataHome, "applications");
        foreach (var dir in DataDirs)
            yield return Path.Combine(dir, "applications");
    }
}

/// <summary>Quoting and splitting of desktop-entry Exec values (Desktop Entry Specification, "The Exec key").</summary>
internal static class DesktopExec
{
    private const string Reserved = " \t\n\"'\\><~|&;$*?#()`";

    /// <summary>Quotes one argument for an Exec value, including the string-level backslash escape.</summary>
    internal static string Quote(string arg)
    {
        var escapedPercent = arg.Replace("%", "%%", StringComparison.Ordinal);
        if (escapedPercent.Length > 0 && escapedPercent.IndexOfAny(Reserved.ToCharArray()) < 0)
            return escapedPercent;
        var sb = new StringBuilder("\"");
        foreach (var c in escapedPercent)
        {
            if (c is '"' or '`' or '$' or '\\')
                sb.Append('\\');
            sb.Append(c);
        }
        sb.Append('"');
        // Desktop files escape backslashes once more at the string level.
        return sb.ToString().Replace("\\", "\\\\", StringComparison.Ordinal);
    }

    /// <summary>Splits an Exec value into arguments (field codes are kept as tokens).</summary>
    internal static IReadOnlyList<string> Split(string exec)
    {
        // String-level unescape first (\\ -> \, \s -> space, ...).
        var unescaped = new StringBuilder();
        for (var i = 0; i < exec.Length; i++)
        {
            if (exec[i] == '\\' && i + 1 < exec.Length)
            {
                var next = exec[++i];
                unescaped.Append(next switch { 's' => ' ', 'n' => '\n', 't' => '\t', 'r' => '\r', _ => next });
                if (next is not ('\\' or 's' or 'n' or 't' or 'r'))
                    unescaped.Insert(unescaped.Length - 1, '\\');
            }
            else
            {
                unescaped.Append(exec[i]);
            }
        }

        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var hasToken = false;
        var s = unescaped.ToString();
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (inQuotes)
            {
                if (c == '\\' && i + 1 < s.Length && s[i + 1] is '"' or '`' or '$' or '\\')
                {
                    current.Append(s[++i]);
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
                continue;
            }
            if (c == '"')
            {
                inQuotes = true;
                hasToken = true;
            }
            else if (c is ' ' or '\t')
            {
                if (hasToken || current.Length > 0)
                    tokens.Add(current.ToString().Replace("%%", "%", StringComparison.Ordinal));
                current.Clear();
                hasToken = false;
            }
            else
            {
                current.Append(c);
                hasToken = true;
            }
        }
        if (hasToken || current.Length > 0)
            tokens.Add(current.ToString().Replace("%%", "%", StringComparison.Ordinal));
        return tokens;
    }
}
