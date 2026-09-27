using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SkiaSharp;
using Svg.Skia;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Generates application icons from MAUI icon metadata.
/// Uses SVG overlay support via Svg.Skia package.
/// </summary>
public static class MauiIconGenerator
{
    private const int DefaultIconSize = 256;

    public static string? GenerateIcon(string metaFilePath)
    {
        if (!File.Exists(metaFilePath))
        {
            DiagnosticLog.Error("MauiIconGenerator", "Metadata file not found: " + metaFilePath);
            return null;
        }

        try
        {
            string path = Path.GetDirectoryName(metaFilePath) ?? "";
            var metadata = ParseMetadata(File.ReadAllText(metaFilePath));

            // Format 2 (current targets): the MauiIcon file is the background
            // layer (for a single-file icon, the whole icon), BackgroundColor
            // fills behind it (transparent when unset), the optional
            // ForegroundFile is drawn at Scale and tinted with TintColor.
            // Older metas carry only Color, which filled the whole square.
            bool format2 = metadata.ContainsKey("Format");
            string? bgPath = metadata.TryGetValue("Background", out var bgName) && bgName.Length > 0
                ? Path.Combine(path, bgName)
                : null;
            string fgPath = Path.Combine(path, "appicon_fg.svg");
            // The app folder is read-only inside an AppImage (and for system
            // installs), so an icon is only reused from there; a fresh one is
            // written to the user's cache and reused while it is newer than
            // its inputs.
            var inputsTime = LatestWriteTime(metaFilePath, fgPath, bgPath ?? metaFilePath);
            string besideApp = Path.Combine(path, "appicon.png");
            if (File.Exists(besideApp) && File.GetLastWriteTimeUtc(besideApp) >= inputsTime)
                return besideApp;
            string outputPath = CachedIconPath();
            if (File.Exists(outputPath) && File.GetLastWriteTimeUtc(outputPath) >= inputsTime)
                return outputPath;
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            int size = metadata.TryGetValue("Size", out var sizeStr) && int.TryParse(sizeStr, out var sizeVal)
                ? sizeVal
                : DefaultIconSize;

            SKColor background = format2
                ? (metadata.TryGetValue("BackgroundColor", out var bgColor) && bgColor.Length > 0 ? ParseColor(bgColor) : SKColors.Transparent)
                : (metadata.TryGetValue("Color", out var colorStr) ? ParseColor(colorStr) : SKColors.Purple);
            SKColor? tint = format2 && metadata.TryGetValue("TintColor", out var tintStr) && tintStr.Length > 0
                ? ParseColor(tintStr)
                : null;

            float scale = metadata.TryGetValue("Scale", out var scaleStr)
                && float.TryParse(scaleStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var scaleVal)
                ? scaleVal
                : 0.65f;

            DiagnosticLog.Debug("MauiIconGenerator", $"Generating {size}x{size} icon (background {bgPath ?? "none"}, color {background}, scale {scale})");

            using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
            var canvas = surface.Canvas;
            canvas.Clear(background);

            if (bgPath != null && File.Exists(bgPath))
                DrawLayer(canvas, bgPath, size, 1f, null);
            if (File.Exists(fgPath))
                DrawLayer(canvas, fgPath, size, scale, tint);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using (var fileStream = File.Create(outputPath))
                data.SaveTo(fileStream);

            DiagnosticLog.Debug("MauiIconGenerator", "Generated: " + outputPath);
            return outputPath;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("MauiIconGenerator", "Generating the app icon failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Draws an SVG or raster layer centred on the icon, its longer side
    /// <paramref name="scale"/> of the icon size, optionally tinted.
    /// </summary>
    private static void DrawLayer(SKCanvas canvas, string file, int size, float scale, SKColor? tint)
    {
        using var paint = tint is { } t
            ? new SKPaint { ColorFilter = SKColorFilter.CreateBlendMode(t, SKBlendMode.SrcIn) }
            : null;
        if (file.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            using var svg = new SKSvg();
            if (svg.Load(file) == null || svg.Picture == null)
                return;
            var cull = svg.Picture.CullRect;
            float s = size * scale / Math.Max(cull.Width, cull.Height);
            canvas.Save();
            canvas.Translate((size - cull.Width * s) / 2f, (size - cull.Height * s) / 2f);
            canvas.Scale(s);
            canvas.Translate(-cull.Left, -cull.Top);
            canvas.DrawPicture(svg.Picture, paint);
            canvas.Restore();
            return;
        }

        using var bitmap = SKBitmap.Decode(file);
        if (bitmap == null)
            return;
        float k = size * scale / Math.Max(bitmap.Width, bitmap.Height);
        float w = bitmap.Width * k, h = bitmap.Height * k;
        var dest = SKRect.Create((size - w) / 2f, (size - h) / 2f, w, h);
        using var image = SKImage.FromBitmap(bitmap);
        canvas.DrawImage(image, dest, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
    }

    private static DateTime LatestWriteTime(params string[] files)
    {
        var latest = DateTime.MinValue;
        foreach (var f in files)
            if (File.Exists(f) && File.GetLastWriteTimeUtc(f) > latest)
                latest = File.GetLastWriteTimeUtc(f);
        return latest;
    }

    /// <summary>$XDG_CACHE_HOME/openmaui/&lt;app&gt;/appicon.png (~/.cache when unset).</summary>
    internal static string CachedIconPath()
    {
        var cache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (string.IsNullOrEmpty(cache))
            cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
        var app = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "MauiApp");
        return Path.Combine(cache, "openmaui", app, "appicon.png");
    }

    private static Dictionary<string, string> ParseMetadata(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                result[parts[0].Trim()] = parts[1].Trim();
            }
        }
        return result;
    }

    private static SKColor ParseColor(string colorStr)
    {
        if (string.IsNullOrEmpty(colorStr))
        {
            return SKColors.Purple;
        }

        colorStr = colorStr.Trim();

        if (colorStr.StartsWith("#"))
        {
            string hex = colorStr.Substring(1);

            // Expand 3-digit hex to 6-digit
            if (hex.Length == 3)
            {
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
            }

            if (hex.Length == 6 && uint.TryParse(hex, NumberStyles.HexNumber, null, out var rgb))
            {
                return new SKColor(
                    (byte)((rgb >> 16) & 0xFF),
                    (byte)((rgb >> 8) & 0xFF),
                    (byte)(rgb & 0xFF));
            }

            if (hex.Length == 8 && uint.TryParse(hex, NumberStyles.HexNumber, null, out var argb))
            {
                return new SKColor(
                    (byte)((argb >> 16) & 0xFF),
                    (byte)((argb >> 8) & 0xFF),
                    (byte)(argb & 0xFF),
                    (byte)((argb >> 24) & 0xFF));
            }
        }

        return colorStr.ToLowerInvariant() switch
        {
            "red" => SKColors.Red,
            "green" => SKColors.Green,
            "blue" => SKColors.Blue,
            "purple" => SKColors.Purple,
            "orange" => SKColors.Orange,
            "white" => SKColors.White,
            "black" => SKColors.Black,
            _ => SKColors.Purple,
        };
    }
}
