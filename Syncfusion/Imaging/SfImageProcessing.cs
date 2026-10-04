// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// The bitmap work of Syncfusion's image editor, done with Skia where the Windows build uses
/// WIC (<c>BitmapEncoder</c>, <c>BitmapTransform</c>) and Win2D effects: flips, quarter turns,
/// crops (rectangle, circle and ellipse), the annotation overlay merge, resizing, encoding
/// (PNG, JPEG, BMP) and the eight adjustment effects with the parameters the Windows handler
/// gives Win2D. Every method returns a new bitmap and leaves its input alone.
/// </summary>
internal static class SfImageProcessing
{
    internal static SKBitmap Copy(SKBitmap source)
    {
        var copy = new SKBitmap(new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(copy);
        canvas.Clear(SKColors.Transparent);
        DrawAt(canvas, source, 0, 0);
        return copy;
    }

    /// <summary>A mirror image: left-right when <paramref name="horizontal"/>, else top-bottom.</summary>
    internal static SKBitmap Flip(SKBitmap source, bool horizontal)
    {
        var result = New(source.Width, source.Height);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        if (horizontal)
        {
            canvas.Translate(source.Width, 0);
            canvas.Scale(-1, 1);
        }
        else
        {
            canvas.Translate(0, source.Height);
            canvas.Scale(1, -1);
        }
        DrawAt(canvas, source, 0, 0);
        return result;
    }

    /// <summary>A quarter turn: clockwise for a positive angle, else counter-clockwise (BitmapRotation.Clockwise90/270Degrees).</summary>
    internal static SKBitmap Rotate(SKBitmap source, double angle)
    {
        var result = New(source.Height, source.Width);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        if (angle > 0)
        {
            canvas.Translate(source.Height, 0);
            canvas.RotateDegrees(90);
        }
        else
        {
            canvas.Translate(0, source.Width);
            canvas.RotateDegrees(-90);
        }
        DrawAt(canvas, source, 0, 0);
        return result;
    }

    /// <summary>
    /// ImageEditorHelper.GetCroppedStream: <paramref name="cropRect"/> is in the image's rendered
    /// size; a circle or ellipse crop leaves the corners transparent.
    /// </summary>
    internal static SKBitmap Crop(SKBitmap source, Rect cropRect, Size renderedSize, string cropType)
    {
        double sx = renderedSize.Width > 0 ? source.Width / renderedSize.Width : 1;
        double sy = renderedSize.Height > 0 ? source.Height / renderedSize.Height : 1;
        int width = Math.Max(1, (int)(cropRect.Width * sx));
        int height = Math.Max(1, (int)(cropRect.Height * sy));
        int x = (int)(cropRect.X * sx);
        int y = (int)(cropRect.Y * sy);
        var result = New(width, height);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        if (cropType is "Circle" or "Ellipse")
        {
            // Circle: a circle as wide as the crop; ellipse: the crop's own axes.
            float rx = width / 2f;
            float ry = cropType == "Circle" ? rx : height / 2f;
            var oval = new SKRect(width / 2f - rx, height / 2f - ry, width / 2f + rx, height / 2f + ry);
            canvas.ClipRoundRect(new SKRoundRect(oval, rx, ry), antialias: true);
        }
        using (var image = SKImage.FromBitmap(source))
            canvas.DrawImage(image, new SKRect(x, y, x + width, y + height), new SKRect(0, 0, width, height), SKSamplingOptions.Default, null);
        return result;
    }

    /// <summary>
    /// ImageEditorHelper.GetMergedStream: the annotation layer (drawn at its own size) scaled
    /// over the image; with no image, the annotation layer alone.
    /// </summary>
    internal static SKBitmap? Merge(SKBitmap? image, SkiaView? annotations)
    {
        var bounds = annotations?.Bounds ?? default;
        bool hasLayer = annotations != null && bounds.Width > 0 && bounds.Height > 0;
        if (image == null)
        {
            if (!hasLayer)
                return null;
            var layer = New((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height));
            using var layerCanvas = new SKCanvas(layer);
            layerCanvas.Clear(SKColors.Transparent);
            DrawView(layerCanvas, annotations!, 1, 1);
            return layer;
        }
        var result = Copy(image);
        if (!hasLayer)
            return result;
        using var canvas = new SKCanvas(result);
        DrawView(canvas, annotations!, (float)(image.Width / bounds.Width), (float)(image.Height / bounds.Height));
        return result;
    }

    /// <summary>Draws a laid-out view (window coordinates) with its top-left at the canvas origin.</summary>
    internal static void DrawView(SKCanvas canvas, SkiaView view, float scaleX, float scaleY)
    {
        canvas.Save();
        canvas.Scale(scaleX, scaleY);
        canvas.Translate((float)-view.Bounds.X, (float)-view.Bounds.Y);
        view.Draw(canvas);
        canvas.Restore();
    }

    internal static SKBitmap Resize(SKBitmap source, int width, int height)
    {
        var result = New(Math.Max(1, width), Math.Max(1, height));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        using var image = SKImage.FromBitmap(source);
        using var paint = new SKPaint();
        canvas.DrawImage(image, new SKRect(0, 0, result.Width, result.Height), new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
        return result;
    }

    /// <summary>
    /// Encodes for an ImageFileType or ImageFileFormat name: "Png", "Jpeg", "Jpg" or "Bmp"
    /// (anything else is PNG). JPEG has no alpha, so it is flattened onto white.
    /// </summary>
    internal static byte[] Encode(SKBitmap bitmap, string? format)
    {
        switch (format)
        {
            case "Jpeg":
            case "Jpg":
            {
                using var flat = New(bitmap.Width, bitmap.Height);
                using (var canvas = new SKCanvas(flat))
                {
                    canvas.Clear(SKColors.White);
                    DrawAt(canvas, bitmap, 0, 0);
                }
                using var data = flat.Encode(SKEncodedImageFormat.Jpeg, 100);
                return data.ToArray();
            }
            case "Bmp":
                return EncodeBmp(bitmap);
            default:
            {
                using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                return data.ToArray();
            }
        }
    }

    internal static SKBitmap? Decode(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return null;
        using var decoded = SKBitmap.Decode(bytes);
        return decoded == null ? null : Copy(decoded);
    }

    /// <summary>A 32-bit BMP (BGRA, bottom-up), as WIC's BMP encoder writes.</summary>
    private static byte[] EncodeBmp(SKBitmap bitmap)
    {
        int w = bitmap.Width, h = bitmap.Height;
        int imageSize = w * h * 4;
        using var ms = new MemoryStream(54 + imageSize);
        using var bw = new BinaryWriter(ms);
        bw.Write((byte)'B'); bw.Write((byte)'M');
        bw.Write(54 + imageSize); bw.Write(0); bw.Write(54);
        bw.Write(40); bw.Write(w); bw.Write(h); bw.Write((short)1); bw.Write((short)32);
        bw.Write(0); bw.Write(imageSize); bw.Write(3780); bw.Write(3780); bw.Write(0); bw.Write(0);
        for (int y = h - 1; y >= 0; y--)
        {
            for (int x = 0; x < w; x++)
            {
                var c = bitmap.GetPixel(x, y); // unpremultiplied
                bw.Write(c.Blue); bw.Write(c.Green); bw.Write(c.Red); bw.Write(c.Alpha);
            }
        }
        bw.Flush();
        return ms.ToArray();
    }

    private static void DrawAt(SKCanvas canvas, SKBitmap bitmap, float x, float y, SKPaint? paint = null)
    {
        using var image = SKImage.FromBitmap(bitmap);
        canvas.DrawImage(image, x, y, SKSamplingOptions.Default, paint);
    }

    private static SKBitmap New(int width, int height) =>
        new(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));

    // ---- Effects --------------------------------------------------------------------------------

    /// <summary>
    /// Applies the effects in order (the handler keeps opacity first, as Win2D's composite does
    /// once opacity joins: BringOpacityToFront). Values are the editor's: -1..1, or 0..1 for
    /// blur, sharpen and opacity.
    /// </summary>
    internal static SKBitmap ApplyEffects(SKBitmap source, IReadOnlyList<KeyValuePair<string, float>> effects)
    {
        var current = Copy(source);
        foreach (var (effect, value) in effects)
        {
            var next = ApplyEffect(current, effect, value);
            if (!ReferenceEquals(next, current))
            {
                current.Dispose();
                current = next;
            }
        }
        return current;
    }

    private static SKBitmap ApplyEffect(SKBitmap source, string effect, float value)
    {
        switch (effect)
        {
            case "Brightness":
            {
                // Win2D BrightnessEffect: BlackPoint (max(1 - n, 0), 0), WhitePoint (min(2 - n, 1), 1).
                float n = (Math.Clamp(value * 100f, -99.9f, 99.9f) + 100f) / 100f;
                float black = Math.Max(1f - n, 0f), white = Math.Min(2f - n, 1f);
                float k = 1f / Math.Max(white - black, 1e-4f);
                return WithColorFilter(source, Linear(k, -black * k));
            }
            case "Contrast":
            {
                // Win2D ContrastEffect, -1..1 about mid-grey.
                float c = Math.Clamp(value, -1f, 0.99f);
                float k = (1f + c) / (1f - c);
                return WithColorFilter(source, Linear(k, 0.5f - 0.5f * k));
            }
            case "Hue":
                // Win2D HueRotationEffect, Angle = value * pi.
                return WithColorFilter(source, HueRotation(Math.Clamp(value, -1f, 1f) * MathF.PI));
            case "Saturation":
                // Win2D SaturationEffect, Saturation = (value * 100 + 100) / 100: 0 grey, 1 unchanged, 2 double.
                return WithColorFilter(source, Saturation((Math.Clamp(value * 100f, -100f, 100f) + 100f) / 100f));
            case "Opacity":
            {
                float a = Math.Clamp(value, 0f, 1f);
                return WithColorFilter(source, new float[]
                {
                    1, 0, 0, 0, 0,
                    0, 1, 0, 0, 0,
                    0, 0, 1, 0, 0,
                    0, 0, 0, a, 0,
                });
            }
            case "Exposure":
            {
                // Win2D ExposureEffect: colours scaled by 2^exposure.
                float k = MathF.Pow(2f, Math.Clamp(value, -1f, 1f));
                return WithColorFilter(source, Linear(k, 0));
            }
            case "Blur":
            {
                // Win2D GaussianBlurEffect, BlurAmount (standard deviation) = value * 10.
                float sigma = Math.Clamp(value * 10f, 0f, 10f);
                if (sigma <= 0f)
                    return source;
                using var filter = SKImageFilter.CreateBlur(sigma, sigma, SKShaderTileMode.Decal);
                return WithImageFilter(source, filter);
            }
            case "Sharpen":
            {
                // Win2D SharpenEffect, Amount = value * 6 (0..10 scale): an unsharp kernel.
                float amount = Math.Clamp(value * 6f, 0f, 6f) / 4f;
                if (amount <= 0f)
                    return source;
                float n = -amount / 8f;
                var kernel = new[] { n, n, n, n, 1f + amount, n, n, n, n };
                using var filter = SKImageFilter.CreateMatrixConvolution(new SKSizeI(3, 3), kernel, 1f, 0f,
                    new SKPointI(1, 1), SKShaderTileMode.Clamp, convolveAlpha: false);
                return WithImageFilter(source, filter);
            }
            default:
                return source;
        }
    }

    private static float[] Linear(float k, float offset) => new[]
    {
        k, 0, 0, 0, offset,
        0, k, 0, 0, offset,
        0, 0, k, 0, offset,
        0, 0, 0, 1, 0,
    };

    private static float[] Saturation(float s)
    {
        const float r = 0.213f, g = 0.715f, b = 0.072f;
        return new[]
        {
            r + (1 - r) * s, g - g * s, b - b * s, 0, 0,
            r - r * s, g + (1 - g) * s, b - b * s, 0, 0,
            r - r * s, g - g * s, b + (1 - b) * s, 0, 0,
            0, 0, 0, 1, 0,
        };
    }

    private static float[] HueRotation(float radians)
    {
        float c = MathF.Cos(radians), s = MathF.Sin(radians);
        return new[]
        {
            0.213f + c * 0.787f - s * 0.213f, 0.715f - c * 0.715f - s * 0.715f, 0.072f - c * 0.072f + s * 0.928f, 0, 0,
            0.213f - c * 0.213f + s * 0.143f, 0.715f + c * 0.285f + s * 0.140f, 0.072f - c * 0.072f - s * 0.283f, 0, 0,
            0.213f - c * 0.213f - s * 0.787f, 0.715f - c * 0.715f + s * 0.715f, 0.072f + c * 0.928f + s * 0.072f, 0, 0,
            0, 0, 0, 1, 0,
        };
    }

    private static SKBitmap WithColorFilter(SKBitmap source, float[] matrix)
    {
        using var filter = SKColorFilter.CreateColorMatrix(matrix);
        var result = New(source.Width, source.Height);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { ColorFilter = filter };
        DrawAt(canvas, source, 0, 0, paint);
        return result;
    }

    private static SKBitmap WithImageFilter(SKBitmap source, SKImageFilter filter)
    {
        var result = New(source.Width, source.Height);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { ImageFilter = filter };
        DrawAt(canvas, source, 0, 0, paint);
        return result;
    }
}
