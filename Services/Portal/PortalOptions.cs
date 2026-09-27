// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text;

namespace Microsoft.Maui.Platform.Linux.Services.Portal;

/// <summary>Conversions for values that arrive inside D-Bus variants.</summary>
internal static class PortalVariant
{
    public static uint? ToUInt32(object? value) => value switch
    {
        uint u => u,
        int i when i >= 0 => (uint)i,
        byte b => b,
        ushort us => us,
        short s when s >= 0 => (uint)s,
        ulong ul when ul <= uint.MaxValue => (uint)ul,
        long l when l >= 0 && l <= uint.MaxValue => (uint)l,
        _ => null,
    };

    public static double? ToDouble(object? value) => value switch
    {
        double d => d,
        float f => f,
        int i => i,
        uint u => u,
        long l => l,
        ulong ul => ul,
        short s => s,
        ushort us => us,
        byte b => b,
        _ => null,
    };

    /// <summary>Items of a D-Bus struct as Tmds.DBus hands it over (ValueTuple or object[]).</summary>
    public static object?[]? StructItems(object? value)
    {
        if (value is object?[] array)
            return array;
        if (value is System.Runtime.CompilerServices.ITuple tuple)
        {
            var items = new object?[tuple.Length];
            for (var i = 0; i < tuple.Length; i++)
                items[i] = tuple[i];
            return items;
        }
        return null;
    }
}

/// <summary>A FileChooser filter: a name plus glob and MIME patterns.</summary>
internal sealed record PortalFileFilter(string Name, IReadOnlyList<string> Globs, IReadOnlyList<string>? MimeTypes = null)
{
    /// <summary>The (sa(us)) struct: type 0 is a glob, type 1 a MIME type.</summary>
    public (string, (uint, string)[]) ToVariant()
    {
        var patterns = new List<(uint, string)>();
        foreach (var glob in Globs)
            patterns.Add((0u, glob));
        if (MimeTypes != null)
        {
            foreach (var mime in MimeTypes)
                patterns.Add((1u, mime));
        }
        return (Name, patterns.ToArray());
    }

    /// <summary>
    /// One filter holding "*.ext" globs for dotted extensions (".png"), the
    /// same patterns the zenity/kdialog fallbacks use. Null for an empty list.
    /// </summary>
    public static PortalFileFilter? FromExtensions(string name, IReadOnlyCollection<string> extensions)
    {
        if (extensions.Count == 0)
            return null;
        return new PortalFileFilter(name, extensions.Select(e => "*" + e).ToArray());
    }
}

/// <summary>Parameters for FileChooser.OpenFile / SaveFile.</summary>
internal sealed class PortalFileChooserRequest
{
    public string Title { get; init; } = "";
    public string? AcceptLabel { get; init; }
    public bool Modal { get; init; } = true;
    public bool Multiple { get; init; }
    public bool Directory { get; init; }
    public IReadOnlyList<PortalFileFilter> Filters { get; init; } = Array.Empty<PortalFileFilter>();
    public PortalFileFilter? CurrentFilter { get; init; }
    public string? CurrentFolder { get; init; }

    /// <summary>SaveFile only: the suggested file name.</summary>
    public string? CurrentName { get; init; }
}

/// <summary>
/// Builders for the a{sv} option dictionaries each portal method takes, and
/// parsers for the results. All pure: unit-tested without a bus.
/// </summary>
internal static class PortalOptions
{
    public const string HandleToken = "handle_token";

    /// <summary>A copy of <paramref name="options"/> with handle_token set.</summary>
    public static Dictionary<string, object> WithHandleToken(IDictionary<string, object>? options, string token)
    {
        var copy = options is null
            ? new Dictionary<string, object>(StringComparer.Ordinal)
            : new Dictionary<string, object>(options, StringComparer.Ordinal);
        copy[HandleToken] = token;
        return copy;
    }

    /// <summary>The "ay" form of a path: UTF-8 with a trailing NUL, as the spec requires.</summary>
    public static byte[] PathBytes(string path)
    {
        var bytes = Encoding.UTF8.GetBytes(path);
        var result = new byte[bytes.Length + 1];
        Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
        return result;
    }

