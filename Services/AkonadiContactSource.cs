// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.ApplicationModel;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>Where the KDE contact stores are looked for (tests point it at a temporary home).</summary>
internal sealed record KdeContactPaths(string Home, string ConfigHome, string DataHome)
{
    /// <summary>
    /// The user's directories. Akonadi's resources run outside any sandbox, so inside Flatpak
    /// (whose XDG_CONFIG_HOME and XDG_DATA_HOME are the app's own) and Snap (whose HOME is) the
    /// host's defaults under the real home are used; the app then needs read access to them.
    /// </summary>
    public static KdeContactPaths FromEnvironment()
        => FromEnvironment(Environment.GetEnvironmentVariable, File.Exists("/.flatpak-info"));

    internal static KdeContactPaths FromEnvironment(Func<string, string?> env, bool flatpak)
    {
        var home = NonEmpty(env("SNAP_REAL_HOME")) ?? NonEmpty(env("HOME")) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var sandboxed = flatpak || NonEmpty(env("FLATPAK_ID")) != null || NonEmpty(env("SNAP")) != null;
        var config = (sandboxed ? null : NonEmpty(env("XDG_CONFIG_HOME"))) ?? Path.Combine(home, ".config");
        var data = (sandboxed ? null : NonEmpty(env("XDG_DATA_HOME"))) ?? Path.Combine(home, ".local", "share");
        return new KdeContactPaths(home, config, data);
    }

    private static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}

/// <summary>How a KDE contact store keeps its vCards.</summary>
internal enum KdeContactStoreKind
{
    /// <summary>akonadi_contacts_resource: one "*.vcf" per contact, sub-folders are sub-address-books.</summary>
    ContactsDirectory,

    /// <summary>akonadi_vcarddir_resource: one file per contact (no extension) in one folder.</summary>
    VCardDirectory,

    /// <summary>akonadi_vcard_resource: every contact in one .vcf file.</summary>
    VCardFile,

    /// <summary>KPeople's vCard folder (~/.local/share/kpeoplevcard, where KDE Connect syncs a phone's contacts).</summary>
    KPeopleDirectory,
}

/// <summary>One KDE contact store: the Akonadi resource instance (or "kpeoplevcard") and its path.</summary>
internal sealed record KdeContactStore(string Id, KdeContactStoreKind Kind, string Path);

/// <summary>
/// KDE's contacts where they are files: Akonadi's vCard resources (the default "Personal
/// Contacts" of akonadi_contacts_resource in ~/.local/share/contacts, vCard folders and vCard
/// files the user added) and KPeople's vCard folder. The instances are listed in
/// ~/.config/akonadi/agentsrc and each one's Path is in ~/.config/&lt;instance&gt;rc.
/// Contacts Akonadi keeps only in its database (CardDAV through akonadi_davgroupware_resource,
/// Google, EWS, Kolab) are not files and are not read: the database is Akonadi's private MySQL
/// (or PostgreSQL/SQLite) server, and items reach clients over Akonadi's own socket protocol,
/// not D-Bus.
/// </summary>
internal sealed class AkonadiContactSource : IContactSource
{
    public static readonly AkonadiContactSource Default = new(KdeContactPaths.FromEnvironment);

    internal const string ContactsResource = "akonadi_contacts_resource";
    internal const string VCardDirResource = "akonadi_vcarddir_resource";
    internal const string VCardResource = "akonadi_vcard_resource";
    private const string ReadMe = "WARNING_README.txt";

    /// <summary>Resources whose contacts live only in Akonadi's database.</summary>
    private static readonly string[] DatabaseOnlyResources =
    {
        "akonadi_davgroupware_resource", "akonadi_google_resource", "akonadi_ews_resource",
        "akonadi_kolab_resource", "akonadi_openxchange_resource", "akonadi_etesync_resource",
    };

    private readonly Func<KdeContactPaths> _paths;

    internal AkonadiContactSource(Func<KdeContactPaths> paths)
    {
        _paths = paths;
    }

    internal AkonadiContactSource(KdeContactPaths paths)
        : this(() => paths)
    {
    }

    public Task<IReadOnlyList<string>> GetVCardsAsync(CancellationToken cancellationToken)
        => Task.Run(() => ReadAll(cancellationToken), cancellationToken);

