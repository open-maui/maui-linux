// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Backend-agnostic outgoing-drag payload. A single drag may carry any
/// combination of plain text, a file list (surfaced as an RFC 2483
/// <c>text/uri-list</c>), and one image (raw bytes plus its MIME, typically
/// <c>image/png</c>). Both the Wayland (wl_data_source) and X11 (XDND) source
/// paths consume this through <see cref="MimeTypes"/> (what to advertise) and
/// <see cref="GetBytes(string)"/> (what to write when a target requests a
/// specific MIME), so the per-MIME resolution lives in one place.
/// </summary>
public sealed class DragPayload
{
    // Text MIME variants offered for a text payload, preferred-first — the
    // same set the clipboard negotiates.
    internal static readonly string[] TextMimeTypes =
    {
        "text/plain;charset=utf-8",
        "text/plain",
        "UTF8_STRING",
        "STRING",
        "TEXT",
    };

    internal const string UriListMime = "text/uri-list";

    /// <summary>Plain-text payload, or null.</summary>
    public string? Text { get; set; }

    /// <summary>Absolute file paths, surfaced to targets as a uri-list; or null.</summary>
    public string[]? FilePaths { get; set; }

    /// <summary>Raw image bytes (already encoded in <see cref="ImageMime"/>), or null.</summary>
    public byte[]? ImageBytes { get; set; }

    /// <summary>MIME of <see cref="ImageBytes"/>, e.g. <c>image/png</c>.</summary>
    public string? ImageMime { get; set; }

    public bool HasText => !string.IsNullOrEmpty(Text);
    public bool HasFiles => FilePaths is { Length: > 0 };
    public bool HasImage => ImageBytes is { Length: > 0 } && !string.IsNullOrEmpty(ImageMime);

    public bool IsEmpty => !HasText && !HasFiles && !HasImage;

    public static DragPayload FromText(string text) => new() { Text = text };

    public static DragPayload FromFiles(params string[] paths) => new() { FilePaths = paths };

    public static DragPayload FromImage(byte[] bytes, string mime) =>
        new() { ImageBytes = bytes, ImageMime = mime };

    /// <summary>
    /// MIME types to advertise, richest-first: image, then uri-list (files),
    /// then the text variants. Only MIMEs actually backed by data are listed,
    /// so a target that accepts any of them can always be served.
    /// </summary>
    public IReadOnlyList<string> MimeTypes
    {
        get
        {
            var list = new List<string>();
            if (HasImage) list.Add(ImageMime!);
            if (HasFiles) list.Add(UriListMime);
            if (HasText) list.AddRange(TextMimeTypes);
            return list;
        }
    }

    /// <summary>
    /// Resolve the bytes to write for a requested MIME, or null when this
    /// payload does not offer it. Text variants all map to the UTF-8 text;
    /// the uri-list MIME maps to the encoded file list; the image MIME maps to
    /// the raw image bytes.
    /// </summary>
    public byte[]? GetBytes(string mime)
    {
        if (string.IsNullOrEmpty(mime)) return null;

        if (HasImage && string.Equals(mime, ImageMime, StringComparison.OrdinalIgnoreCase))
            return ImageBytes;

        if (HasFiles && string.Equals(mime, UriListMime, StringComparison.OrdinalIgnoreCase))
            return Encoding.UTF8.GetBytes(BuildUriList()!);

        if (HasText)
        {
            foreach (var t in TextMimeTypes)
            {
                if (string.Equals(mime, t, StringComparison.OrdinalIgnoreCase))
                    return Encoding.UTF8.GetBytes(Text!);
            }
        }

        return null;
    }

    /// <summary>
    /// Encode <see cref="FilePaths"/> as an RFC 2483 text/uri-list: one
    /// <c>file://</c> URI per line, CRLF-terminated, path segments
    /// percent-encoded. Returns null when there are no files.
    /// </summary>
    public string? BuildUriList()
    {
        if (!HasFiles) return null;

        var sb = new StringBuilder();
        foreach (var path in FilePaths!)
        {
            if (string.IsNullOrEmpty(path)) continue;
            sb.Append(PathToFileUri(path));
            sb.Append("\r\n"); // RFC 2483: lines are CRLF-terminated
        }
        return sb.ToString();
    }

    // Build a file:// URI with each path segment percent-encoded (spaces,
    // non-ASCII, reserved chars), leading slash preserved, so file managers
    // and toolkits parse it back to the original path.
    internal static string PathToFileUri(string path)
    {
        var segments = path.Split('/');
        var encoded = new StringBuilder("file://");
        for (int i = 0; i < segments.Length; i++)
        {
            if (i > 0) encoded.Append('/');
            encoded.Append(Uri.EscapeDataString(segments[i]));
        }
        return encoded.ToString();
    }
}