    public static Dictionary<string, object> FileChooserOpen(PortalFileChooserRequest request)
    {
        var options = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["modal"] = request.Modal,
            ["multiple"] = request.Multiple,
        };
        if (request.Directory)
            options["directory"] = true;
        AddCommonFileChooser(options, request);
        return options;
    }

    public static Dictionary<string, object> FileChooserSave(PortalFileChooserRequest request)
    {
        var options = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["modal"] = request.Modal,
        };
        if (!string.IsNullOrEmpty(request.CurrentName))
            options["current_name"] = request.CurrentName!;
        AddCommonFileChooser(options, request);
        return options;
    }

    private static void AddCommonFileChooser(Dictionary<string, object> options, PortalFileChooserRequest request)
    {
        if (!string.IsNullOrEmpty(request.AcceptLabel))
            options["accept_label"] = request.AcceptLabel!;
        if (request.Filters.Count > 0)
            options["filters"] = request.Filters.Select(f => f.ToVariant()).ToArray();
        var current = request.CurrentFilter ?? (request.Filters.Count > 0 ? request.Filters[0] : null);
        if (current != null)
            options["current_filter"] = current.ToVariant();
        if (!string.IsNullOrEmpty(request.CurrentFolder))
            options["current_folder"] = PathBytes(request.CurrentFolder!);
    }

    /// <summary>
    /// Local paths for the "uris" result. file:// URIs are unescaped; anything
    /// else (a non-local URI a backend might return) is skipped because a
    /// FileResult needs a path.
    /// </summary>
    public static List<string> UrisToLocalPaths(IEnumerable<string> uris)
    {
        var paths = new List<string>();
        foreach (var raw in uris)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            if (Uri.TryCreate(raw, UriKind.Absolute, out var uri) && uri.IsFile)
                paths.Add(uri.LocalPath);
            else if (raw.StartsWith('/'))
                paths.Add(raw);
        }
        return paths;
    }

    public static Dictionary<string, object> OpenUri(bool ask = false, bool writable = false, string? activationToken = null)
    {
        var options = new Dictionary<string, object>(StringComparer.Ordinal);
        if (ask)
            options["ask"] = true;
        if (writable)
            options["writable"] = true;
        if (!string.IsNullOrEmpty(activationToken))
            options["activation_token"] = activationToken!;
        return options;
    }

    public static Dictionary<string, object> Screenshot(bool interactive, bool modal = true)
        => new(StringComparer.Ordinal)
        {
            ["modal"] = modal,
            ["interactive"] = interactive,
        };

    public static Dictionary<string, object> Inhibit(string? reason)
    {
        var options = new Dictionary<string, object>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(reason))
            options["reason"] = reason!;
        return options;
    }

    public static Dictionary<string, object> Background(string? reason, bool autostart, IReadOnlyList<string>? commandLine, bool dbusActivatable)
    {
        var options = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["autostart"] = autostart,
            ["dbus-activatable"] = dbusActivatable,
        };
        if (!string.IsNullOrEmpty(reason))
            options["reason"] = reason!;
        if (commandLine is { Count: > 0 })
            options["commandline"] = commandLine.ToArray();
        return options;
    }

    /// <summary>
    /// Portal Notification v1 dictionary: title, body, priority, optional
    /// themed icon and buttons (action names are prefixed "app." as the spec
    /// expects for exported actions; ActionInvoked reports them back).
    /// </summary>
    public static Dictionary<string, object> Notification(NotificationOptions options, IEnumerable<KeyValuePair<string, string>>? buttons)
    {
        var dict = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["title"] = options.Title ?? "",
            ["body"] = options.Message ?? "",
            ["priority"] = options.Urgency switch
            {
                NotificationUrgency.Low => "low",
                NotificationUrgency.Critical => "urgent",
                _ => "normal",
            },
        };

        if (!string.IsNullOrEmpty(options.IconName))
            dict["icon"] = ("themed", (object)new[] { options.IconName! });

        if (buttons != null)
        {
            var list = new List<IDictionary<string, object>>();
            foreach (var (action, label) in buttons)
            {
                list.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["label"] = label,
                    ["action"] = PortalActionName(action),
                });
            }
            if (list.Count > 0)
                dict["buttons"] = list.ToArray();
        }

        return dict;
    }

    public const string PortalActionPrefix = "app.";

    public static string PortalActionName(string key) => PortalActionPrefix + key;

    /// <summary>The caller's action key for an ActionInvoked action name.</summary>
    public static string ActionKeyFromPortal(string action)
        => action.StartsWith(PortalActionPrefix, StringComparison.Ordinal) ? action.Substring(PortalActionPrefix.Length) : action;

    /// <summary>
    /// Freedesktop Notify hints for the unsandboxed path (same meaning as the
    /// notify-send flags the service used before).
    /// </summary>
    public static Dictionary<string, object> NotifyHints(NotificationOptions options)
    {
        var hints = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["urgency"] = (byte)(options.Urgency switch
            {
                NotificationUrgency.Low => 0,
                NotificationUrgency.Critical => 2,
                _ => 1,
            }),
        };
        if (!string.IsNullOrEmpty(options.Category))
            hints["category"] = options.Category!;
        if (options.IsTransient)
            hints["transient"] = true;
        return hints;
    }

    /// <summary>Freedesktop "as" actions: key, label, key, label, ...</summary>
    public static string[] NotifyActions(IEnumerable<KeyValuePair<string, string>>? actions)
    {
        if (actions == null)
            return Array.Empty<string>();
        var list = new List<string>();
        foreach (var (key, label) in actions)
        {
            list.Add(key);
            list.Add(label);
        }
        return list.ToArray();
    }

    /// <summary>Location session options: accuracy is 0 (none) .. 5 (exact).</summary>
    public static Dictionary<string, object> LocationSession(uint accuracy, string sessionToken)
        => new(StringComparer.Ordinal)
        {
            ["session_handle_token"] = sessionToken,
            ["accuracy"] = Math.Min(accuracy, 5u),
            ["distance-threshold"] = 0u,
            ["time-threshold"] = 0u,
        };
}

