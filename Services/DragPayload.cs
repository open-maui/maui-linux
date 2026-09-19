// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Result of a lazy image resolution: the encoded bytes plus the MIME sniffed
/// from their header (see <see cref="DragPayload.SniffImageMime"/>).
/// </summary>
public sealed record ResolvedImage(byte[] Bytes, string Mime);

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

    /// <summary>
    /// In-flight lazy image resolution, or null. Started at drag time (e.g. a
    /// StreamImageSource being read on a background task) so the drag can
    /// START synchronously — Wayland needs the still-held press serial and X11
    /// grabs the in-progress press. Transfer paths await it with a bounded
    /// timeout (<see cref="PendingImageTimeout"/>); the resolved MIME is
    /// sniffed from the bytes and gates which advertised image MIME is served.
    /// A faulted/cancelled/timed-out task honest-fails only the image MIMEs —
    /// text/file MIMEs in the same payload keep working.
    /// </summary>
    public Task<ResolvedImage?>? PendingImage { get; set; }

    /// <summary>
    /// Bound applied when a transfer path waits for <see cref="PendingImage"/>.
    /// </summary>
    public static readonly TimeSpan PendingImageTimeout = TimeSpan.FromSeconds(5);

    // Advertised optimistically for a pending image whose format isn't known
    // yet; the sniffed format decides which one is actually served.
    internal static readonly string[] OptimisticImageMimes = { "image/png", "image/jpeg" };

    public bool HasText => !string.IsNullOrEmpty(Text);
    public bool HasFiles => FilePaths is { Length: > 0 };
    public bool HasImage => ImageBytes is { Length: > 0 } && !string.IsNullOrEmpty(ImageMime);
    public bool HasPendingImage => PendingImage != null;

    public bool IsEmpty => !HasText && !HasFiles && !HasImage && !HasPendingImage;

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
            if (HasImage)
            {
                list.Add(ImageMime!);
            }
            else if (HasPendingImage)
            {
                // Format not known until the lazy read completes — advertise
                // the declared MIME when the caller supplied one, otherwise
                // the common candidates; the sniffed format gates serving.
                if (!string.IsNullOrEmpty(ImageMime)) list.Add(ImageMime!);
                else list.AddRange(OptimisticImageMimes);
            }
            if (HasFiles) list.Add(UriListMime);
            if (HasText) list.AddRange(TextMimeTypes);
            return list;
        }
    }

    /// <summary>
    /// Resolve the bytes to write for a requested MIME, or null when this
    /// payload does not offer it (or cannot serve it yet). Text variants all
    /// map to the UTF-8 text; the uri-list MIME maps to the encoded file list;
    /// image MIMEs map to the raw bytes — including a pending image whose task
    /// has ALREADY completed (this method never blocks; use
    /// <see cref="GetBytesAsync"/> where waiting is allowed).
    /// </summary>
    public byte[]? GetBytes(string mime)
    {
        if (string.IsNullOrEmpty(mime)) return null;

        if (HasImage && string.Equals(mime, ImageMime, StringComparison.OrdinalIgnoreCase))
            return ImageBytes;

        if (HasPendingImage && IsAdvertisedImageMime(mime)
            && PendingImage is { IsCompletedSuccessfully: true } completed)
            return MatchResolved(completed.Result, mime);

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
    /// Async-aware resolution: identical to <see cref="GetBytes"/> for
    /// synchronously available MIMEs, and additionally awaits a pending image
    /// up to <paramref name="timeout"/>. Returns null on unsupported MIME,
    /// timeout, fault, or a sniffed-format mismatch with the requested MIME.
    /// Never throws.
    /// </summary>
    public async Task<byte[]?> GetBytesAsync(string mime, TimeSpan timeout)
    {
        var sync = GetBytes(mime);
        if (sync != null) return sync;

        var pending = PendingImage;
        if (pending == null || !IsAdvertisedImageMime(mime)) return null;

        try
        {
            var winner = await Task.WhenAny(pending, Task.Delay(timeout)).ConfigureAwait(false);
            if (winner != pending) return null; // timed out — honest-fail this MIME
            var resolved = await pending.ConfigureAwait(false); // rethrows faults → caught below
            return MatchResolved(resolved, mime);
        }
        catch
        {
            return null;
        }
    }

    // Does the requested MIME correspond to one of the image MIMEs this
    // payload advertised for its pending image?
    private bool IsAdvertisedImageMime(string mime)
    {
        if (!string.IsNullOrEmpty(ImageMime))
            return string.Equals(mime, ImageMime, StringComparison.OrdinalIgnoreCase);
        foreach (var m in OptimisticImageMimes)
        {
            if (string.Equals(mime, m, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Serve resolved bytes only when the sniffed format matches the requested
    // MIME — advertising was optimistic, serving must be honest.
    private static byte[]? MatchResolved(ResolvedImage? resolved, string mime)
    {
        if (resolved == null || resolved.Bytes.Length == 0) return null;
        return string.Equals(mime, resolved.Mime, StringComparison.OrdinalIgnoreCase)
            ? resolved.Bytes
            : null;
    }

    /// <summary>
    /// Sniff an image MIME from the leading bytes (PNG, JPEG, GIF, BMP, WebP
    /// magic numbers); null when unrecognized or too short.
    /// </summary>
    public static string? SniffImageMime(byte[]? bytes)
    {
        if (bytes == null) return null;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return "image/png";

        // JPEG: FF D8 FF
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        // GIF: "GIF8"
        if (bytes.Length >= 4
            && bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'8')
            return "image/gif";

        // WebP: "RIFF" .... "WEBP" (check before BMP — both are ASCII-tagged)
        if (bytes.Length >= 12
            && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
            && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
            return "image/webp";

        // BMP: "BM"
        if (bytes.Length >= 2 && bytes[0] == (byte)'B' && bytes[1] == (byte)'M')
            return "image/bmp";

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
