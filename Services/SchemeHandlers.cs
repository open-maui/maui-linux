// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services.Portal;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Whether the desktop has an application for a URI scheme (mailto:, sms:, tel:), from the XDG
/// MIME associations xdg-open itself uses: mimeapps.list (user, system, desktop) and the
/// applications' mimeinfo.cache. A sandboxed app cannot see the host's list; the portal's app
/// chooser handles the URI there, so the answer is yes.
/// </summary>
internal static class SchemeHandlers
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);
    private static readonly Dictionary<string, (bool Has, DateTime At)> s_cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly AsyncLocal<IReadOnlyList<string>?> s_searchFilesOverride = new();

    /// <summary>The association files searched, for the current test's flow; null uses the XDG environment.</summary>
    internal static IReadOnlyList<string>? SearchFilesOverride
    {
        get => s_searchFilesOverride.Value;
        set => s_searchFilesOverride.Value = value;
    }

    public static bool HasHandler(string scheme)
    {
        if (SearchFilesOverride == null && DesktopPortal.IsSandboxed)
            return true;
        lock (s_cache)
        {
            if (SearchFilesOverride == null && s_cache.TryGetValue(scheme, out var hit) && DateTime.UtcNow - hit.At < CacheFor)
                return hit.Has;
        }
        var has = Lookup(scheme);
        lock (s_cache)
            s_cache[scheme] = (has, DateTime.UtcNow);
        return has;
    }

    private static bool Lookup(string scheme)
    {
        var key = "x-scheme-handler/" + scheme.ToLowerInvariant() + "=";
        var removed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in SearchFilesOverride ?? AssociationFiles())
        {
            string[] lines;
            try
            {
                if (!File.Exists(file))
                    continue;
                lines = File.ReadAllLines(file);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            var section = "";
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    section = line;
                    continue;
                }
                if (!line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    continue;
                var apps = line[key.Length..].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (section == "[Removed Associations]")
                {
                    removed.UnionWith(apps);
                    continue;
                }
                if (apps.Any(app => !removed.Contains(app)))
                    return true;
            }
        }
        return false;
    }

    /// <summary>The association files in xdg-open's order of precedence.</summary>
    private static IEnumerable<string> AssociationFiles()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string Env(string name, string fallback) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : fallback;
        var configHome = Env("XDG_CONFIG_HOME", Path.Combine(home, ".config"));
        var configDirs = Env("XDG_CONFIG_DIRS", "/etc/xdg").Split(':', StringSplitOptions.RemoveEmptyEntries);
        var dataHome = Env("XDG_DATA_HOME", Path.Combine(home, ".local", "share"));
        var dataDirs = Env("XDG_DATA_DIRS", "/usr/local/share:/usr/share").Split(':', StringSplitOptions.RemoveEmptyEntries);

        yield return Path.Combine(configHome, "mimeapps.list");
        foreach (var dir in configDirs)
            yield return Path.Combine(dir, "mimeapps.list");
        foreach (var dir in new[] { dataHome }.Concat(dataDirs))
        {
            yield return Path.Combine(dir, "applications", "mimeapps.list");
            yield return Path.Combine(dir, "applications", "mimeinfo.cache");
        }
    }
}