/// <summary>org.freedesktop.appearance color-scheme values.</summary>
internal enum PortalColorScheme : uint
{
    NoPreference = 0,
    PreferDark = 1,
    PreferLight = 2,
}

/// <summary>Parsers for org.freedesktop.appearance settings.</summary>
internal static class PortalAppearance
{
    public const string Namespace = "org.freedesktop.appearance";
    public const string ColorSchemeKey = "color-scheme";
    public const string AccentColorKey = "accent-color";
    public const string ContrastKey = "contrast";

    public const string GnomeInterfaceNamespace = "org.gnome.desktop.interface";

    /// <summary>color-scheme u; unknown values read as no preference; null when absent.</summary>
    public static PortalColorScheme? ParseColorScheme(object? value)
    {
        var raw = PortalVariant.ToUInt32(value);
        if (raw is null)
            return null;
        return raw.Value switch
        {
            1 => PortalColorScheme.PreferDark,
            2 => PortalColorScheme.PreferLight,
            _ => PortalColorScheme.NoPreference,
        };
    }

    /// <summary>
    /// accent-color (ddd) in sRGB 0..1. Out-of-range components mean "unset"
    /// per the spec, so the whole value is rejected.
    /// </summary>
    public static (byte R, byte G, byte B)? ParseAccentColor(object? value)
    {
        var items = PortalVariant.StructItems(value);
        if (items == null || items.Length != 3)
            return null;
        var rgb = new byte[3];
        for (var i = 0; i < 3; i++)
        {
            var d = PortalVariant.ToDouble(items[i]);
            if (d is null || double.IsNaN(d.Value) || d < 0 || d > 1)
                return null;
            rgb[i] = (byte)Math.Round(d.Value * 255.0);
        }
        return (rgb[0], rgb[1], rgb[2]);
    }

    /// <summary>A positive number from a gsettings-style value (u, i, d).</summary>
    public static double? ParsePositiveNumber(object? value)
    {
        var d = PortalVariant.ToDouble(value);
        return d is > 0 ? d : null;
    }
}

/// <summary>A decoded LocationUpdated payload.</summary>
internal sealed record PortalLocationFix(
    double Latitude,
    double Longitude,
    double? Altitude,
    double? Accuracy,
    double? Speed,
    double? Heading,
    DateTimeOffset? Timestamp)
{
    /// <summary>
    /// Reads the a{sv} of LocationUpdated. Latitude/Longitude are required;
    /// the spec uses -G_MAXDOUBLE for unknown altitude/speed/heading, which
    /// maps to null here.
    /// </summary>
    public static PortalLocationFix? Parse(IReadOnlyDictionary<string, object>? location)
    {
        if (location == null)
            return null;
        double? Get(string key) => location.TryGetValue(key, out var v) ? PortalVariant.ToDouble(v) : null;
        double? Known(string key)
        {
            var v = Get(key);
            return v is null || v <= -1e300 || double.IsNaN(v.Value) ? null : v;
        }

        var lat = Get("Latitude");
        var lon = Get("Longitude");
        if (lat is null || lon is null || lat is < -90 or > 90 || lon is < -180 or > 180)
            return null;

        DateTimeOffset? timestamp = null;
        if (location.TryGetValue("Timestamp", out var ts))
        {
            var items = PortalVariant.StructItems(ts);
            if (items is { Length: 2 } && PortalVariant.ToDouble(items[0]) is double secs)
            {
                var micros = PortalVariant.ToDouble(items[1]) ?? 0;
                try
                {
                    timestamp = DateTimeOffset.FromUnixTimeSeconds((long)secs).AddTicks((long)(micros * 10));
                }
                catch (ArgumentOutOfRangeException)
                {
                    timestamp = null;
                }
            }
        }

        return new PortalLocationFix(lat.Value, lon.Value, Known("Altitude"), Known("Accuracy"), Known("Speed"), Known("Heading"), timestamp);
    }

    /// <summary>The Location portal accuracy level for a MAUI accuracy.</summary>
    public static uint AccuracyFor(Microsoft.Maui.Devices.Sensors.GeolocationAccuracy accuracy) => accuracy switch
    {
        Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.Lowest => 1,  // country
        Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.Low => 2,     // city
        Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.Medium => 4,  // street
        Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.High => 5,    // exact
        Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.Best => 5,
        _ => 4,
    };

    internal static string Describe(PortalLocationFix fix)
        => string.Create(CultureInfo.InvariantCulture, $"{fix.Latitude:F4},{fix.Longitude:F4}");
}
