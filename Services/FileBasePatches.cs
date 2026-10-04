// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// <see cref="FileBase"/> (FileResult, ReadOnlyFile, MediaFile, ShareFile's base) on Linux. The
/// portable Essentials build throws NotImplementedInReferenceAssemblyException from its
/// platform members, so on Linux every <c>FileResult.OpenReadAsync()</c> threw, reading
/// <c>ContentType</c> of a file with an extension threw, and the copy constructor
/// (<c>new ReadOnlyFile(fileResult)</c>, <c>new OpenFileRequest(title, file)</c>) threw.
/// Each is replaced with what the Windows build does with a path:
/// <list type="bullet">
/// <item>OpenReadAsync opens the file at FullPath for reading (a new stream per call).</item>
/// <item>ContentType comes from the extension (<see cref="MimeTypes"/>), application/octet-stream
/// when unknown (MAUI's default).</item>
/// <item>The copy constructor copies the path, name and type; there is no native file object.</item>
/// </list>
/// </summary>
internal static class FileBasePatches
{
    internal static void Install(Harmony harmony)
    {
        var type = typeof(FileBase);
        Patch(harmony, type.GetMethod("PlatformOpenReadAsync", BindingFlags.Instance | BindingFlags.NonPublic), nameof(PlatformOpenReadAsync_Prefix));
        Patch(harmony, type.GetMethod("PlatformGetContentType", BindingFlags.Static | BindingFlags.NonPublic), nameof(PlatformGetContentType_Prefix));
        Patch(harmony, type.GetMethod("PlatformInit", BindingFlags.Instance | BindingFlags.NonPublic), nameof(Skip_Prefix));
        Patch(harmony, type.GetMethod("Init", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(FileBase) }, null), nameof(Skip_Prefix));
    }

    private static void Patch(Harmony harmony, MethodInfo? original, string prefixName)
    {
        if (original == null)
        {
            DiagnosticLog.Error("FileBasePatches", $"FileBase member for {prefixName} not found");
            return;
        }
        harmony.Patch(original, new HarmonyMethod(typeof(FileBasePatches).GetMethod(prefixName, BindingFlags.Static | BindingFlags.NonPublic)!));
    }

    private static bool PlatformOpenReadAsync_Prefix(FileBase __instance, ref Task<Stream> __result)
    {
        __result = OpenRead(__instance.FullPath);
        return false;
    }

    /// <summary>A read stream over <paramref name="fullPath"/>; FileNotFoundException when it does not exist, as on Windows.</summary>
    internal static Task<Stream> OpenRead(string fullPath)
    {
        try
        {
            Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            return Task.FromResult(stream);
        }
        catch (Exception ex)
        {
            return Task.FromException<Stream>(ex);
        }
    }

    private static bool PlatformGetContentType_Prefix(string extension, ref string? __result)
    {
        __result = MimeTypes.FromExtension(extension);
        return false;
    }

    private static bool Skip_Prefix() => false;
}

/// <summary>
/// MIME type of a file extension: the table MAUI's Windows build uses for FileResult.ContentType
/// first (so the same file gets the same type on both), then the desktop's shared-mime-info
/// glob database (/usr/share/mime/globs2, the source GIO and xdg-mime read) for the rest.
/// </summary>
internal static class MimeTypes
{
    private static readonly Dictionary<string, string> s_known = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".svg"] = "image/svg+xml",
        [".webp"] = "image/webp",
        [".tiff"] = "image/tiff",
        [".tif"] = "image/tiff",
        [".ico"] = "image/x-icon",
        // Audio
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".flac"] = "audio/flac",
        [".aac"] = "audio/aac",
        [".ogg"] = "audio/ogg",
        [".wma"] = "audio/x-ms-wma",
        // Video
        [".mp4"] = "video/mp4",
        [".avi"] = "video/x-msvideo",
        [".mov"] = "video/quicktime",
        [".wmv"] = "video/x-ms-wmv",
        [".webm"] = "video/webm",
        [".mkv"] = "video/x-matroska",
        [".flv"] = "video/x-flv",
        // Documents
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".txt"] = "text/plain",
        [".rtf"] = "application/rtf",
        // Web
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".css"] = "text/css",
        [".js"] = "application/javascript",
        [".json"] = "application/json",
        [".xml"] = "text/xml",
        // Archives
        [".zip"] = "application/zip",
        [".rar"] = "application/x-rar-compressed",
        [".7z"] = "application/x-7z-compressed",
        [".tar"] = "application/x-tar",
        [".tar.gz"] = "application/gzip",
        [".gz"] = "application/gzip",
    };

    private static readonly Lazy<Dictionary<string, string>> s_globs = new(LoadGlobs);

    /// <summary>The glob files read, in order of precedence; replaceable by tests.</summary>
    internal static IReadOnlyList<string>? GlobFilesOverride { get; set; }

    /// <summary>
    /// The MIME type for <paramref name="extension"/> (".png", "png", " .JPG ", ".tar.gz"), or
    /// null when neither table knows it (FileBase then reports application/octet-stream).
    /// </summary>
    internal static string? FromExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;
        extension = extension.Trim().ToLowerInvariant();
        if (!extension.StartsWith('.'))
            extension = "." + extension;

        if (s_known.TryGetValue(extension, out var known))
            return known;
        // ".tar.gz" style: the longest known suffix.
        var dot = extension.IndexOf('.', 1);
        while (dot > 0)
        {
            if (s_known.TryGetValue(extension[dot..], out known))
                return known;
            dot = extension.IndexOf('.', dot + 1);
        }

        var globs = GlobFilesOverride != null ? LoadGlobs(GlobFilesOverride) : s_globs.Value;
        return globs.TryGetValue(extension, out var fromGlobs) ? fromGlobs : null;
    }

    private static Dictionary<string, string> LoadGlobs() => LoadGlobs(DefaultGlobFiles());

    private static IEnumerable<string> DefaultGlobFiles()
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrEmpty(dataHome))
            dataHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        yield return Path.Combine(dataHome, "mime", "globs2");

        var dataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
        if (string.IsNullOrEmpty(dataDirs))
            dataDirs = "/usr/local/share:/usr/share";
        foreach (var dir in dataDirs.Split(':', StringSplitOptions.RemoveEmptyEntries))
            yield return Path.Combine(dir, "mime", "globs2");
    }

    /// <summary>
    /// Simple "*.ext" globs from globs2 files ("weight:type:glob[:flags]"); the first file and,
    /// within it, the highest weight wins.
    /// </summary>
    private static Dictionary<string, string> LoadGlobs(IEnumerable<string> files)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            string[] lines;
            try
            {
                if (!File.Exists(file))
                    continue;
                lines = File.ReadAllLines(file);
            }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            var weights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines)
            {
                if (line.Length == 0 || line[0] == '#')
                    continue;
                var parts = line.Split(':');
                if (parts.Length < 3 || !int.TryParse(parts[0], out var weight))
                    continue;
                var glob = parts[2];
                if (!glob.StartsWith("*.", StringComparison.Ordinal) || glob.IndexOfAny(new[] { '*', '?', '[' }, 1) >= 0)
                    continue;
                var ext = glob[1..].ToLowerInvariant();
                if (result.ContainsKey(ext) && !weights.ContainsKey(ext))
                    continue; // set by a file of higher precedence
                if (weights.TryGetValue(ext, out var existing) && existing >= weight)
                    continue;
                weights[ext] = weight;
                result[ext] = parts[1];
            }
        }
        return result;
    }
}
