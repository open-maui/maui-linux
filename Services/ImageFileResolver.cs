// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using SkiaSharp;
using Svg.Skia;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Resolves app image files the way MAUI apps reference them. On the other
/// platforms MAUI's build converts every SVG image to a same-named PNG, so
/// apps write <c>Icon="save_24dp.png"</c> for <c>save_24dp.svg</c>; on Linux the
/// SVG is copied as is, so a <c>.png</c> reference falls back to the SVG. Used
/// by buttons, Shell flyout icons and anything else that takes a file name;
/// <see cref="LinuxFileImageSourceService"/> (Image, ImageButton) resolves files with it.
/// </summary>
public static class ImageFileResolver
{
    /// <summary>The file name of a <see cref="FileImageSource"/>, or null for other sources.</summary>
    /// <remarks>Never use <c>ImageSource.ToString()</c> for this: it returns "File: name.png".</remarks>
    public static string? FileOf(ImageSource? source) => (source as FileImageSource)?.File;

    /// <summary>The path of an existing file for <paramref name="file"/>, or null.</summary>
    public static string? ResolvePath(string? file)
    {
        if (string.IsNullOrWhiteSpace(file)) return null;
        foreach (var candidate in Candidates(file))
            if (File.Exists(candidate))
                return candidate;
        return null;
    }

    internal static IEnumerable<string> Candidates(string file)
    {
        var baseDir = AppContext.BaseDirectory;
        IEnumerable<string> Around(string name)
        {
            yield return name;
            if (!Path.IsPathRooted(name))
            {
                yield return Path.Combine(baseDir, name);
                yield return Path.Combine(baseDir, "Resources", "Images", name);
                yield return Path.Combine(baseDir, "Resources", name);
            }
        }

        foreach (var c in Around(file)) yield return c;
        if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            foreach (var c in Around(Path.ChangeExtension(file, ".svg"))) yield return c;
    }

    /// <summary>
    /// Loads <paramref name="file"/> as a bitmap: raster files decoded as they
    /// are, SVGs rendered so their longer side is <paramref name="svgSizePx"/>
    /// pixels. Null when the file cannot be found or read.
    /// </summary>
    public static SKBitmap? LoadBitmap(string? file, int svgSizePx = 48)
    {
        var path = ResolvePath(file);
        if (path == null) return null;
        try
        {
            if (!path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                return SKBitmap.Decode(path);

            using var svg = new SKSvg();
            svg.Load(path);
            if (svg.Picture == null) return null;
            var cull = svg.Picture.CullRect;
            float longest = Math.Max(cull.Width, cull.Height);
            if (longest <= 0) return null;
            float scale = svgSizePx / longest;
            int w = Math.Max(1, (int)Math.Ceiling(cull.Width * scale));
            int h = Math.Max(1, (int)Math.Ceiling(cull.Height * scale));
            var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(scale);
            canvas.Translate(-cull.Left, -cull.Top);
            canvas.DrawPicture(svg.Picture);
            return bitmap;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("ImageFileResolver", $"Loading image {path} failed", ex);
            return null;
        }
    }
}
