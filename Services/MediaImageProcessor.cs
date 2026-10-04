// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Media;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// The photo processing <see cref="MediaPickerOptions"/> asks for (<c>RotateImage</c>,
/// <c>MaximumWidth</c>, <c>MaximumHeight</c>, <c>CompressionQuality</c>), done with SkiaSharp the
/// way MAUI's Windows build does it with its ImageProcessor (src/Essentials/src/MediaPicker,
/// ImageProcessor.shared.cs and .windows.cs). The portable Essentials build has no image
/// processor (IsProcessingNeeded is always false there), so the options were ignored on Linux.
/// <list type="bullet">
/// <item>Rotation: the EXIF orientation is applied to the pixels (normal, 90, 180, 270 degrees and
/// the mirrored ones), re-encoded as PNG for a .png, else JPEG.</item>
/// <item>Resizing keeps the aspect ratio and only scales down.</item>
/// <item>Format: PNG at quality 95 and up, or 90 and up for a PNG; JPEG otherwise, at the quality.</item>
/// <item>Metadata is not carried over (the Windows build drops it too).</item>
/// </list>
/// Unlike Windows, which hands back an in-memory result whose FullPath is a bare file name, the
/// processed image is written to a file in the temporary directory, so FullPath opens it.
/// </summary>
internal static class MediaImageProcessor
{
    /// <summary>Whether resizing or compression is asked for (MAUI's ImageProcessor.IsProcessingNeeded on Windows).</summary>
    internal static bool IsProcessingNeeded(int? maxWidth, int? maxHeight, int qualityPercent)
        => maxWidth.HasValue || maxHeight.HasValue || qualityPercent < 100;

    internal static bool IsRotationNeeded(MediaPickerOptions? options) => options?.RotateImage ?? false;

    /// <summary>New dimensions within the bounds, aspect ratio kept, never scaled up (CalculateResizedDimensions).</summary>
    internal static (float Width, float Height) CalculateResizedDimensions(float width, float height, int? maxWidth, int? maxHeight)
    {
        if (!maxWidth.HasValue && !maxHeight.HasValue)
            return (width, height);
        var scale = Math.Min((maxWidth ?? float.MaxValue) / width, (maxHeight ?? float.MaxValue) / height);
        return scale >= 1.0f ? (width, height) : (width * scale, height * scale);
    }

    /// <summary>PNG output: quality 95 and up, or 90 and up when the original was a PNG (ShouldUsePngFormat).</summary>
    internal static bool ShouldUsePngFormat(string? originalFileName, int qualityPercent)
    {
        var originalWasPng = !string.IsNullOrEmpty(originalFileName)
            && Path.GetExtension(originalFileName).Equals(".png", StringComparison.OrdinalIgnoreCase);
        return qualityPercent >= 95 || (qualityPercent >= 90 && originalWasPng);
    }

    /// <summary>".png" or ".jpg" from the leading bytes, null for anything else (DetectImageFormat).</summary>
    internal static string? DetectImageFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return ".png";
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ".jpg";
        return null;
    }

    /// <summary>
    /// The image at <paramref name="path"/> with its EXIF orientation applied, written to a new
    /// file; the original path when it needs no rotation; null when it cannot be decoded.
    /// </summary>
    internal static string? Rotate(string path)
    {
        using var decoded = Decode(path, applyOrientation: true, out var rotated);
        if (decoded == null)
            return null;
        if (!rotated)
            return path;
        var png = Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase);
        // WIC's default JPEG quality (Windows' RotateImageAsync re-encodes with it).
        return Write(decoded, png ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, 90);
    }

    /// <summary>
    /// The image at <paramref name="path"/> rotated (when asked), resized within the bounds and
    /// re-encoded at the quality, written to a new file; null when it cannot be decoded.
    /// </summary>
    internal static string? Process(string path, int? maxWidth, int? maxHeight, int qualityPercent, bool rotateImage)
    {
        using var decoded = Decode(path, rotateImage, out _);
        if (decoded == null)
            return null;

        var bitmap = decoded;
        SKBitmap? resized = null;
        try
        {
            var (width, height) = CalculateResizedDimensions(decoded.Width, decoded.Height, maxWidth, maxHeight);
            var w = Math.Max(1, (int)Math.Round(width));
            var h = Math.Max(1, (int)Math.Round(height));
            if (w != decoded.Width || h != decoded.Height)
            {
                resized = decoded.Resize(new SKImageInfo(w, h, decoded.ColorType, decoded.AlphaType), new SKSamplingOptions(SKCubicResampler.Mitchell));
                if (resized != null)
                    bitmap = resized;
            }

            var quality = Math.Clamp(qualityPercent, 0, 100);
            var format = ShouldUsePngFormat(Path.GetFileName(path), quality) ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
            return Write(bitmap, format, quality);
        }
        finally
        {
            resized?.Dispose();
        }
    }

    /// <summary>Decodes <paramref name="path"/>, turning it upright when <paramref name="applyOrientation"/>.</summary>
    internal static SKBitmap? Decode(string path, bool applyOrientation, out bool rotated)
    {
        rotated = false;
        using var codec = SKCodec.Create(path);
        if (codec == null)
            return null;
        var info = codec.Info.WithColorType(SKImageInfo.PlatformColorType)
            .WithAlphaType(codec.Info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            return null;
        }

        var origin = codec.EncodedOrigin;
        if (!applyOrientation || origin == SKEncodedOrigin.TopLeft)
            return bitmap;

        rotated = true;
        var oriented = ApplyOrigin(bitmap, origin);
        bitmap.Dispose();
        return oriented;
    }

    /// <summary>The bitmap turned from its EXIF <paramref name="origin"/> to upright.</summary>
    internal static SKBitmap ApplyOrigin(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = swap ? bitmap.Height : bitmap.Width;
        var height = swap ? bitmap.Width : bitmap.Height;
        var result = new SKBitmap(new SKImageInfo(width, height, bitmap.ColorType, bitmap.AlphaType));
        using var canvas = new SKCanvas(result);
        canvas.SetMatrix(OriginMatrix(origin, bitmap.Width, bitmap.Height));
        canvas.DrawBitmap(bitmap, 0, 0);
        canvas.Flush();
        return result;
    }

    /// <summary>The transform drawing a stored image of <paramref name="w"/> x <paramref name="h"/> upright (EXIF orientations 1 to 8).</summary>
    internal static SKMatrix OriginMatrix(SKEncodedOrigin origin, int w, int h) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),        // 2: mirror horizontally
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),    // 3: rotate 180
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),      // 4: mirror vertically
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),          // 5: transpose
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),        // 6: rotate 90 clockwise
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),     // 7: transverse
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),      // 8: rotate 90 counter-clockwise
        _ => SKMatrix.Identity,
    };

    private static string? Write(SKBitmap bitmap, SKEncodedImageFormat format, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        if (data == null)
            return null;
        var extension = format == SKEncodedImageFormat.Png ? ".png" : ".jpg";
        var output = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        using (var stream = File.Create(output))
            data.SaveTo(stream);
        return output;
    }
}
