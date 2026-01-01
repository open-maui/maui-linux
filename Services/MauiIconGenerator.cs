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
            Console.WriteLine("[MauiIconGenerator] Metadata file not found: " + metaFilePath);
            return null;
        }

        try
        {
            string path = Path.GetDirectoryName(metaFilePath) ?? "";
            var metadata = ParseMetadata(File.ReadAllText(metaFilePath));

            // bg and fg paths (bg not currently used, but available for future)
            Path.Combine(path, "appicon_bg.svg");
            string fgPath = Path.Combine(path, "appicon_fg.svg");
            string outputPath = Path.Combine(path, "appicon.png");

            // Parse size from metadata or use default
            int size = metadata.TryGetValue("Size", out var sizeStr) && int.TryParse(sizeStr, out var sizeVal)
                ? sizeVal
                : DefaultIconSize;

            // Parse color from metadata or use default purple
            SKColor color = metadata.TryGetValue("Color", out var colorStr)
                ? ParseColor(colorStr)
                : SKColors.Purple;

            // Parse scale from metadata or use default 0.65
            float scale = metadata.TryGetValue("Scale", out var scaleStr) && float.TryParse(scaleStr, out var scaleVal)
                ? scaleVal
                : 0.65f;

            Console.WriteLine($"[MauiIconGenerator] Generating {size}x{size} icon");
            Console.WriteLine($"[MauiIconGenerator]   Color: {color}");
            Console.WriteLine($"[MauiIconGenerator]   Scale: {scale}");

            using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
            var canvas = surface.Canvas;

            // Fill background with color
            canvas.Clear(color);

            // Load and draw SVG foreground if it exists
            if (File.Exists(fgPath))
            {
                using var svg = new SKSvg();
                if (svg.Load(fgPath) != null && svg.Picture != null)
                {
                    var cullRect = svg.Picture.CullRect;
                    float svgScale = size * scale / Math.Max(cullRect.Width, cullRect.Height);
                    float offsetX = (size - cullRect.Width * svgScale) / 2f;
                    float offsetY = (size - cullRect.Height * svgScale) / 2f;

                    canvas.Save();
                    canvas.Translate(offsetX, offsetY);
                    canvas.Scale(svgScale);
                    canvas.DrawPicture(svg.Picture);
                    canvas.Restore();
                }
            }

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var fileStream = File.OpenWrite(outputPath);
            data.SaveTo(fileStream);

            Console.WriteLine("[MauiIconGenerator] Generated: " + outputPath);
            return outputPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[MauiIconGenerator] Error: " + ex.Message);
            return null;
        }
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
