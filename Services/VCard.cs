// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Maui.ApplicationModel.Communication;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// The vCard (RFC 2426 / 6350) fields MAUI's Contact carries: UID, FN, N, TEL, EMAIL. Lines are
/// unfolded, grouped names ("item1.EMAIL") and parameters (quoted values included) are skipped
/// over, and text values unescaped.
/// </summary>
internal static class VCard
{
    /// <summary>The (upper-case name, value) properties of a vCard, in order.</summary>
    internal static IEnumerable<(string Name, string Value)> Properties(string vcard)
    {
        foreach (var line in Unfold(vcard))
        {
            var colon = ValueSeparator(line);
            if (colon <= 0)
                continue;
            var head = line[..colon];
            var semicolon = head.IndexOf(';');
            var name = semicolon >= 0 ? head[..semicolon] : head;
            var dot = name.LastIndexOf('.');
            if (dot >= 0)
                name = name[(dot + 1)..];
            yield return (name.Trim().ToUpperInvariant(), line[(colon + 1)..]);
        }
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

        foreach (var (name, value) in Properties(vcard))
        {
            switch (name)
            {
                case "UID":
                    id ??= Unescape(value);
                    break;
                case "FN":
                    displayName ??= Unescape(value);
                    break;
                case "N":
                    var parts = SplitComponents(value);
                    family = Part(parts, 0);
                    given = Part(parts, 1);
                    middle = Part(parts, 2);
                    prefix = Part(parts, 3);
                    suffix = Part(parts, 4);
                    break;
                case "TEL":
                    var number = Unescape(value).Trim();
                    if (number.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
                        number = number[4..];
                    if (number.Length > 0)
                        phones.Add(new ContactPhone(number));
                    break;
                case "EMAIL":
                    var address = Unescape(value).Trim();
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

    /// <summary>The logical lines: a line starting with a space or tab continues the previous one.</summary>
    internal static IEnumerable<string> Unfold(string text)
    {
        var current = new StringBuilder();
        var any = false;
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t') && any)
            {
                current.Append(raw, 1, raw.Length - 1);
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
}
