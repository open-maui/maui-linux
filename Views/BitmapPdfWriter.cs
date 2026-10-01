// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Views;

/// <summary>
/// Writes a page image as a PDF of A4 pages: scaled to the page width and cut into page-height
/// slices, top to bottom. For saving a web page where the engine cannot print (WPE).
/// </summary>
internal static class BitmapPdfWriter
{
    private const float A4Width = 595f;  // points
    private const float A4Height = 842f;

    /// <summary>Writes <paramref name="bitmap"/> (rendered at <paramref name="deviceScale"/> pixels per CSS pixel) to <paramref name="path"/>.</summary>
    internal static bool Write(SKBitmap bitmap, float deviceScale, string path)
    {
        if (bitmap.Width <= 0 || bitmap.Height <= 0)
            return false;
        float pixelsPerPoint = bitmap.Width / A4Width;
        float sliceHeight = A4Height * pixelsPerPoint; // bitmap pixels per page

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var stream = File.Create(path);
        using var document = SKDocument.CreatePdf(stream);
        using var image = SKImage.FromBitmap(bitmap);
        for (float top = 0; top < bitmap.Height; top += sliceHeight)
        {
            float height = Math.Min(sliceHeight, bitmap.Height - top);
            var canvas = document.BeginPage(A4Width, A4Height);
            canvas.Clear(SKColors.White);
            var source = new SKRect(0, top, bitmap.Width, top + height);
            var destination = new SKRect(0, 0, A4Width, height / pixelsPerPoint);
            canvas.DrawImage(image, source, destination, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            document.EndPage();
        }
        document.Close();
        return true;
    }
}