    private IReadOnlyList<string> ReadAll(CancellationToken cancellationToken)
    {
        var stores = Discover(_paths());
        if (stores.Count == 0)
            throw new FeatureNotSupportedException("No KDE contacts: there is no Akonadi vCard resource or KPeople vCard folder.");

        var cards = new List<string>();
        var denied = 0;
        foreach (var store in stores)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                cards.AddRange(ReadStore(store, cancellationToken));
            }
            catch (UnauthorizedAccessException ex)
            {
                denied++;
                DiagnosticLog.Warn("Contacts", $"KDE contact store {store.Id} could not be read: {ex.Message}");
            }
        }
        if (denied == stores.Count)
            throw new PermissionException("Permission to read the KDE contacts was denied.");
        return cards;
    }

    /// <summary>The KDE contact stores under <paramref name="paths"/>, each path once.</summary>
    internal static IReadOnlyList<KdeContactStore> Discover(KdeContactPaths paths)
    {
        var stores = new List<KdeContactStore>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(KdeContactStore store)
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(store.Path));
            if (seen.Add(full))
                stores.Add(store with { Path = full });
        }

        foreach (var (id, type) in ResourceInstances(paths))
        {
            if (DatabaseOnlyResources.Contains(type))
            {
                DiagnosticLog.Debug("Contacts", $"Akonadi resource {id} ({type}) keeps its contacts in Akonadi's database; they are not read.");
                continue;
            }
            KdeContactStoreKind kind;
            if (type == ContactsResource)
                kind = KdeContactStoreKind.ContactsDirectory;
            else if (type == VCardDirResource)
                kind = KdeContactStoreKind.VCardDirectory;
            else if (type == VCardResource)
                kind = KdeContactStoreKind.VCardFile;
            else
                continue;

            var path = ResourcePath(paths, id, type);
            if (path == null)
            {
                DiagnosticLog.Debug("Contacts", $"Akonadi resource {id} has no local path.");
                continue;
            }
            Add(new KdeContactStore(id, kind, path));
        }

        var kpeople = Path.Combine(paths.DataHome, "kpeoplevcard");
        if (Directory.Exists(kpeople))
            Add(new KdeContactStore("kpeoplevcard", KdeContactStoreKind.KPeopleDirectory, kpeople));
        return stores;
    }

    /// <summary>
    /// The (instance id, agent type) of every Akonadi agent: agentsrc's [Instances] group
    /// ("akonadi_contacts_resource_0\AgentType=akonadi_contacts_resource"), or, without
    /// agentsrc, the vCard resources that have an "&lt;instance&gt;rc" file.
    /// </summary>
    internal static IReadOnlyList<(string Id, string Type)> ResourceInstances(KdeContactPaths paths)
    {
        var instances = new List<(string, string)>();
        var agentsrc = Path.Combine(paths.ConfigHome, "akonadi", "agentsrc");
        if (File.Exists(agentsrc))
        {
            foreach (var (group, key, value) in ReadIni(agentsrc))
            {
                if (group != "Instances")
                    continue;
                var slash = key.IndexOf('\\');
                if (slash > 0 && key[(slash + 1)..] == "AgentType")
                    instances.Add((key[..slash], value));
            }
            return instances;
        }

        if (!Directory.Exists(paths.ConfigHome))
            return instances;
        foreach (var file in Directory.EnumerateFiles(paths.ConfigHome, "akonadi_*_resource_*rc").Order(StringComparer.Ordinal))
        {
            var id = Path.GetFileName(file)[..^2];
            var number = id.LastIndexOf('_');
            if (number <= 0 || number == id.Length - 1 || !id[(number + 1)..].All(char.IsAsciiDigit))
                continue;
            var type = id[..number];
            if (type is ContactsResource or VCardDirResource or VCardResource)
                instances.Add((id, type));
        }
        return instances;
    }

    /// <summary>
    /// A resource's local path: [General] Path in its rc file (KConfig's "$HOME" and "[$e]"
    /// expansion, "file://" URLs), else the contacts resource's default $HOME/.local/share/contacts/.
    /// </summary>
    internal static string? ResourcePath(KdeContactPaths paths, string id, string type)
    {
        string? path = null;
        var rc = Path.Combine(paths.ConfigHome, id + "rc");
        if (File.Exists(rc))
        {
            foreach (var (group, key, value) in ReadIni(rc))
            {
                if (group != "General")
                    continue;
                if (key == "Path")
                    path = UnescapeKConfig(value);
                else if (key == "Path[$e]")
                    path = Expand(UnescapeKConfig(value), paths);
            }
        }

        if (string.IsNullOrWhiteSpace(path))
            return type == ContactsResource ? Path.Combine(paths.Home, ".local", "share", "contacts") : null;

        if (path.StartsWith("$HOME", StringComparison.Ordinal) || path.StartsWith("${HOME}", StringComparison.Ordinal))
            path = Expand(path, paths);
        if (path.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(path, UriKind.Absolute, out var uri) || !uri.IsFile)
                return null;
            path = uri.LocalPath;
        }
        if (path.StartsWith("~/", StringComparison.Ordinal))
            path = Path.Combine(paths.Home, path[2..]);
        return Path.IsPathRooted(path) ? path : null;
    }

    /// <summary>KConfig's "$VAR" / "${VAR}" expansion ("$$" a literal '$'); HOME is the seam's home.</summary>
    internal static string Expand(string value, KdeContactPaths paths)
    {
        var sb = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] != '$' || i + 1 >= value.Length)
            {
                sb.Append(value[i]);
                continue;
            }
            if (value[i + 1] == '$')
            {
                sb.Append('$');
                i++;
                continue;
            }
            string name;
            if (value[i + 1] == '{')
            {
                var close = value.IndexOf('}', i + 2);
                if (close < 0)
                {
                    sb.Append(value[i]);
                    continue;
                }
                name = value[(i + 2)..close];
                i = close;
            }
            else
            {
                var end = i + 1;
                while (end < value.Length && (char.IsAsciiLetterOrDigit(value[end]) || value[end] == '_'))
                    end++;
                if (end == i + 1)
                {
                    sb.Append(value[i]);
                    continue;
                }
                name = value[(i + 1)..end];
                i = end - 1;
            }
            sb.Append(name == "HOME" ? paths.Home : Environment.GetEnvironmentVariable(name) ?? string.Empty);
        }
        return sb.ToString();
    }

    /// <summary>KConfig value escapes: \\, \n, \t, \r, \s (a space), \xNN.</summary>
    private static string UnescapeKConfig(string value)
    {
        if (value.IndexOf('\\') < 0)
            return value;
        var sb = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                sb.Append(value[i]);
                continue;
            }
            var next = value[++i];
            switch (next)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 's': sb.Append(' '); break;
                case 'x' when i + 2 < value.Length && byte.TryParse(value.AsSpan(i + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var b):
                    sb.Append((char)b);
                    i += 2;
                    break;
                default: sb.Append(next); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>The (group, key, value) entries of an INI-style file (KConfig or QSettings).</summary>
    private static IEnumerable<(string Group, string Key, string Value)> ReadIni(string file)
    {
        string group = string.Empty;
        foreach (var raw in File.ReadLines(file))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or ';')
                continue;
            if (line[0] == '[')
            {
                // KConfig nests as "[A][B]"; the first group is the one the keys here live in.
                var close = line.IndexOf(']');
                group = close > 0 ? line[1..close] : line[1..];
                continue;
            }
            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;
            var value = line[(eq + 1)..].Trim();
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                value = value[1..^1];
            yield return (group, line[..eq].Trim(), value);
        }
    }

    /// <summary>The vCards of one store, each known by its file name when it has no UID.</summary>
    internal static IEnumerable<string> ReadStore(KdeContactStore store, CancellationToken cancellationToken)
    {
        if (store.Kind == KdeContactStoreKind.VCardFile)
        {
            if (!File.Exists(store.Path))
            {
                DiagnosticLog.Debug("Contacts", $"vCard file of {store.Id} does not exist.");
                yield break;
            }
            var index = 0;
            foreach (var card in VCard.Split(VCard.ReadFileText(File.ReadAllBytes(store.Path))))
                yield return VCard.WithUid(card, $"{store.Id}:{index++}");
            yield break;
        }

        if (!Directory.Exists(store.Path))
        {
            // Akonadi creates the Personal Contacts folder on its first run.
            DiagnosticLog.Debug("Contacts", $"Folder of {store.Id} does not exist.");
            yield break;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = store.Kind != KdeContactStoreKind.VCardDirectory,
            MaxRecursionDepth = 16,
            IgnoreInaccessible = true,
        };
        var pattern = store.Kind == KdeContactStoreKind.VCardDirectory ? "*" : "*.vcf";
        foreach (var file in Directory.EnumerateFiles(store.Path, pattern, options).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Path.GetFileName(file) == ReadMe)
                continue;
            string text;
            try
            {
                text = VCard.ReadFileText(File.ReadAllBytes(file));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                DiagnosticLog.Warn("Contacts", $"A contact file of {store.Id} could not be read: {ex.Message}");
                continue;
            }
            var cards = VCard.Split(text);
            var name = Path.GetFileNameWithoutExtension(file);
            for (int i = 0; i < cards.Count; i++)
                yield return VCard.WithUid(cards[i], cards.Count == 1 ? name : $"{name}:{i}");
        }
    }
}
