// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Reflection;
using SkiaSharp;
using Svg.Skia;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Decoded-image cache shared by every image view (and by the built-in image-source
/// services that fill it). Key is the file path, embedded resource or URI.
/// </summary>
internal static class LinuxImageCache
{
    private const int MaxCacheSize = 50;
    private const long MaxCacheMemoryBytes = 100 * 1024 * 1024;

    private sealed class Entry
    {
        public required SKBitmap Bitmap { get; init; }
        public IReadOnlyList<ImageFrame>? Frames { get; init; }
        public DateTime LastAccessed { get; set; }
        public long MemorySize { get; init; }
    }

    private static readonly ConcurrentDictionary<string, Entry> s_cache = new();
    private static readonly Lock s_lock = new();

    // Bitmaps that have been put in the cache: shared by every view showing that source, so
    // no one disposes them. Dropping them from the cache drops the reference, and SkiaSharp's
    // finalizer frees the pixels once no view holds them. Disposing on eviction (or in a view
    // whose source had just been evicted) freed pixels another view was still drawing or
    // copying (SKImage.FromBitmap), a SIGSEGV in memcpy.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SKBitmap, object> s_shared = new();

    public static bool IsShared(SKBitmap? bitmap) => bitmap != null && s_shared.TryGetValue(bitmap, out _);

    /// <summary>A result for the cached image under <paramref name="key"/>, or null.</summary>
    public static LinuxImageSourceServiceResult? TryGet(string key)
    {
        if (!s_cache.TryGetValue(key, out var entry))
            return null;
        entry.LastAccessed = DateTime.UtcNow;
        return Shared(entry.Bitmap, entry.Frames);
    }

    /// <summary>Caches a decoded image and returns it as a shared result.</summary>
    public static LinuxImageSourceServiceResult Add(string key, DecodedImage image)
    {
        s_shared.AddOrUpdate(image.Bitmap, true);
        long memory = image.Bitmap.ByteCount;
        if (image.Frames != null)
        {
            memory = 0;
            foreach (var frame in image.Frames)
            {
                s_shared.AddOrUpdate(frame.Bitmap, true);
                memory += frame.Bitmap.ByteCount;
            }
        }

        s_cache[key] = new Entry
        {
            Bitmap = image.Bitmap,
            Frames = image.Frames,
            LastAccessed = DateTime.UtcNow,
            MemorySize = memory,
        };
        Trim();
        return Shared(image.Bitmap, image.Frames);
    }

    // No dispose action: the cache and other views share these bitmaps.
    private static LinuxImageSourceServiceResult Shared(SKBitmap bitmap, IReadOnlyList<ImageFrame>? frames) =>
        new(bitmap, false, null) { Frames = frames };

    public static void Clear()
    {
        lock (s_lock)
        {
            // Views may still be showing these bitmaps; the finalizer frees them after.
            s_cache.Clear();
        }
    }

    private static void Trim()
    {
        lock (s_lock)
        {
            if (s_cache.Count <= MaxCacheSize)
                return;

            long totalMemory = 0;
            foreach (var cached in s_cache.Values)
                totalMemory += cached.MemorySize;

            if (totalMemory < MaxCacheMemoryBytes && s_cache.Count <= MaxCacheSize)
                return;

            var sorted = s_cache.ToArray();
            Array.Sort(sorted, (a, b) => a.Value.LastAccessed.CompareTo(b.Value.LastAccessed));

            int removeCount = Math.Max(1, s_cache.Count - MaxCacheSize + 10);
            for (int i = 0; i < removeCount && i < sorted.Length; i++)
            {
                // Dropped, not disposed: views may still be showing it.
                s_cache.TryRemove(sorted[i].Key, out _);
            }
        }
    }
}

/// <summary>A decoded raster image: its first (or only) frame, and every frame when animated.</summary>
internal sealed record DecodedImage(SKBitmap Bitmap, IReadOnlyList<ImageFrame>? Frames);

/// <summary>
/// Decoding and SVG rendering shared by the built-in image-source services and the
/// image views' own re-render of an SVG at a new size.
/// </summary>
internal static class LinuxImageLoader
{
    private const float MinimumSvgSize = 24f;

    /// <summary>
    /// Decodes a raster image (every frame of an animated GIF), or null when the
    /// data is not an image Skia can decode.
    /// </summary>
    public static DecodedImage? Decode(Stream stream)
    {
        if (!stream.CanSeek)
        {
            var copy = new MemoryStream();
            stream.CopyTo(copy);
            copy.Position = 0;
            stream = copy;
        }

        using var codec = SKCodec.Create(stream);
        if (codec == null)
        {
            stream.Position = 0;
            var fallback = SKBitmap.Decode(stream);
            return fallback == null ? null : new DecodedImage(fallback, null);
        }

        int frameCount = codec.FrameCount;
        if (frameCount > 1)
        {
            var info = codec.Info;
            var frames = new List<ImageFrame>(frameCount);
            for (int i = 0; i < frameCount; i++)
            {
                var frameInfo = codec.FrameInfo[i];
                var bitmap = new SKBitmap(info.Width, info.Height);
                codec.GetPixels(bitmap.Info, bitmap.GetPixels(), new SKCodecOptions(i));
                frames.Add(new ImageFrame(bitmap, frameInfo.Duration > 0 ? frameInfo.Duration : 100));
            }
            return new DecodedImage(frames[0].Bitmap, frames);
        }

        // Premultiplied alpha, so transparent PNGs render correctly.
        var decoded = SKBitmap.Decode(codec, codec.Info.WithAlphaType(SKAlphaType.Premul));
        return decoded == null ? null : new DecodedImage(decoded, null);
    }

    /// <summary>Renders an SVG file; see <see cref="RenderSvg(SKSvg, double, double, float)"/>.</summary>
    public static SKBitmap? RenderSvgFile(string path, double targetWidth, double targetHeight, float scale)
    {
        using var svg = new SKSvg();
        svg.Load(path);
        return RenderSvg(svg, targetWidth, targetHeight, scale);
    }

    /// <summary>Renders an SVG stream; see <see cref="RenderSvg(SKSvg, double, double, float)"/>.</summary>
    public static SKBitmap? RenderSvgStream(Stream stream, double targetWidth, double targetHeight, float scale)
    {
        using var svg = new SKSvg();
        svg.Load(stream);
        return RenderSvg(svg, targetWidth, targetHeight, scale);
    }

    /// <summary>
    /// Renders an SVG fitted (aspect kept) into the target logical size, at
    /// <paramref name="scale"/> pixels per unit. A target dimension of 0 takes the
    /// SVG's own, with a 24-unit floor so small icons stay legible.
    /// </summary>
    private static SKBitmap? RenderSvg(SKSvg svg, double targetWidth, double targetHeight, float scale)
    {
        var picture = svg.Picture;
        if (picture == null)
            return null;

        SKRect cull = picture.CullRect;
        if (cull.Width <= 0 || cull.Height <= 0)
            return null;

        float requestedWidth = targetWidth > 0 ? (float)targetWidth : Math.Max(cull.Width, MinimumSvgSize);
        float requestedHeight = targetHeight > 0 ? (float)targetHeight : Math.Max(cull.Height, MinimumSvgSize);
        float fit = Math.Min(requestedWidth / cull.Width, requestedHeight / cull.Height) * (scale > 0 ? scale : 1f);

        int width = Math.Max(1, (int)(cull.Width * fit));
        int height = Math.Max(1, (int)(cull.Height * fit));

        var bitmap = new SKBitmap(width, height, false);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(fit);
        // Negative viewBox origins (Material icons use 0 -960 960 960).
        canvas.Translate(-cull.Left, -cull.Top);
        canvas.DrawPicture(picture);
        return bitmap;
    }

    /// <summary>
    /// Opens an image embedded in a loaded assembly (MAUI's convention: the app references
    /// <c>name.png</c>; on Linux the resource may be the SVG it was converted from).
    /// </summary>
    /// <returns>The stream and whether it is an SVG, or a null stream.</returns>
    public static (Stream? Stream, bool IsSvg) OpenEmbeddedResource(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        string requestedExt = Path.GetExtension(fileName);

        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .ToList();
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null && !assemblies.Contains(entryAssembly))
            assemblies.Insert(0, entryAssembly);

        foreach (var assembly in assemblies)
        {
            try
            {
                var resourceNames = assembly.GetManifestResourceNames();

                // A '.' boundary, so bmc_logo.png does not match logo.png.
                var stream = OpenResource(assembly, resourceNames, fileName);
                if (stream != null)
                {
                    DiagnosticLog.Debug("ImageSources", $"Loaded embedded resource {fileName} from {assembly.GetName().Name}");
                    return (stream, requestedExt.Equals(".svg", StringComparison.OrdinalIgnoreCase));
                }

                if (requestedExt.Equals(".png", StringComparison.OrdinalIgnoreCase))
                {
                    stream = OpenResource(assembly, resourceNames, Path.GetFileNameWithoutExtension(fileName) + ".svg");
                    if (stream != null)
                    {
                        DiagnosticLog.Debug("ImageSources", $"Loaded SVG as PNG substitute for {fileName}");
                        return (stream, true);
                    }
                }
            }
            catch
            {
                // Assemblies that cannot be inspected are skipped.
            }
        }

        return (null, false);
    }

    private static Stream? OpenResource(Assembly assembly, string[] resourceNames, string fileName)
    {
        string dotted = "." + fileName;
        foreach (var name in resourceNames)
        {
            if (name.EndsWith(dotted, StringComparison.OrdinalIgnoreCase) || name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            {
                var stream = assembly.GetManifestResourceStream(name);
                if (stream != null)
                    return stream;
            }
        }
        return null;
    }
}
