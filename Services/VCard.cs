// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Maui.ApplicationModel.Communication;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>One content line of a vCard: its name (group dropped), parameters and raw value.</summary>
internal readonly record struct VCardProperty(string Name, IReadOnlyList<KeyValuePair<string, string>> Parameters, string RawValue)
{
    /// <summary>The first value of parameter <paramref name="key"/> (upper case), or null.</summary>
    public string? Parameter(string key)
    {
        foreach (var p in Parameters)
        {
            if (p.Key == key)
                return p.Value;
        }
        return null;
    }

    /// <summary>The TYPE values, upper case ("TYPE=work,voice", "TYPE=CELL;TYPE=PREF", vCard 2.1's bare "CELL").</summary>
    public IEnumerable<string> Types
        => Parameters.Where(p => p.Key == "TYPE")
            .SelectMany(p => p.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(t => t.ToUpperInvariant());

    /// <summary>
    /// The value as text: quoted-printable or base64 decoded, in its CHARSET (UTF-8 when it
    /// names none). Backslash escapes are left for the caller (a structured value splits first).
    /// </summary>
    public string DecodedValue => VCard.Decode(this);
}

/// <summary>
/// The vCard (2.1, RFC 2426 3.0, RFC 6350 4.0) fields MAUI's Contact carries: UID, FN, N, TEL,
/// EMAIL. Lines are unfolded (and quoted-printable soft breaks joined), grouped names
/// ("item1.EMAIL") and parameters (quoted values included, vCard 2.1's bare ones too) are
/// parsed, quoted-printable and base64 values decoded in their CHARSET, and text values
/// unescaped. MAUI's ContactPhone and ContactEmail have no type, so TEL and EMAIL types are
/// read (<see cref="VCardProperty.Types"/>) but not carried.
/// </summary>
internal static class VCard
{
    /// <summary>The (upper-case name, raw value) properties of a vCard, in order.</summary>
    internal static IEnumerable<(string Name, string Value)> Properties(string vcard)
        => Parse(vcard).Select(p => (p.Name, p.RawValue));

    /// <summary>The properties of a vCard, in order.</summary>
    internal static IEnumerable<VCardProperty> Parse(string vcard)
    {
        foreach (var line in Unfold(vcard))
        {
            if (TryParseLine(line, out var property))
                yield return property;
        }
    }

    internal static bool TryParseLine(string line, out VCardProperty property)
    {
        property = default;
        var colon = ValueSeparator(line);
        if (colon <= 0)
            return false;
        var head = SplitParameters(line[..colon]);
        var name = head[0].Trim();
        var dot = name.LastIndexOf('.');
        if (dot >= 0)
            name = name[(dot + 1)..];
        if (name.Length == 0)
            return false;

        var parameters = new List<KeyValuePair<string, string>>(head.Count - 1);
        for (int i = 1; i < head.Count; i++)
        {
            var raw = head[i].Trim();
            if (raw.Length == 0)
                continue;
            var eq = raw.IndexOf('=');
            string key, value;
            if (eq < 0)
            {
                // vCard 2.1: "TEL;CELL;PREF:" and "FN;QUOTED-PRINTABLE:" name the value alone.
                value = raw;
                key = raw.ToUpperInvariant() is "QUOTED-PRINTABLE" or "BASE64" or "B" or "8BIT" or "7BIT" ? "ENCODING" : "TYPE";
            }
            else
            {
                key = raw[..eq].Trim().ToUpperInvariant();
                value = Unquote(raw[(eq + 1)..].Trim());
            }
            parameters.Add(new KeyValuePair<string, string>(key, value));
        }
        property = new VCardProperty(name.ToUpperInvariant(), parameters, line[(colon + 1)..]);
        return true;
    }

    /// <summary>A MAUI contact from <paramref name="vcard"/>, or null when it is not a vCard.</summary>
    internal static Contact? ToContact(string vcard)
    {
        if (string.IsNullOrWhiteSpace(vcard) || vcard.IndexOf("BEGIN:VCARD", StringComparison.OrdinalIgnoreCase) < 0)
            return null;

        string? id = null, displayName = null;
        string? family = null, given = null, middle = null, prefix = null, suffix = null;
        var phones = new List<ContactPhone>();
        var emails = new List<ContactEmail>();

        foreach (var property in OwnProperties(vcard))
        {
            switch (property.Name)
            {
                case "UID":
                    id ??= NullIfEmpty(Unescape(property.DecodedValue).Trim());
                    break;
                case "FN":
                    displayName ??= NullIfEmpty(Unescape(property.DecodedValue).Trim());
                    break;
                case "N":
                    var parts = SplitComponents(property.DecodedValue);
                    family = Part(parts, 0);
                    given = Part(parts, 1);
                    middle = Part(parts, 2);
                    prefix = Part(parts, 3);
                    suffix = Part(parts, 4);
                    break;
                case "TEL":
                    var number = Unescape(property.DecodedValue).Trim();
                    if (number.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
                        number = number[4..];
                    if (number.Length > 0)
                        phones.Add(new ContactPhone(number));
                    break;
                case "EMAIL":
                    var address = Unescape(property.DecodedValue).Trim();
                    if (address.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                        address = address[7..];
                    if (address.Length > 0)
                        emails.Add(new ContactEmail(address));
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            var composed = string.Join(" ", new[] { prefix, given, middle, family, suffix }.Where(p => !string.IsNullOrWhiteSpace(p)));
            displayName = composed.Length > 0 ? composed : null;
        }

        return new Contact(id!, prefix!, given!, middle!, family!, suffix!, phones, emails, displayName!);
    }

    /// <summary>The UID of <paramref name="vcard"/>, or null when it has none.</summary>
    internal static string? Uid(string vcard)
    {
        foreach (var property in OwnProperties(vcard))
        {
            if (property.Name == "UID")
                return NullIfEmpty(Unescape(property.DecodedValue).Trim());
        }
        return null;
    }

    /// <summary>
    /// <paramref name="vcard"/> with a UID line of <paramref name="fallbackUid"/> when it has no
    /// UID of its own (a card read from a file is then known by its file name).
    /// </summary>
    internal static string WithUid(string vcard, string fallbackUid)
    {
        if (Uid(vcard) != null)
            return vcard;
        var end = vcard.LastIndexOf("END:VCARD", StringComparison.OrdinalIgnoreCase);
        if (end < 0)
            return vcard;
        var escaped = fallbackUid.Replace("\\", "\\\\").Replace(",", "\\,").Replace(";", "\\;").Replace("\n", "\\n").Replace("\r", string.Empty);
        return vcard[..end] + "UID:" + escaped + "\r\n" + vcard[end..];
    }

    /// <summary>The properties of the outer card (those of a vCard 2.1 AGENT nested in it skipped).</summary>
    private static IEnumerable<VCardProperty> OwnProperties(string vcard)
    {
        var depth = 0;
        foreach (var property in Parse(vcard))
        {
            var isMarker = property.Name is "BEGIN" or "END" && property.RawValue.Trim().Equals("VCARD", StringComparison.OrdinalIgnoreCase);
            if (isMarker)
            {
                depth += property.Name == "BEGIN" ? 1 : -1;
                continue;
            }
            if (depth <= 1)
                yield return property;
        }
    }

    /// <summary>The vCards in <paramref name="text"/> (a .vcf file can hold several), each as its own text.</summary>
    internal static IReadOnlyList<string> Split(string text)
    {
        var cards = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Trim();
            if (line.Equals("BEGIN:VCARD", StringComparison.OrdinalIgnoreCase))
            {
                depth++;
                current.Append(raw).Append("\r\n");
                continue;
            }
            if (depth == 0)
                continue;
            current.Append(raw).Append("\r\n");
            if (line.Equals("END:VCARD", StringComparison.OrdinalIgnoreCase) && --depth == 0)
            {
                cards.Add(current.ToString());
                current.Clear();
            }
        }
        return cards;
    }

    /// <summary>
    /// The logical lines: a line starting with a space or tab continues the previous one, and a
    /// quoted-printable value ending in a soft line break ("=") continues on the next line.
    /// </summary>
    internal static IEnumerable<string> Unfold(string text)
    {
        if (text.Length > 0 && text[0] == '\uFEFF')
            text = text[1..];
        var current = new StringBuilder();
        var any = false;
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (any && raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t'))
            {
                current.Append(raw, 1, raw.Length - 1);
                continue;
            }
            if (any && current.Length > 0 && current[^1] == '=' && IsQuotedPrintable(current))
            {
                current.Length--;
                current.Append(raw);
                continue;
            }
            if (any)
                yield return current.ToString();
            current.Clear();
            current.Append(raw);
            any = true;
        }
        if (any && current.Length > 0)
            yield return current.ToString();
    }

    private static bool IsQuotedPrintable(StringBuilder line)
    {
        var text = line.ToString();
        var colon = ValueSeparator(text);
        return colon > 0 && text.AsSpan(0, colon).Contains("QUOTED-PRINTABLE", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The colon that ends a property's name and parameters (a colon in a quoted parameter value does not).</summary>
    private static int ValueSeparator(string line)
    {
        var quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
                quoted = !quoted;
            else if (line[i] == ':' && !quoted)
                return i;
        }
        return -1;
    }

    /// <summary>A property's head split at the ';' outside quotes: its name, then each parameter.</summary>
    private static List<string> SplitParameters(string head)
    {
        var parts = new List<string>();
        var quoted = false;
        var start = 0;
        for (int i = 0; i < head.Length; i++)
        {
            if (head[i] == '"')
            {
                quoted = !quoted;
            }
            else if (head[i] == ';' && !quoted)
            {
                parts.Add(head[start..i]);
                start = i + 1;
            }
        }
        parts.Add(head[start..]);
        return parts;
    }

    private static string Unquote(string value)
        => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

    /// <summary>The ';'-separated components of a structured value, a "\;" kept in its component.</summary>
    internal static List<string> SplitComponents(string value)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '\\' && i + 1 < value.Length)
            {
                current.Append(c).Append(value[++i]);
            }
            else if (c == ';')
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        parts.Add(current.ToString());
        return parts;
    }

    private static string? Part(List<string> parts, int index)
    {
        if (index >= parts.Count)
            return null;
        // Several values (",") in one component: the name parts MAUI has are single strings.
        var value = Unescape(parts[index]).Trim();
        return value.Length == 0 ? null : value;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    /// <summary>A text value with its escapes (\\, \;, \,, \n) resolved.</summary>
    internal static string Unescape(string value)
    {
        if (value.IndexOf('\\') < 0)
            return value;
        var sb = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '\\' && i + 1 < value.Length)
            {
                var next = value[++i];
                sb.Append(next is 'n' or 'N' ? '\n' : next);
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>A property's value with its ENCODING and CHARSET applied.</summary>
    internal static string Decode(VCardProperty property)
    {
        var raw = property.RawValue;
        var encoding = property.Parameter("ENCODING")?.ToUpperInvariant();
        var charset = property.Parameter("CHARSET");
        switch (encoding)
        {
            case "QUOTED-PRINTABLE":
                return CharsetOrUtf8(charset).GetString(DecodeQuotedPrintable(raw));
            case "B" or "BASE64":
                try
                {
                    return CharsetOrUtf8(charset).GetString(Convert.FromBase64String(raw.Trim()));
                }
                catch (FormatException)
                {
                    return raw;
                }
        }

        // A file that was not UTF-8 is read byte for byte (Latin-1); its CHARSET names the real one.
        if (charset != null && Charset(charset) is { } named && named.CodePage != Encoding.UTF8.CodePage && raw.All(c => c <= '\u00FF'))
            return named.GetString(Encoding.Latin1.GetBytes(raw));
        return raw;
    }

    /// <summary>The bytes of a quoted-printable value ("=XX" escapes, soft breaks already joined).</summary>
    internal static byte[] DecodeQuotedPrintable(string value)
    {
        var bytes = new List<byte>(value.Length);
        Span<byte> utf8 = stackalloc byte[4];
        for (int i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '=' && i + 2 < value.Length && IsHex(value[i + 1]) && IsHex(value[i + 2]))
            {
                bytes.Add((byte)Convert.ToInt32(value.Substring(i + 1, 2), 16));
                i += 2;
            }
            else if (c < 0x80)
            {
                bytes.Add((byte)c);
            }
            else
            {
                var n = Encoding.UTF8.GetBytes(value.AsSpan(i, char.IsHighSurrogate(c) && i + 1 < value.Length ? 2 : 1), utf8);
                for (int k = 0; k < n; k++)
                    bytes.Add(utf8[k]);
                if (char.IsHighSurrogate(c) && i + 1 < value.Length)
                    i++;
            }
        }
        return bytes.ToArray();
    }

    private static bool IsHex(char c) => c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';

    private static Encoding CharsetOrUtf8(string? charset)
        => charset != null && Charset(charset) is { } named ? named : Encoding.UTF8;

    /// <summary>The encoding a CHARSET names (the code-page ones too), or null when it is unknown.</summary>
    internal static Encoding? Charset(string name)
    {
        try
        {
            return Encoding.GetEncoding(name);
        }
        catch (ArgumentException)
        {
        }
        try
        {
            return CodePagesEncodingProvider.Instance.GetEncoding(name);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// The text of a vCard file: by its byte-order mark, else UTF-8, else (not valid UTF-8, an
    /// old vCard 2.1 export) byte for byte, so a CHARSET parameter can still decode it.
    /// </summary>
    internal static string ReadFileText(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }
}
