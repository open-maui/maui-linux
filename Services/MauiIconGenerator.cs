using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Generates application icons from MAUI icon metadata.
/// Creates PNG icons suitable for use as window icons on Linux.
/// Note: SVG overlay support requires Svg.Skia package (optional).
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

            string outputPath = Path.Combine(path, "appicon.png");

            int size = metadata.TryGetValue("Size", out var sizeStr) && int.TryParse(sizeStr, out var sizeVal)
                ? sizeVal
                : DefaultIconSize;

            SKColor color = metadata.TryGetValue("Color", out var colorStr)
                ? ParseColor(colorStr)
                : SKColors.Purple;

            Console.WriteLine($"[MauiIconGenerator] Generating {size}x{size} icon");
            Console.WriteLine($"[MauiIconGenerator]   Color: {color}");

            using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
            var canvas = surface.Canvas;

            // Draw background with rounded corners
            canvas.Clear(SKColors.Transparent);
            float cornerRadius = size * 0.2f;
            using var paint = new SKPaint { Color = color, IsAntialias = true };
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(0, 0, size, size), cornerRadius), paint);

            // Try to load PNG foreground as fallback (appicon_fg.png)
            string fgPngPath = Path.Combine(path, "appicon_fg.png");
            if (File.Exists(fgPngPath))
            {
                try
                {
                    using var fgBitmap = SKBitmap.Decode(fgPngPath);
                    if (fgBitmap != null)
                    {
                        float scale = size * 0.65f / Math.Max(fgBitmap.Width, fgBitmap.Height);
                        float fgWidth = fgBitmap.Width * scale;
                        float fgHeight = fgBitmap.Height * scale;
                        float offsetX = (size - fgWidth) / 2f;
                        float offsetY = (size - fgHeight) / 2f;

                        var dstRect = new SKRect(offsetX, offsetY, offsetX + fgWidth, offsetY + fgHeight);
                        canvas.DrawBitmap(fgBitmap, dstRect);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[MauiIconGenerator] Failed to load foreground PNG: " + ex.Message);
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
